namespace HIS.Domain.Entities;

/// <summary>Patient master — SRS §3.1. UHID unique across all branches.</summary>
public sealed class Patient
{
    public long PatientId { get; set; }
    public string Uhid { get; set; } = "";
    public int RegBranchId { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public string FullName { get; set; } = "";
    public string? GuardianName { get; set; }
    public int? AgeYears { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Sex { get; set; } = "";
    public string? BloodGroup { get; set; }
    public string Mobile { get; set; } = "";
    public string? Email { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Category { get; set; }          // General(Cash)/Insurance/PM-JAY/ESIC/Corporate
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Occupation { get; set; }
    public string? EmployerPayerCode { get; set; }
    // Identity (SRS §6) — Aadhaar stored masked/encrypted, never plain.
    public string? AadhaarMasked { get; set; }
    public string? AbhaNumber { get; set; }
    public string? AbhaAddress { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Cross-branch visit history row shown on Registration (was static in modules.js).</summary>
public sealed class PatientVisit
{
    public long VisitId { get; set; }
    public long PatientId { get; set; }
    public int BranchId { get; set; }
    public DateTime VisitDate { get; set; }
    public string VisitType { get; set; } = "";     // OPD/IPD/Lab
    public string? DoctorName { get; set; }
    public string? Diagnosis { get; set; }
    public string? PayerName { get; set; }
}

public sealed class Doctor
{
    public int DoctorId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
