# Development Plan cum Tracker
### Multi-Branch Industrial Hospital ERP System — Finnid Infotech Pvt. Ltd.
**Source of truth:** `Hospital_SRS_v2.docx` (SRS Version 2.0, Revision Date 19-06-2026)
**This plan is derived strictly from the SRS modules (Sections 1–10). No scope has been invented.**

---

## 0. Golden Rules (read before writing any code)

> ### ⛔ NOTHING IS TO BE HARDCODED
> This is a **mandatory, non-negotiable principle** for the entire project. Every value that can change across branches, payers, schemes, regulations, environments, or time **must be configuration-driven** (database master tables, `appsettings.{Environment}.json`, Azure App Configuration, Key Vault, or admin-managed settings screens). No business value belongs in C# code, MVC views, jQuery scripts, or SQL.
>
> **Specifically, the following must NEVER be hardcoded** (all are drawn from the SRS):
> - Connection strings, API keys/secrets, payment gateway keys (Razorpay/Stripe/PayU/Cashfree), ABDM/UIDAI/NHCX/PM-JAY/ESIC/CGHS/ECHS endpoints & credentials → **Azure Key Vault / config only**.
> - Branch list, branch-specific rules, bed/ward counts, OT/ICU capacities → **Branch master tables**.
> - User roles & permissions (the 14 roles in SRS §2.2) → **RBAC tables**, not enums-as-logic.
> - Scheme/package rate masters: PM-JAY HBP package codes & rates, CGHS/ECHS package rates, ESIC entitlements, State scheme rates → **Master tables, admin-editable**.
> - Payer/TPA/insurer empanelment list, co-pay %, deductibles, room-rent caps, sub-limits → **Insurance master tables** (SRS §3.15).
> - Tax/GST rates, discount rules, tariff/price lists for OPD/IPD/Lab/Pharmacy → **Tariff master**.
> - SLA/TAT timers, escalation matrices, reminder intervals (SMS/email/WhatsApp) → **Config tables**.
> - Reference data: blood groups, ICD-10 codes, waste colour-codes (BMWM Rules 2016), certificate templates, consent templates (multilingual), diet plans → **Reference/master tables**.
> - Thresholds: low-stock levels, expiry-alert windows, AMC schedules, AI model thresholds → **Config**.
> - Feature flags, environment URLs, file-storage paths, queue/counter mappings → **Config**.
>
> **Enforcement:** A static-analysis/code-review checklist item ("no magic strings/numbers, no inline secrets") gates every PR. Any literal that represents a business rule is a review blocker.

### Other guiding principles
- **CQRS + MediatR**: every use case is a `Command` (writes) or `Query` (reads) with its own handler; cross-cutting concerns (validation, logging, audit, transactions, authorization) implemented as MediatR pipeline behaviors.
- **Dapper for data access**: hand-tuned SQL in repositories; parameterized queries only (no string-concatenated SQL → blocks injection); CRUD operations standardized via a generic repository + per-aggregate repositories.
- **Configuration-driven multi-tenancy/multi-branch**: branch context resolved per request, never assumed.
- **Immutable audit trail** on every write (SRS §8.1) — implemented once as a pipeline behavior, applied everywhere.
- **Interoperability-first**: HL7/FHIR R4 DTOs are first-class for clinical + claims data (SRS §8.6, §6.2, §7.2).
- **Security & privacy by design**: DPDP Act 2023, EHR Standards 2016, ISO 27001, UIDAI masking baked into the platform layer (SRS §8.2).

---

## 1. Confirmed Technology Stack (from SRS §9, as updated)

| Layer | Technology |
|---|---|
| Programming Language | **C#** |
| Web Application (UI) | **ASP.NET Core MVC** + **jQuery**, HTML5, CSS3 |
| Backend / REST API | **ASP.NET Core Web API** |
| Application Architecture | **CQRS** (Command Query Responsibility Segregation) |
| Mediator / Messaging | **MediatR** |
| Data Access | **Dapper** (micro-ORM) with standardized **CRUD** operations |
| Database | **Microsoft SQL Server (MS SQL)** |
| Real-Time | **SignalR** (queue boards, GPS tracking, emergency alerts, dashboards) |
| AI Layer | Azure AI Services + Python ML (consumed via API) |
| Payments | Razorpay / Stripe / PayU / Cashfree APIs |
| Insurance & Claims | NHCX adapter + HL7 FHIR R4 |
| Govt. Schemes | PM-JAY (BIS/TMS), ESIC, CGHS, ECHS APIs |
| Identity | ABDM (ABHA / HFR / HPR) + UIDAI Aadhaar |
| Hosting | Microsoft Azure Cloud |

