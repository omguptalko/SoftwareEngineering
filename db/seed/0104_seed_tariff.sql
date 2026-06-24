/* =====================================================================
   Seed 0104 — Tariff / service-price master (idempotent)
   SRS §3.14. All-branches (BranchId NULL) price list; admin-editable.
   Rates/GST are master data — never hardcoded in the app.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tariff)
INSERT dbo.Tariff (BranchId, ServiceCode, ServiceName, Category, Rate, GstRatePct) VALUES
 (NULL, 'CONS-GM',  'Consultation - General Medicine', 'OPD',       500.00, 0),
 (NULL, 'CONS-CARD','Consultation - Cardiology',       'OPD',       800.00, 0),
 (NULL, 'LAB-CBC',  'CBC - Complete Blood Count',      'Lab',       250.00, 0),
 (NULL, 'LAB-CRP',  'CRP',                             'Lab',       400.00, 0),
 (NULL, 'LAB-LIPID','Lipid Profile',                   'Lab',       600.00, 0),
 (NULL, 'RAD-CXR',  'Chest X-Ray PA',                  'Radiology', 400.00, 0),
 (NULL, 'RAD-CT',   'CT Thorax',                       'Radiology', 4500.00, 0),
 (NULL, 'IPD-GEN',  'General Ward / day',              'IPD',       2000.00, 0),
 (NULL, 'IPD-ICU',  'ICU / day',                       'IPD',       8000.00, 0),
 (NULL, 'OT-MINOR', 'Minor OT Procedure',              'OT',        5000.00, 0);
GO
