/* =====================================================================
   Seed 0108 — Phase 10 reference/operational data (idempotent)
   SRS: §3.25 BMWM colour codes, §3.29 consent templates, §3.16 cert
   templates, §3.31 queue counters, §3.6 ambulances. All admin-editable.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* BMWM colour codes (BMWM Rules 2016) -------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.WasteColourCode)
INSERT dbo.WasteColourCode (ColourCode, Description) VALUES
 ('Yellow', 'Human/animal anatomical, soiled, expired meds, chemical waste'),
 ('Red',    'Contaminated recyclable (tubing, bottles, catheters)'),
 ('White',  'Waste sharps (needles, syringes) - puncture-proof'),
 ('Blue',   'Glassware, metallic body implants');
GO

/* Consent templates (multilingual) ----------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ConsentTemplate)
INSERT dbo.ConsentTemplate (Code, Title, LanguageCode, Body, Version) VALUES
 ('SURG-EN', 'Surgery Consent',      'en', 'I consent to the surgical procedure as explained to me.', 1),
 ('SURG-HI', 'Surgery Consent (Hindi)', 'hi', N'मैं मुझे समझाई गई शल्य प्रक्रिया के लिए सहमति देता/देती हूँ।', 1),
 ('ANAES-EN','Anaesthesia Consent',  'en', 'I consent to anaesthesia and understand its risks.', 1),
 ('DATA-EN', 'Data Sharing Consent (ABDM)', 'en', 'I consent to share my health records under ABDM.', 1);
GO

/* Certificate templates ---------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.CertificateTemplate)
INSERT dbo.CertificateTemplate (CertType, Title, Body) VALUES
 ('Birth',     'Birth Certificate',     'This certifies the birth of the named child.'),
 ('Death',     'Death Certificate',     'This certifies the death of the named person.'),
 ('Fitness',   'Fitness Certificate',   'This certifies fitness for duty.'),
 ('Medical',   'Medical Certificate',   'This certifies the medical condition stated.'),
 ('Discharge', 'Discharge Summary',     'Discharge summary for the admission.'),
 ('Referral',  'Referral Letter',       'Referral to the specified facility/specialist.');
GO

/* Queue counters + ambulances for BR1 -------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.QueueCounter)
BEGIN
    DECLARE @br1 INT = (SELECT BranchId FROM dbo.Branch WHERE Code = 'BR1');
    INSERT dbo.QueueCounter (BranchId, Area, CounterName) VALUES
     (@br1, 'OPD', 'OPD-1'), (@br1, 'OPD', 'OPD-2'),
     (@br1, 'Pharmacy', 'Pharmacy-1'), (@br1, 'Billing', 'Billing-1');

    INSERT dbo.Ambulance (BranchId, VehicleNo, Status) VALUES
     (@br1, 'UP32-AB-1023', 'Available'),
     (@br1, 'UP32-AB-1090', 'Available'),
     (@br1, 'UP32-AB-1145', 'Available');
END
GO
