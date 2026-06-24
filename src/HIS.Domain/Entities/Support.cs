namespace HIS.Domain.Entities;

// ---- §3.6 Ambulance ----
public sealed class Ambulance
{
    public int AmbulanceId { get; set; }
    public int BranchId { get; set; }
    public string VehicleNo { get; set; } = "";
    public string Status { get; set; } = "Available";
}

public sealed class AmbulanceDispatch
{
    public long DispatchId { get; set; }
    public int AmbulanceId { get; set; }
    public DateTime CallLoggedUtc { get; set; }
    public decimal? PickupLat { get; set; }
    public decimal? PickupLng { get; set; }
    public decimal? LastLat { get; set; }
    public decimal? LastLng { get; set; }
    public DateTime? ArrivedUtc { get; set; }
    public string Status { get; set; } = "Dispatched";
}

// ---- §3.26 Diet ----
public sealed class DietOrder
{
    public long DietOrderId { get; set; }
    public long AdmissionId { get; set; }
    public string DietType { get; set; } = "";
    public DateTime OrderedUtc { get; set; }
    public decimal? Cost { get; set; }
}

// ---- §3.25 BMWM ----
public sealed class WasteBag
{
    public long BagId { get; set; }
    public int BranchId { get; set; }
    public string Barcode { get; set; } = "";
    public string ColourCode { get; set; } = "";
    public decimal? WeightKg { get; set; }
    public DateTime GeneratedUtc { get; set; }
    public DateTime? CbwtfHandoverUtc { get; set; }
}

// ---- §3.27 Mortuary ----
public sealed class MortuaryRecord
{
    public long RecordId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }
    public string? StorageNo { get; set; }
    public DateTime AdmittedUtc { get; set; }
    public DateTime? ReleasedUtc { get; set; }
    public bool PoliceIntimated { get; set; }
    public bool MlcLinked { get; set; }
}

// ---- §3.28 MLC ----
public sealed class MlcCase
{
    public long MlcId { get; set; }
    public string MlcNo { get; set; } = "";
    public int BranchId { get; set; }
    public long? PatientId { get; set; }
    public string? PoliceStation { get; set; }
    public string? PoliceAckRef { get; set; }
    public string? InjuryDetails { get; set; }
    public DateTime CreatedUtc { get; set; }
}

// ---- §3.29 Consent ----
public sealed class ConsentTemplate
{
    public int TemplateId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string LanguageCode { get; set; } = "en";
    public string Body { get; set; } = "";
    public int Version { get; set; } = 1;
}

public sealed class ConsentCapture
{
    public long ConsentId { get; set; }
    public int TemplateId { get; set; }
    public long PatientId { get; set; }
    public string? SignatureType { get; set; }
    public DateTime CapturedUtc { get; set; }
}

// ---- §3.16 Certificates ----
public sealed class CertificateTemplate
{
    public int TemplateId { get; set; }
    public string CertType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}

public sealed class IssuedCertificate
{
    public long CertId { get; set; }
    public int TemplateId { get; set; }
    public long PatientId { get; set; }
    public int? ApprovedByDoctorId { get; set; }
    public DateTime IssuedUtc { get; set; }
    public string? PdfUrl { get; set; }
    public string Status { get; set; } = "Draft";
}

// ---- §3.30 Feedback & Grievance ----
public sealed class Grievance
{
    public long GrievanceId { get; set; }
    public int BranchId { get; set; }
    public long? PatientId { get; set; }
    public string? Category { get; set; }
    public DateTime? SlaDueUtc { get; set; }
    public int? ResolutionTatMinutes { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedUtc { get; set; }
}

public sealed class FeedbackSurvey
{
    public long SurveyId { get; set; }
    public long? PatientId { get; set; }
    public int? Score { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedUtc { get; set; }
}

// ---- §3.31 Queue ----
public sealed class QueueCounter
{
    public int CounterId { get; set; }
    public int BranchId { get; set; }
    public string Area { get; set; } = "";
    public string CounterName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class QueueToken
{
    public long TokenId { get; set; }
    public int CounterId { get; set; }
    public string TokenNo { get; set; } = "";
    public long? PatientId { get; set; }
    public DateTime IssuedUtc { get; set; }
    public string Status { get; set; } = "Waiting";
}
