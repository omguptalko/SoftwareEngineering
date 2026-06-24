# L1 Enhancement — Development Plan cum Tracker
### SaaS Multi-Tenant, Schema-Organised, Fiscal-Year-Partitioned re-platform of the Finnid Hospital ERP
**Parent plan:** `developmentplancumtracker.md` (Phases 0–13, the functional build).
**This L1 plan is additive:** it re-platforms the foundation (DB topology, tenancy, RBAC, onboarding) without re-stating SRS functional scope. Every "current state" claim below is cited to a real file/line in this repo — **nothing is assumed**.

---

## 0. Why this document exists (the five requirements, verbatim)

The user requirement, captured word-for-word, drives this plan:

> 1. there should be proper schema handling.
> 2. the master tables must be in master schema & other tables must be in respective schema.
> 3. while onboarding the hospital on SAAS there should be fiscal dropdown so that each hospital must be registered for fiscal year & on next year shift on superadmin the billing need to be managed in accordance with fiscal year as well as other variables such as module, rights, page etc. the super admin must have dynamic module, page & assign page module kind of architecture.
> 4. at the time of onboarding of the hospital, the hospital shall be created with db specific to fiscal year ie. the DB shall have the master table & proc db, hospital specific table db further divided into fiscal years. the db complete mechanism shall be dealt automatically no human intervention shall be needed.
> 5. Every hospital shall be allowed to be hosted on its domain & can accessed with common domain also. so website domain & login mapping shall also be needed.

Plus the agreed item: **add a superadmin login with RBAC.**

---

## 1. Verified current architecture (as-is) — analysed line by line

| # | Fact (verified) | Evidence in repo |
|---|---|---|
| A1 | **One physical database, one schema.** All 90 tables + 2 procs across 13 migrations are created as `dbo.*`. No SQL schemas other than `dbo`. | `db/migrations/0001…0013_*.sql` (every `CREATE TABLE dbo.` / `CREATE OR ALTER PROCEDURE dbo.`) |
| A2 | **One static connection string.** `SqlConnectionFactory` reads `ConnectionStrings:His` once in its constructor and opens every connection against it. No tenant/year awareness. | `src/HIS.Infrastructure/Persistence/SqlConnectionFactory.cs:17-28` |
| A3 | **Org unit is `Branch` only.** No `Hospital`/`Tenant`/`FiscalYear`/`Domain`/`Subscription` entity exists. | `src/HIS.Domain/Entities/Organisation.cs:4-12`; `db/migrations/0001_core_platform.sql:10-18` |
| A4 | **RBAC tables exist but are empty of grants/users.** `Role`, `Permission`, `RolePermission`, `AppUser`, `UserRole` are defined; only the **14 roles** are seeded — **0 permissions, 0 role-permissions, 0 users**. | tables: `0001_core_platform.sql:44-89`; seed: `db/seed/0100_seed_reference.sql:80-97`; grep confirms no `INSERT dbo.Permission` / `AppUser` / `RolePermission` anywhere in `db/` |
| A5 | **No login / token issuance.** `Program.cs` configures JWT **validation** only, and only when `Jwt:SigningKey` is present (it is not set in dev). There is no `/api/auth/login`, no password hashing, no token minting. | `src/HIS.Api/Program.cs:24-43,84-88`; `appsettings.Development.json` has no `Jwt` section |
| A6 | **Context = branch + user from JWT claims, else dev fallback.** `BranchContextMiddleware` reads `uid/name/branchId/branchCode/role` claims; if unauthenticated it falls back to `Dev:DefaultBranchId`/`Dev:DefaultBranchCode`. | `src/HIS.Api/Middleware/BranchContextMiddleware.cs:21-32`; `src/HIS.Shared/Context/IBranchContext.cs:7-26` |
| A7 | **Navigation is module-only, global, partly static.** `ModuleGroup`+`Module` tables drive the sidebar, but there is **no `Page`**, no page↔module link, no role↔module/page mapping. The wireframe still hardcodes which screens are wired (`HIS.screens = {…}`). | `0001_core_platform.sql:21-42`; `src/HIS.Api/wwwroot/assets/js/modules.js:773` |
| A8 | **Numbering is calendar-year, per branch.** `UhidCounter` / `DocCounter` key on `YEAR(SYSUTCDATETIME())` — **not fiscal year**. | `0001_core_platform.sql:111-143`; `0012_sequences.sql:11-45` |
| A9 | **Audit is single-DB, dbo.** `AuditEntry` carries `BranchId`/`UserId` but no `TenantId`/`FiscalYearId`. | `0001_core_platform.sql:91-109`; `src/HIS.Domain/Entities/Security.cs:31-45` |

