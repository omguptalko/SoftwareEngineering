namespace HIS.Domain.Entities;

/// <summary>Service/price master — SRS §3.14. Rates/GST are master data, never hardcoded.</summary>
public sealed class Tariff
{
    public int TariffId { get; set; }
    public int? BranchId { get; set; }
    public string ServiceCode { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string? Category { get; set; }
    public decimal Rate { get; set; }
    public decimal GstRatePct { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Consolidated bill — SRS §3.14.</summary>
public sealed class Bill
{
    public long BillId { get; set; }
    public string BillNo { get; set; } = "";
    public int BranchId { get; set; }
    public long PatientId { get; set; }
    public long? AdmissionId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal InsurancePays { get; set; }
    public decimal PatientPays { get; set; }
    public string Status { get; set; } = "Open";
}

/// <summary>Bill line. Amount is a computed column in the DB (Qty * Rate).</summary>
public sealed class BillLine
{
    public long LineId { get; set; }
    public long BillId { get; set; }
    public int? TariffId { get; set; }
    public string Description { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>Payment against a bill — SRS §5. Gateway keys live in config/Key Vault.</summary>
/// <summary>Accrued-but-unbilled charge (billing Phase 2) — e.g. a doctor's consultation fee
/// captured at consult/admission time; pulled into a bill later (BilledBillId set).</summary>
public sealed class PendingCharge
{
    public long ChargeId { get; set; }
    public int? BranchId { get; set; }
    public long PatientId { get; set; }
    public long? AdmissionId { get; set; }
    public string Source { get; set; } = "";
    public string Description { get; set; } = "";
    public int? DoctorId { get; set; }
    public int? TariffId { get; set; }
    public decimal Qty { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public long? BilledBillId { get; set; }
}

public sealed class Payment
{
    public long PaymentId { get; set; }
    public long? BillId { get; set; }
    public long PatientId { get; set; }
    public string Mode { get; set; } = "";      // UPI/Card/NetBanking/QR/Cash
    public string? Gateway { get; set; }         // Razorpay/Stripe/PayU/Cashfree/Sandbox
    public decimal Amount { get; set; }
    public string? GatewayRef { get; set; }
    public string Status { get; set; } = "Initiated";
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Patient advance/deposit — SRS §5.</summary>
public sealed class PatientDeposit
{
    public long DepositId { get; set; }
    public long PatientId { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedUtc { get; set; }
}
