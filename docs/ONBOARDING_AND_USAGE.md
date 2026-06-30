# Onboarding a New Hospital & Using the Platform

This is a SaaS, multi-tenant Hospital ERP. One running platform serves many
hospitals; each hospital ("tenant") gets its **own databases**, its **own
fiscal-year data**, its **own domain**, and **role-scoped logins** — all
provisioned automatically.

---

## 1. Key concepts (read this first)

| Term | Meaning |
|---|---|
| **Platform / control plane** | The single shared `HIS_Platform` database + the superadmin who runs the SaaS. It holds the list of tenants, fiscal years, domains, RBAC and billing. |
| **Tenant = one Hospital** | e.g. "ACME Multispeciality". Gets a `{CODE}_Master` DB (longitudinal: patients, clinical history, masters) + one `{CODE}_FY{YYYY-YY}` DB per fiscal year (billing, claims, payroll, pharmacy…). |
| **Branch** | A physical unit *inside* a hospital (e.g. Lucknow, Kanpur). Branches live inside the tenant's DBs — they are **not** separate tenants. |
| **Superadmin** | Platform owner. Belongs to **no** tenant. Onboards hospitals, manages modules/roles, runs year-shifts. |
| **Tenant users** | A hospital's own staff (admin, doctor, nurse, receptionist, billing…). They can log in **only** to their own hospital. |

**Why two DBs per tenant:** clinical history must stay continuous across years
(it never splits), while money (bills, claims, payroll) is fiscal-year-scoped.
So patient/clinical data → `_Master`, financial data → `_FY{year}`.

---

## 2. One-time platform setup (developer / first run)

```bash
# Prereqs: .NET 9 SDK, MS SQL (LocalDB or a server), sqlcmd, Node 18+ (for tests)

# 1. Create the control-plane DB + seed it
pwsh db/platform/run-platform-migrations.ps1     # creates HIS_Platform

# 2. Run the API (also serves the web UI; auto-provisions a dev tenant "DEV")
dotnet run --project src/HIS.Api
#   → open the printed URL, e.g. http://localhost:5142
```

On first boot the app creates the **superadmin** from config
(`Platform:Bootstrap:*`) and a demo `DEV` tenant. Production supplies the
connection strings / JWT key / bootstrap password via environment or Key Vault.

Superadmin login (dev): `superadmin` / `ChangeMe!2026` → **Platform Admin** link
in the title bar opens the console (`/app/admin.html`).

---

## 3. Add a NEW hospital — the onboarding process

> Done by the **superadmin**, in the browser, in ~2 seconds, with **zero manual
> DB work**.

### Steps
1. Sign in as **superadmin** → click **Platform Admin** → **Onboard hospital**.
2. Fill the wizard:
   - **Tenant code** — short id, letters/digits/`-` (e.g. `ACME`). Becomes the DB name prefix.
   - **Hospital name** — display name (e.g. `ACME Multispeciality`).
   - **Fiscal year** — dropdown (e.g. `FY 2026-27`). Indian FY (Apr→Mar) by default; configurable per tenant.
   - **Primary domain** — the hospital's own URL (e.g. `acme.hospital.in`).
   - **Common-domain alias** *(optional)* — shared-domain access (e.g. `acme.app.finnid.in`).
3. Click **Provision & onboard**.

### What happens automatically (verified end-to-end)
`POST /api/platform/tenants/onboard` →
1. **Provisions the databases first** (the only failure-prone step): creates
   `{CODE}_Master` and `{CODE}_FY{YYYY-YY}`, applies the schema-split templates,
   seeds reference masters.
2. **Registers the tenant**, the fiscal year (start/end), and the **domains**
   (primary + common) for request routing.
3. **Registers the DBs** in `platform.DbCatalog` (the routing source of truth).
4. **Enables all modules** for that tenant × fiscal year.
5. **Writes billing**: a per-FY `Subscription` + a ledger charge.
6. **Audits** the action. If any step fails, it **rolls back** (no orphan
   tenant/DBs).

### Verify
- The **Tenants** table now lists the hospital with its `{CODE}_Master` +
  `{CODE}_FY…` rows.
- (DB level) `{CODE}_Master` and `{CODE}_FY…` exist in SQL Server with the
  schema split (`master`, `patient`, `clinical`, `billing`, `insurance`, …).

---

## 4. Set up the hospital after onboarding

