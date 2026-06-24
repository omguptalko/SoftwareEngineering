/* =====================================================================
   Migration 0013 — Emergency triage disposition status (Phase 2.4, §3.5)
   Adds a Status column to EmergencyTriage so the ED board can track
   disposition (Waiting → InTreatment → Admitted/Discharged). Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.EmergencyTriage', 'Status') IS NULL
    ALTER TABLE dbo.EmergencyTriage
        ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Triage_Status DEFAULT('Waiting');
GO
