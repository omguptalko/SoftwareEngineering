/* =====================================================================
   Migration 0005 — Diagnostics, Pharmacy, Inventory, Assets (Phases 3-4)
   SRS: §3.8 LIS, §3.9 Radiology, §3.7 Blood Bank, §3.10 Pharmacy,
        §3.11 Inventory, §3.19 Assets.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* §3.8 Laboratory ---------------------------------------------------- */
IF OBJECT_ID('dbo.LabOrder') IS NULL
CREATE TABLE dbo.LabOrder (
    LabOrderId BIGINT IDENTITY(1,1) CONSTRAINT PK_LabOrder PRIMARY KEY,
    Barcode NVARCHAR(30) NOT NULL CONSTRAINT UQ_LabOrder_Barcode UNIQUE,
    EncounterId BIGINT NULL CONSTRAINT FK_Lab_Enc REFERENCES dbo.Encounter(EncounterId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Lab_Patient REFERENCES dbo.Patient(PatientId),
    TestName NVARCHAR(120) NOT NULL,
    CollectedUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Lab_Status DEFAULT('Awaited')  -- Awaited/Received/ResultEntry/Released
);
GO

IF OBJECT_ID('dbo.LabResult') IS NULL
CREATE TABLE dbo.LabResult (
    ResultId BIGINT IDENTITY(1,1) CONSTRAINT PK_LabResult PRIMARY KEY,
    LabOrderId BIGINT NOT NULL CONSTRAINT FK_LabRes_Order REFERENCES dbo.LabOrder(LabOrderId),
    Parameter NVARCHAR(80) NOT NULL,
    ResultValue NVARCHAR(40) NULL,
    Unit NVARCHAR(20) NULL,
    ReferenceRange NVARCHAR(40) NULL,
    Flag NVARCHAR(10) NULL,   -- Low/High/Normal
    ValidatedUtc DATETIME2(3) NULL
);
GO

/* §3.9 Radiology (PC-PNDT controlled access tracked) ----------------- */
IF OBJECT_ID('dbo.RadiologyOrder') IS NULL
CREATE TABLE dbo.RadiologyOrder (
    RadOrderId BIGINT IDENTITY(1,1) CONSTRAINT PK_RadOrder PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Rad_Patient REFERENCES dbo.Patient(PatientId),
    Modality NVARCHAR(20) NOT NULL,   -- X-Ray/MRI/CT/USG/ECG
    StudyName NVARCHAR(120) NULL,
    ScheduledUtc DATETIME2(0) NULL,
    ReportUrl NVARCHAR(400) NULL,
    ReportedByDoctorId INT NULL CONSTRAINT FK_Rad_Doctor REFERENCES dbo.Doctor(DoctorId),
    IsPcPndtRegulated BIT NOT NULL CONSTRAINT DF_Rad_Pcpndt DEFAULT(0),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Rad_Status DEFAULT('Scheduled')
);
GO

/* §3.7 Blood Bank ---------------------------------------------------- */
IF OBJECT_ID('dbo.BloodStock') IS NULL
CREATE TABLE dbo.BloodStock (
    BloodStockId INT IDENTITY(1,1) CONSTRAINT PK_BloodStock PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_BS_Branch REFERENCES dbo.Branch(BranchId),
    BloodGroup NVARCHAR(5) NOT NULL CONSTRAINT FK_BS_Group REFERENCES dbo.BloodGroup(Code),
    Units INT NOT NULL CONSTRAINT DF_BS_Units DEFAULT(0),
    SafetyThreshold INT NOT NULL CONSTRAINT DF_BS_Threshold DEFAULT(0)
);
GO

IF OBJECT_ID('dbo.BloodRequest') IS NULL
CREATE TABLE dbo.BloodRequest (
    RequestId BIGINT IDENTITY(1,1) CONSTRAINT PK_BloodRequest PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_BR_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NULL CONSTRAINT FK_BR_Patient REFERENCES dbo.Patient(PatientId),
    BloodGroup NVARCHAR(5) NOT NULL CONSTRAINT FK_BR_Group REFERENCES dbo.BloodGroup(Code),
    Units INT NOT NULL,
    IsEmergency BIT NOT NULL CONSTRAINT DF_BR_Emergency DEFAULT(0),
    RequestedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_BR_Requested DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_BR_Status DEFAULT('Requested')
);
GO

/* §3.10 Pharmacy dispensing (batch/expiry) --------------------------- */
IF OBJECT_ID('dbo.DrugBatch') IS NULL
CREATE TABLE dbo.DrugBatch (
    BatchId BIGINT IDENTITY(1,1) CONSTRAINT PK_DrugBatch PRIMARY KEY,
    DrugId INT NOT NULL CONSTRAINT FK_Batch_Drug REFERENCES dbo.Drug(DrugId),
    BatchNo NVARCHAR(40) NOT NULL,
    ExpiryDate DATE NOT NULL,
    Mrp DECIMAL(10,2) NOT NULL,
    QtyOnHand INT NOT NULL CONSTRAINT DF_Batch_Qty DEFAULT(0)
);
GO

IF OBJECT_ID('dbo.Dispense') IS NULL
CREATE TABLE dbo.Dispense (
    DispenseId BIGINT IDENTITY(1,1) CONSTRAINT PK_Dispense PRIMARY KEY,
    PrescriptionId BIGINT NULL CONSTRAINT FK_Disp_Rx REFERENCES dbo.Prescription(PrescriptionId),
    BranchId INT NOT NULL CONSTRAINT FK_Disp_Branch REFERENCES dbo.Branch(BranchId),
    DispensedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Disp_Utc DEFAULT(SYSUTCDATETIME()),
    IsNdps BIT NOT NULL CONSTRAINT DF_Disp_Ndps DEFAULT(0)   -- NDPS register flag (SRS §10)
);
GO

IF OBJECT_ID('dbo.DispenseLine') IS NULL
CREATE TABLE dbo.DispenseLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_DispLine PRIMARY KEY,
    DispenseId BIGINT NOT NULL CONSTRAINT FK_DispLine_Disp REFERENCES dbo.Dispense(DispenseId),
    BatchId BIGINT NOT NULL CONSTRAINT FK_DispLine_Batch REFERENCES dbo.DrugBatch(BatchId),
    Qty INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL
);
GO

