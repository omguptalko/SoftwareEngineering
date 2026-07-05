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
    // Dev-only realm override (login-page tenant selector): forces every request into the
    // chosen tenant realm on localhost, where the host doesn't map to a hospital domain.
    try { const dt = sessionStorage.getItem('HIS_devTenant'); if (dt) h['X-Tenant'] = dt; } catch (e) {}
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
    listPatients:    (q) => get(`/api/patients${q ? `?q=${encodeURIComponent(q)}` : ''}`),
    updatePatient:   (cmd) => post('/api/patients/update', cmd),
    setPatientActive:(uhid, isActive) => post('/api/patients/set-active', { uhid, isActive }),
    patientEncounters:(uhid) => get(`/api/patients/${encodeURIComponent(uhid)}/encounters`),
    dashboard:       () => get('/api/dashboard'),
    dashboardAlerts: () => get('/api/dashboard/alerts'),
    auditTrail:      (take) => get(`/api/audit${take ? `?take=${take}` : ''}`),
    fhirPatient:     (uhid) => get(`/api/fhir/Patient/${encodeURIComponent(uhid)}`),
    aiRisk:          (vitals) => post('/api/ai/risk', vitals),
    aiForecast:      () => get('/api/ai/inventory-forecast'),
    aiPreScrub:      (input) => post('/api/ai/claim-prescrub', input),
    health:          () => get('/api/health'),
    // Phase 2 — Appointments & OPD
    apptQueue:       (doctorCode, status) => { const q = new URLSearchParams(); if (doctorCode) q.set('doctorCode', doctorCode); if (status) q.set('status', status); const s = q.toString(); return get('/api/appointments/queue' + (s ? '?' + s : '')); },
    apptSlots:       (doctorCode, date) => get(`/api/appointments/slots?doctorCode=${encodeURIComponent(doctorCode)}&date=${encodeURIComponent(date)}`),
    upcomingAppts:   (doctorCode, followUpOnly) => { const q = new URLSearchParams(); if (doctorCode) q.set('doctorCode', doctorCode); if (followUpOnly) q.set('followUpOnly', 'true'); const s = q.toString(); return get('/api/appointments/upcoming' + (s ? '?' + s : '')); },
    bookAppointment: (cmd) => post('/api/appointments', cmd),
    recordVitals:    (apptId, vitals) => post(`/api/appointments/${apptId}/vitals`, vitals),
    callNext:        (apptId) => post(`/api/appointments/${apptId}/call`, {}),
    apptVitals:      (apptId) => get(`/api/appointments/${apptId}/vitals`),
    opdTemplates:    () => get('/api/opd/templates'),
    saveOpdTemplate: (department, fields) => post('/api/opd/templates', { department, fields }),
    saveConsultation:(cmd) => post('/api/encounters/consultation', cmd),
    // Phase 2.3 — IPD
    bedBoard:        () => get('/api/ipd/bedboard'),
    admittedPatients:() => get('/api/ipd/admissions'),
    admitPatient:    (cmd) => post('/api/ipd/admit', cmd),
    transferBed:     (cmd) => post('/api/ipd/transfer', cmd),
    dischargePatient:(cmd) => post('/api/ipd/discharge', cmd),
    markBedReady:    (bedNo) => post(`/api/ipd/beds/${encodeURIComponent(bedNo)}/ready`, {}),
    // ICU & Emergency Trauma
    triageBoard:     () => get('/api/emergency/triage'),
    registerTriage:  (cmd) => post('/api/emergency/triage', cmd),
    disposeTriage:   (cmd) => post('/api/emergency/triage/dispose', cmd),
    icuAdmissions:   () => get('/api/icu/admissions'),
    otBoard:         () => get('/api/ot/board'),
    scheduleSurgery: (cmd) => post('/api/ot/schedule', cmd),
    startSurgery:    (otId) => post('/api/ot/start', { otId }),
    completeSurgery: (otId, postOpNotes) => post('/api/ot/complete', { otId, postOpNotes }),
    nursingNotes:    (admissionId) => get(`/api/nursing/admissions/${admissionId}/notes`),
    addNursingNote:  (cmd) => post('/api/nursing/notes', cmd),
    icuFlowsheet:    (admissionId) => get(`/api/icu/admissions/${admissionId}/observations`),
    recordIcuObs:    (admissionId, cmd) => post(`/api/icu/admissions/${admissionId}/observations`, cmd),
    // Phase 3 — Diagnostics
    labWorklist:     () => get('/api/lab/worklist'),
    createLabOrder:  (cmd) => post('/api/lab/orders', cmd),
    enterLabResults: (cmd) => post('/api/lab/results', cmd),
    bloodStock:      () => get('/api/bloodbank/stock'),
    radWorklist:     () => get('/api/radiology/worklist'),
    createRadOrder:  (cmd) => post('/api/radiology/orders', cmd),
    reportRadiology: (radOrderId, reportUrl) => post('/api/radiology/report', { radOrderId, reportUrl }),
    // Phase 4 — Pharmacy / Inventory / Assets
    pharmacyQueue:   () => get('/api/pharmacy/queue'),
    drugBatches:     (drugCode) => get(`/api/pharmacy/batches?drugCode=${encodeURIComponent(drugCode)}`),
    dispense:        (cmd) => post('/api/pharmacy/dispense', cmd),
    lowStock:        () => get('/api/inventory/lowstock'),
    inventoryStock:  () => get('/api/inventory/stock'),
    inventorySuppliers:() => get('/api/inventory/suppliers'),
    createPurchaseOrder:(cmd) => post('/api/inventory/purchase-orders', cmd),
    drugMaster:      () => get('/api/masters/drugs'),
    saveDrug:        (cmd) => post('/api/masters/drugs', cmd),
    setDrugActive:   (drugId, isActive) => post('/api/masters/drugs/set-active', { drugId, isActive }),
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
    ambLocation:     (id, body) => post(`/api/ambulance/${id}/location`, body),
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
    certTemplates:   () => get('/api/certificates/templates'),
    certificates:    () => get('/api/certificates'),
    issueCertificate:(cmd) => post('/api/certificates', cmd),
    approveCertificate:(certId, doctorCode) => post(`/api/certificates/${certId}/approve`, { doctorCode }),
    grievances:      () => get('/api/feedback/grievances'),
    submitSurvey:    (cmd) => post('/api/feedback/survey', cmd),
    logGrievance:    (cmd) => post('/api/feedback/grievances', cmd),
  };
})();
