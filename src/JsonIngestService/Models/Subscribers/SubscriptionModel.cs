using System.Text.Json.Serialization;
using JsonIngestService.Models.Events;

namespace JsonIngestService.Models.Subscribers;

public sealed class SubscriptionModel
{
    [JsonPropertyName("subscriptionID")]
    public string? SubscriptionID { get; set; }

    [JsonPropertyName("subscriptionTypeRaw")]
    public string? SubscriptionTypeRaw { get; set; }

    [JsonPropertyName("subscriptionActivationDateRaw")]
    public string? SubscriptionActivationDateRaw { get; set; }

    [JsonPropertyName("subscriptionActivationDateRawComposition")]
    public string? SubscriptionActivationDateRawComposition { get; set; }

    [JsonPropertyName("subscriptionActivationDate")]
    public string? SubscriptionActivationDate { get; set; }

    [JsonPropertyName("subscriptionActivationDateComposition")]
    public string? SubscriptionActivationDateComposition { get; set; }

    [JsonPropertyName("subscriptionDeactivationDateRaw")]
    public string? SubscriptionDeactivationDateRaw { get; set; }

    [JsonPropertyName("subscriptionDeactivationDateRawComposition")]
    public string? SubscriptionDeactivationDateRawComposition { get; set; }

    [JsonPropertyName("subscriptionDeactivationDate")]
    public string? SubscriptionDeactivationDate { get; set; }

    [JsonPropertyName("subscriptionDeactivationDateComposition")]
    public string? SubscriptionDeactivationDateComposition { get; set; }

    [JsonPropertyName("subscriptionDeactivationReasonRaw")]
    public string? SubscriptionDeactivationReasonRaw { get; set; }

    [JsonPropertyName("subscriptionIdentifierID")]
    public List<string>? SubscriptionIdentifierIDs { get; set; }

    [JsonPropertyName("subscriptionAttribute")]
    public List<KeyValueAttribute>? SubscriptionAttributes { get; set; }

    [JsonPropertyName("subscriptionLocation")]
    public List<SubscriptionLocationModel>? SubscriptionLocations { get; set; }
}

public sealed class SubscriberLocationModel
{
    [JsonPropertyName("locationID")]
    public string? LocationID { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

public sealed class SubscriptionLocationModel
{
    [JsonPropertyName("locationID")]
    public string? LocationID { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}
