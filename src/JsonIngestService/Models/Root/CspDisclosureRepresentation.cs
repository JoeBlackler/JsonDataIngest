using System.Text.Json.Serialization;

namespace JsonIngestService.Models.Root;

public sealed class CspDisclosureRepresentation
{
    [JsonPropertyName("cspOrganisationID")]
    public string? CspOrganisationID { get; set; }

    [JsonPropertyName("cspOrganisationName")]
    public string? CspOrganisationName { get; set; }

    [JsonPropertyName("cspDisclosureSystemID")]
    public string? CspDisclosureSystemID { get; set; }

    [JsonPropertyName("cspDisclosureSystemName")]
    public string? CspDisclosureSystemName { get; set; }

    [JsonPropertyName("cspDisclosureProductID")]
    public string? CspDisclosureProductID { get; set; }

    [JsonPropertyName("cspDisclosureProductName")]
    public string? CspDisclosureProductName { get; set; }

    [JsonPropertyName("cspDisclosureProductSchemaID")]
    public string? CspDisclosureProductSchemaID { get; set; }

    [JsonPropertyName("cspDisclosureProductSchemaName")]
    public string? CspDisclosureProductSchemaName { get; set; }

    [JsonPropertyName("cspDisclosureProductSchemaVersion")]
    public string? CspDisclosureProductSchemaVersion { get; set; }

    [JsonPropertyName("originalRequestingAuthority")]
    public string? OriginalRequestingAuthority { get; set; }

    [JsonPropertyName("authorisationIdentifier")]
    public string? AuthorisationIdentifier { get; set; }

    [JsonPropertyName("authorityRequestID")]
    public string? AuthorityRequestID { get; set; }

    [JsonPropertyName("cspRequestCreatedOn")]
    public string? CspRequestCreatedOn { get; set; }

    [JsonPropertyName("cdRequestDescription")]
    public string? CdRequestDescription { get; set; }

    [JsonPropertyName("requestParameters")]
    public List<RequestParameter>? RequestParameters { get; set; }

    [JsonPropertyName("cspResultsFile")]
    public CspResultsFile? CspResultsFile { get; set; }

    [JsonPropertyName("standardisedType")]
    public StandardisedType? StandardisedType { get; set; }
}
