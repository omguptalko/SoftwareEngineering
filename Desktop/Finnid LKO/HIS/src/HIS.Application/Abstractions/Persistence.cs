using System.Data;
using HIS.Domain.Entities;

namespace HIS.Application.Abstractions;

/// <summary>
/// Creates open ADO connections for Dapper. Connection string comes from config
/// (Key Vault / appsettings) — never hardcoded (SRS §8.1/§8.2).
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}

/// <summary>Writes immutable audit rows (SRS §8.1). Backed by Dapper.</summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct = default);
}

/// <summary>Reads the resolved tenant's immutable audit trail (SRS §3.22, Phase 12.2).</summary>
public interface IAuditQueryRepository
{
    Task<IReadOnlyList<(DateTime OccurredAtUtc, string? UserName, string Action, string Entity, string? EntityId, bool Succeeded)>>
        GetRecentAsync(int take, CancellationToken ct = default);
}

// ---- Read/write repositories (Dapper-backed, parameterized only) ----

public interface IModuleRegistryRepository
{
    Task<IReadOnlyList<ModuleGroup>> GetGroupsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Module>> GetModulesAsync(CancellationToken ct = default);
}

/// <summary>Generic F3 lookup feed — replaces the static HIS.lookups in data.js.</summary>
public interface ILookupRepository
{
    Task<IReadOnlyList<Doctor>> GetDoctorsAsync(string? q, CancellationToken ct = default);
    Task<IReadOnlyList<Drug>> GetDrugsAsync(string? q, CancellationToken ct = default);
    Task<IReadOnlyList<Icd10Code>> GetIcd10Async(string? q, CancellationToken ct = default);
    Task<IReadOnlyList<Payer>> GetPayersAsync(string? q, CancellationToken ct = default);
    Task<IReadOnlyList<HbpPackage>> GetPackagesAsync(string? q, CancellationToken ct = default);
    Task<IReadOnlyList<(string Ward, string Bed, string Status)>> GetWardBedsAsync(int branchId, string? q, CancellationToken ct = default);
    Task<IReadOnlyList<BloodGroup>> GetBloodGroupsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Tariff>> GetTariffsAsync(int branchId, string? q, CancellationToken ct = default);
}

public interface IPatientRepository
{
    Task<Patient?> GetByUhidAsync(string uhid, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> SearchAsync(string? q, int branchId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<PatientVisit>> GetVisitsAsync(long patientId, CancellationToken ct = default);
    Task<string> GetNextUhidAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertAsync(Patient patient, CancellationToken ct = default);
    Task<bool> AadhaarExistsAsync(string aadhaarMasked, CancellationToken ct = default);
}

public interface IDashboardRepository
{
    Task<IReadOnlyList<(string Value, string Label, string Trend)>> GetKpisAsync(int branchId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Service, int Count, decimal Revenue)>> GetServiceActivityAsync(int branchId, CancellationToken ct = default);
}

public interface IAppointmentRepository
{
    Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<DateTime>> GetBookedSlotStartsAsync(int doctorId, DateTime date, CancellationToken ct = default);
    Task<string> NextTokenAsync(int branchId, int doctorId, DateTime date, CancellationToken ct = default);
    Task<long> InsertAsync(Appointment appt, CancellationToken ct = default);
    Task<IReadOnlyList<(string TokenNo, string PatientName, string DoctorName, string Status)>> GetTodayQueueAsync(int branchId, int? doctorId, DateTime date, CancellationToken ct = default);
}

public interface IEncounterRepository
{
    Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default);
    Task<int?> GetDrugIdByCodeAsync(string code, CancellationToken ct = default);
    Task<long> CreateEncounterAsync(Encounter e, CancellationToken ct = default);
    Task SaveVitalsAsync(Vitals v, CancellationToken ct = default);
    Task AddDiagnosisAsync(long encounterId, string icd10, bool provisional, CancellationToken ct = default);
    Task<long> CreatePrescriptionAsync(long encounterId, CancellationToken ct = default);
    Task AddPrescriptionLineAsync(PrescriptionLine line, CancellationToken ct = default);
}

public interface IAdmissionRepository
{
    Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default);
    Task<(int BedId, string Status)?> GetBedByNoAsync(int branchId, string bedNo, CancellationToken ct = default);
    Task SetBedStatusAsync(int bedId, string status, CancellationToken ct = default);
    Task<string> NextAdmissionNoAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertAsync(Admission a, CancellationToken ct = default);
    Task<Admission?> GetAsync(long admissionId, CancellationToken ct = default);
    Task UpdateBedAsync(long admissionId, int? bedId, CancellationToken ct = default);
    Task InsertTransferAsync(BedTransfer t, CancellationToken ct = default);
    Task DischargeAsync(long admissionId, string? summary, DateTime dischargedUtc, CancellationToken ct = default);
    Task<IReadOnlyList<(string Ward, string BedNo, string Status, string? Occupant)>> GetBedBoardAsync(int branchId, CancellationToken ct = default);
}

