/* M0009 — structured OPD template answers. Each department-template field the doctor
   fills at consult is stored as a row linked to the encounter (queryable/reportable),
   instead of being concatenated into free-text history. Idempotent. */
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
               WHERE s.name='clinical' AND t.name='EncounterTemplateData')
BEGIN
    CREATE TABLE clinical.EncounterTemplateData (
        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
        EncounterId BIGINT        NOT NULL,
        Department  NVARCHAR(80)  NULL,
        FieldLabel  NVARCHAR(120) NOT NULL,
        FieldType   NVARCHAR(20)  NULL,
        Value       NVARCHAR(400) NULL,
        CONSTRAINT FK_EncTplData_Encounter FOREIGN KEY (EncounterId) REFERENCES clinical.Encounter(EncounterId)
    );
    CREATE INDEX IX_EncTplData_Enc ON clinical.EncounterTemplateData(EncounterId);
END
GO
