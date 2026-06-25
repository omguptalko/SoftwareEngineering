/* =====================================================================
   P0002 — per-fiscal-year billing (L1.4.2, R3)
   Scopes a subscription to a fiscal year so each hospital is billed in
   accordance with its fiscal year. platform.BillingLedger is already
   (TenantId, FiscalYearId)-scoped (P0001); this only extends Subscription.
   Idempotent — safe to re-run.
   ===================================================================== */
SET NOCOUNT ON;
GO

IF COL_LENGTH('platform.Subscription', 'FiscalYearId') IS NULL
BEGIN
    ALTER TABLE platform.Subscription ADD FiscalYearId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Subscription_FiscalYear')
BEGIN
    ALTER TABLE platform.Subscription
        ADD CONSTRAINT FK_Subscription_FiscalYear
        FOREIGN KEY (FiscalYearId) REFERENCES platform.FiscalYear(FiscalYearId);
END
GO
