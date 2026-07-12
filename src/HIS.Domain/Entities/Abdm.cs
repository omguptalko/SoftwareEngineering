namespace HIS.Domain.Entities;

/// <summary>
/// ABDM consent artifact (HIP/HIU) — SRS §6.2. Longitudinal, lives in the tenant
/// master DB (abdm.AbdmConsent). Status lifecycle: Requested → Granted → Revoked/Expired.
/// </summary>
public sealed class AbdmConsent
{
    public long ConsentArtifactId { get; set; }
    public long PatientId { get; set; }
    public string? AbhaNumber { get; set; }
    public string? Purpose { get; set; }
    public string? HiTypes { get; set; }
    public DateTime? GrantedUtc { get; set; }
    public DateTime? ExpiryUtc { get; set; }
    public string Status { get; set; } = "Requested";
    public string? FhirBundleUrl { get; set; }
}

/// <summary>Health Facility Registry (HFR) link for a branch — SRS §6.2 (master.HfrFacility).</summary>
public sealed class HfrFacility
{
    public int HfrId { get; set; }
    public int BranchId { get; set; }
    public string? HfrCode { get; set; }
    public DateTime? OnboardedUtc { get; set; }
}

/// <summary>Healthcare Professional Registry (HPR) link for a doctor — SRS §6.2 (master.HprProfessional).</summary>
public sealed class HprProfessional
{
    public int HprId { get; set; }
    public int DoctorId { get; set; }
    public string? HprCode { get; set; }
    public DateTime? OnboardedUtc { get; set; }
}
