# Deep Test Plan with Dummy Data
### Multi-Branch Industrial Hospital ERP System — Finnid Infotech Pvt. Ltd.
**Source of truth:** `Hospital_SRS_v2.docx` (SRS v2.0). Every test below maps to a real SRS module/section. No requirement is invented.
**All data here is FICTITIOUS** — invalid/placeholder IDs for testing only. Never use real Aadhaar/ABHA/policy numbers.

---

## 0. Test Strategy & Levels
| Level | Scope | Tooling |
|---|---|---|
| Unit | CQRS Command/Query handlers, validators, domain rules | xUnit + Moq |
| Integration | Dapper repositories against MS SQL (test DB), MediatR pipeline | xUnit + Testcontainers/LocalDB |
| Contract | External adapters: ABDM, NHCX, PM-JAY (BIS/TMS), ESIC, CGHS, ECHS, UIDAI, payment gateways (sandbox/mocked) | WireMock |
| API | ASP.NET Core Web API endpoints, JWT/RBAC | Postman/Newman |
| UI | ASP.NET Core MVC + jQuery flows | Selenium/Playwright |
| Real-time | SignalR (queue, GPS, alerts, dashboards) | SignalR test client |
| E2E | Full patient journeys & claim lifecycles | Playwright |
| NFR | Security, performance (1000+ users), reliability, interoperability | k6/JMeter, OWASP ZAP |

> **Config-driven testing rule (mirrors dev plan):** tests must read scheme rates, tariffs, caps, endpoints, and roles from **seeded master/config tables** — never assert against hardcoded business values. If a test needs a package rate, it seeds it; it does not assume one.

---

## 1. Master / Reference Dummy Data (seed once for the whole suite)

### 1.1 Branches (SRS §3.21)
| BranchId | Name | City | Type |
|---|---|---|---|
| BR-001 | Finnid Industrial Hospital — Lucknow | Lucknow | Multi-specialty + Occupational |
| BR-002 | Finnid Industrial Hospital — Kanpur | Kanpur | Multi-specialty |
| BR-003 | Finnid Clinic — Unnao (Factory tie-up) | Unnao | Occupational/OPD |

### 1.2 Users — one per SRS §2.2 role (14 roles)
| UserId | Name | Role | Branch |
|---|---|---|---|
| U-ADMIN | Asha Verma | Admin | BR-001 |
| U-DOC1 | Dr. Rakesh Nigam | Doctor | BR-001 |
| U-NURSE1 | Sunita Yadav | Nurse | BR-001 |
| U-RECEP1 | Pooja Singh | Receptionist | BR-001 |
| U-LAB1 | Imran Khan | Lab Technician | BR-001 |
| U-PHARM1 | Neha Gupta | Pharmacist | BR-001 |
| U-BILL1 | Ramesh Tiwari | Billing Staff | BR-001 |
| U-TPA1 | Kavita Rao | TPA / Insurance Desk Officer | BR-001 |
| U-MITRA1 | Suresh Kumar | Ayushman Mitra / PMAM | BR-001 |
| U-OHO1 | Dr. Meena Joshi | Occupational Health / Factory Medical Officer | BR-003 |
| U-TELE1 | Anil Mishra | Telemedicine Coordinator | BR-002 |
| U-HR1 | Deepak Sharma | HR Manager | BR-001 |
| U-AMB1 | Vijay Pal | Ambulance Driver | BR-001 |
| U-PAT1 | (patient login) | Patient | — |

### 1.3 Reference data seeds (must come from tables, NOT code)
- **Blood groups:** A+, A-, B+, B-, AB+, AB-, O+, O- (SRS §3.7)
- **BMWM colour codes (Rules 2016):** Yellow, Red, White (puncture-proof), Blue (SRS §3.25)
- **ICD-10 sample:** `J45.9` Asthma, `S52.5` Wrist fracture, `I10` Hypertension (SRS §7.1)
- **PM-JAY HBP package (dummy):** `HBP-CARD-001` Coronary Angiography, rate ₹12,000 (SRS §7.3)
- **CGHS package (dummy):** `CGHS-OPD-001`, rate ₹350 (SRS §7.5)
- **Tariff master (dummy):** OPD consult ₹500; CBC ₹250; X-Ray chest ₹400; General ward/day ₹2,000 (SRS §3.14)
- **Insurance caps (dummy):** Room-rent cap 1% of SI/day, co-pay 10%, sub-limit cataract ₹40,000 (SRS §3.15)