---

## 2. Proposed Solution Architecture (CQRS + MediatR + Dapper)

```
HIS.sln
├─ src/
│  ├─ HIS.Web            (ASP.NET Core MVC + jQuery — staff/admin portals, dashboards, SignalR hubs)
│  ├─ HIS.Api            (ASP.NET Core Web API — mobile/portal/integration surface, JWT)
│  ├─ HIS.Application    (CQRS: Commands, Queries, Handlers, Validators, MediatR pipeline behaviors, DTOs)
│  ├─ HIS.Domain         (Entities/aggregates, value objects, domain events, enums-as-data contracts)
│  ├─ HIS.Infrastructure (Dapper repositories, MS SQL access, external adapters: ABDM/NHCX/PM-JAY/ESIC/
│  │                       CGHS/ECHS/UIDAI/payment gateways/AI services, Key Vault, file storage)
│  └─ HIS.Shared         (Config contracts, FHIR R4 models, constants pulled FROM config, result types)
├─ db/                   (MS SQL scripts: schema, master/seed data — all reference data lives here, not in code)
└─ tests/                (unit, integration, contract, e2e — see deeptestwithdummydata.md)
```

- **Pipeline behaviors (apply globally, written once):** Validation → Authorization (RBAC) → Branch-context → Transaction/UnitOfWork → Audit-trail → Logging → Performance-timing.
- **No business rules in controllers/views** — controllers only `Send` MediatR requests.

---

## 3. Status Legend
`⬜ Not Started` · `🟦 In Progress` · `🟩 Done` · `🟥 Blocked` · `🧪 In Test`

---

## 3a. Current Build Status (live)

**Solution builds clean (`dotnet build HIS.sln` → 0 errors). DB migrations apply + re-apply idempotently on MS SQL LocalDB. API verified serving live data to the de-staticized wireframe.**

What is implemented and verified so far:
- **Phase 0 platform**: 6-project solution (`HIS.Web/Api/Application/Domain/Infrastructure/Shared`); CQRS via MediatR with **Validation → Logging → Audit** pipeline behaviors; Dapper data access (parameterized only); config-driven connection string + JWT + CORS + dev branch (nothing hardcoded); **immutable audit trail** (verified: `RegisterPatientCommand` wrote an audit row); per-request **branch context**; UHID generator proc.
- **SQL migrations (ALL phases)**: `db/migrations/0001…0013` + `db/seed/0100…0108`, runner `db/run-migrations.ps1`. Covers core platform, masters, patients, clinical, diagnostics/pharmacy/inventory/assets, billing/payments, insurance+all govt schemes, HR/payroll, occ-health/telemedicine, support/statutory, ABDM/AI/compliance.
- **Phase 1 vertical slice (live)**: Patient Registration (UHID gen + Aadhaar de-dup + validation), F3 lookups (doctor/drug/icd10/ward/payer/patient/package), patient banner + cross-branch visit history, admin dashboard KPIs/activity — all served by the Web API from MS SQL.
- **Wireframe de-staticised**: moved to `src/HIS.Api/wwwroot`; `data.js` static arrays removed → loaded from `/api/meta/registry`, `/api/lookups/{type}`, `/api/patients/default`, `/api/dashboard`; remaining un-wired module screens show honest empty-states (no fabricated rows).

How to run: see **`README.dev.md`**. Endpoints verified: `/api/health`, `/api/meta/registry` (6 groups/40 modules), `/api/lookups/doctor`, `/api/dashboard`, `/api/patients/default`, `POST /api/patients` (→ `BR1-2026-000002`).

**Phase 2 (Appointments & OPD) — done & verified:** `GET /api/appointments/slots` (16 config-driven slots), `POST /api/appointments` (→ `T-01`/`T-02`), `GET /api/appointments/queue`, `POST /api/encounters/consultation` (persisted encounter+vitals+2 diagnoses+2 Rx lines). Wireframe Appointments + OPD screens wired (slots picker, queue, booking, consultation save via F9/Save). All writes audited; validation → 400.

