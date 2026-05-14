using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Root;

public sealed class CdHashDetail
{
    [JsonPropertyName("hashValue")]
    public string? HashValue { get; set; }

    [JsonPropertyName("hashingAlgorithm")]
    public string? HashingAlgorithm { get; set; }

    [JsonPropertyName("hashCreatedOn")]
    public string? HashCreatedOn { get; set; }
}

public sealed class RequestParameter
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
