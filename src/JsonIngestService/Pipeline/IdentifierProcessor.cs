using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using JsonIngestService.DataAccess;
using JsonIngestService.Models.Shared;

namespace JsonIngestService.Pipeline;

/// <summary>
/// Bulk-inserts all identifiers for a file and returns a map of
/// InDocumentID (JSON identifierID value) → database IDENTITY.
/// </summary>
public sealed class IdentifierProcessor
{
    private readonly BulkCopyHelper _bulk;

    public IdentifierProcessor(BulkCopyHelper bulk) => _bulk = bulk;

    public async Task<Dictionary<string, long>> InsertAndMapAsync(
        IReadOnlyList<IdentifierModel> identifiers,
        long jsonFileId,
        SqlConnection conn,
        SqlTransaction tx,
        CancellationToken ct)
    {
        if (identifiers.Count == 0)
            return new Dictionary<string, long>(0);

        var table = BuildTable(identifiers, jsonFileId);
        await _bulk.InsertAsync("cdm.Identifier", table, conn, tx, ct);

        // Recover IDENTITY values via the UNIQUE constraint index on (JsonFile_ID, InDocumentID).
        return await RecoverIdsAsync(jsonFileId, conn, tx, ct);
    }

    private static DataTable BuildTable(IReadOnlyList<IdentifierModel> identifiers, long jsonFileId)
    {
        var dt = new DataTable();
        dt.Columns.Add("JsonFile_ID",           typeof(long));
        dt.Columns.Add("InDocumentID",          typeof(string));
        dt.Columns.Add("IdentifierRaw",         typeof(string));
        dt.Columns.Add("IdentifierStandardised",typeof(string));
        dt.Columns.Add("IdentifierTypeID",      typeof(string));
        dt.Columns.Add("IdentifierTypeName",    typeof(string));
        dt.Columns.Add("IdentifierSubType",     typeof(string));
        dt.Columns.Add("Description",           typeof(string));
        dt.Columns.Add("FirstSeen",             typeof(DateTime));
        dt.Columns.Add("LastSeen",              typeof(DateTime));
        dt.Columns.Add("ValidFrom",             typeof(DateTime)); // stored as DATE
        dt.Columns.Add("ValidTo",               typeof(DateTime));
        dt.Columns.Add("EventCount",            typeof(int));
        dt.Columns.Add("RawData",               typeof(string));

        foreach (var id in identifiers)
        {
            var rawData = JsonSerializer.Serialize(new
            {
                id.IdentifierRawComposition,
                id.IdentifierStandardisedComposition
            });

            dt.Rows.Add(
                jsonFileId,
                id.IdentifierID ?? string.Empty,
                id.IdentifierRaw ?? string.Empty,
                id.IdentifierStandardised      as object ?? DBNull.Value,
                id.IdentifierTypeID            ?? string.Empty,
                id.IdentifierTypeName          ?? string.Empty,
                id.IdentifierSubType           as object ?? DBNull.Value,
                id.Description                 as object ?? DBNull.Value,
                ParseOrNull(id.FirstSeen)      as object ?? DBNull.Value,
                ParseOrNull(id.LastSeen)       as object ?? DBNull.Value,
                ParseDateOrNull(id.ValidFrom)  as object ?? DBNull.Value,
                ParseDateOrNull(id.ValidTo)    as object ?? DBNull.Value,
                id.EventCount                  as object ?? DBNull.Value,
                rawData
            );
        }

        return dt;
    }

    private static async Task<Dictionary<string, long>> RecoverIdsAsync(
        long jsonFileId, SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        // IX_Identifier_JsonFile covers this query — narrow index seek.
        const string sql = @"
            SELECT InDocumentID, ID
            FROM cdm.Identifier
            WHERE JsonFile_ID = @JsonFileId";

        using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@JsonFileId", SqlDbType.BigInt).Value = jsonFileId;

        var map = new Dictionary<string, long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetString(0)] = reader.GetInt64(1);

        return map;
    }

    private static DateTime? ParseOrNull(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;

    private static DateTime? ParseDateOrNull(string? s) =>
        DateOnly.TryParse(s, out var d) ? d.ToDateTime(TimeOnly.MinValue) : null;
}
