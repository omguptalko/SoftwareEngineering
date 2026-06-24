/* =====================================================================
   L1.8 step 2 — data backfill: HIS.dbo  ->  DEV tenant databases
   Relocates existing single-DB data into the provisioned per-tenant DBs,
   PRESERVING identity values (so cross-DB references stay valid as plain
   columns). Master/longitudinal data -> DEV_Master; fiscal-scoped -> the
   current-FY DB (DEV_FY2026_27). Template reference seeds are cleared first
   so this is a faithful 1:1 copy. Re-runnable (delete + reinsert).

   Generalising to another tenant = swap the two USE targets + the current FY.
   Only tables that currently hold rows are copied; empty tables need nothing.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================ MASTER plane ========================== */
USE DEV_Master;
GO

/* clear template seeds + any prior data (child -> parent) */
DELETE FROM clinical.BedTransfer;       DELETE FROM clinical.NursingNote;
DELETE FROM clinical.Vitals;            DELETE FROM clinical.EncounterDiagnosis;
DELETE FROM clinical.PrescriptionLine;  DELETE FROM clinical.Prescription;
DELETE FROM clinical.OtSchedule;        DELETE FROM clinical.EmergencyTriage;
DELETE FROM clinical.Appointment;       DELETE FROM clinical.Admission;
DELETE FROM clinical.Encounter;         DELETE FROM abdm.AbdmConsent;
DELETE FROM patient.PatientVisit;       DELETE FROM patient.Patient;
DELETE FROM master.HprProfessional;     DELETE FROM master.Bed;
DELETE FROM master.Ward;                DELETE FROM master.Tariff;
DELETE FROM master.Asset;               DELETE FROM master.Ambulance;
DELETE FROM master.Staff;               DELETE FROM master.HfrFacility;
DELETE FROM master.CompanyContract;     DELETE FROM master.SchemePackage;
DELETE FROM master.Supplier;            DELETE FROM master.CertificateTemplate;
DELETE FROM master.ConsentTemplate;     DELETE FROM master.WasteColourCode;
DELETE FROM master.HbpPackage;          DELETE FROM master.Payer;
DELETE FROM master.Drug;                DELETE FROM master.Doctor;
DELETE FROM master.Icd10Code;           DELETE FROM master.BloodGroup;
DELETE FROM master.Module;              DELETE FROM master.ModuleGroup;
DELETE FROM master.Branch;
GO

/* navigation registry */
INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder)
SELECT GroupId, Label, Icon, SortOrder FROM HIS.dbo.ModuleGroup;
INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef)
SELECT ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef FROM HIS.dbo.Module;
GO

/* masters (parents first) */
SET IDENTITY_INSERT master.Branch ON;
INSERT master.Branch (BranchId, Code, Name, City, State, IsActive)
SELECT BranchId, Code, Name, City, State, IsActive FROM HIS.dbo.Branch;
SET IDENTITY_INSERT master.Branch OFF;

INSERT master.BloodGroup (Code, SortOrder) SELECT Code, SortOrder FROM HIS.dbo.BloodGroup;
INSERT master.Icd10Code (Code, Description) SELECT Code, Description FROM HIS.dbo.Icd10Code;

SET IDENTITY_INSERT master.Doctor ON;
INSERT master.Doctor (DoctorId, Code, Name, Department, IsActive)
SELECT DoctorId, Code, Name, Department, IsActive FROM HIS.dbo.Doctor;
SET IDENTITY_INSERT master.Doctor OFF;

SET IDENTITY_INSERT master.Drug ON;
INSERT master.Drug (DrugId, Code, Name, Form, StockQty, ReorderLevel, IsActive)
SELECT DrugId, Code, Name, Form, StockQty, ReorderLevel, IsActive FROM HIS.dbo.Drug;
SET IDENTITY_INSERT master.Drug OFF;

SET IDENTITY_INSERT master.Payer ON;
INSERT master.Payer (PayerId, Code, Name, PayerType, IsActive)
SELECT PayerId, Code, Name, PayerType, IsActive FROM HIS.dbo.Payer;
SET IDENTITY_INSERT master.Payer OFF;

