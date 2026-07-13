using FluentValidation;
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

// ============================ Add / update a branch ============================
/// <summary>Create (BranchId null/0) or update a branch in the tenant's master DB.
/// Code is immutable on update (it seeds every UHID for that branch). A new branch is a
/// row in the shared master.Branch — no new database is provisioned; branches of one tenant
/// stay strongly consistent by sharing the master DB (SRS — Multi-Branch Sync).</summary>
public sealed record SaveBranchCommand(int? BranchId, string Code, string Name, string? City, string? State)
    : ICommand<int>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Branch";
    public string? AuditEntityId => Code;
    public string RequiredPermission => "masters.manage";
}

public sealed class SaveBranchValidator : AbstractValidator<SaveBranchCommand>
{
    public SaveBranchValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Code may use letters, digits and - only (max 10 chars).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).MaximumLength(80);
    }
}

public sealed class SaveBranchHandler : MediatR.IRequestHandler<SaveBranchCommand, int>
{
    private readonly IBranchRepository _branches;
    public SaveBranchHandler(IBranchRepository branches) => _branches = branches;

    public async Task<int> Handle(SaveBranchCommand c, CancellationToken ct)
    {
        var isNew = c.BranchId is null or 0;
        if (await _branches.CodeExistsAsync(c.Code, isNew ? null : c.BranchId, ct))
            throw new InvalidOperationException($"A branch with code '{c.Code}' already exists.");

        if (isNew)
            return await _branches.InsertAsync(c.Code, c.Name, c.City, c.State, ct);

        var ok = await _branches.UpdateAsync(c.BranchId!.Value, c.Name, c.City, c.State, ct);
        if (!ok) throw new InvalidOperationException($"Branch '{c.BranchId}' not found.");
        return c.BranchId.Value;
    }
}

// ============================ Activate / deactivate a branch ============================
public sealed record SetBranchActiveCommand(int BranchId, bool IsActive) : ICommand<bool>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Branch";
    public string? AuditEntityId => BranchId.ToString();
    public string RequiredPermission => "masters.manage";
}

public sealed class SetBranchActiveHandler : MediatR.IRequestHandler<SetBranchActiveCommand, bool>
{
    private readonly IBranchRepository _branches;
    public SetBranchActiveHandler(IBranchRepository branches) => _branches = branches;

    public async Task<bool> Handle(SetBranchActiveCommand c, CancellationToken ct)
    {
        var ok = await _branches.SetActiveAsync(c.BranchId, c.IsActive, ct);
        if (!ok) throw new InvalidOperationException($"Branch '{c.BranchId}' not found.");
        return true;
    }
}
