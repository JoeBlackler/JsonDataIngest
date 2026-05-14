using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Events;

public sealed class EventModel
{
    [JsonPropertyName("processedEventID")]
    public string? ProcessedEventID { get; set; }

    [JsonPropertyName("sourceEventNumber")]
    public string? SourceEventNumber { get; set; }

    [JsonPropertyName("eventTypeRaw")]
    public string? EventTypeRaw { get; set; }

    [JsonPropertyName("eventTypeRawComposition")]
    public string? EventTypeRawComposition { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("eventSuperType")]
    public string? EventSuperType { get; set; }

    [JsonPropertyName("eventAttribute")]
    public List<KeyValueAttribute>? EventAttributes { get; set; }

    [JsonPropertyName("eventParty")]
    public List<EventPartyModel>? EventParties { get; set; }
}
