using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Shared;

public sealed class CellLocationModel
{
    [JsonPropertyName("mCCRaw")]
    public string? MCCRaw { get; set; }

    [JsonPropertyName("mCCRawComposition")]
    public string? MCCRawComposition { get; set; }

    [JsonPropertyName("mCCStandardised")]
    public string? MCCStandardised { get; set; }

    [JsonPropertyName("mCCStandardisedComposition")]
    public string? MCCStandardisedComposition { get; set; }

    [JsonPropertyName("mNCRaw")]
    public string? MNCRaw { get; set; }

    [JsonPropertyName("mNCRawComposition")]
    public string? MNCRawComposition { get; set; }

    [JsonPropertyName("mNCStandardised")]
    public string? MNCStandardised { get; set; }

    [JsonPropertyName("mNCStandardisedComposition")]
    public string? MNCStandardisedComposition { get; set; }

    [JsonPropertyName("lACRaw")]
    public string? LACRaw { get; set; }

    [JsonPropertyName("lACRawComposition")]
    public string? LACRawComposition { get; set; }

    [JsonPropertyName("lACStandardised")]
    public string? LACStandardised { get; set; }

    [JsonPropertyName("lACStandardisedComposition")]
    public string? LACStandardisedComposition { get; set; }

    [JsonPropertyName("cellIDRaw")]
    public string? CellIDRaw { get; set; }

    [JsonPropertyName("cellIDRawComposition")]
    public string? CellIDRawComposition { get; set; }

    [JsonPropertyName("cellIDStandardised")]
    public string? CellIDStandardised { get; set; }

    [JsonPropertyName("cellIDStandardisedComposition")]
    public string? CellIDStandardisedComposition { get; set; }

    [JsonPropertyName("cGIRaw")]
    public string? CGIRaw { get; set; }

    [JsonPropertyName("cGIRawComposition")]
    public string? CGIRawComposition { get; set; }

    [JsonPropertyName("cGIStandardised")]
    public string? CGIStandardised { get; set; }

    [JsonPropertyName("cGIStandardisedComposition")]
    public string? CGIStandardisedComposition { get; set; }

    [JsonPropertyName("sACRaw")]
    public string? SACRaw { get; set; }

    [JsonPropertyName("sACRawComposition")]
    public string? SACRawComposition { get; set; }

    [JsonPropertyName("sACStandardised")]
    public string? SACStandardised { get; set; }

    [JsonPropertyName("sACStandardisedComposition")]
    public string? SACStandardisedComposition { get; set; }

    [JsonPropertyName("azimuthRaw")]
    public string? AzimuthRaw { get; set; }

    [JsonPropertyName("azimuthRawComposition")]
    public string? AzimuthRawComposition { get; set; }

    [JsonPropertyName("azimuthDegrees")]
    public string? AzimuthDegrees { get; set; }

    [JsonPropertyName("azimuthDegreesComposition")]
    public string? AzimuthDegreesComposition { get; set; }

    [JsonPropertyName("rATRaw")]
    public string? RATRaw { get; set; }

    [JsonPropertyName("rATRawComposition")]
    public string? RATRawComposition { get; set; }

    [JsonPropertyName("rATStandardised")]
    public string? RATStandardised { get; set; }

    [JsonPropertyName("rATStandardisedComposition")]
    public string? RATStandardisedComposition { get; set; }

    [JsonPropertyName("eCellIDRaw")]
    public string? ECellIDRaw { get; set; }

    [JsonPropertyName("eCellIDRawComposition")]
    public string? ECellIDRawComposition { get; set; }

    [JsonPropertyName("eCellIDStandardised")]
    public string? ECellIDStandardised { get; set; }

    [JsonPropertyName("eCellIDStandardisedComposition")]
    public string? ECellIDStandardisedComposition { get; set; }

    [JsonPropertyName("eNodeBRaw")]
    public string? ENodeBRaw { get; set; }

    [JsonPropertyName("eNodeBRawComposition")]
    public string? ENodeBRawComposition { get; set; }

    [JsonPropertyName("eNodeBStandardised")]
    public string? ENodeBStandardised { get; set; }

    [JsonPropertyName("eNodeBStandardisedComposition")]
    public string? ENodeBStandardisedComposition { get; set; }

    [JsonPropertyName("eCGIRaw")]
    public string? ECGIRaw { get; set; }

    [JsonPropertyName("eCGIRawComposition")]
    public string? ECGIRawComposition { get; set; }

    [JsonPropertyName("eCGIStandardised")]
    public string? ECGIStandardised { get; set; }

    [JsonPropertyName("eCGIStandardisedComposition")]
    public string? ECGIStandardisedComposition { get; set; }

    [JsonPropertyName("tACRaw")]
    public string? TACRaw { get; set; }

    [JsonPropertyName("tACRawComposition")]
    public string? TACRawComposition { get; set; }

    [JsonPropertyName("tACStandardised")]
    public string? TACStandardised { get; set; }

    [JsonPropertyName("tACStandardisedComposition")]
    public string? TACStandardisedComposition { get; set; }

    [JsonPropertyName("beamwidthRaw")]
    public string? BeamwidthRaw { get; set; }

    [JsonPropertyName("radiatedPowerRaw")]
    public string? RadiatedPowerRaw { get; set; }

    [JsonPropertyName("antennaHeightRaw")]
    public string? AntennaHeightRaw { get; set; }

    [JsonPropertyName("rangeRaw")]
    public string? RangeRaw { get; set; }
}
