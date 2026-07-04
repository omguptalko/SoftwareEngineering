/* =====================================================================
   01_production_cleanup.sql  —  run on the PRODUCTION copy of HIS_Platform
   AFTER restoring it on the server. Removes every non-HIS (demo) tenant and
   its users from the control plane, so production only knows the HIS tenant.

   Keeps: the HIS tenant + the platform superadmin (TenantId IS NULL).
   Does NOT drop any databases — it only cleans control-plane rows. The demo
   tenant DBs simply are never copied to the server (see backup script).

   *** DO NOT RUN THIS ON YOUR LOCAL DEV MACHINE *** — it would remove the DEV
   tenant that local dev + the smoke suite depend on. Production copy only.
   Idempotent: re-running is a no-op once only HIS remains.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM platform.Tenant WHERE Code = N'HIS')
BEGIN
    RAISERROR('Safety stop: HIS tenant not found in this HIS_Platform. Aborting cleanup.', 16, 1);
    RETURN;
END

DECLARE @demo TABLE (TenantId INT PRIMARY KEY);
INSERT @demo SELECT TenantId FROM platform.Tenant WHERE Code <> N'HIS';

-- children first, then the tenant row (mirrors DecommissionTenant's platform cleanup)
DELETE ur FROM security.UserRole ur JOIN security.AppUser u ON u.UserId = ur.UserId WHERE u.TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM security.AppUser       WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.TenantModule  WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.Subscription  WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.BillingLedger WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.DbCatalog     WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.TenantDomain  WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.FiscalYear    WHERE TenantId IN (SELECT TenantId FROM @demo);
DELETE FROM platform.Tenant        WHERE TenantId IN (SELECT TenantId FROM @demo);

-- Remove leftover platform-level (tenant-NULL) demo logins, keeping ONLY the superadmin.
DELETE ur FROM security.UserRole ur JOIN security.AppUser u ON u.UserId = ur.UserId
    WHERE u.TenantId IS NULL AND u.IsSuperAdmin = 0;
DELETE FROM security.AppUser WHERE TenantId IS NULL AND IsSuperAdmin = 0;

PRINT 'Cleanup complete. Remaining tenants:';
SELECT Code, Name FROM platform.Tenant;
PRINT 'Remaining logins (superadmin + HIS users):';
SELECT UserName, TenantId, IsSuperAdmin FROM security.AppUser;
GO