### 1.4 Patients (cross-branch UHID, SRS §3.1)
| UHID | Name | DOB | Branch reg. | Linked IDs (dummy/invalid) |
|---|---|---|---|---|
| UHID-100001 | Mohan Lal | 1979-04-12 | BR-001 | Aadhaar `9999-8888-7777` (masked), ABHA `91-1111-2222-3333` |
| UHID-100002 | Rekha Devi | 1991-09-30 | BR-002 | ABHA `91-4444-5555-6666` |
| UHID-100003 | Arjun Mehra | 1965-01-05 | BR-001 | PM-JAY ID `PMJAY-UP-0001`, Aadhaar `1111-2222-3333` |
| UHID-100004 | Salim Ansari | 1988-07-18 | BR-003 | ESIC IP `IP-1234567890`, Pehchan `PEH-0001` |
| UHID-100005 | Baby of Rekha | 2026-06-20 | BR-002 | (newborn, for Birth cert) |

---

## 2. Module-by-Module Deep Test Cases

> Format: **TC-ID | Module (SRS ref) | Steps with dummy data | Expected result**. Each "Expected" includes that an **audit-trail entry** is written (SRS §8.1) and **branch context** is honored (SRS §3.21).

### 2.1 Patient Registration & UHID (§3.1)
- **TC-REG-01:** Register `Mohan Lal` at BR-001 → unique `UHID-100001` generated; demographics stored.
- **TC-REG-02:** Same patient visits BR-002 → existing UHID reused; visit history shows both branches.
- **TC-REG-03 (de-dup, §6.1):** Attempt second registration with same Aadhaar `9999-8888-7777` → blocked as duplicate.
- **TC-REG-04 (masking):** Display patient → Aadhaar shown masked `XXXX-XXXX-7777`.

### 2.2 Aadhaar / ABHA / ABDM (§6.1, §6.2)
- **TC-ID-01:** Aadhaar OTP verify (UIDAI sandbox/mock) for `1111-2222-3333` → success; number stored encrypted/masked.
- **TC-ID-02:** Create ABHA via mobile for `Rekha Devi` → ABHA `91-4444-5555-6666` linked to UHID-100002.
- **TC-ID-03 (Scan & Share):** Receptionist scans QR → OPD registration auto-filled.
- **TC-ID-04 (HFR/HPR):** Facility BR-001 onboarded to HFR; Dr. Rakesh onboarded to HPR.
- **TC-ID-05 (Consent HIE):** Patient grants consent → record shared HIP→HIU as FHIR R4; consent artifact logged. Revoke → sharing stops.

### 2.3 Appointment & Token (§3.2)
- **TC-APT-01:** Online booking for `Mohan Lal` with Dr. Rakesh → token generated, queue updated (SignalR board reflects it).
- **TC-APT-02:** Reminder fires via SMS/email/WhatsApp at interval **read from config** (not hardcoded).
- **TC-APT-03:** Offline walk-in booking → token sequenced correctly with online ones.

### 2.4 OPD Consultation (§3.3)
- **TC-OPD-01:** Dr. Rakesh records dx `I10 Hypertension`, digital prescription + lab referral (CBC) + follow-up date.
- **TC-OPD-02:** Prescription flows to Pharmacy; lab referral appears in LIS queue.

### 2.5 IPD Admission & Ward (§3.4)
- **TC-IPD-01:** Admit `Arjun Mehra`, allocate General ward bed → bed marked occupied.
- **TC-IPD-02:** Ward transfer + nursing notes recorded; discharge summary generated.
- **TC-IPD-03:** Branch-to-branch transfer BR-001→BR-002 → record + bed states consistent both branches.

### 2.6 ICU & Emergency Trauma (§3.5)
- **TC-ER-01:** Emergency arrival → triage category assigned; ICU admission workflow; emergency billing opened.
- **TC-ER-02 (NFR §8.3):** Triage screen responds instantly under load.

### 2.7 Ambulance & GPS (§3.6)
- **TC-AMB-01:** Emergency call logged → nearest ambulance (driver `Vijay Pal`) dispatched.
- **TC-AMB-02:** Live GPS updates stream via SignalR; arrival notification fired.

### 2.8 Blood Bank (§3.7)
- **TC-BB-01:** Add O+ units; emergency request for O+ → fulfilled, inventory decremented.
- **TC-BB-02:** Shortage of AB- → donor alert triggered; branch-to-branch transfer BR-002→BR-001.

### 2.9 LIS (§3.8)
- **TC-LIS-01:** Doctor requests CBC for UHID-100001 → barcode sample generated, tracked.
- **TC-LIS-02:** Result entered → report auto-generated, uploaded to patient portal.

### 2.10 Radiology & Imaging (§3.9, PC-PNDT §10)
- **TC-RAD-01:** Schedule chest X-Ray; upload report; doctor review recorded.
- **TC-RAD-02:** PC-PNDT-regulated study → controlled record access enforced.

### 2.11 Pharmacy (§3.10, D&C/NDPS §10)
- **TC-PHA-01:** Dispense against prescription with batch/expiry; stock auto-deducted; pharmacy bill raised.
- **TC-PHA-02:** Dispense near-expiry batch blocked per expiry window **from config**.
- **TC-PHA-03:** Narcotic item → NDPS register entry mandatory.

### 2.12 Inventory & Store (§3.11)
- **TC-INV-01:** Stock falls below low-stock level (config) → alert; PO auto-suggested to supplier.
- **TC-INV-02:** Branch stock transfer BR-001→BR-003 reconciles both inventories.

### 2.13 Operation Theatre (§3.12)
- **TC-OT-01:** Schedule surgery, allocate OT + surgeon/staff, record post-op notes; conflict double-booking blocked.

### 2.14 Nursing & Patient Care (§3.13)
- **TC-NUR-01:** Record vitals + medication administration record; shift handover note saved; diet/care plan linked.

### 2.15 Billing & RCM (§3.14)
- **TC-BIL-01:** Consolidated bill for OPD+Lab+Pharmacy using **tariff master** values; discount + refund handled; GST from config.
- **TC-BIL-02:** Emergency billing integrates with ICU episode.

### 2.16 Insurance / TPA / Cashless (§3.15, §7.1) — core engine
- **TC-CASH-01:** Capture policy for `Mohan Lal`, e-card verify → real-time eligibility + sum-insured shown.
- **TC-CASH-02:** Raise pre-auth with dx `S52.5`, ICD-10, planned procedure, cost estimate + clinical docs → submitted.
- **TC-CASH-03:** Insurer raises **query/shortfall** → desk responds; **enhancement** on extended stay submitted.
- **TC-CASH-04:** Final bill with co-pay 10%, room-rent cap, non-payables auto-calculated **from master**; submitted; discharge clearance.
- **TC-CASH-05:** Claim dashboard shows states submitted→queried→approved→settled→denied; **denial → appeals** workflow.
- **TC-CASH-06 (IRDAI §7.1):** "Cashless Everywhere" at non-network facility supported.

### 2.17 NHCX (§7.2)
- **TC-NHCX-01:** Send FHIR coverage-eligibility → pre-auth → claim → payment-notice via single gateway (mock); paperless lifecycle verified.

### 2.18 AB PM-JAY (§7.3)
- **TC-PMJAY-01:** BIS beneficiary search for `Arjun Mehra` (PM-JAY ID `PMJAY-UP-0001`) via Aadhaar e-KYC → verified; e-card validated.
- **TC-PMJAY-02:** Select HBP package `HBP-CARD-001` (rate from master); submit pre-auth via TMS (mock).
- **TC-PMJAY-03:** Ayushman Mitra (`U-MITRA1`) uploads docs; query handled; adjudication status from SHA/NHA.
- **TC-PMJAY-04:** Aadhaar-based discharge verification (OTP/face/biometric mock); anti-fraud photo capture; TAT logged.

### 2.19 ESIC (§7.4)
- **TC-ESIC-01:** Verify IP `IP-1234567890` + Pehchan `PEH-0001` (mock ESIC DB); dependent eligibility checked.
- **TC-ESIC-02:** Credit billing under ESI; SST referral + IMP handling; e-bill submitted; settlement tracked.

### 2.20 CGHS (§7.5)
- **TC-CGHS-01:** Verify CGHS beneficiary; bill at CGHS package rate `CGHS-OPD-001`; permission-letter captured; claim submitted.

### 2.21 ECHS (§7.6)
- **TC-ECHS-01:** Verify ECHS card + referral; BPA online billing; emergency + claim submission handled.

### 2.22 State Schemes & Reconciliation (§7.7, §7.8, §7.9)
- **TC-STATE-01:** Configure a new State scheme via master (no code change) → beneficiary verify + claim workflow works.
- **TC-COB-01:** Patient with two schemes → coordination-of-benefits + priority rules applied.
- **TC-RECON-01:** Upload UTR/bank settlement file → auto-reconciliation; ageing + denial-trend analytics; revenue-leakage report.
- **TC-MATRIX-01:** Each payer type routes to correct claim channel per Payer Coverage Matrix (config-driven).

### 2.23 Payment Gateway (§5)
- **TC-PAY-01:** UPI + Card + NetBanking + QR via Razorpay/Stripe/PayU/Cashfree **sandbox** → success; auto receipt.
- **TC-PAY-02:** Refund + settlement; patient deposit/advance top-up online.
- **TC-PAY-03 (nothing hardcoded):** Gateway keys resolved from Key Vault/config; switching active gateway is a config change.

### 2.24 Certificate & Document (§3.16)
- **TC-CERT-01:** Generate Birth cert for `Baby of Rekha`; workflow Patient Record→Doctor Approval→PDF; template from config.
- **TC-CERT-02:** Fitness + Death + Referral + Discharge certificates generated.

### 2.25 HR & Payroll (§3.17, §3.18)
- **TC-HR-01:** Staff master + attendance + duty-roster + leave for `U-NURSE1`.
- **TC-PAY-04:** Overtime logged → supervisor approval → included in salary slip; monthly OT summary; pay rules from config.

### 2.26 Asset & Equipment (§3.19)
- **TC-AST-01:** Track ventilator; schedule maintenance + AMC; breakdown alert raised.

### 2.27 Occupational Health (§3.23, Factories Act §10)
- **TC-OHC-01:** PEME for new recruit at BR-003 → fitness cert (fit/unfit/fit-with-conditions).
- **TC-OHC-02:** Schedule PME per Factories Act 1948; audiometry/spirometry/vision/vaccination records stored.
- **TC-OHC-03:** Workplace injury → register entry linked to MLC; corporate (company-wise) billing; ESIC linkage.

### 2.28 Telemedicine (§3.24, TPG 2020)
- **TC-TEL-01:** Secure video teleconsult cross-branch; e-prescription with digital signature; patient consent + session audit log.
- **TC-TEL-02:** Tele-ICU / tele-radiology session supported.

### 2.29 Bio-Medical Waste (§3.25, BMWM 2016)
- **TC-BMW-01:** Categorise waste by colour code (from reference table); barcoded bag tracked generation→CBWTF handover; daily qty logged; Form-IV annual report generated.

### 2.30 Diet & Kitchen (§3.26)
- **TC-DIET-01:** Doctor-ordered therapeutic diet → ward indent → kitchen schedule; diet cost flows to IPD bill.