**Conclusion:** the functional build (Phases 0–10) is healthy, but the platform is **single-tenant, single-DB, single-schema, no-auth**. All five requirements are foundational additions, not tweaks. This warrants a dedicated L1 track with a careful cutover.

---

## 2. Target architecture (recommended) — three planes

```
┌─────────────────────────────────────────────────────────────────────┐
│ CONTROL PLANE  — DB: HIS_Platform   (one, shared)                     │
│   schema platform : Tenant, Hospital, FiscalYear, TenantFiscalYear,   │
│                     TenantDomain, Subscription, BillingLedger,        │
│                     TenantModule (entitlements per FY), DbCatalog     │
│   schema security : SuperAdmin/AppUser, Role, Permission, AppModule,  │
│                     AppPage, PageAction, RoleModule, RolePage,        │
│                     RolePageAction, UserRole                          │
│   schema audit    : PlatformAudit                                     │
└─────────────────────────────────────────────────────────────────────┘
        │ provisions + routes (automated, no human intervention)
        ▼
┌───────────────────────────────┐   ┌───────────────────────────────────┐
│ TENANT MASTER PLANE            │   │ TENANT DATA PLANE (per fiscal year) │
│ DB: {Tenant}_Master            │   │ DB: {Tenant}_FY{YYYY-YY}            │
│  schema master : Doctor, Drug, │   │  schema clinical, diagnostics,      │
│   Icd10Code, Payer, HbpPackage,│   │   pharmacy, inventory, billing,     │
│   BloodGroup, Tariff,          │   │   insurance, scheme, hr, occhealth, │
│   SchemePackage, WasteColour…, │   │   telemedicine, support, abdm, ai,  │
│   Consent/CertTemplate,        │   │   compliance, audit                 │
│   Branch, Ward, Bed, Supplier  │   │  (fiscal-scoped transactions + the  │
│  schema patient: Patient,      │   │   per-FY numbering counters)        │
│   PatientVisit (longitudinal)  │   │                                     │
│  schema proc   : usp_* + procs │   │                                     │
└───────────────────────────────┘   └───────────────────────────────────┘
```

**Reconciling requirements 2 and 4 (they are about different levels, not contradictory):**
- **R2 = schema-level** separation *inside* a database (`master` schema vs domain schemas).
- **R4 = database-level** separation: a **master+proc DB** distinct from a **per-fiscal-year data DB**.
- We apply **both**: the master+proc DB internally uses the `master`/`patient`/`proc` schemas; each per-FY data DB internally uses the domain schemas.

**Longitudinal-vs-fiscal split (design rule):** clinical/patient history (Patient, Encounter, EMR) is longitudinal and must not fracture at year boundaries (SRS §3.1 cross-branch UHID + `PatientVisit` history). **Fiscal-scoped** data (Bills, Payments, Claims, Payroll, deposits, the per-FY counters) lives in `{Tenant}_FY{…}`. **Longitudinal** data lives in `{Tenant}_Master`. → See **Decision D3** (the literal text "hospital specific table db further divided into fiscal years" could be read as *all* tables per-FY; confirm before building).

---

## 3. Requirement → workstream traceability

| Req | What it demands | L1 Phase(s) |
|---|---|---|
| **R1** proper schema handling | introduce SQL schemas; schema-qualify all access | **L1.1** |
| **R2** master→`master` schema, others→respective schemas | full 90-table schema map; refactor migrations & all Dapper SQL | **L1.1** |
| **R3** fiscal dropdown on onboarding; per-FY billing + module/rights/page variables; dynamic module/page/assign-page-to-module RBAC | fiscal-year model, per-FY entitlements & billing, dynamic RBAC | **L1.3, L1.4, L1.7** |
| **R4** per-FY DBs auto-created (master+proc DB + per-FY data DB), zero human intervention | provisioning engine + migration runner per DB | **L1.5** |
| **R5** per-hospital domain + common domain; domain↔login mapping | tenant/domain resolver + connection routing + login realm mapping | **L1.6, L1.7** |
| **Superadmin + RBAC login** (agreed) | superadmin identity, `/auth/login`, JWT issuance, password hashing, MFA for privileged | **L1.2** |

---

## 4. Status legend
`⬜ Not Started` · `🟦 In Progress` · `🟩 Done` · `🟥 Blocked` · `🧪 In Test`

---

## 4a. L1 Build Status (live)

**Control plane provisioned and the superadmin login works end-to-end.** Solution builds clean (`dotnet build HIS.sln` → 0 errors); `HIS_Platform` migrates idempotently on MS SQL LocalDB.

