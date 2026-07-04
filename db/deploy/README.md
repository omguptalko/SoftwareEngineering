# HIS — single-tenant production deployment

Move the **HIS** tenant to a production server as a clean, self-contained
deployment. Only **3 databases** go to the server; the demo tenants
(DEV/AIMS/RBK/AIMM) are never copied, and any demo control-plane rows are
scrubbed from the restored platform DB.

> The app is multi-tenant, but for one hospital you deploy exactly one tenant.
> `.bak` files carry the schema **and** the stored procedures (`proc.usp_NextUhid`,
> `proc.usp_NextDocNo`) — nothing extra to deploy for those.

## The 3 databases

| Database | Contains |
|----------|----------|
| `HIS_Platform` | Control plane — tenant/DB catalog, RBAC, users |
| `HIS_Master` | Patient + clinical + master data |
| `HIS_FY2026_27` | Financial data for FY 2026-27 |

## Prerequisites on the server
- **SQL Server** (Express/Standard/Enterprise or Azure SQL) — **not** LocalDB.
- **.NET 9 runtime** (or run the self-contained publish).
- DNS: the hospital's host (e.g. `his.hospital.in`) resolves to the server. This
  host is already in `platform.TenantDomain`; requests resolve to HIS by host.

## Steps

### 1) Back up the 3 DBs (on this dev machine)
Edit `@dir` in `02_backup_his.sql` to an existing folder, then:
```
sqlcmd -S "(localdb)\MSSQLLocalDB" -i db/deploy/02_backup_his.sql
```
→ produces `HIS_Platform.bak`, `HIS_Master.bak`, `HIS_FY2026_27.bak`.

### 2) Copy the 3 `.bak` files to the server.

### 3) Restore on the server (production SQL Server)
Edit `@bak` and `@data` paths in `03_restore_his.sql` to match the server, then:
```
sqlcmd -S <server> -E -i db/deploy/03_restore_his.sql
```
DB names are preserved exactly (routing depends on them).

### 4) Scrub demo tenants from the restored platform DB
```
sqlcmd -S <server> -E -d HIS_Platform -i db/deploy/01_production_cleanup.sql
```
Leaves only the **HIS** tenant + the **superadmin** login (removes DEV/AIMS/RBK/AIMM
and the `billing.demo` user). Prints what remains.

### 5) Deploy the app (HIS.Api) with production settings
Publish and run with `ASPNETCORE_ENVIRONMENT=Production`. Secrets are supplied via
environment variables (never committed) — see `docs/DEPLOYMENT.md`. Minimum:

| Env var | Value |
|---------|-------|
| `ConnectionStrings__Platform` | `Server=<server>;Database=HIS_Platform;...` |
| `Provisioning__BaseConnection` | `Server=<server>;Database=HIS_Platform;...` (catalog is swapped per tenant DB) |
| `Jwt__SigningKey` | strong secret |
| `Security__DataProtection__Key` | strong secret (field encryption at rest) |

`appsettings.Production.json` already sets `Tenancy:DevDefaultTenant = HIS`, so any
non-host-matched request falls back to HIS (never a missing demo tenant).

### 6) Verify
- Browse `https://his.hospital.in` → login `his.admin` / (the password you set).
- Platform/superadmin: `superadmin` (password from `HIS_Platform` / your seed).
- Check the sidebar renders (42 modules), and `patient.Patient` is empty (clean start).

## Notes
- **Don't** run `01_production_cleanup.sql` on your local dev machine — it would
  remove the DEV tenant that local dev + the smoke suite depend on. Production
  copy only (it hard-stops if the HIS tenant isn't present).
- Adding more fiscal years later: use the app's **Open Fiscal Year** flow
  (`POST /api/platform/fiscal-years/open`) — it provisions the next `HIS_FY…` DB.
- Onboarding additional hospitals later: **Onboard Tenant**
  (`POST /api/platform/tenants/onboard`) — no manual DB work.
