using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using JsonIngestService.DataAccess;
using JsonIngestService.Models.Shared;

namespace JsonIngestService.Pipeline;

/// <summary>
/// Bulk-inserts all locations (and their sub-type records) for a file.
/// Returns a map of InDocumentID (JSON locationID value) → database IDENTITY.
/// </summary>
public sealed class LocationProcessor
{
    private readonly BulkCopyHelper _bulk;

    public LocationProcessor(BulkCopyHelper bulk) => _bulk = bulk;

    public async Task<Dictionary<string, long>> InsertAndMapAsync(
        IReadOnlyList<LocationModel> locations,
        long jsonFileId,
        SqlConnection conn,
        SqlTransaction tx,
        CancellationToken ct)
    {
        if (locations.Count == 0)
            return new Dictionary<string, long>(0);

        // Insert parent Location rows first; sub-types need the FK.
        var locationTable = BuildLocationTable(locations, jsonFileId);
        await _bulk.InsertAsync("cdm.Location", locationTable, conn, tx, ct);

        var idMap = await RecoverLocationIdsAsync(jsonFileId, conn, tx, ct);

        // Insert sub-type rows in parallel batches (no inter-dependency).
        var cellTable   = BuildCellTable(locations, idMap);
        var geoTable    = BuildGeoTable(locations, idMap);
        var postalTable = BuildPostalTable(locations, idMap);
        var wifiTable   = BuildWifiTable(locations, idMap);

        // SqlBulkCopy does not support concurrent operations on the same SqlConnection.
        // Run the four sub-type inserts sequentially.
        await _bulk.InsertAsync("cdm.CellLocation",    cellTable,   conn, tx, ct);
        await _bulk.InsertAsync("cdm.GeoLocation",     geoTable,    conn, tx, ct);
        await _bulk.InsertAsync("cdm.PostalAddress",   postalTable, conn, tx, ct);
        await _bulk.InsertAsync("cdm.WifiAccessPoint", wifiTable,   conn, tx, ct);

        // SpatialPoint is GEOGRAPHY — SqlBulkCopy cannot write to spatial columns.
        // Compute it in SQL after the bulk insert using geography::Point().
        await UpdateSpatialPointsAsync(jsonFileId, conn, tx, ct);

        return idMap;
    }

    // -------------------------------------------------------------------------
    private static DataTable BuildLocationTable(
        IReadOnlyList<LocationModel> locations, long jsonFileId)
    {
        var dt = new DataTable();
        dt.Columns.Add("JsonFile_ID",   typeof(long));
        dt.Columns.Add("InDocumentID",  typeof(string));
        dt.Columns.Add("FirstSeen",     typeof(DateTime));
        dt.Columns.Add("LastSeen",      typeof(DateTime));
        dt.Columns.Add("ValidFrom",     typeof(DateTime));
        dt.Columns.Add("ValidTo",       typeof(DateTime));
        dt.Columns.Add("EventCount",    typeof(int));

        foreach (var loc in locations)
            dt.Rows.Add(
                jsonFileId,
                loc.LocationID ?? string.Empty,
                ParseOrNull(loc.FirstSeen)    as object ?? DBNull.Value,
                ParseOrNull(loc.LastSeen)     as object ?? DBNull.Value,
                ParseDateOrNull(loc.ValidFrom) as object ?? DBNull.Value,
                ParseDateOrNull(loc.ValidTo)  as object ?? DBNull.Value,
                loc.EventCount                as object ?? DBNull.Value);

        return dt;
    }

