/* =====================================================================
   Migration 0011 — ABDM, AI outputs, Compliance (Phases 1/11/12)
   SRS: §6.2 ABDM/ABHA/HFR/HPR/consent, §4 AI modules, §3.22/§10 compliance.
   AI model thresholds/endpoints are config; this stores OUTPUTS only.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* §6.2 ABDM consent artifacts (HIP/HIU) ------------------------------ */
IF OBJECT_ID('dbo.AbdmConsent') IS NULL
CREATE TABLE dbo.AbdmConsent (
    ConsentArtifactId BIGINT IDENTITY(1,1) CONSTRAINT PK_AbdmConsent PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Abdm_Patient REFERENCES dbo.Patient(PatientId),
    AbhaNumber NVARCHAR(20) NULL,
    Purpose NVARCHAR(120) NULL,
    HiTypes NVARCHAR(200) NULL,
    GrantedUtc DATETIME2(3) NULL,
    ExpiryUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Abdm_Status DEFAULT('Requested'),  -- Requested/Granted/Revoked/Expired
    FhirBundleUrl NVARCHAR(400) NULL
);
GO

/* §6.2 Facility (HFR) & Professional (HPR) registry links ------------ */
IF OBJECT_ID('dbo.HfrFacility') IS NULL
CREATE TABLE dbo.HfrFacility (
    HfrId INT IDENTITY(1,1) CONSTRAINT PK_HfrFacility PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Hfr_Branch REFERENCES dbo.Branch(BranchId),
    HfrCode NVARCHAR(40) NULL,
    OnboardedUtc DATETIME2(3) NULL
);
GO
IF OBJECT_ID('dbo.HprProfessional') IS NULL
CREATE TABLE dbo.HprProfessional (
    HprId INT IDENTITY(1,1) CONSTRAINT PK_HprProfessional PRIMARY KEY,
    DoctorId INT NOT NULL CONSTRAINT FK_Hpr_Doctor REFERENCES dbo.Doctor(DoctorId),
    HprCode NVARCHAR(40) NULL,
    OnboardedUtc DATETIME2(3) NULL
);
GO

/* §4 AI outputs (risk/forecast/fraud/pre-scrub) --------------------- */
IF OBJECT_ID('dbo.AiInsight') IS NULL
CREATE TABLE dbo.AiInsight (
    InsightId BIGINT IDENTITY(1,1) CONSTRAINT PK_AiInsight PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Ai_Branch REFERENCES dbo.Branch(BranchId),
    InsightType NVARCHAR(40) NOT NULL,  -- RiskPrediction/InventoryForecast/FraudDetection/ClaimPreScrub
    SubjectType NVARCHAR(40) NULL,      -- Patient/Drug/Claim
    SubjectId NVARCHAR(40) NULL,
    Score DECIMAL(7,4) NULL,
    DetailJson NVARCHAR(MAX) NULL,
    GeneratedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Ai_Utc DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.22/§10 Compliance reporting register --------------------------- */
IF OBJECT_ID('dbo.ComplianceReport') IS NULL
CREATE TABLE dbo.ComplianceReport (
    ReportId BIGINT IDENTITY(1,1) CONSTRAINT PK_ComplianceReport PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Comp_Branch REFERENCES dbo.Branch(BranchId),
    Regulation NVARCHAR(120) NOT NULL,  -- NABH/BMWM Form-IV/Factories Act/PC-PNDT/NDPS/DPDP...
    PeriodFrom DATE NULL, PeriodTo DATE NULL,
    GeneratedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Comp_Utc DEFAULT(SYSUTCDATETIME()),
    FileUrl NVARCHAR(400) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Comp_Status DEFAULT('Generated')
);
GO
