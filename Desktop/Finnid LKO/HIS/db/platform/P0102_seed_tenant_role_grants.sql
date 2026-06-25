/* =====================================================================
   P0102 — Seed module grants for the 14 tenant roles (L1.2.4, R3)
   Dynamic RBAC is DATA: each SRS §2.2 tenant role is granted the modules
   its staff need (security.RoleModule). Superadmin already has every module
   (P0101); 'billing' already has the Billing module (P0101). Set-based and
   idempotent (NOT EXISTS guard) — safe to re-run. Modules are the 10 seeded
   in P0101; roles not listed (e.g. hr_manager) get no grant until their
   module exists in the registry.
   ===================================================================== */
SET NOCOUNT ON;
GO

;WITH grants(RoleCode, ModuleCode) AS (
    SELECT v.RoleCode, v.ModuleCode FROM (VALUES
        -- Hospital admin: every functional module (not the platform-admin surface).
        ('admin','registration'),('admin','opd'),('admin','ipd'),('admin','emergency'),
        ('admin','nursing'),('admin','ot'),('admin','lab'),('admin','pharmacy'),('admin','billing'),
        -- Doctor: full clinical surface.
        ('doctor','registration'),('doctor','opd'),('doctor','ipd'),('doctor','emergency'),
        ('doctor','nursing'),('doctor','ot'),('doctor','lab'),('doctor','pharmacy'),
        -- Nurse: ward/bedside care.
        ('nurse','opd'),('nurse','ipd'),('nurse','emergency'),('nurse','nursing'),
        -- Receptionist / front office.
        ('receptionist','registration'),('receptionist','opd'),('receptionist','ipd'),('receptionist','billing'),
        -- Single-desk roles.
        ('lab_tech','lab'),
        ('pharmacist','pharmacy'),
        ('tpa_desk','billing'),
        ('ayushman_mitra','registration'),('ayushman_mitra','billing'),
        ('occ_health_officer','opd'),('occ_health_officer','lab'),
        ('tele_coordinator','opd'),
        ('ambulance_driver','emergency'),
        ('patient','registration')
    ) v(RoleCode, ModuleCode)
)
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM grants g
INNER JOIN security.Role r       ON r.Code = g.RoleCode
INNER JOIN security.AppModule m  ON m.Code = g.ModuleCode
WHERE NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO
