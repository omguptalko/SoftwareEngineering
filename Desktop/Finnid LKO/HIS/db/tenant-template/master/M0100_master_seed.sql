/* =====================================================================
   Tenant MASTER DB seed (L1.5) — universal reference data only.
   No tenant-specific business values are hardcoded here; branch/doctor/
   tariff rows are inserted by the onboarding flow or admin screens.
   Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM master.BloodGroup)
INSERT master.BloodGroup (Code, SortOrder) VALUES
 ('A+',1),('A-',2),('B+',3),('B-',4),('AB+',5),('AB-',6),('O+',7),('O-',8);
GO