**Phase 2.3 (IPD) — done & verified:** `POST /api/ipd/admit` (→ `IPD-2026-000001`, bed→`occ`), double-admit → `409`, `POST /api/ipd/transfer` (old bed→`clean`, new→`occ`), `POST /api/ipd/discharge` (frees bed), `GET /api/ipd/bedboard` (live occupants). Wireframe IPD bed board + admit wired. Generic doc-number proc `usp_NextDocNo` added (migration 0012).

**Phase 3 (Diagnostics) — verified:** LIS `POST /api/lab/orders` (→ `LB-2026-000001`), `GET /api/lab/worklist`, `POST /api/lab/results` (status→`Released`) — LIS screen fully wired (new order, worklist select, result entry+release via F9). Radiology `POST/GET /api/radiology/*` (PC-PNDT flag) + Blood Bank `GET /api/bloodbank/stock` (low-threshold flags) + `POST /api/bloodbank/requests` (donor-alert when stock<units) — APIs verified. Blood-stock seeded (0102).

**Phase 4 (Pharmacy/Inventory/Assets) — verified:** `GET /api/pharmacy/queue` (from prescriptions), `GET /api/pharmacy/batches`, `POST /api/pharmacy/dispense` — **transactional**: validates batch/expiry/stock, **auto-deducts** Drug + DrugBatch stock (PARA 4210→4200), bills total ₹126, marks Rx Dispensed; insufficient-stock/unknown-batch → 409 (and the failed attempts are audited `ok=0`). `GET /api/inventory/lowstock` (NS→90≤100 flagged), `POST /api/inventory/purchase-orders` (→ `PO-2026-000001`). `GET/POST /api/assets` with AMC + maintenance-due flags. Pharmacy screen wired (queue select, dispense via F9, live low-stock alerts). Seeds: batches/suppliers/assets (0103).

**Phase 6 (Billing & Payments) — verified:** `GET /api/lookups/tariff` (master price list), `POST /api/billing/bills` (rates pulled from tariff master, computed line Amounts, gross 1150 → patientPays 300 after discount+insurance, `BILL-2026-000001`), `GET /api/billing/bills/{id}`. `POST /api/payments/collect` via `IPaymentGateway` (provider `Razorpay` **from config** — switching is config-only) — partial 100 → not settled, +200 → bill `Paid`; `POST /api/payments/deposit` (top-up balance 5000); zero-amount → 400. Billing screen wired (tariff F3, create bill via F9, collect payment). Tariff seeded (0104).

**Phase 7 (Insurance/Cashless/Schemes) — verified:** cashless engine — `POST /api/claims/policies` + `GET /api/claims/eligibility` (SI 500000, co-pay 10%), `POST /api/claims/preauth` (`CL-2026-000001`, TAT due +24h from config), `POST /api/claims/{id}/events` full lifecycle Query→Enhancement→Approval(80000)→FinalBill→Settlement(80000) → status `Settled` w/ 6 events, `GET /api/claims/{id}`, `GET /api/claims/mis` (status counts + list — fixed a ValueTuple→JSON serialization bug found in testing), `POST /api/claims/reconcile` (UTR 80000→`Matched`, 75000→`Mismatch`). PM-JAY — `POST /api/pmjay/verify` (BIS) + `POST /api/pmjay/claim` (`TMS-2026-000001`, rate 60000 from HBP master). Schemes — `POST /api/schemes/verify` (ESIC IP+Pehchan; invalid type→400) + `GET /api/schemes/packages` (CGHS/ECHS/State rate masters). Cashless + PM-JAY screens wired. Scheme packages seeded (0105). All 12 Phase-7 writes audited `ok=1`.

**Phase 8 (HR & Payroll) — verified:** `GET/POST /api/hr/staff` (add → id 6, duplicate code → 409), `POST /api/hr/attendance` (upsert: Present→Half-day single row) + `GET /api/hr/attendance`, leave request/approve. `POST /api/payroll/run` (OT 12h×₹150 + 12% PF **from config** → OT ₹1800, gross 31800, net 28200), `GET /api/payroll` monthly summary w/ totals (OT 18h/₹2700, net 51100), `POST /api/payroll/{id}/approve` (Draft→Approved; re-approve → 409); invalid month → 400. **New HR + Payroll built screens wired** (added to `HIS.screens`). Staff seeded (0106). All Phase-8 writes audited incl. failures (`ok=0`).

