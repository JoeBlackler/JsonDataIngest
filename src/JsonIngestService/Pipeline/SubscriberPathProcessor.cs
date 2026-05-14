using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using JsonIngestService.DataAccess;
using JsonIngestService.Models.Subscribers;

namespace JsonIngestService.Pipeline;

/// <summary>
/// Processes the subscriber path (RepresentationType = 'Standardised Subscriber').
/// Inserts: Subscriber → SubscriberAttribute → SubscriberIdentifier → SubscriberLocation
///        → Subscription → SubscriptionAttribute → SubscriptionIdentifier → SubscriptionLocation.
/// </summary>
public sealed class SubscriberPathProcessor
{
    private readonly BulkCopyHelper _bulk;

    public SubscriberPathProcessor(BulkCopyHelper bulk) => _bulk = bulk;

    public async Task ProcessAsync(
        IReadOnlyList<SubscriberModel> subscribers,
        long jsonFileId,
        Dictionary<string, long> identifierIdMap,
        Dictionary<string, long> locationIdMap,
        SqlConnection conn,
        SqlTransaction tx,
        CancellationToken ct)
    {
        if (subscribers.Count == 0) return;

        // ---- 1. Subscribers ------------------------------------------------
        var subTable = BuildSubscriberTable(subscribers, jsonFileId);
        await _bulk.InsertAsync("cdm.Subscriber", subTable, conn, tx, ct);

        var subscriberIdMap = await RecoverSubscriberIdsAsync(jsonFileId, conn, tx, ct);

        // ---- 2. SubscriberAttributes ----------------------------------------
        var subAttrTable = BuildSubscriberAttributeTable(subscribers, subscriberIdMap);
        await _bulk.InsertAsync("cdm.SubscriberAttribute", subAttrTable, conn, tx, ct);

        // ---- 3. SubscriberIdentifier junction --------------------------------
        var subIdentTable = BuildSubscriberIdentifierTable(subscribers, subscriberIdMap, identifierIdMap);
        await _bulk.InsertAsync("cdm.SubscriberIdentifier", subIdentTable, conn, tx, ct);

        // ---- 4. SubscriberLocation ------------------------------------------
        var subLocTable = BuildSubscriberLocationTable(subscribers, subscriberIdMap, locationIdMap);
        await _bulk.InsertAsync("cdm.SubscriberLocation", subLocTable, conn, tx, ct);

        // ---- 5. Subscriptions -----------------------------------------------
        var subscriptionTable = BuildSubscriptionTable(subscribers, subscriberIdMap);
        await _bulk.InsertAsync("cdm.Subscription", subscriptionTable, conn, tx, ct);

        // Recover subscription IDs: scope by subscriber ID set for this file.
        var subscriptionIdMap = await RecoverSubscriptionIdsAsync(
            subscriberIdMap.Values, conn, tx, ct);

        // ---- 6. SubscriptionAttributes, SubscriptionIdentifier, SubscriptionLocation ----
        var subsAttrTable  = BuildSubscriptionAttributeTable(subscribers, subscriberIdMap, subscriptionIdMap);
        var subsIdentTable = BuildSubscriptionIdentifierTable(subscribers, subscriberIdMap, subscriptionIdMap, identifierIdMap);
        var subsLocTable   = BuildSubscriptionLocationTable(subscribers, subscriberIdMap, subscriptionIdMap, locationIdMap);

        await _bulk.InsertAsync("cdm.SubscriptionAttribute",  subsAttrTable,  conn, tx, ct);
        await _bulk.InsertAsync("cdm.SubscriptionIdentifier", subsIdentTable, conn, tx, ct);
        await _bulk.InsertAsync("cdm.SubscriptionLocation",   subsLocTable,   conn, tx, ct);
    }

    // ---- Builders -----------------------------------------------------------

