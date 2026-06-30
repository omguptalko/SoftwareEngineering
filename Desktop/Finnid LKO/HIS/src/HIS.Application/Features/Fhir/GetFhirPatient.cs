using HIS.Application.Abstractions;
using HIS.Application.Features.Patients;
using HIS.Shared.Fhir;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Fhir;

/// <summary>
/// Exposes a patient as an HL7 FHIR R4 Patient resource (SRS 8.6, task 0.10) — the
/// HIP/HIU export shape reused by ABDM record exchange (Phase 1) and claims (Phase 7).
/// Identifier system URIs are config-driven (Fhir:*System), never hardcoded.
/// </summary>
public sealed record GetFhirPatientQuery(string Uhid) : IQuery<FhirPatient?>, IRequireAuthentication;

public sealed class GetFhirPatientHandler : MediatR.IRequestHandler<GetFhirPatientQuery, FhirPatient?>
{
    // ABDM/NDHM default identifier namespaces — overridable via config.
    private const string DefaultUhidSystem = "https://finnid.in/fhir/sid/uhid";
    private const string DefaultAbhaSystem = "https://healthid.ndhm.gov.in";

    private readonly MediatR.IMediator _mediator;
    private readonly IConfiguration _config;

    public GetFhirPatientHandler(MediatR.IMediator mediator, IConfiguration config)
    {
        _mediator = mediator; _config = config;
    }

    public async Task<FhirPatient?> Handle(GetFhirPatientQuery request, CancellationToken ct)
    {
        var p = await _mediator.Send(new GetPatientByUhidQuery(request.Uhid), ct);
        if (p is null) return null;

        var uhidSystem = _config["Fhir:UhidSystem"] ?? DefaultUhidSystem;
        var abhaSystem = _config["Fhir:AbhaSystem"] ?? DefaultAbhaSystem;

        var ids = new List<FhirIdentifier> { new() { System = uhidSystem, Value = p.Uhid } };
        if (!string.IsNullOrWhiteSpace(p.Abha))
            ids.Add(new FhirIdentifier { System = abhaSystem, Value = p.Abha });

        var telecom = new List<FhirContactPoint>();
        if (!string.IsNullOrWhiteSpace(p.Mobile))
            telecom.Add(new FhirContactPoint { System = "phone", Value = p.Mobile, Use = "mobile" });

        return new FhirPatient
        {
            Id = p.Uhid,
            Identifier = ids,
            Name = new[] { ToHumanName(p.Name) },
            Gender = ToFhirGender(p.Sex),
            Telecom = telecom.Count > 0 ? telecom : null
        };
    }

    private static FhirHumanName ToHumanName(string fullName)
    {
        var parts = (fullName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return new FhirHumanName { Text = fullName };
        var family = parts[^1];
        var given = parts.Length > 1 ? parts[..^1] : null;
        return new FhirHumanName { Text = fullName, Family = family, Given = given };
    }

    // FHIR administrative-gender value set: male | female | other | unknown.
    private static string ToFhirGender(string? sex) => (sex ?? "").Trim().ToUpperInvariant() switch
    {
        "M" or "MALE" => "male",
        "F" or "FEMALE" => "female",
        "O" or "OTHER" => "other",
        _ => "unknown"
    };
}
