using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Ot;

// ============================ Schedule surgery (§3.12) ============================
/// <summary>Schedule a surgery in an operation theatre — SRS §3.12. Resolves patient + surgeon
/// server-side; theatre/procedure free-text per case.</summary>
public sealed record ScheduleSurgeryCommand(
    string PatientUhid, string? SurgeonCode, string? Theatre, DateTime ScheduledUtc, string? Procedure)
    : ICommand<ScheduleSurgeryResult>, IAuditable
{
    public string AuditEntity => "OtSchedule";
    public string? AuditEntityId => PatientUhid;
}
public sealed record ScheduleSurgeryResult(long OtId, string Status);

public sealed class ScheduleSurgeryValidator : AbstractValidator<ScheduleSurgeryCommand>
{
    public ScheduleSurgeryValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.ScheduledUtc).NotEmpty();
    }
}

public sealed class ScheduleSurgeryHandler : MediatR.IRequestHandler<ScheduleSurgeryCommand, ScheduleSurgeryResult>
{
    private readonly IOtRepository _ot;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    public ScheduleSurgeryHandler(IOtRepository ot, IPatientRepository patients, IBranchContext ctx)
    { _ot = ot; _patients = patients; _ctx = ctx; }

    public async Task<ScheduleSurgeryResult> Handle(ScheduleSurgeryCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");

        var surgeonId = string.IsNullOrWhiteSpace(c.SurgeonCode)
            ? (int?)null : await _ot.GetDoctorIdByCodeAsync(LookupCode.Parse(c.SurgeonCode!), ct);

        var id = await _ot.InsertScheduleAsync(new OtSchedule
        {
            BranchId = branchId,
            PatientId = patient.PatientId,
            SurgeonId = surgeonId,
            Theatre = c.Theatre,
            ScheduledUtc = c.ScheduledUtc,
            Procedure = c.Procedure,
            Status = "Scheduled"
        }, ct);

        return new ScheduleSurgeryResult(id, "Scheduled");
    }
}

// ============================ Start surgery (wheel-in / In Progress) (§3.12) ============================
public sealed record StartSurgeryCommand(long OtId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "OtSchedule";
    public string? AuditEntityId => OtId.ToString();
}

public sealed class StartSurgeryHandler : MediatR.IRequestHandler<StartSurgeryCommand, bool>
{
    private readonly IOtRepository _ot;
    public StartSurgeryHandler(IOtRepository ot) => _ot = ot;

    public async Task<bool> Handle(StartSurgeryCommand c, CancellationToken ct)
    {
        var sched = await _ot.GetScheduleAsync(c.OtId, ct)
            ?? throw new InvalidOperationException("OT schedule not found.");
        if (!string.Equals(sched.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot start a surgery in status '{sched.Status}'.");
        await _ot.SetStatusAsync(c.OtId, "InProgress", ct);
        return true;
    }
}

// ============================ Complete surgery / post-op (§3.12) ============================
public sealed record CompleteSurgeryCommand(long OtId, string? PostOpNotes)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "OtSchedule";
    public string? AuditEntityId => OtId.ToString();
}

public sealed class CompleteSurgeryHandler : MediatR.IRequestHandler<CompleteSurgeryCommand, bool>
{
    private readonly IOtRepository _ot;
    public CompleteSurgeryHandler(IOtRepository ot) => _ot = ot;

    public async Task<bool> Handle(CompleteSurgeryCommand c, CancellationToken ct)
    {
        var sched = await _ot.GetScheduleAsync(c.OtId, ct)
            ?? throw new InvalidOperationException("OT schedule not found.");
        if (string.Equals(sched.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Surgery already completed.");

        await _ot.CompleteAsync(c.OtId, c.PostOpNotes, ct);
        return true;
    }
}

// ============================ OT board (§3.12) ============================
public sealed record OtBoardRow(long OtId, string Patient, string? Surgeon, string? Theatre, string? Procedure, DateTime ScheduledUtc, string Status);
public sealed record GetOtBoardQuery : IQuery<IReadOnlyList<OtBoardRow>>;

public sealed class GetOtBoardHandler : MediatR.IRequestHandler<GetOtBoardQuery, IReadOnlyList<OtBoardRow>>
{
    private readonly IOtRepository _ot;
    private readonly IBranchContext _ctx;
    public GetOtBoardHandler(IOtRepository ot, IBranchContext ctx) { _ot = ot; _ctx = ctx; }

    public async Task<IReadOnlyList<OtBoardRow>> Handle(GetOtBoardQuery q, CancellationToken ct)
    {
        var rows = await _ot.GetBoardAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new OtBoardRow(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6, r.Item7)).ToList();
    }
}
