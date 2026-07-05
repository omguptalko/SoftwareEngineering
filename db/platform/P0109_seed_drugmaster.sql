/* =====================================================================
   P0109 — Register the Drug Master module + masters.manage permission.
   Admin screen to manage the pharmacy drug catalogue (master.Drug). Granted
   to admin + superadmin only. Idempotent — safe to re-run.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- Module + page: slot after Pharmacy ---- */
IF NOT EXISTS (SELECT 1 FROM security.AppModule WHERE Code = 'drugmaster')
BEGIN
    DECLARE @after INT = ISNULL((SELECT SortOrder FROM security.AppModule WHERE Code = 'pharmacy'),
                                (SELECT ISNULL(MAX(SortOrder), 0) FROM security.AppModule));
    UPDATE security.AppModule SET SortOrder = SortOrder + 1 WHERE SortOrder > @after;
    INSERT security.AppModule (Code, Label, Icon, SortOrder) VALUES ('drugmaster', 'Drug Master', 'bi-capsule-pill', @after + 1);
    DECLARE @mid INT = SCOPE_IDENTITY();
    INSERT security.AppPage (ModuleId, Code, Label, Route, SortOrder) VALUES (@mid, 'drug.master', 'Drug Master', '/app/drugmaster', 1);
    INSERT security.PageAction (PageId, Code, Label)
      SELECT p.PageId, a.Code, a.Label FROM security.AppPage p
      CROSS JOIN (VALUES ('view','View'),('create','Create'),('edit','Edit'),('delete','Delete')) a(Code, Label)
      WHERE p.ModuleId = @mid;
END
GO

/* ---- Permission ---- */
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'masters.manage')
    INSERT security.Permission (Code, Description) VALUES ('masters.manage', 'Manage master data (drug catalogue, etc.)');
GO

/* ---- Grants: module + permission to admin + superadmin ---- */
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM security.Role r CROSS JOIN security.AppModule m
WHERE r.Code IN ('admin','superadmin') AND m.Code = 'drugmaster'
  AND NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);

INSERT security.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM security.Role r CROSS JOIN security.Permission p
WHERE r.Code IN ('admin','superadmin') AND p.Code = 'masters.manage'
  AND NOT EXISTS (SELECT 1 FROM security.RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO

/* ---- Entitlement back-fill ---- */
INSERT platform.TenantModule (TenantId, FiscalYearId, ModuleId, Enabled)
SELECT DISTINCT tm.TenantId, tm.FiscalYearId, dm.ModuleId, 1
FROM platform.TenantModule tm
CROSS JOIN (SELECT ModuleId FROM security.AppModule WHERE Code = 'drugmaster') dm
WHERE tm.Enabled = 1
  AND NOT EXISTS (SELECT 1 FROM platform.TenantModule x
                  WHERE x.TenantId = tm.TenantId AND x.FiscalYearId = tm.FiscalYearId AND x.ModuleId = dm.ModuleId);
GO
