/* =====================================================================
   Tenant MASTER DB — clinical & ABDM (L1.8 schema-split, D3 longitudinal)
   EMR/clinical history is longitudinal → lives in the master DB, not the
   per-fiscal-year DB. Schemas: clinical, abdm, patient. Intra-DB FKs kept
   (schema-qualified); there are no cross-DB FKs here. Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID('clinical') IS NULL EXEC('CREATE SCHEMA clinical');
GO
IF SCHEMA_ID('abdm')     IS NULL EXEC('CREATE SCHEMA abdm');
GO

/* §3.21 cross-branch visit history (patient schema) ------------------ */
IF OBJECT_ID('patient.PatientVisit') IS NULL
CREATE TABLE patient.PatientVisit (
    VisitId    BIGINT IDENTITY(1,1) CONSTRAINT PK_p_Visit PRIMARY KEY,
    PatientId  BIGINT NOT NULL CONSTRAINT FK_p_Visit_Patient REFERENCES patient.Patient(PatientId),
    BranchId   INT NOT NULL CONSTRAINT FK_p_Visit_Branch REFERENCES master.Branch(BranchId),
    VisitDate  DATE NOT NULL,
    VisitType  NVARCHAR(20)  NOT NULL,
    DoctorName NVARCHAR(120) NULL,
    Diagnosis  NVARCHAR(200) NULL,
    PayerName  NVARCHAR(120) NULL
);
GO

