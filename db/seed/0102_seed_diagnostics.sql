/* =====================================================================
   Seed 0102 — Blood bank opening stock (idempotent)
   SRS §3.7. Initial units + safety thresholds per group for BR1.
   Operational data; seeded so the Blood Bank screen has live values.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BloodStock)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    INSERT dbo.BloodStock (BranchId, BloodGroup, Units, SafetyThreshold)
    SELECT @br1, v.Grp, v.Units, v.Threshold FROM (VALUES
      ('A+', 18, 5), ('A-', 4, 3), ('B+', 22, 5), ('B-', 3, 3),
      ('AB+', 9, 3), ('AB-', 2, 2), ('O+', 26, 6), ('O-', 2, 4)
    ) v(Grp, Units, Threshold);
END
GO
