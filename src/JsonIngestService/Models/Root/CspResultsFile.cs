using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Root;

public sealed class CspResultsFile
{
    [JsonPropertyName("cspResultsFileID")]
    public string? CspResultsFileID { get; set; }

    [JsonPropertyName("cspResultsFileName")]
    public string? CspResultsFileName { get; set; }

    [JsonPropertyName("cspResultsFileCreatedOn")]
    public string? CspResultsFileCreatedOn { get; set; }

    [JsonPropertyName("cspResultsFileFormat")]
    public string? CspResultsFileFormat { get; set; }

    [JsonPropertyName("cspResultsDescription")]
    public string? CspResultsDescription { get; set; }

    [JsonPropertyName("cspResultsFileSize")]
    public string? CspResultsFileSize { get; set; }

    [JsonPropertyName("cdHashDetail")]
    public List<CdHashDetail>? HashDetails { get; set; }
}