### 2.31 Mortuary & Death (§3.27)
- **TC-MORT-01:** Body register + storage allocation + release workflow; death cert linkage; police/MLC intimation where applicable.

### 2.32 MLC (§3.28)
- **TC-MLC-01:** Create MLC → auto MLC number; mandatory police-station intimation + acknowledgement; chain-of-custody log; linked to emergency/trauma/mortuary.

### 2.33 Consent & e-Document (§3.29)
- **TC-CON-01:** Surgery consent in Hindi template (multilingual); e-sign/thumb captured; versioned; audit trail.

### 2.34 Feedback & Grievance (§3.30)
- **TC-FB-01:** NABH-aligned survey submitted; grievance logged with category + SLA; breach → escalation matrix (config); trend analytics.

### 2.35 Queue & Signage (§3.31)
- **TC-QUE-01:** Token queue for OPD/pharmacy/billing; real-time display board + voice/visual call via SignalR; counter load-balancing.

### 2.36 AI Modules (§4.1–§4.6)
- **TC-AI-01 (§4.1):** Feed vitals+history for high-risk patient → risk flag returned.
- **TC-AI-02 (§4.2):** Smart scheduling reduces waiting time vs baseline.
- **TC-AI-03 (§4.3):** Chatbot answers appointment/report/reminder/emergency queries 24x7.
- **TC-AI-04 (§4.4):** Inventory forecast → auto-reorder suggestion for a fast-moving drug.
- **TC-AI-05 (§4.5):** Inject anomalous billing pattern → fraud risk flagged.
- **TC-AI-06 (§4.6):** Claim pre-scrubbing rejects a packet missing mandatory PM-JAY package document before submission.

### 2.37 Dashboards, Compliance & Audit (§3.20, §3.22, §10)
- **TC-DASH-01:** Admin dashboard shows daily patient count, emergency stats, revenue, inventory alerts, OT cost.
- **TC-AUD-01:** Every create/update/delete across modules writes an immutable audit log entry (user, time, before/after, branch).
- **TC-COMP-01:** Statutory mappings (BMWM Form-IV, Factories Act PME, PC-PNDT, NDPS, NABH) produce required reports.

---

## 3. End-to-End Journey Tests (chained, with dummy data)

- **E2E-01 — OPD cash journey:** `Mohan Lal` book→token→OPD consult (`I10`)→CBC in LIS→pharmacy dispense→consolidated bill→UPI payment (sandbox)→receipt. Verify audit + cross-branch UHID history.
- **E2E-02 — IPD cashless (TPA) journey:** `Mohan Lal` admit→treatment→pre-auth (TC-CASH-02)→query/enhancement→final bill with caps/co-pay→claim submitted via NHCX→settlement reconciled (UTR).
- **E2E-03 — PM-JAY journey:** `Arjun Mehra` BIS verify→HBP package→TMS pre-auth→Ayushman Mitra docs→Aadhaar discharge verify→claim adjudication.
- **E2E-04 — ESIC industrial worker:** `Salim Ansari` IP/Pehchan verify→treatment→SST referral→ESI credit billing→e-bill→settlement.
- **E2E-05 — Emergency/trauma + MLC:** Ambulance dispatch (GPS)→ER triage→ICU→MLC auto-number + police intimation→(if death) mortuary + death cert.
- **E2E-06 — Occupational health contract:** Factory tie-up at BR-003→PEME/PME batch→fitness certs→corporate billing→ESIC linkage.
- **E2E-07 — Telemedicine cross-branch:** Patient at BR-002 ↔ specialist at BR-001→consent→video consult→e-Rx digital signature→pharmacy fulfilment.
- **E2E-08 — Newborn:** `Baby of Rekha` registration→Birth certificate workflow→PDF.

---

## 4. Non-Functional Test Cases (SRS §8)

