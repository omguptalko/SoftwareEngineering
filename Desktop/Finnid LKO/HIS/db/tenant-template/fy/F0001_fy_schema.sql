/* =====================================================================
   Tenant per-FISCAL-YEAR DB template (L1.5, applied to {Tenant}_FY{code})
   Per Decision D3: only financial/fiscal-scoped transactions live here.
   Proper schemas (R1/R2): billing / insurance / hr / seq / proc / audit.
   Cross-DB references (e.g. PatientId in master) are by convention — no
   cross-database FKs (inherent to the multi-DB design).
   Representative table set; full refactor is L1.1.2-4. Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('billing')   IS NULL EXEC('CREATE SCHEMA billing');
GO
IF SCHEMA_ID('insurance') IS NULL EXEC('CREATE SCHEMA insurance');
GO
IF SCHEMA_ID('hr')        IS NULL EXEC('CREATE SCHEMA hr');
GO
IF SCHEMA_ID('seq')       IS NULL EXEC('CREATE SCHEMA seq');
GO
IF SCHEMA_ID('proc')      IS NULL EXEC('CREATE SCHEMA [proc]');
GO
IF SCHEMA_ID('audit')     IS NULL EXEC('CREATE SCHEMA audit');
GO

/* ---- billing schema (fiscal-scoped) -------------------------------- */
IF OBJECT_ID('billing.Bill') IS NULL
CREATE TABLE billing.Bill (
    BillId     BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Bill PRIMARY KEY,
    BillNo     NVARCHAR(30) NOT NULL CONSTRAINT UQ_f_Bill_No UNIQUE,
    BranchId   INT NULL,           -- references master.Branch by convention (cross-DB)
    PatientId  BIGINT NULL,        -- references patient.Patient by convention (cross-DB)
    Gross      DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Gross DEFAULT(0),
    Discount   DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Disc DEFAULT(0),
    PatientPays DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Pays DEFAULT(0),
    Status     NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Bill_Status DEFAULT('Open'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_f_Bill_Created DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('billing.BillLine') IS NULL
CREATE TABLE billing.BillLine (
    LineId      BIGINT IDENTITY(1,1) CONSTRAINT PK_f_BillLine PRIMARY KEY,
    BillId      BIGINT NOT NULL CONSTRAINT FK_f_BillLine_Bill REFERENCES billing.Bill(BillId),
    Description NVARCHAR(200) NOT NULL,
    Qty         DECIMAL(9,2) NOT NULL,
    Rate        DECIMAL(12,2) NOT NULL,
    Amount      DECIMAL(14,2) NOT NULL
);
GO

IF OBJECT_ID('billing.Payment') IS NULL
CREATE TABLE billing.Payment (
    PaymentId  BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Payment PRIMARY KEY,
    BillId     BIGINT NOT NULL CONSTRAINT FK_f_Payment_Bill REFERENCES billing.Bill(BillId),
    Amount     DECIMAL(14,2) NOT NULL,
    Provider   NVARCHAR(40) NULL,
    PaidUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_f_Payment_Paid DEFAULT(SYSUTCDATETIME())
);
GO

/* ---- insurance schema (fiscal-scoped) ------------------------------ */
IF OBJECT_ID('insurance.Claim') IS NULL
CREATE TABLE insurance.Claim (
    ClaimId   BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Claim PRIMARY KEY,
    ClaimNo   NVARCHAR(30) NOT NULL CONSTRAINT UQ_f_Claim_No UNIQUE,
    PatientId BIGINT NULL,
    PayerCode NVARCHAR(20) NULL,
    PreAuth   DECIMAL(14,2) NULL,
    Approved  DECIMAL(14,2) NULL,
    Status    NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Claim_Status DEFAULT('Initiated')
);
GO

/* ---- hr schema (fiscal-scoped payroll) ----------------------------- */
IF OBJECT_ID('hr.PayrollRun') IS NULL
CREATE TABLE hr.PayrollRun (
    PayrollId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Payroll PRIMARY KEY,
    StaffId   BIGINT NULL,
    PeriodYear INT NOT NULL,
    PeriodMonth INT NOT NULL,
    Gross     DECIMAL(14,2) NOT NULL,
    Net       DECIMAL(14,2) NOT NULL,
    Status    NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Payroll_Status DEFAULT('Draft')
);
GO

/* ---- seq + proc: per-fiscal-year document numbering ---------------- */
IF OBJECT_ID('seq.DocCounter') IS NULL
CREATE TABLE seq.DocCounter (
    BranchId INT NOT NULL,
    DocType  NVARCHAR(20) NOT NULL,
    LastSeq  INT NOT NULL CONSTRAINT DF_f_DocCounter_Seq DEFAULT(0),
    CONSTRAINT PK_f_DocCounter PRIMARY KEY (BranchId, DocType)
);
GO

CREATE OR ALTER PROCEDURE [proc].usp_NextDocNo
    @BranchId INT, @DocType NVARCHAR(20), @Prefix NVARCHAR(10), @FyCode NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Seq INT;
    BEGIN TRAN;
        UPDATE seq.DocCounter WITH (UPDLOCK, SERIALIZABLE)
            SET @Seq = LastSeq = LastSeq + 1
        WHERE BranchId = @BranchId AND DocType = @DocType;
        IF @@ROWCOUNT = 0
        BEGIN
            SET @Seq = 1;
            INSERT seq.DocCounter (BranchId, DocType, LastSeq) VALUES (@BranchId, @DocType, 1);
        END
    COMMIT;
    -- Numbering is scoped to the fiscal year because each FY has its own DB.
    SELECT CONCAT(@Prefix, '-', @FyCode, '-', RIGHT('000000' + CAST(@Seq AS VARCHAR(6)), 6));
END
GO

/* ---- audit schema -------------------------------------------------- */
IF OBJECT_ID('audit.AuditEntry') IS NULL
CREATE TABLE audit.AuditEntry (
    AuditId       BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Audit PRIMARY KEY,
    OccurredAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_f_Audit_When DEFAULT(SYSUTCDATETIME()),
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
