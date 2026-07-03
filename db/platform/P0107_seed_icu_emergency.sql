/* =====================================================================
   P0107 — Register the ICU & Emergency Trauma modules (SRS §3.5/§3.6).
   Adds two RBAC-scoped modules to the sidebar (Emergency & Trauma, ICU
   Monitoring), the icu.monitor / emergency.* permissions, role grants,
   and back-fills entitlements for tenants onboarded before they existed.
   Fully idempotent — safe to re-run. New tenants get them automatically.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- Modules + pages: register each independently (Emergency may pre-exist
        from the wireframe build; ICU is new). Slotted after IPD. ---- */
IF NOT EXISTS (SELECT 1 FROM security.AppModule WHERE Code = 'emergency')
BEGIN
    DECLARE @afterE INT = ISNULL((SELECT SortOrder FROM security.AppModule WHERE Code = 'ipd'), 4);
    UPDATE security.AppModule SET SortOrder = SortOrder + 1 WHERE SortOrder > @afterE;
    INSERT security.AppModule (Code, Label, Icon, SortOrder) VALUES ('emergency', 'Emergency & Trauma', 'bi-truck-front', @afterE + 1);
    DECLARE @er INT = SCOPE_IDENTITY();
    INSERT security.AppPage (ModuleId, Code, Label, Route, SortOrder) VALUES (@er, 'er.triage', 'Triage Board', '/app/emergency', 1);
    INSERT security.PageAction (PageId, Code, Label)
      SELECT p.PageId, a.Code, a.Label FROM security.AppPage p
      CROSS JOIN (VALUES ('view','View'),('create','Create'),('edit','Edit'),('delete','Delete')) a(Code, Label)
      WHERE p.ModuleId = @er;
END
GO

IF NOT EXISTS (SELECT 1 FROM security.AppModule WHERE Code = 'icu')
BEGIN
    DECLARE @afterI INT = ISNULL((SELECT SortOrder FROM security.AppModule WHERE Code = 'emergency'),
                                 ISNULL((SELECT SortOrder FROM security.AppModule WHERE Code = 'ipd'), 4));
    UPDATE security.AppModule SET SortOrder = SortOrder + 1 WHERE SortOrder > @afterI;
    INSERT security.AppModule (Code, Label, Icon, SortOrder) VALUES ('icu', 'ICU Monitoring', 'bi-activity', @afterI + 1);
    DECLARE @ic INT = SCOPE_IDENTITY();
    INSERT security.AppPage (ModuleId, Code, Label, Route, SortOrder) VALUES (@ic, 'icu.mon', 'ICU Monitoring', '/app/icu', 1);
    INSERT security.PageAction (PageId, Code, Label)
      SELECT p.PageId, a.Code, a.Label FROM security.AppPage p
      CROSS JOIN (VALUES ('view','View'),('create','Create'),('edit','Edit'),('delete','Delete')) a(Code, Label)
      WHERE p.ModuleId = @ic;
END
GO

/* ---- Permissions ---- */
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'emergency.triage')
    INSERT security.Permission (Code, Description) VALUES ('emergency.triage', 'Triage emergency arrivals');
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'emergency.manage')
    INSERT security.Permission (Code, Description) VALUES ('emergency.manage', 'Dispose emergency visits (admit/discharge)');
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'icu.monitor')
    INSERT security.Permission (Code, Description) VALUES ('icu.monitor', 'Record ICU monitoring observations');
GO

/* ---- Role → module grants (sidebar visibility) ---- */
;WITH grants(RoleCode, ModuleCode) AS (
    SELECT v.RoleCode, v.ModuleCode FROM (VALUES
        ('superadmin','emergency'),('admin','emergency'),('doctor','emergency'),('nurse','emergency'),('receptionist','emergency'),
        ('superadmin','icu'),('admin','icu'),('doctor','icu'),('nurse','icu')
    ) v(RoleCode, ModuleCode)
)
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM grants g
INNER JOIN security.Role r      ON r.Code = g.RoleCode
INNER JOIN security.AppModule m ON m.Code = g.ModuleCode
WHERE NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO

/* ---- Role → permission grants ---- */
;WITH grants(RoleCode, PermCode) AS (
    SELECT v.RoleCode, v.PermCode FROM (VALUES
        ('doctor','emergency.triage'),('nurse','emergency.triage'),('receptionist','emergency.triage'),('admin','emergency.triage'),('superadmin','emergency.triage'),
        ('doctor','emergency.manage'),('admin','emergency.manage'),('superadmin','emergency.manage'),
        ('doctor','icu.monitor'),('nurse','icu.monitor'),('admin','icu.monitor'),('superadmin','icu.monitor')
    ) v(RoleCode, PermCode)
)
INSERT security.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM grants g
INNER JOIN security.Role r       ON r.Code = g.RoleCode
INNER JOIN security.Permission p ON p.Code = g.PermCode
WHERE NOT EXISTS (SELECT 1 FROM security.RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO

/* ---- Entitlement back-fill for existing tenants/FYs ---- */
INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
SELECT DISTINCT tm.TenantId, tm.FiscalYearId, m.ModuleId, 1
FROM platform.TenantModule tm
CROSS JOIN (SELECT ModuleId FROM security.AppModule WHERE Code IN ('emergency','icu')) m
WHERE tm.Enabled = 1
  AND NOT EXISTS (SELECT 1 FROM platform.TenantModule x
                  WHERE x.TenantId = tm.TenantId AND x.FiscalYearId = tm.FiscalYearId AND x.ModuleId = m.ModuleId);
GO
