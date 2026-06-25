# Finnid HIS ERP — Developer Run Guide

Multi-Branch Industrial Hospital ERP. Stack (per SRS §9): **C# / ASP.NET Core MVC + Web API / CQRS + MediatR / Dapper / MS SQL / jQuery / SignalR**.
Build plan & status: `developmentplancumtracker.md`. Test plan: `deeptestwithdummydata.md`.

## Prerequisites
- .NET 9 SDK
- MS SQL (SQL Server, or LocalDB `(localdb)\MSSQLLocalDB`)
- `sqlcmd` (for the migration runner)

## 1. Create the database + apply migrations & seed
The schema is plain SQL migrations (we use Dapper, not EF). Scripts are **idempotent**.

```powershell
# from the HIS/ folder
pwsh db/run-migrations.ps1                                   # LocalDB, database "HIS"
# or target a server:
pwsh db/run-migrations.ps1 -Server "myserver" -Database "HIS"
```

This runs `db/migrations/0001…0011` then `db/seed/0100…0101`.
Reference/master data (module registry, lookups, the 14 SRS roles, dashboard) is seeded here — it is **not** hardcoded in the UI.

## 2. Configure the connection string
- **Dev**: already set in `src/HIS.Api/appsettings.Development.json`: `ConnectionStrings:Platform` → `HIS_Platform` (control plane), `Provisioning:*` (template root + base connection), and `Tenancy:DevDefaultTenant=DEV`.
- **Prod**: supply `ConnectionStrings:Platform`, `Provisioning:*`, `Jwt:*`, `Cors:Origins` via environment / Azure App Configuration / **Key Vault**. Nothing is hardcoded.

> **L1.8.5 cutover (multi-tenant data plane).** Application data no longer flows through a single `HIS` DB / `SqlConnectionFactory` (retired). All repositories now use **`ITenantConnectionFactory`**, which routes each request to the resolved tenant's **`{Tenant}_Master`** DB (longitudinal: patient/clinical/masters/audit) or its **`{Tenant}_FY{…}`** DB (fiscal-scoped: billing/insurance/HR/pharmacy/…), per `platform.DbCatalog`. On startup the dev tenant `DEV` is auto-provisioned (`DEV_Master` + `DEV_FY2026_27`) from `db/tenant-template/{master,fy}`. To load existing single-DB data into it, run `db/tenant-template/migrate/Backfill_DEV_from_dbo.sql` (identity-preserving). `ConnectionStrings:His` is now only the legacy/backfill source.

## 3. Run the API (also serves the wireframe)
```bash
dotnet run --project src/HIS.Api
```
Open the printed URL (e.g. `http://localhost:5142/`). The wireframe lives in `src/HIS.Api/wwwroot` and is served same-origin, so the JS calls the API with relative URLs (no hardcoded base).

### Verified endpoints
| Endpoint | Purpose |
|---|---|
| `GET /api/health` | liveness |
| `GET /api/meta/registry` | sidebar groups + 40 modules (was static `data.js`) |
| `GET /api/lookups/{type}` | F3 lookups: doctor/drug/icd10/ward/payer/patient/package |
| `GET /api/patients/default` · `GET /api/patients/{uhid}` | patient + cross-branch visit history |
| `POST /api/patients` | register patient → generates UHID, de-dups Aadhaar, audited |
| `GET /api/dashboard` | KPIs + service activity |

## 4. Solution layout
```
src/HIS.Domain          entities (Patient, Branch, Module, Role, Claim, …)
src/HIS.Shared          Result<T>, IBranchContext
src/HIS.Application      CQRS commands/queries/handlers, FluentValidation, MediatR behaviors
src/HIS.Infrastructure   Dapper repositories, ITenantConnectionFactory (per-tenant master/FY DB routing), AuditWriter
src/HIS.Api              Web API controllers + serves wwwroot wireframe
src/HIS.Web              ASP.NET Core MVC (reserved for server-rendered screens)
db/migrations, db/seed   MS SQL schema + reference data
```

## 5. Architecture rules (enforced)
- Every use-case is a MediatR `ICommand`/`IQuery` + handler; controllers only `Send`.
- Cross-cutting pipeline: **Validation → Logging → Audit** (audit row per write, SRS §8.1).
- Dapper with **parameterized SQL only** (no string concatenation → injection-safe).
- **Nothing hardcoded**: connection strings, JWT, CORS, default branch, scheme/tariff/package rates, reference data — all config- or DB-driven.
