using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Shared;

public sealed class IdentifierModel
{
    [JsonPropertyName("identifierID")]
    public string? IdentifierID { get; set; }

    [JsonPropertyName("identifierRaw")]
    public string? IdentifierRaw { get; set; }

    [JsonPropertyName("identifierRawComposition")]
    public string? IdentifierRawComposition { get; set; }

    [JsonPropertyName("identifierStandardised")]
    public string? IdentifierStandardised { get; set; }

    [JsonPropertyName("identifierStandardisedComposition")]
    public string? IdentifierStandardisedComposition { get; set; }

    [JsonPropertyName("identifierTypeID")]
    public string? IdentifierTypeID { get; set; }

    [JsonPropertyName("identifierTypeName")]
    public string? IdentifierTypeName { get; set; }

    [JsonPropertyName("identifierSubType")]
    public string? IdentifierSubType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("firstSeen")]
    public string? FirstSeen { get; set; }

    [JsonPropertyName("lastSeen")]
    public string? LastSeen { get; set; }

    [JsonPropertyName("validFrom")]
    public string? ValidFrom { get; set; }

    [JsonPropertyName("validTo")]
    public string? ValidTo { get; set; }

    [JsonPropertyName("eventCount")]
    public int? EventCount { get; set; }
}
