/* =====================================================================
   Tenant per-FISCAL-YEAR DB — AI outputs / compliance / analytics (L1.8)
   Fiscal-scoped outputs & registers. BranchId is a cross-DB plain column.
   Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('ai')         IS NULL EXEC('CREATE SCHEMA ai');
GO
IF SCHEMA_ID('compliance') IS NULL EXEC('CREATE SCHEMA compliance');
GO
IF SCHEMA_ID('analytics')  IS NULL EXEC('CREATE SCHEMA analytics');
GO

/* §4 AI outputs ------------------------------------------------------ */
IF OBJECT_ID('ai.AiInsight') IS NULL
CREATE TABLE ai.AiInsight (
    InsightId BIGINT IDENTITY(1,1) CONSTRAINT PK_ai_AiInsight PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    InsightType NVARCHAR(40) NOT NULL,
    SubjectType NVARCHAR(40) NULL,
    SubjectId NVARCHAR(40) NULL,
    Score DECIMAL(7,4) NULL,
    DetailJson NVARCHAR(MAX) NULL,
    GeneratedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_ai_Utc DEFAULT(SYSUTCDATETIME())
);
GO

/* §3.22/§10 Compliance reports --------------------------------------- */
IF OBJECT_ID('compliance.ComplianceReport') IS NULL
CREATE TABLE compliance.ComplianceReport (
    ReportId BIGINT IDENTITY(1,1) CONSTRAINT PK_co_ComplianceReport PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    Regulation NVARCHAR(120) NOT NULL,
    PeriodFrom DATE NULL, PeriodTo DATE NULL,
    GeneratedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_co_Utc DEFAULT(SYSUTCDATETIME()),
    FileUrl NVARCHAR(400) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_co_Status DEFAULT('Generated')
);
GO

/* §3.20 Dashboard snapshots ------------------------------------------ */
IF OBJECT_ID('analytics.DashboardKpi') IS NULL
CREATE TABLE analytics.DashboardKpi (
    KpiId     INT IDENTITY(1,1) CONSTRAINT PK_an_DashboardKpi PRIMARY KEY,
    BranchId  INT NOT NULL,       -- cross-DB
    [Value]   NVARCHAR(20)  NOT NULL,
    Label     NVARCHAR(60)  NOT NULL,
    Trend     NVARCHAR(20)  NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_an_Kpi_Sort DEFAULT(0)
);
GO

IF OBJECT_ID('analytics.ServiceActivityDaily') IS NULL
CREATE TABLE analytics.ServiceActivityDaily (
    RowId     INT IDENTITY(1,1) CONSTRAINT PK_an_ServiceActivity PRIMARY KEY,
    BranchId  INT NOT NULL,       -- cross-DB
    Service   NVARCHAR(80)  NOT NULL,
    [Count]   INT NOT NULL,
    Revenue   DECIMAL(14,2) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_an_SAD_Sort DEFAULT(0)
);
GO
