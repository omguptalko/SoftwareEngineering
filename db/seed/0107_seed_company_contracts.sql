/* =====================================================================
   Seed 0107 — Employer / company health contracts (idempotent)
   SRS §3.23 occupational health. Admin-editable contract master.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CompanyContract)
INSERT dbo.CompanyContract (CompanyName, PayerCode, ContractType, ValidFrom, ValidTo) VALUES
 ('Refinery Corp (Unit-1)', 'CORP', 'PME',  '2026-01-01', '2026-12-31'),
 ('Steelworks Industrial',  'CORP', 'PEME', '2026-01-01', '2026-12-31'),
 ('Port Logistics Pvt Ltd', 'CORP', 'Corporate', '2026-04-01', '2027-03-31');
GO
