using HIS.Application.Abstractions;

namespace HIS.Application.Features.Meta;

public sealed record ModuleGroupDto(string Id, string Label, string Icon);
public sealed record ModuleDto(string Id, string Group, string Icon, string Label, bool Built, string? Badge);
public sealed record ModuleRegistryDto(IReadOnlyList<ModuleGroupDto> Groups, IReadOnlyList<ModuleDto> Modules);

/// <summary>
/// Returns the sidebar groups + module registry from the DB — replaces the static
/// HIS.groups / HIS.modules arrays that used to live in data.js.
/// </summary>
public sealed record GetModuleRegistryQuery : IQuery<ModuleRegistryDto>;

public sealed class GetModuleRegistryHandler : MediatR.IRequestHandler<GetModuleRegistryQuery, ModuleRegistryDto>
{
    private readonly IModuleRegistryRepository _repo;
    public GetModuleRegistryHandler(IModuleRegistryRepository repo) => _repo = repo;

    public async Task<ModuleRegistryDto> Handle(GetModuleRegistryQuery request, CancellationToken ct)
    {
        var groups = await _repo.GetGroupsAsync(ct);
        var modules = await _repo.GetModulesAsync(ct);
        return new ModuleRegistryDto(
            groups.Select(g => new ModuleGroupDto(g.GroupId, g.Label, g.Icon)).ToList(),
            modules.Select(m => new ModuleDto(m.ModuleId, m.GroupId, m.Icon, m.Label, m.Built, m.Badge)).ToList());
    }
}