    private static DataTable BuildCellTable(
        IReadOnlyList<LocationModel> locations, Dictionary<string, long> idMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Location_ID",       typeof(long));
        dt.Columns.Add("MCCStandardised",   typeof(string));
        dt.Columns.Add("MNCStandardised",   typeof(string));
        dt.Columns.Add("LACStandardised",   typeof(string));
        dt.Columns.Add("CellIDStandardised",typeof(string));
        dt.Columns.Add("CGIStandardised",   typeof(string));
        dt.Columns.Add("SACStandardised",   typeof(string));
        dt.Columns.Add("AzimuthDegrees",    typeof(string));
        dt.Columns.Add("RATStandardised",   typeof(string));
        dt.Columns.Add("ECellIDStandardised",typeof(string));
        dt.Columns.Add("ENodeBStandardised",typeof(string));
        dt.Columns.Add("ECGIStandardised",  typeof(string));
        dt.Columns.Add("TACStandardised",   typeof(string));
        dt.Columns.Add("BeamwidthRaw",      typeof(string));
        dt.Columns.Add("RadiatedPowerRaw",  typeof(string));
        dt.Columns.Add("AntennaHeightRaw",  typeof(string));
        dt.Columns.Add("RangeRaw",          typeof(string));
        dt.Columns.Add("RawData",           typeof(string));

        foreach (var loc in locations)
        {
            var cl = loc.CellLocation;
            if (cl is null || !idMap.TryGetValue(loc.LocationID ?? "", out var locId))
                continue;

            dt.Rows.Add(
                locId,
                cl.MCCStandardised    as object ?? DBNull.Value,
                cl.MNCStandardised    as object ?? DBNull.Value,
                cl.LACStandardised    as object ?? DBNull.Value,
                cl.CellIDStandardised as object ?? DBNull.Value,
                cl.CGIStandardised    as object ?? DBNull.Value,
                cl.SACStandardised    as object ?? DBNull.Value,
                cl.AzimuthDegrees     as object ?? DBNull.Value,
                cl.RATStandardised    as object ?? DBNull.Value,
                cl.ECellIDStandardised as object ?? DBNull.Value,
                cl.ENodeBStandardised as object ?? DBNull.Value,
                cl.ECGIStandardised   as object ?? DBNull.Value,
                cl.TACStandardised    as object ?? DBNull.Value,
                cl.BeamwidthRaw       as object ?? DBNull.Value,
                cl.RadiatedPowerRaw   as object ?? DBNull.Value,
                cl.AntennaHeightRaw   as object ?? DBNull.Value,
                cl.RangeRaw           as object ?? DBNull.Value,
                JsonSerializer.Serialize(new
                {
                    cl.MCCRaw, cl.MCCRawComposition, cl.MCCStandardisedComposition,
                    cl.MNCRaw, cl.MNCRawComposition, cl.MNCStandardisedComposition,
                    cl.LACRaw, cl.LACRawComposition, cl.LACStandardisedComposition,
                    cl.CellIDRaw, cl.CellIDRawComposition, cl.CellIDStandardisedComposition,
                    cl.CGIRaw, cl.CGIRawComposition, cl.CGIStandardisedComposition,
                    cl.SACRaw, cl.SACRawComposition, cl.SACStandardisedComposition,
                    cl.AzimuthRaw, cl.AzimuthRawComposition, cl.AzimuthDegreesComposition,
                    cl.RATRaw, cl.RATRawComposition, cl.RATStandardisedComposition,
                    cl.ECellIDRaw, cl.ECellIDRawComposition, cl.ECellIDStandardisedComposition,
                    cl.ENodeBRaw, cl.ENodeBRawComposition, cl.ENodeBStandardisedComposition,
                    cl.ECGIRaw, cl.ECGIRawComposition, cl.ECGIStandardisedComposition,
                    cl.TACRaw, cl.TACRawComposition, cl.TACStandardisedComposition
                })
            );
        }
        return dt;
    }

    private static DataTable BuildGeoTable(
        IReadOnlyList<LocationModel> locations, Dictionary<string, long> idMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Location_ID",           typeof(long));
        dt.Columns.Add("LatitudeWGS84",         typeof(double));
        dt.Columns.Add("LongitudeWGS84",        typeof(double));
        dt.Columns.Add("EastingOSGBMeterRef",   typeof(int));
        dt.Columns.Add("NorthingOSGBMeterRef",  typeof(int));
        // SpatialPoint is GEOGRAPHY — omitted from bulk insert; computed by
        // UpdateSpatialPointsAsync() after the bulk copy via geography::Point().
        dt.Columns.Add("RawData",               typeof(string));

        foreach (var loc in locations)
        {
            var geo = loc.GeoLocation;
            if (geo is null || !idMap.TryGetValue(loc.LocationID ?? "", out var locId))
                continue;

            dt.Rows.Add(
                locId,
                geo.LatitudeWGS84       as object ?? DBNull.Value,
                geo.LongitudeWGS84      as object ?? DBNull.Value,
                geo.EastingOSGBMeterRef  as object ?? DBNull.Value,
                geo.NorthingOSGBMeterRef as object ?? DBNull.Value,
                JsonSerializer.Serialize(new
                {
                    geo.LatitudeRaw, geo.LatitudeRawComposition, geo.LatitudeWGS84Composition,
                    geo.LongitudeRaw, geo.LongitudeRawComposition, geo.LongitudeWGS84Composition,
                    geo.EastingRaw, geo.EastingRawComposition, geo.EastingOSGBMeterRefComposition,
                    geo.NorthingRaw, geo.NorthingRawComposition, geo.NorthingOSGBMeterRefComposition
                }));
        }
        return dt;
    }

