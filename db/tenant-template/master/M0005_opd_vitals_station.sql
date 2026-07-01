/* =====================================================================
   M0005 — OPD vitals-station integration (SRS §3.2/3.3)
   Vitals are taken at a station BEFORE the doctor's encounter, so a vitals
   row must be able to reference the appointment and exist before any
   encounter. Make clinical.Vitals.EncounterId nullable + add AppointmentId.
   Idempotent.
   ===================================================================== */
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('clinical.Vitals') AND name = 'EncounterId' AND is_nullable = 0)
    ALTER TABLE clinical.Vitals ALTER COLUMN EncounterId BIGINT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('clinical.Vitals') AND name = 'AppointmentId')
    ALTER TABLE clinical.Vitals ADD AppointmentId BIGINT NULL;
GO
