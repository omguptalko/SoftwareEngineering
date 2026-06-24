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

Pending in these phases: tenant-role permission grants (depends on L1.3 module/page model), MFA for privileged roles (L1.2.5), RBAC authorization pipeline behavior (L1.2.6), login UI wiring (L1.2.7), and the tenant-DB schema refactor (L1.1.2–4).

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
| L1.1.1 | Define schema set: `master`, `patient`, `clinical`, `diagnostics`, `pharmacy`, `inventory`, `billing`, `insurance`, `scheme`, `hr`, `occhealth`, `telemedicine`, `support`, `abdm`, `ai`, `compliance`, `audit`, `seq`/`proc` | 🟦 | control-plane schemas live (`platform`/`security`/`audit`); tenant schema set defined in §6, applied during provisioning |
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
| L1.2.6 | RBAC **authorization pipeline behavior** (parent **0.2** authz pending) | ⬜ | enforce permission per command |
| L1.2.7 | Login UI wired in wireframe (`app/login.html` exists, currently static) | ⬜ | |

### Phase L1.3 — Dynamic Module / Page / Assignment architecture (R3)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.3.1 | `security.AppModule` (supersedes static `Module`) — superadmin CRUD | ⬜ | dynamic, not seed-only |
| L1.3.2 | `security.AppPage` (pages within a module) + `security.PageAction` (view/create/edit/delete/print…) | ⬜ | the "page" dimension R3 asks for |
| L1.3.3 | Assignment tables: `RoleModule`, `RolePage`, `RolePageAction` (+ `TenantModule` from L1.0.5) | ⬜ | "assign page-module" architecture |
| L1.3.4 | Superadmin admin screens: manage modules/pages, assign to roles & tenants | ⬜ | |
| L1.3.5 | Menu/registry API returns **effective** modules/pages for (user, tenant, fiscalYear) | ⬜ | replaces `/api/meta/registry` static feed |

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
| L1.5.1 | Provisioning service: on onboarding create `{Tenant}_Master` + `{Tenant}_FY{…}` DBs programmatically | ⬜ | **zero human intervention** |
| L1.5.2 | Programmatic migration runner (apply schema-split scripts to each new DB; idempotent) | ⬜ | reuse logic from `db/run-migrations.ps1` in C# |
| L1.5.3 | Auto-seed masters into `{Tenant}_Master`; register DBs in `platform.DbCatalog` | ⬜ | |
| L1.5.4 | Next-FY DB auto-creation on year shift (L1.4.3); carry-forward masters | ⬜ | |
| L1.5.5 | Idempotent/retry-safe + rollback on partial failure; provisioning audit | ⬜ | |

### Phase L1.6 — Tenant/domain resolution & connection routing (R4, R5)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.6.1 | `ITenantContext` (TenantId, FiscalYearId, MasterDb, DataDb) — extends `IBranchContext` (A6) | ⬜ | |
| L1.6.2 | `TenantResolutionMiddleware`: resolve tenant from **host/domain** (own domain) or path/subdomain (common domain) via `platform.TenantDomain` | ⬜ | R5 |
| L1.6.3 | Rework `SqlConnectionFactory` (A2) → resolve master/data connection from `DbCatalog` per `ITenantContext` | ⬜ | the single biggest infra change |
| L1.6.4 | Cross-DB query strategy (master joins vs data joins) + per-request DB selection (master vs current-FY) | ⬜ | |

### Phase L1.7 — Onboarding wizard + domain↔login mapping (R3, R5)
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.7.1 | Superadmin onboarding wizard with **fiscal-year dropdown** (R3) | ⬜ | drives L1.5 provisioning |
| L1.7.2 | Capture hospital profile, primary domain, common-domain alias, initial modules/roles | ⬜ | R5 |
| L1.7.3 | Domain→tenant→login-realm mapping (which login page/branding per host) | ⬜ | R5 "domain & login mapping" |
| L1.7.4 | Tenant-scoped login (user authenticates within resolved tenant) | ⬜ | |

### Phase L1.8 — Cutover from single `dbo` DB
| # | Task | Status | Notes |
|---|------|--------|-------|
| L1.8.1 | Treat current `HIS` (all-`dbo`) as the **reference template**; generate schema-split scripts from it | ⬜ | |
| L1.8.2 | Seed one real tenant (e.g. BR1 hospital) through the new provisioning path to validate parity | ⬜ | |
| L1.8.3 | Data backfill/migration of existing demo data into the new topology | ⬜ | |
| L1.8.4 | Deprecate single-DB path; update `README.dev.md` + `developmentplancumtracker.md` §3a | ⬜ | |

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
