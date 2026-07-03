/* =====================================================================
   deploy_stored_procs.sql — re-deploy all tenant stored procedures to the
   CURRENT version. Idempotent (CREATE OR ALTER + schema guard). Run against
   every tenant Master DB; the numbering proc (usp_NextDocNo) is also part of
   each FY DB.

   The app itself uses Dapper inline SQL for business logic — these are the
   only stored procedures: server-side identifier/number generation.

     proc.usp_NextUhid   (Master)      — new UHID for a branch (source: M0001)
     proc.usp_NextDocNo  (Master + FY) — next document number  (source: M0004/F0001)
   ===================================================================== */
IF SCHEMA_ID('proc') IS NULL EXEC('CREATE SCHEMA proc');
GO

/* ---- UHID generator (Master DBs — references master.Branch) ---- */
CREATE OR ALTER PROCEDURE [proc].usp_NextUhid @BranchId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Year INT = YEAR(SYSUTCDATETIME());
    DECLARE @Code NVARCHAR(10) = (SELECT Code FROM master.Branch WHERE BranchId = @BranchId);
    SELECT CONCAT(ISNULL(@Code, CONCAT('BR', @BranchId)), '-', @Year, '-', RIGHT('000000' + CAST(ABS(CHECKSUM(NEWID())) % 1000000 AS VARCHAR(6)), 6));
END
GO

/* ---- Document-number generator (Master + FY DBs — uses seq.DocCounter) ---- */
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
