using HIS.Application.Abstractions;

namespace HIS.Application.Features.Masters;

// ============================ Branch directory (Multi-Branch Sync) ============================
public sealed record BranchRowDto(int BranchId, string Code, string Name, string? City, string? State, bool IsActive);

public sealed record GetBranchesQuery : IQuery<IReadOnlyList<BranchRowDto>>, IRequireAuthentication;

public sealed class GetBranchesHandler : MediatR.IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchRowDto>>
{
    private readonly IBranchRepository _branches;
    public GetBranchesHandler(IBranchRepository branches) => _branches = branches;

    public async Task<IReadOnlyList<BranchRowDto>> Handle(GetBranchesQuery q, CancellationToken ct)
    {
        var rows = await _branches.GetAllAsync(ct);
        return rows.Select(b => new BranchRowDto(b.BranchId, b.Code, b.Name, b.City, b.State, b.IsActive)).ToList();
    }
}
