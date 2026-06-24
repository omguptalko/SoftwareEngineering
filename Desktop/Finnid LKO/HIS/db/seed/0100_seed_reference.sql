/* =====================================================================
   Seed 0100 — Reference & master data (idempotent: seeds only when empty)
   This is the data that USED to be hardcoded in assets/js/data.js.
   It is reference/master data and rightly lives in the DB, not the UI.
   SRS refs noted per block. Re-running will not duplicate rows.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required when inserting into tables with filtered indexes
SET ANSI_NULLS ON;
GO

/* ---- Branches (SRS §3.21) ------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Branch)
INSERT dbo.Branch (Code, Name, City, State) VALUES
 ('BR1', 'Indl-North', 'Refinery Township', 'Gujarat'),
 ('BR2', 'Indl-South', 'Port Industrial Area', 'Gujarat');
GO

/* ---- Module groups + registry (drives wireframe sidebar) ------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.ModuleGroup)
INSERT dbo.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES
 ('front',    'Registration & Front Office', 'bi-person-badge',     1),
 ('clinical', 'Clinical Services',           'bi-clipboard2-pulse', 2),
 ('diag',     'Diagnostics & Pharmacy',      'bi-capsule',          3),
 ('ins',      'Insurance & Schemes',         'bi-shield-plus',      4),
 ('support',  'Support Services',            'bi-life-preserver',   5),
 ('admin',    'Admin · HR · Compliance',     'bi-sliders',          6);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Module)
INSERT dbo.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES
 -- Front office
 ('registration','front','bi-person-vcard','Patient Registration & UHID',1,NULL,1,'§3.1'),
 ('appointments','front','bi-calendar-check','Appointment & Token',1,NULL,2,'§3.2'),
 ('queue','front','bi-display','Queue & Digital Signage',0,'NEW',3,'§3.31'),
 ('feedback','front','bi-chat-square-heart','Feedback & Grievance',0,'NEW',4,'§3.30'),
 -- Clinical
 ('opd','clinical','bi-clipboard2-pulse','OPD Consultation',1,NULL,5,'§3.3'),
 ('ipd','clinical','bi-hospital','IPD Admission & Bed Board',1,NULL,6,'§3.4'),
 ('icu','clinical','bi-heart-pulse','ICU & Emergency Trauma',0,NULL,7,'§3.5'),
 ('ot','clinical','bi-scissors','Operation Theatre (OT)',0,NULL,8,'§3.12'),
 ('nursing','clinical','bi-clipboard2-heart','Nursing & Patient Care',0,NULL,9,'§3.13'),
 ('telemedicine','clinical','bi-camera-video','Telemedicine',0,'NEW',10,'§3.24'),
 ('certificates','clinical','bi-file-earmark-medical','Certificates & Documents',0,NULL,11,'§3.16'),
 -- Diagnostics & Pharmacy
 ('lab','diag','bi-eyedropper','Laboratory (LIS)',1,NULL,12,'§3.8'),
 ('radiology','diag','bi-radioactive','Radiology & Imaging',0,NULL,13,'§3.9'),
 ('pharmacy','diag','bi-capsule','Pharmacy Management',1,NULL,14,'§3.10'),
 ('inventory','diag','bi-box-seam','Inventory & Store',0,NULL,15,'§3.11'),
 ('bloodbank','diag','bi-droplet-half','Blood Bank',0,NULL,16,'§3.7'),
 -- Insurance & Schemes
 ('cashless','ins','bi-credit-card-2-front','Cashless / TPA Claims',1,NULL,17,'§3.15'),
 ('pmjay','ins','bi-bank2','AB PM-JAY (BIS/TMS)',1,'NEW',18,'§7.3'),
 ('esic','ins','bi-building-check','ESIC',0,'NEW',19,'§7.4'),
 ('cghs','ins','bi-shield-plus','CGHS',0,'NEW',20,'§7.5'),
 ('echs','ins','bi-shield-shaded','ECHS',0,'NEW',21,'§7.6'),
 ('statescheme','ins','bi-map','State Health Schemes',0,'NEW',22,'§7.7'),
 ('claimsmis','ins','bi-graph-up','Claims MIS & Reconciliation',0,'NEW',23,'§7.8'),
 -- Support
 ('ambulance','support','bi-truck-front','Ambulance & GPS',0,NULL,24,'§3.6'),
 ('occhealth','support','bi-hospital','Occupational Health',0,'NEW',25,'§3.23'),
 ('diet','support','bi-egg-fried','Diet & Kitchen',0,'NEW',26,'§3.26'),
 ('bmwm','support','bi-trash3','Bio-Medical Waste',0,'NEW',27,'§3.25'),
 ('mortuary','support','bi-file-earmark-x','Mortuary & Death',0,'NEW',28,'§3.27'),
 ('mlc','support','bi-shield-fill-exclamation','Medico-Legal Case (MLC)',0,'NEW',29,'§3.28'),
 ('consent','support','bi-pen','Consent & e-Documents',0,'NEW',30,'§3.29'),
 -- Admin / HR / Compliance
 ('dashboard','admin','bi-speedometer2','Admin Dashboard & Analytics',1,NULL,31,'§3.20'),
 ('billing','admin','bi-receipt','Billing & Revenue Cycle',1,NULL,32,'§3.14'),
 ('hr','admin','bi-people','HR Management',0,NULL,33,'§3.17'),
 ('payroll','admin','bi-cash-coin','Payroll & Overtime',0,NULL,34,'§3.18'),
 ('assets','admin','bi-tools','Asset & Equipment',0,NULL,35,'§3.19'),
 ('multibranch','admin','bi-diagram-3','Multi-Branch Sync',0,NULL,36,'§3.21'),
 ('compliance','admin','bi-shield-check','Compliance & Audit',0,NULL,37,'§3.22'),
 ('abdm','admin','bi-fingerprint','ABDM / ABHA Console',0,'NEW',38,'§6.2'),
 ('ai','admin','bi-cpu','AI Suite',0,NULL,39,'§4'),
 ('paymentgw','admin','bi-wallet2','Payment Gateway',0,NULL,40,'§5');
GO

/* ---- RBAC: the 14 SRS §2.2 roles (data, not code enums) ------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Role)
INSERT dbo.Role (Code, Name, IsPrivileged) VALUES
 ('admin','Admin',1),
 ('doctor','Doctor',0),
 ('nurse','Nurse',0),
 ('receptionist','Receptionist',0),
 ('lab_tech','Lab Technician',0),
 ('pharmacist','Pharmacist',0),
 ('billing','Billing Staff',0),
 ('tpa_desk','TPA / Insurance Desk Officer',0),
 ('ayushman_mitra','Ayushman Mitra / PMAM',0),
 ('occ_health_officer','Occupational Health / Factory Medical Officer',0),
 ('tele_coordinator','Telemedicine Coordinator',0),
 ('hr_manager','HR Manager',1),
 ('ambulance_driver','Ambulance Driver',0),
 ('patient','Patient',0);
GO

/* ---- Blood groups (SRS §3.7) --------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.BloodGroup)
INSERT dbo.BloodGroup (Code, SortOrder) VALUES
 ('A+',1),('A-',2),('B+',3),('B-',4),('AB+',5),('AB-',6),('O+',7),('O-',8);
GO

/* ---- Doctors (SRS §3.3) -------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Doctor)
INSERT dbo.Doctor (Code, Name, Department) VALUES
 ('DR001','Dr. K. Rao','General Medicine'),
 ('DR002','Dr. S. Mehta','Cardiology'),
 ('DR003','Dr. A. Iyer','Orthopaedics'),
 ('DR004','Dr. P. Nair','Pulmonology'),
 ('DR005','Dr. R. Khan','Emergency Medicine'),
 ('DR006','Dr. M. Das','Surgery'),
 ('DR007','Dr. L. Verma','Occupational Health'),
 ('DR008','Dr. T. Bose','Radiology');
GO

/* ---- Drug formulary + stock (SRS §3.10/§3.11) ----------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Drug)
INSERT dbo.Drug (Code, Name, Form, StockQty, ReorderLevel) VALUES
 ('PARA','Paracetamol 500mg','TAB',4210,500),
 ('PANT','Pantoprazole 40mg','TAB',1880,300),
 ('AMOX','Amoxicillin 500mg','CAP',940,300),
 ('AZIT','Azithromycin 500mg','TAB',530,200),
 ('META','Metformin 500mg','TAB',3120,400),
 ('ATOR','Atorvastatin 10mg','TAB',2050,300),
 ('OND','Ondansetron 4mg','INJ',610,200),
 ('NS','Normal Saline 500ml','IVF',220,100);
GO

/* ---- ICD-10 reference (SRS §7.1) ----------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Icd10Code)
INSERT dbo.Icd10Code (Code, Description) VALUES
 ('J18.9','Pneumonia, unspecified organism'),
 ('I10','Essential (primary) hypertension'),
 ('E11.9','Type 2 diabetes mellitus without complications'),
 ('K29.7','Gastritis, unspecified'),
 ('S52.5','Fracture of lower end of radius'),
 ('J45.9','Asthma, unspecified'),
 ('A09','Infectious gastroenteritis'),
 ('R51','Headache');
GO

/* ---- Payer / insurer / scheme empanelment (SRS §3.15/§7) ----------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Payer)
INSERT dbo.Payer (Code, Name, PayerType) VALUES
 ('STAR','Star Health Insurance','Private Insurer'),
 ('HDFC','HDFC ERGO','Private Insurer'),
 ('MDIN','MediAssist TPA','TPA'),
 ('PMJAY','Ayushman Bharat PM-JAY','Govt Scheme'),
 ('ESIC','Employee State Insurance','Govt Scheme'),
 ('CGHS','Central Govt Health Scheme','Govt Scheme'),
 ('ECHS','Ex-servicemen CHS','Govt Scheme'),
 ('CORP','Refinery Corp MoU','Corporate');
GO

/* ---- PM-JAY HBP package rate master (SRS §7.3) --------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.HbpPackage)
INSERT dbo.HbpPackage (Code, Name, Specialty, Rate) VALUES
 ('MG-001','General Medicine — Routine','General Medicine',5000),
 ('CD-014','Angioplasty single stent','Cardiology',60000),
 ('OR-022','Closed reduction fracture','Orthopaedics',18000),
 ('GS-007','Appendectomy','General Surgery',15000),
 ('PD-003','Neonatal care / day','Paediatrics',3500),
 ('OB-002','Caesarean section','Obstetrics',9000);
GO

/* ---- Wards & beds for BR1 (SRS §3.4) ------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Ward)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    INSERT dbo.Ward (BranchId, Name) VALUES
     (@br1,'ICU'),(@br1,'HDU'),(@br1,'General Male'),(@br1,'General Female'),(@br1,'Private'),(@br1,'Semi-Private');

    INSERT dbo.Bed (WardId, BedNo, Status)
    SELECT w.WardId, v.BedNo, v.Status FROM (VALUES
     ('ICU','ICU-01','occ'),('ICU','ICU-02','occ'),('ICU','ICU-03','free'),
     ('HDU','HDU-01','occ'),
     ('General Male','GM-11','occ'),('General Male','GM-12','free'),('General Male','GM-13','clean'),
     ('General Female','GF-05','free'),('General Female','GF-06','occ'),
     ('Private','PR-02','free'),('Private','PR-03','occ'),
     ('Semi-Private','SP-08','block')
    ) v(WardName, BedNo, Status)
    INNER JOIN dbo.Ward w ON w.Name = v.WardName AND w.BranchId = @br1;
END
GO

/* ---- Dashboard snapshot for BR1 (SRS §3.20) ------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.DashboardKpi)
BEGIN
    DECLARE @b INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    INSERT dbo.DashboardKpi (BranchId, [Value], Label, Trend, SortOrder) VALUES
     (@b,'428','OPD Today','up 6.2%',1),
     (@b,'96','IPD Admitted','up 3',2),
     (@b,'11','Emergency','live',3),
     (@b,'18.4L','Revenue Today','up 4.1%',4),
     (@b,'37','Claims Pending','up 5',5),
     (@b,'78%','Bed Occupancy','up 2%',6);

    INSERT dbo.ServiceActivityDaily (BranchId, Service, [Count], Revenue, SortOrder) VALUES
     (@b,'OPD Consultation',428,384200,1),
     (@b,'IPD (active)',96,710500,2),
     (@b,'Laboratory',512,246800,3),
     (@b,'Radiology',88,192000,4),
     (@b,'Pharmacy',640,288300,5),
     (@b,'Operation Theatre',14,320000,6);
END
GO
