/* =====================================================================
   Tenant per-FISCAL-YEAR DB — support & statutory transactions (L1.8)
   Ambulance dispatch, diet, BMWM bags, mortuary, MLC, consent capture,
   issued certificates, feedback/grievance, queue tokens. Masters (Ambulance,
   WasteColourCode, Consent/Cert templates) live in the master DB → PLAIN
   cross-DB columns. Intra-DB FKs kept. Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('support') IS NULL EXEC('CREATE SCHEMA support');
GO

/* §3.6 Ambulance dispatch (Ambulance master in master DB) ------------ */
IF OBJECT_ID('support.AmbulanceDispatch') IS NULL
CREATE TABLE support.AmbulanceDispatch (
    DispatchId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_AmbDispatch PRIMARY KEY,
    AmbulanceId INT NOT NULL,     -- master.Ambulance (cross-DB)
    CallLoggedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_AmbDisp_Logged DEFAULT(SYSUTCDATETIME()),
    PickupLat DECIMAL(9,6) NULL, PickupLng DECIMAL(9,6) NULL,
    LastLat DECIMAL(9,6) NULL, LastLng DECIMAL(9,6) NULL,
    ArrivedUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_su_AmbDisp_Status DEFAULT('Dispatched')
);
GO

/* §3.26 Diet (Admission in master DB) -------------------------------- */
IF OBJECT_ID('support.DietOrder') IS NULL
CREATE TABLE support.DietOrder (
    DietOrderId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_DietOrder PRIMARY KEY,
    AdmissionId BIGINT NOT NULL,  -- clinical.Admission (cross-DB)
    DietType NVARCHAR(40) NOT NULL,
    OrderedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Diet_Utc DEFAULT(SYSUTCDATETIME()),
    Cost DECIMAL(10,2) NULL
);
GO

/* §3.25 BMWM bags (WasteColourCode master in master DB) -------------- */
IF OBJECT_ID('support.WasteBag') IS NULL
CREATE TABLE support.WasteBag (
    BagId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_WasteBag PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    Barcode NVARCHAR(40) NOT NULL CONSTRAINT UQ_su_WasteBag_Barcode UNIQUE,
    ColourCode NVARCHAR(20) NOT NULL,  -- master.WasteColourCode (cross-DB)
    WeightKg DECIMAL(7,2) NULL,
    GeneratedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Waste_Gen DEFAULT(SYSUTCDATETIME()),
    CbwtfHandoverUtc DATETIME2(3) NULL
);
GO

/* §3.27 Mortuary ----------------------------------------------------- */
IF OBJECT_ID('support.MortuaryRecord') IS NULL
CREATE TABLE support.MortuaryRecord (
    RecordId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_MortuaryRecord PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    PatientId BIGINT NULL,        -- cross-DB
    StorageNo NVARCHAR(20) NULL,
    AdmittedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Mort_Admitted DEFAULT(SYSUTCDATETIME()),
    ReleasedUtc DATETIME2(3) NULL,
    PoliceIntimated BIT NOT NULL CONSTRAINT DF_su_Mort_Police DEFAULT(0),
    MlcLinked BIT NOT NULL CONSTRAINT DF_su_Mort_Mlc DEFAULT(0)
);
GO

/* §3.28 Medico-Legal Case -------------------------------------------- */
IF OBJECT_ID('support.MlcCase') IS NULL
CREATE TABLE support.MlcCase (
    MlcId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_MlcCase PRIMARY KEY,
    MlcNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_su_Mlc_No UNIQUE,
    BranchId INT NOT NULL,        -- cross-DB
    PatientId BIGINT NULL,        -- cross-DB
    PoliceStation NVARCHAR(120) NULL,
    PoliceAckRef NVARCHAR(60) NULL,
    InjuryDetails NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Mlc_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.29 Consent capture (ConsentTemplate master in master DB) -------- */
IF OBJECT_ID('support.ConsentCapture') IS NULL
CREATE TABLE support.ConsentCapture (
    ConsentId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_ConsentCapture PRIMARY KEY,
    TemplateId INT NOT NULL,      -- master.ConsentTemplate (cross-DB)
    PatientId BIGINT NOT NULL,    -- cross-DB
    SignatureType NVARCHAR(20) NULL,
    CapturedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Consent_Utc DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.16 Issued certificates (CertificateTemplate master in master DB)  */
IF OBJECT_ID('support.IssuedCertificate') IS NULL
CREATE TABLE support.IssuedCertificate (
    CertId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_IssuedCert PRIMARY KEY,
    TemplateId INT NOT NULL,      -- master.CertificateTemplate (cross-DB)
    PatientId BIGINT NOT NULL,    -- cross-DB
    ApprovedByDoctorId INT NULL,  -- master.Doctor (cross-DB)
    IssuedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Cert_Utc DEFAULT(SYSUTCDATETIME()),
    PdfUrl NVARCHAR(400) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_su_Cert_Status DEFAULT('Draft')
);
GO

/* §3.30 Feedback & grievance ----------------------------------------- */
IF OBJECT_ID('support.Grievance') IS NULL
CREATE TABLE support.Grievance (
    GrievanceId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_Grievance PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    PatientId BIGINT NULL,        -- cross-DB
    Category NVARCHAR(60) NULL,
    SlaDueUtc DATETIME2(3) NULL,
    ResolutionTatMinutes INT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_su_Griev_Status DEFAULT('Open'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Griev_Created DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('support.FeedbackSurvey') IS NULL
CREATE TABLE support.FeedbackSurvey (
    SurveyId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_FeedbackSurvey PRIMARY KEY,
    PatientId BIGINT NULL,        -- cross-DB
    Score INT NULL,
    Comments NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_Survey_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.31 Queue & signage ---------------------------------------------- */
IF OBJECT_ID('support.QueueCounter') IS NULL
CREATE TABLE support.QueueCounter (
    CounterId INT IDENTITY(1,1) CONSTRAINT PK_su_QueueCounter PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    Area NVARCHAR(30) NOT NULL,
    CounterName NVARCHAR(40) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_su_QC_Active DEFAULT(1)
);
GO

IF OBJECT_ID('support.QueueToken') IS NULL
CREATE TABLE support.QueueToken (
    TokenId BIGINT IDENTITY(1,1) CONSTRAINT PK_su_QueueToken PRIMARY KEY,
    CounterId INT NOT NULL CONSTRAINT FK_su_QT_Counter REFERENCES support.QueueCounter(CounterId),
    TokenNo NVARCHAR(10) NOT NULL,
    PatientId BIGINT NULL,        -- cross-DB
    IssuedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_su_QT_Issued DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_su_QT_Status DEFAULT('Waiting')
);
GO
