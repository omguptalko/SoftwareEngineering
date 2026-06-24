/* =====================================================================
   Seed 0105 — Government scheme package rate masters (idempotent)
   SRS §7.5 CGHS, §7.6 ECHS, §7.7 State. Admin-editable rate masters.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemePackage)
INSERT dbo.SchemePackage (SchemeType, Code, Name, Rate) VALUES
 ('CGHS',  'CGHS-OPD-001', 'OPD Consultation (CGHS rate)',        350.00),
 ('CGHS',  'CGHS-CARD-014','Coronary Angiography (CGHS)',        12000.00),
 ('ECHS',  'ECHS-GEN-002', 'General Ward / day (ECHS)',           1800.00),
 ('ECHS',  'ECHS-ORTHO-07','Closed Reduction Fracture (ECHS)',   16000.00),
 ('State', 'ST-UP-ARG-01', 'State Arogya - General Medicine',     4500.00),
 ('State', 'ST-UP-ARG-22', 'State Arogya - Appendectomy',        14000.00);
GO
