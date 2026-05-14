using System.Text.Json.Serialization;
using JsonIngestService.Models.Events;
using JsonIngestService.Models.Subscribers;

namespace JsonIngestService.Models.Root;

public sealed class StandardisedType
{
    [JsonPropertyName("standardisedEventParty")]
    public StandardisedEventParty? StandardisedEventParty { get; set; }

    [JsonPropertyName("standardisedEntitySubscriber")]
    public StandardisedEntitySubscriber? StandardisedEntitySubscriber { get; set; }
}

public sealed class StandardisedEventParty
{
    [JsonPropertyName("event")]
    public List<EventModel>? Events { get; set; }
}

public sealed class StandardisedEntitySubscriber
{
    [JsonPropertyName("subscriber")]
    public List<SubscriberModel>? Subscribers { get; set; }
}