**Phase 9 (Occ Health & Telemedicine) — verified:** occ health — `GET/POST /api/occhealth/contracts`, `POST /api/occhealth/exams` PEME/PME (type + fitness validated → 400 on invalid), `GET /api/occhealth/exams`, injury register w/ MLC flag. Telemedicine — `POST /api/telemedicine` schedule, **TPG-2020 rule enforced**: `POST .../sign` before consent → 409 (audited `ok=0`), then `.../consent` → `.../sign` → `.../complete` → `Completed`; `GET /api/telemedicine` list. **New Occ-Health + Telemedicine built screens wired** (added to `HIS.screens`). Company contracts seeded (0107).

**Phase 10 (Support & Statutory — 9 modules) — verified:** Ambulance (dispatch nearest-available → arrive), Diet (order/list), **BMWM** (colour-coded bag; invalid colour → 409; CBWTF handover; **Form-IV** colour summary), Mortuary (admit MLC/police flags → release), **MLC** (auto `MLC-2026-000001` + police-intimation ack), Consent (multilingual en/hi templates + e-sign/thumb capture), Certificates (issue Draft → doctor-approve Issued+PDF url), Feedback (survey score 1–5 validated → 400; grievance SLA from config → resolve), Queue (counters + per-counter/day token + call-next + live board). **Hardened the API error middleware**: DB constraint violations (FK/unique) now map to 409 instead of 500. **5 new built screens wired** (ambulance, bmwm, mlc, queue, feedback); diet/mortuary/consent/certificates backend-verified via API. Reference seeds 0108 (colour codes, consent/cert templates, counters, ambulances). DB set now **12 migrations + 9 seeds**, clean idempotent apply.

**Phase 2.4 (ICU & Emergency Trauma) — verified:** `POST /api/emergency/triage` (category validated against config `Emergency:TriageCategories` → invalid `Purple` → 409; patient optional for unknown/unconscious arrivals), `GET /api/emergency/triage` (live ED board for today, **ordered by config-driven severity** Red→Yellow→Green then arrival, MLC flag shown), `POST /api/emergency/triage/disposition` (Waiting→InTreatment; invalid status → 409). Emergency admission reuses IPD admit (`AdmissionType='Emergency'`), emergency billing reuses Phase-6 billing. Triage `Status` column added (migration 0013). All writes audited incl. failures (`Succeeded=0`).

**Phase 2.5 (Nursing & Patient Care) — verified:** `POST /api/nursing/notes` (note type validated against config `Nursing:NoteTypes` = Vitals/MAR/Handover/CarePlan → invalid → 409; unknown/cross-branch admission → 409), `GET /api/nursing/admissions/{id}/notes` (per-admission notes timeline, newest-first). Backed by `NursingNote` table. All writes audited.

**Phase 5.1 (Operation Theatre) — verified:** `POST /api/ot/schedule` (resolves patient + surgeon server-side, theatre/procedure per case → `Scheduled`), `GET /api/ot/board` (scheduled + completed cases with surgeon/theatre/procedure), `POST /api/ot/complete` (records post-op notes → `Completed`; re-complete → 409). Fixed a SQL reserved-keyword bug (`Procedure` alias must be bracketed) found in testing. Backed by `OtSchedule` table. All writes audited. **DB set now 13 migrations + 9 seeds.**

**L1 SaaS re-platform (tracked in `L1EnhancementDevPlanCumTracker.md`) — data-plane cutover complete (2026-06-25):** the single `HIS`/`dbo` database + `SqlConnectionFactory` are **retired**. Every repository now routes through `ITenantConnectionFactory` to a per-tenant `{Tenant}_Master` DB (longitudinal: patient/clinical/masters/audit) or per-fiscal-year `{Tenant}_FY{…}` DB (billing/insurance/HR/pharmacy/…), schema-organised, auto-provisioned on onboarding. Verified end-to-end on the DEV tenant (patient register + FY-numbered billing across the two planes). See the L1 tracker for the full multi-tenant / RBAC / provisioning workstream and its remaining UI/MFA items.

Remaining: SignalR hubs (0.9), FHIR R4 adapters (0.10), AI modules (Phase 11), Admin dashboards/compliance (Phase 12), non-functional hardening (Phase 13); plus external integrations (ABDM/NHCX/PM-JAY/ESIC/CGHS/ECHS/UIDAI/gateways/AI) and remaining per-module sub-features noted in the trackers below.