Implemented & verified so far:
- **L1.0 control plane** — `HIS_Platform` DB created with **proper schemas** `platform` / `security` / `audit` (`db/platform/P0001_platform_core.sql`). Tables: `platform.Tenant` (per-tenant fiscal-year start = D1), `FiscalYear`, `TenantDomain`, `DbCatalog` (D2/D5 routing), `Subscription`, `BillingLedger`, `TenantModule`; full **dynamic RBAC** set `security.AppUser/Role/Permission/RolePermission/UserRole/AppModule/AppPage/PageAction/RoleModule/RolePage/RolePageAction`; `audit.PlatformAudit`. Runner `db/platform/run-platform-migrations.ps1`.
- **L1.1 schemas (control-plane)** — schema set defined & applied; **0 `dbo` objects** in `HIS_Platform`. (Tenant data-plane re-schema of the 90 `dbo` tables is deferred to the provisioning work, per D7 parallel-build.)
- **L1.2 superadmin + login** — PBKDF2 `PasswordHasher` (config-driven iterations), `JwtTokenIssuer` (key/issuer/audience/expiry from `Jwt:*`), `PlatformConnectionFactory` + `PlatformUserRepository`, `LoginCommand`/`LoginHandler`, `POST /api/auth/login`, and a startup `SuperAdminSeeder` (creates the superadmin from `Platform:Bootstrap:*`, hashed — no SQL-embedded password). **Closes parent tracker 0.6 (token issuance).** Verified: login → JWT with `uid/name/role/superadmin` claims (200); wrong password & unknown user → 401; empty fields → 400; every attempt written to `audit.PlatformAudit` (seed + success + 2 failures observed).

- **L1.2.6 RBAC authorization behavior** — `IAuthorizable` marker + `AuthorizationBehavior` (pipeline order Validation → **Authorization** → Logging → Audit) + `IPermissionResolver` (resolves permission codes from `security.RolePermission`). `IBranchContext` extended with `IsSuperAdmin` (set from the `superadmin` JWT claim); superadmin bypasses checks (D6). Unauthenticated → 401 (`AuthenticationException`), authenticated-but-unpermitted → 403 (`UnauthorizedAccessException`). Demonstrated on `GET /api/platform/audit` (gated by `audit.read`): no token → 401, superadmin → 200 + data, `billing.demo` (role with no grants) → 403. A config-driven dev demo user (`billing.demo`) is seeded to exercise the deny path. **Closes parent tracker 0.2 authorization.**
- **L1.3 dynamic module/page RBAC** — seeded registry `security.AppModule` (10) / `AppPage` (21) / `PageAction` (84) + role grants (`P0101`). `IRequireAuthentication` marker added. Commands: create module/page (`module.manage`), assign module/page to role (`rbac.manage`) — `PlatformController`. Effective menu `GET /api/menu` (`MenuController`, auth-only): superadmin → all modules; other roles → only granted modules/pages. **Verified end-to-end:** `billing.demo` saw only Billing; after superadmin created `telemed` module+page and assigned it to the `billing` role, `billing.demo`'s menu live-updated to Billing + Telemedicine — no deploy. **Fixed a real bug:** JwtBearer's default inbound-claim remapping renamed the `role` claim to a long URI, so role-based context/menus were empty for non-superadmins; set `MapInboundClaims=false` + explicit `RoleClaimType`/`NameClaimType`.

- **L1.5 + L1.7 automated provisioning & onboarding** — `SqlProvisioningEngine` creates per-tenant databases and applies schema templates (`db/tenant-template/master` + `…/fy`) with a `GO`-aware runner; validated DB names; `BaseConnection`/`TemplateRoot` from `Provisioning:*` config (D5). `OnboardTenantCommand` (gated `tenant.onboard`) registers tenant + fiscal year (Apr–Mar, D1) + primary/common domains, **provisions DBs before writing platform rows** (so a failure leaves no orphan tenant, L1.5.5), registers them in `platform.DbCatalog`, and enables all modules for the year. `OpenFiscalYearCommand` (gated `fiscalyear.manage`) is the year-shift: provisions the next FY's data DB. `GetTenantsQuery` lists tenants + DBs (gated `tenant.manage`). **Verified end-to-end:** onboarding `ACME` created `ACME_Master` (schemas `master`/`patient`/`proc`/`audit`; blood groups seeded; `proc.usp_NextUhid`) + `ACME_FY2026_27` (schemas `billing`/`insurance`/`hr`/`seq`/`proc`/`audit`) — the **D3 split made physical**; year-shift created `ACME_FY2027_28`; re-onboard → 409; non-privileged user → 403; both actions audited; 11 modules enabled per FY. **Bugs fixed in testing:** template path now resolved from ContentRootPath; `EXEC` needs a variable (not an inline `QUOTENAME` call); `proc` is a reserved keyword (bracket the schema); FY DB name no longer double-prefixes `FY`.

