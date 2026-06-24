/* =====================================================================
   Tenant per-FISCAL-YEAR DB — scheme / occ-health / telemedicine (L1.8)
   Fiscal-scoped. Cross-DB refs (Patient/Branch/Doctor/Package/Contract →
   master/patient DB) are PLAIN columns. PmjayCase→insurance.Claim is an
   intra-DB cross-schema FK (kept). Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('scheme')       IS NULL EXEC('CREATE SCHEMA scheme');
GO
IF SCHEMA_ID('occhealth')    IS NULL EXEC('CREATE SCHEMA occhealth');
GO
IF SCHEMA_ID('telemedicine') IS NULL EXEC('CREATE SCHEMA telemedicine');
GO

/* ---- scheme (§7.3 PM-JAY, §7.4-7.7 membership) --------------------- */
IF OBJECT_ID('scheme.PmjayBeneficiary') IS NULL
CREATE TABLE scheme.PmjayBeneficiary (
    BeneficiaryId BIGINT IDENTITY(1,1) CONSTRAINT PK_s_PmjayBeneficiary PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    PmjayId NVARCHAR(40) NULL,
    BisVerified BIT NOT NULL CONSTRAINT DF_s_Pmjay_Bis DEFAULT(0),
    FamilyFloater DECIMAL(14,2) NULL,
    UsedAmount DECIMAL(14,2) NULL
);
GO

IF OBJECT_ID('scheme.PmjayCase') IS NULL
CREATE TABLE scheme.PmjayCase (
    CaseId BIGINT IDENTITY(1,1) CONSTRAINT PK_s_PmjayCase PRIMARY KEY,
    ClaimId BIGINT NOT NULL CONSTRAINT FK_s_PmjayCase_Claim REFERENCES insurance.Claim(ClaimId),
    PackageId INT NULL,           -- master.HbpPackage (cross-DB)
    TmsCaseNo NVARCHAR(40) NULL,
    AyushmanMitra NVARCHAR(80) NULL,
    AadhaarDischargeVerified BIT NOT NULL CONSTRAINT DF_s_Pmjay_Discharge DEFAULT(0)
);
GO

IF OBJECT_ID('scheme.SchemeMembership') IS NULL
CREATE TABLE scheme.SchemeMembership (
    MembershipId BIGINT IDENTITY(1,1) CONSTRAINT PK_s_SchemeMembership PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    SchemeType NVARCHAR(20) NOT NULL,
    MemberNo NVARCHAR(60) NULL,
    SecondaryRef NVARCHAR(60) NULL,
    Verified BIT NOT NULL CONSTRAINT DF_s_Scheme_Verified DEFAULT(0),
    ValidTo DATE NULL
);
GO

/* ---- occupational health (§3.23) — CompanyContract in master DB ---- */
IF OBJECT_ID('occhealth.MedicalExam') IS NULL
CREATE TABLE occhealth.MedicalExam (
    ExamId BIGINT IDENTITY(1,1) CONSTRAINT PK_o_MedicalExam PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    PatientId BIGINT NULL,        -- cross-DB
    ContractId INT NULL,          -- master.CompanyContract (cross-DB)
    ExamType NVARCHAR(20) NOT NULL,
    ExamDate DATE NOT NULL,
    FitnessResult NVARCHAR(30) NULL,
    Audiometry NVARCHAR(40) NULL, Spirometry NVARCHAR(40) NULL, Vision NVARCHAR(40) NULL,
    VaccinationNotes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('occhealth.HazardExposure') IS NULL
CREATE TABLE occhealth.HazardExposure (
    ExposureId BIGINT IDENTITY(1,1) CONSTRAINT PK_o_HazardExposure PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    HazardType NVARCHAR(40) NOT NULL,
    RecordedDate DATE NOT NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('occhealth.WorkplaceInjury') IS NULL
CREATE TABLE occhealth.WorkplaceInjury (
    InjuryId BIGINT IDENTITY(1,1) CONSTRAINT PK_o_WorkplaceInjury PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    ContractId INT NULL,          -- cross-DB
    InjuryDate DATETIME2(0) NOT NULL,
    MlcLinked BIT NOT NULL CONSTRAINT DF_o_Injury_Mlc DEFAULT(0),
    Description NVARCHAR(MAX) NULL
);
GO

/* ---- telemedicine (§3.24) ------------------------------------------ */
IF OBJECT_ID('telemedicine.TeleConsult') IS NULL
CREATE TABLE telemedicine.TeleConsult (
    TeleId BIGINT IDENTITY(1,1) CONSTRAINT PK_t_TeleConsult PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    DoctorId INT NULL,            -- cross-DB
    FromBranchId INT NULL,        -- cross-DB
    ToBranchId INT NULL,          -- cross-DB
    ConsultType NVARCHAR(30) NULL,
    ScheduledUtc DATETIME2(0) NULL,
    ConsentCaptured BIT NOT NULL CONSTRAINT DF_t_Tele_Consent DEFAULT(0),
    EPrescriptionSigned BIT NOT NULL CONSTRAINT DF_t_Tele_Esign DEFAULT(0),
    SessionAuditUrl NVARCHAR(400) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_t_Tele_Status DEFAULT('Scheduled')
);
GO
