/* =====================================================================
   Migration 0008 — HR & Payroll (Phase 8)
   SRS: §3.17 HR Management, §3.18 Payroll & Overtime.
   Pay rules / rates are config/master, never hardcoded.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.Staff') IS NULL
CREATE TABLE dbo.Staff (
    StaffId BIGINT IDENTITY(1,1) CONSTRAINT PK_Staff PRIMARY KEY,
    BranchId INT NOT NULL CONSTRAINT FK_Staff_Branch REFERENCES dbo.Branch(BranchId),
    EmployeeCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_Staff_Code UNIQUE,
    FullName NVARCHAR(120) NOT NULL,
    Designation NVARCHAR(80) NULL,
    Department NVARCHAR(80) NULL,
    DateOfJoining DATE NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Staff_Active DEFAULT(1)
);
GO

IF OBJECT_ID('dbo.Attendance') IS NULL
CREATE TABLE dbo.Attendance (
    AttendanceId BIGINT IDENTITY(1,1) CONSTRAINT PK_Attendance PRIMARY KEY,
    StaffId BIGINT NOT NULL CONSTRAINT FK_Att_Staff REFERENCES dbo.Staff(StaffId),
    WorkDate DATE NOT NULL,
    InTime TIME NULL, OutTime TIME NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Att_Status DEFAULT('Present'),
    CONSTRAINT UQ_Attendance UNIQUE (StaffId, WorkDate)
);
GO

IF OBJECT_ID('dbo.DutyRoster') IS NULL
CREATE TABLE dbo.DutyRoster (
    RosterId BIGINT IDENTITY(1,1) CONSTRAINT PK_DutyRoster PRIMARY KEY,
    StaffId BIGINT NOT NULL CONSTRAINT FK_Roster_Staff REFERENCES dbo.Staff(StaffId),
    ShiftDate DATE NOT NULL,
    Shift NVARCHAR(20) NULL    -- Morning/Evening/Night
);
GO

IF OBJECT_ID('dbo.LeaveRequest') IS NULL
CREATE TABLE dbo.LeaveRequest (
    LeaveId BIGINT IDENTITY(1,1) CONSTRAINT PK_LeaveRequest PRIMARY KEY,
    StaffId BIGINT NOT NULL CONSTRAINT FK_Leave_Staff REFERENCES dbo.Staff(StaffId),
    FromDate DATE NOT NULL, ToDate DATE NOT NULL,
    LeaveType NVARCHAR(30) NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Leave_Status DEFAULT('Pending')
);
GO

IF OBJECT_ID('dbo.PayrollRun') IS NULL
CREATE TABLE dbo.PayrollRun (
    PayrollId BIGINT IDENTITY(1,1) CONSTRAINT PK_PayrollRun PRIMARY KEY,
    StaffId BIGINT NOT NULL CONSTRAINT FK_Payroll_Staff REFERENCES dbo.Staff(StaffId),
    PeriodYear INT NOT NULL, PeriodMonth INT NOT NULL,
    BasicPay DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pay_Basic DEFAULT(0),
    OvertimeHours DECIMAL(7,2) NOT NULL CONSTRAINT DF_Pay_OtHrs DEFAULT(0),
    OvertimeAmount DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pay_OtAmt DEFAULT(0),
    GrossPay DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pay_Gross DEFAULT(0),
    NetPay DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pay_Net DEFAULT(0),
    OvertimeApprovedBy BIGINT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Pay_Status DEFAULT('Draft'),
    CONSTRAINT UQ_PayrollRun UNIQUE (StaffId, PeriodYear, PeriodMonth)
);
GO