- **L1.6 tenant/domain routing + connection resolution** — `ITenantContext` + `TenantResolutionMiddleware` resolve the tenant per request from the host (own domain), a common-domain subdomain (`Tenancy:CommonDomain`), or an `X-Tenant` hint (D4), then load the tenant's master + current-FY data DB names from `platform.DbCatalog`. New `ITenantConnectionFactory` opens the correct DB (additive — the legacy `SqlConnectionFactory` is untouched until the L1.8 cutover, D7). Demonstrator `TenantController`: patients → tenant **master** DB, bills → tenant **current-FY** DB. **Verified end-to-end:** resolution via own domain / common-domain subdomain / header; onboarded a 2nd tenant (FINN); patients & bills written to and read from the correct per-tenant, per-FY databases; **tenant isolation confirmed at the DB level** (ACME_Master vs FINN_Master hold only their own rows); ACME bill numbered `BILL-FY2027-28-…` (its current FY after year-shift) vs FINN `BILL-FY2026-27-…` (per-FY billing); unresolved request → 409; legacy single-DB endpoints still 200 (no regression).

- **L1.8 cutover (started — read-only masters slice)** — expanded the tenant **master** template to the full master set (Branch/Doctor/Drug/Icd10Code/Payer/HbpPackage/BloodGroup/Tariff/Ward/Bed + patient) with reference seeds, and **cut over `LookupRepository`** (the 7 F3 master lookups: doctor/drug/icd10/payer/package/ward/tariff) from the single `dbo` DB to the resolved tenant's `master.*` schema via `ITenantConnectionFactory`. Added a config-driven **dev default tenant** (`Tenancy:DevDefaultTenant=DEV`) auto-provisioned at startup so the localhost wireframe routes to a real per-tenant DB. **Verified:** localhost lookups serve from `DEV_Master` (proven by a marker row inserted only into `DEV_Master` appearing in `/api/lookups/doctor`); the `patient` lookup intentionally stays on legacy `dbo` (part of the later write-aggregate migration); legacy endpoints (`/api/dashboard`, `/api/meta`, `/api/patients`) still 200 — **no regression**.

- **L1.8 step 1 — complete schema-split templates (done)** — all ~90 tenant tables are now faithfully reproduced across the two planes: **master DB** (`db/tenant-template/master/M0001–M0003`: `master` 21, `patient` 2, `clinical` 11, `abdm` 1 + `proc.usp_NextUhid`) and **per-fiscal-year DB** (`fy/F0001–F0005`: `billing`/`insurance`/`hr`/`diagnostics`/`pharmacy`/`inventory`/`scheme`/`occhealth`/`telemedicine`/`support`/`ai`/`compliance`/`analytics` + `seq`/`proc`/`audit`). Cross-DB references (PatientId/BranchId/StaffId/TariffId/PayerId, etc.) are plain columns (no cross-database FKs); intra-DB FKs are kept and schema-qualified. **Verified** by dropping + re-provisioning the DEV tenant: 35 master-plane tables + 45 fiscal-plane tables created correctly; lookups, the tenant write path (patient→master, bill→current-FY `billing.Bill`), and legacy endpoints all green.

- **L1.8 step 2 — data backfill (done)** — `db/tenant-template/migrate/Backfill_DEV_from_dbo.sql` relocates `HIS.dbo.*` into the provisioned DEV tenant databases with `IDENTITY_INSERT` (identity preservation, so cross-DB references remain valid as plain columns); masters/longitudinal → `DEV_Master`, fiscal-scoped → `DEV_FY2026_27`; template reference seeds are cleared first for a faithful 1:1 copy. Also expanded the template `patient.Patient` to the full `dbo.Patient` column set. **Verified:** row-count parity (Patient 6, Doctor 8, Tariff 10, Bed 12, Admission 1, PatientVisit 3, EmergencyTriage 2; FY BloodStock 8 / DrugBatch 8 / DashboardKpi 6 / QueueCounter 4) with original IDs preserved (PatientId 1 = BR1-2026-000123).

  The remaining cutover (**L1.8.5, step 3**) is the larger coordinated migration: the **write-side aggregates** (patient → clinical → billing → insurance → …) must move together with a **data backfill** because they share `PatientId`/`BranchId` references that cross databases once split — a piecemeal move would break FK consistency. Sequence: (1) finish the schema-split templates for all ~90 tables [L1.1.2–4], (2) build a `dbo → {Tenant}_Master/{Tenant}_FY` data-migration step, (3) switch the interdependent repositories to `ITenantConnectionFactory` in one coordinated change, (4) retire the single-DB `SqlConnectionFactory` and update `README.dev.md`.

