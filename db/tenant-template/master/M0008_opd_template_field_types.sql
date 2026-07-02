/* M0008 — richer OPD template field types. Add FieldType (text/number/checkbox/select)
   and Options (comma-separated, for select). Idempotent; showcases a few types. */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('master.DeptTemplateField') AND name='FieldType')
    ALTER TABLE master.DeptTemplateField ADD FieldType NVARCHAR(20) NOT NULL DEFAULT 'text';
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('master.DeptTemplateField') AND name='Options')
    ALTER TABLE master.DeptTemplateField ADD Options NVARCHAR(400) NULL;
GO
UPDATE master.DeptTemplateField SET FieldType='checkbox'
  WHERE Label IN ('Consent taken','X-ray done','Chest pain (Y/N)','Pruritus (Y/N)','Biopsy taken (Y/N)') AND FieldType='text';
UPDATE master.DeptTemplateField SET FieldType='select', Options='I,II,III,IV,V' WHERE Label='ASA grade' AND FieldType='text';
UPDATE master.DeptTemplateField SET FieldType='select', Options='I,II,III,IV'   WHERE Label='NYHA class' AND FieldType='text';
UPDATE master.DeptTemplateField SET FieldType='number' WHERE Label='Body surface area %' AND FieldType='text';
GO
