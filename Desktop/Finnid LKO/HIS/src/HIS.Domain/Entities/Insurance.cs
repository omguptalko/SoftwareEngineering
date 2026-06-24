namespace HIS.Domain.Entities;

/// <summary>Patient insurance policy + caps — SRS §3.15. Caps/co-pay are data, not code.</summary>
public sealed class InsurancePolicy
{
    public long PolicyId { get; set; }
    public long PatientId { get; set; }
    public int PayerId { get; set; }
    public string? PolicyNo { get; set; }
    public string? MemberId { get; set; }
    public decimal? SumInsured { get; set; }
    public decimal? AvailableBalance { get; set; }
    public decimal? RoomRentCapPerDay { get; set; }
    public decimal? CoPayPct { get; set; }
    public DateTime? ValidTo { get; set; }
}

/// <summary>Cashless claim — SRS §7.1. Lifecycle drives Status.</summary>
public sealed class Claim
{
    public long ClaimId { get; set; }
    public string ClaimNo { get; set; } = "";
    public int BranchId { get; set; }
    public long PatientId { get; set; }
    public int PayerId { get; set; }
    public long? PolicyId { get; set; }
    public long? AdmissionId { get; set; }
    public string? Channel { get; set; }            // NHCX/TPA Portal/TMS/ESIC e-bill…
    public string? ProvisionalIcd10 { get; set; }
    public decimal? PreAuthAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? SettledAmount { get; set; }
    public string Status { get; set; } = "Eligibility";
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? TatDueUtc { get; set; }
}

public sealed class ClaimEvent
{
    public long EventId { get; set; }
    public long ClaimId { get; set; }
    public string EventType { get; set; } = "";     // PreAuth/Query/Shortfall/Enhancement/FinalBill/Approval/Denial/Settlement/Appeal
    public decimal? Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredUtc { get; set; }
}

public sealed class ClaimDocument
{
    public long DocId { get; set; }
    public long ClaimId { get; set; }
    public string DocType { get; set; } = "";
    public string? DocUrl { get; set; }
    public bool IsMandatory { get; set; }
    public bool Attached { get; set; }
}

/// <summary>PM-JAY beneficiary (BIS) — SRS §7.3.</summary>
public sealed class PmjayBeneficiary
{
    public long BeneficiaryId { get; set; }
    public long PatientId { get; set; }
    public string? PmjayId { get; set; }
    public bool BisVerified { get; set; }
    public decimal? FamilyFloater { get; set; }
    public decimal? UsedAmount { get; set; }
}

public sealed class PmjayCase
{
    public long CaseId { get; set; }
    public long ClaimId { get; set; }
    public int? PackageId { get; set; }
    public string? TmsCaseNo { get; set; }
    public string? AyushmanMitra { get; set; }
    public bool AadhaarDischargeVerified { get; set; }
}

/// <summary>ESIC/CGHS/ECHS/State membership — SRS §7.4–§7.7.</summary>
public sealed class SchemeMembership
{
    public long MembershipId { get; set; }
    public long PatientId { get; set; }
    public string SchemeType { get; set; } = "";    // ESIC/CGHS/ECHS/State
    public string? MemberNo { get; set; }
    public string? SecondaryRef { get; set; }        // Pehchan / referral / permission-letter
    public bool Verified { get; set; }
    public DateTime? ValidTo { get; set; }
}

public sealed class SchemePackage
{
    public int SchemePackageId { get; set; }
    public string SchemeType { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SettlementReconciliation
{
    public long ReconId { get; set; }
    public long? ClaimId { get; set; }
    public string? Utr { get; set; }
    public decimal? BankAmount { get; set; }
    public DateTime? ReconciledUtc { get; set; }
    public string Status { get; set; } = "Unmatched";
}
