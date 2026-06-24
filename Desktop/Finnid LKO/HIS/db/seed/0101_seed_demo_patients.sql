/* =====================================================================
   Seed 0101 — DEMO patients & visit history (optional, idempotent)
   Provides the patient banner + cross-branch visit history the wireframe
   shows. These are DEMO records (skip in production). Mirrors the sample
   patients that were hardcoded in data.js so the UI has live data to bind.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required: Patient has a filtered index (IX_Patient_Aadhaar)
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Patient)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();

    INSERT dbo.Patient (Uhid, RegBranchId, RegisteredAtUtc, FullName, AgeYears, Sex, BloodGroup, Mobile, Category, AadhaarMasked, AbhaNumber)
    VALUES
     ('BR1-2026-000123', @br1, @now, 'Anita Sharma', 34, 'Female', 'B+', '98xxxxxx10', 'Insurance / Cashless', 'XXXX-XXXX-1234', '14-xxxx-xxxx-9921'),
     ('BR1-2026-000124', @br1, @now, 'Rakesh Yadav', 51, 'Male',   'O+', '99xxxxxx22', 'Insurance / Cashless', NULL, NULL),
     ('BR1-2026-000125', @br1, @now, 'Mohan Lal',    62, 'Male',   'A+', '97xxxxxx41', 'PM-JAY', NULL, NULL),
     ('BR1-2026-000126', @br1, @now, 'Sunita Devi',  29, 'Female', 'O-', '96xxxxxx08', 'General (Cash)', NULL, NULL),
     ('BR1-2026-000127', @br1, @now, 'Imran Ali',    45, 'Male',   'B+', '95xxxxxx77', 'ESIC', NULL, NULL),
     ('BR1-2026-000128', @br1, @now, 'Geeta Kumari', 38, 'Female', 'AB+','94xxxxxx33', 'CGHS', NULL, NULL);

    -- Cross-branch visit history for Anita Sharma (shown on Registration screen)
    DECLARE @anita BIGINT = (SELECT PatientId FROM dbo.Patient WHERE Uhid = 'BR1-2026-000123');
    DECLARE @br2 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR2');

    INSERT dbo.PatientVisit (PatientId, BranchId, VisitDate, VisitType, DoctorName, Diagnosis, PayerName) VALUES
     (@anita, @br1, '2026-03-12', 'OPD', 'Dr. K. Rao',   'Hypertension (I10)', 'Star Health'),
     (@anita, @br2, '2026-01-02', 'IPD', 'Dr. S. Mehta', 'Angina',             'Star Health'),
     (@anita, @br1, '2025-11-18', 'Lab', '—',            'Lipid profile',      'Cash');
END
GO
