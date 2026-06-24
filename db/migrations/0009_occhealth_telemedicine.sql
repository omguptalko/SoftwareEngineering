/* =====================================================================
   Migration 0009 — Occupational Health & Telemedicine (Phase 9)
   SRS: §3.23 Occupational Health (Factories Act 1948), §3.24 Telemedicine
        (TPG 2020). Company contracts are master data.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.CompanyContract') IS NULL
CREATE TABLE dbo.CompanyContract (
    ContractId INT IDENTITY(1,1) CONSTRAINT PK_CompanyContract PRIMARY KEY,
    CompanyName NVARCHAR(160) NOT NULL,
    PayerCode NVARCHAR(20) NULL,
    ContractType NVARCHAR(40) NULL,   -- PEME/PME/Corporate
    ValidFrom DATE NULL, ValidTo DATE NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Contract_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.MedicalExam') IS NULL
CREATE TABLE dbo.MedicalExam (
    ExamId BIGINT IDENTITY(1,1) CONSTRAINT PK_MedicalExam PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Exam_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_Exam_Patient REFERENCES dbo.Patient(PatientId),
    ContractId INT NULL CONSTRAINT FK_Exam_Contract REFERENCES dbo.CompanyContract(ContractId),
    ExamType NVARCHAR(20) NOT NULL,    -- PEME/PME
    ExamDate DATE NOT NULL,
    FitnessResult NVARCHAR(30) NULL,   -- Fit/Unfit/Fit-with-conditions
    Audiometry NVARCHAR(40) NULL, Spirometry NVARCHAR(40) NULL, Vision NVARCHAR(40) NULL,
    VaccinationNotes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('dbo.HazardExposure') IS NULL
CREATE TABLE dbo.HazardExposure (
    ExposureId BIGINT IDENTITY(1,1) CONSTRAINT PK_HazardExposure PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Hazard_Patient REFERENCES dbo.Patient(PatientId),
    HazardType NVARCHAR(40) NOT NULL,   -- noise/dust/chemical/vision
    RecordedDate DATE NOT NULL,
    Notes NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('dbo.WorkplaceInjury') IS NULL
CREATE TABLE dbo.WorkplaceInjury (
    InjuryId BIGINT IDENTITY(1,1) CONSTRAINT PK_WorkplaceInjury PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Injury_Patient REFERENCES dbo.Patient(PatientId),
    ContractId INT NULL CONSTRAINT FK_Injury_Contract REFERENCES dbo.CompanyContract(ContractId),
    InjuryDate DATETIME2(0) NOT NULL,
    MlcLinked BIT NOT NULL CONSTRAINT DF_Injury_Mlc DEFAULT(0),
    Description NVARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('dbo.TeleConsult') IS NULL
CREATE TABLE dbo.TeleConsult (
    TeleId BIGINT IDENTITY(1,1) CONSTRAINT PK_TeleConsult PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Tele_Patient REFERENCES dbo.Patient(PatientId),
    DoctorId INT NULL CONSTRAINT FK_Tele_Doctor REFERENCES dbo.Doctor(DoctorId),
    FromBranchId INT NULL CONSTRAINT FK_Tele_From REFERENCES dbo.Branch(BranchId),
    ToBranchId INT NULL CONSTRAINT FK_Tele_To REFERENCES dbo.Branch(BranchId),
    ConsultType NVARCHAR(30) NULL,     -- Video/Audio/Tele-ICU/Tele-Radiology
    ScheduledUtc DATETIME2(0) NULL,
    ConsentCaptured BIT NOT NULL CONSTRAINT DF_Tele_Consent DEFAULT(0),
    EPrescriptionSigned BIT NOT NULL CONSTRAINT DF_Tele_Esign DEFAULT(0),
    SessionAuditUrl NVARCHAR(400) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Tele_Status DEFAULT('Scheduled')
);
GO