---

## 4. PHASED DEVELOPMENT PLAN & TRACKER

### Phase 0 — Foundation & Cross-Cutting Platform
| # | Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 0.1 | Solution scaffolding (Web/Api/Application/Domain/Infrastructure/Shared) | §9 | 🟩 | | 6 projects, builds clean |
| 0.2 | MediatR + CQRS pipeline behaviors (validation, auth, audit, txn, logging) | §8.1 | 🟩 | | Validation+**Authorization (RBAC, L1.2.6)**+Logging+Audit done; txn/UnitOfWork pending |
| 0.3 | Dapper data-access base (generic CRUD repo, parameterized queries, UnitOfWork) | §9 | 🟩 | | ConnectionFactory + repos; UnitOfWork pending |
| 0.4 | MS SQL schema baseline + migrations + **all reference/master tables seeded from scripts** | §9 | 🟩 | | 0001–0011 + seed, idempotent, verified |
| 0.5 | Config & secrets via appsettings + Azure App Config + **Key Vault** (no inline secrets) | §8.1/§8.2 | 🟦 | | Config-driven done; Key Vault wiring pending |
| 0.6 | JWT auth + **RBAC** (14 roles, permission tables) | §2.2/§8.1 | 🟦 | | JWT + role/permission tables done; **token issuance done via `POST /api/auth/login`** (L1.2, control plane); per-command RBAC authz behavior pending |
| 0.7 | AES-256 at rest, TLS in transit, MFA for privileged roles, Aadhaar/PII masking | §8.1/§8.2 | 🟦 | | Aadhaar stored masked; AES/MFA pending |
| 0.8 | Immutable audit-trail behavior (all writes) | §8.1/§3.22 | 🟩 | | Verified writing rows |
| 0.9 | SignalR infrastructure (hubs, groups per branch) | §9 | ⬜ | | |
| 0.10 | FHIR R4 model library + HL7 interoperability scaffolding | §8.6 | ⬜ | | |
| 0.11 | Multi-branch context resolver + branch master | §3.21 | 🟩 | | Middleware + Branch master/seed |

### Phase 1 — Core Platform & Identity
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 1.1 | Patient Registration & UHID (unique UHID, demographics, cross-branch history) | §3.1 | 🟩 | | API + UI live; UHID gen + visit history verified |
| 1.2 | Aadhaar Identity (UIDAI OTP verify, encryption, masking, de-dup) | §6.1 | 🟦 | | Masked storage + de-dup done; UIDAI OTP pending |
| 1.3 | ABHA & ABDM Stack (ABHA create/link, Scan & Share, HFR, HPR, consent HIE, EHR 2016/FHIR R4) | §6.2 | 🟦 | | Schema (AbdmConsent/HFR/HPR) done; adapters pending |
| 1.4 | Multi-Branch Synchronization (centralised DB, unified EMR, transfer workflow) | §3.21 | 🟩 | | Branch context + cross-branch UHID/visits |
| 1.5 | Master data admin screens (branches, departments, roles, tariffs, reference data) | §9/§2.2 | 🟦 | | Masters seeded + lookup APIs; admin CRUD UI pending |

### Phase 2 — Clinical Core (OPD / IPD / ICU / Emergency)
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 2.1 | Appointment & Token Management (online/offline, doctor-wise, SMS/email/WhatsApp reminders) | §3.2 | 🟩 | | Book + token + slots (config hours) + queue live & verified; reminders pending |
| 2.2 | OPD Consultation (diagnosis, digital prescription, referrals, follow-up) | §3.3 | 🟩 | | Save consultation (encounter+vitals+dx+Rx) live & verified |
| 2.3 | IPD Admission & Ward Management (bed allocation, transfer, nursing notes, discharge summary) | §3.4 | 🟩 | | Admit (AdmissionNo gen + bed occupy) / transfer / discharge / live bed board — verified; nursing notes pending |
| 2.4 | ICU & Emergency Trauma (triage, emergency admission, ICU monitoring, emergency billing) | §3.5 | 🟩 | | Triage register (category from config→409) + severity-ordered ED board + disposition — verified (API). Emergency admit reuses IPD admit; emergency billing reuses Phase-6 |
| 2.5 | Nursing & Patient Care (vitals, MAR, shift handover, diet, care plans) | §3.13 | 🟩 | | Nursing note (type from config: Vitals/MAR/Handover/CarePlan→409) + per-admission timeline — verified (API). Diet via §3.26 (Phase 5.2) |

