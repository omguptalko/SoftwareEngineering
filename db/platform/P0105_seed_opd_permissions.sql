/* =============================================================================
   P0105 — OPD role separation (vitals attendant vs doctor).
   Two capability permissions gate the OPD flow:
     - opd.vitals   : record vitals at the station (attendant step)
     - opd.consult  : call a patient in + save the consultation (doctor step)
   Plus a dedicated 'vitals_attendant' tenant role that can ONLY do vitals.
   Idempotent — safe to re-run. Superadmin bypasses RBAC (D6).
   ============================================================================= */
SET NOCOUNT ON;
GO

/* ---- The vitals-attendant role (tenant scope) ---- */
IF NOT EXISTS (SELECT 1 FROM security.Role WHERE Code = 'vitals_attendant')
    INSERT security.Role (Code, Name, Scope) VALUES ('vitals_attendant', 'Vitals Attendant', 'tenant');
GO

/* ---- Permissions ---- */
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'opd.vitals')
    INSERT security.Permission (Code, Description) VALUES ('opd.vitals', 'Record vitals at the OPD station');
IF NOT EXISTS (SELECT 1 FROM security.Permission WHERE Code = 'opd.consult')
    INSERT security.Permission (Code, Description) VALUES ('opd.consult', 'Call a patient in and save the OPD consultation');
GO

/* ---- Grants (RolePermission) ---- */
;WITH grants(RoleCode, PermCode) AS (
    SELECT v.RoleCode, v.PermCode FROM (VALUES
        -- who may take vitals: the attendant + front-office/clinical roles + doctor
        ('vitals_attendant','opd.vitals'), ('nurse','opd.vitals'), ('receptionist','opd.vitals'),
        ('admin','opd.vitals'), ('doctor','opd.vitals'), ('superadmin','opd.vitals'),
        -- who may consult (call-in + save): doctor + admin only (NOT the attendant/nurse/reception)
        ('doctor','opd.consult'), ('admin','opd.consult'), ('superadmin','opd.consult')
    ) v(RoleCode, PermCode)
)
INSERT security.RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM grants g
INNER JOIN security.Role r       ON r.Code = g.RoleCode
INNER JOIN security.Permission p ON p.Code = g.PermCode
WHERE NOT EXISTS (SELECT 1 FROM security.RolePermission rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
GO

/* ---- The vitals attendant needs the Appointments module (to reach the station) ---- */
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM security.Role r CROSS JOIN security.AppModule m
WHERE r.Code = 'vitals_attendant' AND m.Code IN ('appointments','registration')
  AND NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO
