/* Clone all table DATA from source DB $(src) into the connected (target) DB.
   Both must share an identical schema. FK constraints are disabled during the
   copy and re-checked at the end. Identity columns are preserved.

   Operational/demo utility — NOT part of the auto-run migration/seed pipeline.
   WARNING: this DELETES all rows in the target DB before copying. Use only for
   demo/test tenants.

   Usage (seed a bare tenant from an already-populated one):
     sqlcmd -S "(localdb)\MSSQLLocalDB" -d RBK_Master    -v src=DEV_Master    -i db/tools/clone_tenant.sql
     sqlcmd -S "(localdb)\MSSQLLocalDB" -d RBK_FY2026_27 -v src=DEV_FY2026_27 -i db/tools/clone_tenant.sql */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
DECLARE @src SYSNAME = N'$(src)';
DECLARE @sql NVARCHAR(MAX);

/* 1) Disable all FK constraints in target */
SELECT @sql = STRING_AGG(CAST('ALTER TABLE '+QUOTENAME(s.name)+'.'+QUOTENAME(t.name)+' NOCHECK CONSTRAINT ALL;' AS NVARCHAR(MAX)), CHAR(10))
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id;
EXEC sys.sp_executesql @sql;

/* 2) Empty all target tables */
SELECT @sql = STRING_AGG(CAST('DELETE '+QUOTENAME(s.name)+'.'+QUOTENAME(t.name)+';' AS NVARCHAR(MAX)), CHAR(10))
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id;
EXEC sys.sp_executesql @sql;

/* 3) Copy each table from source (identity preserved, computed/timestamp skipped) */
DECLARE @schema SYSNAME, @tbl SYSNAME, @hasIdent BIT, @cols NVARCHAR(MAX);
DECLARE tc CURSOR LOCAL FAST_FORWARD FOR
  SELECT s.name, t.name,
         CASE WHEN EXISTS(SELECT 1 FROM sys.columns c WHERE c.object_id=t.object_id AND c.is_identity=1) THEN 1 ELSE 0 END
  FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id;
OPEN tc;
FETCH NEXT FROM tc INTO @schema,@tbl,@hasIdent;
WHILE @@FETCH_STATUS=0
BEGIN
  SELECT @cols = STRING_AGG(CAST(QUOTENAME(c.name) AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY c.column_id)
  FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
  WHERE c.object_id = OBJECT_ID(QUOTENAME(@schema)+'.'+QUOTENAME(@tbl))
    AND c.is_computed = 0 AND ty.name <> 'timestamp';

  IF @cols IS NOT NULL
  BEGIN
    SET @sql =
      CASE WHEN @hasIdent=1 THEN 'SET IDENTITY_INSERT '+QUOTENAME(@schema)+'.'+QUOTENAME(@tbl)+' ON;' ELSE '' END +
      'INSERT '+QUOTENAME(@schema)+'.'+QUOTENAME(@tbl)+' ('+@cols+') SELECT '+@cols+
        ' FROM '+QUOTENAME(@src)+'.'+QUOTENAME(@schema)+'.'+QUOTENAME(@tbl)+';' +
      CASE WHEN @hasIdent=1 THEN 'SET IDENTITY_INSERT '+QUOTENAME(@schema)+'.'+QUOTENAME(@tbl)+' OFF;' ELSE '' END;
    BEGIN TRY EXEC sys.sp_executesql @sql;
    END TRY BEGIN CATCH PRINT 'SKIP '+@schema+'.'+@tbl+' : '+ERROR_MESSAGE(); END CATCH
  END
  FETCH NEXT FROM tc INTO @schema,@tbl,@hasIdent;
END
CLOSE tc; DEALLOCATE tc;

/* 4) Re-enable + re-validate FK constraints */
SELECT @sql = STRING_AGG(CAST('ALTER TABLE '+QUOTENAME(s.name)+'.'+QUOTENAME(t.name)+' WITH CHECK CHECK CONSTRAINT ALL;' AS NVARCHAR(MAX)), CHAR(10))
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id;
BEGIN TRY EXEC sys.sp_executesql @sql; PRINT 'FK re-validation OK';
END TRY BEGIN CATCH PRINT 'FK re-validation warning: '+ERROR_MESSAGE(); END CATCH
