using System.Text.Json.Serialization;
using JsonIngestService.Models.Events;

namespace JsonIngestService.Models.Subscribers;

public sealed class SubscriberModel
{
    [JsonPropertyName("subscriberID")]
    public string? SubscriberID { get; set; }

    [JsonPropertyName("subscriberNameRaw")]
    public string? SubscriberNameRaw { get; set; }

    [JsonPropertyName("subscriberNameRawComposition")]
    public string? SubscriberNameRawComposition { get; set; }

    [JsonPropertyName("subscriberNameStandardised")]
    public string? SubscriberNameStandardised { get; set; }

    [JsonPropertyName("subscriberNameStandardisedComposition")]
    public string? SubscriberNameStandardisedComposition { get; set; }

    [JsonPropertyName("dateOfBirthRaw")]
    public string? DateOfBirthRaw { get; set; }

    [JsonPropertyName("dateOfBirthRawComposition")]
    public string? DateOfBirthRawComposition { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("dateOfBirthComposition")]
    public string? DateOfBirthComposition { get; set; }

    [JsonPropertyName("genderRaw")]
    public string? GenderRaw { get; set; }

    [JsonPropertyName("salutationRaw")]
    public string? SalutationRaw { get; set; }

    [JsonPropertyName("firstNameRaw")]
    public string? FirstNameRaw { get; set; }

    [JsonPropertyName("secondNameRaw")]
    public string? SecondNameRaw { get; set; }

    [JsonPropertyName("middleNamesRaw")]
    public string? MiddleNamesRaw { get; set; }

    [JsonPropertyName("surnameRaw")]
    public string? SurnameRaw { get; set; }

    [JsonPropertyName("organisationNameRaw")]
    public string? OrganisationNameRaw { get; set; }

    [JsonPropertyName("professionRaw")]
    public string? ProfessionRaw { get; set; }

    [JsonPropertyName("contactIdentifierID")]
    public List<string>? ContactIdentifierIDs { get; set; }

    [JsonPropertyName("subscriberAttribute")]
    public List<KeyValueAttribute>? SubscriberAttributes { get; set; }

    [JsonPropertyName("subscriberLocation")]
    public List<SubscriberLocationModel>? SubscriberLocations { get; set; }

    [JsonPropertyName("subscription")]
    public List<SubscriptionModel>? Subscriptions { get; set; }
}
