/* =====================================================================
   Migration 0010 — Support & Statutory modules (Phases 5 & 10)
   SRS: §3.6 Ambulance, §3.26 Diet, §3.25 BMWM, §3.27 Mortuary,
        §3.28 MLC, §3.29 Consent, §3.16 Certificates, §3.30 Feedback,
        §3.31 Queue.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* §3.6 Ambulance & GPS ----------------------------------------------- */
IF OBJECT_ID('dbo.Ambulance') IS NULL
CREATE TABLE dbo.Ambulance (
    AmbulanceId INT IDENTITY(1,1) CONSTRAINT PK_Ambulance PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Amb_Branch REFERENCES dbo.Branch(BranchId),
    VehicleNo NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Amb_Status DEFAULT('Available')
);
GO
IF OBJECT_ID('dbo.AmbulanceDispatch') IS NULL
CREATE TABLE dbo.AmbulanceDispatch (
    DispatchId BIGINT IDENTITY(1,1) CONSTRAINT PK_AmbDispatch PRIMARY KEY,
    AmbulanceId INT NOT NULL CONSTRAINT FK_Disp_Amb REFERENCES dbo.Ambulance(AmbulanceId),
    CallLoggedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AmbDisp_Logged DEFAULT(SYSUTCDATETIME()),
    PickupLat DECIMAL(9,6) NULL, PickupLng DECIMAL(9,6) NULL,
    LastLat DECIMAL(9,6) NULL, LastLng DECIMAL(9,6) NULL,
    ArrivedUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_AmbDisp_Status DEFAULT('Dispatched')
);
GO

/* §3.26 Diet & Kitchen ----------------------------------------------- */
IF OBJECT_ID('dbo.DietOrder') IS NULL
CREATE TABLE dbo.DietOrder (
    DietOrderId BIGINT IDENTITY(1,1) CONSTRAINT PK_DietOrder PRIMARY KEY,
    AdmissionId BIGINT NOT NULL CONSTRAINT FK_Diet_Adm REFERENCES dbo.Admission(AdmissionId),
    DietType NVARCHAR(40) NOT NULL,    -- Therapeutic/Routine
    OrderedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Diet_Utc DEFAULT(SYSUTCDATETIME()),
    Cost DECIMAL(10,2) NULL
);
GO

/* §3.25 Bio-Medical Waste (BMWM Rules 2016) -------------------------- */
IF OBJECT_ID('dbo.WasteColourCode') IS NULL
CREATE TABLE dbo.WasteColourCode (
    ColourCode NVARCHAR(20) NOT NULL CONSTRAINT PK_WasteColour PRIMARY KEY,  -- Yellow/Red/White/Blue
    Description NVARCHAR(200) NOT NULL
);
GO
IF OBJECT_ID('dbo.WasteBag') IS NULL
CREATE TABLE dbo.WasteBag (
    BagId BIGINT IDENTITY(1,1) CONSTRAINT PK_WasteBag PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Waste_Branch REFERENCES dbo.Branch(BranchId),
    Barcode NVARCHAR(40) NOT NULL CONSTRAINT UQ_WasteBag_Barcode UNIQUE,
    ColourCode NVARCHAR(20) NOT NULL CONSTRAINT FK_Waste_Colour REFERENCES dbo.WasteColourCode(ColourCode),
    WeightKg DECIMAL(7,2) NULL,
    GeneratedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Waste_Gen DEFAULT(SYSUTCDATETIME()),
    CbwtfHandoverUtc DATETIME2(3) NULL
);
GO

/* §3.27 Mortuary ----------------------------------------------------- */
IF OBJECT_ID('dbo.MortuaryRecord') IS NULL
CREATE TABLE dbo.MortuaryRecord (
    RecordId BIGINT IDENTITY(1,1) CONSTRAINT PK_MortuaryRecord PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Mort_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_Mort_Patient REFERENCES dbo.Patient(PatientId),
    StorageNo NVARCHAR(20) NULL,
    AdmittedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Mort_Admitted DEFAULT(SYSUTCDATETIME()),
    ReleasedUtc DATETIME2(3) NULL,
    PoliceIntimated BIT NOT NULL CONSTRAINT DF_Mort_Police DEFAULT(0),
    MlcLinked BIT NOT NULL CONSTRAINT DF_Mort_Mlc DEFAULT(0)
);
GO

