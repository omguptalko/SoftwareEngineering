using System.Text.Json;
using System.Text.Json.Serialization;

namespace HIS.Shared.Fhir;

// ---------------------------------------------------------------------------
// Minimal HL7 FHIR R4 model library (SRS 8.6, task 0.10). These records map 1:1
// onto the FHIR R4 JSON wire format (property names are the FHIR element names).
// Optional elements are nullable and omitted on serialization via FhirJson.Options,
// so the output stays a conformant FHIR resource. Extend with more resources
// (Encounter, Observation, Claim, Bundle) as interoperability surfaces grow.
// ---------------------------------------------------------------------------

/// <summary>Shared JSON options producing conformant FHIR JSON (null elements omitted).</summary>
public static class FhirJson
{
    public const string MediaType = "application/fhir+json";

    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null   // FHIR element names are explicit via [JsonPropertyName]
    };
}

public sealed class FhirIdentifier
{
    [JsonPropertyName("system")] public string? System { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
}

public sealed class FhirHumanName
{
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("family")] public string? Family { get; set; }
    [JsonPropertyName("given")] public IReadOnlyList<string>? Given { get; set; }
}

public sealed class FhirContactPoint
{
    [JsonPropertyName("system")] public string? System { get; set; }   // phone | email | ...
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("use")] public string? Use { get; set; }
}

public sealed class FhirCoding
{
    [JsonPropertyName("system")] public string? System { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("display")] public string? Display { get; set; }
}

public sealed class FhirCodeableConcept
{
    [JsonPropertyName("coding")] public IReadOnlyList<FhirCoding>? Coding { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

/// <summary>FHIR R4 Patient resource (subset).</summary>
public sealed class FhirPatient
{
    [JsonPropertyName("resourceType")] public string ResourceType => "Patient";
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("identifier")] public IReadOnlyList<FhirIdentifier>? Identifier { get; set; }
    [JsonPropertyName("active")] public bool Active { get; set; } = true;
    [JsonPropertyName("name")] public IReadOnlyList<FhirHumanName>? Name { get; set; }
    [JsonPropertyName("gender")] public string? Gender { get; set; }   // male | female | other | unknown
    [JsonPropertyName("telecom")] public IReadOnlyList<FhirContactPoint>? Telecom { get; set; }
}
