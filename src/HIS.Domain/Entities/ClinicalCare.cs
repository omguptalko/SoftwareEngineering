namespace HIS.Domain.Entities;

/// <summary>Emergency / ICU triage record — SRS §3.5. Category is config-driven (Red/Yellow/Green).</summary>
public sealed class EmergencyTriage
{
    public long TriageId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }          // null for unknown/unconscious arrivals
    public DateTime ArrivedUtc { get; set; }
    public string Category { get; set; } = "";     // Red/Orange/Yellow/Green/Blue — validated from config
    public bool IsMlc { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Waiting"; // Waiting/InTreatment/Admitted/Discharged
    // M0010 — richer triage (SRS 3.5): acuity + presenting picture + triage vitals.
    public string? ChiefComplaint { get; set; }
    public string? ArrivalMode { get; set; }       // Ambulance/Walk-in/Referral/Police/BroughtDead
    public byte? TriageLevel { get; set; }         // 1..5 (1=Resuscitation .. 5=Non-urgent)
    public byte? PainScore { get; set; }           // 0..10
    public byte? GcsTotal { get; set; }            // 3..15
    public decimal? TempF { get; set; }
    public int? Pulse { get; set; }
    public int? BpSystolic { get; set; }
    public int? BpDiastolic { get; set; }
    public int? Spo2 { get; set; }
    public int? RespRate { get; set; }
    public int? Grbs { get; set; }
    public int? AttendingDoctorId { get; set; }
    public long? AdmissionId { get; set; }         // set when disposed to admission
    public DateTime? DisposedUtc { get; set; }
}

/// <summary>One time-point on the ICU monitoring flowsheet for an admission — SRS §3.6.</summary>
public sealed class IcuObservation
{
    public long IcuObservationId { get; set; }
    public long AdmissionId { get; set; }
    public DateTime RecordedUtc { get; set; }
    public int? HeartRate { get; set; }
    public int? BpSystolic { get; set; }
    public int? BpDiastolic { get; set; }
    public int? Map { get; set; }
    public int? Spo2 { get; set; }
    public int? RespRate { get; set; }
    public decimal? TempF { get; set; }
    public int? Cvp { get; set; }
    public int? EtCo2 { get; set; }
    public int? Fio2 { get; set; }
    public byte? GcsTotal { get; set; }
    public byte? PainScore { get; set; }
    public int? UrineOutputMl { get; set; }
    public int? BloodSugar { get; set; }
    public string? VentMode { get; set; }
    public string? Notes { get; set; }
    public int? RecordedById { get; set; }
}

/// <summary>Nursing care note against an admission — SRS §3.13 (vitals/MAR/handover/care-plan).</summary>
public sealed class NursingNote
{
    public long NoteId { get; set; }
    public long AdmissionId { get; set; }
    public DateTime RecordedUtc { get; set; }
    public string? NoteType { get; set; }          // Vitals/MAR/Handover/CarePlan — validated from config
    public string? Note { get; set; }
}

/// <summary>Operation Theatre schedule + post-op record — SRS §3.12.</summary>
public sealed class OtSchedule
{
    public long OtId { get; set; }
    public int BranchId { get; set; }
    public long PatientId { get; set; }
    public int? SurgeonId { get; set; }
    public string? Theatre { get; set; }
    public DateTime ScheduledUtc { get; set; }
    public string? Procedure { get; set; }         // maps to column Procedure_
    public string? PostOpNotes { get; set; }
    public string Status { get; set; } = "Scheduled"; // Scheduled/InProgress/Completed/Cancelled
}
