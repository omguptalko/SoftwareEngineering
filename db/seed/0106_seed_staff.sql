/* =====================================================================
   Seed 0106 — Staff master (idempotent)
   SRS §3.17. A few BR1 staff so HR/Payroll screens have live data.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Staff)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    INSERT dbo.Staff (BranchId, EmployeeCode, FullName, Designation, Department, DateOfJoining) VALUES
     (@br1, 'EMP-001', 'Sunita Yadav',  'Staff Nurse',     'Nursing',          '2021-06-15'),
     (@br1, 'EMP-002', 'Ramesh Tiwari', 'Billing Officer', 'Accounts',         '2019-02-01'),
     (@br1, 'EMP-003', 'Imran Khan',    'Lab Technician',  'Laboratory',       '2022-09-10'),
     (@br1, 'EMP-004', 'Pooja Singh',   'Receptionist',    'Front Office',     '2023-01-20'),
     (@br1, 'EMP-005', 'Vijay Pal',     'Ambulance Driver','Transport',        '2020-11-05');
END
GO