### 4.1 Security (§8.1)
- **NFR-SEC-01:** JWT required on all API endpoints; expired/invalid token rejected.
- **NFR-SEC-02:** RBAC — Lab Technician cannot access Billing/Insurance endpoints (403).
- **NFR-SEC-03:** AES-256 at rest verified for sensitive fields; TLS enforced in transit.
- **NFR-SEC-04:** MFA enforced for Admin/privileged roles.
- **NFR-SEC-05:** Aadhaar/PII masked in all UI + logs.
- **NFR-SEC-06:** Audit trail immutable — attempt to edit/delete a log entry fails.
- **NFR-SEC-07 (injection):** Dapper parameterization — SQL injection payload in inputs has no effect.

### 4.2 Privacy & Regulatory (§8.2, §10)
- **NFR-PRIV-01:** DPDP 2023 — consent required before processing/ sharing; withdrawal honored.
- **NFR-PRIV-02:** ABDM EHR Standards 2016 — consent artifacts stored correctly.
- **NFR-PRIV-03:** ISO 27001-aligned controls + UIDAI Aadhaar data-vault/masking verified.

### 4.3 Performance (§8.3)
- **NFR-PERF-01:** Load test **1000+ concurrent users** — system stable, within SLA.
- **NFR-PERF-02:** Emergency/triage workflow responds instantly (sub-second) under peak load.

### 4.4 Scalability (§8.4)
- **NFR-SCAL-01:** Onboard a 4th branch via config/master only (no code change) → operational.
- **NFR-SCAL-02:** Horizontal scale-out under load keeps response times stable.

### 4.5 Reliability (§8.5)
- **NFR-REL-01:** Daily backup runs; restore validated.
- **NFR-REL-02:** DR failover drill; **99.9% uptime** budget tracked.

### 4.6 Interoperability (§8.6)
- **NFR-INT-01:** HL7/FHIR R4 clinical exchange validated against R4 schema.
- **NFR-INT-02:** NHCX claim messages conform to spec.
- **NFR-INT-03:** ABDM HIP/HIU exchange validated.

---

## 5. Negative / Edge Cases (samples)
- Duplicate Aadhaar registration blocked (TC-REG-03).
- Pre-auth without mandatory ICD-10 / clinical docs rejected by validation + AI pre-scrubbing (§4.6).
- Dispensing expired/near-expiry batch blocked (config window).
- Booking OT/bed conflict prevented.
- Claim final-bill exceeding sum-insured → shortfall flow triggered, not silent approval.
- Cross-branch transfer with inconsistent bed state rejected.
- Multi-scheme patient → coordination-of-benefits prevents double payment.
- Payment gateway timeout → idempotent retry, no double charge.

---

## 6. "Nothing Hardcoded" Test Assertions (must pass)
- [ ] No test depends on a business value baked in code; all such values are seeded into master/config and asserted from there.
- [ ] Switching active payment gateway = config change only; E2E re-runs green.
- [ ] Adding a new State scheme = master data only; TC-STATE-01 passes without deployment.
- [ ] Changing co-pay %, room-rent cap, package rate, low-stock threshold, reminder interval = master/config edit; relevant TCs recompute correctly.
- [ ] All external endpoints/keys (ABDM/NHCX/PM-JAY/ESIC/CGHS/ECHS/UIDAI/gateways/AI) resolved from config in every environment.

---

## 7. Traceability Summary
| SRS Area | Covered by |
|---|---|
| §3.1–§3.31 (31 functional modules) | TC sections 2.1–2.35 + E2E |
| §4.1–§4.6 (AI) | TC-AI-01…06 |
| §5 (Payments) | TC-PAY-01…04 |
| §6.1–§6.2 (Aadhaar/ABHA/ABDM) | TC-ID-01…05, TC-REG-03/04 |
| §7.1–§7.9 (Cashless & Govt schemes) | TC-CASH/NHCX/PMJAY/ESIC/CGHS/ECHS/STATE/RECON/MATRIX |
| §8.1–§8.6 (NFR) | NFR-SEC/PRIV/PERF/SCAL/REL/INT |
| §10 (Regulatory framework) | TC-COMP-01 + module-level statutory checks |
| **L1 SaaS re-platform** (`L1EnhancementDevPlanCumTracker.md`) | TC-AUTH / TC-SCHEMA / TC-TENANT / TC-FY / TC-PROV / TC-DOMAIN / TC-RBACX (sections 8–13) |

