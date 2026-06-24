/* =====================================================================
   Tenant MASTER DB template (L1.5, applied to {Tenant}_Master)
   Per Decision D2 (one master DB per tenant) + D3 (longitudinal data lives
   in master, not the per-fiscal-year DB). Proper schemas (R1/R2):
     master  : reference/master data (admin-editable)
     patient : longitudinal patient/EMR identity
     proc    : stored procedures (numbering, etc.)
     audit   : per-tenant immutable audit
   Representative table set — the full 90-table refactor is L1.1.2-4.
   Idempotent: safe to re-run (the provisioning engine may re-apply).
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
    IsActive   BIT NOT NULL CONSTRAINT DF_m_Doctor_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.Tariff') IS NULL
CREATE TABLE master.Tariff (
    TariffId INT IDENTITY(1,1) CONSTRAINT PK_m_Tariff PRIMARY KEY,
    BranchId INT NULL CONSTRAINT FK_m_Tariff_Branch REFERENCES master.Branch(BranchId),
    Code     NVARCHAR(30)  NOT NULL,
    Name     NVARCHAR(160) NOT NULL,
    Rate     DECIMAL(12,2) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_m_Tariff_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.BloodGroup') IS NULL
CREATE TABLE master.BloodGroup (
    Code      NVARCHAR(5) NOT NULL CONSTRAINT PK_m_BloodGroup PRIMARY KEY,
    SortOrder INT NOT NULL CONSTRAINT DF_m_BloodGroup_Sort DEFAULT(0)
);
GO

/* ---- patient schema: longitudinal identity (D3) -------------------- */
IF OBJECT_ID('patient.Patient') IS NULL
CREATE TABLE patient.Patient (
    PatientId   BIGINT IDENTITY(1,1) CONSTRAINT PK_p_Patient PRIMARY KEY,
    Uhid        NVARCHAR(30) NOT NULL CONSTRAINT UQ_p_Patient_Uhid UNIQUE,
    RegBranchId INT NULL CONSTRAINT FK_p_Patient_Branch REFERENCES master.Branch(BranchId),
    RegisteredAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_p_Patient_Reg DEFAULT(SYSUTCDATETIME()),
    FullName    NVARCHAR(160) NOT NULL,
    Sex         NVARCHAR(10)  NULL,
    Mobile      NVARCHAR(20)  NULL,
    AadhaarMasked NVARCHAR(20) NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_p_Patient_Active DEFAULT(1)
);
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
