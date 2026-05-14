using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using JsonIngestService.DataAccess;
using JsonIngestService.Models.Events;

namespace JsonIngestService.Pipeline;

/// <summary>
/// Processes the event path (RepresentationType = Standardised Telephony/Access Events).
/// Inserts: Event → EventAttribute → EventParty → PartyAttribute → EventPartyIdentifier.
/// Uses IngestSeqNum to recover EventParty IDENTITY values without a temp-table staging step.
/// </summary>
public sealed class EventPathProcessor
{
    private readonly BulkCopyHelper _bulk;

    public EventPathProcessor(BulkCopyHelper bulk) => _bulk = bulk;

    public async Task ProcessAsync(
        IReadOnlyList<EventModel> events,
        long jsonFileId,
        Dictionary<string, long> identifierIdMap,
        Dictionary<string, long> locationIdMap,
        SqlConnection conn,
        SqlTransaction tx,
        CancellationToken ct)
    {
        if (events.Count == 0) return;

        // ---- 1. Events -------------------------------------------------------
        var eventTable = BuildEventTable(events, jsonFileId);
        await _bulk.InsertAsync("cdm.Event", eventTable, conn, tx, ct);

        // Recover: IX_Event_JsonFile seek; ProcessedEventID assumed unique per file.
        var eventIdMap = await RecoverEventIdsAsync(jsonFileId, conn, tx, ct);

        // ---- 2. EventAttributes (key-value rows) ----------------------------
        var attrTable = BuildEventAttributeTable(events, eventIdMap);
        await _bulk.InsertAsync("cdm.EventAttribute", attrTable, conn, tx, ct);

        // ---- 3. EventParties ------------------------------------------------
        // Accumulate junction data while building party rows.
        var junctionLinks  = new List<(int seqNum, string inDocIdentifierId)>();
        var partyAttrRows  = new List<(int seqNum, string key, string? value)>();

        var partyTable = BuildEventPartyTable(
            events, eventIdMap, locationIdMap, junctionLinks, partyAttrRows);

        await _bulk.InsertAsync("cdm.EventParty", partyTable, conn, tx, ct);

        // Recover EventParty IDs by IngestSeqNum (scope: all parties in this file).
        var partyIdMap = await RecoverEventPartyIdsAsync(jsonFileId, conn, tx, ct);

        // ---- 4. EventPartyIdentifier junction --------------------------------
        var junctionTable = BuildJunctionTable(junctionLinks, partyIdMap, identifierIdMap);
        await _bulk.InsertAsync("cdm.EventPartyIdentifier", junctionTable, conn, tx, ct);

        // ---- 5. PartyAttributes ---------------------------------------------
        var partyAttrTable = BuildPartyAttributeTable(partyAttrRows, partyIdMap);
        await _bulk.InsertAsync("cdm.PartyAttribute", partyAttrTable, conn, tx, ct);
    }

    // -------------------------------------------------------------------------
    private static DataTable BuildEventTable(
        IReadOnlyList<EventModel> events, long jsonFileId)
    {
        var dt = new DataTable();
        dt.Columns.Add("JsonFile_ID",       typeof(long));
        dt.Columns.Add("ProcessedEventID",  typeof(string));
        dt.Columns.Add("SourceEventNumber", typeof(string));
        dt.Columns.Add("EventType",         typeof(string));
        dt.Columns.Add("EventSuperType",    typeof(string));
        dt.Columns.Add("RawData",           typeof(string));

        foreach (var e in events)
            dt.Rows.Add(
                jsonFileId,
                e.ProcessedEventID  as object ?? DBNull.Value,
                e.SourceEventNumber as object ?? DBNull.Value,
                e.EventType         ?? "Unknown",
                e.EventSuperType    ?? "Unknown",
                JsonSerializer.Serialize(new
                {
                    e.EventTypeRaw,
                    e.EventTypeRawComposition
                }));

        return dt;
    }

    private static DataTable BuildEventAttributeTable(
        IReadOnlyList<EventModel> events,
        Dictionary<string, long> eventIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Event_ID",  typeof(long));
        dt.Columns.Add("AttrKey",   typeof(string));
        dt.Columns.Add("AttrValue", typeof(string));

        foreach (var e in events)
        {
            if (e.EventAttributes is null || e.EventAttributes.Count == 0) continue;
            if (!eventIdMap.TryGetValue(e.ProcessedEventID ?? "", out var eventId)) continue;

            foreach (var attr in e.EventAttributes)
                dt.Rows.Add(
                    eventId,
                    attr.Key    ?? string.Empty,
                    attr.Value  as object ?? DBNull.Value);
        }
        return dt;
    }

