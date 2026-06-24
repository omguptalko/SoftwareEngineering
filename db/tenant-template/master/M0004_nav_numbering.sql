/* =====================================================================
   Tenant MASTER DB — navigation registry + master-resident numbering (L1.8.5)
   The legacy sidebar registry (ModuleGroup/Module) and a master-side document
   numbering proc (for master-resident numbered docs like AdmissionNo).
   Schemas: master (nav), seq, proc. Idempotent.
   ===================================================================== */
SET XACT_ABORT ON;
GO

IF SCHEMA_ID('seq') IS NULL EXEC('CREATE SCHEMA seq');
GO

IF OBJECT_ID('master.ModuleGroup') IS NULL
CREATE TABLE master.ModuleGroup (
    GroupId    NVARCHAR(40)  NOT NULL CONSTRAINT PK_m_ModuleGroup PRIMARY KEY,
    Label      NVARCHAR(120) NOT NULL,
    Icon       NVARCHAR(60)  NOT NULL,
    SortOrder  INT NOT NULL CONSTRAINT DF_m_ModuleGroup_Sort DEFAULT(0)
);
GO

IF OBJECT_ID('master.Module') IS NULL
CREATE TABLE master.Module (
    ModuleId   NVARCHAR(40)  NOT NULL CONSTRAINT PK_m_Module PRIMARY KEY,
    GroupId    NVARCHAR(40)  NOT NULL CONSTRAINT FK_m_Module_Group REFERENCES master.ModuleGroup(GroupId),
    Icon       NVARCHAR(60)  NOT NULL,
    Label      NVARCHAR(120) NOT NULL,
    Built      BIT NOT NULL CONSTRAINT DF_m_Module_Built DEFAULT(0),
    Badge      NVARCHAR(20)  NULL,
    SortOrder  INT NOT NULL CONSTRAINT DF_m_Module_Sort DEFAULT(0),
    SrsRef     NVARCHAR(20)  NULL
);
GO

/* master-resident document counter + generator (AdmissionNo, etc.) */
IF OBJECT_ID('seq.DocCounter') IS NULL
CREATE TABLE seq.DocCounter (
    BranchId INT NOT NULL,
    DocType  NVARCHAR(20) NOT NULL,
    LastSeq  INT NOT NULL CONSTRAINT DF_m_DocCounter_Seq DEFAULT(0),
    CONSTRAINT PK_m_DocCounter PRIMARY KEY (BranchId, DocType)
);
GO

CREATE OR ALTER PROCEDURE [proc].usp_NextDocNo
    @BranchId INT, @DocType NVARCHAR(20), @Prefix NVARCHAR(10), @FyCode NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Seq INT;
    BEGIN TRAN;
        UPDATE seq.DocCounter WITH (UPDLOCK, SERIALIZABLE)
            SET @Seq = LastSeq = LastSeq + 1
        WHERE BranchId = @BranchId AND DocType = @DocType;
        IF @@ROWCOUNT = 0
        BEGIN
            SET @Seq = 1;
            INSERT seq.DocCounter (BranchId, DocType, LastSeq) VALUES (@BranchId, @DocType, 1);
        END
    COMMIT;
    SELECT CONCAT(@Prefix, '-', @FyCode, '-', RIGHT('000000' + CAST(@Seq AS VARCHAR(6)), 6));
END
GO
