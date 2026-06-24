/* =====================================================================
   L1 Migration P0001 — Control Plane (HIS_Platform)
   Plan: L1EnhancementDevPlanCumTracker.md  Phases L1.0 + L1.1 + L1.2.
   Creates the SaaS control plane with PROPER SCHEMAS (R1/R2):
     platform : tenant / fiscal-year / domain / db-catalog / billing
     security : identity + dynamic module/page RBAC
     audit    : control-plane immutable audit
   Idempotent: safe to re-run. Run against the HIS_Platform database.
   Decisions D1–D7 (confirmed 2026-06-24) are baked in:
     D1 per-tenant FY (FyStartMonth/Day on Tenant), D2 per-tenant master DB,
     D5 config-driven hosting (DbCatalog.ConnectionRef), D6 platform-only
     superadmin (security.AppUser.IsSuperAdmin, TenantId NULL).
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('platform') IS NULL EXEC('CREATE SCHEMA platform');
GO
IF SCHEMA_ID('security') IS NULL EXEC('CREATE SCHEMA security');
GO
IF SCHEMA_ID('audit') IS NULL EXEC('CREATE SCHEMA audit');
GO

/* ===================== platform schema (tenant registry) ============= */

/* Tenant = a hospital onboarded on the SaaS. FyStart* defines its fiscal
   year boundary (D1: per-tenant, default Apr 1). */