/* §3.11 Inventory & purchase ----------------------------------------- */
IF OBJECT_ID('dbo.Supplier') IS NULL
CREATE TABLE dbo.Supplier (
    SupplierId INT IDENTITY(1,1) CONSTRAINT PK_Supplier PRIMARY KEY,
    Name NVARCHAR(160) NOT NULL,
    Gstin NVARCHAR(20) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Supplier_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.PurchaseOrder') IS NULL
CREATE TABLE dbo.PurchaseOrder (
    PoId BIGINT IDENTITY(1,1) CONSTRAINT PK_PurchaseOrder PRIMARY KEY,
    PoNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_PO_No UNIQUE,
    BranchId INT NOT NULL CONSTRAINT FK_PO_Branch REFERENCES dbo.Branch(BranchId),
    SupplierId INT NOT NULL CONSTRAINT FK_PO_Supplier REFERENCES dbo.Supplier(SupplierId),
    CreatedUtc DATETIME2(3) NOT NULL CONSTRAINT DF_PO_Created DEFAULT(SYSUTCDATETIME()),
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_PO_Status DEFAULT('Draft')
);
GO

IF OBJECT_ID('dbo.PurchaseOrderLine') IS NULL
CREATE TABLE dbo.PurchaseOrderLine (
    LineId BIGINT IDENTITY(1,1) CONSTRAINT PK_POLine PRIMARY KEY,
    PoId BIGINT NOT NULL CONSTRAINT FK_POLine_PO REFERENCES dbo.PurchaseOrder(PoId),
    DrugId INT NULL CONSTRAINT FK_POLine_Drug REFERENCES dbo.Drug(DrugId),
    ItemName NVARCHAR(160) NULL,
    Qty INT NOT NULL,
    UnitPrice DECIMAL(10,2) NULL
);
GO

/* §3.19 Assets & equipment ------------------------------------------- */
IF OBJECT_ID('dbo.Asset') IS NULL
CREATE TABLE dbo.Asset (
    AssetId BIGINT IDENTITY(1,1) CONSTRAINT PK_Asset PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Asset_Branch REFERENCES dbo.Branch(BranchId),
    AssetTag NVARCHAR(40) NOT NULL CONSTRAINT UQ_Asset_Tag UNIQUE,
    Name NVARCHAR(160) NOT NULL,
    Category NVARCHAR(60) NULL,    -- Ventilator/MRI/ICU Monitor...
    AmcExpiry DATE NULL,
    NextMaintenance DATE NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Asset_Status DEFAULT('Active')
);
GO
