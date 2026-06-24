namespace HIS.Domain.Entities;

/// <summary>Drug/formulary master with live stock — SRS §3.10/§3.11 (was static drug lookup).</summary>
public sealed class Drug
{
    public int DrugId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Form { get; set; } = "";        // TAB/CAP/INJ/IVF
    public int StockQty { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>ICD-10 reference (SRS §7.1). Reference data lives in DB, never in code.</summary>
public sealed class Icd10Code
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class Ward
{
    public int WardId { get; set; }
    public int BranchId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Bed
{
    public int BedId { get; set; }
    public int WardId { get; set; }
    public string BedNo { get; set; } = "";
    public string Status { get; set; } = "free";  // free/occ/clean/block
}

/// <summary>Payer/insurer/TPA/scheme empanelment master — SRS §3.15/§7 (was static payer lookup).</summary>
public sealed class Payer
{
    public int PayerId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string PayerType { get; set; } = "";   // Private Insurer/TPA/Govt Scheme/Corporate
    public bool IsActive { get; set; } = true;
}

/// <summary>PM-JAY Health Benefit Package master — SRS §7.3. Rates admin-editable, never hardcoded.</summary>
public sealed class HbpPackage
{
    public int PackageId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Specialty { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Blood group reference (SRS §3.7).</summary>
public sealed class BloodGroup
{
    public string Code { get; set; } = "";
    public int SortOrder { get; set; }
}