Pending in these phases: tenant-role *permission* grants (platform permission set is admin-only today); per-page action assignment + tenant/FY entitlement filtering of the menu; MFA (L1.2.5); tenant-scoped login + login-realm branding (L1.7.4); login + admin UI wiring (L1.2.7 / L1.3.4 / L1.7.1); and the **write-aggregate cutover + data backfill (L1.8.3/4)** — the legacy app still runs on the single `HIS` DB except for master lookups.

> **Dev-only note:** `appsettings.Development.json` now carries a dev `Jwt:SigningKey` and a bootstrap superadmin password (`ChangeMe!2026`). Both are **dev placeholders** — prod must supply them via environment / Key Vault, and the superadmin must rotate the password on first login (future task).

---

## 5. PHASED L1 PLAN & TRACKER

### Phase L1.0 — Control-plane foundation
| # | Task | Maps to | Status | Notes |
|---|------|---------|--------|-------|
| L1.0.1 | Create `HIS_Platform` DB + `platform`/`security`/`audit` schemas | R3,R4,R5 | 🟩 | `db/platform/P0001`; idempotent, verified |
| L1.0.2 | `platform.Tenant` + `platform.DbCatalog` (tenant+FY→DB name/conn) | R4,R5 | 🟩 | DbCatalog is the routing source of truth |
| L1.0.3 | `platform.FiscalYear` (Code, StartDate, EndDate — config-driven) + per-tenant FY start on `Tenant` | R3,R4 | 🟩 | D1 baked in (FyStartMonth/Day, default Apr 1) |
| L1.0.4 | `platform.TenantDomain` (host, IsPrimary, IsCommon) + `platform.Subscription` + `platform.BillingLedger` | R3,R5 | 🟩 | tables done; per-FY billing logic pending (L1.4.2) |
| L1.0.5 | `platform.TenantModule` entitlements (tenant × fiscalYear × module, enabled flag) | R3 | 🟩 | table done; enforcement pending (L1.3.5) |

### Phase L1.1 — Schema reorganisation (R1, R2)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.1.1 | Define schema set: `master`, `patient`, `clinical`, `diagnostics`, `pharmacy`, `inventory`, `billing`, `insurance`, `scheme`, `hr`, `occhealth`, `telemedicine`, `support`, `abdm`, `ai`, `compliance`, `analytics`, `audit`, `seq`/`proc` | 🟩 | all schemas live: control-plane (`platform`/`security`/`audit`) + tenant master/FY templates provisioned & verified |
| L1.1.2 | Schema-split tenant templates for all ~90 tables (master DB vs per-FY DB) | 🟩 | `db/tenant-template/master/M0001–M0003` (35 tables) + `fy/F0001–F0005` (45 tables); cross-DB FKs dropped, intra-DB kept; verified on a fresh provision |
| L1.1.2 | Refactor `db/migrations/0001…0013` to schema-qualified `CREATE`s (split master-DB vs data-DB scripts) | ⬜ | keep idempotency |
| L1.1.3 | Update **every** Dapper query in `src/HIS.Infrastructure/Persistence/*Repositories.cs` to schema-qualified names | ⬜ | grep `dbo.` → 0 hits when done |
| L1.1.4 | Move `usp_NextUhid`/`usp_NextDocNo` to `proc`/`master`; fiscal-year-aware counters | ⬜ | ties to L1.4 |

### Phase L1.2 — Superadmin + Authentication + RBAC login
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.2.1 | Password hashing (PBKDF2) — `PasswordHasher`, config-driven iterations | 🟩 | `src/HIS.Infrastructure/Security/PasswordHasher.cs`; verified |
| L1.2.2 | `POST /api/auth/login` → validate creds, mint JWT (closes parent tracker **0.6**) | 🟩 | `JwtTokenIssuer` + `AuthController`; verified 200/401/400 |
| L1.2.3 | Seed **superadmin** user + `superadmin` role + full permission grants | 🟩 | startup `SuperAdminSeeder` (config bootstrap) + `P0100` grants all perms to superadmin |
| L1.2.4 | Seed **Permission** rows + `RolePermission` | 🟦 | platform perms + superadmin grants done; **14 tenant-role** grants pending (need L1.3 page model) |
| L1.2.5 | MFA for privileged roles (`IsPrivileged`) + AES/TLS posture (parent **0.7**) | ⬜ | |
| L1.2.6 | RBAC **authorization pipeline behavior** (parent **0.2** authz pending) | 🟩 | `IAuthorizable` + `AuthorizationBehavior` + `IPermissionResolver`; verified 401/403/200 on `GET /api/platform/audit` |
| L1.2.7 | Login UI wired in wireframe (`app/login.html` exists, currently static) | ⬜ | |

