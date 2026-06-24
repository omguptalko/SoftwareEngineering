/* =====================================================================
   Seed 0103 — Phase 4 operational data (idempotent)
   SRS §3.10 drug batches, §3.11 suppliers, §3.19 assets.
   One batch per seeded drug (QtyOnHand = the drug's StockQty), suppliers,
   and a few tracked assets for BR1. Re-running will not duplicate.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* Drug batches — one per drug, expiry/MRP per item ------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.DrugBatch)
INSERT dbo.DrugBatch (DrugId, BatchNo, ExpiryDate, Mrp, QtyOnHand)
SELECT d.DrugId, v.BatchNo, v.Expiry, v.Mrp, d.StockQty
FROM (VALUES
  ('PARA','PC2241','2027-08-01', 1.20),
  ('PANT','PT1180','2027-05-01', 4.50),
  ('AMOX','AX1190','2026-07-01', 6.80),
  ('AZIT','AZ5510','2027-03-01', 38.00),
  ('META','MF3320','2027-11-01', 2.10),
  ('ATOR','AT9087','2027-09-01', 7.40),
  ('OND','ON4456','2026-12-01', 9.90),
  ('NS','NS7781','2027-02-01', 28.00)
) v(Code, BatchNo, Expiry, Mrp)
INNER JOIN dbo.Drug d ON d.Code = v.Code;
GO

/* Suppliers (SRS §3.11) ---------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Supplier)
INSERT dbo.Supplier (Name, Gstin) VALUES
 ('MediSupply Co.', '24ABCDE1234F1Z5'),
 ('PharmaDist Pvt Ltd', '24FGHIJ5678K2Z9'),
 ('SurgiCare Equipments', '24LMNOP9012Q3Z1');
GO

/* Assets (SRS §3.19) — with AMC + next-maintenance dates ------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Asset)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    INSERT dbo.Asset (BranchId, AssetTag, Name, Category, AmcExpiry, NextMaintenance, Status) VALUES
     (@br1, 'VENT-001', 'Ventilator — ICU Bay 1', 'Ventilator',  '2026-12-31', '2026-07-15', 'Active'),
     (@br1, 'MRI-001',  'MRI 1.5T Scanner',       'MRI',         '2027-03-31', '2026-09-01', 'Active'),
     (@br1, 'MON-014',  'ICU Multipara Monitor',  'ICU Monitor', '2026-06-30', '2026-06-20', 'Active');
END
GO
