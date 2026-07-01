# db/tools — operational / demo-data utilities

These scripts are **not** part of the automated migration/seed pipeline
(`db/run-migrations.ps1` only globs `db/migrations/*.sql` and `db/seed/*.sql`).
Run them **manually** when you want to populate a demo/test tenant.

| Script | Purpose |
|---|---|
| `fill_demo.sql` | Inserts one demo row into each transactional table that a lightly-used tenant leaves empty (vitals, prescriptions, deposits, compliance reports, rosters, mortuary, etc.). Idempotent (`IF NOT EXISTS` guards). Edit the `USE` lines to target a different tenant. |
| `clone_tenant.sql` | Copies **all** table data from a populated source DB into a same-schema target DB (identity values preserved, FK constraints disabled during copy then re-validated). Used to seed a bare-provisioned tenant from an existing one. **Destructive**: empties the target first — demo/test tenants only. |

## Examples

```bash
# Fill the DEV tenant's remaining empty transactional tables
sqlcmd -S "(localdb)\MSSQLLocalDB" -i db/tools/fill_demo.sql

# Seed a bare RBK tenant from the populated DEV tenant (identical schema)
sqlcmd -S "(localdb)\MSSQLLocalDB" -d RBK_Master    -v src=DEV_Master    -i db/tools/clone_tenant.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d RBK_FY2026_27 -v src=DEV_FY2026_27 -i db/tools/clone_tenant.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d RBK_FY2027_28 -v src=DEV_FY2026_27 -i db/tools/clone_tenant.sql
```

> These write **dummy data** for testing/demo only. Never point `clone_tenant.sql`
> at a production tenant — it deletes the target's rows before copying.
