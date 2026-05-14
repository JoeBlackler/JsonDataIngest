namespace JsonIngestService.Worker;

/// <summary>
/// Represents a single file to be ingested along with its database context.
/// </summary>
public sealed record IngestionRequest(
    string FilePath,
    long DocumentId);

/// <summary>
/// Abstraction over a file source (file-drop folder, queue, etc.).
/// Implement this to swap between file-system watching and a message bus
/// without changing any pipeline code.
/// </summary>
public interface IIngestionSource
{
    /// <summary>
    /// Yields ingestion requests one at a time.
    /// Blocks until a file is available or <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<IngestionRequest> ReadAsync(CancellationToken ct);

    /// <summary>
    /// Removes the given file path from the "already seen" set so that,
    /// if the file is moved back to the drop folder, it will be re-detected
    /// and retried on the next poll cycle.
    /// </summary>
    void ReleaseForRetry(string filePath);
}
