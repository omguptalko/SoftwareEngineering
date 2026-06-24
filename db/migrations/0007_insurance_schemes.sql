/* =====================================================================
   Migration 0007 — Insurance, Cashless & Government Schemes (Phase 7)
   SRS: §3.15/§7.1 cashless engine, §7.2 NHCX, §7.3 PM-JAY, §7.4 ESIC,
        §7.5 CGHS, §7.6 ECHS, §7.7 State, §7.8 reconciliation.
   Co-pay, caps, sub-limits, package/scheme rates are MASTER data.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* Insurance policy capture + cap/co-pay master (SRS §3.15) ----------- */
IF OBJECT_ID('dbo.InsurancePolicy') IS NULL
CREATE TABLE dbo.InsurancePolicy (
    PolicyId BIGINT IDENTITY(1,1) CONSTRAINT PK_InsurancePolicy PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Policy_Patient REFERENCES dbo.Patient(PatientId),
    PayerId INT NOT NULL CONSTRAINT FK_Policy_Payer REFERENCES dbo.Payer(PayerId),
    PolicyNo NVARCHAR(60) NULL,
    MemberId NVARCHAR(60) NULL,
    SumInsured DECIMAL(14,2) NULL,
    AvailableBalance DECIMAL(14,2) NULL,
    RoomRentCapPerDay DECIMAL(12,2) NULL,
    CoPayPct DECIMAL(5,2) NULL,
    ValidTo DATE NULL
);
GO

/* Cashless claim lifecycle: pre-auth -> query/shortfall -> enhancement
   -> final bill -> settlement (SRS §7.1) ----------------------------- */
IF OBJECT_ID('dbo.Claim') IS NULL
CREATE TABLE dbo.Claim (
    ClaimId BIGINT IDENTITY(1,1) CONSTRAINT PK_Claim PRIMARY KEY,
    ClaimNo NVARCHAR(30) NOT NULL CONSTRAINT UQ_Claim_No UNIQUE,
    BranchId INT NOT NULL CONSTRAINT FK_Claim_Branch REFERENCES dbo.Branch(BranchId),
    PatientId BIGINT NOT NULL CONSTRAINT FK_Claim_Patient REFERENCES dbo.Patient(PatientId),
    PayerId INT NOT NULL CONSTRAINT FK_Claim_Payer REFERENCES dbo.Payer(PayerId),
    PolicyId BIGINT NULL CONSTRAINT FK_Claim_Policy REFERENCES dbo.InsurancePolicy(PolicyId),
    AdmissionId BIGINT NULL CONSTRAINT FK_Claim_Adm REFERENCES dbo.Admission(AdmissionId),
    Channel NVARCHAR(20) NULL,        -- NHCX/TPA Portal/TMS/ESIC e-bill...
    ProvisionalIcd10 NVARCHAR(10) NULL,
    PreAuthAmount DECIMAL(14,2) NULL,
    ApprovedAmount DECIMAL(14,2) NULL,
    SettledAmount DECIMAL(14,2) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Claim_Status DEFAULT('Eligibility'),
    -- Eligibility/PreAuth/Query/Shortfall/Enhancement/FinalBill/Approved/Settled/Denied
    SubmittedUtc DATETIME2(3) NULL,
    TatDueUtc DATETIME2(3) NULL
);
GO

IF OBJECT_ID('dbo.ClaimEvent') IS NULL
CREATE TABLE dbo.ClaimEvent (
    EventId BIGINT IDENTITY(1,1) CONSTRAINT PK_ClaimEvent PRIMARY KEY,
    ClaimId BIGINT NOT NULL CONSTRAINT FK_ClaimEvent_Claim REFERENCES dbo.Claim(ClaimId),
    EventType NVARCHAR(30) NOT NULL,   -- PreAuth/Query/Shortfall/Enhancement/FinalBill/Approval/Denial/Settlement/Appeal
    Amount DECIMAL(14,2) NULL,
    Notes NVARCHAR(MAX) NULL,
    OccurredUtc DATETIME2(3) NOT NULL CONSTRAINT DF_ClaimEvent_Utc DEFAULT(SYSUTCDATETIME())
);
GO