public interface IEmergencyRepository
{
    Task<long> InsertTriageAsync(EmergencyTriage t, CancellationToken ct = default);
    Task<EmergencyTriage?> GetTriageAsync(long triageId, CancellationToken ct = default);
    Task SetTriageStatusAsync(long triageId, string status, CancellationToken ct = default);
    /// <summary>Live ED board for today, ordered by triage severity (config order) then arrival.</summary>
    Task<IReadOnlyList<(long TriageId, string? Patient, string Category, bool IsMlc, string Status, DateTime ArrivedUtc)>> GetBoardAsync(int branchId, IReadOnlyList<string> severityOrder, CancellationToken ct = default);
}

public interface INursingRepository
{
    Task<bool> AdmissionExistsAsync(int branchId, long admissionId, CancellationToken ct = default);
    Task<long> InsertNoteAsync(NursingNote n, CancellationToken ct = default);
    Task<IReadOnlyList<(long NoteId, string? NoteType, string? Note, DateTime RecordedUtc)>> GetNotesAsync(long admissionId, CancellationToken ct = default);
}

public interface IOtRepository
{
    Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default);
    Task<long> InsertScheduleAsync(OtSchedule s, CancellationToken ct = default);
    Task<OtSchedule?> GetScheduleAsync(long otId, CancellationToken ct = default);
    Task CompleteAsync(long otId, string? postOpNotes, CancellationToken ct = default);
    Task<IReadOnlyList<(long OtId, string Patient, string? Surgeon, string? Theatre, string? Procedure, DateTime ScheduledUtc, string Status)>> GetBoardAsync(int branchId, CancellationToken ct = default);
}

public interface ILisRepository
{
    Task<string> NextBarcodeAsync(int branchId, CancellationToken ct = default);
    Task<long> CreateOrderAsync(LabOrder o, CancellationToken ct = default);
    Task<IReadOnlyList<(string Barcode, string Patient, string Test, string Status, long LabOrderId)>> GetWorklistAsync(int branchId, CancellationToken ct = default);
    Task AddResultAsync(LabResult r, CancellationToken ct = default);
    Task SetOrderStatusAsync(long labOrderId, string status, CancellationToken ct = default);
    Task<IReadOnlyList<LabResult>> GetResultsAsync(long labOrderId, CancellationToken ct = default);
}

public interface IRadiologyRepository
{
    Task<long> CreateOrderAsync(RadiologyOrder o, CancellationToken ct = default);
    Task<IReadOnlyList<(string Modality, string? Study, string Patient, string Status)>> GetWorklistAsync(int branchId, CancellationToken ct = default);
}

public interface IBloodBankRepository
{
    Task<IReadOnlyList<BloodStock>> GetStockAsync(int branchId, CancellationToken ct = default);
    Task<long> CreateRequestAsync(BloodRequest r, CancellationToken ct = default);
    Task<int> GetAvailableUnitsAsync(int branchId, string bloodGroup, CancellationToken ct = default);
}