/* §3.28 Medico-Legal Case -------------------------------------------- */
IF OBJECT_ID('dbo.MlcCase') IS NULL
CREATE TABLE dbo.MlcCase (
    MlcId BIGINT IDENTITY(1,1) CONSTRAINT PK_MlcCase PRIMARY KEY,
    MlcNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_Mlc_No UNIQUE,
    BranchId INT NOT NULL CONSTRAINT FK_Mlc_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_Mlc_Patient REFERENCES dbo.Patient(PatientId),
    PoliceStation NVARCHAR(120) NULL,
    PoliceAckRef NVARCHAR(60) NULL,
    InjuryDetails NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Mlc_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.29 Consent & e-Documents ---------------------------------------- */
IF OBJECT_ID('dbo.ConsentTemplate') IS NULL
CREATE TABLE dbo.ConsentTemplate (
    TemplateId INT IDENTITY(1,1) CONSTRAINT PK_ConsentTemplate PRIMARY KEY,
    Code NVARCHAR(40) NOT NULL CONSTRAINT UQ_ConsentTpl_Code UNIQUE,
    Title NVARCHAR(160) NOT NULL,
    LanguageCode NVARCHAR(10) NOT NULL CONSTRAINT DF_ConsentTpl_Lang DEFAULT('en'),
    Body NVARCHAR(MAX) NOT NULL,
    Version INT NOT NULL CONSTRAINT DF_ConsentTpl_Ver DEFAULT(1)
);
GO
IF OBJECT_ID('dbo.ConsentCapture') IS NULL
CREATE TABLE dbo.ConsentCapture (
    ConsentId BIGINT IDENTITY(1,1) CONSTRAINT PK_ConsentCapture PRIMARY KEY,
    TemplateId INT NOT NULL CONSTRAINT FK_Consent_Tpl REFERENCES dbo.ConsentTemplate(TemplateId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Consent_Patient REFERENCES dbo.Patient(PatientId),
    SignatureType NVARCHAR(20) NULL,   -- e-Signature/Thumb
    CapturedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Consent_Utc DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.16 Certificates ------------------------------------------------- */
IF OBJECT_ID('dbo.CertificateTemplate') IS NULL
CREATE TABLE dbo.CertificateTemplate (
    TemplateId INT IDENTITY(1,1) CONSTRAINT PK_CertTemplate PRIMARY KEY,
    CertType NVARCHAR(40) NOT NULL CONSTRAINT UQ_CertTpl_Type UNIQUE,  -- Birth/Death/Referral/Fitness/Medical/Discharge
    Title NVARCHAR(160) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL
);
GO
IF OBJECT_ID('dbo.IssuedCertificate') IS NULL
CREATE TABLE dbo.IssuedCertificate (
    CertId BIGINT IDENTITY(1,1) CONSTRAINT PK_IssuedCert PRIMARY KEY,
    TemplateId INT NOT NULL CONSTRAINT FK_Cert_Tpl REFERENCES dbo.CertificateTemplate(TemplateId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Cert_Patient REFERENCES dbo.Patient(PatientId),
    ApprovedByDoctorId INT NULL CONSTRAINT FK_Cert_Doctor REFERENCES dbo.Doctor(DoctorId),
    IssuedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Cert_Utc DEFAULT(SYSUTCDATETIME()),
    PdfUrl NVARCHAR(400) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Cert_Status DEFAULT('Draft')
);
GO

/* §3.30 Feedback & Grievance ----------------------------------------- */
IF OBJECT_ID('dbo.Grievance') IS NULL
CREATE TABLE dbo.Grievance (
    GrievanceId BIGINT IDENTITY(1,1) CONSTRAINT PK_Grievance PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Griev_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_Griev_Patient REFERENCES dbo.Patient(PatientId),
    Category NVARCHAR(60) NULL,
    SlaDueUtc DATETIME2(3) NULL,
    ResolutionTatMinutes INT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Griev_Status DEFAULT('Open'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Griev_Created DEFAULT(SYSUTCDATETIME())
);
GO
IF OBJECT_ID('dbo.FeedbackSurvey') IS NULL
CREATE TABLE dbo.FeedbackSurvey (
    SurveyId BIGINT IDENTITY(1,1) CONSTRAINT PK_FeedbackSurvey PRIMARY KEY,
    PatientId BIGINT NULL CONSTRAINT FK_Survey_Patient REFERENCES dbo.Patient(PatientId),
    Score INT NULL,
    Comments NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Survey_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.31 Queue & Digital Signage -------------------------------------- */
IF OBJECT_ID('dbo.QueueCounter') IS NULL
CREATE TABLE dbo.QueueCounter (
    CounterId INT IDENTITY(1,1) CONSTRAINT PK_QueueCounter PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_QC_Branch REFERENCES dbo.Branch(BranchId),
    Area NVARCHAR(30) NOT NULL,        -- OPD/Pharmacy/Billing
    CounterName NVARCHAR(40) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_QC_Active DEFAULT(1)
);
GO
IF OBJECT_ID('dbo.QueueToken') IS NULL
CREATE TABLE dbo.QueueToken (
    TokenId BIGINT IDENTITY(1,1) CONSTRAINT PK_QueueToken PRIMARY KEY,
    CounterId INT NOT NULL CONSTRAINT FK_QT_Counter REFERENCES dbo.QueueCounter(CounterId),
    TokenNo NVARCHAR(10) NOT NULL,
    PatientId BIGINT NULL CONSTRAINT FK_QT_Patient REFERENCES dbo.Patient(PatientId),
    IssuedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_QT_Issued DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_QT_Status DEFAULT('Waiting')
);
GO