IF OBJECT_ID('dbo.ClaimDocument') IS NULL
CREATE TABLE dbo.ClaimDocument (
    DocId BIGINT IDENTITY(1,1) CONSTRAINT PK_ClaimDocument PRIMARY KEY,
    ClaimId BIGINT NOT NULL CONSTRAINT FK_ClaimDoc_Claim REFERENCES dbo.Claim(ClaimId),
    DocType NVARCHAR(60) NOT NULL,
    DocUrl NVARCHAR(400) NULL,
    IsMandatory BIT NOT NULL CONSTRAINT DF_ClaimDoc_Mand DEFAULT(0),
    Attached BIT NOT NULL CONSTRAINT DF_ClaimDoc_Att DEFAULT(0)
);
GO

/* §7.3 PM-JAY: beneficiary + TMS case ------------------------------- */
IF OBJECT_ID('dbo.PmjayBeneficiary') IS NULL
CREATE TABLE dbo.PmjayBeneficiary (
    BeneficiaryId BIGINT IDENTITY(1,1) CONSTRAINT PK_PmjayBeneficiary PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Pmjay_Patient REFERENCES dbo.Patient(PatientId),
    PmjayId NVARCHAR(40) NULL,
    BisVerified BIT NOT NULL CONSTRAINT DF_Pmjay_Bis DEFAULT(0),
    FamilyFloater DECIMAL(14,2) NULL,
    UsedAmount DECIMAL(14,2) NULL
);
GO

IF OBJECT_ID('dbo.PmjayCase') IS NULL
CREATE TABLE dbo.PmjayCase (
    CaseId BIGINT IDENTITY(1,1) CONSTRAINT PK_PmjayCase PRIMARY KEY,
    ClaimId BIGINT NOT NULL CONSTRAINT FK_PmjayCase_Claim REFERENCES dbo.Claim(ClaimId),
    PackageId INT NULL CONSTRAINT FK_PmjayCase_Pkg REFERENCES dbo.HbpPackage(PackageId),
    TmsCaseNo NVARCHAR(40) NULL,
    AyushmanMitra NVARCHAR(80) NULL,
    AadhaarDischargeVerified BIT NOT NULL CONSTRAINT DF_Pmjay_Discharge DEFAULT(0)
);
GO

/* §7.4-§7.7 Scheme membership (ESIC/CGHS/ECHS/State) ----------------- */
IF OBJECT_ID('dbo.SchemeMembership') IS NULL
CREATE TABLE dbo.SchemeMembership (
    MembershipId BIGINT IDENTITY(1,1) CONSTRAINT PK_SchemeMembership PRIMARY KEY,
    PatientId BIGINT NOT NULL CONSTRAINT FK_Scheme_Patient REFERENCES dbo.Patient(PatientId),
    SchemeType NVARCHAR(20) NOT NULL,   -- ESIC/CGHS/ECHS/State
    MemberNo NVARCHAR(60) NULL,         -- IP no / CGHS card / ECHS card / State beneficiary id
    SecondaryRef NVARCHAR(60) NULL,     -- Pehchan / referral / permission-letter
    Verified BIT NOT NULL CONSTRAINT DF_Scheme_Verified DEFAULT(0),
    ValidTo DATE NULL
);
GO

/* §7.5/§7.6 Government scheme package rate master (CGHS/ECHS/State) --- */
IF OBJECT_ID('dbo.SchemePackage') IS NULL
CREATE TABLE dbo.SchemePackage (
    SchemePackageId INT IDENTITY(1,1) CONSTRAINT PK_SchemePackage PRIMARY KEY,
    SchemeType NVARCHAR(20) NOT NULL,
    Code NVARCHAR(30) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Rate DECIMAL(12,2) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_SchemePkg_Active DEFAULT(1),
    CONSTRAINT UQ_SchemePackage UNIQUE (SchemeType, Code)
);
GO

/* §7.8 Reconciliation against bank UTR files ------------------------- */
IF OBJECT_ID('dbo.SettlementReconciliation') IS NULL
CREATE TABLE dbo.SettlementReconciliation (
    ReconId BIGINT IDENTITY(1,1) CONSTRAINT PK_SettlementRecon PRIMARY KEY,
    ClaimId BIGINT NULL CONSTRAINT FK_Recon_Claim REFERENCES dbo.Claim(ClaimId),
    Utr NVARCHAR(40) NULL,
    BankAmount DECIMAL(14,2) NULL,
    ReconciledUtc DATETIME2(3) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Recon_Status DEFAULT('Unmatched')
);
GO