### Phase 3 — Diagnostics
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 3.1 | Laboratory Information System (test request, barcode samples, auto report, portal upload) | §3.8 | 🟩 | | Order+barcode / worklist / result entry+release — verified; UI wired |
| 3.2 | Radiology & Imaging (X-Ray/MRI/CT scheduling, report upload, doctor review; PC-PNDT controls) | §3.9/§10 | 🟦 | | Order + worklist + PC-PNDT flag (API verified); report upload + screen pending |
| 3.3 | Blood Bank Management (group-wise inventory, emergency request, donor alerts, branch transfer) | §3.7 | 🟦 | | Stock + request + donor-alert (API verified); branch transfer + screen pending |

### Phase 4 — Pharmacy, Inventory & Assets
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 4.1 | Pharmacy Management (batch/expiry dispensing, stock deduction, pharmacy billing; D&C/NDPS register) | §3.10/§10 | 🟩 | | Queue/batches/dispense w/ atomic stock deduction + expiry/stock validation (config block-days) + NDPS flag — verified; UI wired |
| 4.2 | Inventory & Store (low-stock alerts, PO generation, supplier, branch transfer) | §3.11 | 🟩 | | Low-stock + PO (PO-2026-000001) — verified; branch transfer pending |
| 4.3 | Asset & Equipment (ventilators/MRI/monitors tracking, maintenance, AMC, breakdown alerts) | §3.19 | 🟩 | | List w/ AMC+maintenance-due flags + register — verified (API) |

### Phase 5 — Surgical, Ward-Support & Statutory Clinical Modules
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 5.1 | Operation Theatre Management (surgery scheduling, resource allocation, post-op notes) | §3.12 | 🟩 | | Schedule surgery (patient+surgeon resolved) + OT board + complete w/ post-op notes (re-complete→409) — verified (API). Resource/anaesthesia checklist pending |
| 5.2 | Diet & Kitchen Management (therapeutic/routine diets, ward indents, costing → IPD billing) | §3.26 | 🟩 | | Diet order + list (per-admission) — verified (API). Kitchen indents pending |
| 5.3 | Bio-Medical Waste Management (colour-coded categories, barcoded bags, Form-IV, CBWTF handover) | §3.25/§10 | 🟩 | | Bag generate (colour FK→409) + handover + Form-IV summary — verified; screen wired |
| 5.4 | Mortuary & Death Management (body register, storage, release, death cert, police/MLC intimation) | §3.27 | 🟩 | | Admit (MLC/police flags) + release + register — verified (API) |
| 5.5 | Medico-Legal Case (MLC) Management (auto MLC no., police intimation, chain-of-custody) | §3.28 | 🟩 | | Auto MLC no. (MLC-2026-000001) + police intimation(ack) + register — verified; screen wired |
| 5.6 | Ambulance Management & GPS Tracking (call logging, nearest dispatch, live GPS via SignalR) | §3.6 | 🟩 | | Dispatch nearest-available + arrive + list — verified; screen wired. Live GPS/SignalR pending |

### Phase 6 — Billing & Revenue Cycle
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 6.1 | Billing & RCM (integrated OPD/IPD/Lab/Pharmacy, multi-mode, invoice, refund, discount) | §3.14 | 🟩 | | Tariff F3 + create bill (rates from master, computed amounts, BILL-2026-000001) + get bill — verified; UI wired. Refund pending |
| 6.2 | Payment Gateway (UPI/Card/NetBanking/QR; Razorpay/Stripe/PayU/Cashfree; receipts, refunds, deposits) | §5 | 🟩 | | IPaymentGateway abstraction (provider from config), collect (partial→settle), deposit top-up — verified; real adapters/refunds pending |

