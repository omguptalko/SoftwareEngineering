/* =====================================================================
   Tenant MASTER DB — remaining reference/master tables (L1.8 schema-split)
   Supplier, scheme/waste/consent/cert templates, ambulance, asset, staff,
   company contract, HFR/HPR registry. All in `master`. Intra-DB FKs kept.
   Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF OBJECT_ID('master.Supplier') IS NULL
CREATE TABLE master.Supplier (
    SupplierId INT IDENTITY(1,1) CONSTRAINT PK_m_Supplier PRIMARY KEY,
    Name NVARCHAR(160) NOT NULL,
    Gstin NVARCHAR(20) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_m_Supplier_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.SchemePackage') IS NULL
CREATE TABLE master.SchemePackage (
    SchemePackageId INT IDENTITY(1,1) CONSTRAINT PK_m_SchemePackage PRIMARY KEY,
    SchemeType NVARCHAR(20) NOT NULL,
    Code NVARCHAR(30) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Rate DECIMAL(12,2) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_m_SchemePkg_Active DEFAULT(1),
    CONSTRAINT UQ_m_SchemePackage UNIQUE (SchemeType, Code)
);
GO

IF OBJECT_ID('master.WasteColourCode') IS NULL
CREATE TABLE master.WasteColourCode (
    ColourCode NVARCHAR(20) NOT NULL CONSTRAINT PK_m_WasteColour PRIMARY KEY,
    Description NVARCHAR(200) NOT NULL
);
GO

IF OBJECT_ID('master.ConsentTemplate') IS NULL
CREATE TABLE master.ConsentTemplate (
    TemplateId INT IDENTITY(1,1) CONSTRAINT PK_m_ConsentTemplate PRIMARY KEY,
    Code NVARCHAR(40) NOT NULL CONSTRAINT UQ_m_ConsentTpl_Code UNIQUE,
    Title NVARCHAR(160) NOT NULL,
    LanguageCode NVARCHAR(10) NOT NULL CONSTRAINT DF_m_ConsentTpl_Lang DEFAULT('en'),
    Body NVARCHAR(MAX) NOT NULL,
    Version INT NOT NULL CONSTRAINT DF_m_ConsentTpl_Ver DEFAULT(1)
);
GO

IF OBJECT_ID('master.CertificateTemplate') IS NULL
CREATE TABLE master.CertificateTemplate (
    TemplateId INT IDENTITY(1,1) CONSTRAINT PK_m_CertTemplate PRIMARY KEY,
    CertType NVARCHAR(40) NOT NULL CONSTRAINT UQ_m_CertTpl_Type UNIQUE,
    Title NVARCHAR(160) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL
);
GO

IF OBJECT_ID('master.Ambulance') IS NULL
CREATE TABLE master.Ambulance (
    AmbulanceId INT IDENTITY(1,1) CONSTRAINT PK_m_Ambulance PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_m_Amb_Branch REFERENCES master.Branch(BranchId),
    VehicleNo NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_m_Amb_Status DEFAULT('Available')
);
GO

IF OBJECT_ID('master.Asset') IS NULL
CREATE TABLE master.Asset (
    AssetId BIGINT IDENTITY(1,1) CONSTRAINT PK_m_Asset PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_m_Asset_Branch REFERENCES master.Branch(BranchId),
    AssetTag NVARCHAR(40) NOT NULL CONSTRAINT UQ_m_Asset_Tag UNIQUE,
    Name NVARCHAR(160) NOT NULL,
    Category NVARCHAR(60) NULL,
    AmcExpiry DATE NULL,
    NextMaintenance DATE NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_m_Asset_Status DEFAULT('Active')
);
GO

IF OBJECT_ID('master.Staff') IS NULL
CREATE TABLE master.Staff (
    StaffId BIGINT IDENTITY(1,1) CONSTRAINT PK_m_Staff PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_m_Staff_Branch REFERENCES master.Branch(BranchId),
    EmployeeCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_m_Staff_Code UNIQUE,
    FullName NVARCHAR(120) NOT NULL,
    Designation NVARCHAR(80) NULL,
    Department NVARCHAR(80) NULL,
    DateOfJoining DATE NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_m_Staff_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.CompanyContract') IS NULL
CREATE TABLE master.CompanyContract (
    ContractId INT IDENTITY(1,1) CONSTRAINT PK_m_CompanyContract PRIMARY KEY,
    CompanyName NVARCHAR(160) NOT NULL,
    PayerCode NVARCHAR(20) NULL,
    ContractType NVARCHAR(40) NULL,
    ValidFrom DATE NULL, ValidTo DATE NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_m_Contract_Active DEFAULT(1)
);
GO

IF OBJECT_ID('master.HfrFacility') IS NULL
CREATE TABLE master.HfrFacility (
    HfrId INT IDENTITY(1,1) CONSTRAINT PK_m_HfrFacility PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_m_Hfr_Branch REFERENCES master.Branch(BranchId),
    HfrCode NVARCHAR(40) NULL,
    OnboardedUtc DATETIME2(3) NULL
);
GO

IF OBJECT_ID('master.HprProfessional') IS NULL
CREATE TABLE master.HprProfessional (
    HprId INT IDENTITY(1,1) CONSTRAINT PK_m_HprProfessional PRIMARY KEY,
    DoctorId INT NOT NULL CONSTRAINT FK_m_Hpr_Doctor REFERENCES master.Doctor(DoctorId),
    HprCode NVARCHAR(40) NULL,
    OnboardedUtc DATETIME2(3) NULL
);
GO
