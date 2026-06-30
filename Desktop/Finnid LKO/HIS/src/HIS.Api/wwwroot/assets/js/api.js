/* ============================================================================
   HIS ERP — API client
   Replaces the static datasets that used to live in data.js. Every dataset is
   now fetched from the ASP.NET Core Web API (same origin, so no hardcoded URL).
   ========================================================================== */
window.HIS = window.HIS || {};

HIS.api = (function () {
  // Same-origin: the wireframe is served by HIS.Api, so the base is relative.
  // (If ever hosted separately, set window.HIS_API_BASE — still not hardcoded here.)
  const base = (window.HIS_API_BASE || '').replace(/\/$/, '');

  // Attach the JWT (if signed in) so RBAC-gated endpoints + per-request context work (L1.2.7).
  function headers(extra) {
    const h = Object.assign({ 'Accept': 'application/json' }, extra || {});
    const tok = window.HIS && HIS.auth && HIS.auth.token();
    if (tok) h['Authorization'] = 'Bearer ' + tok;
    return h;
  }

  // A 401 mid-session means the token expired/was revoked → bounce to login.
  function onUnauthorized() {
    if (window.HIS && HIS.auth) HIS.auth.clear();
    if (/\/app\//.test(location.pathname)) location.href = 'login.html';
  }

  async function get(path) {
    const res = await fetch(base + path, { headers: headers() });
    if (res.status === 401) { onUnauthorized(); throw new Error(`GET ${path} → 401`); }
    if (res.status === 204) return null;
    if (!res.ok) throw new Error(`GET ${path} → ${res.status}`);
    return res.json();
  }

  async function post(path, body) {
    const res = await fetch(base + path, {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(body)
    });
    if (res.status === 401) { onUnauthorized(); throw new Error(`POST ${path} → 401`); }
    if (!res.ok) {
      let detail = '';
      try { detail = JSON.stringify(await res.json()); } catch (e) {}
      throw new Error(`POST ${path} → ${res.status} ${detail}`);
    }
    return res.json();
  }

  return {
    get, post,
    // L1.2 — auth / control plane
    menu:            () => get('/api/menu'),
    registry:        () => get('/api/meta/registry'),
    lookup:          (type, q) => get(`/api/lookups/${encodeURIComponent(type)}${q ? `?q=${encodeURIComponent(q)}` : ''}`),
    defaultPatient:  () => get('/api/patients/default'),
    patientByUhid:   (uhid) => get(`/api/patients/${encodeURIComponent(uhid)}`),
    registerPatient: (cmd) => post('/api/patients', cmd),
    dashboard:       () => get('/api/dashboard'),
    dashboardAlerts: () => get('/api/dashboard/alerts'),
    auditTrail:      (take) => get(`/api/audit${take ? `?take=${take}` : ''}`),
    fhirPatient:     (uhid) => get(`/api/fhir/Patient/${encodeURIComponent(uhid)}`),
    aiRisk:          (vitals) => post('/api/ai/risk', vitals),
    aiForecast:      () => get('/api/ai/inventory-forecast'),
    aiPreScrub:      (input) => post('/api/ai/claim-prescrub', input),
    health:          () => get('/api/health'),
    // Phase 2 — Appointments & OPD
    apptQueue:       (doctorCode) => get(`/api/appointments/queue${doctorCode ? `?doctorCode=${encodeURIComponent(doctorCode)}` : ''}`),
    apptSlots:       (doctorCode, date) => get(`/api/appointments/slots?doctorCode=${encodeURIComponent(doctorCode)}&date=${encodeURIComponent(date)}`),
    bookAppointment: (cmd) => post('/api/appointments', cmd),
    saveConsultation:(cmd) => post('/api/encounters/consultation', cmd),
    // Phase 2.3 — IPD
    bedBoard:        () => get('/api/ipd/bedboard'),
    admitPatient:    (cmd) => post('/api/ipd/admit', cmd),
    transferBed:     (cmd) => post('/api/ipd/transfer', cmd),
    dischargePatient:(cmd) => post('/api/ipd/discharge', cmd),
    // Phase 3 — Diagnostics
    labWorklist:     () => get('/api/lab/worklist'),
    createLabOrder:  (cmd) => post('/api/lab/orders', cmd),
    enterLabResults: (cmd) => post('/api/lab/results', cmd),
    bloodStock:      () => get('/api/bloodbank/stock'),
    // Phase 4 — Pharmacy / Inventory / Assets
    pharmacyQueue:   () => get('/api/pharmacy/queue'),
    drugBatches:     (drugCode) => get(`/api/pharmacy/batches?drugCode=${encodeURIComponent(drugCode)}`),
    dispense:        (cmd) => post('/api/pharmacy/dispense', cmd),
    lowStock:        () => get('/api/inventory/lowstock'),
    assets:          () => get('/api/assets'),
    // Phase 6 — Billing & Payments
    createBill:      (cmd) => post('/api/billing/bills', cmd),
    getBill:         (id) => get(`/api/billing/bills/${id}`),
    collectPayment:  (cmd) => post('/api/payments/collect', cmd),
    // Phase 7 — Insurance / Cashless / Schemes
    capturePolicy:   (cmd) => post('/api/claims/policies', cmd),
    eligibility:     (uhid) => get(`/api/claims/eligibility?patientUhid=${encodeURIComponent(uhid)}`),
    createPreAuth:   (cmd) => post('/api/claims/preauth', cmd),
    claimEvent:      (claimId, body) => post(`/api/claims/${claimId}/events`, body),
    claimsMis:       () => get('/api/claims/mis'),
    pmjayVerify:     (cmd) => post('/api/pmjay/verify', cmd),
    pmjayClaim:      (cmd) => post('/api/pmjay/claim', cmd),
    // Phase 8 — HR & Payroll
    hrStaff:         () => get('/api/hr/staff'),
    addStaff:        (cmd) => post('/api/hr/staff', cmd),
    hrAttendance:    (date) => get(`/api/hr/attendance?date=${encodeURIComponent(date)}`),
    markAttendance:  (cmd) => post('/api/hr/attendance', cmd),
    payrollGet:      (year, month) => get(`/api/payroll?year=${year}&month=${month}`),
    payrollRun:      (cmd) => post('/api/payroll/run', cmd),
    payrollApprove:  (id, body) => post(`/api/payroll/${id}/approve`, body),
    // Phase 9 — Occupational Health & Telemedicine
    occContracts:    () => get('/api/occhealth/contracts'),
    occExams:        () => get('/api/occhealth/exams'),
    conductExam:     (cmd) => post('/api/occhealth/exams', cmd),
    occInjuries:     () => get('/api/occhealth/injuries'),
    recordInjury:    (cmd) => post('/api/occhealth/injuries', cmd),
    teleList:        () => get('/api/telemedicine'),
    teleSchedule:    (cmd) => post('/api/telemedicine', cmd),
    teleConsent:     (id) => post(`/api/telemedicine/${id}/consent`, {}),
    teleSign:        (id) => post(`/api/telemedicine/${id}/sign`, {}),
    teleComplete:    (id) => post(`/api/telemedicine/${id}/complete`, {}),
    // Phase 10 — Support & statutory
    ambulances:      () => get('/api/ambulance'),
    ambDispatches:   () => get('/api/ambulance/dispatches'),
    ambDispatch:     (cmd) => post('/api/ambulance/dispatch', cmd),
    ambArrive:       (id) => post(`/api/ambulance/dispatches/${id}/arrive`, {}),
    bmwm:            () => get('/api/bmwm'),
    bmwmBag:         (cmd) => post('/api/bmwm/bags', cmd),
    bmwmHandover:    (id) => post(`/api/bmwm/bags/${id}/handover`, {}),
    mlcList:         () => get('/api/mlc'),
    mlcCreate:       (cmd) => post('/api/mlc', cmd),
    mlcIntimate:     (id, ackRef) => post(`/api/mlc/${id}/intimate`, { ackRef }),
    queueCounters:   () => get('/api/queue/counters'),
    queueBoard:      () => get('/api/queue'),
    queueToken:      (counterId, patientUhid) => post(`/api/queue/counters/${counterId}/token`, { patientUhid }),
    queueCallNext:   (counterId) => post(`/api/queue/counters/${counterId}/call-next`, {}),
    grievances:      () => get('/api/feedback/grievances'),
    submitSurvey:    (cmd) => post('/api/feedback/survey', cmd),
    logGrievance:    (cmd) => post('/api/feedback/grievances', cmd),
  };
})();