SET IDENTITY_INSERT master.HbpPackage ON;
INSERT master.HbpPackage (PackageId, Code, Name, Specialty, Rate, IsActive)
SELECT PackageId, Code, Name, Specialty, Rate, IsActive FROM HIS.dbo.HbpPackage;
SET IDENTITY_INSERT master.HbpPackage OFF;

SET IDENTITY_INSERT master.Supplier ON;
INSERT master.Supplier (SupplierId, Name, Gstin, IsActive)
SELECT SupplierId, Name, Gstin, IsActive FROM HIS.dbo.Supplier;
SET IDENTITY_INSERT master.Supplier OFF;

SET IDENTITY_INSERT master.SchemePackage ON;
INSERT master.SchemePackage (SchemePackageId, SchemeType, Code, Name, Rate, IsActive)
SELECT SchemePackageId, SchemeType, Code, Name, Rate, IsActive FROM HIS.dbo.SchemePackage;
SET IDENTITY_INSERT master.SchemePackage OFF;

INSERT master.WasteColourCode (ColourCode, Description) SELECT ColourCode, Description FROM HIS.dbo.WasteColourCode;

SET IDENTITY_INSERT master.ConsentTemplate ON;
INSERT master.ConsentTemplate (TemplateId, Code, Title, LanguageCode, Body, Version)
SELECT TemplateId, Code, Title, LanguageCode, Body, Version FROM HIS.dbo.ConsentTemplate;
SET IDENTITY_INSERT master.ConsentTemplate OFF;

SET IDENTITY_INSERT master.CertificateTemplate ON;
INSERT master.CertificateTemplate (TemplateId, CertType, Title, Body)
SELECT TemplateId, CertType, Title, Body FROM HIS.dbo.CertificateTemplate;
SET IDENTITY_INSERT master.CertificateTemplate OFF;

SET IDENTITY_INSERT master.CompanyContract ON;
INSERT master.CompanyContract (ContractId, CompanyName, PayerCode, ContractType, ValidFrom, ValidTo, IsActive)
SELECT ContractId, CompanyName, PayerCode, ContractType, ValidFrom, ValidTo, IsActive FROM HIS.dbo.CompanyContract;
SET IDENTITY_INSERT master.CompanyContract OFF;

SET IDENTITY_INSERT master.Staff ON;
INSERT master.Staff (StaffId, BranchId, EmployeeCode, FullName, Designation, Department, DateOfJoining, IsActive)
SELECT StaffId, BranchId, EmployeeCode, FullName, Designation, Department, DateOfJoining, IsActive FROM HIS.dbo.Staff;
SET IDENTITY_INSERT master.Staff OFF;

SET IDENTITY_INSERT master.Ambulance ON;
INSERT master.Ambulance (AmbulanceId, BranchId, VehicleNo, Status)
SELECT AmbulanceId, BranchId, VehicleNo, Status FROM HIS.dbo.Ambulance;
SET IDENTITY_INSERT master.Ambulance OFF;

SET IDENTITY_INSERT master.Asset ON;
INSERT master.Asset (AssetId, BranchId, AssetTag, Name, Category, AmcExpiry, NextMaintenance, Status)
SELECT AssetId, BranchId, AssetTag, Name, Category, AmcExpiry, NextMaintenance, Status FROM HIS.dbo.Asset;
SET IDENTITY_INSERT master.Asset OFF;

SET IDENTITY_INSERT master.HfrFacility ON;
INSERT master.HfrFacility (HfrId, BranchId, HfrCode, OnboardedUtc)
SELECT HfrId, BranchId, HfrCode, OnboardedUtc FROM HIS.dbo.HfrFacility;
SET IDENTITY_INSERT master.HfrFacility OFF;

SET IDENTITY_INSERT master.HprProfessional ON;
INSERT master.HprProfessional (HprId, DoctorId, HprCode, OnboardedUtc)
SELECT HprId, DoctorId, HprCode, OnboardedUtc FROM HIS.dbo.HprProfessional;
SET IDENTITY_INSERT master.HprProfessional OFF;

SET IDENTITY_INSERT master.Tariff ON;
INSERT master.Tariff (TariffId, BranchId, ServiceCode, ServiceName, Category, Rate, GstRatePct, IsActive)
SELECT TariffId, BranchId, ServiceCode, ServiceName, Category, Rate, GstRatePct, IsActive FROM HIS.dbo.Tariff;
SET IDENTITY_INSERT master.Tariff OFF;

SET IDENTITY_INSERT master.Ward ON;
INSERT master.Ward (WardId, BranchId, Name) SELECT WardId, BranchId, Name FROM HIS.dbo.Ward;
SET IDENTITY_INSERT master.Ward OFF;

SET IDENTITY_INSERT master.Bed ON;
INSERT master.Bed (BedId, WardId, BedNo, Status) SELECT BedId, WardId, BedNo, Status FROM HIS.dbo.Bed;
SET IDENTITY_INSERT master.Bed OFF;
GO

/* patient (longitudinal) */
SET IDENTITY_INSERT patient.Patient ON;
INSERT patient.Patient (PatientId, Uhid, RegBranchId, RegisteredAtUtc, FullName, GuardianName, AgeYears, DateOfBirth, Sex, BloodGroup, Mobile, Email, MaritalStatus, Category, Address, City, State, Pincode, Occupation, EmployerPayerCode, AadhaarMasked, AbhaNumber, AbhaAddress, IsActive)
SELECT PatientId, Uhid, RegBranchId, RegisteredAtUtc, FullName, GuardianName, AgeYears, DateOfBirth, Sex, BloodGroup, Mobile, Email, MaritalStatus, Category, Address, City, State, Pincode, Occupation, EmployerPayerCode, AadhaarMasked, AbhaNumber, AbhaAddress, IsActive
FROM HIS.dbo.Patient;
SET IDENTITY_INSERT patient.Patient OFF;

SET IDENTITY_INSERT patient.PatientVisit ON;
INSERT patient.PatientVisit (VisitId, PatientId, BranchId, VisitDate, VisitType, DoctorName, Diagnosis, PayerName)
SELECT VisitId, PatientId, BranchId, VisitDate, VisitType, DoctorName, Diagnosis, PayerName FROM HIS.dbo.PatientVisit;
SET IDENTITY_INSERT patient.PatientVisit OFF;
GO

/* clinical (longitudinal EMR) */
SET IDENTITY_INSERT clinical.Encounter ON;
INSERT clinical.Encounter (EncounterId, BranchId, PatientId, DoctorId, EncType, StartedUtc, Complaints, History, Advice, FollowUpDate, Status)
SELECT EncounterId, BranchId, PatientId, DoctorId, EncType, StartedUtc, Complaints, History, Advice, FollowUpDate, Status FROM HIS.dbo.Encounter;
SET IDENTITY_INSERT clinical.Encounter OFF;

SET IDENTITY_INSERT clinical.Appointment ON;
INSERT clinical.Appointment (AppointmentId, BranchId, PatientId, DoctorId, Department, SlotStart, VisitType, Mode, TokenNo, Status, CreatedUtc)
SELECT AppointmentId, BranchId, PatientId, DoctorId, Department, SlotStart, VisitType, Mode, TokenNo, Status, CreatedUtc FROM HIS.dbo.Appointment;
SET IDENTITY_INSERT clinical.Appointment OFF;

SET IDENTITY_INSERT clinical.Admission ON;
INSERT clinical.Admission (AdmissionId, AdmissionNo, BranchId, PatientId, BedId, ConsultantId, AdmittedUtc, AdmissionType, PaymentClass, ProvisionalIcd10, EstStayDays, DischargedUtc, DischargeSummary, Status)
SELECT AdmissionId, AdmissionNo, BranchId, PatientId, BedId, ConsultantId, AdmittedUtc, AdmissionType, PaymentClass, ProvisionalIcd10, EstStayDays, DischargedUtc, DischargeSummary, Status FROM HIS.dbo.Admission;
SET IDENTITY_INSERT clinical.Admission OFF;

