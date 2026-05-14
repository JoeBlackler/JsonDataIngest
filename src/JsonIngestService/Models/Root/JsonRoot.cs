using System.Text.Json.Serialization;
using JsonIngestService.Models.Shared;

namespace JsonIngestService.Models.Root;

/// <summary>Root wrapper matching the top-level {"standardisedRepresentation": {...}} JSON.</summary>
public sealed class JsonRoot
{
    [JsonPropertyName("standardisedRepresentation")]
    public StandardisedRepresentation? StandardisedRepresentation { get; set; }
}
