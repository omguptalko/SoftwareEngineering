/* Fill every remaining empty table in the DEV tenant with one demo row.
   Idempotent: each insert is guarded by IF NOT EXISTS.

   Operational/demo utility — NOT part of the auto-run migration/seed pipeline.
   Run manually against a tenant that has base masters + a few patients/encounters:
     sqlcmd -S "(localdb)\MSSQLLocalDB" -i db/tools/fill_demo.sql
   Adjust the USE statements below to target a different tenant's DBs. */

/* =================== DEV_Master =================== */
USE DEV_Master;
IF NOT EXISTS (SELECT 1 FROM abdm.AbdmConsent)
  INSERT abdm.AbdmConsent (PatientId, AbhaNumber, Purpose, HiTypes, GrantedUtc, ExpiryUtc, Status, FhirBundleUrl)
  SELECT TOP 1 PatientId, '91-1111-2222-3333', 'Care Management', 'DiagnosticReport,Prescription',
         SYSUTCDATETIME(), DATEADD(MONTH,6,SYSUTCDATETIME()), 'Granted', 'https://finnid.in/fhir/Bundle/demo'
  FROM patient.Patient ORDER BY PatientId;

IF NOT EXISTS (SELECT 1 FROM master.HfrFacility)
  INSERT master.HfrFacility (BranchId, HfrCode, OnboardedUtc)
  SELECT TOP 1 BranchId, 'HFR-DEMO-0001', SYSUTCDATETIME() FROM master.Branch ORDER BY BranchId;

IF NOT EXISTS (SELECT 1 FROM master.HprProfessional)
  INSERT master.HprProfessional (DoctorId, HprCode, OnboardedUtc)
  SELECT TOP 1 DoctorId, 'HPR-DEMO-0001', SYSUTCDATETIME() FROM master.Doctor ORDER BY DoctorId;

IF NOT EXISTS (SELECT 1 FROM clinical.Vitals)
  INSERT clinical.Vitals (EncounterId, RecordedUtc, TempF, Pulse, BpSystolic, BpDiastolic, Spo2, RespRate, WeightKg, HeightCm, Grbs)
  SELECT TOP 1 EncounterId, SYSUTCDATETIME(), 98.6, 78, 120, 80, 98, 16, 68.5, 170, 95 FROM clinical.Encounter ORDER BY EncounterId;

IF NOT EXISTS (SELECT 1 FROM clinical.Prescription)
BEGIN
  INSERT clinical.Prescription (EncounterId, CreatedUtc, Status)
  SELECT TOP 1 EncounterId, SYSUTCDATETIME(), 'Active' FROM clinical.Encounter ORDER BY EncounterId;
  DECLARE @rx BIGINT = SCOPE_IDENTITY();
  INSERT clinical.PrescriptionLine (PrescriptionId, DrugId, Dose, Frequency, Days, Route, Qty)
  SELECT @rx, (SELECT TOP 1 DrugId FROM master.Drug ORDER BY DrugId), '500mg', 'BD', 5, 'Oral', 10;
END

IF NOT EXISTS (SELECT 1 FROM clinical.BedTransfer)
  INSERT clinical.BedTransfer (AdmissionId, FromBedId, ToBedId, TransferUtc, Reason)
  SELECT TOP 1 a.AdmissionId, (SELECT MIN(BedId) FROM master.Bed), (SELECT MAX(BedId) FROM master.Bed),
         SYSUTCDATETIME(), 'Ward change - step-down' FROM clinical.Admission a ORDER BY a.AdmissionId;
GO

/* =================== DEV_FY2026_27 =================== */
USE DEV_FY2026_27;
DECLARE @pid BIGINT = (SELECT MIN(PatientId) FROM DEV_Master.patient.Patient);
DECLARE @sid BIGINT = (SELECT MIN(StaffId)   FROM DEV_Master.master.Staff);

