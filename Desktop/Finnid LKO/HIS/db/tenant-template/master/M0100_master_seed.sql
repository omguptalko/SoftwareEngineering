/* =====================================================================
   Tenant MASTER DB seed (L1.5/L1.8) — reference/master data each tenant
   starts with (admin-editable thereafter; isolation is structural — every
   tenant gets its OWN copy in its own DB). Idempotent (seeds only when empty).
   No tenant-specific business values are hardcoded.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM master.BloodGroup)
INSERT master.BloodGroup (Code, SortOrder) VALUES
 ('A+',1),('A-',2),('B+',3),('B-',4),('AB+',5),('AB-',6),('O+',7),('O-',8);
GO

IF NOT EXISTS (SELECT 1 FROM master.Branch)
INSERT master.Branch (Code, Name, City, State) VALUES
 ('BR1','Main Hospital','—','—');
GO

IF NOT EXISTS (SELECT 1 FROM master.Doctor)
INSERT master.Doctor (Code, Name, Department) VALUES
 ('DR001','Dr. K. Rao','General Medicine'),
 ('DR002','Dr. S. Mehta','Cardiology'),
 ('DR003','Dr. A. Iyer','Orthopaedics'),
 ('DR004','Dr. P. Nair','Pulmonology'),
 ('DR005','Dr. R. Khan','Emergency Medicine'),
 ('DR006','Dr. M. Das','Surgery'),
 ('DR007','Dr. L. Verma','Occupational Health'),
 ('DR008','Dr. T. Bose','Radiology');
GO

IF NOT EXISTS (SELECT 1 FROM master.Drug)
INSERT master.Drug (Code, Name, Form, StockQty, ReorderLevel) VALUES
 ('PARA','Paracetamol 500mg','TAB',4210,500),
 ('PANT','Pantoprazole 40mg','TAB',1880,300),
 ('AMOX','Amoxicillin 500mg','CAP',940,300),
 ('AZIT','Azithromycin 500mg','TAB',530,200),
 ('META','Metformin 500mg','TAB',3120,400),
 ('ATOR','Atorvastatin 10mg','TAB',2050,300),
 ('OND','Ondansetron 4mg','INJ',610,200),
 ('NS','Normal Saline 500ml','IVF',220,100);
GO

IF NOT EXISTS (SELECT 1 FROM master.Icd10Code)
INSERT master.Icd10Code (Code, Description) VALUES
 ('J18.9','Pneumonia, unspecified organism'),
 ('I10','Essential (primary) hypertension'),
 ('E11.9','Type 2 diabetes mellitus without complications'),
 ('K29.7','Gastritis, unspecified'),
 ('S52.5','Fracture of lower end of radius'),
 ('J45.9','Asthma, unspecified'),
 ('A09','Infectious gastroenteritis'),
 ('R51','Headache');
GO

IF NOT EXISTS (SELECT 1 FROM master.Payer)
INSERT master.Payer (Code, Name, PayerType) VALUES
 ('STAR','Star Health Insurance','Private Insurer'),
 ('HDFC','HDFC ERGO','Private Insurer'),
 ('MDIN','MediAssist TPA','TPA'),
 ('PMJAY','Ayushman Bharat PM-JAY','Govt Scheme'),
 ('ESIC','Employee State Insurance','Govt Scheme'),
 ('CGHS','Central Govt Health Scheme','Govt Scheme'),
 ('ECHS','Ex-servicemen CHS','Govt Scheme'),
 ('CORP','Refinery Corp MoU','Corporate');
GO

IF NOT EXISTS (SELECT 1 FROM master.HbpPackage)
INSERT master.HbpPackage (Code, Name, Specialty, Rate) VALUES
 ('MG-001','General Medicine — Routine','General Medicine',5000),
 ('CD-014','Angioplasty single stent','Cardiology',60000),
 ('OR-022','Closed reduction fracture','Orthopaedics',18000),
 ('GS-007','Appendectomy','General Surgery',15000),
 ('PD-003','Neonatal care / day','Paediatrics',3500),
 ('OB-002','Caesarean section','Obstetrics',9000);
GO

IF NOT EXISTS (SELECT 1 FROM master.Tariff)
INSERT master.Tariff (BranchId, ServiceCode, ServiceName, Category, Rate, GstRatePct) VALUES
 (NULL,'OPD-CONS','OPD Consultation','OPD',500,0),
 (NULL,'LAB-CBC','Complete Blood Count','Lab',250,0),
 (NULL,'RAD-XR','X-Ray Chest PA','Radiology',400,0),
 (NULL,'IPD-GEN','General Ward / day','IPD',2000,0),
 (NULL,'IPD-ICU','ICU / day','IPD',8000,0);
GO

IF NOT EXISTS (SELECT 1 FROM master.Ward)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM master.Branch WHERE Code = 'BR1');
    INSERT master.Ward (BranchId, Name) VALUES (@br1,'ICU'),(@br1,'General Male'),(@br1,'General Female'),(@br1,'Private');
    INSERT master.Bed (WardId, BedNo, Status)
    SELECT w.WardId, v.BedNo, v.Status FROM (VALUES
     ('ICU','ICU-01','free'),('ICU','ICU-02','occ'),
     ('General Male','GM-11','free'),('General Male','GM-12','occ'),
     ('General Female','GF-05','free'),('Private','PR-02','free')
    ) v(WardName, BedNo, Status)
    INNER JOIN master.Ward w ON w.Name = v.WardName AND w.BranchId = @br1;
END
GO
