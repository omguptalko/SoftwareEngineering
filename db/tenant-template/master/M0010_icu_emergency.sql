/* M0010 — ICU & Emergency Trauma (SRS v2.0 §3.5/§3.6).
   Enriches the wireframe-stage clinical.EmergencyTriage into a full triage record
   (5-level colour acuity + triage vitals/GCS + chief complaint + arrival mode +
   attending doctor + emergency-admission link) and adds the ICU monitoring
   flowsheet (clinical.IcuObservation, hung off an ICU admission). Idempotent. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---- (1) Enrich clinical.EmergencyTriage (add columns if missing) ---- */
IF COL_LENGTH('clinical.EmergencyTriage', 'ChiefComplaint')    IS NULL ALTER TABLE clinical.EmergencyTriage ADD ChiefComplaint    NVARCHAR(300) NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'ArrivalMode')       IS NULL ALTER TABLE clinical.EmergencyTriage ADD ArrivalMode       NVARCHAR(20)  NULL;   -- Ambulance/Walk-in/Referral/Police/BroughtDead
IF COL_LENGTH('clinical.EmergencyTriage', 'TriageLevel')       IS NULL ALTER TABLE clinical.EmergencyTriage ADD TriageLevel       TINYINT       NULL;   -- 1..5 (1=Resuscitation .. 5=Non-urgent)
IF COL_LENGTH('clinical.EmergencyTriage', 'PainScore')         IS NULL ALTER TABLE clinical.EmergencyTriage ADD PainScore         TINYINT       NULL;   -- 0..10
IF COL_LENGTH('clinical.EmergencyTriage', 'GcsTotal')          IS NULL ALTER TABLE clinical.EmergencyTriage ADD GcsTotal          TINYINT       NULL;   -- 3..15
IF COL_LENGTH('clinical.EmergencyTriage', 'TempF')             IS NULL ALTER TABLE clinical.EmergencyTriage ADD TempF             DECIMAL(4,1)  NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'Pulse')             IS NULL ALTER TABLE clinical.EmergencyTriage ADD Pulse             INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'BpSystolic')        IS NULL ALTER TABLE clinical.EmergencyTriage ADD BpSystolic        INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'BpDiastolic')       IS NULL ALTER TABLE clinical.EmergencyTriage ADD BpDiastolic       INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'Spo2')              IS NULL ALTER TABLE clinical.EmergencyTriage ADD Spo2              INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'RespRate')          IS NULL ALTER TABLE clinical.EmergencyTriage ADD RespRate          INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'Grbs')              IS NULL ALTER TABLE clinical.EmergencyTriage ADD Grbs              INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'AttendingDoctorId') IS NULL ALTER TABLE clinical.EmergencyTriage ADD AttendingDoctorId INT           NULL;
IF COL_LENGTH('clinical.EmergencyTriage', 'AdmissionId')       IS NULL ALTER TABLE clinical.EmergencyTriage ADD AdmissionId       BIGINT        NULL;   -- set when disposed to admission
IF COL_LENGTH('clinical.EmergencyTriage', 'DisposedUtc')       IS NULL ALTER TABLE clinical.EmergencyTriage ADD DisposedUtc       DATETIME2(0)  NULL;
GO

/* ---- (2) ICU monitoring flowsheet — time-series obs on an ICU admission ---- */
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
               WHERE s.name='clinical' AND t.name='IcuObservation')
BEGIN
    CREATE TABLE clinical.IcuObservation (
        IcuObservationId BIGINT IDENTITY(1,1) CONSTRAINT PK_c_IcuObs PRIMARY KEY,
        AdmissionId  BIGINT NOT NULL CONSTRAINT FK_c_IcuObs_Adm REFERENCES clinical.Admission(AdmissionId),
        RecordedUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_c_IcuObs_Rec DEFAULT SYSUTCDATETIME(),
        HeartRate    INT NULL,
        BpSystolic   INT NULL,
        BpDiastolic  INT NULL,
        Map          INT NULL,          -- mean arterial pressure
        Spo2         INT NULL,
        RespRate     INT NULL,
        TempF        DECIMAL(4,1) NULL,
        Cvp          INT NULL,
        EtCo2        INT NULL,
        Fio2         INT NULL,
        GcsTotal     TINYINT NULL,
        PainScore    TINYINT NULL,
        UrineOutputMl INT NULL,
        BloodSugar   INT NULL,
        VentMode     NVARCHAR(20) NULL, -- SIMV/AC/CPAP/RoomAir
        Notes        NVARCHAR(500) NULL,
        RecordedById INT NULL
    );
    CREATE INDEX IX_c_IcuObs_Adm_Time ON clinical.IcuObservation(AdmissionId, RecordedUtc);
END
GO
