/* =====================================================================
   Migration 0001 — Core Platform (Phase 0)
   SRS: §2.2 roles, §3.21 multi-branch, §3.22/§8.1 audit, §9 architecture.
   Idempotent: safe to re-run. All reference data is seeded separately (0100).
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* ---- Branch (multi-branch context source, SRS §3.21) ---------------- */
IF OBJECT_ID('dbo.Branch') IS NULL
CREATE TABLE dbo.Branch (
    BranchId   INT IDENTITY(1,1) CONSTRAINT PK_Branch PRIMARY KEY,
    Code       NVARCHAR(10)  NOT NULL CONSTRAINT UQ_Branch_Code UNIQUE,
    Name       NVARCHAR(120) NOT NULL,
    City       NVARCHAR(80)  NULL,
    State      NVARCHAR(80)  NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Branch_IsActive DEFAULT(1)
);
GO

/* ---- Module registry (drives wireframe sidebar; was static data.js) -- */
IF OBJECT_ID('dbo.ModuleGroup') IS NULL
CREATE TABLE dbo.ModuleGroup (
    GroupId    NVARCHAR(40)  NOT NULL CONSTRAINT PK_ModuleGroup PRIMARY KEY,
    Label      NVARCHAR(120) NOT NULL,
    Icon       NVARCHAR(60)  NOT NULL,
    SortOrder  INT NOT NULL CONSTRAINT DF_ModuleGroup_Sort DEFAULT(0)
);
GO

IF OBJECT_ID('dbo.Module') IS NULL
CREATE TABLE dbo.Module (
    ModuleId   NVARCHAR(40)  NOT NULL CONSTRAINT PK_Module PRIMARY KEY,
    GroupId    NVARCHAR(40)  NOT NULL CONSTRAINT FK_Module_Group REFERENCES dbo.ModuleGroup(GroupId),
    Icon       NVARCHAR(60)  NOT NULL,
    Label      NVARCHAR(120) NOT NULL,
    Built      BIT NOT NULL CONSTRAINT DF_Module_Built DEFAULT(0),
    Badge      NVARCHAR(20)  NULL,
    SortOrder  INT NOT NULL CONSTRAINT DF_Module_Sort DEFAULT(0),
    SrsRef     NVARCHAR(20)  NULL
);
GO

/* ---- RBAC (SRS §2.2 / §8.1) — roles & permissions are data, not code -- */
IF OBJECT_ID('dbo.Role') IS NULL
CREATE TABLE dbo.Role (
    RoleId       INT IDENTITY(1,1) CONSTRAINT PK_Role PRIMARY KEY,
    Code         NVARCHAR(40)  NOT NULL CONSTRAINT UQ_Role_Code UNIQUE,
    Name         NVARCHAR(120) NOT NULL,
    IsPrivileged BIT NOT NULL CONSTRAINT DF_Role_Priv DEFAULT(0)
);
GO

IF OBJECT_ID('dbo.Permission') IS NULL
CREATE TABLE dbo.Permission (
    PermissionId INT IDENTITY(1,1) CONSTRAINT PK_Permission PRIMARY KEY,
    Code         NVARCHAR(80)  NOT NULL CONSTRAINT UQ_Permission_Code UNIQUE,
    Description  NVARCHAR(200) NOT NULL
);
GO

IF OBJECT_ID('dbo.RolePermission') IS NULL
CREATE TABLE dbo.RolePermission (
    RoleId       INT NOT NULL CONSTRAINT FK_RP_Role REFERENCES dbo.Role(RoleId),
    PermissionId INT NOT NULL CONSTRAINT FK_RP_Perm REFERENCES dbo.Permission(PermissionId),
    CONSTRAINT PK_RolePermission PRIMARY KEY (RoleId, PermissionId)
);
GO

IF OBJECT_ID('dbo.AppUser') IS NULL
CREATE TABLE dbo.AppUser (
    UserId       BIGINT IDENTITY(1,1) CONSTRAINT PK_AppUser PRIMARY KEY,
    BranchId     INT NOT NULL CONSTRAINT FK_AppUser_Branch REFERENCES dbo.Branch(BranchId),
    UserName     NVARCHAR(80)  NOT NULL CONSTRAINT UQ_AppUser_UserName UNIQUE,
    DisplayName  NVARCHAR(120) NOT NULL,
    PasswordHash NVARCHAR(256) NOT NULL,
    PasswordSalt NVARCHAR(256) NOT NULL,
    MfaEnabled   BIT NOT NULL CONSTRAINT DF_AppUser_Mfa DEFAULT(0),
    IsActive     BIT NOT NULL CONSTRAINT DF_AppUser_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.UserRole') IS NULL
CREATE TABLE dbo.UserRole (
    UserId BIGINT NOT NULL CONSTRAINT FK_UR_User REFERENCES dbo.AppUser(UserId),
    RoleId INT    NOT NULL CONSTRAINT FK_UR_Role REFERENCES dbo.Role(RoleId),
    CONSTRAINT PK_UserRole PRIMARY KEY (UserId, RoleId)
);
GO

/* ---- Immutable audit trail (SRS §8.1 / §3.22) — insert-only ---------- */
IF OBJECT_ID('dbo.AuditEntry') IS NULL
CREATE TABLE dbo.AuditEntry (
    AuditId       BIGINT IDENTITY(1,1) CONSTRAINT PK_AuditEntry PRIMARY KEY,
    OccurredAtUtc DATETIME2(3) NOT NULL,
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
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditEntry_Occurred')
    CREATE INDEX IX_AuditEntry_Occurred ON dbo.AuditEntry(OccurredAtUtc DESC);
GO

/* ---- UHID sequence + generator (SRS §3.1) — per branch+year --------- */
IF OBJECT_ID('dbo.UhidCounter') IS NULL
CREATE TABLE dbo.UhidCounter (
    BranchId INT NOT NULL,
    [Year]   INT NOT NULL,
    LastSeq  INT NOT NULL CONSTRAINT DF_UhidCounter_Seq DEFAULT(0),
    CONSTRAINT PK_UhidCounter PRIMARY KEY (BranchId, [Year])
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_NextUhid @BranchId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = YEAR(SYSUTCDATETIME());
    DECLARE @Seq INT, @Code NVARCHAR(10);

    BEGIN TRAN;
        UPDATE dbo.UhidCounter WITH (UPDLOCK, SERIALIZABLE)
            SET @Seq = LastSeq = LastSeq + 1
        WHERE BranchId = @BranchId AND [Year] = @Year;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @Seq = 1;
            INSERT dbo.UhidCounter (BranchId, [Year], LastSeq) VALUES (@BranchId, @Year, 1);
        END
    COMMIT;

    SELECT @Code = Code FROM dbo.Branch WHERE BranchId = @BranchId;
    SELECT CONCAT(ISNULL(@Code, CONCAT('BR', @BranchId)), '-', @Year, '-', RIGHT('000000' + CAST(@Seq AS VARCHAR(6)), 6));
END
GO
