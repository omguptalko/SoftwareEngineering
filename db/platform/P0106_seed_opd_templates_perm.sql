/* P0106 — 'opd.templates.manage' permission (admin-configurable OPD templates).
   Lets a hospital admin (and superadmin) edit the per-department consult templates.
   Idempotent. */
SET NOCOUNT ON;
GO
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'opd.templates.manage')
    INSERT security.Permission (Code, Description) VALUES ('opd.templates.manage', 'Configure OPD department consult templates');
GO
INSERT security.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM security.Role r CROSS JOIN security.Permission p
WHERE p.Code = 'opd.templates.manage' AND r.Code IN ('superadmin', 'admin')
  AND NOT EXISTS (SELECT 1 FROM security.RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO
