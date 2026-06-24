/* =====================================================================
   Migration 0004 — Clinical Core (Phase 2)
   SRS: §3.2 Appointments/Token, §3.3 OPD, §3.5 ICU/Emergency, §3.4 IPD,
        §3.13 Nursing, §3.12 OT.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* §3.2 Appointment & Token ------------------------------------------- */
IF OBJECT_ID('dbo.Appointment') IS NULL
CREATE TABLE dbo.Appointment (
    AppointmentId BIGINT IDENTITY(1,1) CONSTRAINT PK_Appointment PRIMARY KEY,
    BranchId   INT NOT NULL CONSTRAINT FK_Appt_Branch REFERENCES dbo.Branch(BranchId),
    PatientId  BIGINT NULL CONSTRAINT FK_Appt_Patient REFERENCES dbo.Patient(PatientId),
    DoctorId   INT NOT NULL CONSTRAINT FK_Appt_Doctor REFERENCES dbo.Doctor(DoctorId),
    Department NVARCHAR(80) NULL,
    SlotStart  DATETIME2(0) NOT NULL,
    VisitType  NVARCHAR(20) NULL,     -- New/Follow-up/Review
    Mode       NVARCHAR(20) NULL,     -- Walk-in/Online/Tele-consult
    TokenNo    NVARCHAR(10) NULL,
    Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_Appt_Status DEFAULT('Booked'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Appt_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.3 OPD encounter + vitals + diagnosis + prescription ------------- */
IF OBJECT_ID('dbo.Encounter') IS NULL
CREATE TABLE dbo.Encounter (
    EncounterId BIGINT IDENTITY(1,1) CONSTRAINT PK_Encounter PRIMARY KEY,
    BranchId   INT NOT NULL CONSTRAINT FK_Enc_Branch REFERENCES dbo.Branch(BranchId),
    PatientId  BIGINT NOT NULL CONSTRAINT FK_Enc_Patient REFERENCES dbo.Patient(PatientId),
    DoctorId   INT NULL CONSTRAINT FK_Enc_Doctor REFERENCES dbo.Doctor(DoctorId),
    EncType    NVARCHAR(20) NOT NULL,   -- OPD/IPD/Emergency/Tele
    StartedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Enc_Started DEFAULT(SYSUTCDATETIME()),
    Complaints NVARCHAR(MAX) NULL,
    History    NVARCHAR(MAX) NULL,
    Advice     NVARCHAR(MAX) NULL,
    FollowUpDate DATE NULL,
    Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_Enc_Status DEFAULT('Open')
);
GO

IF OBJECT_ID('dbo.Vitals') IS NULL
CREATE TABLE dbo.Vitals (
    VitalsId   BIGINT IDENTITY(1,1) CONSTRAINT PK_Vitals PRIMARY KEY,
    EncounterId BIGINT NOT NULL CONSTRAINT FK_Vitals_Enc REFERENCES dbo.Encounter(EncounterId),
    RecordedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Vitals_Rec DEFAULT(SYSUTCDATETIME()),
    TempF DECIMAL(5,1) NULL, Pulse INT NULL, BpSystolic INT NULL, BpDiastolic INT NULL,
    Spo2 INT NULL, RespRate INT NULL, WeightKg DECIMAL(5,1) NULL, HeightCm DECIMAL(5,1) NULL, Grbs INT NULL
);
GO

IF OBJECT_ID('dbo.EncounterDiagnosis') IS NULL
CREATE TABLE dbo.EncounterDiagnosis (
    Id BIGINT IDENTITY(1,1) CONSTRAINT PK_EncDx PRIMARY KEY,
    EncounterId BIGINT NOT NULL CONSTRAINT FK_EncDx_Enc REFERENCES dbo.Encounter(EncounterId),
    Icd10Code NVARCHAR(10) NOT NULL CONSTRAINT FK_EncDx_Icd REFERENCES dbo.Icd10Code(Code),
    IsProvisional BIT NOT NULL CONSTRAINT DF_EncDx_Prov DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.Prescription') IS NULL
CREATE TABLE dbo.Prescription (
    PrescriptionId BIGINT IDENTITY(1,1) CONSTRAINT PK_Prescription PRIMARY KEY,
    EncounterId BIGINT NOT NULL CONSTRAINT FK_Rx_Enc REFERENCES dbo.Encounter(EncounterId),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Rx_Created DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Rx_Status DEFAULT('Pending')
);
GO

IF OBJECT_ID('dbo.PrescriptionLine') IS NULL
CREATE TABLE dbo.PrescriptionLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_RxLine PRIMARY KEY,
    PrescriptionId BIGINT NOT NULL CONSTRAINT FK_RxLine_Rx REFERENCES dbo.Prescription(PrescriptionId),
    DrugId INT NULL CONSTRAINT FK_RxLine_Drug REFERENCES dbo.Drug(DrugId),
    Dose NVARCHAR(40) NULL, Frequency NVARCHAR(20) NULL, Days INT NULL, Route NVARCHAR(20) NULL, Qty INT NULL
);
GO

/* §3.5 ICU / Emergency triage --------------------------------------- */
IF OBJECT_ID('dbo.EmergencyTriage') IS NULL
CREATE TABLE dbo.EmergencyTriage (
    TriageId BIGINT IDENTITY(1,1) CONSTRAINT PK_Triage PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Triage_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_Triage_Patient REFERENCES dbo.Patient(PatientId),
    ArrivedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Triage_Arrived DEFAULT(SYSUTCDATETIME()),
    Category NVARCHAR(20) NOT NULL,    -- Red/Yellow/Green (config-driven)
    IsMlc BIT NOT NULL CONSTRAINT DF_Triage_Mlc DEFAULT(0),
    Notes NVARCHAR(MAX) NULL
);
GO

/* §3.4 IPD admission ------------------------------------------------- */
IF OBJECT_ID('dbo.Admission') IS NULL
CREATE TABLE dbo.Admission (
    AdmissionId BIGINT IDENTITY(1,1) CONSTRAINT PK_Admission PRIMARY KEY,
    AdmissionNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_Admission_No UNIQUE,
    BranchId INT NOT NULL CONSTRAINT FK_Adm_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Adm_Patient REFERENCES dbo.Patient(PatientId),
    BedId INT NULL CONSTRAINT FK_Adm_Bed REFERENCES dbo.Bed(BedId),
    ConsultantId INT NULL CONSTRAINT FK_Adm_Doctor REFERENCES dbo.Doctor(DoctorId),
    AdmittedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Adm_Admitted DEFAULT(SYSUTCDATETIME()),
    AdmissionType NVARCHAR(20) NULL,   -- Planned/Emergency/DayCare/Transfer-in
    PaymentClass NVARCHAR(30) NULL,    -- Cashless/PM-JAY/ESIC/Cash/Corporate
    ProvisionalIcd10 NVARCHAR(10) NULL,
    EstStayDays INT NULL,
    DischargedUtc DATETIME2(3) NULL,
    DischargeSummary NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Adm_Status DEFAULT('Admitted')
);
GO

IF OBJECT_ID('dbo.BedTransfer') IS NULL
CREATE TABLE dbo.BedTransfer (
    TransferId BIGINT IDENTITY(1,1) CONSTRAINT PK_BedTransfer PRIMARY KEY,
    AdmissionId BIGINT NOT NULL CONSTRAINT FK_Xfer_Adm REFERENCES dbo.Admission(AdmissionId),
    FromBedId INT NULL CONSTRAINT FK_Xfer_From REFERENCES dbo.Bed(BedId),
    ToBedId INT NULL CONSTRAINT FK_Xfer_To REFERENCES dbo.Bed(BedId),
    TransferUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Xfer_Utc DEFAULT(SYSUTCDATETIME()),
    Reason NVARCHAR(200) NULL
);
GO

/* §3.13 Nursing care ------------------------------------------------- */
IF OBJECT_ID('dbo.NursingNote') IS NULL
CREATE TABLE dbo.NursingNote (
    NoteId BIGINT IDENTITY(1,1) CONSTRAINT PK_NursingNote PRIMARY KEY,
    AdmissionId BIGINT NOT NULL CONSTRAINT FK_NN_Adm REFERENCES dbo.Admission(AdmissionId),
    RecordedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_NN_Rec DEFAULT(SYSUTCDATETIME()),
    NoteType NVARCHAR(30) NULL,   -- Vitals/MAR/Handover/CarePlan
    Note NVARCHAR(MAX) NULL
);
GO

/* §3.12 Operation Theatre -------------------------------------------- */
IF OBJECT_ID('dbo.OtSchedule') IS NULL
CREATE TABLE dbo.OtSchedule (
    OtId BIGINT IDENTITY(1,1) CONSTRAINT PK_OtSchedule PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Ot_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Ot_Patient REFERENCES dbo.Patient(PatientId),
    SurgeonId INT NULL CONSTRAINT FK_Ot_Surgeon REFERENCES dbo.Doctor(DoctorId),
    Theatre NVARCHAR(20) NULL,
    ScheduledUtc DATETIME2(0) NOT NULL,
    Procedure_ NVARCHAR(200) NULL,
    PostOpNotes NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Ot_Status DEFAULT('Scheduled')
);
GO