    private static DataTable BuildEventPartyTable(
        IReadOnlyList<EventModel> events,
        Dictionary<string, long> eventIdMap,
        Dictionary<string, long> locationIdMap,
        List<(int seqNum, string inDocIdentifierId)> junctionAccumulator,
        List<(int seqNum, string key, string? value)> attrAccumulator)
    {
        var dt = new DataTable();
        dt.Columns.Add("Event_ID",              typeof(long));
        dt.Columns.Add("RoleStandardised",      typeof(string));
        dt.Columns.Add("StartDateTimeISO8601",  typeof(DateTime));
        dt.Columns.Add("EndDateTimeISO8601",    typeof(DateTime));
        dt.Columns.Add("DurationSeconds",       typeof(int));
        dt.Columns.Add("DataReceivedBytes",     typeof(long));
        dt.Columns.Add("DataSentBytes",         typeof(long));
        dt.Columns.Add("StartLocation_ID",      typeof(long));
        dt.Columns.Add("EndLocation_ID",        typeof(long));
        dt.Columns.Add("IngestSeqNum",          typeof(int));
        dt.Columns.Add("RawData",               typeof(string));

        int seq = 0;
        foreach (var e in events)
        {
            if (e.EventParties is null) continue;
            if (!eventIdMap.TryGetValue(e.ProcessedEventID ?? "", out var eventId)) continue;

            foreach (var ep in e.EventParties)
            {
                int currentSeq = ++seq;

                // Accumulate junction links (resolved later using partyIdMap)
                foreach (var identRef in ep.PartyIdentifierIDs ?? [])
                    junctionAccumulator.Add((currentSeq, identRef));

                // Accumulate attribute rows
                foreach (var attr in ep.PartyAttributes ?? [])
                    attrAccumulator.Add((currentSeq, attr.Key ?? string.Empty, attr.Value));

                locationIdMap.TryGetValue(ep.StartLocationID ?? "", out var startLocId);
                locationIdMap.TryGetValue(ep.EndLocationID   ?? "", out var endLocId);

                dt.Rows.Add(
                    eventId,
                    ep.RoleStandardised ?? "Unknown",
                    ParseOrNull(ep.StartDateTimeISO8601) as object ?? DBNull.Value,
                    ParseOrNull(ep.EndDateTimeISO8601)   as object ?? DBNull.Value,
                    ep.DurationSeconds      as object ?? DBNull.Value,
                    ep.DataReceivedBytes    as object ?? DBNull.Value,
                    ep.DataSentBytes        as object ?? DBNull.Value,
                    startLocId > 0          ? startLocId : (object)DBNull.Value,
                    endLocId   > 0          ? endLocId   : (object)DBNull.Value,
                    currentSeq,
                    JsonSerializer.Serialize(new
                    {
                        ep.StartDateTimeRaw, ep.StartDateTimeRawComposition, ep.StartDateTimeISO8601Composition,
                        ep.EndDateTimeRaw,   ep.EndDateTimeRawComposition,   ep.EndDateTimeISO8601Composition,
                        ep.DurationRaw,      ep.DurationRawComposition,      ep.DurationSecondsComposition,
                        ep.DataReceivedRaw,  ep.DataReceivedRawComposition,  ep.DataReceivedBytesComposition,
                        ep.DataSentRaw,      ep.DataSentRawComposition,      ep.DataSentBytesComposition,
                        ep.RoleRaw,          ep.RoleRawComposition,          ep.RoleStandardisedComposition
                    }));
            }
        }
        return dt;
    }

    private static DataTable BuildJunctionTable(
        List<(int seqNum, string inDocIdentifierId)> links,
        Dictionary<int, long> partyIdMap,
        Dictionary<string, long> identifierIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("EventParty_ID", typeof(long));
        dt.Columns.Add("Identifier_ID", typeof(long));

        var seen = new HashSet<(long, long)>();
        foreach (var (seq, inDocId) in links)
        {
            if (!partyIdMap.TryGetValue(seq, out var partyId)) continue;
            if (!identifierIdMap.TryGetValue(inDocId, out var identId)) continue;
            if (seen.Add((partyId, identId)))
                dt.Rows.Add(partyId, identId);
        }
        return dt;
    }

    private static DataTable BuildPartyAttributeTable(
        List<(int seqNum, string key, string? value)> rows,
        Dictionary<int, long> partyIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("EventParty_ID", typeof(long));
        dt.Columns.Add("AttrKey",       typeof(string));
        dt.Columns.Add("AttrValue",     typeof(string));

        foreach (var (seq, key, value) in rows)
        {
            if (!partyIdMap.TryGetValue(seq, out var partyId)) continue;
            dt.Rows.Add(partyId, key, value as object ?? DBNull.Value);
        }
        return dt;
    }

    // ---- ID recovery --------------------------------------------------------

    private static async Task<Dictionary<string, long>> RecoverEventIdsAsync(
        long jsonFileId, SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        const string sql = @"
            SELECT ProcessedEventID, ID
            FROM cdm.Event
            WHERE JsonFile_ID = @JsonFileId
              AND ProcessedEventID IS NOT NULL";

        using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@JsonFileId", SqlDbType.BigInt).Value = jsonFileId;

        var map = new Dictionary<string, long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetString(0)] = reader.GetInt64(1);
        return map;
    }

    private static async Task<Dictionary<int, long>> RecoverEventPartyIdsAsync(
        long jsonFileId, SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        // Uses IX_EventParty_IngestSeqNum (Event_ID, IngestSeqNum) INCLUDE (ID)
        // scoped to this file via IX_Event_JsonFile.
        const string sql = @"
            SELECT ep.IngestSeqNum, ep.ID
            FROM cdm.EventParty ep
            INNER JOIN cdm.Event e ON e.ID = ep.Event_ID
            WHERE e.JsonFile_ID = @JsonFileId";

        using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@JsonFileId", SqlDbType.BigInt).Value = jsonFileId;

        var map = new Dictionary<int, long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetInt32(0)] = reader.GetInt64(1);
        return map;
    }

    private static DateTime? ParseOrNull(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;
}
