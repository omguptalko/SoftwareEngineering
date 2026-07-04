/* =====================================================================
   P0108 — Register the Vitals Station module (SRS §3.2).
   In larger hospitals vitals are taken at a dedicated desk, separate from
   booking. This registers a standalone "Vitals Station" module (its own
   worklist of booked patients needing vitals), granted only to vitals-desk
   staff (vitals_attendant / nurse). Reuses the existing opd.vitals permission
   and POST /api/appointments/{id}/vitals endpoint — no backend change.

   Per-hospital: entitlement-gated via platform.TenantModule, so a small clinic
   can disable it (keep the combined flow) and a large hospital can enable it.
   Fully idempotent — safe to re-run.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- Module + page: slot it right after Appointments ---- */
IF NOT EXISTS (SELECT 1 FROM security.AppModule WHERE Code = 'vitals')
BEGIN
    DECLARE @after INT = ISNULL((SELECT SortOrder FROM security.AppModule WHERE Code = 'appointments'),
                                ISNULL((SELECT SortOrder FROM security.AppModule WHERE Code = 'registration'), 2));
    UPDATE security.AppModule SET SortOrder = SortOrder + 1 WHERE SortOrder > @after;
    INSERT security.AppModule (Code, Label, Icon, SortOrder) VALUES ('vitals', 'Vitals Station', 'bi-heart-pulse', @after + 1);

    DECLARE @vid INT = SCOPE_IDENTITY();
    INSERT security.AppPage (ModuleId, Code, Label, Route, SortOrder) VALUES (@vid, 'vitals.worklist', 'Vitals Worklist', '/app/vitals', 1);
    INSERT security.PageAction (PageId, Code, Label)
      SELECT p.PageId, a.Code, a.Label FROM security.AppPage p
      CROSS JOIN (VALUES ('view','View'),('create','Create'),('edit','Edit'),('delete','Delete')) a(Code, Label)
      WHERE p.ModuleId = @vid;
END
GO

/* ---- Grants: vitals-desk staff (the attendant + nurse) + admins ---- */
;WITH grants(RoleCode) AS (
    SELECT v.RoleCode FROM (VALUES ('superadmin'),('admin'),('vitals_attendant'),('nurse')) v(RoleCode)
)
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM grants g
INNER JOIN security.Role r      ON r.Code = g.RoleCode
INNER JOIN security.AppModule m ON m.Code = 'vitals'
WHERE NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO

/* ---- Entitlement back-fill for existing tenants/FYs ---- */
INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
SELECT DISTINCT tm.TenantId, tm.FiscalYearId, v.ModuleId, 1
FROM platform.TenantModule tm
CROSS JOIN (SELECT ModuleId FROM security.AppModule WHERE Code = 'vitals') v
WHERE tm.Enabled = 1
  AND NOT EXISTS (SELECT 1 FROM platform.TenantModule x
                  WHERE x.TenantId = tm.TenantId AND x.FiscalYearId = tm.FiscalYearId AND x.ModuleId = v.ModuleId);
GO
