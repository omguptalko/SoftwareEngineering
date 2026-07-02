/* M0006 — OPD "call next": record when a waiting (VitalsDone) patient is called
   into the consult room (status -> InConsultation). Idempotent. */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('clinical.Appointment') AND name = 'CalledUtc')
    ALTER TABLE clinical.Appointment ADD CalledUtc DATETIME2 NULL;
GO
