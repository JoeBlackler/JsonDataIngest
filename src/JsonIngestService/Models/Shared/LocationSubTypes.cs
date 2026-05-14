using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Shared;

public sealed class GeoLocationModel
{
    [JsonPropertyName("latitudeRaw")]
    public string? LatitudeRaw { get; set; }

    [JsonPropertyName("latitudeRawComposition")]
    public string? LatitudeRawComposition { get; set; }

    [JsonPropertyName("latitudeWGS84")]
    public double? LatitudeWGS84 { get; set; }

    [JsonPropertyName("latitudeWGS84Composition")]
    public string? LatitudeWGS84Composition { get; set; }

    [JsonPropertyName("longitudeRaw")]
    public string? LongitudeRaw { get; set; }

    [JsonPropertyName("longitudeRawComposition")]
    public string? LongitudeRawComposition { get; set; }

    [JsonPropertyName("longitudeWGS84")]
    public double? LongitudeWGS84 { get; set; }

    [JsonPropertyName("longitudeWGS84Composition")]
    public string? LongitudeWGS84Composition { get; set; }

    [JsonPropertyName("eastingRaw")]
    public string? EastingRaw { get; set; }

    [JsonPropertyName("eastingRawComposition")]
    public string? EastingRawComposition { get; set; }

    [JsonPropertyName("eastingOSGBMeterRef")]
    public int? EastingOSGBMeterRef { get; set; }

    [JsonPropertyName("eastingOSGBMeterRefComposition")]
    public string? EastingOSGBMeterRefComposition { get; set; }

    [JsonPropertyName("northingRaw")]
    public string? NorthingRaw { get; set; }

    [JsonPropertyName("northingRawComposition")]
    public string? NorthingRawComposition { get; set; }

    [JsonPropertyName("northingOSGBMeterRef")]
    public int? NorthingOSGBMeterRef { get; set; }

    [JsonPropertyName("northingOSGBMeterRefComposition")]
    public string? NorthingOSGBMeterRefComposition { get; set; }
}

public sealed class PostalAddressModel
{
    [JsonPropertyName("fullAddressRaw")]
    public string? FullAddressRaw { get; set; }

    [JsonPropertyName("fullAddressRawComposition")]
    public string? FullAddressRawComposition { get; set; }

    [JsonPropertyName("fullAddressStandardised")]
    public string? FullAddressStandardised { get; set; }

    [JsonPropertyName("fullAddressStandardisedComposition")]
    public string? FullAddressStandardisedComposition { get; set; }

    [JsonPropertyName("buildingNameRaw")]
    public string? BuildingNameRaw { get; set; }

    [JsonPropertyName("buildingNameRawComposition")]
    public string? BuildingNameRawComposition { get; set; }

    [JsonPropertyName("buildingNumberRaw")]
    public string? BuildingNumberRaw { get; set; }

    [JsonPropertyName("flatNumberRaw")]
    public string? FlatNumberRaw { get; set; }

    [JsonPropertyName("streetNameRaw")]
    public string? StreetNameRaw { get; set; }

    [JsonPropertyName("streetNameRawComposition")]
    public string? StreetNameRawComposition { get; set; }

    [JsonPropertyName("cityRaw")]
    public string? CityRaw { get; set; }

    [JsonPropertyName("postcodeRaw")]
    public string? PostcodeRaw { get; set; }

    [JsonPropertyName("poBoxRaw")]
    public string? PoBoxRaw { get; set; }

    [JsonPropertyName("countryRaw")]
    public string? CountryRaw { get; set; }
}

public sealed class WifiAccessPointModel
{
    [JsonPropertyName("accessPointIDRaw")]
    public string? AccessPointIDRaw { get; set; }

    [JsonPropertyName("accessPointNameRaw")]
    public string? AccessPointNameRaw { get; set; }

    [JsonPropertyName("sSIDRaw")]
    public string? SSIDRaw { get; set; }
}