### Phase L1.3 — Dynamic Module / Page / Assignment architecture (R3)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.3.1 | `security.AppModule` (supersedes static `Module`) — superadmin CRUD | 🟩 | `POST /api/platform/modules` (gated `module.manage`); seeded 10 modules (P0101) |
| L1.3.2 | `security.AppPage` (pages within a module) + `security.PageAction` (view/create/edit/delete/print…) | 🟩 | `POST /api/platform/pages`; seeded 21 pages + 84 actions |
| L1.3.3 | Assignment tables: `RoleModule`, `RolePage`, `RolePageAction` (+ `TenantModule` from L1.0.5) | 🟩 | `POST /api/platform/assign/module|page` (gated `rbac.manage`); RolePageAction/TenantModule tables exist (action/tenant-FY assignment APIs pending) |
| L1.3.4 | Superadmin admin screens: manage modules/pages, assign to roles & tenants | 🟦 | APIs done & verified; wireframe admin UI deferred |
| L1.3.5 | Menu/registry API returns **effective** modules/pages for (user, tenant, fiscalYear) | 🟩 | `GET /api/menu` (auth-only): superadmin → all; role-scoped otherwise; tenant/FY entitlement filter pending |

### Phase L1.4 — Fiscal-year model & per-FY billing/variables (R3)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.4.1 | Fiscal-year-aware numbering (UHID/Doc counters key on FiscalYearId, not calendar year — fixes A8) | ⬜ | |
| L1.4.2 | Per-FY billing: subscription/invoice rows scoped to `TenantFiscalYear`; rollover carries balances | ⬜ | R3 "billing per fiscal year" |
| L1.4.3 | "Year shift" workflow (superadmin): open next FY → provision next-FY DB (L1.5) → migrate entitlements/rights/modules | ⬜ | R3 "on next year shift on superadmin" |
| L1.4.4 | Per-FY variability of module/rights/page (entitlements snapshot per FY) | ⬜ | R3 "module, rights, page etc." |

### Phase L1.5 — Automated multi-DB provisioning engine (R4)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.5.1 | Provisioning service: on onboarding create `{Tenant}_Master` + `{Tenant}_FY{…}` DBs programmatically | 🟩 | `SqlProvisioningEngine`; **zero human intervention** — verified ACME_Master + ACME_FY2026_27 created |
| L1.5.2 | Programmatic migration runner (apply schema-split scripts to each new DB; idempotent) | 🟩 | GO-split runner over `db/tenant-template/{master,fy}`; idempotent |
| L1.5.3 | Auto-seed masters into `{Tenant}_Master`; register DBs in `platform.DbCatalog` | 🟩 | blood groups seeded; DbCatalog rows registered + verified |
| L1.5.4 | Next-FY DB auto-creation on year shift (L1.4.3); carry-forward entitlements | 🟩 | `OpenFiscalYearCommand` → ACME_FY2027_28 created; modules enabled per FY |
| L1.5.5 | Idempotent/retry-safe + rollback on partial failure; provisioning audit | 🟦 | provision-before-write ordering avoids orphan tenants; scripts idempotent; audited. Full compensating drop of created DBs on later failure pending |

### Phase L1.6 — Tenant/domain resolution & connection routing (R4, R5)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.6.1 | `ITenantContext` (TenantId, FiscalYearId, MasterDb, DataDb) | 🟩 | `HIS.Shared/Context/ITenantContext.cs`; populated per request |
| L1.6.2 | `TenantResolutionMiddleware`: resolve tenant from **host/domain** (own domain) or **common-domain subdomain** + `X-Tenant` hint via `platform.TenantDomain`/`DbCatalog` | 🟩 | all 3 paths verified (D4) |
| L1.6.3 | Tenant-aware connection resolution from `DbCatalog` per `ITenantContext` | 🟩 | new `ITenantConnectionFactory` (additive seam); legacy `SqlConnectionFactory` untouched until L1.8 cutover (D7) |
| L1.6.4 | Per-request DB selection (master vs current-FY); cross-DB by-convention (no cross-DB FKs) | 🟩 | patients→master, bills→current-FY verified; isolation confirmed |

