using FluentValidation;
using HIS.Application.Abstractions;

namespace HIS.Application.Features.Opd;

/// <summary>A department's OPD consult template: the extra clinical fields shown for that specialty.</summary>
public sealed record OpdTemplateDto(string Department, IReadOnlyList<string> Fields);

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
                   .Select(g => new OpdTemplateDto(g.Key, g.OrderBy(x => x.SortOrder).Select(x => x.Label).ToList()))
                   .OrderBy(t => t.Department)
                   .ToList();
    }
}

// ---------------------------- Save (admin-configurable) ----------------------------
public sealed record SaveOpdTemplateCommand(string Department, IReadOnlyList<string> Fields)
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
        await _enc.ReplaceDeptTemplateAsync(c.Department, c.Fields ?? new List<string>(), ct);
        return true;
    }
}
