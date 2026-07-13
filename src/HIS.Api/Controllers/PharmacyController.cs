using HIS.Application.Features.Inventory;
using HIS.Application.Features.Pharmacy;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/pharmacy")]
public sealed class PharmacyController : ControllerBase
{
    private readonly IMediator _mediator;
    public PharmacyController(IMediator mediator) => _mediator = mediator;

    [HttpGet("queue")]
    public Task<IReadOnlyList<RxQueueItemDto>> Queue(CancellationToken ct) => _mediator.Send(new GetPrescriptionQueueQuery(), ct);

    [HttpGet("batches")]
    public Task<IReadOnlyList<DrugBatchDto>> Batches([FromQuery] string drugCode, CancellationToken ct) => _mediator.Send(new GetDrugBatchesQuery(drugCode), ct);

    [HttpPost("dispense")]
    public Task<DispenseResult> Dispense([FromBody] DispenseCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;
    public InventoryController(IMediator mediator) => _mediator = mediator;

    [HttpGet("lowstock")]
    public Task<IReadOnlyList<LowStockItemDto>> LowStock(CancellationToken ct) => _mediator.Send(new GetLowStockQuery(), ct);

    /// <summary>Full stock levels for all active items (with a below-reorder flag).</summary>
    [HttpGet("stock")]
    public Task<IReadOnlyList<StockItemDto>> Stock(CancellationToken ct) => _mediator.Send(new GetStockLevelsQuery(), ct);

    /// <summary>Suppliers for the purchase-order form.</summary>
    [HttpGet("suppliers")]
    public Task<IReadOnlyList<SupplierDto>> Suppliers(CancellationToken ct) => _mediator.Send(new GetSuppliersQuery(), ct);

    /// <summary>Add a supplier (dynamic supplier master, idempotent by name).</summary>
    [HttpPost("suppliers")]
    public Task<int> AddSupplier([FromBody] AddSupplierCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    /// <summary>Purchase orders raised (newest first).</summary>
    [HttpGet("purchase-orders")]
    public Task<IReadOnlyList<PurchaseOrderRowDto>> PurchaseOrders(CancellationToken ct) => _mediator.Send(new GetPurchaseOrdersQuery(), ct);

    [HttpPost("purchase-orders")]
    public Task<CreatePurchaseOrderResult> CreatePo([FromBody] CreatePurchaseOrderCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}

[ApiController]
[Route("api/assets")]
public sealed class AssetsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AssetsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public Task<IReadOnlyList<AssetDto>> List(CancellationToken ct) => _mediator.Send(new GetAssetsQuery(), ct);

    [HttpPost]
    public Task<long> Register([FromBody] RegisterAssetCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);
}
