using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Events;

public sealed class EventPartyModel
{
    [JsonPropertyName("partyIdentifierID")]
    public List<string>? PartyIdentifierIDs { get; set; }

    // ---- Start datetime ----
    [JsonPropertyName("startDateTimeRaw")]
    public string? StartDateTimeRaw { get; set; }

    [JsonPropertyName("startDateTimeRawComposition")]
    public string? StartDateTimeRawComposition { get; set; }

    [JsonPropertyName("startDateTimeISO8601")]
    public string? StartDateTimeISO8601 { get; set; }

    [JsonPropertyName("startDateTimeISO8601Composition")]
    public string? StartDateTimeISO8601Composition { get; set; }

    // ---- End datetime ----
    [JsonPropertyName("endDateTimeRaw")]
    public string? EndDateTimeRaw { get; set; }

    [JsonPropertyName("endDateTimeRawComposition")]
    public string? EndDateTimeRawComposition { get; set; }

    [JsonPropertyName("endDateTimeISO8601")]
    public string? EndDateTimeISO8601 { get; set; }

    [JsonPropertyName("endDateTimeISO8601Composition")]
    public string? EndDateTimeISO8601Composition { get; set; }

    // ---- Duration ----
    [JsonPropertyName("durationRaw")]
    public string? DurationRaw { get; set; }

    [JsonPropertyName("durationRawComposition")]
    public string? DurationRawComposition { get; set; }

    [JsonPropertyName("durationSeconds")]
    public int? DurationSeconds { get; set; }

    [JsonPropertyName("durationSecondsComposition")]
    public string? DurationSecondsComposition { get; set; }

    // ---- Data received ----
    [JsonPropertyName("dataReceivedRaw")]
    public string? DataReceivedRaw { get; set; }

    [JsonPropertyName("dataReceivedRawComposition")]
    public string? DataReceivedRawComposition { get; set; }

    [JsonPropertyName("dataReceivedBytes")]
    public long? DataReceivedBytes { get; set; }

    [JsonPropertyName("dataReceivedBytesComposition")]
    public string? DataReceivedBytesComposition { get; set; }

    // ---- Data sent ----
    [JsonPropertyName("dataSentRaw")]
    public string? DataSentRaw { get; set; }

    [JsonPropertyName("dataSentRawComposition")]
    public string? DataSentRawComposition { get; set; }

    [JsonPropertyName("dataSentBytes")]
    public long? DataSentBytes { get; set; }

    [JsonPropertyName("dataSentBytesComposition")]
    public string? DataSentBytesComposition { get; set; }

    // ---- Location references ----
    [JsonPropertyName("startLocationID")]
    public string? StartLocationID { get; set; }

    [JsonPropertyName("endLocationID")]
    public string? EndLocationID { get; set; }

    // ---- Role ----
    [JsonPropertyName("roleRaw")]
    public string? RoleRaw { get; set; }

    [JsonPropertyName("roleRawComposition")]
    public string? RoleRawComposition { get; set; }

    [JsonPropertyName("roleStandardised")]
    public string? RoleStandardised { get; set; }

    [JsonPropertyName("roleStandardisedComposition")]
    public string? RoleStandardisedComposition { get; set; }

    [JsonPropertyName("partyAttribute")]
    public List<KeyValueAttribute>? PartyAttributes { get; set; }
}

/// <summary>Shared key-value attribute used by events, parties, subscribers, and subscriptions.</summary>
public sealed class KeyValueAttribute
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