    private static DataTable BuildSubscriberTable(
        IReadOnlyList<SubscriberModel> subs, long jsonFileId)
    {
        var dt = new DataTable();
        dt.Columns.Add("JsonFile_ID",               typeof(long));
        dt.Columns.Add("InDocumentID",              typeof(string));
        dt.Columns.Add("SubscriberNameRaw",         typeof(string));
        dt.Columns.Add("SubscriberNameStandardised",typeof(string));
        dt.Columns.Add("DateOfBirth",               typeof(DateTime));
        dt.Columns.Add("GenderRaw",                 typeof(string));
        dt.Columns.Add("SalutationRaw",             typeof(string));
        dt.Columns.Add("FirstNameRaw",              typeof(string));
        dt.Columns.Add("SecondNameRaw",             typeof(string));
        dt.Columns.Add("MiddleNamesRaw",            typeof(string));
        dt.Columns.Add("SurnameRaw",                typeof(string));
        dt.Columns.Add("OrganisationNameRaw",       typeof(string));
        dt.Columns.Add("ProfessionRaw",             typeof(string));
        dt.Columns.Add("RawData",                   typeof(string));

        foreach (var s in subs)
            dt.Rows.Add(
                jsonFileId,
                s.SubscriberID          ?? string.Empty,
                s.SubscriberNameRaw     ?? string.Empty,
                s.SubscriberNameStandardised as object ?? DBNull.Value,
                ParseDateOrNull(s.DateOfBirth) as object ?? DBNull.Value,
                s.GenderRaw             as object ?? DBNull.Value,
                s.SalutationRaw         as object ?? DBNull.Value,
                s.FirstNameRaw          as object ?? DBNull.Value,
                s.SecondNameRaw         as object ?? DBNull.Value,
                s.MiddleNamesRaw        as object ?? DBNull.Value,
                s.SurnameRaw            as object ?? DBNull.Value,
                s.OrganisationNameRaw   as object ?? DBNull.Value,
                s.ProfessionRaw         as object ?? DBNull.Value,
                JsonSerializer.Serialize(new
                {
                    s.SubscriberNameRawComposition,
                    s.SubscriberNameStandardisedComposition,
                    s.DateOfBirthRaw,
                    s.DateOfBirthRawComposition,
                    s.DateOfBirthComposition
                }));

        return dt;
    }

    private static DataTable BuildSubscriberAttributeTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscriber_ID", typeof(long));
        dt.Columns.Add("AttrKey",       typeof(string));
        dt.Columns.Add("AttrValue",     typeof(string));

