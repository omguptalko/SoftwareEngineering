/* =====================================================================
   Tenant per-FISCAL-YEAR DB — diagnostics / pharmacy / inventory (L1.8)
   Fiscal-scoped operational transactions. Cross-DB refs (Patient/Branch/
   Doctor/Drug/Encounter/Prescription/Supplier → master/patient DB) are PLAIN
   columns. Intra-DB FKs kept. Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('diagnostics') IS NULL EXEC('CREATE SCHEMA diagnostics');
GO
IF SCHEMA_ID('pharmacy')    IS NULL EXEC('CREATE SCHEMA pharmacy');
GO
IF SCHEMA_ID('inventory')   IS NULL EXEC('CREATE SCHEMA inventory');
GO

/* ---- diagnostics (§3.8 / §3.9 / §3.7) ------------------------------ */
IF OBJECT_ID('diagnostics.LabOrder') IS NULL
CREATE TABLE diagnostics.LabOrder (
    LabOrderId BIGINT IDENTITY(1,1) CONSTRAINT PK_d_LabOrder PRIMARY KEY,
    Barcode NVARCHAR(30) NOT NULL CONSTRAINT UQ_d_LabOrder_Barcode UNIQUE,
    EncounterId BIGINT NULL,      -- clinical.Encounter (cross-DB)
    PatientId BIGINT NOT NULL,    -- patient.Patient (cross-DB)
    TestName NVARCHAR(120) NOT NULL,
    CollectedUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_d_Lab_Status DEFAULT('Awaited')
);
GO

IF OBJECT_ID('diagnostics.LabResult') IS NULL
CREATE TABLE diagnostics.LabResult (
    ResultId BIGINT IDENTITY(1,1) CONSTRAINT PK_d_LabResult PRIMARY KEY,
    LabOrderId BIGINT NOT NULL CONSTRAINT FK_d_LabRes_Order REFERENCES diagnostics.LabOrder(LabOrderId),
    Parameter NVARCHAR(80) NOT NULL,
    ResultValue NVARCHAR(40) NULL,
    Unit NVARCHAR(20) NULL,
    ReferenceRange NVARCHAR(40) NULL,
    Flag NVARCHAR(10) NULL,
    ValidatedUtc DATETIME2(3) NULL
);
GO

IF OBJECT_ID('diagnostics.RadiologyOrder') IS NULL
CREATE TABLE diagnostics.RadiologyOrder (
    RadOrderId BIGINT IDENTITY(1,1) CONSTRAINT PK_d_RadOrder PRIMARY KEY,
    PatientId BIGINT NOT NULL,    -- cross-DB
    Modality NVARCHAR(20) NOT NULL,
    StudyName NVARCHAR(120) NULL,
    ScheduledUtc DATETIME2(0) NULL,
    ReportUrl NVARCHAR(400) NULL,
    ReportedByDoctorId INT NULL,  -- master.Doctor (cross-DB)
    IsPcPndtRegulated BIT NOT NULL CONSTRAINT DF_d_Rad_Pcpndt DEFAULT(0),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_d_Rad_Status DEFAULT('Scheduled')
);
GO

IF OBJECT_ID('diagnostics.BloodStock') IS NULL
CREATE TABLE diagnostics.BloodStock (
    BloodStockId INT IDENTITY(1,1) CONSTRAINT PK_d_BloodStock PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    BloodGroup NVARCHAR(5) NOT NULL,  -- master.BloodGroup (cross-DB)
    Units INT NOT NULL CONSTRAINT DF_d_BS_Units DEFAULT(0),
    SafetyThreshold INT NOT NULL CONSTRAINT DF_d_BS_Threshold DEFAULT(0)
);
GO

IF OBJECT_ID('diagnostics.BloodRequest') IS NULL
CREATE TABLE diagnostics.BloodRequest (
    RequestId BIGINT IDENTITY(1,1) CONSTRAINT PK_d_BloodRequest PRIMARY KEY,
    BranchId INT NOT NULL,        -- cross-DB
    PatientId BIGINT NULL,        -- cross-DB
    BloodGroup NVARCHAR(5) NOT NULL,
    Units INT NOT NULL,
    IsEmergency BIT NOT NULL CONSTRAINT DF_d_BR_Emergency DEFAULT(0),
    RequestedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_d_BR_Requested DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_d_BR_Status DEFAULT('Requested')
);
GO

/* ---- pharmacy (§3.10) ---------------------------------------------- */
IF OBJECT_ID('pharmacy.DrugBatch') IS NULL
CREATE TABLE pharmacy.DrugBatch (
    BatchId BIGINT IDENTITY(1,1) CONSTRAINT PK_ph_DrugBatch PRIMARY KEY,
    DrugId INT NOT NULL,          -- master.Drug (cross-DB)
    BatchNo NVARCHAR(40) NOT NULL,
    ExpiryDate DATE NOT NULL,
    Mrp DECIMAL(10,2) NOT NULL,
    QtyOnHand INT NOT NULL CONSTRAINT DF_ph_Batch_Qty DEFAULT(0)
);
GO

IF OBJECT_ID('pharmacy.Dispense') IS NULL
CREATE TABLE pharmacy.Dispense (
    DispenseId BIGINT IDENTITY(1,1) CONSTRAINT PK_ph_Dispense PRIMARY KEY,
    PrescriptionId BIGINT NULL,   -- clinical.Prescription (cross-DB)
    BranchId INT NOT NULL,        -- cross-DB
    DispensedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_ph_Disp_Utc DEFAULT(SYSUTCDATETIME()),
    IsNdps BIT NOT NULL CONSTRAINT DF_ph_Disp_Ndps DEFAULT(0)
);
GO

IF OBJECT_ID('pharmacy.DispenseLine') IS NULL
CREATE TABLE pharmacy.DispenseLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_ph_DispLine PRIMARY KEY,
    DispenseId BIGINT NOT NULL CONSTRAINT FK_ph_DispLine_Disp REFERENCES pharmacy.Dispense(DispenseId),
    BatchId BIGINT NOT NULL CONSTRAINT FK_ph_DispLine_Batch REFERENCES pharmacy.DrugBatch(BatchId),
    Qty INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL
);
GO

/* ---- inventory (§3.11) — Supplier master lives in master DB -------- */
IF OBJECT_ID('inventory.PurchaseOrder') IS NULL
CREATE TABLE inventory.PurchaseOrder (
    PoId BIGINT IDENTITY(1,1) CONSTRAINT PK_i_PurchaseOrder PRIMARY KEY,
    PoNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_i_PO_No UNIQUE,
    BranchId INT NOT NULL,        -- cross-DB
    SupplierId INT NOT NULL,      -- master.Supplier (cross-DB)
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_i_PO_Created DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_i_PO_Status DEFAULT('Draft')
);
GO

IF OBJECT_ID('inventory.PurchaseOrderLine') IS NULL
CREATE TABLE inventory.PurchaseOrderLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_i_POLine PRIMARY KEY,
    PoId BIGINT NOT NULL CONSTRAINT FK_i_POLine_PO REFERENCES inventory.PurchaseOrder(PoId),
    DrugId INT NULL,              -- cross-DB
    ItemName NVARCHAR(160) NULL,
    Qty INT NOT NULL,
    UnitPrice DECIMAL(10,2) NULL
);
GO
