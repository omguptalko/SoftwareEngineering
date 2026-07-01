/* =====================================================================
   P0104 — Register the Appointments module (L1.3, R3)
   The appointment feature (book slot / token / queue) shipped in the app +
   API but was never added to the module registry, so it never appeared in
   the RBAC-scoped sidebar. This seed registers it end-to-end and back-fills
   entitlements for tenants onboarded before it existed.
   Fully idempotent (guards on the 'appointments' module) — safe to re-run.
   New tenants get it automatically (onboarding enables every module).
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- Module + pages: insert once, slotting it after Registration ----- */
IF NOT EXISTS (SELECT 1 FROM security.AppModule WHERE Code = 'appointments')
BEGIN
    -- Make room at SortOrder 2 (Registration=1, then Appointments, then OPD…).
    UPDATE security.AppModule SET SortOrder = SortOrder + 1 WHERE SortOrder BETWEEN 2 AND 49;
    INSERT security.AppModule (Code, Label, Icon, SortOrder)
    VALUES ('appointments', 'Appointments', 'bi-calendar-check', 2);

    DECLARE @mid INT = (SELECT ModuleId FROM security.AppModule WHERE Code = 'appointments');
    INSERT security.AppPage (ModuleId, Code, Label, Route, SortOrder) VALUES
        (@mid, 'appt.book',  'Appointment Booking', '/app/appointments',       1),
        (@mid, 'appt.queue', 'Token Queue',         '/app/appointments/queue', 2);

    -- Page actions (view/create/edit/delete) for the new pages, for consistency.
    INSERT security.PageAction (PageId, Code, Label)
    SELECT p.PageId, a.Code, a.Label
    FROM security.AppPage p
    CROSS JOIN (VALUES ('view','View'),('create','Create'),('edit','Edit'),('delete','Delete')) a(Code, Label)
    WHERE p.ModuleId = @mid;
END
GO

/* ---- Grants: front-office + clinical roles (mirrors OPD's audience) --- */
;WITH grants(RoleCode) AS (
    SELECT v.RoleCode FROM (VALUES
        ('superadmin'),('admin'),('doctor'),('nurse'),('receptionist')
    ) v(RoleCode)
)
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM grants g
INNER JOIN security.Role r      ON r.Code = g.RoleCode
INNER JOIN security.AppModule m ON m.Code = 'appointments'
WHERE NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO

/* ---- Entitlement back-fill: enable it for every tenant/FY that already
        has other modules enabled (tenants onboarded before it existed). --- */
INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
SELECT DISTINCT tm.TenantId, tm.FiscalYearId, appt.ModuleId, 1
FROM platform.TenantModule tm
CROSS JOIN (SELECT ModuleId FROM security.AppModule WHERE Code = 'appointments') appt
WHERE tm.Enabled = 1
  AND NOT EXISTS (SELECT 1 FROM platform.TenantModule x
                  WHERE x.TenantId = tm.TenantId AND x.FiscalYearId = tm.FiscalYearId AND x.ModuleId = appt.ModuleId);
GO
