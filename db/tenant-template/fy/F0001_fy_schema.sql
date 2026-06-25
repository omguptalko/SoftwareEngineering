/* =====================================================================
   Tenant per-FISCAL-YEAR DB template (L1.5/L1.8) — billing / insurance / hr
   Per D3: financial/fiscal-scoped transactions only. Schemas billing,
   insurance, hr, seq, proc, audit. Cross-DB references (PatientId/BranchId/
   StaffId/TariffId/PayerId → master/patient DB) are PLAIN columns — no
   cross-database FKs. Intra-DB FKs are kept (schema-qualified). Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required for the BillLine computed column
SET ANSI_NULLS ON;
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

/* ---- billing (§3.14 / §5) ------------------------------------------ */
IF OBJECT_ID('billing.Bill') IS NULL
CREATE TABLE billing.Bill (
    BillId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Bill PRIMARY KEY,
    BillNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_f_Bill_No UNIQUE,
    BranchId INT NULL,            -- master.Branch (cross-DB, by convention)
    PatientId BIGINT NULL,        -- patient.Patient (cross-DB, by convention)
    AdmissionId BIGINT NULL,      -- clinical.Admission (cross-DB, by convention)
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_f_Bill_Created DEFAULT(SYSUTCDATETIME()),
    GrossAmount DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Gross DEFAULT(0),
    DiscountAmount DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Disc DEFAULT(0),
    InsurancePays DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Ins DEFAULT(0),
    PatientPays DECIMAL(14,2) NOT NULL CONSTRAINT DF_f_Bill_Pat DEFAULT(0),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Bill_Status DEFAULT('Open')
);
GO

IF OBJECT_ID('billing.BillLine') IS NULL
CREATE TABLE billing.BillLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_BillLine PRIMARY KEY,
    BillId BIGINT NOT NULL CONSTRAINT FK_f_BillLine_Bill REFERENCES billing.Bill(BillId),
    TariffId INT NULL,            -- master.Tariff (cross-DB, by convention)
    Description NVARCHAR(200) NOT NULL,
    Qty DECIMAL(9,2) NOT NULL CONSTRAINT DF_f_BillLine_Qty DEFAULT(1),
    Rate DECIMAL(12,2) NOT NULL,
    Amount AS (Qty * Rate) PERSISTED
);
GO