/// <summary>Line to dispense: drug code, batch, quantity. Price comes from the batch MRP.</summary>
public sealed record DispenseLineInput(int DrugId, string BatchNo, int Qty);

public interface IPharmacyRepository
{
    Task<int?> GetDrugIdByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<(long PrescriptionId, string Patient, string Doctor, int Items, string Status)>> GetQueueAsync(int branchId, CancellationToken ct = default);
    Task<IReadOnlyList<(string BatchNo, string Expiry, decimal Mrp, int QtyOnHand)>> GetBatchesAsync(int drugId, CancellationToken ct = default);
    /// <summary>Atomically validates batches (existence/expiry/stock), deducts stock and records the dispense.</summary>
    Task<(long DispenseId, decimal Total)> DispenseAsync(Dispense dispense, IReadOnlyList<DispenseLineInput> lines, int expiryBlockDays, CancellationToken ct = default);
}

public interface IInventoryRepository
{
    Task<IReadOnlyList<(string Code, string Name, int Stock, int ReorderLevel)>> GetLowStockAsync(CancellationToken ct = default);
    /// <summary>All active stock items + their reorder levels (for demand forecasting, Phase 11.4).</summary>
    Task<IReadOnlyList<(string Code, string Name, int Stock, int ReorderLevel)>> GetStockLevelsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default);
    Task<string> NextPoNoAsync(int branchId, CancellationToken ct = default);
    Task<long> CreatePoAsync(PurchaseOrder po, IReadOnlyList<PurchaseOrderLine> lines, CancellationToken ct = default);
}

public interface IAssetRepository
{
    Task<IReadOnlyList<Asset>> GetAssetsAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertAsync(Asset a, CancellationToken ct = default);
}

public interface IAmbulanceRepository
{
    Task<IReadOnlyList<(int AmbulanceId, string VehicleNo, string Status)>> GetAmbulancesAsync(int branchId, CancellationToken ct = default);
    Task<int?> GetFirstAvailableAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertDispatchAsync(AmbulanceDispatch d, CancellationToken ct = default);
    Task SetAmbulanceStatusAsync(int ambulanceId, string status, CancellationToken ct = default);
    Task ArriveAsync(long dispatchId, decimal? lat, decimal? lng, CancellationToken ct = default);
    Task<int> GetDispatchAmbulanceAsync(long dispatchId, CancellationToken ct = default);
    Task<IReadOnlyList<(long DispatchId, string Vehicle, string Logged, string? Arrived, string Status)>> GetDispatchesAsync(int branchId, CancellationToken ct = default);
}

public interface IStatutoryRepository
{
    Task<long> InsertDietAsync(DietOrder d, CancellationToken ct = default);
    Task<IReadOnlyList<(long DietOrderId, string Patient, string DietType, decimal? Cost)>> GetDietAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertWasteBagAsync(WasteBag b, CancellationToken ct = default);
    Task HandoverWasteBagAsync(long bagId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Barcode, string Colour, decimal? WeightKg, bool HandedOver)>> GetWasteBagsAsync(int branchId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Colour, int Bags, decimal Weight)>> GetFormIvAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertMortuaryAsync(MortuaryRecord m, CancellationToken ct = default);
    Task ReleaseMortuaryAsync(long recordId, CancellationToken ct = default);
    Task<IReadOnlyList<(long RecordId, string? Patient, string? StorageNo, string Admitted, string? Released, bool Mlc)>> GetMortuaryAsync(int branchId, CancellationToken ct = default);
    Task<string> NextMlcNoAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertMlcAsync(MlcCase m, CancellationToken ct = default);
    Task IntimatePoliceAsync(long mlcId, string ackRef, CancellationToken ct = default);
    Task<IReadOnlyList<(long MlcId, string MlcNo, string? Patient, string? PoliceStation, string? PoliceAck, string Created)>> GetMlcAsync(int branchId, CancellationToken ct = default);
}

