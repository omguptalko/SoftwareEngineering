namespace HIS.Domain.Entities;

/// <summary>IPD admission — SRS §3.4.</summary>
public sealed class Admission
{
    public long AdmissionId { get; set; }
    public string AdmissionNo { get; set; } = "";
    public int BranchId { get; set; }
    public long PatientId { get; set; }
    public int? BedId { get; set; }
    public int? ConsultantId { get; set; }
    public DateTime AdmittedUtc { get; set; }
    public string? AdmissionType { get; set; }     // Planned/Emergency/DayCare/Transfer-in
    public string? PaymentClass { get; set; }       // Cashless/PM-JAY/ESIC/Cash/Corporate
    public string? ProvisionalIcd10 { get; set; }
    public int? EstStayDays { get; set; }
    public DateTime? DischargedUtc { get; set; }
    public string? DischargeSummary { get; set; }
    public string Status { get; set; } = "Admitted";
}

/// <summary>Bed transfer between wards/beds during a stay — SRS §3.4.</summary>
public sealed class BedTransfer
{
    public long TransferId { get; set; }
    public long AdmissionId { get; set; }
    public int? FromBedId { get; set; }
    public int? ToBedId { get; set; }
    public DateTime TransferUtc { get; set; }
    public string? Reason { get; set; }
}
