# Deep functional smoke test

`deeptest.js` drives the **running** Finnid HIS ERP over HTTP with the fictitious
dummy data from [`../../deeptestwithdummydata.md`](../../deeptestwithdummydata.md)
and prints a PASS / FAIL / SKIP matrix per module (one happy-path API test per
SRS module, plus auth/RBAC and FHIR/AI/compliance).

It is an **API-level integration smoke test**, complementary to the unit/integration
suites described in the deep-test plan — fast feedback that every implemented
endpoint works end-to-end against a real tenant database.

## Run

```bash
# 1. start the app (provisions the DEV tenant on first run — see ../../README.dev.md)
dotnet run --project src/HIS.Api

# 2. in another terminal (Node 18+ for global fetch)
node tests/smoke/deeptest.js
```

Exit code is `0` when all tests pass, `1` if any FAIL, `2` on a harness error —
so it can gate CI.

## Config (env vars, dev defaults shown)

| Var | Default |
|---|---|
| `HIS_BASE` | `http://localhost:5142` |
| `HIS_SU_USER` / `HIS_SU_PASS` | `superadmin` / `ChangeMe!2026` |
| `HIS_DEMO_USER` / `HIS_DEMO_PASS` | `billing.demo` / `Demo!2026` |

## Scope

- **Covered (happy path + auth/RBAC):** registration, appointments, OPD, IPD,
  nursing, diet, emergency, OT, LIS, radiology, blood bank, pharmacy, inventory,
  assets, billing, payments, insurance/cashless, PM-JAY, schemes, HR, payroll,
  occupational health, telemedicine, BMWM, MLC, queue, feedback, ambulance+GPS,
  AI (risk/forecast/pre-scrub), dashboard, audit, FHIR R4 export.
- **SKIP (need external services/credentials):** UIDAI OTP, ABDM/ABHA HIE, NHCX,
  real payment gateways, Azure-ML AI (chatbot/scheduling/fraud), 1000-user load,
  backup/DR. These are integration points, not gaps in the app logic.

Each write the harness performs is audited, tenant-scoped and fiscal-year-numbered
server-side (e.g. `PO-FY2026-27-…`, `MLC-FY2026-27-…`).
