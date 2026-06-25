/* =====================================================================
   P0003 — MFA for privileged roles (L1.2.5, parent 0.7)
   Stores the per-user TOTP shared secret (Base32). MfaEnabled already
   exists on security.AppUser (P0001). Idempotent — safe to re-run.
   ===================================================================== */
SET NOCOUNT ON;
GO

IF COL_LENGTH('security.AppUser', 'MfaSecret') IS NULL
    ALTER TABLE security.AppUser ADD MfaSecret NVARCHAR(64) NULL;
GO