---

## 8. L1 — SaaS Multi-Tenant, Schema & Fiscal-Year Test Cases
> These exercise the **L1 enhancement** (`L1EnhancementDevPlanCumTracker.md`). All hospitals/domains/users below are **FICTITIOUS**. These tests assume Design Decisions D1–D7 are confirmed; where a test depends on a decision it is tagged `[D#]`.

### 8.1 L1 dummy tenants / hospitals (control plane)
| TenantCode | Hospital (fictitious) | Primary domain | Common-domain alias | Fiscal year(s) |
|---|---|---|---|---|
| FH-LKO | Finnid Industrial Hospital — Lucknow | `lko.finnidhospital.test` | `app.finnid.test/t/fh-lko` | FY2025-26, FY2026-27 |
| FH-KNP | Finnid Industrial Hospital — Kanpur | `knp.finnidhospital.test` | `app.finnid.test/t/fh-knp` | FY2025-26 |
| ACME-DEMO | Acme Township Hospital (SaaS trial) | `acme.hospitals.test` | `app.finnid.test/t/acme` | FY2026-27 |

### 8.2 L1 dummy platform users
| User | Plane / scope | Role |
|---|---|---|
| `superadmin@finnid.test` | Platform (HIS_Platform) | superadmin (all modules/pages/tenants) |
| `admin@fh-lko.test` | Tenant FH-LKO | admin (tenant-scoped) |
| `recep@fh-lko.test` | Tenant FH-LKO | receptionist (limited pages) |

---

## 9. Superadmin Login & RBAC (L1.2)
| ID | Scenario | Expected |
|---|---|---|
| TC-AUTH-01 | `POST /api/auth/login` with seeded superadmin creds | 200 + JWT containing tenant/role/perm claims; token validates |
| TC-AUTH-02 | Login with wrong password | 401; failed attempt audited; no token |
| TC-AUTH-03 | Password storage check | `PasswordHash`/`PasswordSalt` populated via PBKDF2/BCrypt; **no plaintext** anywhere |
| TC-AUTH-04 | Privileged role (`IsPrivileged=1`) login | MFA challenge enforced (L1.2.5) |
| TC-AUTH-05 | Receptionist calls an unassigned command (e.g. payroll run) | 403 from RBAC authorization behavior (L1.2.6) |
| TC-AUTH-06 | Permission seed verification | 14 roles have non-empty `RolePermission` grants (fixes current empty state) |
| TC-RBACX-01 | Superadmin creates a new module + page, assigns to a role `[D6]` | menu/registry API returns the new page only for that role |
| TC-RBACX-02 | Module disabled for a tenant in a fiscal year | that tenant's users do not see the module in that FY (L1.0.5 / L1.4.4) |
| TC-RBACX-03 | Assign-page-to-module mapping change | effective navigation recomputes without code deploy |

---

## 10. Schema Handling (L1.1, R1/R2)
| ID | Scenario | Expected |
|---|---|---|
| TC-SCHEMA-01 | Inspect a provisioned master DB | master tables live in `master`/`patient`/`proc` schemas; **0 tables in `dbo`** |
| TC-SCHEMA-02 | Inspect a provisioned per-FY data DB | domain tables in `clinical`/`billing`/`insurance`/… ; none in `dbo` |
| TC-SCHEMA-03 | Repository SQL audit | `grep dbo.` across `src/HIS.Infrastructure/Persistence` → 0 hits; all queries schema-qualified |
| TC-SCHEMA-04 | Master vs transactional placement | a master table edit (e.g. Tariff) is visible across fiscal years; a transaction (Bill) is not |

---

