/* =====================================================================
   Migration 0002 — Masters & Reference data tables (Phase 1)
   SRS: §3.7 blood groups, §3.8/§3.10 drugs, §7.1 ICD-10, §3.4 wards/beds,
        §3.15/§7 payers, §7.3 HBP packages, §3.20 dashboard.
   These tables hold the reference data that USED to be hardcoded in data.js.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.Doctor') IS NULL
CREATE TABLE dbo.Doctor (
    DoctorId   INT IDENTITY(1,1) CONSTRAINT PK_Doctor PRIMARY KEY,
    Code       NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Doctor_Code UNIQUE,
    Name       NVARCHAR(120) NOT NULL,
    Department NVARCHAR(80)  NOT NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Doctor_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.Drug') IS NULL
CREATE TABLE dbo.Drug (
    DrugId       INT IDENTITY(1,1) CONSTRAINT PK_Drug PRIMARY KEY,
    Code         NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Drug_Code UNIQUE,
    Name         NVARCHAR(160) NOT NULL,
    Form         NVARCHAR(20)  NOT NULL,
    StockQty     INT NOT NULL CONSTRAINT DF_Drug_Stock DEFAULT(0),
    ReorderLevel INT NOT NULL CONSTRAINT DF_Drug_Reorder DEFAULT(0),
    IsActive     BIT NOT NULL CONSTRAINT DF_Drug_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.Icd10Code') IS NULL
CREATE TABLE dbo.Icd10Code (
    Code        NVARCHAR(10)  NOT NULL CONSTRAINT PK_Icd10 PRIMARY KEY,
    Description NVARCHAR(200) NOT NULL
);
GO

IF OBJECT_ID('dbo.Ward') IS NULL
CREATE TABLE dbo.Ward (
    WardId   INT IDENTITY(1,1) CONSTRAINT PK_Ward PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Ward_Branch REFERENCES dbo.Branch(BranchId),
    Name     NVARCHAR(80) NOT NULL
);
GO

IF OBJECT_ID('dbo.Bed') IS NULL
CREATE TABLE dbo.Bed (
    BedId  INT IDENTITY(1,1) CONSTRAINT PK_Bed PRIMARY KEY,
    WardId INT NOT NULL CONSTRAINT FK_Bed_Ward REFERENCES dbo.Ward(WardId),
    BedNo  NVARCHAR(20) NOT NULL,
    Status NVARCHAR(10) NOT NULL CONSTRAINT DF_Bed_Status DEFAULT('free')  -- free/occ/clean/block
);
GO

IF OBJECT_ID('dbo.Payer') IS NULL
CREATE TABLE dbo.Payer (
    PayerId   INT IDENTITY(1,1) CONSTRAINT PK_Payer PRIMARY KEY,
    Code      NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Payer_Code UNIQUE,
    Name      NVARCHAR(160) NOT NULL,
    PayerType NVARCHAR(40)  NOT NULL,   -- Private Insurer/TPA/Govt Scheme/Corporate
    IsActive  BIT NOT NULL CONSTRAINT DF_Payer_Active DEFAULT(1)
);
GO

/* PM-JAY HBP package rate master (SRS §7.3) — rates admin-editable -------*/
IF OBJECT_ID('dbo.HbpPackage') IS NULL
CREATE TABLE dbo.HbpPackage (
    PackageId INT IDENTITY(1,1) CONSTRAINT PK_HbpPackage PRIMARY KEY,
    Code      NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Hbp_Code UNIQUE,
    Name      NVARCHAR(200) NOT NULL,
    Specialty NVARCHAR(80)  NULL,
    Rate      DECIMAL(12,2) NOT NULL,
    IsActive  BIT NOT NULL CONSTRAINT DF_Hbp_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.BloodGroup') IS NULL
CREATE TABLE dbo.BloodGroup (
    Code      NVARCHAR(5) NOT NULL CONSTRAINT PK_BloodGroup PRIMARY KEY,
    SortOrder INT NOT NULL CONSTRAINT DF_BloodGroup_Sort DEFAULT(0)
);
GO

/* Dashboard snapshot tables (SRS §3.20). Populated by analytics jobs in
   later phases; seeded now so the UI is DB-driven, not HTML-hardcoded. */
IF OBJECT_ID('dbo.DashboardKpi') IS NULL
CREATE TABLE dbo.DashboardKpi (
    KpiId     INT IDENTITY(1,1) CONSTRAINT PK_DashboardKpi PRIMARY KEY,
    BranchId  INT NOT NULL CONSTRAINT FK_Kpi_Branch REFERENCES dbo.Branch(BranchId),
    [Value]   NVARCHAR(20)  NOT NULL,
    Label     NVARCHAR(60)  NOT NULL,
    Trend     NVARCHAR(20)  NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_Kpi_Sort DEFAULT(0)
);
GO

IF OBJECT_ID('dbo.ServiceActivityDaily') IS NULL
CREATE TABLE dbo.ServiceActivityDaily (
    RowId     INT IDENTITY(1,1) CONSTRAINT PK_ServiceActivity PRIMARY KEY,
    BranchId  INT NOT NULL CONSTRAINT FK_SAD_Branch REFERENCES dbo.Branch(BranchId),
    Service   NVARCHAR(80)  NOT NULL,
    [Count]   INT NOT NULL,
    Revenue   DECIMAL(14,2) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_SAD_Sort DEFAULT(0)
);
GO
