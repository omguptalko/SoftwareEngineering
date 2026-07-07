/* =====================================================================
   Scheme Desk (ESIC / CGHS / ECHS / State) — activate sidebar modules and
   seed ESIC package tariff (CGHS/ECHS/State already seeded). Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

/* ---- ESIC package tariff (master.SchemePackage) ------------------- */
IF OBJECT_ID('master.SchemePackage') IS NOT NULL
BEGIN
    MERGE master.SchemePackage AS t
    USING (VALUES
        ('ESIC-OPD-001', 'ESIC', N'OPD Consultation (ESIC rate)',        300.00),
        ('ESIC-IPD-014', 'ESIC', N'General Ward Admission / day (ESIC)',  1200.00),
        ('ESIC-SUR-021', 'ESIC', N'Appendectomy (ESIC package)',          14000.00),
        ('ESIC-CAR-033', 'ESIC', N'Angioplasty single stent (ESIC)',      58000.00),
        ('ESIC-MAT-040', 'ESIC', N'Normal Delivery (ESIC package)',        9000.00)
    ) AS s(Code, SchemeType, Name, Rate)
    ON t.Code = s.Code
    WHEN NOT MATCHED THEN
        INSERT (SchemeType, Code, Name, Rate, IsActive)
        VALUES (s.SchemeType, s.Code, s.Name, s.Rate, 1);
END
GO

/* ---- activate scheme + MIS + occ-health + telemedicine + ambulance */
IF OBJECT_ID('master.Module') IS NOT NULL
    UPDATE master.Module SET Built = 1
    WHERE ModuleId IN ('esic', 'cghs', 'echs', 'statescheme', 'claimsmis', 'occhealth', 'telemedicine', 'ambulance', 'diet');
GO