        foreach (var s in subs)
        {
            if (s.SubscriberAttributes is null) continue;
            if (!subscriberIdMap.TryGetValue(s.SubscriberID ?? "", out var subId)) continue;
            foreach (var a in s.SubscriberAttributes)
                dt.Rows.Add(subId, a.Key ?? string.Empty, a.Value as object ?? DBNull.Value);
        }
        return dt;
    }

    private static DataTable BuildSubscriberIdentifierTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap,
        Dictionary<string, long> identifierIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscriber_ID", typeof(long));
        dt.Columns.Add("Identifier_ID", typeof(long));

        var seen = new HashSet<(long, long)>();
        foreach (var s in subs)
        {
            if (!subscriberIdMap.TryGetValue(s.SubscriberID ?? "", out var subId)) continue;
            foreach (var idRef in s.ContactIdentifierIDs ?? [])
            {
                if (!identifierIdMap.TryGetValue(idRef, out var identId)) continue;
                if (seen.Add((subId, identId)))
                    dt.Rows.Add(subId, identId);
            }
        }
        return dt;
    }

    private static DataTable BuildSubscriberLocationTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap,
        Dictionary<string, long> locationIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscriber_ID", typeof(long));
        dt.Columns.Add("Location_ID",   typeof(long));
        dt.Columns.Add("StartDate",     typeof(DateTime));
        dt.Columns.Add("EndDate",       typeof(DateTime));
        dt.Columns.Add("Role",          typeof(string));

        foreach (var s in subs)
        {
            if (!subscriberIdMap.TryGetValue(s.SubscriberID ?? "", out var subId)) continue;
            foreach (var sl in s.SubscriberLocations ?? [])
            {
                if (!locationIdMap.TryGetValue(sl.LocationID ?? "", out var locId)) continue;
                dt.Rows.Add(
                    subId, locId,
                    ParseDateOrNull(sl.StartDate) as object ?? DBNull.Value,
                    ParseDateOrNull(sl.EndDate)   as object ?? DBNull.Value,
                    sl.Role as object ?? DBNull.Value);
            }
        }
        return dt;
    }

    private static DataTable BuildSubscriptionTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscriber_ID",                 typeof(long));
        dt.Columns.Add("InDocumentID",                  typeof(string));
        dt.Columns.Add("SubscriptionTypeRaw",           typeof(string));
        dt.Columns.Add("SubscriptionActivationDate",    typeof(DateTime));
        dt.Columns.Add("SubscriptionDeactivationDate",  typeof(DateTime));
        dt.Columns.Add("SubscriptionDeactivationReason",typeof(string));
        dt.Columns.Add("RawData",                       typeof(string));

        foreach (var s in subs)
        {
            if (!subscriberIdMap.TryGetValue(s.SubscriberID ?? "", out var subId)) continue;
            foreach (var sub in s.Subscriptions ?? [])
                dt.Rows.Add(
                    subId,
                    sub.SubscriptionID              ?? string.Empty,
                    sub.SubscriptionTypeRaw         as object ?? DBNull.Value,
                    ParseDateOrNull(sub.SubscriptionActivationDate)   as object ?? DBNull.Value,
                    ParseDateOrNull(sub.SubscriptionDeactivationDate) as object ?? DBNull.Value,
                    sub.SubscriptionDeactivationReasonRaw             as object ?? DBNull.Value,
                    JsonSerializer.Serialize(new
                    {
                        sub.SubscriptionActivationDateRaw,
                        sub.SubscriptionActivationDateRawComposition,
                        sub.SubscriptionActivationDateComposition,
                        sub.SubscriptionDeactivationDateRaw,
                        sub.SubscriptionDeactivationDateRawComposition,
                        sub.SubscriptionDeactivationDateComposition
                    }));
        }
        return dt;
    }

    private static DataTable BuildSubscriptionAttributeTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap,
        Dictionary<(string subId, string subsId), long> subscriptionIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscription_ID",   typeof(long));
        dt.Columns.Add("AttrKey",           typeof(string));
        dt.Columns.Add("AttrValue",         typeof(string));

        foreach (var s in subs)
        {
            if (!subscriberIdMap.ContainsKey(s.SubscriberID ?? "")) continue;
            foreach (var sub in s.Subscriptions ?? [])
            {
                if (!subscriptionIdMap.TryGetValue((s.SubscriberID!, sub.SubscriptionID ?? ""), out var subId)) continue;
                foreach (var a in sub.SubscriptionAttributes ?? [])
                    dt.Rows.Add(subId, a.Key ?? string.Empty, a.Value as object ?? DBNull.Value);
            }
        }
        return dt;
    }

    private static DataTable BuildSubscriptionIdentifierTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap,
        Dictionary<(string subId, string subsId), long> subscriptionIdMap,
        Dictionary<string, long> identifierIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscription_ID",   typeof(long));
        dt.Columns.Add("Identifier_ID",     typeof(long));

        var seen = new HashSet<(long, long)>();
        foreach (var s in subs)
        {
            if (!subscriberIdMap.ContainsKey(s.SubscriberID ?? "")) continue;
            foreach (var sub in s.Subscriptions ?? [])
            {
                if (!subscriptionIdMap.TryGetValue((s.SubscriberID!, sub.SubscriptionID ?? ""), out var subsDbId)) continue;
                foreach (var idRef in sub.SubscriptionIdentifierIDs ?? [])
                {
                    if (!identifierIdMap.TryGetValue(idRef, out var identId)) continue;
                    if (seen.Add((subsDbId, identId)))
                        dt.Rows.Add(subsDbId, identId);
                }
            }
        }
        return dt;
    }

    private static DataTable BuildSubscriptionLocationTable(
        IReadOnlyList<SubscriberModel> subs,
        Dictionary<string, long> subscriberIdMap,
        Dictionary<(string subId, string subsId), long> subscriptionIdMap,
        Dictionary<string, long> locationIdMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Subscription_ID",   typeof(long));
        dt.Columns.Add("Location_ID",       typeof(long));
        dt.Columns.Add("StartDatetime",     typeof(DateTime));
        dt.Columns.Add("EndDatetime",       typeof(DateTime));
        dt.Columns.Add("Role",              typeof(string));

        foreach (var s in subs)
        {
            if (!subscriberIdMap.ContainsKey(s.SubscriberID ?? "")) continue;
            foreach (var sub in s.Subscriptions ?? [])
            {
                if (!subscriptionIdMap.TryGetValue((s.SubscriberID!, sub.SubscriptionID ?? ""), out var subsDbId)) continue;
                foreach (var sl in sub.SubscriptionLocations ?? [])
                {
                    if (!locationIdMap.TryGetValue(sl.LocationID ?? "", out var locId)) continue;
                    dt.Rows.Add(
                        subsDbId, locId,
                        ParseOrNull(sl.StartDate) as object ?? DBNull.Value,
                        ParseOrNull(sl.EndDate)   as object ?? DBNull.Value,
                        sl.Role as object ?? DBNull.Value);
                }
            }
        }
        return dt;
    }

    // ---- ID recovery --------------------------------------------------------

    private static async Task<Dictionary<string, long>> RecoverSubscriberIdsAsync(
        long jsonFileId, SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        const string sql = @"
            SELECT InDocumentID, ID
            FROM cdm.Subscriber
            WHERE JsonFile_ID = @JsonFileId";

        using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@JsonFileId", SqlDbType.BigInt).Value = jsonFileId;

        var map = new Dictionary<string, long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetString(0)] = reader.GetInt64(1);
        return map;
    }

    private static async Task<Dictionary<(string subId, string subsId), long>> RecoverSubscriptionIdsAsync(
        IEnumerable<long> subscriberDbIds,
        SqlConnection conn,
        SqlTransaction tx,
        CancellationToken ct)
    {
        // Pass subscriber IDs via a TVP (cdm.BigIntList) — no dynamic SQL,
        // no injection risk, and the query plan is stable regardless of count.
        var tvp = new DataTable();
        tvp.Columns.Add("ID", typeof(long));
        foreach (var id in subscriberDbIds)
            tvp.Rows.Add(id);

        const string sql = @"
            SELECT s.InDocumentID, sub.InDocumentID, sub.ID
            FROM cdm.Subscription sub
            INNER JOIN cdm.Subscriber s ON s.ID = sub.Subscriber_ID
            INNER JOIN @SubscriberIds  ids ON ids.ID = sub.Subscriber_ID";

        using var cmd = new SqlCommand(sql, conn, tx);
        var param = cmd.Parameters.Add("@SubscriberIds", SqlDbType.Structured);
        param.TypeName = "cdm.BigIntList";
        param.Value = tvp;

        var map = new Dictionary<(string, string), long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[(reader.GetString(0), reader.GetString(1))] = reader.GetInt64(2);
        return map;
    }

    private static DateTime? ParseOrNull(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;

    private static DateTime? ParseDateOrNull(string? s) =>
        DateOnly.TryParse(s, out var d) ? d.ToDateTime(TimeOnly.MinValue) : null;
}
