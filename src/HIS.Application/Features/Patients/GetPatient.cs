using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Application.Features.Patients;

public sealed record VisitDto(string Date, string Branch, string Type, string Doctor, string Diagnosis, string Payer);

public sealed record PatientDto(
    string Uhid, string Name, int? Age, string Sex, string Mobile,
    string? Blood, string? Abha, string? AadhaarMasked, string? Payer, string? Policy,
    IReadOnlyList<VisitDto> Visits);

/// <summary>Loads a patient + cross-branch visit history — replaces HIS.mock.currentPatient.</summary>
public sealed record GetPatientByUhidQuery(string Uhid) : IQuery<PatientDto?>;

public sealed class GetPatientByUhidHandler : MediatR.IRequestHandler<GetPatientByUhidQuery, PatientDto?>
{
    private readonly IPatientRepository _repo;
    public GetPatientByUhidHandler(IPatientRepository repo) => _repo = repo;

    public async Task<PatientDto?> Handle(GetPatientByUhidQuery request, CancellationToken ct)
    {
        var p = await _repo.GetByUhidAsync(request.Uhid, ct);
        if (p is null) return null;

        var visits = await _repo.GetVisitsAsync(p.PatientId, ct);
        return new PatientDto(
            p.Uhid, p.FullName, p.AgeYears, p.Sex, p.Mobile,
            p.BloodGroup, p.AbhaNumber, p.AadhaarMasked,
            null, null,
            visits.Select(v => new VisitDto(
                v.VisitDate.ToString("dd-MMM-yyyy"), $"BR{v.BranchId}", v.VisitType,
                v.DoctorName ?? "—", v.Diagnosis ?? "", v.PayerName ?? "Cash")).ToList());
    }
}

/// <summary>Default patient for an empty workspace (first active patient of the branch).</summary>
public sealed record GetDefaultPatientQuery : IQuery<PatientDto?>;

public sealed class GetDefaultPatientHandler : MediatR.IRequestHandler<GetDefaultPatientQuery, PatientDto?>
{
    private readonly IPatientRepository _repo;
    private readonly IBranchContext _ctx;
    private readonly MediatR.IMediator _mediator;

    public GetDefaultPatientHandler(IPatientRepository repo, IBranchContext ctx, MediatR.IMediator mediator)
    {
        _repo = repo; _ctx = ctx; _mediator = mediator;
    }

    public async Task<PatientDto?> Handle(GetDefaultPatientQuery request, CancellationToken ct)
    {
        var list = await _repo.SearchAsync(null, _ctx.BranchId ?? 0, 1, ct);
        var first = list.FirstOrDefault();
        if (first is null) return null;
        return await _mediator.Send(new GetPatientByUhidQuery(first.Uhid), ct);
    }
}
