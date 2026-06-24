using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Emergency;

// ============================ Triage register (§3.5) ============================
/// <summary>Triage an emergency arrival. Category validated against the config-driven
/// list (Emergency:TriageCategories). Patient optional — unknown/unconscious arrivals allowed.</summary>
public sealed record RegisterTriageCommand(string? PatientUhid, string Category, bool IsMlc, string? Notes)
    : ICommand<RegisterTriageResult>, IAuditable
{
    public string AuditEntity => "EmergencyTriage";
    public string? AuditEntityId => PatientUhid;
}
public sealed record RegisterTriageResult(long TriageId, string Category, string Status);

public sealed class RegisterTriageValidator : AbstractValidator<RegisterTriageCommand>
{
    public RegisterTriageValidator() => RuleFor(x => x.Category).NotEmpty();
}

public sealed class RegisterTriageHandler : MediatR.IRequestHandler<RegisterTriageCommand, RegisterTriageResult>
{
    private readonly IEmergencyRepository _er;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public RegisterTriageHandler(IEmergencyRepository er, IPatientRepository patients, IBranchContext ctx, IConfiguration config)
    { _er = er; _patients = patients; _ctx = ctx; _config = config; }

    public async Task<RegisterTriageResult> Handle(RegisterTriageCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");

        var categories = EmergencyConfig.TriageCategories(_config);
        if (!categories.Any(cat => string.Equals(cat, c.Category, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Invalid triage category '{c.Category}'. Allowed: {string.Join(", ", categories)}.");

        long? patientId = null;
        if (!string.IsNullOrWhiteSpace(c.PatientUhid))
        {
            var p = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct)
                ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
            patientId = p.PatientId;
        }

        var id = await _er.InsertTriageAsync(new EmergencyTriage
        {
            BranchId = branchId,
            PatientId = patientId,
            ArrivedUtc = DateTime.UtcNow,
            Category = c.Category,
            IsMlc = c.IsMlc,
            Notes = c.Notes,
            Status = "Waiting"
        }, ct);

        return new RegisterTriageResult(id, c.Category, "Waiting");
    }
}

// ============================ Triage disposition (§3.5) ============================
public sealed record SetTriageDispositionCommand(long TriageId, string Status)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "EmergencyTriage";
    public string? AuditEntityId => TriageId.ToString();
}

public sealed class SetTriageDispositionHandler : MediatR.IRequestHandler<SetTriageDispositionCommand, bool>
{
    private readonly IEmergencyRepository _er;
    private readonly IConfiguration _config;
    public SetTriageDispositionHandler(IEmergencyRepository er, IConfiguration config) { _er = er; _config = config; }

    public async Task<bool> Handle(SetTriageDispositionCommand c, CancellationToken ct)
    {
        var allowed = EmergencyConfig.TriageStatuses(_config);
        if (!allowed.Any(s => string.Equals(s, c.Status, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Invalid disposition '{c.Status}'. Allowed: {string.Join(", ", allowed)}.");

        _ = await _er.GetTriageAsync(c.TriageId, ct)
            ?? throw new InvalidOperationException("Triage record not found.");

        await _er.SetTriageStatusAsync(c.TriageId, c.Status, ct);
        return true;
    }
}

// ============================ ED board (§3.5) ============================
public sealed record TriageBoardRow(long TriageId, string? Patient, string Category, bool IsMlc, string Status, DateTime ArrivedUtc);
public sealed record GetTriageBoardQuery : IQuery<IReadOnlyList<TriageBoardRow>>;

public sealed class GetTriageBoardHandler : MediatR.IRequestHandler<GetTriageBoardQuery, IReadOnlyList<TriageBoardRow>>
{
    private readonly IEmergencyRepository _er;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;
    public GetTriageBoardHandler(IEmergencyRepository er, IBranchContext ctx, IConfiguration config)
    { _er = er; _ctx = ctx; _config = config; }

    public async Task<IReadOnlyList<TriageBoardRow>> Handle(GetTriageBoardQuery q, CancellationToken ct)
    {
        var rows = await _er.GetBoardAsync(_ctx.BranchId ?? 0, EmergencyConfig.TriageCategories(_config), ct);
        return rows.Select(r => new TriageBoardRow(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6)).ToList();
    }
}

internal static class EmergencyConfig
{
    public static IReadOnlyList<string> TriageCategories(IConfiguration config) =>
        Split(config["Emergency:TriageCategories"], "Red,Yellow,Green");

    public static IReadOnlyList<string> TriageStatuses(IConfiguration config) =>
        Split(config["Emergency:TriageStatuses"], "Waiting,InTreatment,Admitted,Discharged");

    private static IReadOnlyList<string> Split(string? csv, string fallback) =>
        (string.IsNullOrWhiteSpace(csv) ? fallback : csv)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
