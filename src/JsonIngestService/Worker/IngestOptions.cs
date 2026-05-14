namespace JsonIngestService.Worker;

public sealed class IngestOptions
{
    public FileDropOptions FileDrop    { get; set; } = new();
    public BulkCopyOptions BulkCopy   { get; set; } = new();
    public int MaxConcurrentFiles      { get; set; } = 2;
}

public sealed class FileDropOptions
{
    public string WatchPath       { get; set; } = "/data/ingest/drop";
    public string ProcessedPath   { get; set; } = "/data/ingest/processed";
    public string ErrorPath       { get; set; } = "/data/ingest/error";
    public string FilePattern     { get; set; } = "*.json";
    /// <summary>How often (seconds) to scan the drop folder. Default 5s.</summary>
    public int    PollIntervalSeconds { get; set; } = 5;
}

public sealed class BulkCopyOptions
{
    public int BatchSize       { get; set; } = 5000;
    public int TimeoutSeconds  { get; set; } = 300;
}