### Phase 7 — Insurance, Cashless & Government Schemes
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 7.1 | Insurance/TPA/Cashless engine (policy capture, eligibility, pre-auth, enhancement, query/shortfall, final bill, denial/appeals, empanelment master) | §3.15/§7.1 | 🟩 | | Policy/eligibility/pre-auth (CL-…, TAT from config)/full lifecycle events/get claim — verified; UI wired. Sub-limit auto-calc pending |
| 7.2 | NHCX adapter (FHIR-based eligibility/pre-auth/claim/payment-notice; single gateway) | §7.2 | 🟦 | | Claim channel='NHCX' routed; FHIR R4 message adapter pending |
| 7.3 | AB PM-JAY (BIS verify, e-card, HBP packages, TMS submission, Ayushman Mitra desk, Aadhaar discharge verify, anti-fraud) | §7.3 | 🟩 | | BIS verify + HBP package (rate from master) + TMS case (TMS-2026-000001) — verified; UI wired. Aadhaar-discharge/anti-fraud pending |
| 7.4 | ESIC (IP/Pehchan verify, dependents, SST referral, IMP, e-bill, settlement) | §7.4 | 🟦 | | Membership verify (IP+Pehchan) — verified; SST/IMP/e-bill pending |
| 7.5 | CGHS (beneficiary verify, package rate master, credit billing, permission-letter) | §7.5 | 🟦 | | Membership verify + CGHS package master — verified; credit billing pending |
| 7.6 | ECHS (card/referral verify, BPA online billing, emergency/claim) | §7.6 | 🟦 | | Membership verify + ECHS package master — verified; BPA billing pending |
| 7.7 | State Government Schemes (configurable scheme master, verification, coordination-of-benefits) | §7.7 | 🟦 | | Membership verify + State package master — verified; coordination-of-benefits pending |
| 7.8 | Scheme Reconciliation & Claims MIS (dashboards, ageing, UTR auto-reconcile, leakage/denial analytics) | §7.8 | 🟩 | | Status-count MIS + UTR reconcile (matched/mismatch) — verified; ageing/leakage analytics pending |
| 7.9 | Payer Coverage Matrix implementation (verification + claim channel routing) | §7.9 | 🟦 | | Payer type + channel routing in place; full matrix UI pending |

### Phase 8 — HR & Payroll
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 8.1 | HR Management (staff master, attendance, duty-roster, leave) | §3.17 | 🟩 | | Staff add/list (dup→409), attendance upsert, leave request/approve — verified; HR screen wired. Duty-roster pending |
| 8.2 | Payroll & Overtime (salary processing, overtime logging, supervisor approval, slips, monthly summaries) | §3.18 | 🟩 | | Run (OT rate + PF% from config: net 28200), monthly summary w/ totals, supervisor approve (re-approve→409) — verified; Payroll screen wired. Slip PDF pending |

### Phase 9 — Occupational Health & Telemedicine
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 9.1 | Occupational Health & Industrial Medicine (PEME/PME per Factories Act 1948, fitness cert, disease/hazard tracking, audiometry/spirometry/vision/vaccination, injury register→MLC, corporate billing, ESIC linkage) | §3.23/§10 | 🟩 | | Company contracts + PEME/PME exam (fitness/audiometry/spirometry/vision; type+fitness validated→400) + hazard + injury register(MLC flag) — verified; screen wired. Corporate billing pending |
| 9.2 | Telemedicine & Teleconsultation (secure video/audio per TPG 2020, cross-branch, e-Rx w/ digital signature, consent, audit, Tele-ICU/tele-radiology) | §3.24 | 🟩 | | Schedule + consent + e-Rx sign (TPG-2020 consent-before-sign enforced→409) + complete + list — verified; screen wired |

### Phase 10 — Supporting & Experience Modules
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 10.1 | Certificate & Document Management (Birth/Death/Referral/Fitness/Medical/Discharge; approval workflow → PDF) | §3.16 | 🟩 | | Templates + issue(Draft)→doctor-approve(Issued+PDF url) — verified (API). PDF render pending |
| 10.2 | Consent & e-Document Management (digital consent, e-sign/thumb, multilingual templates, versioning, audit) | §3.29 | 🟩 | | Multilingual templates (en/hi) + capture (e-sign/thumb) — verified (API) |
| 10.3 | Feedback, Grievance & Patient Experience (NABH surveys, grievance SLA/TAT, escalation, analytics) | §3.30 | 🟩 | | Survey (score 1–5 validated→400) + grievance (SLA from config) → resolve(TAT) — verified; screen wired |
| 10.4 | Queue & Digital Signage (token queues OPD/pharmacy/billing, real-time boards via SignalR, load-balancing) | §3.31 | 🟩 | | Counters + issue token (per-counter/day) + call-next + live board — verified; screen wired. SignalR push pending |

