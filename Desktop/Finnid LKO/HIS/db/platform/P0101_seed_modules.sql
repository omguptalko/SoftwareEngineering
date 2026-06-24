/* =====================================================================
   L1 Seed P0101 — Dynamic module / page / action registry (L1.3, R3)
   Seeds security.AppModule / AppPage / PageAction and initial role grants.
   These are DATA — the superadmin manages them at runtime (CRUD + assign).
   Grounded in the actually-built modules (Phases 1–10 + L1 admin).
   Idempotent: each block seeds only when its table/grant is empty/missing.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* ---- Modules -------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM security.AppModule)
INSERT security.AppModule (Code, Label, Icon, SortOrder) VALUES
 ('registration','Registration & Front Office','bi-person-badge',1),
 ('opd','OPD','bi-clipboard2-pulse',2),
 ('ipd','IPD','bi-hospital',3),
 ('emergency','Emergency & ICU','bi-heart-pulse',4),
 ('nursing','Nursing & Patient Care','bi-clipboard2-heart',5),
 ('ot','Operation Theatre','bi-scissors',6),
 ('lab','Laboratory','bi-eyedropper',7),
 ('pharmacy','Pharmacy','bi-capsule',8),
 ('billing','Billing & Payments','bi-receipt',9),
 ('admin','Platform Admin','bi-shield-lock',99);
GO

/* ---- Pages (within modules) ----------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM security.AppPage)
INSERT security.AppPage (ModuleId, Code, Label, Route, SortOrder)
SELECT m.ModuleId, v.Code, v.Label, v.Route, v.SortOrder
FROM (VALUES
 ('registration','reg.patient','Patient Registration','/app/registration',1),
 ('registration','reg.search','Patient Search','/app/registration/search',2),
 ('opd','opd.consult','OPD Consultation','/app/opd',1),
 ('opd','opd.queue','OPD Queue','/app/opd/queue',2),
 ('ipd','ipd.admit','Admission','/app/ipd/admit',1),
 ('ipd','ipd.bedboard','Bed Board','/app/ipd/bedboard',2),
 ('emergency','er.triage','Triage','/app/emergency/triage',1),
 ('emergency','er.board','ED Board','/app/emergency/board',2),
 ('nursing','nur.notes','Nursing Notes','/app/nursing',1),
 ('ot','ot.schedule','OT Schedule','/app/ot/schedule',1),
 ('ot','ot.board','OT Board','/app/ot/board',2),
 ('lab','lab.orders','Lab Orders','/app/lab',1),
 ('lab','lab.results','Lab Results','/app/lab/results',2),
 ('pharmacy','pha.dispense','Dispense','/app/pharmacy',1),
 ('billing','bill.create','Create Bill','/app/billing',1),
 ('billing','bill.payments','Payments','/app/billing/payments',2),
 ('admin','adm.tenants','Tenants','/app/admin/tenants',1),
 ('admin','adm.fiscalyears','Fiscal Years','/app/admin/fiscal-years',2),
 ('admin','adm.modules','Modules & Pages','/app/admin/modules',3),
 ('admin','adm.rbac','Roles & Access','/app/admin/rbac',4),
 ('admin','adm.audit','Audit Trail','/app/admin/audit',5)
) v(ModuleCode, Code, Label, Route, SortOrder)
INNER JOIN security.AppModule m ON m.Code = v.ModuleCode;
GO

/* ---- Page actions (view/create/edit/delete per page) ---------------- */
IF NOT EXISTS (SELECT 1 FROM security.PageAction)
INSERT security.PageAction (PageId, Code, Label)
SELECT p.PageId, a.Code, a.Label
FROM security.AppPage p
CROSS JOIN (VALUES ('view','View'),('create','Create'),('edit','Edit'),('delete','Delete')) a(Code, Label);
GO

/* ---- Grants: superadmin → every module (full access) ---------------- */
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM security.Role r CROSS JOIN security.AppModule m
WHERE r.Code = 'superadmin'
  AND NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO

/* ---- Grants: billing role → the Billing module only (demo) ---------- */
INSERT security.RoleModule (RoleId, ModuleId)
SELECT r.RoleId, m.ModuleId
FROM security.Role r CROSS JOIN security.AppModule m
WHERE r.Code = 'billing' AND m.Code = 'billing'
  AND NOT EXISTS (SELECT 1 FROM security.RoleModule rm WHERE rm.RoleId = r.RoleId AND rm.ModuleId = m.ModuleId);
GO