### Phase L1.7 — Onboarding wizard + domain↔login mapping (R3, R5)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.7.1 | Superadmin onboarding wizard with **fiscal-year dropdown** (R3) | 🟦 | `POST /api/platform/tenants/onboard` (fiscal-year selection drives provisioning) verified; wireframe wizard UI pending |
| L1.7.2 | Capture hospital profile, primary domain, common-domain alias, initial modules/roles | 🟩 | code/name/primary+common domains captured; all modules enabled per FY; richer profile fields later |
| L1.7.3 | Domain→tenant mapping (resolve tenant per host) | 🟩 | `TenantResolutionMiddleware` (L1.6) resolves host→tenant; login-realm branding per host pending |
| L1.7.4 | Tenant-scoped login (user authenticates within resolved tenant) | ⬜ | user↔tenant binding at login still pending |

### Phase L1.8 — Cutover from single `dbo` DB
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.8.1 | Schema-split tenant templates (master + per-FY) | 🟩 | **all ~90 tenant tables** split across master (35) + per-FY (45) planes; fresh provision verified (DEV_Master + DEV_FY) |
| L1.8.2 | Seed real tenants through the provisioning path | 🟩 | DEV auto-provisioned at startup; ACME/FINN via API — verified |
| L1.8.3 | Cut over repositories to `ITenantConnectionFactory` (schema-qualified) | 🟦 | **read-only repos cut over & verified by marker-row routing proofs:** `LookupRepository` → tenant master (7 lookups), `DashboardRepository` → tenant current-FY `analytics`. Write-aggregate repos = L1.8.5 big-bang |
| L1.8.4 | Data backfill `dbo` → tenant DBs (identity-preserving) | 🟩 | `migrate/Backfill_DEV_from_dbo.sql`; verified row-count parity (6 patients etc.) into DEV_Master/DEV_FY |
| L1.8.5 | Write-aggregate repository cutover (big-bang) + retire single-DB factory + docs | ⬜ | ~25 repos must switch together (dbo FKs bind all to Patient/Branch); 1 design item: pharmacy dispense is cross-plane (Drug master + DrugBatch FY) |

### Phase L1.9 — Verification, NFR & security
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.9.1 | Execute new L1 test sections in `deeptestwithdummydata.md` (§8–§13 added) | ⬜ | |
| L1.9.2 | Tenant **isolation** tests (no cross-tenant/cross-FY data bleed) | ⬜ | security-critical |
| L1.9.3 | Provisioning load/soak (onboard N tenants × M fiscal years unattended) | ⬜ | |
| L1.9.4 | Connection-routing performance under concurrency (parent **13.3**) | ⬜ | |

---

## 6. Full schema map (all 90 tables) — R2 ground truth

> Target plane: **M** = `{Tenant}_Master` DB · **F** = `{Tenant}_FY{…}` DB · **P** = `HIS_Platform` DB.
> **D3 CONFIRMED:** clinical/EMR + HR/asset *master* data is longitudinal → **M**; only financial/fiscal-scoped transactions → **F**. Rows below reflect the resolved split.

| Source migration | Table | Target schema | Plane |
|---|---|---|---|
| 0001 | Branch | `master` | M |
| 0001 | ModuleGroup, Module | → superseded by `security.AppModule/AppPage` | P |
| 0001 | Role, Permission, RolePermission, AppUser, UserRole | `security` | P |
| 0001 | AuditEntry | `audit` | F (+ `platform.PlatformAudit` for control-plane) |
| 0001 | UhidCounter (+ usp_NextUhid) | `seq`/`proc` | M (fiscal-aware per L1.4.1) |
| 0002 | Doctor, Drug, Icd10Code, Payer, HbpPackage, BloodGroup | `master` | M |
| 0002 | Ward, Bed | `master` | M |
| 0002 | DashboardKpi, ServiceActivityDaily | `analytics` | F |
| 0003 | Patient, PatientVisit | `patient` | M (longitudinal) |
| 0004 | Appointment, Encounter, Vitals, EncounterDiagnosis, Prescription, PrescriptionLine, EmergencyTriage, Admission, BedTransfer, NursingNote, OtSchedule | `clinical` | M (longitudinal EMR) |
| 0005 | LabOrder, LabResult, RadiologyOrder, BloodStock, BloodRequest | `diagnostics` | F |
| 0005 | DrugBatch, Dispense, DispenseLine | `pharmacy` | F |
| 0005 | Supplier | `master` | M |
| 0005 | PurchaseOrder, PurchaseOrderLine | `inventory` | F |
| 0005 | Asset | `inventory` | M (equipment register, longitudinal) |
| 0006 | Tariff | `master` | M |
| 0006 | Bill, BillLine, Payment, PatientDeposit | `billing` | F |
| 0007 | InsurancePolicy, Claim, ClaimEvent, ClaimDocument, SettlementReconciliation | `insurance` | F |
| 0007 | PmjayBeneficiary, PmjayCase, SchemeMembership | `scheme` | F |
| 0007 | SchemePackage | `master` | M |
| 0008 | Staff | `hr` | M (staff master, longitudinal) |
| 0008 | Attendance, DutyRoster, LeaveRequest, PayrollRun | `hr` | F |
| 0009 | CompanyContract | `master` | M |
| 0009 | MedicalExam, HazardExposure, WorkplaceInjury | `occhealth` | F |
| 0009 | TeleConsult | `telemedicine` | F |
| 0010 | Ambulance | `master` | M |
| 0010 | AmbulanceDispatch, DietOrder, WasteBag, MortuaryRecord, MlcCase, ConsentCapture, IssuedCertificate, Grievance, FeedbackSurvey, QueueCounter, QueueToken | `support` | F |
| 0010 | WasteColourCode, ConsentTemplate, CertificateTemplate | `master` | M |
| 0011 | AbdmConsent, HfrFacility, HprProfessional | `abdm` | M (HFR/HPR are facility/professional masters) |
| 0011 | AiInsight | `ai` | F |
| 0011 | ComplianceReport | `compliance` | F |
| 0012 | DocCounter (+ usp_NextDocNo) | `seq`/`proc` | F (fiscal-aware) |

