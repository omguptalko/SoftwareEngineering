namespace HIS.Domain.Entities;

/// <summary>Employer / company health contract — SRS §3.23.</summary>
public sealed class CompanyContract
{
    public int ContractId { get; set; }
    public string CompanyName { get; set; } = "";
    public string? PayerCode { get; set; }
    public string? ContractType { get; set; }   // PEME/PME/Corporate
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Pre-employment / periodic medical exam — SRS §3.23 (Factories Act 1948).</summary>
public sealed class MedicalExam
{
    public long ExamId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }
    public int? ContractId { get; set; }
    public string ExamType { get; set; } = "";       // PEME/PME
    public DateTime ExamDate { get; set; }
    public string? FitnessResult { get; set; }        // Fit/Unfit/Fit-with-conditions
    public string? Audiometry { get; set; }
    public string? Spirometry { get; set; }
    public string? Vision { get; set; }
    public string? VaccinationNotes { get; set; }
}

/// <summary>Occupational hazard / exposure record — SRS §3.23.</summary>
public sealed class HazardExposure
{
    public long ExposureId { get; set; }
    public long PatientId { get; set; }
    public string HazardType { get; set; } = "";      // noise/dust/chemical/vision
    public DateTime RecordedDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Workplace injury register — SRS §3.23 (MLC linkage).</summary>
public sealed class WorkplaceInjury
{
    public long InjuryId { get; set; }
    public long PatientId { get; set; }
    public int? ContractId { get; set; }
    public DateTime InjuryDate { get; set; }
    public bool MlcLinked { get; set; }
    public string? Description { get; set; }
}

/// <summary>Teleconsultation session — SRS §3.24 (TPG 2020).</summary>
public sealed class TeleConsult
{
    public long TeleId { get; set; }
    public long PatientId { get; set; }
    public int? DoctorId { get; set; }
    public int? FromBranchId { get; set; }
    public int? ToBranchId { get; set; }
    public string? ConsultType { get; set; }          // Video/Audio/Tele-ICU/Tele-Radiology
    public DateTime? ScheduledUtc { get; set; }
    public bool ConsentCaptured { get; set; }
    public bool EPrescriptionSigned { get; set; }
    public string? SessionAuditUrl { get; set; }
    public string Status { get; set; } = "Scheduled";
}
