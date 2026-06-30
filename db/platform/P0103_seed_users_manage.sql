/* =============================================================================
   P0103 — 'users.manage' permission + grants (tenant-admin self-service).
   Idempotent: safe to re-run; also patches already-seeded control planes
   (P0100's perm INSERT is guarded by IF NOT EXISTS on the whole table).
   Grants user-management to the platform superadmin and the tenant 'admin' role,
   so a hospital admin can manage ONLY its own tenant's users (scoped server-side).
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'users.manage')
    INSERT security.Permission (Code, Description)
    VALUES ('users.manage', 'Create and manage tenant login users');
GO

INSERT security.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM security.Role r
CROSS JOIN security.Permission p
WHERE p.Code = 'users.manage'
  AND r.Code IN ('superadmin', 'admin')
  AND NOT EXISTS (SELECT 1 FROM security.RolePermission rp
                  WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO
