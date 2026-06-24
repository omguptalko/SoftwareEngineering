/* =====================================================================
   L1 Seed P0100 — Control-plane roles & permissions (security schema)
   Roles & permissions are DATA (R3 dynamic RBAC). The superadmin USER is
   created by the application bootstrap seeder (PBKDF2 hash from config) —
   no password hash is ever hardcoded in SQL. Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* Roles: 1 platform superadmin + the 14 SRS §2.2 tenant roles. */
IF NOT EXISTS (SELECT 1 FROM security.Role WHERE Code = 'superadmin')
INSERT security.Role (Code, Name, Scope, IsPrivileged) VALUES
 ('superadmin','Platform Super Admin','platform',1);
GO

IF NOT EXISTS (SELECT 1 FROM security.Role WHERE Scope = 'tenant')
INSERT security.Role (Code, Name, Scope, IsPrivileged) VALUES
 ('admin','Hospital Admin','tenant',1),
 ('doctor','Doctor','tenant',0),
 ('nurse','Nurse','tenant',0),
 ('receptionist','Receptionist','tenant',0),
 ('lab_tech','Lab Technician','tenant',0),
 ('pharmacist','Pharmacist','tenant',0),
 ('billing','Billing Staff','tenant',0),
 ('tpa_desk','TPA / Insurance Desk Officer','tenant',0),
 ('ayushman_mitra','Ayushman Mitra / PMAM','tenant',0),
 ('occ_health_officer','Occupational Health / Factory Medical Officer','tenant',0),
 ('tele_coordinator','Telemedicine Coordinator','tenant',0),
 ('hr_manager','HR Manager','tenant',1),
 ('ambulance_driver','Ambulance Driver','tenant',0),
 ('patient','Patient','tenant',0);
GO

/* Platform-level permissions (the superadmin surface). */
IF NOT EXISTS (SELECT 1 FROM security.Permission)
INSERT security.Permission (Code, Description) VALUES
 ('tenant.onboard',    'Onboard a new hospital/tenant on the SaaS'),
 ('tenant.manage',     'Manage tenant profile and lifecycle'),
 ('fiscalyear.manage', 'Open/close fiscal years and run year shift'),
 ('domain.manage',     'Manage tenant domains and common-domain mapping'),
 ('dbcatalog.manage',  'Provision/route tenant databases'),
 ('billing.manage',    'Manage per-fiscal-year subscription and billing'),
 ('rbac.manage',       'Manage roles, permissions and assignments'),
 ('module.manage',     'Create modules/pages and assign them to roles/tenants'),
 ('audit.read',        'Read the control-plane audit trail');
GO

/* Grant ALL permissions to the superadmin role (set-based, idempotent). */
INSERT security.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM security.Role r
CROSS JOIN security.Permission p
WHERE r.Code = 'superadmin'
  AND NOT EXISTS (SELECT 1 FROM security.RolePermission rp
                  WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO
