using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Inventory;

// ============================ Inventory (SRS §3.11) ============================
public sealed record LowStockItemDto(string Code, string Name, int Stock, int ReorderLevel);
public sealed record GetLowStockQuery : IQuery<IReadOnlyList<LowStockItemDto>>;

public sealed class GetLowStockHandler : MediatR.IRequestHandler<GetLowStockQuery, IReadOnlyList<LowStockItemDto>>
{
    private readonly IInventoryRepository _inv;
    public GetLowStockHandler(IInventoryRepository inv) { _inv = inv; }

    public async Task<IReadOnlyList<LowStockItemDto>> Handle(GetLowStockQuery q, CancellationToken ct)
    {
        var rows = await _inv.GetLowStockAsync(ct);
        return rows.Select(r => new LowStockItemDto(r.Code, r.Name, r.Stock, r.ReorderLevel)).ToList();
    }
}

public sealed record PoLineDto(string ItemName, int Qty, decimal? UnitPrice);

public sealed record CreatePurchaseOrderCommand(int SupplierId, IReadOnlyList<PoLineDto> Lines)
    : ICommand<CreatePurchaseOrderResult>, IAuditable
{
    public string AuditEntity => "PurchaseOrder";
    public string? AuditEntityId => SupplierId.ToString();
}
public sealed record CreatePurchaseOrderResult(long PoId, string PoNo);

public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty();
    }
}

public sealed class CreatePurchaseOrderHandler : MediatR.IRequestHandler<CreatePurchaseOrderCommand, CreatePurchaseOrderResult>
{
    private readonly IInventoryRepository _inv;
    private readonly IBranchContext _ctx;
    public CreatePurchaseOrderHandler(IInventoryRepository inv, IBranchContext ctx) { _inv = inv; _ctx = ctx; }

    public async Task<CreatePurchaseOrderResult> Handle(CreatePurchaseOrderCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var poNo = await _inv.NextPoNoAsync(branchId, ct);
        var po = new PurchaseOrder { PoNo = poNo, BranchId = branchId, SupplierId = c.SupplierId, CreatedUtc = DateTime.UtcNow, Status = "Draft" };
        var lines = c.Lines.Select(l => new PurchaseOrderLine { ItemName = l.ItemName, Qty = l.Qty, UnitPrice = l.UnitPrice }).ToList();
        var id = await _inv.CreatePoAsync(po, lines, ct);
        return new CreatePurchaseOrderResult(id, poNo);
    }
}

// ============================ Assets (SRS §3.19) ============================
public sealed record AssetDto(long AssetId, string AssetTag, string Name, string? Category, string? AmcExpiry, string? NextMaintenance, string Status, bool AmcDue, bool MaintenanceDue);
public sealed record GetAssetsQuery : IQuery<IReadOnlyList<AssetDto>>;

public sealed class GetAssetsHandler : MediatR.IRequestHandler<GetAssetsQuery, IReadOnlyList<AssetDto>>
{
    private readonly IAssetRepository _assets;
    private readonly IBranchContext _ctx;
    public GetAssetsHandler(IAssetRepository assets, IBranchContext ctx) { _assets = assets; _ctx = ctx; }

    public async Task<IReadOnlyList<AssetDto>> Handle(GetAssetsQuery q, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var rows = await _assets.GetAssetsAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(a => new AssetDto(
            a.AssetId, a.AssetTag, a.Name, a.Category,
            a.AmcExpiry?.ToString("dd-MMM-yyyy"), a.NextMaintenance?.ToString("dd-MMM-yyyy"), a.Status,
            a.AmcExpiry.HasValue && a.AmcExpiry.Value.Date <= today,
            a.NextMaintenance.HasValue && a.NextMaintenance.Value.Date <= today)).ToList();
    }
}

public sealed record RegisterAssetCommand(string AssetTag, string Name, string? Category, DateTime? AmcExpiry, DateTime? NextMaintenance)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "Asset";
    public string? AuditEntityId => AssetTag;
}

public sealed class RegisterAssetValidator : AbstractValidator<RegisterAssetCommand>
{
    public RegisterAssetValidator()
    {
        RuleFor(x => x.AssetTag).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class RegisterAssetHandler : MediatR.IRequestHandler<RegisterAssetCommand, long>
{
    private readonly IAssetRepository _assets;
    private readonly IBranchContext _ctx;
    public RegisterAssetHandler(IAssetRepository assets, IBranchContext ctx) { _assets = assets; _ctx = ctx; }

    public async Task<long> Handle(RegisterAssetCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        return await _assets.InsertAsync(new Asset
        {
            BranchId = branchId, AssetTag = c.AssetTag, Name = c.Name, Category = c.Category,
            AmcExpiry = c.AmcExpiry, NextMaintenance = c.NextMaintenance, Status = "Active"
        }, ct);
    }
}
