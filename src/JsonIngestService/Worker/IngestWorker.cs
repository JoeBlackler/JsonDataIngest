using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JsonIngestService.Pipeline;

namespace JsonIngestService.Worker;

/// <summary>
/// Kubernetes-compatible background worker.
/// Reads from the configured IIngestionSource and processes up to
/// MaxConcurrentFiles files in parallel, bounding resource usage.
/// </summary>
public sealed class IngestWorker : BackgroundService
{
    private readonly IIngestionSource _source;
    private readonly IngestionOrchestrator _orchestrator;
    private readonly IngestOptions _opts;
    private readonly ILogger<IngestWorker> _logger;

    public IngestWorker(
        IIngestionSource source,
        IngestionOrchestrator orchestrator,
        IOptions<IngestOptions> options,
        ILogger<IngestWorker> logger)
    {
        _source       = source;
        _orchestrator = orchestrator;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "IngestWorker started. MaxConcurrentFiles={Max}",
            _opts.MaxConcurrentFiles);

        var semaphore = new SemaphoreSlim(_opts.MaxConcurrentFiles, _opts.MaxConcurrentFiles);

        await foreach (var request in _source.ReadAsync(stoppingToken))
        {
            // Acquire slot — blocks when MaxConcurrentFiles are already in flight.
            await semaphore.WaitAsync(stoppingToken);

            // Fire-and-forget the individual file processing; semaphore released when done.
            _ = ProcessRequestAsync(request, semaphore, stoppingToken);
        }
    }

    private async Task ProcessRequestAsync(
        IngestionRequest request,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        var dropOpts = _opts.FileDrop;
        try
        {
            _logger.LogInformation(
                "Processing {File} (DocumentId={DocId})",
                Path.GetFileName(request.FilePath), request.DocumentId);

            // Dispose the stream inside its own block so the file handle is released
            // before MoveFile is called. On Windows bind-mounts the OS rejects a move
            // while the file is still open, even from the Linux container side.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            IngestionResult result;
            await using (var stream = new FileStream(
                request.FilePath,
                FileMode.Open, FileAccess.Read, FileShare.None,
                bufferSize: 65536,
                useAsync: true))
            {
                result = await _orchestrator.IngestAsync(stream, request.DocumentId, ct);
            }
            sw.Stop();

            if (result.IsSuccess)
            {
                MoveFile(request.FilePath, dropOpts.ProcessedPath);
                _logger.LogInformation(
                    "Ingestion succeeded: JsonFile {Id} in {Elapsed:F1}s", result.JsonFileId, sw.Elapsed.TotalSeconds);
            }
            else
            {
                if (MoveFile(request.FilePath, dropOpts.ErrorPath))
                    _source.ReleaseForRetry(request.FilePath);
                _logger.LogError(
                    "Ingestion failed for {File}: {Error}",
                    Path.GetFileName(request.FilePath), result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception processing {File}", Path.GetFileName(request.FilePath));
            if (MoveFile(request.FilePath, dropOpts.ErrorPath))
                _source.ReleaseForRetry(request.FilePath);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private bool MoveFile(string source, string targetDir)
    {
        try
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(source));
            if (File.Exists(dest))
                dest = Path.Combine(targetDir,
                    $"{Path.GetFileNameWithoutExtension(source)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(source, dest, overwrite: false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive {File}", source);
            return false;
        }
    }
}
