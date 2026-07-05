/* M0011 — sidebar module registry (master.Module drives /api/meta/registry ->
   the sidebar tree). Adds the Vitals Station and Emergency & Trauma modules as
   built, and promotes the old "ICU & Emergency Trauma" wireframe row to the
   built "ICU Monitoring" module. Idempotent. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* Vitals Station — dedicated vitals desk, in the front-office flow (after Appointments).
   Guarded on the group existing so under-provisioned tenants skip gracefully. */
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId = 'vitals')
   AND EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId = 'front')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, SortOrder, SrsRef)
    VALUES ('vitals', 'front', 'bi-heart-pulse', 'Vitals Station', 1, 3, '3.2');

/* Emergency & Trauma — triage board / arrival / disposition (clinical group). */
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId = 'emergency')
   AND EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId = 'clinical')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, SortOrder, SrsRef)
    VALUES ('emergency', 'clinical', 'bi-truck-front', 'Emergency & Trauma', 1, 8, '3.5');

/* Promote the wireframe ICU row to the built ICU Monitoring module. */
UPDATE master.Module SET Label = 'ICU Monitoring', Icon = 'bi-activity', Built = 1 WHERE ModuleId = 'icu';

/* Operation Theatre is now a built module (schedule -> start -> complete). */
UPDATE master.Module SET Built = 1 WHERE ModuleId = 'ot';

/* Nursing & Patient Care is now a built module (notes against admissions). */
UPDATE master.Module SET Built = 1 WHERE ModuleId = 'nursing';

/* Radiology & Imaging is now a built module (order -> report). */
UPDATE master.Module SET Built = 1 WHERE ModuleId = 'radiology';

/* Certificates & Documents is now a built module (issue -> approve). */
UPDATE master.Module SET Built = 1 WHERE ModuleId = 'certificates';

/* Inventory & Store is now a built module (stock levels + purchase orders). */
UPDATE master.Module SET Built = 1 WHERE ModuleId = 'inventory';
GO

/* Drug Master - pharmacy catalogue admin (diagnostics & pharmacy group). */
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId = 'drugmaster')
   AND EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId = 'diag')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, SortOrder, SrsRef)
    VALUES ('drugmaster', 'diag', 'bi-capsule-pill', 'Drug Master', 1, (SELECT ISNULL(MAX(SortOrder),50)+1 FROM master.Module), '3.8');
GO
