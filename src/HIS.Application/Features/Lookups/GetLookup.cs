using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Application.Features.Lookups;

/// <summary>Shape the wireframe's F3 modal expects: a title, column headers and rows.</summary>
public sealed record LookupResultDto(string Title, IReadOnlyList<string> Cols, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Generic F3 lookup query — replaces the static HIS.lookups datasets in data.js.
/// Supported types mirror the wireframe: doctor, drug, icd10, ward, payer, patient, package.
/// </summary>
public sealed record GetLookupQuery(string Type, string? Q) : IQuery<LookupResultDto>;

public sealed class GetLookupHandler : MediatR.IRequestHandler<GetLookupQuery, LookupResultDto>
{
    private readonly ILookupRepository _lk;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;

    public GetLookupHandler(ILookupRepository lk, IPatientRepository patients, IBranchContext ctx)
    {
        _lk = lk; _patients = patients; _ctx = ctx;
    }

    public async Task<LookupResultDto> Handle(GetLookupQuery request, CancellationToken ct)
    {
        var q = request.Q;
        var branchId = _ctx.BranchId ?? 0;

        switch (request.Type?.ToLowerInvariant())
        {
            case "doctor":
                var docs = await _lk.GetDoctorsAsync(q, ct);
                return new LookupResultDto("Doctor Lookup", new[] { "Code", "Name", "Department" },
                    docs.Select(d => (IReadOnlyList<string>)new[] { d.Code, d.Name, d.Department }).ToList());

            case "drug":
                var drugs = await _lk.GetDrugsAsync(q, ct);
                return new LookupResultDto("Drug Lookup", new[] { "Code", "Name", "Form", "Stock" },
                    drugs.Select(d => (IReadOnlyList<string>)new[] { d.Code, d.Name, d.Form, d.StockQty.ToString("N0") }).ToList());

            case "icd10":
                var icd = await _lk.GetIcd10Async(q, ct);
                return new LookupResultDto("ICD-10 Diagnosis", new[] { "Code", "Description" },
                    icd.Select(i => (IReadOnlyList<string>)new[] { i.Code, i.Description }).ToList());

            case "payer":
                var payers = await _lk.GetPayersAsync(q, ct);
                return new LookupResultDto("Payer / Insurer Lookup", new[] { "Code", "Name", "Type" },
                    payers.Select(p => (IReadOnlyList<string>)new[] { p.Code, p.Name, p.PayerType }).ToList());

            case "package":
                var pkgs = await _lk.GetPackagesAsync(q, ct);
                return new LookupResultDto("HBP Package (PM-JAY)", new[] { "Code", "Package", "Rate ₹" },
                    pkgs.Select(p => (IReadOnlyList<string>)new[] { p.Code, p.Name, p.Rate.ToString("N0") }).ToList());

            case "ward":
                var beds = await _lk.GetWardBedsAsync(branchId, q, ct);
                return new LookupResultDto("Ward / Bed Lookup", new[] { "Ward", "Bed", "Status" },
                    beds.Select(b => (IReadOnlyList<string>)new[] { b.Ward, b.Bed, b.Status }).ToList());

            case "tariff":
                var tariffs = await _lk.GetTariffsAsync(branchId, q, ct);
                return new LookupResultDto("Service / Tariff Lookup", new[] { "Code", "Service", "Rate ₹" },
                    tariffs.Select(t => (IReadOnlyList<string>)new[] { t.ServiceCode, t.ServiceName, t.Rate.ToString("N2") }).ToList());

            case "patient":
                var pts = await _patients.SearchAsync(q, branchId, 50, ct);
                return new LookupResultDto("Patient Lookup", new[] { "UHID", "Name", "Age/Sex", "Mobile" },
                    pts.Select(p => (IReadOnlyList<string>)new[]
                    {
                        p.Uhid, p.FullName, $"{p.AgeYears} / {(string.IsNullOrEmpty(p.Sex) ? "" : p.Sex[..1])}", p.Mobile
                    }).ToList());

            default:
                return new LookupResultDto("Lookup", Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
        }
    }
}
