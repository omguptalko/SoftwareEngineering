using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Patients;

/// <summary>A patient row for the registration list. Tenant-scoped: it comes from the
/// resolved hospital's master DB, so each hospital only ever sees its own patients.</summary>
public sealed record PatientListItemDto(
    string Uhid, string FullName, string? GuardianName, int? AgeYears, string Sex,
    string? BloodGroup, string Mobile, string? Email, string? Address, string? City, DateTime RegisteredAtUtc);

// ---------------------------- Read (list) ----------------------------
public sealed record GetPatientsQuery(string? Q = null) : IQuery<IReadOnlyList<PatientListItemDto>>, IRequireAuthentication;

public sealed class GetPatientsHandler : MediatR.IRequestHandler<GetPatientsQuery, IReadOnlyList<PatientListItemDto>>
{
    private readonly IPatientRepository _repo; private readonly IBranchContext _ctx;
    public GetPatientsHandler(IPatientRepository repo, IBranchContext ctx) { _repo = repo; _ctx = ctx; }
    public async Task<IReadOnlyList<PatientListItemDto>> Handle(GetPatientsQuery q, CancellationToken ct)
    {
        var rows = await _repo.SearchAsync(q.Q, _ctx.BranchId ?? 0, 200, ct);
        return rows.Select(p => new PatientListItemDto(
            p.Uhid, p.FullName, p.GuardianName, p.AgeYears, p.Sex, p.BloodGroup,
            p.Mobile, p.Email, p.Address, p.City, p.RegisteredAtUtc)).ToList();
    }
}

// ---------------------------- Update ----------------------------
public sealed record UpdatePatientCommand(
    string Uhid, string FullName, string? GuardianName, int? AgeYears, string Sex,
    string? BloodGroup, string Mobile, string? Email, string? Address, string? City)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Patient";
    public string? AuditEntityId => Uhid;
}

public sealed class UpdatePatientValidator : AbstractValidator<UpdatePatientCommand>
{
    public UpdatePatientValidator()
    {
        RuleFor(x => x.Uhid).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Sex).NotEmpty();
        RuleFor(x => x.Mobile).NotEmpty();
    }
}

public sealed class UpdatePatientHandler : MediatR.IRequestHandler<UpdatePatientCommand, bool>
{
    private readonly IPatientRepository _repo;
    public UpdatePatientHandler(IPatientRepository repo) { _repo = repo; }
    public async Task<bool> Handle(UpdatePatientCommand c, CancellationToken ct)
    {
        var ok = await _repo.UpdateAsync(new Patient
        {
            Uhid = c.Uhid, FullName = c.FullName, GuardianName = c.GuardianName, AgeYears = c.AgeYears,
            Sex = c.Sex, BloodGroup = c.BloodGroup, Mobile = c.Mobile, Email = c.Email, Address = c.Address, City = c.City
        }, ct);
        if (!ok) throw new InvalidOperationException($"Patient '{c.Uhid}' not found.");
        return true;
    }
}

// ---------------------------- Deactivate / restore (soft delete) ----------------------------
public sealed record SetPatientActiveCommand(string Uhid, bool IsActive) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Patient";
    public string? AuditEntityId => Uhid;
}

public sealed class SetPatientActiveHandler : MediatR.IRequestHandler<SetPatientActiveCommand, bool>
{
    private readonly IPatientRepository _repo;
    public SetPatientActiveHandler(IPatientRepository repo) { _repo = repo; }
    public async Task<bool> Handle(SetPatientActiveCommand c, CancellationToken ct)
    {
        var ok = await _repo.SetActiveAsync(c.Uhid, c.IsActive, ct);
        if (!ok) throw new InvalidOperationException($"Patient '{c.Uhid}' not found.");
        return true;
    }
}
