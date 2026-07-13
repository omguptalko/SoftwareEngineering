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

// ---- Full stock levels (all active items) ----
public sealed record StockItemDto(string Code, string Name, int Stock, int ReorderLevel, bool BelowReorder);
public sealed record GetStockLevelsQuery : IQuery<IReadOnlyList<StockItemDto>>;

public sealed class GetStockLevelsHandler : MediatR.IRequestHandler<GetStockLevelsQuery, IReadOnlyList<StockItemDto>>
{
    private readonly IInventoryRepository _inv;
    public GetStockLevelsHandler(IInventoryRepository inv) { _inv = inv; }

    public async Task<IReadOnlyList<StockItemDto>> Handle(GetStockLevelsQuery q, CancellationToken ct)
    {
        var rows = await _inv.GetStockLevelsAsync(ct);
        return rows.Select(r => new StockItemDto(r.Code, r.Name, r.Stock, r.ReorderLevel, r.Stock <= r.ReorderLevel)).ToList();
    }
}

// ---- Suppliers (for the PO form) ----
public sealed record SupplierDto(int SupplierId, string Name, string? Gstin);
public sealed record GetSuppliersQuery : IQuery<IReadOnlyList<SupplierDto>>;

public sealed class GetSuppliersHandler : MediatR.IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly IInventoryRepository _inv;
    public GetSuppliersHandler(IInventoryRepository inv) { _inv = inv; }

    public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery q, CancellationToken ct)
    {
        var rows = await _inv.GetSuppliersAsync(ct);
        return rows.Select(s => new SupplierDto(s.SupplierId, s.Name, s.Gstin)).ToList();
    }
}

// ---- Add a supplier (dynamic supplier master) ----
public sealed record AddSupplierCommand(string Name, string? Gstin) : ICommand<int>, IAuditable
{
    public string AuditEntity => "Supplier";
    public string? AuditEntityId => Name;
}

public sealed class AddSupplierValidator : AbstractValidator<AddSupplierCommand>
{
    public AddSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Gstin).MaximumLength(20);
    }
}

public sealed class AddSupplierHandler : MediatR.IRequestHandler<AddSupplierCommand, int>
{
    private readonly IInventoryRepository _inv;
    public AddSupplierHandler(IInventoryRepository inv) { _inv = inv; }
    public Task<int> Handle(AddSupplierCommand c, CancellationToken ct)
        => _inv.InsertSupplierAsync(c.Name.Trim(), string.IsNullOrWhiteSpace(c.Gstin) ? null : c.Gstin.Trim(), ct);
}

// ---- Purchase-order list ----
public sealed record PurchaseOrderRowDto(long PoId, string PoNo, string? Supplier, int Lines, decimal Total, string Status, DateTime CreatedUtc);
public sealed record GetPurchaseOrdersQuery : IQuery<IReadOnlyList<PurchaseOrderRowDto>>;

public sealed class GetPurchaseOrdersHandler : MediatR.IRequestHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderRowDto>>
{
    private readonly IInventoryRepository _inv;
    private readonly IBranchContext _ctx;
    public GetPurchaseOrdersHandler(IInventoryRepository inv, IBranchContext ctx) { _inv = inv; _ctx = ctx; }

    public async Task<IReadOnlyList<PurchaseOrderRowDto>> Handle(GetPurchaseOrdersQuery q, CancellationToken ct)
    {
        var rows = await _inv.GetPurchaseOrdersAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new PurchaseOrderRowDto(r.PoId, r.PoNo, r.Supplier, r.Lines, r.Total, r.Status, r.CreatedUtc)).ToList();
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