SET IDENTITY_INSERT clinical.EmergencyTriage ON;
INSERT clinical.EmergencyTriage (TriageId, BranchId, PatientId, ArrivedUtc, Category, IsMlc, Notes, Status)
SELECT TriageId, BranchId, PatientId, ArrivedUtc, Category, IsMlc, Notes, Status FROM HIS.dbo.EmergencyTriage;
SET IDENTITY_INSERT clinical.EmergencyTriage OFF;

SET IDENTITY_INSERT clinical.OtSchedule ON;
INSERT clinical.OtSchedule (OtId, BranchId, PatientId, SurgeonId, Theatre, ScheduledUtc, Procedure_, PostOpNotes, Status)
SELECT OtId, BranchId, PatientId, SurgeonId, Theatre, ScheduledUtc, Procedure_, PostOpNotes, Status FROM HIS.dbo.OtSchedule;
SET IDENTITY_INSERT clinical.OtSchedule OFF;

SET IDENTITY_INSERT clinical.NursingNote ON;
INSERT clinical.NursingNote (NoteId, AdmissionId, RecordedUtc, NoteType, Note)
SELECT NoteId, AdmissionId, RecordedUtc, NoteType, Note FROM HIS.dbo.NursingNote;
SET IDENTITY_INSERT clinical.NursingNote OFF;
GO

/* ============================ FY plane ============================== */
USE DEV_FY2026_27;
GO

DELETE FROM diagnostics.BloodStock; DELETE FROM pharmacy.DrugBatch;
DELETE FROM analytics.DashboardKpi; DELETE FROM analytics.ServiceActivityDaily;
DELETE FROM support.QueueCounter;   DELETE FROM seq.DocCounter;
GO

SET IDENTITY_INSERT diagnostics.BloodStock ON;
INSERT diagnostics.BloodStock (BloodStockId, BranchId, BloodGroup, Units, SafetyThreshold)
SELECT BloodStockId, BranchId, BloodGroup, Units, SafetyThreshold FROM HIS.dbo.BloodStock;
SET IDENTITY_INSERT diagnostics.BloodStock OFF;

SET IDENTITY_INSERT pharmacy.DrugBatch ON;
INSERT pharmacy.DrugBatch (BatchId, DrugId, BatchNo, ExpiryDate, Mrp, QtyOnHand)
SELECT BatchId, DrugId, BatchNo, ExpiryDate, Mrp, QtyOnHand FROM HIS.dbo.DrugBatch;
SET IDENTITY_INSERT pharmacy.DrugBatch OFF;

SET IDENTITY_INSERT analytics.DashboardKpi ON;
INSERT analytics.DashboardKpi (KpiId, BranchId, [Value], Label, Trend, SortOrder)
SELECT KpiId, BranchId, [Value], Label, Trend, SortOrder FROM HIS.dbo.DashboardKpi;
SET IDENTITY_INSERT analytics.DashboardKpi OFF;

SET IDENTITY_INSERT analytics.ServiceActivityDaily ON;
INSERT analytics.ServiceActivityDaily (RowId, BranchId, Service, [Count], Revenue, SortOrder)
SELECT RowId, BranchId, Service, [Count], Revenue, SortOrder FROM HIS.dbo.ServiceActivityDaily;
SET IDENTITY_INSERT analytics.ServiceActivityDaily OFF;

SET IDENTITY_INSERT support.QueueCounter ON;
INSERT support.QueueCounter (CounterId, BranchId, Area, CounterName, IsActive)
SELECT CounterId, BranchId, Area, CounterName, IsActive FROM HIS.dbo.QueueCounter;
SET IDENTITY_INSERT support.QueueCounter OFF;

/* per-FY document counter (drop the calendar Year column from dbo.DocCounter) */
INSERT seq.DocCounter (BranchId, DocType, LastSeq)
SELECT BranchId, DocType, LastSeq FROM HIS.dbo.DocCounter;
GO

PRINT 'Backfill complete.';
GO
