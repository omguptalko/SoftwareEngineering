/* M0007 — Admin-configurable OPD department templates (per hospital).
   Each hospital defines the extra clinical fields shown in the OPD consult for
   a specialty/department. Seeds sensible defaults on first create. Idempotent. */
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
               WHERE s.name='master' AND t.name='DeptTemplateField')
BEGIN
    CREATE TABLE master.DeptTemplateField (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Department NVARCHAR(80)  NOT NULL,
        Label      NVARCHAR(120) NOT NULL,
        SortOrder  INT           NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_DeptTemplateField_Dept ON master.DeptTemplateField(Department);
END
GO
IF NOT EXISTS (SELECT 1 FROM master.DeptTemplateField)
    INSERT master.DeptTemplateField (Department, Label, SortOrder) VALUES
     ('Surgery','Procedure planned',1),('Surgery','ASA grade',2),('Surgery','Consent taken',3),
     ('Orthopaedics','Affected joint / limb',1),('Orthopaedics','Range of motion',2),('Orthopaedics','X-ray done',3),
     ('Cardiology','ECG findings',1),('Cardiology','Chest pain (Y/N)',2),('Cardiology','NYHA class',3),
     ('Pulmonology','Spirometry',1),('Pulmonology','Sputum / cough',2),
     ('Dermatology','Lesion type',1),('Dermatology','Distribution',2),('Dermatology','Pruritus (Y/N)',3),
     ('General Medicine','Systemic review',1);
GO