IF OBJECT_ID('platform.Tenant') IS NULL
CREATE TABLE platform.Tenant (
    TenantId     INT IDENTITY(1,1) CONSTRAINT PK_Tenant PRIMARY KEY,
    Code         NVARCHAR(40)  NOT NULL CONSTRAINT UQ_Tenant_Code UNIQUE,
    Name         NVARCHAR(160) NOT NULL,
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_Tenant_Status DEFAULT('Active'),
    FyStartMonth TINYINT       NOT NULL CONSTRAINT DF_Tenant_FyMon DEFAULT(4),   -- Apr (config default, editable)
    FyStartDay   TINYINT       NOT NULL CONSTRAINT DF_Tenant_FyDay DEFAULT(1),
    CreatedUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Tenant_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* Per-tenant fiscal years (D1). One row per tenant per FY. */
IF OBJECT_ID('platform.FiscalYear') IS NULL
CREATE TABLE platform.FiscalYear (
    FiscalYearId INT IDENTITY(1,1) CONSTRAINT PK_FiscalYear PRIMARY KEY,
    TenantId     INT NOT NULL CONSTRAINT FK_FY_Tenant REFERENCES platform.Tenant(TenantId),
    Code         NVARCHAR(20) NOT NULL,           -- e.g. FY2025-26
    StartDate    DATE NOT NULL,
    EndDate      DATE NOT NULL,
    IsCurrent    BIT  NOT NULL CONSTRAINT DF_FY_Current DEFAULT(0),
    CONSTRAINT UQ_FY_Tenant_Code UNIQUE (TenantId, Code)
);
GO

/* Domain → tenant mapping (R5/D4): own domain + common-domain subdomain. */
IF OBJECT_ID('platform.TenantDomain') IS NULL
CREATE TABLE platform.TenantDomain (
    DomainId  INT IDENTITY(1,1) CONSTRAINT PK_TenantDomain PRIMARY KEY,
    TenantId  INT NOT NULL CONSTRAINT FK_Domain_Tenant REFERENCES platform.Tenant(TenantId),
    Host      NVARCHAR(190) NOT NULL CONSTRAINT UQ_Domain_Host UNIQUE,  -- e.g. lko.finnidhospital.in / br1.app.finnid.in
    IsPrimary BIT NOT NULL CONSTRAINT DF_Domain_Primary DEFAULT(0),
    IsCommon  BIT NOT NULL CONSTRAINT DF_Domain_Common  DEFAULT(0)      -- the shared/common-domain alias
);
GO

/* DB catalog (R4/D2/D5): physical DB routing per tenant. DbKind 'master'
   (FiscalYearId NULL) or 'data' (per FY). ConnectionRef names a config/
   Key Vault secret — never a literal connection string. */
IF OBJECT_ID('platform.DbCatalog') IS NULL
CREATE TABLE platform.DbCatalog (
    DbCatalogId  INT IDENTITY(1,1) CONSTRAINT PK_DbCatalog PRIMARY KEY,
    TenantId     INT NOT NULL CONSTRAINT FK_DbCat_Tenant REFERENCES platform.Tenant(TenantId),
    FiscalYearId INT NULL     CONSTRAINT FK_DbCat_FY REFERENCES platform.FiscalYear(FiscalYearId),
    DbKind       NVARCHAR(10) NOT NULL,           -- master / data
    DbName       NVARCHAR(128) NOT NULL,
    ConnectionRef NVARCHAR(120) NULL,             -- config/Key Vault key, not the secret itself
    CreatedUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_DbCat_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT UQ_DbCat UNIQUE (TenantId, DbKind, FiscalYearId)
);
GO

IF OBJECT_ID('platform.Subscription') IS NULL
CREATE TABLE platform.Subscription (
    SubscriptionId INT IDENTITY(1,1) CONSTRAINT PK_Subscription PRIMARY KEY,
    TenantId  INT NOT NULL CONSTRAINT FK_Sub_Tenant REFERENCES platform.Tenant(TenantId),
    [Plan]    NVARCHAR(40) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate   DATE NULL,
    Status    NVARCHAR(20) NOT NULL CONSTRAINT DF_Sub_Status DEFAULT('Active')
);
GO

/* Per-fiscal-year billing ledger (R3: "billing per fiscal year"). */
IF OBJECT_ID('platform.BillingLedger') IS NULL
CREATE TABLE platform.BillingLedger (
    LedgerId     BIGINT IDENTITY(1,1) CONSTRAINT PK_BillingLedger PRIMARY KEY,
    TenantId     INT NOT NULL CONSTRAINT FK_Ledger_Tenant REFERENCES platform.Tenant(TenantId),
    FiscalYearId INT NOT NULL CONSTRAINT FK_Ledger_FY REFERENCES platform.FiscalYear(FiscalYearId),
    EntryType    NVARCHAR(30) NOT NULL,           -- Subscription/Usage/Adjustment/CarryForward
    Amount       DECIMAL(14,2) NOT NULL,
    Currency     NVARCHAR(3) NOT NULL CONSTRAINT DF_Ledger_Ccy DEFAULT('INR'),
    OccurredUtc  DATETIME2(3) NOT NULL CONSTRAINT DF_Ledger_Occurred DEFAULT(SYSUTCDATETIME()),
    Notes        NVARCHAR(400) NULL
);
GO

/* ===================== security schema (identity + RBAC) ============= */

/* Global user store. TenantId NULL + IsSuperAdmin=1 → platform superadmin
   (D6). Tenant users carry their TenantId. Password is PBKDF2 (hash+salt). */
IF OBJECT_ID('security.AppUser') IS NULL
CREATE TABLE security.AppUser (
    UserId       BIGINT IDENTITY(1,1) CONSTRAINT PK_SecUser PRIMARY KEY,
    TenantId     INT NULL CONSTRAINT FK_SecUser_Tenant REFERENCES platform.Tenant(TenantId),
    UserName     NVARCHAR(120) NOT NULL CONSTRAINT UQ_SecUser_UserName UNIQUE,
    DisplayName  NVARCHAR(160) NOT NULL,
    Email        NVARCHAR(190) NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    PasswordSalt NVARCHAR(512) NOT NULL,
    IsSuperAdmin BIT NOT NULL CONSTRAINT DF_SecUser_Super DEFAULT(0),
    MfaEnabled   BIT NOT NULL CONSTRAINT DF_SecUser_Mfa DEFAULT(0),
    IsActive     BIT NOT NULL CONSTRAINT DF_SecUser_Active DEFAULT(1),
    CreatedUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_SecUser_Created DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('security.Role') IS NULL
CREATE TABLE security.Role (
    RoleId       INT IDENTITY(1,1) CONSTRAINT PK_SecRole PRIMARY KEY,
    Code         NVARCHAR(40)  NOT NULL CONSTRAINT UQ_SecRole_Code UNIQUE,
    Name         NVARCHAR(120) NOT NULL,
    Scope        NVARCHAR(10)  NOT NULL CONSTRAINT DF_SecRole_Scope DEFAULT('tenant'), -- platform/tenant
    IsPrivileged BIT NOT NULL CONSTRAINT DF_SecRole_Priv DEFAULT(0)
);
GO

IF OBJECT_ID('security.Permission') IS NULL
CREATE TABLE security.Permission (
    PermissionId INT IDENTITY(1,1) CONSTRAINT PK_SecPerm PRIMARY KEY,
    Code         NVARCHAR(80)  NOT NULL CONSTRAINT UQ_SecPerm_Code UNIQUE,
    Description  NVARCHAR(200) NOT NULL
);
GO

IF OBJECT_ID('security.RolePermission') IS NULL
CREATE TABLE security.RolePermission (
    RoleId       INT NOT NULL CONSTRAINT FK_SecRP_Role REFERENCES security.Role(RoleId),
    PermissionId INT NOT NULL CONSTRAINT FK_SecRP_Perm REFERENCES security.Permission(PermissionId),
    CONSTRAINT PK_SecRolePermission PRIMARY KEY (RoleId, PermissionId)
);
GO

IF OBJECT_ID('security.UserRole') IS NULL
CREATE TABLE security.UserRole (
    UserId BIGINT NOT NULL CONSTRAINT FK_SecUR_User REFERENCES security.AppUser(UserId),
    RoleId INT    NOT NULL CONSTRAINT FK_SecUR_Role REFERENCES security.Role(RoleId),
    CONSTRAINT PK_SecUserRole PRIMARY KEY (UserId, RoleId)
);
GO

/* Dynamic module / page / action registry (R3: "dynamic module, page &
   assign page module"). Superadmin manages these as DATA, not code. */
IF OBJECT_ID('security.AppModule') IS NULL
CREATE TABLE security.AppModule (
    ModuleId  INT IDENTITY(1,1) CONSTRAINT PK_AppModule PRIMARY KEY,
    Code      NVARCHAR(40)  NOT NULL CONSTRAINT UQ_AppModule_Code UNIQUE,
    Label     NVARCHAR(120) NOT NULL,
    Icon      NVARCHAR(60)  NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_AppModule_Sort DEFAULT(0),
    IsActive  BIT NOT NULL CONSTRAINT DF_AppModule_Active DEFAULT(1)
);
GO

IF OBJECT_ID('security.AppPage') IS NULL
CREATE TABLE security.AppPage (
    PageId    INT IDENTITY(1,1) CONSTRAINT PK_AppPage PRIMARY KEY,
    ModuleId  INT NOT NULL CONSTRAINT FK_AppPage_Module REFERENCES security.AppModule(ModuleId),
    Code      NVARCHAR(60)  NOT NULL CONSTRAINT UQ_AppPage_Code UNIQUE,
    Label     NVARCHAR(120) NOT NULL,
    Route     NVARCHAR(160) NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_AppPage_Sort DEFAULT(0),
    IsActive  BIT NOT NULL CONSTRAINT DF_AppPage_Active DEFAULT(1)
);
GO

IF OBJECT_ID('security.PageAction') IS NULL
CREATE TABLE security.PageAction (
    ActionId INT IDENTITY(1,1) CONSTRAINT PK_PageAction PRIMARY KEY,
    PageId   INT NOT NULL CONSTRAINT FK_PageAction_Page REFERENCES security.AppPage(PageId),
    Code     NVARCHAR(40) NOT NULL,    -- view/create/edit/delete/print/approve…
    Label    NVARCHAR(80) NOT NULL,
    CONSTRAINT UQ_PageAction UNIQUE (PageId, Code)
);
GO

/* Assignment ("assign page-module"): role → module / page / action. */
IF OBJECT_ID('security.RoleModule') IS NULL
CREATE TABLE security.RoleModule (
    RoleId   INT NOT NULL CONSTRAINT FK_RoleModule_Role REFERENCES security.Role(RoleId),
    ModuleId INT NOT NULL CONSTRAINT FK_RoleModule_Module REFERENCES security.AppModule(ModuleId),
    CONSTRAINT PK_RoleModule PRIMARY KEY (RoleId, ModuleId)
);
GO

IF OBJECT_ID('security.RolePage') IS NULL
CREATE TABLE security.RolePage (
    RoleId INT NOT NULL CONSTRAINT FK_RolePage_Role REFERENCES security.Role(RoleId),
    PageId INT NOT NULL CONSTRAINT FK_RolePage_Page REFERENCES security.AppPage(PageId),
    CONSTRAINT PK_RolePage PRIMARY KEY (RoleId, PageId)
);
GO

IF OBJECT_ID('security.RolePageAction') IS NULL
CREATE TABLE security.RolePageAction (
    RoleId   INT NOT NULL CONSTRAINT FK_RPA_Role REFERENCES security.Role(RoleId),
    ActionId INT NOT NULL CONSTRAINT FK_RPA_Action REFERENCES security.PageAction(ActionId),
    CONSTRAINT PK_RolePageAction PRIMARY KEY (RoleId, ActionId)
);
GO

/* Per-tenant, per-fiscal-year module entitlements (R3: modules vary by FY). */
IF OBJECT_ID('platform.TenantModule') IS NULL
CREATE TABLE platform.TenantModule (
    TenantId     INT NOT NULL CONSTRAINT FK_TenantModule_Tenant REFERENCES platform.Tenant(TenantId),
    FiscalYearId INT NOT NULL CONSTRAINT FK_TenantModule_FY REFERENCES platform.FiscalYear(FiscalYearId),
    ModuleId     INT NOT NULL CONSTRAINT FK_TenantModule_Module REFERENCES security.AppModule(ModuleId),
    Enabled      BIT NOT NULL CONSTRAINT DF_TenantModule_Enabled DEFAULT(1),
    CONSTRAINT PK_TenantModule PRIMARY KEY (TenantId, FiscalYearId, ModuleId)
);
GO

/* ===================== audit schema =================================== */
IF OBJECT_ID('audit.PlatformAudit') IS NULL
CREATE TABLE audit.PlatformAudit (
    AuditId       BIGINT IDENTITY(1,1) CONSTRAINT PK_PlatformAudit PRIMARY KEY,
    OccurredUtc   DATETIME2(3) NOT NULL CONSTRAINT DF_PAudit_Occurred DEFAULT(SYSUTCDATETIME()),
    ActorUserId   BIGINT NULL,
    ActorUserName NVARCHAR(160) NULL,
    TenantId      INT NULL,
    Action        NVARCHAR(120) NOT NULL,
    Entity        NVARCHAR(120) NOT NULL,
    EntityId      NVARCHAR(80)  NULL,
    Succeeded     BIT NOT NULL,
    Error         NVARCHAR(MAX) NULL,
    PayloadJson   NVARCHAR(MAX) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PlatformAudit_Occurred')
    CREATE INDEX IX_PlatformAudit_Occurred ON audit.PlatformAudit(OccurredUtc DESC);
GO
