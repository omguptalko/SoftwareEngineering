/* =====================================================================
   Migration 0012 — Generic document-number generator
   Used for AdmissionNo (IPD-…), lab barcodes (LB-…), claim/bill numbers, etc.
   Per branch + doc type + year. Mirrors the UHID generator pattern.
   ===================================================================== */
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('dbo.DocCounter') IS NULL
CREATE TABLE dbo.DocCounter (
    BranchId INT NOT NULL,
    DocType  NVARCHAR(20) NOT NULL,
    [Year]   INT NOT NULL,
    LastSeq  INT NOT NULL CONSTRAINT DF_DocCounter_Seq DEFAULT(0),
    CONSTRAINT PK_DocCounter PRIMARY KEY (BranchId, DocType, [Year])
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_NextDocNo
    @BranchId INT,
    @DocType  NVARCHAR(20),
    @Prefix   NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = YEAR(SYSUTCDATETIME());
    DECLARE @Seq INT;

    BEGIN TRAN;
        UPDATE dbo.DocCounter WITH (UPDLOCK, SERIALIZABLE)
            SET @Seq = LastSeq = LastSeq + 1
        WHERE BranchId = @BranchId AND DocType = @DocType AND [Year] = @Year;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @Seq = 1;
            INSERT dbo.DocCounter (BranchId, DocType, [Year], LastSeq) VALUES (@BranchId, @DocType, @Year, 1);
        END
    COMMIT;

    SELECT CONCAT(@Prefix, '-', @Year, '-', RIGHT('000000' + CAST(@Seq AS VARCHAR(6)), 6));
END
GO
