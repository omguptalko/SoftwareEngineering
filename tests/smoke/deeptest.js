// Deep functional smoke harness for the Finnid HIS ERP.
//
// Drives the RUNNING app over HTTP with the fictitious dummy data from
// `deeptestwithdummydata.md` and prints a PASS/FAIL/SKIP matrix per module.
// Read-only where possible; happy-path writes elsewhere (each write is audited,
// tenant-scoped and FY-numbered server-side). External integrations
// (UIDAI/ABDM/NHCX/real gateways/Azure-ML, load, DR) are reported as SKIP — they
// need external services/credentials, not code.
//
// Usage (app must be running — see README.dev.md):
//   dotnet run --project src/HIS.Api          # in one terminal
//   node tests/smoke/deeptest.js              # in another
//
// Config via env (dev defaults shown):
//   HIS_BASE=http://localhost:5142
//   HIS_SU_USER=superadmin   HIS_SU_PASS=ChangeMe!2026
//   HIS_DEMO_USER=billing.demo   HIS_DEMO_PASS=Demo!2026
//
// Requires Node 18+ (global fetch). Exit code is non-zero if any test FAILs.

const BASE = process.env.HIS_BASE || 'http://localhost:5142';
const SU = { user: process.env.HIS_SU_USER || 'superadmin', pass: process.env.HIS_SU_PASS || 'ChangeMe!2026' };
const DEMO = { user: process.env.HIS_DEMO_USER || 'billing.demo', pass: process.env.HIS_DEMO_PASS || 'Demo!2026' };

let TOKEN = null;
const results = [];
const rec = (sec, id, name, status, detail = '') => results.push({ sec, id, name, status, detail });

async function http(method, path, body, hdrs = {}) {
  const headers = Object.assign({ 'Accept': 'application/json' }, hdrs);
  if (TOKEN) headers['Authorization'] = 'Bearer ' + TOKEN;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const res = await fetch(BASE + path, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined });
  let json = null; const txt = await res.text();
  try { json = txt ? JSON.parse(txt) : null; } catch { json = txt; }
  return { status: res.status, json };
}
const GET = (p, h) => http('GET', p, undefined, h);
const POST = (p, b, h) => http('POST', p, b, h);
const ok = s => s >= 200 && s < 300;

async function T(sec, id, name, fn) {
  try { const r = await fn(); rec(sec, id, name, r.pass ? 'PASS' : 'FAIL', r.detail || ''); }
  catch (e) { rec(sec, id, name, 'FAIL', 'ERR ' + e.message); }
}
const SKIP = (sec, id, name, why) => rec(sec, id, name, 'SKIP', why);

