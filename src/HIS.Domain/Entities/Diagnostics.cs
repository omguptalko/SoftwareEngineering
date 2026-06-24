namespace HIS.Domain.Entities;

/// <summary>Lab order with barcode — SRS §3.8.</summary>
public sealed class LabOrder
{
    public long LabOrderId { get; set; }
    public string Barcode { get; set; } = "";
    public long? EncounterId { get; set; }
    public long PatientId { get; set; }
    public string TestName { get; set; } = "";
    public DateTime? CollectedUtc { get; set; }
    public string Status { get; set; } = "Awaited";   // Awaited/Received/ResultEntry/Released
}

/// <summary>A single result line for a lab order — SRS §3.8.</summary>
public sealed class LabResult
{
    public long ResultId { get; set; }
    public long LabOrderId { get; set; }
    public string Parameter { get; set; } = "";
    public string? ResultValue { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? Flag { get; set; }                 // Low/High/Normal
    public DateTime? ValidatedUtc { get; set; }
}

/// <summary>Radiology / imaging order — SRS §3.9 (PC-PNDT flagged).</summary>
public sealed class RadiologyOrder
{
    public long RadOrderId { get; set; }
    public long PatientId { get; set; }
    public string Modality { get; set; } = "";        // X-Ray/MRI/CT/USG/ECG
    public string? StudyName { get; set; }
    public DateTime? ScheduledUtc { get; set; }
    public string? ReportUrl { get; set; }
    public int? ReportedByDoctorId { get; set; }
    public bool IsPcPndtRegulated { get; set; }
    public string Status { get; set; } = "Scheduled";
}

/// <summary>Blood bank stock by group — SRS §3.7.</summary>
public sealed class BloodStock
{
    public int BloodStockId { get; set; }
    public int BranchId { get; set; }
    public string BloodGroup { get; set; } = "";
    public int Units { get; set; }
    public int SafetyThreshold { get; set; }
}

/// <summary>Blood request (emergency / routine) — SRS §3.7.</summary>
public sealed class BloodRequest
{
    public long RequestId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }
    public string BloodGroup { get; set; } = "";
    public int Units { get; set; }
    public bool IsEmergency { get; set; }
    public DateTime RequestedUtc { get; set; }
    public string Status { get; set; } = "Requested";
}
