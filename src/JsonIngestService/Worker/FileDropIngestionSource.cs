using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JsonIngestService.Worker;

/// <summary>
/// Polls a directory for new JSON files and yields them as IngestionRequest objects.
///
/// FileSystemWatcher is NOT used because inotify events are not propagated into
/// Linux containers on Docker Desktop / Windows bind-mounts. Polling is reliable
/// in all environments.
///
/// File naming convention:  {documentId}_{anything}.json
///   e.g.  4821_standardised_events.json  →  DocumentId = 4821
/// </summary>
public sealed class FileDropIngestionSource : IIngestionSource
{
    private readonly FileDropOptions _opts;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<FileDropIngestionSource> _logger;
    private readonly Channel<IngestionRequest> _channel;
    // Tracks paths currently in-flight so each file is enqueued exactly once.
    // Uses ConcurrentDictionary for thread-safe removal via ReleaseForRetry.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte>
        _seen = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex _fileNamePattern = new(@"^(\d+)_", RegexOptions.Compiled);

    public FileDropIngestionSource(
        IOptions<IngestOptions> options,
        ILogger<FileDropIngestionSource> logger)
    {
        _opts         = options.Value.FileDrop;
        _pollInterval = TimeSpan.FromSeconds(_opts.PollIntervalSeconds);
        _logger       = logger;

        Directory.CreateDirectory(_opts.WatchPath);
        Directory.CreateDirectory(_opts.ProcessedPath);
        Directory.CreateDirectory(_opts.ErrorPath);

        // Bounded channel: back-pressure if ingestion falls behind file drop rate.
        _channel = Channel.CreateBounded<IngestionRequest>(new BoundedChannelOptions(50)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public void ReleaseForRetry(string filePath) => _seen.TryRemove(filePath, out _);

    public async IAsyncEnumerable<IngestionRequest> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Run the poll loop as a fire-and-forget background task scoped to ct.
        _ = Task.Run(() => PollLoopAsync(ct), ct);

        await foreach (var request in _channel.Reader.ReadAllAsync(ct))
            yield return request;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "File drop polling started. Path={Path}  Interval={Interval}s",
            _opts.WatchPath, _pollInterval.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var file in Directory.GetFiles(_opts.WatchPath, _opts.FilePattern))
                {
                    if (_seen.ContainsKey(file)) continue; // already enqueued
                    if (!IsFileReady(file))
                    {
                        _logger.LogDebug("File not yet ready (still being written): {File}", Path.GetFileName(file));
                        continue; // will be retried on the next poll
                    }
                    _seen.TryAdd(file, 0);
                    _logger.LogDebug("Detected new file: {File}", Path.GetFileName(file));
                    TryEnqueue(file);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error scanning drop folder {Path}", _opts.WatchPath);
            }

            await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
        }
    }

    private void TryEnqueue(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var match    = _fileNamePattern.Match(fileName);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, out var documentId))
        {
            _logger.LogWarning(
                "Skipping file with unrecognised name pattern: {File}. " +
                "Expected: {{documentId}}_*.json", fileName);
            MoveFile(fullPath, _opts.ErrorPath);
            return;
        }

        _channel.Writer.TryWrite(new IngestionRequest(fullPath, documentId));
    }

    private void MoveFile(string source, string targetDir)
    {
        try
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(source));
            if (File.Exists(dest)) dest = Path.Combine(targetDir,
                $"{Path.GetFileNameWithoutExtension(source)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(source, dest, overwrite: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move {File} to {Dir}", source, targetDir);
        }
    }

    /// <summary>
    /// Returns true only when the file can be opened exclusively — i.e. it is
    /// fully written and not locked by the process that is still copying it in.
    /// </summary>
    private static bool IsFileReady(string path)
    {
        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return fs.Length > 0;
        }
        catch (IOException)
        {
            return false; // still being written or locked
        }
    }
}