IF NOT EXISTS (SELECT 1 FROM ai.AiInsight)
  INSERT ai.AiInsight (BranchId, InsightType, SubjectType, SubjectId, Score, DetailJson, GeneratedUtc)
  VALUES (1, 'RiskPrediction', 'Patient', CAST(@pid AS NVARCHAR(80)), 13.0, '{"band":"High","score":13}', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM audit.AuditEntry)
  INSERT audit.AuditEntry (OccurredAtUtc, BranchId, UserId, UserName, Action, Entity, EntityId, PayloadJson, Succeeded, Error)
  VALUES (SYSUTCDATETIME(), 1, NULL, 'Seed', 'SeedDemoData', 'AuditEntry', 'FY-DEMO', '{"seed":true}', 1, NULL);

IF NOT EXISTS (SELECT 1 FROM billing.PatientDeposit)
  INSERT billing.PatientDeposit (PatientId, Amount, Balance, CreatedUtc) VALUES (@pid, 5000, 5000, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM compliance.ComplianceReport)
  INSERT compliance.ComplianceReport (BranchId, Regulation, PeriodFrom, PeriodTo, GeneratedUtc, FileUrl, Status)
  VALUES (1, 'BMWM Rules 2016 - Form IV', '2026-04-01', '2027-03-31', SYSUTCDATETIME(), 'https://finnid.in/reports/formiv-demo.pdf', 'Generated');

IF NOT EXISTS (SELECT 1 FROM hr.DutyRoster)
  INSERT hr.DutyRoster (StaffId, ShiftDate, Shift) VALUES (@sid, CAST(SYSUTCDATETIME() AS DATE), 'Morning');

IF NOT EXISTS (SELECT 1 FROM hr.LeaveRequest)
  INSERT hr.LeaveRequest (StaffId, FromDate, ToDate, LeaveType, Status) VALUES (@sid, '2026-07-10', '2026-07-12', 'Casual', 'Approved');

IF NOT EXISTS (SELECT 1 FROM insurance.SettlementReconciliation)
  INSERT insurance.SettlementReconciliation (ClaimId, Utr, BankAmount, ReconciledUtc, Status)
  SELECT TOP 1 ClaimId, 'UTR-DEMO-0001', 40000, SYSUTCDATETIME(), 'Matched' FROM insurance.Claim ORDER BY ClaimId;

IF NOT EXISTS (SELECT 1 FROM occhealth.HazardExposure)
  INSERT occhealth.HazardExposure (PatientId, HazardType, RecordedDate, Notes)
  VALUES (@pid, 'Noise', CAST(SYSUTCDATETIME() AS DATE), 'Audiometry advised; PPE issued');

IF NOT EXISTS (SELECT 1 FROM occhealth.WorkplaceInjury)
  INSERT occhealth.WorkplaceInjury (PatientId, ContractId, InjuryDate, MlcLinked, Description)
  VALUES (@pid, 1, SYSUTCDATETIME(), 0, 'Minor laceration - left hand, dressed');

IF NOT EXISTS (SELECT 1 FROM support.ConsentCapture)
  INSERT support.ConsentCapture (TemplateId, PatientId, SignatureType, CapturedUtc) VALUES (1, @pid, 'e-sign', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM support.IssuedCertificate)
  INSERT support.IssuedCertificate (TemplateId, PatientId, ApprovedByDoctorId, IssuedUtc, PdfUrl, Status)
  VALUES (1, @pid, 1, SYSUTCDATETIME(), 'https://finnid.in/certs/fitness-demo.pdf', 'Issued');

IF NOT EXISTS (SELECT 1 FROM support.MortuaryRecord)
  INSERT support.MortuaryRecord (BranchId, PatientId, StorageNo, AdmittedUtc, ReleasedUtc, PoliceIntimated, MlcLinked)
  VALUES (1, @pid, 'M-01', SYSUTCDATETIME(), NULL, 0, 0);
GO

/* =================== HIS_Platform =================== */
USE HIS_Platform;
IF NOT EXISTS (SELECT 1 FROM security.RolePage)
  INSERT security.RolePage (RoleId, PageId)
  SELECT TOP 1 r.RoleId, p.PageId FROM security.Role r CROSS JOIN security.AppPage p
  WHERE r.Code = 'admin' ORDER BY p.PageId;
GO
