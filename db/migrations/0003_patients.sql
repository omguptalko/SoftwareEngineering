/* =====================================================================
   Migration 0003 — Patient master & visit history (Phase 1)
   SRS: §3.1 Patient Registration & UHID, §6.1 Aadhaar (stored masked),
        §6.2 ABHA, §3.21 cross-branch visit history.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required for the filtered index below
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('dbo.Patient') IS NULL
CREATE TABLE dbo.Patient (
    PatientId         BIGINT IDENTITY(1,1) CONSTRAINT PK_Patient PRIMARY KEY,
    Uhid              NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Patient_Uhid UNIQUE,
    RegBranchId       INT NOT NULL CONSTRAINT FK_Patient_Branch REFERENCES dbo.Branch(BranchId),
    RegisteredAtUtc   DATETIME2(3)  NOT NULL,
    FullName          NVARCHAR(120) NOT NULL,
    GuardianName      NVARCHAR(120) NULL,
    AgeYears          INT NULL,
    DateOfBirth       DATE NULL,
    Sex               NVARCHAR(10)  NOT NULL,
    BloodGroup        NVARCHAR(5)   NULL,
    Mobile            NVARCHAR(15)  NOT NULL,
    Email             NVARCHAR(120) NULL,
    MaritalStatus     NVARCHAR(20)  NULL,
    Category          NVARCHAR(40)  NULL,
    Address           NVARCHAR(200) NULL,
    City              NVARCHAR(80)  NULL,
    State             NVARCHAR(80)  NULL,
    Pincode           NVARCHAR(10)  NULL,
    Occupation        NVARCHAR(80)  NULL,
    EmployerPayerCode NVARCHAR(20)  NULL,
    AadhaarMasked     NVARCHAR(20)  NULL,   -- masked only; never store full Aadhaar plain (SRS §8.1/§8.2)
    AbhaNumber        NVARCHAR(20)  NULL,
    AbhaAddress       NVARCHAR(80)  NULL,
    IsActive          BIT NOT NULL CONSTRAINT DF_Patient_Active DEFAULT(1)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Patient_Search')
    CREATE INDEX IX_Patient_Search ON dbo.Patient(FullName, Mobile);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Patient_Aadhaar')
    CREATE INDEX IX_Patient_Aadhaar ON dbo.Patient(AadhaarMasked) WHERE AadhaarMasked IS NOT NULL;
GO

IF OBJECT_ID('dbo.PatientVisit') IS NULL
CREATE TABLE dbo.PatientVisit (
    VisitId    BIGINT IDENTITY(1,1) CONSTRAINT PK_PatientVisit PRIMARY KEY,
    PatientId  BIGINT NOT NULL CONSTRAINT FK_Visit_Patient REFERENCES dbo.Patient(PatientId),
    BranchId   INT NOT NULL CONSTRAINT FK_Visit_Branch REFERENCES dbo.Branch(BranchId),
    VisitDate  DATE NOT NULL,
    VisitType  NVARCHAR(20)  NOT NULL,   -- OPD/IPD/Lab
    DoctorName NVARCHAR(120) NULL,
    Diagnosis  NVARCHAR(200) NULL,
    PayerName  NVARCHAR(120) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Visit_Patient')
    CREATE INDEX IX_Visit_Patient ON dbo.PatientVisit(PatientId, VisitDate DESC);
GO