IF OBJECT_ID('billing.Payment') IS NULL
CREATE TABLE billing.Payment (
    PaymentId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Payment PRIMARY KEY,
    BillId BIGINT NULL CONSTRAINT FK_f_Pay_Bill REFERENCES billing.Bill(BillId),
    PatientId BIGINT NOT NULL,    -- cross-DB
    Mode NVARCHAR(20) NOT NULL,
    Gateway NVARCHAR(20) NULL,
    Amount DECIMAL(14,2) NOT NULL,
    GatewayRef NVARCHAR(80) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Payment_Status DEFAULT('Initiated'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_f_Payment_Created DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('billing.PatientDeposit') IS NULL
CREATE TABLE billing.PatientDeposit (
    DepositId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_PatientDeposit PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    Amount DECIMAL(14,2) NOT NULL,
    Balance DECIMAL(14,2) NOT NULL,
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_f_Dep_Created DEFAULT(SYSUTCDATETIME())
);
GO

/* ---- insurance / cashless (§3.15 / §7) ----------------------------- */
IF OBJECT_ID('insurance.InsurancePolicy') IS NULL
CREATE TABLE insurance.InsurancePolicy (
    PolicyId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_InsurancePolicy PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    PayerId INT NOT NULL,         -- master.Payer (cross-DB)
    PolicyNo NVARCHAR(60) NULL,
    MemberId NVARCHAR(60) NULL,
    SumInsured DECIMAL(14,2) NULL,
    AvailableBalance DECIMAL(14,2) NULL,
    RoomRentCapPerDay DECIMAL(12,2) NULL,
    CoPayPct DECIMAL(5,2) NULL,
    ValidTo DATE NULL
);
GO

IF OBJECT_ID('insurance.Claim') IS NULL
CREATE TABLE insurance.Claim (
    ClaimId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Claim PRIMARY KEY,
    ClaimNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_f_Claim_No UNIQUE,
    BranchId INT NOT NULL,        -- cross-DB
    PatientId BIGINT NOT NULL,    -- cross-DB
    PayerId INT NOT NULL,         -- cross-DB
    PolicyId BIGINT NULL CONSTRAINT FK_f_Claim_Policy REFERENCES insurance.InsurancePolicy(PolicyId),
    AdmissionId BIGINT NULL,      -- cross-DB
    Channel NVARCHAR(20) NULL,
    ProvisionalIcd10 NVARCHAR(10) NULL,
    PreAuthAmount DECIMAL(14,2) NULL,
    ApprovedAmount DECIMAL(14,2) NULL,
    SettledAmount DECIMAL(14,2) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Claim_Status DEFAULT('Eligibility'),
    SubmittedUtc DATETIME2(3) NULL,
    TatDueUtc DATETIME2(3) NULL
);
GO

IF OBJECT_ID('insurance.ClaimEvent') IS NULL
CREATE TABLE insurance.ClaimEvent (
    EventId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_ClaimEvent PRIMARY KEY,
    ClaimId BIGINT NOT NULL CONSTRAINT FK_f_ClaimEvent_Claim REFERENCES insurance.Claim(ClaimId),
    EventType NVARCHAR(30) NOT NULL,
    Amount DECIMAL(14,2) NULL,
    Notes NVARCHAR(MAX) NULL,
    OccurredUtc DATETIME2(3) NOT NULL CONSTRAINT DF_f_ClaimEvent_Utc DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('insurance.ClaimDocument') IS NULL
CREATE TABLE insurance.ClaimDocument (
    DocId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_ClaimDocument PRIMARY KEY,
    ClaimId BIGINT NOT NULL CONSTRAINT FK_f_ClaimDoc_Claim REFERENCES insurance.Claim(ClaimId),
    DocType NVARCHAR(60) NOT NULL,
    DocUrl NVARCHAR(400) NULL,
    IsMandatory BIT NOT NULL CONSTRAINT DF_f_ClaimDoc_Mand DEFAULT(0),
    Attached BIT NOT NULL CONSTRAINT DF_f_ClaimDoc_Att DEFAULT(0)
);
GO

IF OBJECT_ID('insurance.SettlementReconciliation') IS NULL
CREATE TABLE insurance.SettlementReconciliation (
    ReconId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_SettlementRecon PRIMARY KEY,
    ClaimId BIGINT NULL CONSTRAINT FK_f_Recon_Claim REFERENCES insurance.Claim(ClaimId),
    Utr NVARCHAR(40) NULL,
    BankAmount DECIMAL(14,2) NULL,
    ReconciledUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Recon_Status DEFAULT('Unmatched')
);
GO

/* ---- hr / payroll (§3.17 / §3.18) — Staff master lives in master DB - */
IF OBJECT_ID('hr.Attendance') IS NULL
CREATE TABLE hr.Attendance (
    AttendanceId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_Attendance PRIMARY KEY,
    StaffId BIGINT NOT NULL,      -- master.Staff (cross-DB)
    WorkDate DATE NOT NULL,
    InTime TIME NULL, OutTime TIME NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Att_Status DEFAULT('Present'),
    CONSTRAINT UQ_f_Attendance UNIQUE (StaffId, WorkDate)
);
GO

IF OBJECT_ID('hr.DutyRoster') IS NULL
CREATE TABLE hr.DutyRoster (
    RosterId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_DutyRoster PRIMARY KEY,
    StaffId BIGINT NOT NULL,      -- cross-DB
    ShiftDate DATE NOT NULL,
    Shift NVARCHAR(20) NULL
);
GO

IF OBJECT_ID('hr.LeaveRequest') IS NULL
CREATE TABLE hr.LeaveRequest (
    LeaveId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_LeaveRequest PRIMARY KEY,
    StaffId BIGINT NOT NULL,      -- cross-DB
    FromDate DATE NOT NULL, ToDate DATE NOT NULL,
    LeaveType NVARCHAR(30) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Leave_Status DEFAULT('Pending')
);
GO

IF OBJECT_ID('hr.PayrollRun') IS NULL
CREATE TABLE hr.PayrollRun (
    PayrollId BIGINT IDENTITY(1,1) CONSTRAINT PK_f_PayrollRun PRIMARY KEY,
    StaffId BIGINT NOT NULL,      -- cross-DB
    PeriodYear INT NOT NULL, PeriodMonth INT NOT NULL,
    BasicPay DECIMAL(12,2) NOT NULL CONSTRAINT DF_f_Pay_Basic DEFAULT(0),
    OvertimeHours DECIMAL(7,2) NOT NULL CONSTRAINT DF_f_Pay_OtHrs DEFAULT(0),
    OvertimeAmount DECIMAL(12,2) NOT NULL CONSTRAINT DF_f_Pay_OtAmt DEFAULT(0),
    GrossPay DECIMAL(12,2) NOT NULL CONSTRAINT DF_f_Pay_Gross DEFAULT(0),
    NetPay DECIMAL(12,2) NOT NULL CONSTRAINT DF_f_Pay_Net DEFAULT(0),
    OvertimeApprovedBy BIGINT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_f_Pay_Status DEFAULT('Draft'),
    CONSTRAINT UQ_f_PayrollRun UNIQUE (StaffId, PeriodYear, PeriodMonth)
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
    SELECT CONCAT(@Prefix, '-', @FyCode, '-', RIGHT('000000' + CAST(@Seq AS VARCHAR(6)), 6));
END
GO

/* ---- audit --------------------------------------------------------- */
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
    PayloadJson   NVARCHAR(MAX) NULL,
    Succeeded     BIT NOT NULL,
    Error         NVARCHAR(MAX) NULL
);
GO