(async () => {
  const ctx = {};

  // ---------- AUTH / RBAC (L1.2 / NFR-SEC) ----------
  await T('Auth', 'TC-AUTH-01', 'superadmin login -> JWT', async () => {
    const r = await POST('/api/auth/login', { userName: SU.user, password: SU.pass, mfaCode: null });
    TOKEN = r.json && r.json.token; return { pass: ok(r.status) && !!TOKEN, detail: `HTTP ${r.status}` };
  });
  await T('Auth', 'TC-AUTH-02', 'wrong password -> 401', async () => {
    const r = await POST('/api/auth/login', { userName: SU.user, password: 'WRONG', mfaCode: null });
    return { pass: r.status === 401, detail: `HTTP ${r.status}` };
  });
  await T('Auth', 'NFR-SEC-01', 'no token on gated endpoint -> 401', async () => {
    const saved = TOKEN; TOKEN = null; const r = await GET('/api/audit'); TOKEN = saved;
    return { pass: r.status === 401, detail: `HTTP ${r.status}` };
  });
  await T('Auth', 'NFR-SEC-02', 'RBAC: non-privileged -> 403', async () => {
    const lg = await POST('/api/auth/login', { userName: DEMO.user, password: DEMO.pass, mfaCode: null });
    const saved = TOKEN; TOKEN = lg.json && lg.json.token; const r = await GET('/api/platform/audit'); TOKEN = saved;
    return { pass: r.status === 403, detail: `HTTP ${r.status}` };
  });

  // ---------- REFERENCE / LOOKUPS ----------
  const lk = {};
  for (const t of ['doctor', 'drug', 'icd10', 'tariff', 'ward', 'payer', 'package', 'patient']) {
    const r = await GET('/api/lookups/' + t);
    lk[t] = (r.json && r.json.rows) || [];
    rec('Reference', 'LK-' + t, `lookup ${t}`, ok(r.status) ? 'PASS' : 'FAIL', `HTTP ${r.status}, ${lk[t].length} rows`);
  }
  const code = (t, i = 0) => (lk[t][i] && lk[t][i][0]) || null;
  ctx.doctor = code('doctor'); ctx.payer = code('payer'); ctx.tariff = code('tariff'); ctx.drug = code('drug');
  await T('Reference', 'META', 'module registry', async () => { const r = await GET('/api/meta/registry'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });
  await T('Reference', 'REALM', 'tenant realm resolves', async () => { const r = await GET('/api/realm'); return { pass: ok(r.status) && r.json && r.json.resolved, detail: JSON.stringify(r.json) }; });
  await T('Reference', 'MENU', 'effective menu (superadmin)', async () => { const r = await GET('/api/menu'); return { pass: ok(r.status) && Array.isArray(r.json), detail: `HTTP ${r.status}, ${(r.json || []).length} modules` }; });

  // ---------- 2.1 PATIENT ----------
  const uniq = Date.now().toString().slice(-6);
  await T('Patient', 'TC-REG-01', 'register patient -> UHID', async () => {
    const mobile = ('9' + uniq + '000').slice(0, 10);
    const r = await POST('/api/patients', { fullName: 'Test Mohan ' + uniq, sex: 'Male', mobile, ageYears: 45, bloodGroup: 'O+', aadhaarMasked: 'XXXX-XXXX-' + uniq.slice(-4), abhaNumber: '91-1111-2222-' + uniq.slice(-4) });
    ctx.uhid = r.json && r.json.uhid; return { pass: ok(r.status) && !!ctx.uhid, detail: `UHID ${ctx.uhid} (HTTP ${r.status})` };
  });
  await T('Patient', 'TC-REG-02', 'fetch patient + history', async () => {
    if (!ctx.uhid) return { pass: false, detail: 'no uhid' };
    const r = await GET('/api/patients/' + encodeURIComponent(ctx.uhid)); return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.3 APPOINTMENT ----------
  const today = new Date().toISOString().slice(0, 10);
  await T('Appointment', 'TC-APT-slots', 'slots list', async () => { const r = await GET(`/api/appointments/slots?doctorCode=${encodeURIComponent(ctx.doctor || '')}&date=${today}`); ctx.slots = r.json; return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });
  await T('Appointment', 'TC-APT-01', 'book appointment -> token', async () => {
    const slot = Array.isArray(ctx.slots) && ctx.slots.find(s => s.available !== false);
    const slotStart = (slot && (slot.start || slot.slotStart)) || new Date(Date.now() + 3600e3).toISOString();
    const r = await POST('/api/appointments', { doctorCode: ctx.doctor, patientUhid: ctx.uhid, slotStart, visitType: 'OPD', mode: 'Walk-in' });
    ctx.appt = r.json; return { pass: ok(r.status), detail: `HTTP ${r.status} ${JSON.stringify(r.json).slice(0, 60)}` };
  });
  await T('Appointment', 'TC-APT-q', 'queue', async () => { const r = await GET('/api/appointments/queue'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });

  // ---------- 2.4 OPD ----------
  await T('OPD', 'TC-OPD-01', 'save consultation (dx I10)', async () => {
    const r = await POST('/api/encounters/consultation', { patientUhid: ctx.uhid, doctorCode: ctx.doctor, complaints: 'HTN follow-up', advice: 'continue meds', diagnosisCodes: ['I10'] });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.5 IPD ----------
  // Admit/nursing/diet/discharge need a free bed. If the ward is at capacity
  // (e.g. repeated runs against the same tenant), these are SKIPped, not FAILed.
  await T('IPD', 'TC-IPD-bed', 'bed board', async () => { const r = await GET('/api/ipd/bedboard'); ctx.beds = r.json; return { pass: ok(r.status), detail: `HTTP ${r.status}, ${(r.json || []).length} beds` }; });
  const freeBed = Array.isArray(ctx.beds) && ctx.beds.find(b => b.status === 'free');
  if (freeBed) {
    await T('IPD', 'TC-IPD-01', 'admit patient -> bed occupied', async () => {
      const r = await POST('/api/ipd/admit', { patientUhid: ctx.uhid, bedLabel: freeBed.bedNo, consultantCode: ctx.doctor, admissionType: 'Planned', paymentClass: 'Cash' });
      ctx.admissionId = r.json && r.json.admissionId; return { pass: ok(r.status) && !!ctx.admissionId, detail: `Adm ${r.json && r.json.admissionNo} (HTTP ${r.status})` };
    });
    await T('Nursing', 'TC-NUR-01', 'nursing note', async () => {
      if (!ctx.admissionId) return { pass: false, detail: 'admit failed' };
      const r = await POST('/api/nursing/notes', { admissionId: ctx.admissionId, noteType: 'Vitals', note: 'BP 120/80, HR 78' });
      return { pass: ok(r.status), detail: `HTTP ${r.status}` };
    });
    await T('Diet', 'TC-DIET-01', 'diet order', async () => {
      if (!ctx.admissionId) return { pass: false, detail: 'admit failed' };
      const r = await POST('/api/diet', { admissionId: ctx.admissionId, dietType: 'Diabetic', cost: 200 });
      return { pass: ok(r.status), detail: `HTTP ${r.status}` };
    });
  } else {
    SKIP('IPD', 'TC-IPD-01', 'admit patient -> bed occupied', 'ward at capacity (no free bed)');
    SKIP('Nursing', 'TC-NUR-01', 'nursing note', 'no free bed to admit into');
    SKIP('Diet', 'TC-DIET-01', 'diet order', 'no free bed to admit into');
  }

  // ---------- 2.6 EMERGENCY ----------
  await T('Emergency', 'TC-ER-01', 'triage register', async () => {
    const r = await POST('/api/emergency/triage', { category: 'Red', isMlc: false, notes: 'chest pain' });
    ctx.triageId = r.json && r.json.triageId; return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });
  await T('Emergency', 'TC-ER-board', 'ED board', async () => { const r = await GET('/api/emergency/triage'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });

  // ---------- 2.13 OT ----------
  await T('OT', 'TC-OT-01', 'schedule surgery', async () => {
    const r = await POST('/api/ot/schedule', { patientUhid: ctx.uhid, surgeonCode: ctx.doctor, theatre: 'OT-1', scheduledUtc: new Date(Date.now() + 7200e3).toISOString(), procedure: 'Appendectomy' });
    ctx.otId = r.json && (r.json.otId || r.json); return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });
  await T('OT', 'TC-OT-complete', 'complete surgery', async () => {
    if (!ctx.otId) return { pass: false, detail: 'no otId' };
    const r = await POST('/api/ot/complete', { otId: ctx.otId, postOpNotes: 'uneventful' });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.9 LIS ----------
  await T('LIS', 'TC-LIS-01', 'lab order -> barcode', async () => {
    const r = await POST('/api/lab/orders', { patientUhid: ctx.uhid, testName: 'CBC' });
    ctx.labId = r.json && r.json.labOrderId; return { pass: ok(r.status) && !!ctx.labId, detail: `HTTP ${r.status} ${r.json && r.json.barcode || ''}` };
  });
  await T('LIS', 'TC-LIS-02', 'enter results', async () => {
    if (!ctx.labId) return { pass: false, detail: 'no labId' };
    const r = await POST('/api/lab/results', { labOrderId: ctx.labId, results: [{ parameter: 'Hb', resultValue: '13.5', unit: 'g/dL', referenceRange: '12-16', flag: 'Normal' }] });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.10 RADIOLOGY ----------
  await T('Radiology', 'TC-RAD-01', 'radiology order', async () => {
    const r = await POST('/api/radiology/orders', { patientUhid: ctx.uhid, modality: 'XRay', studyName: 'Chest', isPcPndtRegulated: false });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.8 BLOOD BANK ----------
  await T('BloodBank', 'TC-BB-stock', 'blood stock', async () => { const r = await GET('/api/bloodbank/stock'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });
  await T('BloodBank', 'TC-BB-01', 'blood request', async () => {
    const r = await POST('/api/bloodbank/requests', { bloodGroup: 'O+', units: 1, isEmergency: true });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.11 PHARMACY ----------
  await T('Pharmacy', 'TC-PHA-queue', 'pharmacy queue', async () => { const r = await GET('/api/pharmacy/queue'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });
  // Dispense needs a seeded drug batch; a bare tenant (no batch seed) -> SKIP.
  let phaBatch = null;
  if (ctx.drug) { const b = await GET('/api/pharmacy/batches?drugCode=' + encodeURIComponent(ctx.drug)); phaBatch = Array.isArray(b.json) && b.json[0]; }
  if (phaBatch) {
    await T('Pharmacy', 'TC-PHA-01', 'dispense (batch/stock)', async () => {
      const r = await POST('/api/pharmacy/dispense', { isNdps: false, lines: [{ drugCode: ctx.drug, batchNo: phaBatch.batchNo, qty: 1 }] });
      return { pass: ok(r.status), detail: `HTTP ${r.status} ${JSON.stringify(r.json).slice(0, 50)}` };
    });
  } else { SKIP('Pharmacy', 'TC-PHA-01', 'dispense (batch/stock)', `no drug batches seeded for ${ctx.drug}`); }

  // ---------- 2.12 INVENTORY ----------
  await T('Inventory', 'TC-INV-low', 'low-stock alerts', async () => { const r = await GET('/api/inventory/lowstock'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });
  await T('Inventory', 'TC-INV-01', 'create PO', async () => {
    const r = await POST('/api/inventory/purchase-orders', { supplierId: 1, lines: [{ itemName: 'Surgical Gloves', qty: 100, unitPrice: 5 }] });
    return { pass: ok(r.status), detail: `HTTP ${r.status} ${r.json && r.json.poNo || ''}` };
  });

  // ---------- 2.26 ASSETS ----------
  await T('Assets', 'TC-AST-list', 'asset list', async () => { const r = await GET('/api/assets'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });
  await T('Assets', 'TC-AST-01', 'register asset', async () => {
    const r = await POST('/api/assets', { assetTag: 'VENT-' + uniq, name: 'Ventilator', category: 'ICU' });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.15 BILLING ----------
  await T('Billing', 'TC-BIL-01', 'create bill (tariff master)', async () => {
    const r = await POST('/api/billing/bills', { patientUhid: ctx.uhid, discountAmount: 0, insurancePays: 0, lines: [{ tariffCode: ctx.tariff, description: 'OPD consult', qty: 1, rate: 500 }] });
    ctx.billId = r.json && r.json.billId; return { pass: ok(r.status) && !!ctx.billId, detail: `${r.json && r.json.billNo} pays ${r.json && r.json.patientPays} (HTTP ${r.status})` };
  });
  await T('Payments', 'TC-PAY-01', 'collect payment (gateway from config)', async () => {
    if (!ctx.billId) return { pass: false, detail: 'no bill' };
    const r = await POST('/api/payments/collect', { billId: ctx.billId, patientUhid: ctx.uhid, mode: 'UPI', amount: 100 });
    return { pass: ok(r.status), detail: `HTTP ${r.status} ${r.json && r.json.provider || ''}` };
  });

  // ---------- 2.16 INSURANCE / CASHLESS ----------
  await T('Insurance', 'TC-CASH-01', 'capture policy + eligibility', async () => {
    const p = await POST('/api/claims/policies', { patientUhid: ctx.uhid, payerCode: ctx.payer, policyNo: 'POL-' + uniq, sumInsured: 500000, roomRentCapPerDay: 5000, coPayPct: 10 });
    const e = await GET('/api/claims/eligibility?patientUhid=' + encodeURIComponent(ctx.uhid));
    return { pass: ok(p.status) && ok(e.status), detail: `policy ${p.status}, eligibility ${e.status}` };
  });
  await T('Insurance', 'TC-CASH-02', 'pre-auth', async () => {
    const r = await POST('/api/claims/preauth', { patientUhid: ctx.uhid, payerCode: ctx.payer, preAuthAmount: 50000, provisionalIcd10: 'S52.5', channel: 'TPA', mandatoryDocs: ['ICD-10', 'Estimate'] });
    ctx.claimId = r.json && r.json.claimId; return { pass: ok(r.status) && !!ctx.claimId, detail: `${r.json && r.json.claimNo} (HTTP ${r.status})` };
  });
  await T('Insurance', 'TC-CASH-03', 'claim lifecycle event', async () => {
    if (!ctx.claimId) return { pass: false, detail: 'no claim' };
    const r = await POST(`/api/claims/${ctx.claimId}/events`, { eventType: 'Approval', amount: 40000, notes: 'approved' });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });
  await T('Insurance', 'TC-CASH-05', 'claims MIS', async () => { const r = await GET('/api/claims/mis'); return { pass: ok(r.status), detail: `HTTP ${r.status}` }; });

  // ---------- 2.18 PM-JAY ----------
  await T('PMJAY', 'TC-PMJAY-01', 'BIS verify', async () => {
    const r = await POST('/api/pmjay/verify', { patientUhid: ctx.uhid, pmjayId: 'PMJAY-UP-0001' });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });
  await T('PMJAY', 'TC-PMJAY-02', 'TMS claim (HBP rate)', async () => {
    const r = await POST('/api/pmjay/claim', { patientUhid: ctx.uhid, packageCode: 'CD-014' });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });

  // ---------- 2.19-2.22 SCHEMES ----------
  await T('Schemes', 'TC-ESIC-01', 'ESIC verify', async () => {
    const r = await POST('/api/schemes/verify', { patientUhid: ctx.uhid, schemeType: 'ESIC', memberNo: 'IP-1234567890', secondaryRef: 'PEH-0001' });
    return { pass: ok(r.status), detail: `HTTP ${r.status}` };
  });
  await T('Schemes', 'TC-CGHS-pkg', 'scheme packages', async () => { const r = await GET('/api/schemes/packages?schemeType=CGHS'); return { pass: ok(r.status), detail: `HTTP ${r.status}, ${(r.json || []).length} pkgs` }; });

  // ---------- 2.25 HR & PAYROLL ----------
  await T('HR', 'TC-HR-01', 'add staff + attendance', async () => {
    const ec = 'EMP-' + uniq;
    const s = await POST('/api/hr/staff', { employeeCode: ec, fullName: 'Test Nurse ' + uniq, designation: 'Nurse', department: 'Ward' });
    const a = await POST('/api/hr/attendance', { employeeCode: ec, workDate: today, status: 'Present' });
    ctx.emp = ec; return { pass: ok(s.status) && ok(a.status), detail: `staff ${s.status}, attendance ${a.status}` };
  });
  await T('Payroll', 'TC-PAY-04', 'payroll run (OT+PF from config)', async () => {
    if (!ctx.emp) return { pass: false, detail: 'no emp' };
    const d = new Date(); const r = await POST('/api/payroll/run', { employeeCode: ctx.emp, year: d.getUTCFullYear(), month: d.getUTCMonth() + 1, basicPay: 30000, overtimeHours: 12 });
    return { pass: ok(r.status), detail: `net ${r.json && r.json.netPay} (HTTP ${r.status})` };
  });

  // ---------- 2.27 OCC HEALTH ----------
  await T('OccHealth', 'TC-OHC-01', 'contract + PEME exam', async () => {
    const c = await POST('/api/occhealth/contracts', { companyName: 'Acme Ltd ' + uniq, contractType: 'Annual' });
    const e = await POST('/api/occhealth/exams', { patientUhid: ctx.uhid, examType: 'PEME', examDate: today, fitnessResult: 'Fit', audiometry: 'Normal', vision: '6/6' });
    return { pass: ok(c.status) && ok(e.status), detail: `contract ${c.status}, exam ${e.status}` };
  });

  // ---------- 2.28 TELEMEDICINE ----------
  await T('Telemedicine', 'TC-TEL-01', 'schedule->consent->sign->complete', async () => {
    const s = await POST('/api/telemedicine', { patientUhid: ctx.uhid, doctorCode: ctx.doctor, consultType: 'Video', scheduledUtc: new Date(Date.now() + 3600e3).toISOString() });
    const id = (s.json && s.json.teleId) || s.json; if (!ok(s.status) || !id) return { pass: false, detail: `schedule HTTP ${s.status}` };
    const co = await POST(`/api/telemedicine/${id}/consent`, {});
    const sg = await POST(`/api/telemedicine/${id}/sign`, {});
    const cp = await POST(`/api/telemedicine/${id}/complete`, {});
    return { pass: [s, co, sg, cp].every(x => ok(x.status)), detail: `s${s.status} c${co.status} sign${sg.status} done${cp.status}` };
  });

  // ---------- 2.29 BMWM ----------
  await T('BMWM', 'TC-BMW-01', 'waste bag (colour FK) + Form-IV', async () => {
    const r = await POST('/api/bmwm/bags', { barcode: 'BAG-' + uniq, colourCode: 'Yellow', weightKg: 1.2 });
    const g = await GET('/api/bmwm');
    return { pass: ok(r.status) && ok(g.status), detail: `bag ${r.status}, form-iv ${g.status}` };
  });

  // ---------- 2.32 MLC ----------
  await T('MLC', 'TC-MLC-01', 'create MLC (auto number)', async () => {
    const r = await POST('/api/mlc', { patientUhid: ctx.uhid, policeStation: 'PS-Hazratganj', injuryDetails: 'RTA polytrauma' });
    return { pass: ok(r.status), detail: `${r.json && r.json.mlcNo} (HTTP ${r.status})` };
  });

  // ---------- 2.35 QUEUE (needs seeded counters) ----------
  const qc = await GET('/api/queue/counters');
  const counterId = Array.isArray(qc.json) && qc.json[0] && qc.json[0].counterId;
  if (counterId) {
    await T('Queue', 'TC-QUE-01', 'counters + token + board', async () => {
      const tk = await POST(`/api/queue/counters/${counterId}/token`, {});
      const bd = await GET('/api/queue');
      return { pass: ok(tk.status) && ok(bd.status), detail: `token ${tk.json}, board ${bd.status}` };
    });
  } else { SKIP('Queue', 'TC-QUE-01', 'counters + token + board', 'no queue counters seeded in this tenant'); }

  // ---------- 2.34 FEEDBACK ----------
  await T('Feedback', 'TC-FB-01', 'survey + grievance', async () => {
    const s = await POST('/api/feedback/survey', { score: 5, comments: 'Great care' });
    const g = await POST('/api/feedback/grievances', { category: 'Billing delay' });
    return { pass: ok(s.status) && ok(g.status), detail: `survey ${s.status}, grievance ${g.status}` };
  });

  // ---------- 2.7 AMBULANCE + GPS (needs a seeded fleet) ----------
  // GPS ping is tested against any fleet vehicle; dispatch is accepted as 2xx
  // (dispatched) OR 409 (fleet all busy — a correct capacity response). A bare
  // tenant with no ambulances seeded -> SKIP.
  const fleetR = await GET('/api/ambulance');
  const fleet = Array.isArray(fleetR.json) ? fleetR.json : [];
  if (fleet.length) {
    await T('Ambulance', 'TC-AMB-01', 'dispatch + GPS ping', async () => {
      const g = await POST(`/api/ambulance/${fleet[0].ambulanceId}/location`, { lat: 26.85, lng: 80.95, speedKmph: 40 });
      const d = await POST('/api/ambulance/dispatch', { pickupLat: null, pickupLng: null });
      const dispatchOk = ok(d.status) || d.status === 409;
      return { pass: ok(g.status) && dispatchOk, detail: `gps ${g.status}, dispatch ${d.status}${d.status === 409 ? ' (fleet busy)' : ''}` };
    });
  } else { SKIP('Ambulance', 'TC-AMB-01', 'dispatch + GPS ping', 'no ambulances seeded in this tenant'); }

  // ---------- 2.36 AI ----------
  await T('AI', 'TC-AI-01', 'risk prediction', async () => {
    const r = await POST('/api/ai/risk', { respiratoryRate: 28, spO2: 90, temperatureC: 39.5, systolicBp: 88, heartRate: 125, consciousness: 'Confused' });
    return { pass: ok(r.status) && r.json && r.json.band === 'High', detail: `score ${r.json && r.json.score}/${r.json && r.json.band}` };
  });
  await T('AI', 'TC-AI-04', 'inventory forecast', async () => { const r = await GET('/api/ai/inventory-forecast'); return { pass: ok(r.status), detail: `HTTP ${r.status}, ${(r.json || []).length} items` }; });
  await T('AI', 'TC-AI-06', 'claim pre-scrub (reject over-package)', async () => {
    const r = await POST('/api/ai/claim-prescrub', { packageCode: 'CD-014', claimedAmount: 70000, documents: ['Final Bill'] });
    return { pass: ok(r.status) && r.json && r.json.verdict === 'Reject', detail: `verdict ${r.json && r.json.verdict}` };
  });

  // ---------- 2.37 DASHBOARD / COMPLIANCE / AUDIT ----------
  await T('Dashboard', 'TC-DASH-01', 'dashboard KPIs + alerts', async () => {
    const d = await GET('/api/dashboard'); const a = await GET('/api/dashboard/alerts');
    return { pass: ok(d.status) && ok(a.status), detail: `dash ${d.status}, alerts ${(a.json || []).length}` };
  });
  await T('Compliance', 'TC-AUD-01', 'audit trail view', async () => {
    const r = await GET('/api/audit?take=10'); return { pass: ok(r.status) && Array.isArray(r.json), detail: `HTTP ${r.status}, ${(r.json || []).length} rows` };
  });

  // ---------- NFR-INT FHIR ----------
  await T('Interop', 'NFR-INT-01', 'FHIR R4 Patient resource', async () => {
    if (!ctx.uhid) return { pass: false, detail: 'no uhid' };
    const r = await GET('/api/fhir/Patient/' + encodeURIComponent(ctx.uhid));
    return { pass: ok(r.status) && r.json && r.json.resourceType === 'Patient', detail: `HTTP ${r.status} ${r.json && r.json.resourceType}` };
  });

  // ---------- IPD discharge (cleanup — frees the bed this run occupied) ----------
  if (ctx.admissionId) {
    await T('IPD', 'TC-IPD-02', 'discharge (frees bed)', async () => {
      const r = await POST('/api/ipd/discharge', { admissionId: ctx.admissionId, dischargeSummary: 'recovered' });
      return { pass: ok(r.status), detail: `HTTP ${r.status}` };
    });
  } else {
    SKIP('IPD', 'TC-IPD-02', 'discharge (frees bed)', 'nothing admitted this run');
  }

  // ---------- External / not testable without services+credentials ----------
  SKIP('Identity', 'TC-ID-01', 'UIDAI Aadhaar OTP verify', 'external UIDAI sandbox — adapter pending');
  SKIP('ABDM', 'TC-ID-05', 'ABHA/HIE consent exchange', 'external ABDM — adapter pending');
  SKIP('NHCX', 'TC-NHCX-01', 'NHCX FHIR claim exchange', 'external NHCX gateway — adapter pending');
  SKIP('Payments', 'TC-PAY-real', 'real gateway sandbox (Razorpay/etc)', 'external gateway — IPaymentGateway is config-mocked');
  SKIP('AI', 'TC-AI-02/03/05', 'smart-scheduling / chatbot / fraud', 'need Azure AI / Python ML');
  SKIP('NFR', 'NFR-PERF-01', '1000+ concurrent load', 'needs k6/JMeter (L1.9.4 ran 2000@64 on LocalDB)');
  SKIP('NFR', 'NFR-REL-01', 'daily backup / DR failover', 'ops/infra');

  // ---------- REPORT ----------
  const by = { PASS: 0, FAIL: 0, SKIP: 0 };
  results.forEach(r => by[r.status]++);
  console.log('\n================ DEEP FUNCTIONAL SMOKE RESULTS ================');
  let lastSec = null;
  for (const r of results) {
    if (r.sec !== lastSec) { console.log(`\n— ${r.sec} —`); lastSec = r.sec; }
    const mark = r.status === 'PASS' ? 'PASS' : r.status === 'FAIL' ? 'FAIL' : 'skip';
    console.log(`  [${mark}] ${r.id.padEnd(14)} ${r.name}  ${r.detail ? '· ' + r.detail : ''}`);
  }
  console.log('\n================ SUMMARY ================');
  console.log(`PASS ${by.PASS}   FAIL ${by.FAIL}   SKIP ${by.SKIP}   (total ${results.length})`);
  if (by.FAIL) {
    console.log('\nFAILURES:');
    results.filter(r => r.status === 'FAIL').forEach(r => console.log(`  ${r.id} ${r.name} — ${r.detail}`));
  }
  process.exit(by.FAIL ? 1 : 0);
})().catch(e => { console.error('HARNESS FATAL', e); process.exit(2); });
