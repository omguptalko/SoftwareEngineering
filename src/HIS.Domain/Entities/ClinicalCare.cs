namespace HIS.Domain.Entities;

/// <summary>Emergency / ICU triage record — SRS §3.5. Category is config-driven (Red/Yellow/Green).</summary>
public sealed class EmergencyTriage
{
    public long TriageId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }          // null for unknown/unconscious arrivals
    public DateTime ArrivedUtc { get; set; }
    public string Category { get; set; } = "";     // Red/Yellow/Green — validated from config
    public bool IsMlc { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Waiting"; // Waiting/InTreatment/Admitted/Discharged
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