---

## 7. Design Decisions — CONFIRMED (2026-06-24)

All seven forks were confirmed by the product owner. The plan is now executable; these are binding for L1.

- **D1 — Fiscal-year boundary → CONFIRMED: per-tenant configurable, default Indian FY Apr 1 → Mar 31**, stored in `platform.FiscalYear` (never hardcoded). Each tenant may override its start/end.
- **D2 — Master DB scope → CONFIRMED: per-tenant `{Tenant}_Master` DB** (one master/proc DB per hospital; strongest isolation; matches R4 literal).
- **D3 — Fiscal-scoped vs longitudinal split → CONFIRMED: only financial/fiscal-scoped data is per-FY** (billing, payments, claims, payroll, scheme cases, deposits + the per-FY counters). **Patient, PatientVisit, and clinical/EMR encounters stay longitudinal in `{Tenant}_Master`** so clinical history is continuous across years. → §6 "M or F" rows resolve to the master plane for clinical tables; see updated §6 note.
- **D4 — Common-domain disambiguation → CONFIRMED: subdomain primary, login-time fallback** (`br1.app.finnid.in`; if ambiguous, pick tenant at login). Own-domain hosting also supported.
- **D5 — DB hosting → CONFIRMED: config-driven — single instance / LocalDB for dev, Azure SQL elastic pool for prod.** Hosting model is a config switch, not code.
- **D6 — Superadmin tenancy → CONFIRMED: platform-only superadmin** in `HIS_Platform`, belonging to no tenant, separate from each tenant's own `admin` role.
- **D7 — Cutover → CONFIRMED: build L1 in parallel; the current single `HIS` DB continues for functional dev until the L1.8 cutover.**

---

## 8. Cross-cutting guardrails (carried from parent "Nothing Hardcoded")
- Fiscal boundaries, DB names, domains, connection strings, module/page/permission sets, fiscal-year rates → **config / platform tables / Key Vault**, never literals.
- All new SQL stays **Dapper parameterized**; schema names from a single constants source, not scattered strings.
- Tenant + fiscal context resolved per request, **never assumed** (extends the existing branch-context rule, A6).
- Every provisioning + onboarding + year-shift action is **audited** (extends A9 with `TenantId`/`FiscalYearId`).

---

## 9. Sequencing
- **L1.0 → L1.1** are prerequisites for everything (control plane + schemas).
- **L1.2** (superadmin/login) can proceed in parallel with L1.1 (RBAC tables already exist, A4).
- **L1.3 + L1.4** depend on L1.0 (tenant/fiscal registry).
- **L1.5 (provisioning)** depends on L1.1 (schema-split scripts) + L1.0 (catalog).
- **L1.6 (routing)** depends on L1.0 (DbCatalog/TenantDomain) + L1.5 (real DBs to route to).
- **L1.7 (onboarding wizard)** is the user-facing assembly of L1.0/L1.4/L1.5/L1.6.
- **L1.8 cutover** last; **L1.9** verifies throughout (see `deeptestwithdummydata.md` §8–§13).

---

*Decisions D1–D7 are confirmed (2026-06-24) — this plan is now executable phase by phase, starting with L1.0 (control plane) + L1.1 (schemas), with L1.2 (superadmin/login) in parallel. Update the Status column as work progresses; keep this file as the living L1 tracker alongside `developmentplancumtracker.md`.*