/* §3.2 Appointments -------------------------------------------------- */
IF OBJECT_ID('clinical.Appointment') IS NULL
CREATE TABLE clinical.Appointment (
    AppointmentId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_Appt PRIMARY KEY,
    BranchId   INT NOT NULL CONSTRAINT FK_c_Appt_Branch REFERENCES master.Branch(BranchId),
    PatientId  BIGINT NULL CONSTRAINT FK_c_Appt_Patient REFERENCES patient.Patient(PatientId),
    DoctorId   INT NOT NULL CONSTRAINT FK_c_Appt_Doctor REFERENCES master.Doctor(DoctorId),
    Department NVARCHAR(80) NULL,
    SlotStart  DATETIME2(0) NOT NULL,
    VisitType  NVARCHAR(20) NULL,
    Mode       NVARCHAR(20) NULL,
    TokenNo    NVARCHAR(10) NULL,
    Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_c_Appt_Status DEFAULT('Booked'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Appt_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.3 OPD encounter + vitals + diagnosis + prescription ------------- */
IF OBJECT_ID('clinical.Encounter') IS NULL
CREATE TABLE clinical.Encounter (
    EncounterId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_Encounter PRIMARY KEY,
    BranchId   INT NOT NULL CONSTRAINT FK_c_Enc_Branch REFERENCES master.Branch(BranchId),
    PatientId  BIGINT NOT NULL CONSTRAINT FK_c_Enc_Patient REFERENCES patient.Patient(PatientId),
    DoctorId   INT NULL CONSTRAINT FK_c_Enc_Doctor REFERENCES master.Doctor(DoctorId),
    EncType    NVARCHAR(20) NOT NULL,
    StartedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Enc_Started DEFAULT(SYSUTCDATETIME()),
    Complaints NVARCHAR(MAX) NULL,
    History    NVARCHAR(MAX) NULL,
    Advice     NVARCHAR(MAX) NULL,
    FollowUpDate DATE NULL,
    Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_c_Enc_Status DEFAULT('Open')
);
GO

IF OBJECT_ID('clinical.Vitals') IS NULL
CREATE TABLE clinical.Vitals (
    VitalsId   BIGINT IDENTITY(1,1) CONSTRAINT PK_c_Vitals PRIMARY KEY,
    EncounterId BIGINT NOT NULL CONSTRAINT FK_c_Vitals_Enc REFERENCES clinical.Encounter(EncounterId),
    RecordedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Vitals_Rec DEFAULT(SYSUTCDATETIME()),
    TempF DECIMAL(5,1) NULL, Pulse INT NULL, BpSystolic INT NULL, BpDiastolic INT NULL,
    Spo2 INT NULL, RespRate INT NULL, WeightKg DECIMAL(5,1) NULL, HeightCm DECIMAL(5,1) NULL, Grbs INT NULL
);
GO

IF OBJECT_ID('clinical.EncounterDiagnosis') IS NULL
CREATE TABLE clinical.EncounterDiagnosis (
    Id BIGINT IDENTITY(1,1) CONSTRAINT PK_c_EncDx PRIMARY KEY,
    EncounterId BIGINT NOT NULL CONSTRAINT FK_c_EncDx_Enc REFERENCES clinical.Encounter(EncounterId),
    Icd10Code NVARCHAR(10) NOT NULL CONSTRAINT FK_c_EncDx_Icd REFERENCES master.Icd10Code(Code),
    IsProvisional BIT NOT NULL CONSTRAINT DF_c_EncDx_Prov DEFAULT(1)
);
GO

IF OBJECT_ID('clinical.Prescription') IS NULL
CREATE TABLE clinical.Prescription (
    PrescriptionId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_Rx PRIMARY KEY,
    EncounterId BIGINT NOT NULL CONSTRAINT FK_c_Rx_Enc REFERENCES clinical.Encounter(EncounterId),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Rx_Created DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_c_Rx_Status DEFAULT('Pending')
);
GO

IF OBJECT_ID('clinical.PrescriptionLine') IS NULL
CREATE TABLE clinical.PrescriptionLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_RxLine PRIMARY KEY,
    PrescriptionId BIGINT NOT NULL CONSTRAINT FK_c_RxLine_Rx REFERENCES clinical.Prescription(PrescriptionId),
    DrugId INT NULL CONSTRAINT FK_c_RxLine_Drug REFERENCES master.Drug(DrugId),
    Dose NVARCHAR(40) NULL, Frequency NVARCHAR(20) NULL, Days INT NULL, Route NVARCHAR(20) NULL, Qty INT NULL
);
GO

/* §3.5 ICU / Emergency triage (incl. disposition Status, mig 0013) --- */
IF OBJECT_ID('clinical.EmergencyTriage') IS NULL
CREATE TABLE clinical.EmergencyTriage (
    TriageId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_Triage PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_c_Triage_Branch REFERENCES master.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_c_Triage_Patient REFERENCES patient.Patient(PatientId),
    ArrivedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Triage_Arrived DEFAULT(SYSUTCDATETIME()),
    Category NVARCHAR(20) NOT NULL,
    IsMlc BIT NOT NULL CONSTRAINT DF_c_Triage_Mlc DEFAULT(0),
    Notes NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_c_Triage_Status DEFAULT('Waiting')
);
GO

/* §3.4 IPD admission ------------------------------------------------- */
IF OBJECT_ID('clinical.Admission') IS NULL
CREATE TABLE clinical.Admission (
    AdmissionId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_Admission PRIMARY KEY,
    AdmissionNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_c_Admission_No UNIQUE,
    BranchId INT NOT NULL CONSTRAINT FK_c_Adm_Branch REFERENCES master.Branch(BranchId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_c_Adm_Patient REFERENCES patient.Patient(PatientId),
    BedId INT NULL CONSTRAINT FK_c_Adm_Bed REFERENCES master.Bed(BedId),
    ConsultantId INT NULL CONSTRAINT FK_c_Adm_Doctor REFERENCES master.Doctor(DoctorId),
    AdmittedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Adm_Admitted DEFAULT(SYSUTCDATETIME()),
    AdmissionType NVARCHAR(20) NULL,
    PaymentClass NVARCHAR(30) NULL,
    ProvisionalIcd10 NVARCHAR(10) NULL,
    EstStayDays INT NULL,
    DischargedUtc DATETIME2(3) NULL,
    DischargeSummary NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_c_Adm_Status DEFAULT('Admitted')
);
GO

IF OBJECT_ID('clinical.BedTransfer') IS NULL
CREATE TABLE clinical.BedTransfer (
    TransferId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_BedTransfer PRIMARY KEY,
    AdmissionId BIGINT NOT NULL CONSTRAINT FK_c_Xfer_Adm REFERENCES clinical.Admission(AdmissionId),
    FromBedId INT NULL CONSTRAINT FK_c_Xfer_From REFERENCES master.Bed(BedId),
    ToBedId INT NULL CONSTRAINT FK_c_Xfer_To REFERENCES master.Bed(BedId),
    TransferUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_Xfer_Utc DEFAULT(SYSUTCDATETIME()),
    Reason NVARCHAR(200) NULL
);
GO

IF OBJECT_ID('clinical.NursingNote') IS NULL
CREATE TABLE clinical.NursingNote (
    NoteId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_NursingNote PRIMARY KEY,
    AdmissionId BIGINT NOT NULL CONSTRAINT FK_c_NN_Adm REFERENCES clinical.Admission(AdmissionId),
    RecordedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_c_NN_Rec DEFAULT(SYSUTCDATETIME()),
    NoteType NVARCHAR(30) NULL,
    Note NVARCHAR(MAX) NULL
);
GO

/* §3.12 Operation Theatre -------------------------------------------- */
IF OBJECT_ID('clinical.OtSchedule') IS NULL
CREATE TABLE clinical.OtSchedule (
    OtId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_OtSchedule PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_c_Ot_Branch REFERENCES master.Branch(BranchId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_c_Ot_Patient REFERENCES patient.Patient(PatientId),
    SurgeonId INT NULL CONSTRAINT FK_c_Ot_Surgeon REFERENCES master.Doctor(DoctorId),
    Theatre NVARCHAR(20) NULL,
    ScheduledUtc DATETIME2(0) NOT NULL,
    Procedure_ NVARCHAR(200) NULL,
    PostOpNotes NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_c_Ot_Status DEFAULT('Scheduled')
);
GO

/* §6.2 ABDM consent (longitudinal) ----------------------------------- */
IF OBJECT_ID('abdm.AbdmConsent') IS NULL
CREATE TABLE abdm.AbdmConsent (
    ConsentArtifactId BIGINT IDENTITY(1,1) CONSTRAINT PK_a_AbdmConsent PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_a_Abdm_Patient REFERENCES patient.Patient(PatientId),
    AbhaNumber NVARCHAR(20) NULL,
    Purpose NVARCHAR(120) NULL,
    HiTypes NVARCHAR(200) NULL,
    GrantedUtc DATETIME2(3) NULL,
    ExpiryUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_a_Abdm_Status DEFAULT('Requested'),
    FhirBundleUrl NVARCHAR(400) NULL
);
GO
