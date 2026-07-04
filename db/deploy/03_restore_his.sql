/* =====================================================================
   03_restore_his.sql  —  run on the TARGET production SQL Server.
   Copy the 3 .bak files to the server first, then run this. Restores the
   databases with their ORIGINAL names (routing depends on the names being
   exactly HIS_Platform / HIS_Master / HIS_FY2026_27).

   Adjust @bak (folder holding the .bak files) and @data (the server's SQL
   data directory). Logical file names are {DbName} and {DbName}_log.
   ===================================================================== */
SET NOCOUNT ON;
DECLARE @bak  NVARCHAR(260) = N'C:\HIS_Deploy\';   -- where the .bak files are on the server
DECLARE @data NVARCHAR(260) = N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\';  -- target data dir (adjust to your instance)

/* ---- HIS_Platform ---- */
DECLARE @pbak NVARCHAR(300)=@bak+N'HIS_Platform.bak', @pmdf NVARCHAR(300)=@data+N'HIS_Platform.mdf', @pldf NVARCHAR(300)=@data+N'HIS_Platform_log.ldf';
RESTORE DATABASE HIS_Platform FROM DISK=@pbak WITH REPLACE, STATS=10,
    MOVE N'HIS_Platform'     TO @pmdf,
    MOVE N'HIS_Platform_log' TO @pldf;

/* ---- HIS_Master ---- */
DECLARE @mbak NVARCHAR(300)=@bak+N'HIS_Master.bak', @mmdf NVARCHAR(300)=@data+N'HIS_Master.mdf', @mldf NVARCHAR(300)=@data+N'HIS_Master_log.ldf';
RESTORE DATABASE HIS_Master FROM DISK=@mbak WITH REPLACE, STATS=10,
    MOVE N'HIS_Master'     TO @mmdf,
    MOVE N'HIS_Master_log' TO @mldf;

/* ---- HIS_FY2026_27 ---- */
DECLARE @fbak NVARCHAR(300)=@bak+N'HIS_FY2026_27.bak', @fmdf NVARCHAR(300)=@data+N'HIS_FY2026_27.mdf', @fldf NVARCHAR(300)=@data+N'HIS_FY2026_27_log.ldf';
RESTORE DATABASE HIS_FY2026_27 FROM DISK=@fbak WITH REPLACE, STATS=10,
    MOVE N'HIS_FY2026_27'     TO @fmdf,
    MOVE N'HIS_FY2026_27_log' TO @fldf;

PRINT 'Restored HIS_Platform, HIS_Master, HIS_FY2026_27.';
PRINT 'NEXT: run 01_production_cleanup.sql against HIS_Platform to remove demo tenants.';
GO
