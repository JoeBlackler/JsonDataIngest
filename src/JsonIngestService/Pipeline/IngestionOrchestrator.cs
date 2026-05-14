using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using JsonIngestService.DataAccess;
using JsonIngestService.Models.Root;

namespace JsonIngestService.Pipeline;

/// <summary>
/// Top-level ingestion orchestrator.
/// Owns the transaction; delegates to specialised processors for each entity group.
/// </summary>
public sealed class IngestionOrchestrator
{
    private readonly IDbConnectionFactory _connFactory;
    private readonly JsonFileInserter _fileInserter;
    private readonly IdentifierProcessor _identifierProcessor;
    private readonly LocationProcessor _locationProcessor;
    private readonly EventPathProcessor _eventProcessor;
    private readonly SubscriberPathProcessor _subscriberProcessor;
    private readonly ILogger<IngestionOrchestrator> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip
    };

    public IngestionOrchestrator(
        IDbConnectionFactory connFactory,
        JsonFileInserter fileInserter,
        IdentifierProcessor identifierProcessor,
        LocationProcessor locationProcessor,
        EventPathProcessor eventProcessor,
        SubscriberPathProcessor subscriberProcessor,
        ILogger<IngestionOrchestrator> logger)
    {
        _connFactory          = connFactory;
        _fileInserter         = fileInserter;
        _identifierProcessor  = identifierProcessor;
        _locationProcessor    = locationProcessor;
        _eventProcessor       = eventProcessor;
        _subscriberProcessor  = subscriberProcessor;
        _logger               = logger;
    }

    /// <summary>
    /// Parses, validates, and persists a single JSON file within one database transaction.
    /// </summary>
    /// <param name="jsonStream">Raw JSON stream (file contents).</param>
    /// <param name="documentId">The existing dbo.Document.ID this file belongs to.</param>
    public async Task<IngestionResult> IngestAsync(
        Stream jsonStream,
        long documentId,
        CancellationToken ct)
    {
        JsonRoot root;
        try
        {
            root = await JsonSerializer.DeserializeAsync<JsonRoot>(jsonStream, _jsonOptions, ct)
                   ?? throw new InvalidOperationException("JSON deserialisation returned null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON for Document {DocumentId}", documentId);
            return IngestionResult.Failure($"JSON parse error: {ex.Message}");
        }

        var rep = root.StandardisedRepresentation;
        if (rep is null)
            return IngestionResult.Failure("standardisedRepresentation element missing.");

        var csp = rep.CspDisclosureRepresentation;
        if (csp is null)
            return IngestionResult.Failure("cspDisclosureRepresentation element missing.");

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 2s, 4s
                _logger.LogWarning(
                    "Retrying ingestion for Document {DocumentId} (attempt {Attempt}/{Max}) after {Delay}s",
                    documentId, attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }

            await using var conn = _connFactory.CreateConnection();
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

            try
            {
                _logger.LogInformation(
                    "Ingesting {Type} file for Document {DocumentId} ({FileId})",
                    rep.RepresentationType, documentId, rep.ProcessedFileID);

                // Step 1 — anchor row
                long jsonFileId = await _fileInserter.InsertAsync(rep, documentId, conn, tx, ct);

                // Step 2 — identifiers (no FK dependencies)
                var identifierIdMap = await _identifierProcessor.InsertAndMapAsync(
                    rep.Identifiers ?? [], jsonFileId, conn, tx, ct);

                // Step 3 — locations + sub-types
                var locationIdMap = await _locationProcessor.InsertAndMapAsync(
                    rep.Locations ?? [], jsonFileId, conn, tx, ct);

                // Step 4 — event or subscriber path (mutually exclusive per schema)
                var stdType = csp.StandardisedType;

                if (stdType?.StandardisedEventParty?.Events is { } events)
                {
                    await _eventProcessor.ProcessAsync(
                        events, jsonFileId, identifierIdMap, locationIdMap, conn, tx, ct);
                }
                else if (stdType?.StandardisedEntitySubscriber?.Subscribers is { } subscribers)
                {
                    await _subscriberProcessor.ProcessAsync(
                        subscribers, jsonFileId, identifierIdMap, locationIdMap, conn, tx, ct);
                }

                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Ingestion complete: JsonFile {JsonFileId} for Document {DocumentId}",
                    jsonFileId, documentId);

                return IngestionResult.Success(jsonFileId);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "Transient error for Document {DocumentId} on attempt {Attempt} — will retry",
                    documentId, attempt);
                try { await tx.RollbackAsync(); } catch { /* connection may already be dead */ }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Ingestion failed for Document {DocumentId} — rolling back", documentId);
                try { await tx.RollbackAsync(); } catch { }
                return IngestionResult.Failure(ex.Message);
            }
        }

        return IngestionResult.Failure("Ingestion failed after all retry attempts.");
    }

    /// <summary>
    /// Returns true for transient SQL errors that are safe to retry:
    /// VPN/TCP connection drops and deadlocks.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        var current = (Exception?)ex;
        while (current is not null)
        {
            if (current is SqlException sqlEx)
            {
                if (sqlEx.Number == 1205 || sqlEx.Number == 20 || sqlEx.Number == -2)
                    return true;
                if (sqlEx.Message.Contains("transport-level", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            if (current is InvalidOperationException &&
                (current.Message.Contains("open and available", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("current state is closed", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("current state is broken", StringComparison.OrdinalIgnoreCase)))
                return true;
            current = current.InnerException;
        }
        return false;
    }
}

public sealed class IngestionResult
{
    public bool IsSuccess  { get; private init; }
    public long? JsonFileId { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static IngestionResult Success(long jsonFileId) =>
        new() { IsSuccess = true, JsonFileId = jsonFileId };

    public static IngestionResult Failure(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
