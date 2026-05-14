using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using JsonIngestService.Models.Root;

namespace JsonIngestService.Pipeline;

/// <summary>
/// Inserts the top-level cdm.JsonFile row and returns its generated IDENTITY value.
/// All CSP disclosure metadata and results-file metadata are folded into this
/// single row as they are 1:1 with the file.
/// </summary>
public sealed class JsonFileInserter
{
    private const string Sql = @"
        INSERT INTO cdm.JsonFile (
            Document_ID, SchemaVersion, RepresentationType,
            ProcessingEngine, ProcessingEngineVersion,
            ProcessingRequestID, ProcessingCompletionTimestamp, ProcessedFileID,
            CspOrganisationID, CspOrganisationName,
            CspDisclosureSystemID, CspDisclosureSystemName,
            CspDisclosureProductID, CspDisclosureProductName,
            CspDisclosureProductSchemaID, CspDisclosureProductSchemaName,
            CspDisclosureProductSchemaVersion,
            OriginalRequestingAuthority, AuthorisationIdentifier, AuthorityRequestID,
            CspRequestCreatedOn, CdRequestDescription,
            CspResultsFileID, CspResultsFileName, CspResultsFileCreatedOn,
            CspResultsFileFormat, CspResultsDescription, CspResultsFileSizeBytes,
            RequestParameters, HashDetails
        )
        OUTPUT INSERTED.ID
        VALUES (
            @DocumentId, @SchemaVersion, @RepresentationType,
            @ProcessingEngine, @ProcessingEngineVersion,
            @ProcessingRequestID, @ProcessingCompletionTimestamp, @ProcessedFileID,
            @CspOrganisationID, @CspOrganisationName,
            @CspDisclosureSystemID, @CspDisclosureSystemName,
            @CspDisclosureProductID, @CspDisclosureProductName,
            @CspDisclosureProductSchemaID, @CspDisclosureProductSchemaName,
            @CspDisclosureProductSchemaVersion,
            @OriginalRequestingAuthority, @AuthorisationIdentifier, @AuthorityRequestID,
            @CspRequestCreatedOn, @CdRequestDescription,
            @CspResultsFileID, @CspResultsFileName, @CspResultsFileCreatedOn,
            @CspResultsFileFormat, @CspResultsDescription, @CspResultsFileSizeBytes,
            @RequestParameters, @HashDetails
        )";

    public async Task<long> InsertAsync(
        StandardisedRepresentation rep,
        long documentId,
        SqlConnection conn,
        SqlTransaction tx,
        CancellationToken ct)
    {
        var csp = rep.CspDisclosureRepresentation;
        var rf  = csp?.CspResultsFile;

        using var cmd = new SqlCommand(Sql, conn, tx);

        Add(cmd, "@DocumentId",                     SqlDbType.BigInt,    documentId);
        Add(cmd, "@SchemaVersion",                  SqlDbType.VarChar,   rep.SchemaVersion);
        Add(cmd, "@RepresentationType",             SqlDbType.VarChar,   rep.RepresentationType ?? "Unknown");
        Add(cmd, "@ProcessingEngine",               SqlDbType.VarChar,   rep.ProcessingEngine);
        Add(cmd, "@ProcessingEngineVersion",        SqlDbType.VarChar,   rep.ProcessingEngineVersion);
        Add(cmd, "@ProcessingRequestID",            SqlDbType.VarChar,   rep.ProcessingRequestID);
        Add(cmd, "@ProcessingCompletionTimestamp",  SqlDbType.DateTime2, ParseDateTimeOrNull(rep.ProcessingCompletionTimestamp));
        Add(cmd, "@ProcessedFileID",                SqlDbType.VarChar,   rep.ProcessedFileID);

        Add(cmd, "@CspOrganisationID",              SqlDbType.VarChar,   csp?.CspOrganisationID);
        Add(cmd, "@CspOrganisationName",            SqlDbType.NVarChar,  csp?.CspOrganisationName);
        Add(cmd, "@CspDisclosureSystemID",          SqlDbType.VarChar,   csp?.CspDisclosureSystemID);
        Add(cmd, "@CspDisclosureSystemName",        SqlDbType.NVarChar,  csp?.CspDisclosureSystemName);
        Add(cmd, "@CspDisclosureProductID",         SqlDbType.VarChar,   csp?.CspDisclosureProductID);
        Add(cmd, "@CspDisclosureProductName",       SqlDbType.NVarChar,  csp?.CspDisclosureProductName);
        Add(cmd, "@CspDisclosureProductSchemaID",   SqlDbType.VarChar,   csp?.CspDisclosureProductSchemaID);
        Add(cmd, "@CspDisclosureProductSchemaName", SqlDbType.NVarChar,  csp?.CspDisclosureProductSchemaName);
        Add(cmd, "@CspDisclosureProductSchemaVersion", SqlDbType.VarChar, csp?.CspDisclosureProductSchemaVersion);
        Add(cmd, "@OriginalRequestingAuthority",    SqlDbType.VarChar,   csp?.OriginalRequestingAuthority);
        Add(cmd, "@AuthorisationIdentifier",        SqlDbType.NVarChar,  csp?.AuthorisationIdentifier);
        Add(cmd, "@AuthorityRequestID",             SqlDbType.NVarChar,  csp?.AuthorityRequestID);
        Add(cmd, "@CspRequestCreatedOn",            SqlDbType.DateTime2, ParseDateTimeOrNull(csp?.CspRequestCreatedOn));
        Add(cmd, "@CdRequestDescription",           SqlDbType.NVarChar,  csp?.CdRequestDescription);

        Add(cmd, "@CspResultsFileID",               SqlDbType.VarChar,   rf?.CspResultsFileID);
        Add(cmd, "@CspResultsFileName",             SqlDbType.NVarChar,  rf?.CspResultsFileName);
        Add(cmd, "@CspResultsFileCreatedOn",        SqlDbType.DateTime2, ParseDateTimeOrNull(rf?.CspResultsFileCreatedOn));
        Add(cmd, "@CspResultsFileFormat",           SqlDbType.VarChar,   rf?.CspResultsFileFormat);
        Add(cmd, "@CspResultsDescription",          SqlDbType.NVarChar,  rf?.CspResultsDescription);
        Add(cmd, "@CspResultsFileSizeBytes",        SqlDbType.BigInt,
            long.TryParse(rf?.CspResultsFileSize, out var sz) ? sz : (object)DBNull.Value);

        // Pack infrequently-queried arrays as JSON blobs
        Add(cmd, "@RequestParameters", SqlDbType.NVarChar,
            csp?.RequestParameters is { Count: > 0 }
                ? JsonSerializer.Serialize(csp.RequestParameters)
                : (object)DBNull.Value);

        Add(cmd, "@HashDetails", SqlDbType.NVarChar,
            rf?.HashDetails is { Count: > 0 }
                ? JsonSerializer.Serialize(rf.HashDetails)
                : (object)DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (long)result!;
    }

    private static void Add(SqlCommand cmd, string name, SqlDbType type, object? value)
    {
        var p = cmd.Parameters.Add(name, type);
        p.Value = value ?? DBNull.Value;
    }

    private static DateTime? ParseDateTimeOrNull(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;
}