    private static DataTable BuildPostalTable(
        IReadOnlyList<LocationModel> locations, Dictionary<string, long> idMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Location_ID",               typeof(long));
        dt.Columns.Add("FullAddressStandardised",   typeof(string));
        dt.Columns.Add("BuildingNameRaw",           typeof(string));
        dt.Columns.Add("BuildingNumberRaw",         typeof(string));
        dt.Columns.Add("FlatNumberRaw",             typeof(string));
        dt.Columns.Add("StreetNameRaw",             typeof(string));
        dt.Columns.Add("CityRaw",                   typeof(string));
        dt.Columns.Add("PostcodeRaw",               typeof(string));
        dt.Columns.Add("PoBoxRaw",                  typeof(string));
        dt.Columns.Add("CountryRaw",                typeof(string));
        dt.Columns.Add("RawData",                   typeof(string));

        foreach (var loc in locations)
        {
            var pa = loc.PostalAddress;
            if (pa is null || !idMap.TryGetValue(loc.LocationID ?? "", out var locId))
                continue;

            dt.Rows.Add(
                locId,
                pa.FullAddressStandardised as object ?? DBNull.Value,
                pa.BuildingNameRaw         as object ?? DBNull.Value,
                pa.BuildingNumberRaw       as object ?? DBNull.Value,
                pa.FlatNumberRaw           as object ?? DBNull.Value,
                pa.StreetNameRaw           as object ?? DBNull.Value,
                pa.CityRaw                 as object ?? DBNull.Value,
                pa.PostcodeRaw             as object ?? DBNull.Value,
                pa.PoBoxRaw                as object ?? DBNull.Value,
                pa.CountryRaw              as object ?? DBNull.Value,
                JsonSerializer.Serialize(new
                {
                    pa.FullAddressRaw, pa.FullAddressRawComposition,
                    pa.FullAddressStandardisedComposition,
                    pa.BuildingNameRawComposition,
                    pa.StreetNameRawComposition
                }));
        }
        return dt;
    }

    private static DataTable BuildWifiTable(
        IReadOnlyList<LocationModel> locations, Dictionary<string, long> idMap)
    {
        var dt = new DataTable();
        dt.Columns.Add("Location_ID",           typeof(long));
        dt.Columns.Add("AccessPointIDRaw",      typeof(string));
        dt.Columns.Add("AccessPointNameRaw",    typeof(string));
        dt.Columns.Add("SSIDRaw",               typeof(string));

        foreach (var loc in locations)
        {
            var wifi = loc.WifiAccessPoint;
            if (wifi is null || !idMap.TryGetValue(loc.LocationID ?? "", out var locId))
                continue;

            dt.Rows.Add(
                locId,
                wifi.AccessPointIDRaw   as object ?? DBNull.Value,
                wifi.AccessPointNameRaw as object ?? DBNull.Value,
                wifi.SSIDRaw            as object ?? DBNull.Value);
        }
        return dt;
    }

    private static async Task<Dictionary<string, long>> RecoverLocationIdsAsync(
        long jsonFileId, SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        const string sql = @"
            SELECT InDocumentID, ID
            FROM cdm.Location
            WHERE JsonFile_ID = @JsonFileId";

        using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@JsonFileId", SqlDbType.BigInt).Value = jsonFileId;

        var map = new Dictionary<string, long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetString(0)] = reader.GetInt64(1);
        return map;
    }

    private static async Task UpdateSpatialPointsAsync(
        long jsonFileId, SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        const string sql = @"
            UPDATE g
            SET    g.SpatialPoint = geography::Point(g.LatitudeWGS84, g.LongitudeWGS84, 4326)
            FROM   cdm.GeoLocation g
            INNER JOIN cdm.Location l ON l.ID = g.Location_ID
            WHERE  l.JsonFile_ID = @JsonFileId
              AND  g.LatitudeWGS84  IS NOT NULL
              AND  g.LongitudeWGS84 IS NOT NULL";

        using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.Add("@JsonFileId", SqlDbType.BigInt).Value = jsonFileId;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DateTime? ParseOrNull(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;

    private static DateTime? ParseDateOrNull(string? s) =>
        DateOnly.TryParse(s, out var d) ? d.ToDateTime(TimeOnly.MinValue) : null;
}
