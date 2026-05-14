using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Shared;

public sealed class LocationModel
{
    [JsonPropertyName("locationID")]
    public string? LocationID { get; set; }

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

    [JsonPropertyName("cellLocation")]
    public CellLocationModel? CellLocation { get; set; }

    [JsonPropertyName("wifiAccessPoint")]
    public WifiAccessPointModel? WifiAccessPoint { get; set; }

    [JsonPropertyName("geoLocation")]
    public GeoLocationModel? GeoLocation { get; set; }

    [JsonPropertyName("postalAddress")]
    public PostalAddressModel? PostalAddress { get; set; }
}