public interface IExperienceRepository
{
    Task<IReadOnlyList<(int TemplateId, string Code, string Title, string Lang)>> GetConsentTemplatesAsync(CancellationToken ct = default);
    Task<long> InsertConsentCaptureAsync(ConsentCapture c, CancellationToken ct = default);
    Task<IReadOnlyList<(int TemplateId, string CertType, string Title)>> GetCertTemplatesAsync(CancellationToken ct = default);
    Task<long> InsertCertificateAsync(IssuedCertificate cert, CancellationToken ct = default);
    Task ApproveCertificateAsync(long certId, int doctorId, CancellationToken ct = default);
    Task<IReadOnlyList<(long CertId, string CertType, string Patient, string Status)>> GetCertificatesAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertSurveyAsync(FeedbackSurvey s, CancellationToken ct = default);
    Task<long> InsertGrievanceAsync(Grievance g, CancellationToken ct = default);
    Task ResolveGrievanceAsync(long grievanceId, int tatMinutes, CancellationToken ct = default);
    Task<IReadOnlyList<(long GrievanceId, string? Category, string Status, string Created)>> GetGrievancesAsync(int branchId, CancellationToken ct = default);
    Task<IReadOnlyList<(int CounterId, string Area, string CounterName)>> GetCountersAsync(int branchId, CancellationToken ct = default);
    Task<string> IssueTokenAsync(int counterId, long? patientId, CancellationToken ct = default);
    Task<string?> CallNextAsync(int counterId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Area, string Counter, string TokenNo, string Status)>> GetQueueAsync(int branchId, CancellationToken ct = default);
}

