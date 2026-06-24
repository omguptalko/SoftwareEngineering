/* =====================================================================
   Migration 0006 — Billing & Revenue Cycle, Payments (Phase 6)
   SRS: §3.14 Billing/RCM, §5 Payment Gateway.
   Tariffs, taxes, discounts are MASTER data — never hardcoded.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required for the BillLine computed column
SET ANSI_NULLS ON;
GO

/* Tariff / service price master (SRS §3.14) -------------------------- */
IF OBJECT_ID('dbo.Tariff') IS NULL
CREATE TABLE dbo.Tariff (
    TariffId INT IDENTITY(1,1) CONSTRAINT PK_Tariff PRIMARY KEY,
    BranchId INT NULL CONSTRAINT FK_Tariff_Branch REFERENCES dbo.Branch(BranchId),  -- NULL = all branches
    ServiceCode NVARCHAR(40) NOT NULL,
    ServiceName NVARCHAR(160) NOT NULL,
    Category NVARCHAR(40) NULL,      -- OPD/IPD/Lab/Pharmacy/Radiology/OT
    Rate DECIMAL(12,2) NOT NULL,
    GstRatePct DECIMAL(5,2) NOT NULL CONSTRAINT DF_Tariff_Gst DEFAULT(0),
    IsActive BIT NOT NULL CONSTRAINT DF_Tariff_Active DEFAULT(1),
    CONSTRAINT UQ_Tariff UNIQUE (BranchId, ServiceCode)
);
GO

IF OBJECT_ID('dbo.Bill') IS NULL
CREATE TABLE dbo.Bill (
    BillId BIGINT IDENTITY(1,1) CONSTRAINT PK_Bill PRIMARY KEY,
    BillNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_Bill_No UNIQUE,
    BranchId INT NOT NULL CONSTRAINT FK_Bill_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Bill_Patient REFERENCES dbo.Patient(PatientId),
    AdmissionId BIGINT NULL CONSTRAINT FK_Bill_Adm REFERENCES dbo.Admission(AdmissionId),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Bill_Created DEFAULT(SYSUTCDATETIME()),
    GrossAmount DECIMAL(14,2) NOT NULL CONSTRAINT DF_Bill_Gross DEFAULT(0),
    DiscountAmount DECIMAL(14,2) NOT NULL CONSTRAINT DF_Bill_Disc DEFAULT(0),
    InsurancePays DECIMAL(14,2) NOT NULL CONSTRAINT DF_Bill_Ins DEFAULT(0),
    PatientPays DECIMAL(14,2) NOT NULL CONSTRAINT DF_Bill_Pat DEFAULT(0),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Bill_Status DEFAULT('Open')
);
GO

IF OBJECT_ID('dbo.BillLine') IS NULL
CREATE TABLE dbo.BillLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_BillLine PRIMARY KEY,
    BillId BIGINT NOT NULL CONSTRAINT FK_BillLine_Bill REFERENCES dbo.Bill(BillId),
    TariffId INT NULL CONSTRAINT FK_BillLine_Tariff REFERENCES dbo.Tariff(TariffId),
    Description NVARCHAR(200) NOT NULL,
    Qty DECIMAL(9,2) NOT NULL CONSTRAINT DF_BillLine_Qty DEFAULT(1),
    Rate DECIMAL(12,2) NOT NULL,
    Amount AS (Qty * Rate) PERSISTED
);
GO

/* §5 Payments (gateway keys live in Key Vault, not here) ------------- */
IF OBJECT_ID('dbo.Payment') IS NULL
CREATE TABLE dbo.Payment (
    PaymentId BIGINT IDENTITY(1,1) CONSTRAINT PK_Payment PRIMARY KEY,
    BillId BIGINT NULL CONSTRAINT FK_Pay_Bill REFERENCES dbo.Bill(BillId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Pay_Patient REFERENCES dbo.Patient(PatientId),
    Mode NVARCHAR(20) NOT NULL,      -- UPI/Card/NetBanking/QR/Cash
    Gateway NVARCHAR(20) NULL,       -- Razorpay/Stripe/PayU/Cashfree
    Amount DECIMAL(14,2) NOT NULL,
    GatewayRef NVARCHAR(80) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Payment_Status DEFAULT('Initiated'),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Payment_Created DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('dbo.PatientDeposit') IS NULL
CREATE TABLE dbo.PatientDeposit (
    DepositId BIGINT IDENTITY(1,1) CONSTRAINT PK_PatientDeposit PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Dep_Patient REFERENCES dbo.Patient(PatientId),
    Amount DECIMAL(14,2) NOT NULL,
    Balance DECIMAL(14,2) NOT NULL,
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Dep_Created DEFAULT(SYSUTCDATETIME())
);
GO
