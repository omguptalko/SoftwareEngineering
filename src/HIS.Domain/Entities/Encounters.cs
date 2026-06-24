namespace HIS.Domain.Entities;

/// <summary>Appointment + token — SRS §3.2.</summary>
public sealed class Appointment
{
    public long AppointmentId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }
    public int DoctorId { get; set; }
    public string? Department { get; set; }
    public DateTime SlotStart { get; set; }
    public string? VisitType { get; set; }   // New/Follow-up/Review
    public string? Mode { get; set; }         // Walk-in/Online/Tele-consult
    public string? TokenNo { get; set; }
    public string Status { get; set; } = "Booked";
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Clinical encounter (OPD/IPD/Emergency/Tele) — SRS §3.3.</summary>
public sealed class Encounter
{
    public long EncounterId { get; set; }
    public int BranchId { get; set; }
    public long PatientId { get; set; }
    public int? DoctorId { get; set; }
    public string EncType { get; set; } = "OPD";
    public DateTime StartedUtc { get; set; }
    public string? Complaints { get; set; }
    public string? History { get; set; }
    public string? Advice { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string Status { get; set; } = "Open";
}

/// <summary>Vitals captured in an encounter — SRS §3.3/§3.13.</summary>
public sealed class Vitals
{
    public long VitalsId { get; set; }
    public long EncounterId { get; set; }
    public DateTime RecordedUtc { get; set; }
    public decimal? TempF { get; set; }
    public int? Pulse { get; set; }
    public int? BpSystolic { get; set; }
    public int? BpDiastolic { get; set; }
    public int? Spo2 { get; set; }
    public int? RespRate { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public int? Grbs { get; set; }
}

/// <summary>A prescribed line within an encounter's prescription — SRS §3.3/§3.10.</summary>
public sealed class PrescriptionLine
{
    public long LineId { get; set; }
    public long PrescriptionId { get; set; }
    public int? DrugId { get; set; }
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public int? Days { get; set; }
    public string? Route { get; set; }
    public int? Qty { get; set; }
}
