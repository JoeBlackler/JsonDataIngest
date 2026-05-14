using System.Text.Json.Serialization;
using JsonIngestService.Models.Shared;

namespace JsonIngestService.Models.Root;

public sealed class StandardisedRepresentation
{
    [JsonPropertyName("standardisedRepresentationSchemaVersion")]
    public string? SchemaVersion { get; set; }

    [JsonPropertyName("standardisedRepresentationType")]
    public string? RepresentationType { get; set; }

    [JsonPropertyName("processingEngine")]
    public string? ProcessingEngine { get; set; }

    [JsonPropertyName("processingEngineVersion")]
    public string? ProcessingEngineVersion { get; set; }

    [JsonPropertyName("processingRequestID")]
    public string? ProcessingRequestID { get; set; }

    [JsonPropertyName("processingCompletionTimestamp")]
    public string? ProcessingCompletionTimestamp { get; set; }

    [JsonPropertyName("processedFileID")]
    public string? ProcessedFileID { get; set; }

    [JsonPropertyName("cspDisclosureRepresentation")]
    public CspDisclosureRepresentation? CspDisclosureRepresentation { get; set; }

    [JsonPropertyName("identifier")]
    public List<IdentifierModel>? Identifiers { get; set; }

    [JsonPropertyName("location")]
    public List<LocationModel>? Locations { get; set; }
}
