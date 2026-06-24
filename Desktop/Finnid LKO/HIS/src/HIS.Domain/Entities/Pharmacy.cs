namespace HIS.Domain.Entities;

/// <summary>Drug batch with expiry + on-hand qty — SRS §3.10.</summary>
public sealed class DrugBatch
{
    public long BatchId { get; set; }
    public int DrugId { get; set; }
    public string BatchNo { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public int QtyOnHand { get; set; }
}

/// <summary>A dispensing event (against a prescription) — SRS §3.10.</summary>
public sealed class Dispense
{
    public long DispenseId { get; set; }
    public long? PrescriptionId { get; set; }
    public int BranchId { get; set; }
    public DateTime DispensedUtc { get; set; }
    public bool IsNdps { get; set; }
}

public sealed class DispenseLine
{
    public long LineId { get; set; }
    public long DispenseId { get; set; }
    public long BatchId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>Supplier master — SRS §3.11.</summary>
public sealed class Supplier
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = "";
    public string? Gstin { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PurchaseOrder
{
    public long PoId { get; set; }
    public string PoNo { get; set; } = "";
    public int BranchId { get; set; }
    public int SupplierId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = "Draft";
}

public sealed class PurchaseOrderLine
{
    public long LineId { get; set; }
    public long PoId { get; set; }
    public int? DrugId { get; set; }
    public string? ItemName { get; set; }
    public int Qty { get; set; }
    public decimal? UnitPrice { get; set; }
}

/// <summary>Equipment / asset with AMC + maintenance schedule — SRS §3.19.</summary>
public sealed class Asset
{
    public long AssetId { get; set; }
    public int BranchId { get; set; }
    public string AssetTag { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public DateTime? AmcExpiry { get; set; }
    public DateTime? NextMaintenance { get; set; }
    public string Status { get; set; } = "Active";
}