| What | How (today) | Status |
|---|---|---|
| **Create + manage the hospital's login users** (admin, doctors, nurses…) | Platform Admin → **Users**: create (tenant + id + password + display name + role), and per-user **Edit** (name/email), **Change role**, **Reset password**, **Deactivate/Activate**. Each user is bound to its tenant (`AppUser.TenantId`), PBKDF2-hashed, logs in **only** to that hospital; a deactivated user cannot log in; a role change takes effect on next login. Backed by `/api/platform/tenants/users[/update|/change-role|/set-active|/reset-password]` + `/roles` + `/tenants/{code}/users`, gated by `users.manage`. | ✅ live |
| **Tenant-admin self-service** | A hospital's own **admin** (role `admin`, which holds `users.manage`) sees a **"Manage Users"** link in the workspace → the same Users screen, **locked to its own tenant** (other tabs hidden, tenant code read-only). The scope is enforced **server-side** from the JWT `tenantId` claim — a tenant admin cannot touch another tenant's users (403), onboard, year-shift, or list all tenants. Superadmin keeps the full console. | ✅ live |
| **Assign modules / pages to roles** (RBAC) | Platform Admin → **Modules & Roles** → assign module/page/action to a role. The sidebar + APIs scope to these grants. | ✅ live |
| **Enable/disable a module per fiscal year** | Platform Admin → **Entitlements** → toggle a module for tenant × FY. The effective menu honours it. | ✅ live |
| **Point the domain** | DNS: point `acme.hospital.in` (and/or the common subdomain) at the app. The platform resolves host → tenant automatically. | ops |

---

## 5. Daily use (hospital staff)

1. Staff open the **hospital's domain** (own domain or common-domain subdomain).
   The platform resolves the host → the correct tenant + current fiscal year.
2. They **log in** with their tenant credentials (role-scoped). The sidebar shows
   only the modules their role grants.
3. Typical patient journey, all writes audited + tenant-scoped + FY-numbered:
   **Register patient (UHID)** → **Appointment/token** → **OPD consult / IPD admit**
   → **Lab / Radiology / Pharmacy** → **Billing & payment** → **Insurance/scheme
   claim** → **Discharge / certificate**.
4. Real-time boards (queue, emergency alerts, ambulance GPS) update live and are
   isolated per tenant.

---

## 6. Yearly: the "year shift"

At each fiscal-year change, the superadmin opens the next year (no downtime,
history intact):

1. Platform Admin → **Open next fiscal year (year shift)** → enter the tenant
   code + pick the next fiscal year → **Open fiscal year**.
2. `POST /api/platform/fiscal-years/open` →
   - **Provisions the next `{CODE}_FY{next}` data DB**.
   - **Carries module entitlements forward** (a disabled module stays disabled).
   - **Rolls billing forward** (a `CarryForward` of the prior balance + new charge).
3. Clinical/patient data stays in the same `{CODE}_Master` — it never splits.

---

## 7. Decommission a tenant (cleanup)

**Self-service (superadmin):** Platform Admin → **Tenants** → the red **Decommission
a tenant** card. Enter the tenant code, re-type it to confirm, and **Decommission**.
`POST /api/platform/tenants/decommission` (gated `tenant.manage`, requires
`Confirm == TenantCode`) **drops all the tenant's databases first**, then deletes its
control-plane rows + users (FK order); the immutable `audit.PlatformAudit` trail is
retained. Tenant admins cannot do this (403). Verified: onboard → decommission drops
both DBs, zero platform rows remain, audit kept.

**Manual fallback (DBA)** — the equivalent SQL, if ever needed:

```sql
-- 1. Drop the tenant's databases (master + every FY)
ALTER DATABASE [ACME_FY2026_27] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [ACME_FY2026_27];   -- repeat for each FY + _Master

-- 2. Remove platform rows (HIS_Platform), child-first; keep audit history
DECLARE @t INT = (SELECT TenantId FROM platform.Tenant WHERE Code='ACME');
DELETE platform.TenantModule  WHERE TenantId=@t;
DELETE platform.Subscription  WHERE TenantId=@t;
DELETE platform.BillingLedger WHERE TenantId=@t;
DELETE platform.DbCatalog     WHERE TenantId=@t;
DELETE platform.TenantDomain  WHERE TenantId=@t;
DELETE platform.FiscalYear    WHERE TenantId=@t;
DELETE platform.Tenant        WHERE TenantId=@t;
-- audit.PlatformAudit rows are intentionally retained (immutable trail).
```

---

## 8. Honest status — what's live vs. pending

**Live & verified:** automated onboarding (DBs/schemas/domains/billing/modules),
year-shift, per-tenant DB isolation, domain→tenant routing, RBAC-scoped menu,
MFA + AES-at-rest, audit trail, the full clinical/financial module set, real-time
(queue/alerts/GPS), AI assists (risk/forecast/pre-scrub), FHIR R4 export.

**Pending (needs work before a real hospital go-live):**
- Real external integrations and Azure-ML AI (scaffolded/mocked — see below).
- Real external integrations (UIDAI/ABDM/NHCX/PM-JAY/ESIC/CGHS/ECHS/payment
  gateways) and real Azure-ML AI — these are scaffolded/mocked.
- Production TLS termination + secret sourcing (Key Vault) and a DR/backup plan.

See `developmentplancumtracker.md` and `L1EnhancementDevPlanCumTracker.md` for the
full status of every item.
