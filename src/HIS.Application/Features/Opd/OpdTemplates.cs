using FluentValidation;
using HIS.Application.Abstractions;

namespace HIS.Application.Features.Opd;

/// <summary>One template field: a labelled input of a given type (text/number/checkbox/select).</summary>
public sealed record OpdTemplateFieldDto(string Label, string FieldType, IReadOnlyList<string> Options);
/// <summary>A department's OPD consult template: the extra clinical fields shown for that specialty.</summary>
public sealed record OpdTemplateDto(string Department, IReadOnlyList<OpdTemplateFieldDto> Fields);

// ---------------------------- Read ----------------------------
public sealed record GetOpdTemplatesQuery : IQuery<IReadOnlyList<OpdTemplateDto>>, IRequireAuthentication;

public sealed class GetOpdTemplatesHandler : MediatR.IRequestHandler<GetOpdTemplatesQuery, IReadOnlyList<OpdTemplateDto>>
{
    private readonly IEncounterRepository _enc;
    public GetOpdTemplatesHandler(IEncounterRepository enc) { _enc = enc; }
    public async Task<IReadOnlyList<OpdTemplateDto>> Handle(GetOpdTemplatesQuery q, CancellationToken ct)
    {
        var rows = await _enc.ListDeptTemplatesAsync(ct);
        return rows.GroupBy(r => r.Department)
                   .Select(g => new OpdTemplateDto(g.Key, g.OrderBy(x => x.SortOrder).Select(x => new OpdTemplateFieldDto(
                       x.Label, x.FieldType,
                       string.IsNullOrWhiteSpace(x.Options) ? System.Array.Empty<string>()
                           : x.Options.Split(',').Select(o => o.Trim()).Where(o => o.Length > 0).ToList())).ToList()))
                   .OrderBy(t => t.Department)
                   .ToList();
    }
}

// ---------------------------- Save (admin-configurable) ----------------------------
public sealed record OpdTemplateFieldInput(string Label, string? FieldType, IReadOnlyList<string>? Options);

public sealed record SaveOpdTemplateCommand(string Department, IReadOnlyList<OpdTemplateFieldInput> Fields)
    : ICommand<bool>, IAuditable, IAuthorizable
{
    public string AuditEntity => "DeptTemplate";
    public string? AuditEntityId => Department;
    public string RequiredPermission => "opd.templates.manage";   // hospital admin / superadmin
}

public sealed class SaveOpdTemplateValidator : AbstractValidator<SaveOpdTemplateCommand>
{
    public SaveOpdTemplateValidator() => RuleFor(x => x.Department).NotEmpty();
}

public sealed class SaveOpdTemplateHandler : MediatR.IRequestHandler<SaveOpdTemplateCommand, bool>
{
    private readonly IEncounterRepository _enc;
    public SaveOpdTemplateHandler(IEncounterRepository enc) { _enc = enc; }
    public async Task<bool> Handle(SaveOpdTemplateCommand c, CancellationToken ct)
    {
        var fields = (c.Fields ?? new List<OpdTemplateFieldInput>())
            .Select(f => (f.Label, f.FieldType ?? "text",
                          (f.Options == null || f.Options.Count == 0) ? (string?)null : string.Join(",", f.Options)))
            .ToList();
        await _enc.ReplaceDeptTemplateAsync(c.Department, fields, ct);
        return true;
    }
}

// ---------------------------- Read structured answers for an encounter ----------------------------
public sealed record GetEncounterTemplateDataQuery(long EncounterId) : IQuery<IReadOnlyList<TemplateAnswerDto>>, IRequireAuthentication;

public sealed class GetEncounterTemplateDataHandler : MediatR.IRequestHandler<GetEncounterTemplateDataQuery, IReadOnlyList<TemplateAnswerDto>>
{
    private readonly IEncounterRepository _enc;
    public GetEncounterTemplateDataHandler(IEncounterRepository enc) { _enc = enc; }
    public async Task<IReadOnlyList<TemplateAnswerDto>> Handle(GetEncounterTemplateDataQuery q, CancellationToken ct)
    {
        var rows = await _enc.GetTemplateAnswersAsync(q.EncounterId, ct);
        return rows.Select(r => new TemplateAnswerDto(r.Label, r.FieldType, r.Value)).ToList();
    }
}
