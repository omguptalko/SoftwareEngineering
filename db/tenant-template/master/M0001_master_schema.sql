/* =====================================================================
   Tenant MASTER DB template (L1.5/L1.8, applied to {Tenant}_Master)
   Per Decision D2 (one master DB per tenant) + D3 (longitudinal + master
   data lives here, not the per-fiscal-year DB). Proper schemas (R1/R2):
     master  : reference/master data (admin-editable) — drives F3 lookups
     patient : longitudinal patient/EMR identity
     proc    : stored procedures (numbering, etc.)
     audit   : per-tenant immutable audit
   Column contracts match the cut-over LookupRepository (L1.8). Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('master')  IS NULL EXEC('CREATE SCHEMA master');
GO
IF SCHEMA_ID('patient') IS NULL EXEC('CREATE SCHEMA patient');
GO
IF SCHEMA_ID('proc')    IS NULL EXEC('CREATE SCHEMA [proc]');
GO
IF SCHEMA_ID('audit')   IS NULL EXEC('CREATE SCHEMA audit');
GO

/* ---- master schema: reference/master data -------------------------- */
IF OBJECT_ID('master.Branch') IS NULL
CREATE TABLE master.Branch (
    BranchId INT IDENTITY(1,1) CONSTRAINT PK_m_Branch PRIMARY KEY,
    Code     NVARCHAR(10)  NOT NULL CONSTRAINT UQ_m_Branch_Code UNIQUE,
    Name     NVARCHAR(120) NOT NULL,
    City     NVARCHAR(80)  NULL,
    State    NVARCHAR(80)  NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_m_Branch_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.Doctor') IS NULL
CREATE TABLE master.Doctor (
    DoctorId   INT IDENTITY(1,1) CONSTRAINT PK_m_Doctor PRIMARY KEY,
    Code       NVARCHAR(20)  NOT NULL CONSTRAINT UQ_m_Doctor_Code UNIQUE,
    Name       NVARCHAR(120) NOT NULL,
    Department NVARCHAR(80)  NOT NULL,
    ConsultationFee DECIMAL(10,2) NULL,   -- per-doctor/specialisation OPD fee (billing Phase 1); NULL → falls back to the OPD-CONS tariff
    IsActive   BIT NOT NULL CONSTRAINT DF_m_Doctor_Active DEFAULT(1)
);
GO
-- Idempotent add for tenants provisioned before ConsultationFee existed.
IF COL_LENGTH('master.Doctor', 'ConsultationFee') IS NULL
    ALTER TABLE master.Doctor ADD ConsultationFee DECIMAL(10,2) NULL;
GO

IF OBJECT_ID('master.Drug') IS NULL
CREATE TABLE master.Drug (
    DrugId       INT IDENTITY(1,1) CONSTRAINT PK_m_Drug PRIMARY KEY,
    Code         NVARCHAR(20)  NOT NULL CONSTRAINT UQ_m_Drug_Code UNIQUE,
    Name         NVARCHAR(160) NOT NULL,
    Form         NVARCHAR(20)  NOT NULL,
    StockQty     INT NOT NULL CONSTRAINT DF_m_Drug_Stock DEFAULT(0),
    ReorderLevel INT NOT NULL CONSTRAINT DF_m_Drug_Reorder DEFAULT(0),
    IsActive     BIT NOT NULL CONSTRAINT DF_m_Drug_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.Icd10Code') IS NULL
CREATE TABLE master.Icd10Code (
    Code        NVARCHAR(10)  NOT NULL CONSTRAINT PK_m_Icd10 PRIMARY KEY,
    Description NVARCHAR(200) NOT NULL
);
GO

IF OBJECT_ID('master.Payer') IS NULL
CREATE TABLE master.Payer (
    PayerId   INT IDENTITY(1,1) CONSTRAINT PK_m_Payer PRIMARY KEY,
    Code      NVARCHAR(20)  NOT NULL CONSTRAINT UQ_m_Payer_Code UNIQUE,
    Name      NVARCHAR(160) NOT NULL,
    PayerType NVARCHAR(40)  NOT NULL,
    IsActive  BIT NOT NULL CONSTRAINT DF_m_Payer_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.HbpPackage') IS NULL
CREATE TABLE master.HbpPackage (
    PackageId INT IDENTITY(1,1) CONSTRAINT PK_m_Hbp PRIMARY KEY,
    Code      NVARCHAR(20)  NOT NULL CONSTRAINT UQ_m_Hbp_Code UNIQUE,
    Name      NVARCHAR(200) NOT NULL,
    Specialty NVARCHAR(80)  NULL,
    Rate      DECIMAL(12,2) NOT NULL,
    IsActive  BIT NOT NULL CONSTRAINT DF_m_Hbp_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.BloodGroup') IS NULL
CREATE TABLE master.BloodGroup (
    Code      NVARCHAR(5) NOT NULL CONSTRAINT PK_m_BloodGroup PRIMARY KEY,
    SortOrder INT NOT NULL CONSTRAINT DF_m_BloodGroup_Sort DEFAULT(0)
);
GO

IF OBJECT_ID('master.Tariff') IS NULL
CREATE TABLE master.Tariff (
    TariffId    INT IDENTITY(1,1) CONSTRAINT PK_m_Tariff PRIMARY KEY,
    BranchId    INT NULL CONSTRAINT FK_m_Tariff_Branch REFERENCES master.Branch(BranchId),
    ServiceCode NVARCHAR(30)  NOT NULL,
    ServiceName NVARCHAR(160) NOT NULL,
    Category    NVARCHAR(40)  NULL,
    Rate        DECIMAL(12,2) NOT NULL,
    GstRatePct  DECIMAL(5,2)  NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_m_Tariff_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.Ward') IS NULL
CREATE TABLE master.Ward (
    WardId   INT IDENTITY(1,1) CONSTRAINT PK_m_Ward PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_m_Ward_Branch REFERENCES master.Branch(BranchId),
    Name     NVARCHAR(80) NOT NULL
);
GO

IF OBJECT_ID('master.Bed') IS NULL
CREATE TABLE master.Bed (
    BedId  INT IDENTITY(1,1) CONSTRAINT PK_m_Bed PRIMARY KEY,
    WardId INT NOT NULL CONSTRAINT FK_m_Bed_Ward REFERENCES master.Ward(WardId),
    BedNo  NVARCHAR(20) NOT NULL,
    Status NVARCHAR(10) NOT NULL CONSTRAINT DF_m_Bed_Status DEFAULT('free')
);
GO

/* ---- patient schema: longitudinal identity (D3), full column set --- */
IF OBJECT_ID('patient.Patient') IS NULL
CREATE TABLE patient.Patient (
    PatientId   BIGINT IDENTITY(1,1) CONSTRAINT PK_p_Patient PRIMARY KEY,
    Uhid        NVARCHAR(30) NOT NULL CONSTRAINT UQ_p_Patient_Uhid UNIQUE,
    RegBranchId INT NULL CONSTRAINT FK_p_Patient_Branch REFERENCES master.Branch(BranchId),
    RegisteredAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_p_Patient_Reg DEFAULT(SYSUTCDATETIME()),
    FullName    NVARCHAR(120) NOT NULL,
    GuardianName NVARCHAR(120) NULL,
    AgeYears    INT NULL,
    DateOfBirth DATE NULL,
    Sex         NVARCHAR(10)  NULL,
    BloodGroup  NVARCHAR(5)   NULL,
    Mobile      NVARCHAR(15)  NULL,
    Email       NVARCHAR(120) NULL,
    MaritalStatus NVARCHAR(20) NULL,
    Category    NVARCHAR(40)  NULL,
    Address     NVARCHAR(200) NULL,
    City        NVARCHAR(80)  NULL,
    State       NVARCHAR(80)  NULL,
    Pincode     NVARCHAR(10)  NULL,
    Occupation  NVARCHAR(80)  NULL,
    EmployerPayerCode NVARCHAR(20) NULL,
    AadhaarMasked NVARCHAR(20) NULL,
    AbhaNumber  NVARCHAR(20)  NULL,
    AbhaAddress NVARCHAR(80)  NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_p_Patient_Active DEFAULT(1)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_p_Patient_Search')
    CREATE INDEX IX_p_Patient_Search ON patient.Patient(FullName, Mobile);
GO

/* ---- audit schema -------------------------------------------------- */
IF OBJECT_ID('audit.AuditEntry') IS NULL
CREATE TABLE audit.AuditEntry (
    AuditId       BIGINT IDENTITY(1,1) CONSTRAINT PK_m_Audit PRIMARY KEY,
    OccurredAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_m_Audit_When DEFAULT(SYSUTCDATETIME()),
    BranchId      INT NULL,
    UserId        BIGINT NULL,
    UserName      NVARCHAR(120) NULL,
    Action        NVARCHAR(120) NOT NULL,
    Entity        NVARCHAR(120) NOT NULL,
    EntityId      NVARCHAR(80)  NULL,
    PayloadJson   NVARCHAR(MAX) NULL,
    Succeeded     BIT NOT NULL,
    Error         NVARCHAR(MAX) NULL
);
GO

/* ---- proc schema: UHID generator (per branch+year) ----------------- */
CREATE OR ALTER PROCEDURE [proc].usp_NextUhid @BranchId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = YEAR(SYSUTCDATETIME());
    DECLARE @Code NVARCHAR(10) = (SELECT Code FROM master.Branch WHERE BranchId = @BranchId);
    SELECT CONCAT(ISNULL(@Code, CONCAT('BR', @BranchId)), '-', @Year, '-', RIGHT('000000' + CAST(ABS(CHECKSUM(NEWID())) % 1000000 AS VARCHAR(6)), 6));
END
GO