## 11. Onboarding, Fiscal Year & Auto-Provisioning (L1.4, L1.5, L1.7 — R3/R4)
| ID | Scenario | Expected |
|---|---|---|
| TC-PROV-01 | Onboard `ACME-DEMO` via wizard with **fiscal-year dropdown** = FY2026-27 | provisioning creates `ACME-DEMO_Master` + `ACME-DEMO_FY2026-27` automatically, **no human step** |
| TC-PROV-02 | Post-onboard catalog check | `platform.DbCatalog` has tenant+FY→DB rows; masters seeded into master DB |
| TC-PROV-03 | Re-run onboarding (idempotency/retry) | no duplicate DBs; partial-failure rollback clean (L1.5.5) |
| TC-FY-01 | Superadmin "year shift" FH-LKO → FY2026-27 | next-FY data DB auto-created; entitlements/modules/rights/pages carried per config (L1.4.3) |
| TC-FY-02 | Per-FY billing | subscription/billing rows scoped to the correct `TenantFiscalYear`; prior-FY balances carried, not merged |
| TC-FY-03 | Fiscal-year-aware numbering | UHID/Doc numbers reset/scope per fiscal year, not calendar year (fixes current `YEAR()` behaviour) `[D1]` |
| TC-FY-04 | Fiscal boundary config | changing FY start/end = `platform.FiscalYear` edit only; **not hardcoded** `[D1]` |
| TC-PROV-04 | Provisioning soak | onboard 25 tenants × 2 FYs unattended; all DBs valid + isolated (L1.9.3) |

---

## 12. Domain Mapping & Tenant Routing (L1.6, L1.7 — R5)
| ID | Scenario | Expected |
|---|---|---|
| TC-DOMAIN-01 | Request to own domain `lko.finnidhospital.test` | resolves to tenant FH-LKO; login realm/branding = FH-LKO |
| TC-DOMAIN-02 | Request to common domain `app.finnid.test/t/fh-lko` (or subdomain `[D4]`) | resolves to same tenant FH-LKO |
| TC-DOMAIN-03 | Unknown/unmapped host | rejected (no default tenant leakage) |
| TC-DOMAIN-04 | Connection routing | `SqlConnectionFactory` opens the **correct** master/per-FY DB for the resolved `ITenantContext` (L1.6.3) |
| TC-DOMAIN-05 | Login mapping | a user of FH-LKO cannot authenticate against FH-KNP's domain |

---

## 13. Tenant Isolation & Security (L1.9.2 — critical)
| ID | Scenario | Expected |
|---|---|---|
| TC-TENANT-01 | FH-LKO user queries patients while routed to FH-LKO | sees only FH-LKO data; **never** FH-KNP/ACME |
| TC-TENANT-02 | Forge JWT tenant claim to another tenant | rejected; resolution trusts domain+catalog, not just claim |
| TC-TENANT-03 | Cross-FY query | current-FY DB returns current-FY transactions only; historical FY accessed explicitly |
| TC-TENANT-04 | Audit scoping | every provisioning/onboarding/year-shift/login action audited with `TenantId`+`FiscalYearId` |
| TC-TENANT-05 | Superadmin reach | superadmin can traverse tenants for admin ops; tenant admins cannot (`[D6]`) |

---

## 14. L1 "Nothing Hardcoded" assertions (must pass)
- [ ] DB names, connection strings, domains, fiscal boundaries, module/page/permission sets resolved from `platform` tables / config / Key Vault — never literals.
- [ ] Adding a tenant or fiscal year = data + automated provisioning only; **no code change, no manual DBA step**.
- [ ] Enabling/disabling a module or page for a tenant/FY/role = assignment-table edit; navigation recomputes without deploy.
- [ ] Switching DB hosting model (single instance ↔ elastic pool) `[D5]` = config change only.

---

*All dummy IDs/numbers/domains/tenants above are intentionally invalid and for testing only. L1 tests tagged `[D#]` depend on the corresponding Design Decision in `L1EnhancementDevPlanCumTracker.md` §7 being confirmed.*
