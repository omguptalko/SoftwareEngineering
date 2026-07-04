/* =====================================================================
   02_backup_his.sql  —  run on the SOURCE server (this dev machine).
   Produces the 3 .bak files to copy to the production server. Only the HIS
   tenant's databases are backed up — the demo tenants (DEV/AIMS/RBK/AIMM) are
   never included, so production never sees them.

   Set @dir to an EXISTING, writable folder (BACKUP will not create it).
   Backup is online + non-destructive to the source DBs.
   ===================================================================== */
SET NOCOUNT ON;
DECLARE @dir NVARCHAR(260) = N'C:\HIS_Deploy\';      -- <-- change to your folder (must exist)

DECLARE @p NVARCHAR(300) = @dir + N'HIS_Platform.bak';
DECLARE @m NVARCHAR(300) = @dir + N'HIS_Master.bak';
DECLARE @f NVARCHAR(300) = @dir + N'HIS_FY2026_27.bak';

BACKUP DATABASE HIS_Platform  TO DISK = @p WITH INIT, FORMAT, STATS = 10;
BACKUP DATABASE HIS_Master    TO DISK = @m WITH INIT, FORMAT, STATS = 10;
BACKUP DATABASE HIS_FY2026_27 TO DISK = @f WITH INIT, FORMAT, STATS = 10;

PRINT 'Backups written to ' + @dir + ' — copy HIS_Platform.bak, HIS_Master.bak, HIS_FY2026_27.bak to the server.';
GO
