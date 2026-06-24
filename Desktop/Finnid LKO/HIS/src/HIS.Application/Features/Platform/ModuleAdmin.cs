using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Application.Features.Platform;

// ============================ Create module (module.manage) ============================
public sealed record CreateModuleCommand(string Code, string Label, string? Icon, int SortOrder)
    : ICommand<int>, IAuthorizable
{
    public string RequiredPermission => "module.manage";
}
public sealed class CreateModuleValidator : AbstractValidator<CreateModuleCommand>
{
    public CreateModuleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
    }
}
public sealed class CreateModuleHandler : MediatR.IRequestHandler<CreateModuleCommand, int>
{
    private readonly IModuleAdminRepository _repo;
    public CreateModuleHandler(IModuleAdminRepository repo) => _repo = repo;
    public Task<int> Handle(CreateModuleCommand c, CancellationToken ct) =>
        _repo.CreateModuleAsync(c.Code, c.Label, c.Icon, c.SortOrder, ct);
}

// ============================ Create page (module.manage) ============================
public sealed record CreatePageCommand(string ModuleCode, string Code, string Label, string? Route, int SortOrder)
    : ICommand<int>, IAuthorizable
{
    public string RequiredPermission => "module.manage";
}
public sealed class CreatePageValidator : AbstractValidator<CreatePageCommand>
{
    public CreatePageValidator()
    {
        RuleFor(x => x.ModuleCode).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
    }
}
public sealed class CreatePageHandler : MediatR.IRequestHandler<CreatePageCommand, int>
{
    private readonly IModuleAdminRepository _repo;
    public CreatePageHandler(IModuleAdminRepository repo) => _repo = repo;
    public Task<int> Handle(CreatePageCommand c, CancellationToken ct) =>
        _repo.CreatePageAsync(c.ModuleCode, c.Code, c.Label, c.Route, c.SortOrder, ct);
}

// ============================ Assign module/page to role (rbac.manage) ============================
public sealed record AssignModuleToRoleCommand(string RoleCode, string ModuleCode) : ICommand<bool>, IAuthorizable
{
    public string RequiredPermission => "rbac.manage";
}
public sealed class AssignModuleToRoleHandler : MediatR.IRequestHandler<AssignModuleToRoleCommand, bool>
{
    private readonly IModuleAdminRepository _repo;
    public AssignModuleToRoleHandler(IModuleAdminRepository repo) => _repo = repo;
    public Task<bool> Handle(AssignModuleToRoleCommand c, CancellationToken ct) =>
        _repo.AssignModuleToRoleAsync(c.RoleCode, c.ModuleCode, ct);
}

public sealed record AssignPageToRoleCommand(string RoleCode, string PageCode) : ICommand<bool>, IAuthorizable
{
    public string RequiredPermission => "rbac.manage";
}
public sealed class AssignPageToRoleHandler : MediatR.IRequestHandler<AssignPageToRoleCommand, bool>
{
    private readonly IModuleAdminRepository _repo;
    public AssignPageToRoleHandler(IModuleAdminRepository repo) => _repo = repo;
    public Task<bool> Handle(AssignPageToRoleCommand c, CancellationToken ct) =>
        _repo.AssignPageToRoleAsync(c.RoleCode, c.PageCode, ct);
}

// ============================ Effective menu (authenticated) ============================
public sealed record MenuPageDto(string Code, string Label, string? Route);
public sealed record MenuModuleDto(string Code, string Label, string? Icon, IReadOnlyList<MenuPageDto> Pages);

/// <summary>The modules/pages the current user may see. Superadmin sees all; others
/// see only what their roles are granted (RoleModule/RolePage). Self-scoped (L1.3.5).</summary>
public sealed record GetMyMenuQuery : IQuery<IReadOnlyList<MenuModuleDto>>, IRequireAuthentication;

public sealed class GetMyMenuHandler : MediatR.IRequestHandler<GetMyMenuQuery, IReadOnlyList<MenuModuleDto>>
{
    private readonly IModuleAdminRepository _repo;
    private readonly IBranchContext _ctx;
    public GetMyMenuHandler(IModuleAdminRepository repo, IBranchContext ctx) { _repo = repo; _ctx = ctx; }

    public async Task<IReadOnlyList<MenuModuleDto>> Handle(GetMyMenuQuery q, CancellationToken ct)
    {
        var rows = _ctx.IsSuperAdmin
            ? await _repo.GetFullMenuAsync(ct)
            : await _repo.GetMenuForRolesAsync(_ctx.Roles, ct);

        return rows
            .GroupBy(r => (r.Item1, r.Item2, r.Item3, r.Item4))   // ModuleCode, Label, Icon, Sort
            .OrderBy(g => g.Key.Item4)
            .Select(g => new MenuModuleDto(
                g.Key.Item1, g.Key.Item2, g.Key.Item3,
                g.Select(p => new MenuPageDto(p.Item5, p.Item6, p.Item7)).ToList()))
            .ToList();
    }
}