### Phase 11 — AI Modules (Azure AI + Python ML, consumed via API)
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 11.1 | AI Patient Risk Prediction (vitals + history) | §4.1 | ⬜ | | Thresholds configurable |
| 11.2 | AI Smart Scheduling (slot optimisation) | §4.2 | ⬜ | | |
| 11.3 | AI Chatbot Support (24x7 appointments/reports/reminders/emergency) | §4.3 | ⬜ | | |
| 11.4 | AI Inventory Forecasting (demand predict + auto-reorder) | §4.4 | ⬜ | | |
| 11.5 | AI Fraud Detection (billing anomalies, insurance/scheme fraud) | §4.5 | ⬜ | | |
| 11.6 | AI Claim Pre-Scrubbing (validate pre-auth/claim vs payer/package rules) | §4.6 | ⬜ | | |

### Phase 12 — Dashboards, Compliance & Audit
| # | Module / Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 12.1 | Admin Dashboard & Analytics (patient counts, emergency stats, revenue, inventory alerts, OT cost) | §3.20 | ⬜ | | |
| 12.2 | Compliance & Audit (NABH reporting, audit logs all actions, govt reporting automation, data-security compliance) | §3.22 | ⬜ | | |
| 12.3 | Regulatory & Compliance Framework mapping (Clinical Establishments Act, NABH/NABL, BMWM 2016, D&C/NDPS, PC-PNDT, Factories Act 1948, ESI Act 1948, PM-JAY/NHA, IRDAI, DPDP/IT Act, ABDM/EHR 2016) | §10 | ⬜ | | |

### Phase 13 — Non-Functional Hardening & Release
| # | Task | SRS Ref | Status | Owner | Notes |
|---|------|---------|--------|-------|-------|
| 13.1 | Security pass (JWT/RBAC/AES-256/MFA/masking/audit) penetration-tested | §8.1 | ⬜ | | |
| 13.2 | Privacy & regulatory compliance verification (DPDP 2023, IT Act/SPDI, EHR 2016, ISO 27001, UIDAI vault) | §8.2 | ⬜ | | |
| 13.3 | Performance: **1000+ concurrent users**, instant emergency/triage response | §8.3 | ⬜ | | Load-tested |
| 13.4 | Scalability: horizontal scale, easy new-branch onboarding | §8.4 | ⬜ | | |
| 13.5 | Reliability: daily backup, DR, **99.9% uptime** | §8.5 | ⬜ | | |
| 13.6 | Interoperability: HL7/FHIR R4, NHCX, ABDM HIP/HIU compliance | §8.6 | ⬜ | | |
| 13.7 | Full deep test execution (see `deeptestwithdummydata.md`) | all | ⬜ | | |
| 13.8 | Azure Cloud deployment + CI/CD | §9 | ⬜ | | Env values per-environment config |

---

## 5. Cross-Cutting "Nothing Hardcoded" Checklist (PR gate)
- [ ] No connection strings/secrets/keys in source or config-committed-to-git → Key Vault only.
- [ ] All scheme/package/tariff/co-pay/cap/sub-limit values in master tables (admin-editable).
- [ ] All endpoints/URLs (ABDM, NHCX, PM-JAY, ESIC, CGHS, ECHS, UIDAI, gateways, AI) from config.
- [ ] Roles/permissions in RBAC tables, not branched logic.
- [ ] Reference data (ICD-10, blood groups, waste colour codes, certificate/consent templates, diet plans) in DB.
- [ ] Thresholds/SLAs/TATs/reminder intervals/feature flags in config.
- [ ] No string-concatenated SQL — Dapper parameterized queries only.
- [ ] Branch-specific behaviour resolved from branch context/master, never assumed.

---

## 6. Dependency / Sequencing Notes
- Phase 0 + 1 are prerequisites for everything (auth, audit, branch context, patient/UHID, ABDM identity).
- Billing (Phase 6) precedes Insurance/Schemes (Phase 7) — claims build on final bills.
- AI modules (Phase 11) depend on data from clinical/billing/inventory modules being live.
- Interoperability (FHIR/NHCX/ABDM) scaffolding in Phase 0 is reused by Phases 1, 7.
- Compliance/Audit (Phase 12) consumes the audit-trail behavior shipped in Phase 0.

---

*Update the Status column as work progresses. Keep this file as the single living tracker for the build.*
