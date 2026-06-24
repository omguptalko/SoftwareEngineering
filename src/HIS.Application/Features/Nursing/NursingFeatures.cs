using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Nursing;

// ============================ Add nursing note (§3.13) ============================
/// <summary>Record a nursing note against an active admission — vitals/MAR/handover/care-plan.
/// Note type validated against the config-driven list (Nursing:NoteTypes).</summary>
public sealed record AddNursingNoteCommand(long AdmissionId, string NoteType, string? Note)
    : ICommand<AddNursingNoteResult>, IAuditable
{
    public string AuditEntity => "NursingNote";
    public string? AuditEntityId => AdmissionId.ToString();
}
public sealed record AddNursingNoteResult(long NoteId, string NoteType);

public sealed class AddNursingNoteValidator : AbstractValidator<AddNursingNoteCommand>
{
    public AddNursingNoteValidator()
    {
        RuleFor(x => x.AdmissionId).GreaterThan(0);
        RuleFor(x => x.NoteType).NotEmpty();
    }
}

public sealed class AddNursingNoteHandler : MediatR.IRequestHandler<AddNursingNoteCommand, AddNursingNoteResult>
{
    private readonly INursingRepository _nr;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;
    public AddNursingNoteHandler(INursingRepository nr, IBranchContext ctx, IConfiguration config)
    { _nr = nr; _ctx = ctx; _config = config; }

    public async Task<AddNursingNoteResult> Handle(AddNursingNoteCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");

        var types = NursingConfig.NoteTypes(_config);
        if (!types.Any(t => string.Equals(t, c.NoteType, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Invalid note type '{c.NoteType}'. Allowed: {string.Join(", ", types)}.");

        if (!await _nr.AdmissionExistsAsync(branchId, c.AdmissionId, ct))
            throw new InvalidOperationException($"Admission {c.AdmissionId} not found in this branch.");

        var id = await _nr.InsertNoteAsync(new NursingNote
        {
            AdmissionId = c.AdmissionId,
            RecordedUtc = DateTime.UtcNow,
            NoteType = c.NoteType,
            Note = c.Note
        }, ct);

        return new AddNursingNoteResult(id, c.NoteType);
    }
}

// ============================ Nursing notes timeline (§3.13) ============================
public sealed record NursingNoteRow(long NoteId, string? NoteType, string? Note, DateTime RecordedUtc);
public sealed record GetNursingNotesQuery(long AdmissionId) : IQuery<IReadOnlyList<NursingNoteRow>>;

public sealed class GetNursingNotesHandler : MediatR.IRequestHandler<GetNursingNotesQuery, IReadOnlyList<NursingNoteRow>>
{
    private readonly INursingRepository _nr;
    public GetNursingNotesHandler(INursingRepository nr) => _nr = nr;

    public async Task<IReadOnlyList<NursingNoteRow>> Handle(GetNursingNotesQuery q, CancellationToken ct)
    {
        var rows = await _nr.GetNotesAsync(q.AdmissionId, ct);
        return rows.Select(r => new NursingNoteRow(r.Item1, r.Item2, r.Item3, r.Item4)).ToList();
    }
}

internal static class NursingConfig
{
    public static IReadOnlyList<string> NoteTypes(IConfiguration config) =>
        (string.IsNullOrWhiteSpace(config["Nursing:NoteTypes"]) ? "Vitals,MAR,Handover,CarePlan" : config["Nursing:NoteTypes"]!)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