public interface IOccHealthRepository
{
    Task<long> InsertContractAsync(CompanyContract c, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyContract>> GetContractsAsync(CancellationToken ct = default);
    Task<long> InsertExamAsync(MedicalExam e, CancellationToken ct = default);
    Task<IReadOnlyList<(long ExamId, string Patient, string? Company, string ExamType, string ExamDate, string? Fitness)>> GetExamsAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertHazardAsync(HazardExposure h, CancellationToken ct = default);
    Task<long> InsertInjuryAsync(WorkplaceInjury i, CancellationToken ct = default);
    Task<IReadOnlyList<(long InjuryId, string Patient, string InjuryDate, bool MlcLinked, string? Description)>> GetInjuriesAsync(int branchId, CancellationToken ct = default);
}

public interface ITelemedicineRepository
{
    Task<int?> GetDoctorIdByCodeAsync(string code, CancellationToken ct = default);
    Task<long> InsertTeleAsync(TeleConsult t, CancellationToken ct = default);
    Task<TeleConsult?> GetTeleAsync(long teleId, CancellationToken ct = default);
    Task UpdateTeleAsync(TeleConsult t, CancellationToken ct = default);
    Task<IReadOnlyList<(long TeleId, string Patient, string? Doctor, string? ConsultType, string? Scheduled, bool Consent, bool Signed, string Status)>> GetTeleListAsync(int branchId, CancellationToken ct = default);
}

public interface IHrRepository
{
    Task<long?> GetStaffIdByCodeAsync(int branchId, string code, CancellationToken ct = default);
    Task<long> InsertStaffAsync(Staff s, CancellationToken ct = default);
    Task<IReadOnlyList<Staff>> GetStaffAsync(int branchId, CancellationToken ct = default);
    Task UpsertAttendanceAsync(Attendance a, CancellationToken ct = default);
    Task<IReadOnlyList<(string EmployeeCode, string Name, string Status, string? InTime, string? OutTime)>> GetAttendanceAsync(int branchId, DateTime date, CancellationToken ct = default);
    Task<long> InsertLeaveAsync(LeaveRequest l, CancellationToken ct = default);
    Task SetLeaveStatusAsync(long leaveId, string status, CancellationToken ct = default);
    Task<IReadOnlyList<(long LeaveId, string Name, string FromDate, string ToDate, string? Type, string Status)>> GetLeavesAsync(int branchId, CancellationToken ct = default);
}

public interface IPayrollRepository
{
    Task<long?> GetStaffIdByCodeAsync(int branchId, string code, CancellationToken ct = default);
    Task<long> UpsertRunAsync(PayrollRun run, CancellationToken ct = default);
    Task<PayrollRun?> GetRunAsync(long payrollId, CancellationToken ct = default);
    Task ApproveOvertimeAsync(long payrollId, long approvedBy, CancellationToken ct = default);
    Task<IReadOnlyList<(long PayrollId, string EmployeeCode, string Name, decimal Basic, decimal OtHours, decimal OtAmount, decimal Net, string Status)>> GetRunsAsync(int branchId, int year, int month, CancellationToken ct = default);
}

public interface IClaimsRepository
{
    Task<int?> GetPayerIdByCodeAsync(string code, CancellationToken ct = default);
    Task<long> InsertPolicyAsync(InsurancePolicy p, CancellationToken ct = default);
    Task<IReadOnlyList<(long PolicyId, string Payer, string? PolicyNo, decimal? SumInsured, decimal? AvailableBalance, decimal? RoomRentCap, decimal? CoPayPct)>> GetPoliciesAsync(long patientId, CancellationToken ct = default);
    Task<string> NextClaimNoAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertClaimAsync(Claim c, CancellationToken ct = default);
    Task AddEventAsync(ClaimEvent e, CancellationToken ct = default);
    Task AddDocumentAsync(ClaimDocument d, CancellationToken ct = default);
    Task<Claim?> GetClaimAsync(long claimId, CancellationToken ct = default);
    Task UpdateClaimAsync(Claim c, CancellationToken ct = default);
    Task<IReadOnlyList<(long EventId, string EventType, decimal? Amount, string? Notes, DateTime OccurredUtc)>> GetEventsAsync(long claimId, CancellationToken ct = default);
    Task<IReadOnlyList<(long ClaimId, string ClaimNo, string Patient, string Payer, decimal? PreAuth, decimal? Approved, string Status)>> GetClaimsAsync(int branchId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Status, int Count)>> GetStatusCountsAsync(int branchId, CancellationToken ct = default);
    Task<long> InsertReconciliationAsync(SettlementReconciliation r, CancellationToken ct = default);
}

public interface IPmjayRepository
{
    Task<(int PackageId, decimal Rate)?> GetPackageByCodeAsync(string code, CancellationToken ct = default);
    Task<long> UpsertBeneficiaryAsync(PmjayBeneficiary b, CancellationToken ct = default);
    Task<long> InsertCaseAsync(PmjayCase c, CancellationToken ct = default);
    Task<string> NextTmsNoAsync(int branchId, CancellationToken ct = default);
}

public interface ISchemeRepository
{
    Task<long> UpsertMembershipAsync(SchemeMembership m, CancellationToken ct = default);
    Task<IReadOnlyList<SchemePackage>> GetPackagesAsync(string schemeType, string? q, CancellationToken ct = default);
}

public interface IBillingRepository
{
    Task<Tariff?> GetTariffByCodeAsync(int branchId, string code, CancellationToken ct = default);
    Task<string> NextBillNoAsync(int branchId, CancellationToken ct = default);
    Task<long> CreateBillAsync(Bill bill, IReadOnlyList<BillLine> lines, CancellationToken ct = default);
    Task<Bill?> GetBillAsync(long billId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Description, decimal Qty, decimal Rate, decimal Amount)>> GetBillLinesAsync(long billId, CancellationToken ct = default);
    Task<long> InsertPaymentAsync(Payment p, CancellationToken ct = default);
    Task<decimal> GetPaidTotalAsync(long billId, CancellationToken ct = default);
    Task UpdateBillStatusAsync(long billId, string status, CancellationToken ct = default);
    Task<long> InsertDepositAsync(PatientDeposit d, CancellationToken ct = default);
    Task<decimal> GetDepositBalanceAsync(long patientId, CancellationToken ct = default);
}
