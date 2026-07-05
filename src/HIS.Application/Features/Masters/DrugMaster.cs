using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Application.Features.Masters;

// ============================ Drug master admin (drug list) ============================
public sealed record DrugDto(int DrugId, string Code, string Name, string Form, int StockQty, int ReorderLevel, bool IsActive);

public sealed record GetDrugsAdminQuery : IQuery<IReadOnlyList<DrugDto>>, IRequireAuthentication;

public sealed class GetDrugsAdminHandler : MediatR.IRequestHandler<GetDrugsAdminQuery, IReadOnlyList<DrugDto>>
{
    private readonly ILookupRepository _lk;
    public GetDrugsAdminHandler(ILookupRepository lk) => _lk = lk;

    public async Task<IReadOnlyList<DrugDto>> Handle(GetDrugsAdminQuery q, CancellationToken ct)
    {
        var rows = await _lk.GetAllDrugsAsync(ct);
        return rows.Select(d => new DrugDto(d.DrugId, d.Code, d.Name, d.Form, d.StockQty, d.ReorderLevel, d.IsActive)).ToList();
    }
}

// ============================ Add / update a drug ============================
/// <summary>Create (DrugId null/0) or update a drug in the master. Code is immutable on update.</summary>
public sealed record SaveDrugCommand(int? DrugId, string Code, string Name, string Form, int ReorderLevel)
    : ICommand<int>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Drug";
    public string? AuditEntityId => Code;
    public string RequiredPermission => "masters.manage";
}

public sealed class SaveDrugValidator : AbstractValidator<SaveDrugCommand>
{
    public SaveDrugValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Code may use letters, digits and - only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Form).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public sealed class SaveDrugHandler : MediatR.IRequestHandler<SaveDrugCommand, int>
{
    private readonly ILookupRepository _lk;
    public SaveDrugHandler(ILookupRepository lk) => _lk = lk;

    public async Task<int> Handle(SaveDrugCommand c, CancellationToken ct)
    {
        var isNew = c.DrugId is null or 0;
        if (await _lk.DrugCodeExistsAsync(c.Code, isNew ? null : c.DrugId, ct))
            throw new InvalidOperationException($"A drug with code '{c.Code}' already exists.");

        if (isNew)
            return await _lk.InsertDrugAsync(new Drug
            {
                Code = c.Code, Name = c.Name, Form = c.Form, StockQty = 0, ReorderLevel = c.ReorderLevel, IsActive = true
            }, ct);

        await _lk.UpdateDrugAsync(new Drug { DrugId = c.DrugId!.Value, Name = c.Name, Form = c.Form, ReorderLevel = c.ReorderLevel }, ct);
        return c.DrugId.Value;
    }
}

// ============================ Activate / deactivate a drug ============================
public sealed record SetDrugActiveCommand(int DrugId, bool IsActive) : ICommand<bool>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Drug";
    public string? AuditEntityId => DrugId.ToString();
    public string RequiredPermission => "masters.manage";
}

public sealed class SetDrugActiveHandler : MediatR.IRequestHandler<SetDrugActiveCommand, bool>
{
    private readonly ILookupRepository _lk;
    public SetDrugActiveHandler(ILookupRepository lk) => _lk = lk;

    public async Task<bool> Handle(SetDrugActiveCommand c, CancellationToken ct)
    {
        await _lk.SetDrugActiveAsync(c.DrugId, c.IsActive, ct);
        return true;
    }
}
