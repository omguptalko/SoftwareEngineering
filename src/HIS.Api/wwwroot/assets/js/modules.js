/* ============================================================================
   HIS ERP — Screen templates (built screens) + placeholder + afterRender
   No static business data: data tables are empty-states until their module API
   is wired; the dashboard, patient banner and IPD bed board load live from the
   Web API. Entry grids start with one blank row for keyboard-first data entry.
   ========================================================================== */
window.HIS = window.HIS || {};

(function () {

  /* ---- shared bits ------------------------------------------------------- */
  const head = (icon, title, sub, actions) => `
    <div class="screen__head">
      <span class="sh-ico"><i class="bi ${icon}"></i></span>
      <div><h1>${title}</h1><p>${sub}</p></div>
      <div class="sh-actions">${actions || `
        <button class="btn btn--ghost btn--sm" data-act="print"><i class="bi bi-printer"></i> Print <span class="fk">F12</span></button>
        <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-save"></i> Save <span class="fk">F9</span></button>`}
      </div>
    </div>`;

  const initials = n => (n || '?').split(' ').map(x => x[0]).slice(0, 2).join('');

  /* Patient banner — driven by the live current patient (no hardcoded record). */
  const banner = (p) => {
    if (!p) return `<div class="pbanner selectable"><div class="av">—</div>
      <div><div class="nm">No patient loaded</div>
      <div class="meta"><span>Use <b>F3</b> to look up a patient</span></div></div></div>`;
    const sex = (p.sex || '').charAt(0);
    return `
    <div class="pbanner selectable">
      <div class="av">${initials(p.name)}</div>
      <div>
        <div class="nm">${p.name || ''}</div>
        <div class="meta"><span>UHID <b>${p.uhid || ''}</b></span><span><b>${p.age ?? ''}</b>/${sex}</span>
          <span>Blood <b>${p.blood || '—'}</b></span><span>Mob <b>${p.mobile || ''}</b></span>
          ${p.abha ? `<span>ABHA <b>${p.abha}</b> <span class="pill pill--ok"><i class="bi bi-check-circle-fill"></i></span></span>` : ''}</div>
      </div>
      <div class="pb-right">
        <div class="s">Payer<b>${p.payer || '—'}</b></div>
        <div class="s">Policy<b>${p.policy || '—'}</b></div>
        <div class="s">Visit<b>OPD · New</b></div>
      </div>
    </div>`;
  };

  /* Empty-state row spanning a table — used wherever a module API is not yet wired. */
  const emptyRow = (cols, msg) =>
    `<tr class="empty-row"><td colspan="${cols}" style="text-align:center;color:var(--muted-2);padding:18px">
       <i class="bi bi-inbox"></i> ${msg || 'No records to display'}</td></tr>`;

  /* Blank entry-grid row templates (no data, just inputs for keyboard entry). */
  const TPL = {
    rxBody: `
      <td><input class="gi" data-lookup="drug" placeholder="F3 drug…"></td>
      <td><input class="gi" placeholder="e.g. 500mg"></td>
      <td><select class="gi"><option>OD</option><option>BD</option><option>TDS</option><option>QID</option><option>HS</option><option>SOS</option></select></td>
      <td class="center"><input class="gi center" placeholder="days" style="width:46px"></td>
      <td><select class="gi"><option>Oral</option><option>IV</option><option>IM</option><option>Topical</option></select></td>
      <td class="num"><input class="gi num" placeholder="qty" style="width:52px"></td>
      <td class="center"><button class="btn btn--sm btn--danger row-del" title="Remove"><i class="bi bi-x"></i></button></td>`,
    chargeBody: `
      <td><input class="gi svc" data-lookup="tariff" placeholder="F3 service / tariff…"></td>
      <td class="center"><input class="gi center qty" value="1" style="width:48px"></td>
      <td class="num"><input class="gi num rate" value="0.00" style="width:84px"></td>
      <td class="num amt">0.00</td>
      <td class="center"><button class="btn btn--sm btn--danger row-del" title="Remove"><i class="bi bi-x"></i></button></td>`,
    dispBody: `
      <td><input class="gi" data-lookup="drug" placeholder="F3 drug…"></td>
      <td><input class="gi" placeholder="Batch no."></td>
      <td><input class="gi" placeholder="MM/YY" style="width:64px"></td>
      <td class="num"><input class="gi num" placeholder="qty" style="width:48px"></td>
      <td class="num"><input class="gi num" placeholder="0.00" style="width:72px"></td>
      <td class="num">0.00</td>
      <td class="center"><button class="btn btn--sm btn--danger row-del" title="Remove"><i class="bi bi-x"></i></button></td>`,
    labResultBody: `
      <td><input class="gi" placeholder="Parameter"></td>
      <td><input class="gi" placeholder="Result"></td>
      <td><input class="gi" placeholder="Unit" style="width:70px"></td>
      <td><input class="gi" placeholder="Reference" style="width:90px"></td>
      <td><select class="gi"><option value="">—</option><option>Normal</option><option>Low</option><option>High</option></select></td>
      <td class="center"><button class="btn btn--sm btn--danger row-del" title="Remove"><i class="bi bi-x"></i></button></td>`,
  };

  /* ============================ DASHBOARD ============================== */
  function dashboard() {
    return `<div class="screen">
      ${head('bi-speedometer2', 'Admin Dashboard &amp; Analytics', 'Live from API · ' + new Date().toDateString(),
        `<button class="btn btn--ghost btn--sm" data-act="refresh"><i class="bi bi-arrow-clockwise"></i> Refresh <span class="fk">F5</span></button>
         <button class="btn btn--primary btn--sm" data-act="print"><i class="bi bi-printer"></i> Export <span class="fk">F12</span></button>`)}
      <div class="kpis" id="kpis"><div class="muted" style="padding:12px">Loading KPIs…</div></div>

      <div class="cols-side mt12">
        <div>
          <div class="panel">
            <div class="panel__head"><i class="bi bi-bar-chart"></i> Branch Activity — Today</div>
            <div class="panel__body tight">
              <div class="grid-wrap" style="border:0">
              <table class="grid"><thead><tr><th>Service</th><th class="num">Count</th><th class="num">Revenue ₹</th></tr></thead>
              <tbody id="svcActivity">${emptyRow(3, 'Loading…')}</tbody>
              <tfoot><tr><td>Total</td><td class="num" id="svcCount">—</td><td class="num" id="svcRevenue">—</td></tr></tfoot>
              </table></div>
            </div>
          </div>
        </div>
        <div class="panel">
          <div class="panel__head"><i class="bi bi-bell"></i> Alerts &amp; Tasks</div>
          <div class="panel__body tight"><div class="alist" id="alerts">
            <div class="aitem"><i class="ai-ico ico-info bi bi-hourglass-split"></i><div class="a-txt"><b>Loading alerts…</b><span>Fetching live operational signals</span></div></div>
          </div></div>
        </div>
      </div>
    </div>`;
  }

  /* ============================ REGISTRATION ========================== */
  function registration() {
    return `<div class="screen">
      ${head('bi-person-vcard', 'Patient Registration &amp; UHID', 'New OPD/IPD registration · Aadhaar / ABHA linked',
        `<button class="btn btn--ghost btn--sm" data-act="fhir-export"><i class="bi bi-diagram-3"></i> Export FHIR R4</button>`)}
      <div class="cols-side">
        <div>
          <div class="panel">
            <div class="panel__head"><i class="bi bi-person"></i> Demographics</div>
            <div class="panel__body">
              <div class="form-grid">
                <div class="f"><label>UHID</label><div class="field"><input class="ctl code ro" id="regUhid" value="(auto on save)" readonly tabindex="-1"></div></div>
                <div class="f"><label>Reg. Date/Time</label><div class="field"><input class="ctl ro" id="regDate" readonly tabindex="-1"></div></div>
                <div class="f"><label>Full Name <span class="req">*</span></label><div class="field"><input class="ctl" id="regName" placeholder="First Middle Last"></div></div>
                <div class="f"><label>Father / Spouse</label><div class="field"><input class="ctl" id="regGuardian" placeholder="Guardian name"></div></div>
                <div class="f"><label>Age <span class="req">*</span></label><div class="field with-unit"><input class="ctl" id="regAge" style="width:70px"><span class="unit">Yrs</span></div></div>
                <div class="f"><label>Date of Birth</label><div class="field"><input class="ctl" type="date"></div></div>
                <div class="f"><label>Sex <span class="req">*</span></label><div class="field"><select class="ctl" id="regSex"><option value="">—</option><option>Female</option><option>Male</option><option>Other</option></select></div></div>
                <div class="f"><label>Blood Group</label><div class="field"><select class="ctl" id="regBlood"><option value="">Unknown</option><option>A+</option><option>B+</option><option>O+</option><option>AB+</option><option>A-</option><option>B-</option><option>O-</option><option>AB-</option></select></div></div>
                <div class="f"><label>Mobile <span class="req">*</span></label><div class="field"><input class="ctl" id="regMobile" placeholder="10-digit mobile" maxlength="10"></div></div>
                <div class="f"><label>Email</label><div class="field"><input class="ctl" id="regEmail" type="email" placeholder="name@email"></div></div>
                <div class="f"><label>Category</label><div class="field"><select class="ctl"><option>General (Cash)</option><option>Insurance / Cashless</option><option>PM-JAY</option><option>ESIC</option><option>Corporate / Industrial</option></select></div></div>
                <div class="f"><label>Employer / Company</label><div class="field with-btn"><input class="ctl" data-lookup="payer" placeholder="F3 corporate / payer…"><button class="lk" data-lookup="payer">F3</button></div></div>
              </div>
            </div>
          </div>

          <div class="panel">
            <div class="panel__head"><i class="bi bi-fingerprint"></i> Identity — Aadhaar / ABHA &amp; ABDM</div>
            <div class="panel__body">
              <div class="form-grid">
                <div class="f"><label>Aadhaar (masked)</label><div class="field with-btn"><input class="ctl code" placeholder="XXXX-XXXX-1234"><button class="btn btn--sm" data-act="otp" style="border-radius:0 3px 3px 0"><i class="bi bi-shield-lock"></i> OTP</button></div></div>
                <div class="f"><label>ABHA Number</label><div class="field with-btn"><input class="ctl code" placeholder="14-digit ABHA"><button class="btn btn--sm" data-act="verify" style="border-radius:0 3px 3px 0">Verify</button></div></div>
                <div class="f"><label>ABHA Address</label><div class="field"><input class="ctl" placeholder="name@abdm"></div></div>
                <div class="f"><label>Consent</label><div class="field"><select class="ctl"><option>Not captured</option><option>Care context linking — granted</option></select></div></div>
              </div>
            </div>
          </div>

          <div class="panel">
            <div class="panel__head"><i class="bi bi-clock-history"></i> Visit History (all branches)</div>
            <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
              <thead><tr><th>Date</th><th>Branch</th><th>Type</th><th>Doctor</th><th>Diagnosis</th><th>Payer</th></tr></thead>
              <tbody id="visitHistory">${emptyRow(6, 'No prior visits — look up an existing patient (F3) to load history')}</tbody>
            </table></div></div>
          </div>
        </div>

        <div>
          <div class="panel">
            <div class="panel__head"><i class="bi bi-lightning-charge"></i> Quick Actions</div>
            <div class="panel__body" style="display:grid;gap:6px">
              <button class="btn btn--primary" data-act="save"><i class="bi bi-save"></i> Register &amp; Generate UHID <span class="fk">F9</span></button>
              <button class="btn" id="regClear"><i class="bi bi-x-circle"></i> Clear / New</button>
              <button class="btn"><i class="bi bi-printer"></i> Print UHID Card</button>
            </div>
          </div>
        </div>
      </div>

      <div class="panel">
        <div class="panel__head"><i class="bi bi-people"></i> Registered Patients — this hospital
          <span class="ph-right"><input class="ctl" id="patSearch" placeholder="Search UHID / name / mobile…" style="width:220px;display:inline-block"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>UHID</th><th>Name</th><th>Age/Sex</th><th>Blood</th><th>Mobile</th><th>Registered</th><th></th></tr></thead>
          <tbody id="patientsBody">${emptyRow(7, 'Loading…')}</tbody>
        </table></div></div>
      </div>

      <div class="panel" id="patientHistory" hidden>
        <div class="panel__head"><i class="bi bi-clock-history"></i> Consultation history <span class="ph-right muted" id="phWho"></span></div>
        <div class="panel__body" id="phBody"></div>
      </div>
    </div>`;
  }

  /* ============================ APPOINTMENTS ========================== */
  function appointments() {
    return `<div class="screen">
      ${head('bi-calendar-check', 'Appointment &amp; Token Management', 'Doctor-wise scheduling · token &amp; queue',
        `<button class="btn btn--ghost btn--sm"><i class="bi bi-whatsapp"></i> Send Reminders</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-ticket-detailed"></i> Book &amp; Token <span class="fk">F9</span></button>`)}
      <div class="panel"><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Specialty / Department</label><div class="field"><select class="ctl" id="apptDept"><option value="">All departments</option></select></div></div>
          <div class="f"><label>Doctor <span class="req">*</span></label><div class="field"><select class="ctl" id="apptDoctor"><option value="">— select doctor —</option></select></div></div>
          <div class="f"><label>Date</label><div class="field"><input class="ctl" id="apptDate" type="date"></div></div>
          <div class="f"><label>Patient</label><div class="field with-btn"><input class="ctl" id="apptPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
          <div class="f"><label>Visit Type</label><div class="field"><select class="ctl" id="apptVisit"><option>New</option><option>Follow-up</option><option>Review</option></select></div></div>
          <div class="f"><label>Mode</label><div class="field"><select class="ctl" id="apptMode"><option>Walk-in</option><option>Online</option><option>Tele-consult</option></select></div></div>
        </div>
        <div class="flex gap6 mt8" style="align-items:center;flex-wrap:wrap">
          <button class="btn btn--primary" data-act="save"><i class="bi bi-ticket-detailed"></i> Book &amp; Generate Token <span class="fk">F9</span></button>
          <span class="pill pill--muted" id="apptSelSlot"><i class="bi bi-clock"></i> No slot picked — defaults to 09:00</span>
        </div>
      </div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-clock"></i> Available Slots <span class="ph-right muted" id="apptSlotHint">pick a doctor &amp; date</span></div>
          <div class="panel__body"><div id="apptSlots" style="display:grid;grid-template-columns:repeat(auto-fill,minmax(120px,1fr));gap:6px"><span class="muted">—</span></div></div>
        </div>
        <div class="panel"><div class="panel__head"><i class="bi bi-list-ol"></i> Today's Queue</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Token</th><th>Patient</th><th>Doctor</th><th>Status</th><th></th></tr></thead>
            <tbody id="apptQueue">${emptyRow(5, 'Loading…')}</tbody>
          </table></div></div>
        </div>
      </div>
      <div class="panel">
        <div class="panel__head"><i class="bi bi-calendar2-week"></i> Upcoming Appointments
          <span class="ph-right"><label class="muted" style="font-size:12px;cursor:pointer"><input type="checkbox" id="apptFuOnly"> Follow-ups only</label></span>
        </div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Date &amp; Time</th><th>Token</th><th>Patient</th><th>Doctor</th><th>Department</th><th>Type</th><th>Status</th></tr></thead>
          <tbody id="apptUpcoming">${emptyRow(7, 'Loading…')}</tbody>
        </table></div></div>
      </div>
      <div class="panel" id="vitalsStation" hidden><div class="panel__head"><i class="bi bi-heart-pulse"></i> Vitals Station <span class="ph-right muted" id="vsWho"></span></div>
        <div class="panel__body">
          <div class="form-grid three">
            <div class="f"><label>Temp</label><div class="field with-unit"><input class="ctl" id="vsTemp"><span class="unit">°F</span></div></div>
            <div class="f"><label>Pulse</label><div class="field with-unit"><input class="ctl" id="vsPulse"><span class="unit">/min</span></div></div>
            <div class="f"><label>BP</label><div class="field with-unit"><input class="ctl" id="vsBp" placeholder="120/80"><span class="unit">mmHg</span></div></div>
            <div class="f"><label>SpO₂</label><div class="field with-unit"><input class="ctl" id="vsSpo2"><span class="unit">%</span></div></div>
            <div class="f"><label>Resp. Rate</label><div class="field with-unit"><input class="ctl" id="vsResp"><span class="unit">/min</span></div></div>
            <div class="f"><label>Weight</label><div class="field with-unit"><input class="ctl" id="vsWeight"><span class="unit">kg</span></div></div>
          </div>
          <div class="flex gap6 mt8"><button class="btn btn--primary" id="vsSave"><i class="bi bi-check2-circle"></i> Save Vitals &amp; Send to Doctor</button><button class="btn" id="vsCancel">Cancel</button></div>
        </div></div>
    </div>`;
  }

  /* ============================ OPD =================================== */
  function opd() {
    const p = HIS.mock.currentPatient;
    return `<div class="screen">
      ${head('bi-clipboard2-pulse', 'OPD Consultation', 'Doctor waiting lobby · consultation &amp; prescription')}
      <div class="panel"><div class="panel__head"><i class="bi bi-people"></i> Waiting Lobby — vitals done
        <span class="ph-right"><input class="ctl" id="opdLobbyDoctor" data-lookup="doctor" placeholder="F3 your doctor code…" style="width:170px;display:inline-block"><button class="lk" data-lookup="doctor">F3</button></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Token</th><th>Patient</th><th>UHID</th><th>Status</th><th></th></tr></thead>
          <tbody id="opdLobby">${emptyRow(5, 'Enter your doctor code to load your queue')}</tbody>
        </table></div></div></div>
      <div id="opdBanner">${banner(p)}</div>
      <div>
        <div class="itabs">
          <div class="itab active" data-tab="vit">Vitals</div>
          <div class="itab" data-tab="dx">Complaints &amp; Diagnosis</div>
          <div class="itab" data-tab="rx">Prescription</div>
          <div class="itab" data-tab="ord">Lab / Imaging Orders</div>
          <div class="itab" data-tab="fu">Follow-up</div>
        </div>

        <div data-pane="vit">
          <div class="panel"><div class="panel__head"><i class="bi bi-heart-pulse"></i> Vitals</div><div class="panel__body">
            <div class="form-grid three">
              <div class="f"><label>Specialty / Department</label><div class="field"><select class="ctl" id="opdDept"><option value="">All departments</option></select></div></div>
              <div class="f"><label>Consultant <span class="req">*</span></label><div class="field"><select class="ctl" id="opdDoctor"><option value="">— select doctor —</option></select></div></div>
              <div class="f"><label>Temp</label><div class="field with-unit"><input class="ctl" id="opdTemp"><span class="unit">°F</span></div></div>
              <div class="f"><label>Pulse</label><div class="field with-unit"><input class="ctl" id="opdPulse"><span class="unit">/min</span></div></div>
              <div class="f"><label>BP</label><div class="field with-unit"><input class="ctl" id="opdBp" placeholder="120/80"><span class="unit">mmHg</span></div></div>
              <div class="f"><label>SpO₂</label><div class="field with-unit"><input class="ctl" id="opdSpo2"><span class="unit">%</span></div></div>
              <div class="f"><label>Resp. Rate</label><div class="field with-unit"><input class="ctl" id="opdResp"><span class="unit">/min</span></div></div>
              <div class="f"><label>Weight</label><div class="field with-unit"><input class="ctl" id="opdWeight"><span class="unit">kg</span></div></div>
            </div></div></div>
          <div class="panel" id="deptTplPanel" hidden><div class="panel__head"><i class="bi bi-clipboard2-pulse"></i> <span id="deptTplTitle">Department template</span></div>
            <div class="panel__body"><div class="form-grid three" id="deptTplFields"></div></div></div>
        </div>

        <div data-pane="dx" hidden>
          <div class="panel"><div class="panel__head"><i class="bi bi-journal-medical"></i> Complaints &amp; Diagnosis</div><div class="panel__body">
            <div class="form-grid one">
              <div class="f"><label>Chief Complaints</label><div class="field"><textarea class="ctl" id="opdComplaints" placeholder="e.g. Fever × 3 days, cough, body ache"></textarea></div></div>
              <div class="f"><label>History</label><div class="field"><textarea class="ctl" id="opdHistory" placeholder="HPI / past history / allergies"></textarea></div></div>
            </div>
            <div class="subhead mt12">Provisional Diagnosis (ICD-10)</div>
            <div class="form-grid">
              <div class="f"><label>Diagnosis 1</label><div class="field with-btn"><input class="ctl" id="opdDx1" data-lookup="icd10" placeholder="F3 ICD-10…"><button class="lk" data-lookup="icd10">F3</button></div></div>
              <div class="f"><label>Diagnosis 2</label><div class="field with-btn"><input class="ctl" id="opdDx2" data-lookup="icd10" placeholder="F3 ICD-10…"><button class="lk" data-lookup="icd10">F3</button></div></div>
            </div>
          </div></div>
        </div>

        <div data-pane="rx" hidden>
          <div class="panel"><div class="panel__head"><i class="bi bi-capsule"></i> Prescription <span class="ph-right"><button class="btn btn--sm" data-addrow="rxGrid"><i class="bi bi-plus-lg"></i> Add Drug</button></span></div>
            <div class="panel__body tight"><div class="grid-wrap grid--editable" style="border:0"><table class="grid" id="rxGrid">
              <thead><tr><th style="width:30%">Drug</th><th>Dose</th><th>Freq</th><th>Days</th><th>Route</th><th class="num">Qty</th><th></th></tr></thead>
              <tbody id="rxBody"><tr>${TPL.rxBody}</tr></tbody></table></div>
              <p class="hintline" style="padding:8px 12px">Type a code and press <b>F3</b> in the Drug column to look up · <b>Enter</b> moves across the row.</p>
            </div></div>
        </div>

        <div data-pane="ord" hidden>
          <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-check"></i> Investigations &amp; Imaging</div><div class="panel__body">
            <div class="grid-2">
              <div><div class="subhead">Laboratory</div><div class="checklist">
                <label><input type="checkbox"> CBC — Complete Blood Count</label>
                <label><input type="checkbox"> CRP</label>
                <label><input type="checkbox"> LFT</label>
                <label><input type="checkbox"> RFT / Electrolytes</label>
              </div></div>
              <div><div class="subhead">Radiology</div><div class="checklist">
                <label><input type="checkbox"> Chest X-Ray PA view</label>
                <label><input type="checkbox"> CT Thorax</label>
                <label><input type="checkbox"> USG Abdomen</label>
                <label><input type="checkbox"> ECG</label>
              </div></div>
            </div>
          </div></div>
        </div>

        <div data-pane="fu" hidden>
          <div class="panel"><div class="panel__head"><i class="bi bi-arrow-repeat"></i> Follow-up &amp; Advice</div><div class="panel__body">
            <div class="form-grid">
              <div class="f"><label>Follow-up Date</label><div class="field"><input class="ctl" id="opdFollowup" type="date"></div></div>
              <div class="f"><label>Referral</label><div class="field with-btn"><input class="ctl" data-lookup="doctor" placeholder="F3 refer to…"><button class="lk" data-lookup="doctor">F3</button></div></div>
              <div class="f wide"><label>Advice</label><div class="field"><textarea class="ctl" id="opdAdvice"></textarea></div></div>
            </div>
            <div class="muted" style="font-size:12px;margin-top:6px"><i class="bi bi-ticket-detailed"></i> Pick a date to auto-issue a follow-up appointment token on save.</div>
            <div id="opdFollowupResult" class="pill" style="display:none;margin-top:8px;background:#e7f6ec;color:#137333;font-weight:600"></div>
            <div class="flex gap6 mt8"><button class="btn btn--primary" data-act="save"><i class="bi bi-save"></i> Save Consultation <span class="fk">F9</span></button><button class="btn"><i class="bi bi-printer"></i> Print Prescription</button></div>
          </div></div>
        </div>
      </div>
    </div>`;
  }

  /* ============================ IPD =================================== */
  function ipd() {
    const p = HIS.mock.currentPatient;
    return `<div class="screen">
      ${head('bi-hospital', 'IPD Admission &amp; Bed Board', 'Ward management · transfers · discharge',
        `<button class="btn btn--ghost btn--sm"><i class="bi bi-arrow-left-right"></i> Transfer</button>
         <button class="btn btn--ghost btn--sm"><i class="bi bi-box-arrow-right"></i> Discharge</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-save"></i> Admit <span class="fk">F9</span></button>`)}
      ${banner(p)}
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-grid-3x3"></i> Bed Board — live
          <span class="ph-right legend-row">
            <span><i class="sw" style="background:var(--ok-bg);border:1px solid #bfe3cc"></i>Free</span>
            <span><i class="sw" style="background:#fdf1ee;border:1px solid #f1c9bf"></i>Occupied</span>
            <span><i class="sw" style="background:var(--warn-bg);border:1px solid #ecd4a3"></i>Cleaning</span>
            <span><i class="sw" style="background:var(--bg-2)"></i>Blocked</span>
          </span></div>
          <div class="panel__body"><div class="bedboard" id="bedboard"><div class="muted">Loading beds…</div></div></div>
        </div>
        <div class="panel"><div class="panel__head"><i class="bi bi-clipboard-plus"></i> Admission</div><div class="panel__body">
          <div class="form-grid one">
            <div class="f"><label>Admission No.</label><div class="field"><input class="ctl code ro" id="ipdAdmNo" value="(auto on admit)" readonly tabindex="-1"></div></div>
            <div class="f"><label>Ward / Bed <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="ipdBed" data-lookup="ward" placeholder="F3 bed…"><button class="lk" data-lookup="ward">F3</button></div></div>
            <div class="f"><label>Consultant <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="ipdConsultant" data-lookup="doctor" placeholder="F3 consultant…"><button class="lk" data-lookup="doctor">F3</button></div></div>
            <div class="f"><label>Provisional Dx</label><div class="field with-btn"><input class="ctl" id="ipdDx" data-lookup="icd10" placeholder="F3 ICD-10…"><button class="lk" data-lookup="icd10">F3</button></div></div>
            <div class="f"><label>Admission Type</label><div class="field"><select class="ctl" id="ipdAdmType"><option>Planned</option><option>Emergency</option><option>Day Care</option><option>Transfer-in</option></select></div></div>
            <div class="f"><label>Payment Class</label><div class="field"><select class="ctl" id="ipdPayClass"><option>Cashless / Insurance</option><option>PM-JAY</option><option>ESIC</option><option>Cash</option><option>Corporate</option></select></div></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-hospital"></i> Confirm Admission <span class="fk">F9</span></button>
        </div></div>
      </div>
    </div>`;
  }

  /* ============================ BILLING =============================== */
  function billing() {
    const p = HIS.mock.currentPatient;
    return `<div class="screen">
      ${head('bi-receipt', 'Billing &amp; Revenue Cycle', 'OPD/IPD/Lab/Pharmacy consolidated invoice',
        `<button class="btn btn--ghost btn--sm" data-act="print"><i class="bi bi-printer"></i> Print Bill <span class="fk">F12</span></button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-cash-coin"></i> Collect &amp; Save <span class="fk">F9</span></button>`)}
      ${banner(p)}
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-list-ul"></i> Charges
          <span class="ph-right"><button class="btn btn--sm" data-addrow="chargeGrid"><i class="bi bi-plus-lg"></i> Add Line</button></span></div>
          <div class="panel__body tight"><div class="grid-wrap grid--editable" style="border:0"><table class="grid" id="chargeGrid">
            <thead><tr><th style="width:48%">Service / Item</th><th class="center">Qty</th><th class="num">Rate ₹</th><th class="num">Amount ₹</th><th></th></tr></thead>
            <tbody id="chargeBody"><tr>${TPL.chargeBody}</tr></tbody>
            <tfoot><tr><td colspan="3">Gross Total</td><td class="num" id="grossTot">0.00</td><td></td></tr></tfoot>
          </table></div>
          <p class="hintline" style="padding:8px 12px">Add lines from the tariff master · totals compute live.</p>
          </div>
        </div>
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-calculator"></i> Summary</div><div class="panel__body">
            <div class="form-grid one" style="gap:6px">
              <div class="f"><label>Gross</label><div class="field"><input class="ctl num ro" id="sumGross" value="0.00" readonly tabindex="-1"></div></div>
              <div class="f"><label>Discount</label><div class="field with-unit"><input class="ctl num" id="billDiscount" value="0"><span class="unit">₹</span></div></div>
              <div class="f"><label>Insurance Pays</label><div class="field with-unit"><input class="ctl num" id="billInsurance" value="0"><span class="unit">₹</span></div></div>
              <div class="f"><label>Net Payable (Patient)</label><div class="field"><input class="ctl num ro" id="billPayable" value="0.00" readonly tabindex="-1"></div></div>
            </div>
            <div class="flex gap6 mt8"><button class="btn" id="btnCreateBill" style="width:100%"><i class="bi bi-receipt"></i> Create Bill (F9)</button></div>
          </div></div>
          <div class="panel"><div class="panel__head"><i class="bi bi-wallet2"></i> Payment <span class="ph-right muted" id="payBillRef">no bill</span></div><div class="panel__body">
            <div class="form-grid one" style="gap:6px">
              <div class="f"><label>Mode</label><div class="field"><select class="ctl" id="payMode"><option>UPI</option><option>Card</option><option>NetBanking</option><option>QR</option><option>Cash</option></select></div></div>
              <div class="f"><label>Amount</label><div class="field"><input class="ctl num" id="payAmount" value="0.00"></div></div>
            </div>
            <div class="flex gap6 mt8"><button class="btn btn--primary" id="btnCollectPay" style="flex:1"><i class="bi bi-check2-circle"></i> Collect Payment</button></div>
            <p class="hintline" style="padding:8px 0 0">Gateway provider is configured server-side (no keys in the UI).</p>
          </div></div>
        </div>
      </div>
    </div>`;
  }

  /* ============================ PHARMACY ============================= */
  function pharmacy() {
    return `<div class="screen">
      ${head('bi-capsule', 'Pharmacy Management', 'Prescription-based dispensing · batch &amp; expiry tracking',
        `<button class="btn btn--ghost btn--sm" data-act="print"><i class="bi bi-printer"></i> Print Bill <span class="fk">F12</span></button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-bag-check"></i> Dispense &amp; Bill <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-pulse"></i> Prescription Queue <span class="ph-right muted" id="pharmaSel">no Rx selected</span></div>
            <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
              <thead><tr><th>Rx #</th><th>Patient</th><th>Doctor</th><th>Items</th><th>Status</th></tr></thead>
              <tbody id="pharmaQueue">${emptyRow(5, 'Loading…')}</tbody>
            </table></div></div>
          </div>
          <div class="panel"><div class="panel__head"><i class="bi bi-box-seam"></i> Dispense
            <span class="ph-right"><button class="btn btn--sm" data-addrow="dispGrid"><i class="bi bi-plus-lg"></i> Add Item</button></span></div>
            <div class="panel__body tight"><div class="grid-wrap grid--editable" style="border:0"><table class="grid" id="dispGrid">
              <thead><tr><th style="width:28%">Drug</th><th>Batch</th><th>Expiry</th><th class="num">Qty</th><th class="num">MRP ₹</th><th class="num">Amount ₹</th><th></th></tr></thead>
              <tbody id="dispBody"><tr>${TPL.dispBody}</tr></tbody></table></div>
              <p class="hintline" style="padding:8px 12px">Stock auto-deducts on dispense · expiry &amp; batch validated against inventory.</p>
            </div></div>
        </div>
        <div class="panel"><div class="panel__head"><i class="bi bi-exclamation-triangle"></i> Stock Alerts</div>
          <div class="panel__body tight"><div class="alist" id="pharmaAlerts">
            <div class="aitem"><i class="ai-ico ico-info bi bi-box-seam"></i><div class="a-txt"><b>Loading…</b></div></div>
          </div></div>
        </div>
      </div>
    </div>`;
  }

  /* ============================ LAB (LIS) ============================ */
  function lab() {
    return `<div class="screen">
      ${head('bi-eyedropper', 'Laboratory Information System (LIS)', 'Order worklist · sample tracking · result entry',
        `<button class="btn btn--ghost btn--sm" id="btnLabOrder"><i class="bi bi-plus-circle"></i> New Order</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-check2-all"></i> Validate &amp; Release <span class="fk">F9</span></button>`)}
      <div class="panel"><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="labPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
          <div class="f"><label>Test <span class="req">*</span></label><div class="field"><input class="ctl" id="labTest" placeholder="e.g. CBC, CRP, Lipid Profile"></div></div>
          <div class="f"><label>&nbsp;</label><div class="field"><button class="btn btn--primary" id="btnLabOrder2" style="width:100%"><i class="bi bi-upc-scan"></i> Create Order &amp; Barcode</button></div></div>
        </div>
      </div></div>
      <div class="cols-side">
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-list-task"></i> Order Worklist <span class="ph-right muted" id="labSel">no order selected</span></div>
            <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
              <thead><tr><th>Barcode</th><th>Patient</th><th>Test</th><th>Status</th></tr></thead>
              <tbody id="labWorklist">${emptyRow(4, 'Loading…')}</tbody>
            </table></div></div>
          </div>
          <div class="panel"><div class="panel__head"><i class="bi bi-clipboard-data"></i> Result Entry
            <span class="ph-right"><button class="btn btn--sm" data-addrow="labResultsGrid"><i class="bi bi-plus-lg"></i> Add Parameter</button></span></div>
            <div class="panel__body tight"><div class="grid-wrap grid--editable" style="border:0"><table class="grid" id="labResultsGrid">
              <thead><tr><th>Parameter</th><th>Result</th><th>Unit</th><th>Reference</th><th>Flag</th><th></th></tr></thead>
              <tbody id="labResultsBody"><tr>${TPL.labResultBody}</tr></tbody>
            </table></div>
            <p class="hintline" style="padding:8px 12px">Select an order in the worklist, enter results, then <b>F9</b> to validate &amp; release.</p>
            </div></div>
        </div>
        <div class="panel"><div class="panel__head"><i class="bi bi-upc"></i> Sample Tracking</div>
          <div class="panel__body tight"><div class="alist">
            <div class="aitem"><i class="ai-ico ico-info bi bi-arrow-right-circle"></i><div class="a-txt"><b>Collected → Received → Result Entry → Released</b><span>Status updates live as you act</span></div></div>
          </div></div>
        </div>
      </div>
    </div>`;
  }

  /* ============================ CASHLESS / TPA ====================== */
  function cashless() {
    const p = HIS.mock.currentPatient;
    return `<div class="screen">
      ${head('bi-credit-card-2-front', 'Cashless / TPA Claims', 'Pre-auth → query → enhancement → final bill → settlement',
        `<button class="btn btn--ghost btn--sm"><i class="bi bi-cpu"></i> AI Pre-Scrub</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-send"></i> Submit Pre-Auth <span class="fk">F9</span></button>`)}
      ${banner(p)}
      <div class="panel"><div class="panel__body">
        <div class="pipeline">
          <div class="pstep"><div class="b">1</div><div class="t">Eligibility</div></div>
          <div class="pstep"><div class="b">2</div><div class="t">Pre-Auth</div></div>
          <div class="pstep"><div class="b">Q</div><div class="t">Query / Shortfall</div></div>
          <div class="pstep"><div class="b">E</div><div class="t">Enhancement</div></div>
          <div class="pstep"><div class="b">B</div><div class="t">Final Bill</div></div>
          <div class="pstep"><div class="b">₹</div><div class="t">Settlement</div></div>
        </div>
      </div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-shield-check"></i> Payer &amp; Eligibility <span class="ph-right"><button class="btn btn--sm" id="btnCapturePolicy"><i class="bi bi-save"></i> Capture Policy</button></span></div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Payer / TPA <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="caPayer" data-lookup="payer" placeholder="F3 payer…"><button class="lk" data-lookup="payer">F3</button></div></div>
            <div class="f"><label>Policy / Member ID</label><div class="field"><input class="ctl code" id="caPolicy" placeholder="Policy / member no."></div></div>
            <div class="f"><label>Sum Insured</label><div class="field with-unit"><input class="ctl num" id="caSumInsured" placeholder="—"><span class="unit">₹</span></div></div>
            <div class="f"><label>Co-pay</label><div class="field with-unit"><input class="ctl num" id="caCopay" placeholder="—"><span class="unit">%</span></div></div>
          </div>
          <div class="mt8" id="caEligNote"></div>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-file-medical"></i> Pre-Authorisation</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Provisional Dx</label><div class="field with-btn"><input class="ctl" id="caDx" data-lookup="icd10" placeholder="F3 ICD-10…"><button class="lk" data-lookup="icd10">F3</button></div></div>
            <div class="f"><label>Est. Cost</label><div class="field with-unit"><input class="ctl num" id="caCost" placeholder="0"><span class="unit">₹</span></div></div>
            <div class="f wide"><label>Clinical Notes</label><div class="field"><textarea class="ctl" id="caNotes" placeholder="Clinical justification for admission…"></textarea></div></div>
          </div>
        </div></div>
      </div>
      <div class="panel"><div class="panel__head"><i class="bi bi-kanban"></i> Claim Tracking Dashboard</div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Claim #</th><th>Patient</th><th>Payer</th><th class="num">Pre-Auth ₹</th><th class="num">Approved ₹</th><th>Status</th></tr></thead>
          <tbody id="caClaims">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div>
      </div>
    </div>`;
  }

  /* ============================ PM-JAY =============================== */
  function pmjay() {
    return `<div class="screen">
      ${head('bi-bank2', 'Ayushman Bharat PM-JAY', 'BIS beneficiary verification · HBP packages · TMS claims',
        `<button class="btn btn--ghost btn--sm"><i class="bi bi-fingerprint"></i> Aadhaar Verify</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-send"></i> Submit to TMS <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-search"></i> BIS Beneficiary Search</div><div class="panel__body">
            <div class="form-grid">
              <div class="f"><label>Search By</label><div class="field"><select class="ctl"><option>Aadhaar e-KYC</option><option>PM-JAY ID</option><option>Ration Card</option><option>Mobile</option></select></div></div>
              <div class="f"><label>ID Value</label><div class="field with-btn"><input class="ctl code" id="pmId" placeholder="PM-JAY ID / Aadhaar e-KYC"><button class="btn btn--sm" id="btnPmVerify" style="border-radius:0 3px 3px 0"><i class="bi bi-search"></i></button></div></div>
            </div>
            <div class="mt8" id="pmVerifyNote"></div>
          </div></div>
          <div class="panel"><div class="panel__head"><i class="bi bi-box2-heart"></i> Health Benefit Package (HBP)</div><div class="panel__body">
            <div class="form-grid">
              <div class="f"><label>Package <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="pmPackage" data-lookup="package" placeholder="F3 HBP package…"><button class="lk" data-lookup="package">F3</button></div></div>
              <div class="f"><label>Ayushman Mitra</label><div class="field"><input class="ctl" id="pmMitra" placeholder="Mitra name / code"></div></div>
            </div>
          </div></div>
        </div>
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-diagram-2"></i> TMS Claim</div><div class="panel__body" style="display:grid;gap:6px">
            <div class="flex between"><span class="muted">TMS Case ID</span><b id="pmTms">—</b></div>
            <div class="flex between"><span class="muted">Stage</span><span class="pill pill--muted" id="pmStage">Not started</span></div>
            <button class="btn btn--primary mt8" data-act="save"><i class="bi bi-send"></i> Submit to TMS <span class="fk">F9</span></button>
            <button class="btn"><i class="bi bi-fingerprint"></i> Aadhaar Discharge Verify</button>
          </div></div>
        </div>
      </div>
    </div>`;
  }

  /* ============================ HR (SRS §3.17) ====================== */
  function hr() {
    return `<div class="screen">
      ${head('bi-people', 'HR Management', 'Staff master · attendance · leave',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-person-plus"></i> Add Staff <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-person-vcard"></i> Add Staff</div><div class="panel__body">
            <div class="form-grid">
              <div class="f"><label>Employee Code <span class="req">*</span></label><div class="field"><input class="ctl code" id="hrCode" placeholder="EMP-00X"></div></div>
              <div class="f"><label>Full Name <span class="req">*</span></label><div class="field"><input class="ctl" id="hrName" placeholder="Name"></div></div>
              <div class="f"><label>Designation</label><div class="field"><input class="ctl" id="hrDesig" placeholder="e.g. Staff Nurse"></div></div>
              <div class="f"><label>Department</label><div class="field"><input class="ctl" id="hrDept" placeholder="e.g. Nursing"></div></div>
              <div class="f"><label>Date of Joining</label><div class="field"><input class="ctl" id="hrDoj" type="date"></div></div>
            </div>
          </div></div>
          <div class="panel"><div class="panel__head"><i class="bi bi-people-fill"></i> Staff <span class="ph-right muted" id="hrSel">click a row to select</span></div>
            <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
              <thead><tr><th>Code</th><th>Name</th><th>Designation</th><th>Department</th></tr></thead>
              <tbody id="hrStaff">${emptyRow(4, 'Loading…')}</tbody>
            </table></div></div>
          </div>
        </div>
        <div class="panel"><div class="panel__head"><i class="bi bi-calendar-check"></i> Attendance</div><div class="panel__body">
          <div class="form-grid one" style="gap:6px">
            <div class="f"><label>Employee</label><div class="field"><input class="ctl code" id="attCode" placeholder="EMP code (or click staff)"></div></div>
            <div class="f"><label>Date</label><div class="field"><input class="ctl" id="attDate" type="date"></div></div>
            <div class="f"><label>Status</label><div class="field"><select class="ctl" id="attStatus"><option>Present</option><option>Absent</option><option>Half-day</option><option>Leave</option></select></div></div>
            <div class="f"><label>In / Out</label><div class="field"><input class="ctl" id="attIn" placeholder="09:00" style="width:70px;margin-right:6px"><input class="ctl" id="attOut" placeholder="17:30" style="width:70px"></div></div>
          </div>
          <button class="btn btn--primary mt8" id="btnMarkAtt" style="width:100%"><i class="bi bi-check2"></i> Mark Attendance</button>
          <div class="grid-wrap mt8" style="border:0"><table class="grid">
            <thead><tr><th>Code</th><th>Name</th><th>Status</th><th>In-Out</th></tr></thead>
            <tbody id="attList">${emptyRow(4, 'Pick a date')}</tbody>
          </table></div>
        </div></div>
      </div>
    </div>`;
  }

  /* ============================ PAYROLL (SRS §3.18) ================= */
  function payroll() {
    const now = new Date();
    return `<div class="screen">
      ${head('bi-cash-coin', 'Payroll &amp; Overtime', 'Salary processing · overtime · supervisor approval',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-calculator"></i> Run Payroll <span class="fk">F9</span></button>`)}
      <div class="panel"><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Employee <span class="req">*</span></label><div class="field"><input class="ctl code" id="prCode" placeholder="EMP code"></div></div>
          <div class="f"><label>Year</label><div class="field"><input class="ctl" id="prYear" value="${now.getFullYear()}"></div></div>
          <div class="f"><label>Month</label><div class="field"><input class="ctl" id="prMonth" value="${now.getMonth() + 1}"></div></div>
          <div class="f"><label>Basic Pay</label><div class="field with-unit"><input class="ctl num" id="prBasic" placeholder="0"><span class="unit">₹</span></div></div>
          <div class="f"><label>Overtime Hours</label><div class="field with-unit"><input class="ctl num" id="prOt" placeholder="0"><span class="unit">hrs</span></div></div>
          <div class="f"><label>&nbsp;</label><div class="field"><button class="btn btn--primary" id="btnRunPayroll" style="width:100%"><i class="bi bi-calculator"></i> Run</button></div></div>
        </div>
        <p class="hintline">Overtime rate &amp; PF % are configured server-side (not hardcoded). Net = Basic + OT − PF.</p>
      </div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-table"></i> Payroll — <span id="prPeriod">this month</span>
        <span class="ph-right" id="prTotals"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Code</th><th>Name</th><th class="num">Basic</th><th class="num">OT hrs</th><th class="num">OT ₹</th><th class="num">Net ₹</th><th>Status</th><th></th></tr></thead>
          <tbody id="prList">${emptyRow(8, 'Loading…')}</tbody>
        </table></div></div>
      </div>
    </div>`;
  }

  /* ====================== OCCUPATIONAL HEALTH (SRS §3.23) =========== */
  function occhealth() {
    return `<div class="screen">
      ${head('bi-hospital', 'Occupational Health &amp; Industrial Medicine', 'PEME / PME · fitness · hazard &amp; injury register',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-clipboard2-check"></i> Save Exam <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div>
          <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-pulse"></i> Medical Examination (PEME / PME)</div><div class="panel__body">
            <div class="form-grid">
              <div class="f"><label>Worker (Patient) <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="ohPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
              <div class="f"><label>Company Contract</label><div class="field"><select class="ctl" id="ohContract"><option value="">—</option></select></div></div>
              <div class="f"><label>Exam Type <span class="req">*</span></label><div class="field"><select class="ctl" id="ohType"><option>PEME</option><option>PME</option></select></div></div>
              <div class="f"><label>Exam Date</label><div class="field"><input class="ctl" id="ohDate" type="date"></div></div>
              <div class="f"><label>Fitness Result</label><div class="field"><select class="ctl" id="ohFit"><option value="">—</option><option>Fit</option><option>Unfit</option><option>Fit-with-conditions</option></select></div></div>
              <div class="f"><label>Audiometry</label><div class="field"><input class="ctl" id="ohAudio" placeholder="e.g. Normal / NIHL"></div></div>
              <div class="f"><label>Spirometry</label><div class="field"><input class="ctl" id="ohSpiro" placeholder="e.g. Normal"></div></div>
              <div class="f"><label>Vision</label><div class="field"><input class="ctl" id="ohVision" placeholder="e.g. 6/6"></div></div>
              <div class="f wide"><label>Vaccination Notes</label><div class="field"><input class="ctl" id="ohVacc" placeholder="e.g. Tetanus booster"></div></div>
            </div>
          </div></div>
          <div class="panel"><div class="panel__head"><i class="bi bi-list-check"></i> Recent Examinations</div>
            <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
              <thead><tr><th>Worker</th><th>Company</th><th>Type</th><th>Date</th><th>Fitness</th></tr></thead>
              <tbody id="ohExams">${emptyRow(5, 'Loading…')}</tbody>
            </table></div></div>
          </div>
        </div>
        <div class="panel"><div class="panel__head"><i class="bi bi-bandaid"></i> Workplace Injury Register</div><div class="panel__body">
          <div class="form-grid one" style="gap:6px">
            <div class="f"><label>Worker</label><div class="field with-btn"><input class="ctl" id="injPatient" data-lookup="patient" placeholder="F3 patient…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Injury Date/Time</label><div class="field"><input class="ctl" id="injDate" type="datetime-local"></div></div>
            <div class="f"><label>MLC Linked</label><div class="field"><select class="ctl" id="injMlc"><option value="false">No</option><option value="true">Yes — link MLC</option></select></div></div>
            <div class="f"><label>Description</label><div class="field"><textarea class="ctl" id="injDesc" placeholder="Injury details"></textarea></div></div>
          </div>
          <button class="btn btn--primary mt8" id="btnRecordInjury" style="width:100%"><i class="bi bi-plus-circle"></i> Record Injury</button>
          <div class="grid-wrap mt8" style="border:0"><table class="grid">
            <thead><tr><th>Worker</th><th>Date</th><th>MLC</th></tr></thead>
            <tbody id="ohInjuries">${emptyRow(3, '—')}</tbody>
          </table></div>
        </div></div>
      </div>
    </div>`;
  }

  /* ====================== TELEMEDICINE (SRS §3.24) ================= */
  function telemedicine() {
    return `<div class="screen">
      ${head('bi-camera-video', 'Telemedicine &amp; Teleconsultation', 'Secure consult · consent · e-Rx (TPG 2020)',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-calendar-plus"></i> Schedule <span class="fk">F9</span></button>`)}
      <div class="panel"><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="tmPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
          <div class="f"><label>Doctor <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="tmDoctor" data-lookup="doctor" placeholder="F3 doctor…"><button class="lk" data-lookup="doctor">F3</button></div></div>
          <div class="f"><label>Type</label><div class="field"><select class="ctl" id="tmType"><option>Video</option><option>Audio</option><option>Tele-ICU</option><option>Tele-Radiology</option></select></div></div>
          <div class="f"><label>Scheduled</label><div class="field"><input class="ctl" id="tmWhen" type="datetime-local"></div></div>
          <div class="f"><label>&nbsp;</label><div class="field"><button class="btn btn--primary" id="btnSchedTele" style="width:100%"><i class="bi bi-calendar-plus"></i> Schedule Session</button></div></div>
        </div>
        <p class="hintline">Per TPG 2020, patient consent must be captured before an e-prescription can be signed.</p>
      </div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-camera-video"></i> Sessions</div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>#</th><th>Patient</th><th>Doctor</th><th>Type</th><th>Scheduled</th><th>Consent</th><th>e-Rx</th><th>Status</th><th>Actions</th></tr></thead>
          <tbody id="tmList">${emptyRow(9, 'Loading…')}</tbody>
        </table></div></div>
      </div>
    </div>`;
  }

  /* ====================== AMBULANCE (SRS §3.6) ===================== */
  function ambulance() {
    return `<div class="screen">
      ${head('bi-truck-front', 'Ambulance &amp; GPS', 'Emergency call logging · nearest dispatch',
        `<button class="btn btn--danger btn--sm" id="btnDispatch"><i class="bi bi-broadcast"></i> Dispatch Nearest</button>`)}
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-truck-front"></i> Fleet</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Vehicle</th><th>Status</th></tr></thead><tbody id="ambFleet">${emptyRow(2, 'Loading…')}</tbody>
          </table></div></div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-geo-alt"></i> Dispatches</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>#</th><th>Vehicle</th><th>Logged</th><th>Arrived</th><th>Status</th><th></th></tr></thead>
            <tbody id="ambDispatches">${emptyRow(6, 'No dispatches')}</tbody>
          </table></div></div></div>
      </div>
      <div class="panel mt12"><div class="panel__head"><i class="bi bi-geo"></i> Live GPS Tracking
        <span id="gpsLive" class="pill pill--warn" style="margin-left:auto"><i class="bi bi-broadcast"></i> Connecting…</span>
        <button class="btn btn--sm" id="gpsSim" style="margin-left:8px"><i class="bi bi-play-fill"></i> Simulate</button></div>
        <div class="panel__body">
          <div id="gpsMap" style="position:relative;height:240px;border:1px solid var(--line);border-radius:8px;overflow:hidden;
               background:repeating-linear-gradient(0deg,transparent,transparent 23px,var(--line-2) 24px),repeating-linear-gradient(90deg,transparent,transparent 23px,var(--line-2) 24px),var(--bg-2)">
            <div class="muted" id="gpsHint" style="position:absolute;inset:0;display:grid;place-items:center">Awaiting GPS pings — press Simulate</div>
          </div>
          <div class="grid-wrap mt8" style="border:0"><table class="grid">
            <thead><tr><th>Vehicle</th><th class="num">Lat</th><th class="num">Lng</th><th class="num">Speed</th><th>Updated</th></tr></thead>
            <tbody id="gpsTable">${emptyRow(5, 'Awaiting GPS pings…')}</tbody>
          </table></div>
        </div></div>
    </div>`;
  }

  /* ====================== BIO-MEDICAL WASTE (SRS §3.25) ============ */
  function bmwm() {
    return `<div class="screen">
      ${head('bi-trash3', 'Bio-Medical Waste Management', 'Colour-coded bags · CBWTF handover · Form-IV (BMWM 2016)',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-upc-scan"></i> Generate Bag <span class="fk">F9</span></button>`)}
      <div class="panel"><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Barcode <span class="req">*</span></label><div class="field"><input class="ctl code" id="bwBarcode" placeholder="BAG-xxxx"></div></div>
          <div class="f"><label>Colour Code <span class="req">*</span></label><div class="field"><select class="ctl" id="bwColour"><option>Yellow</option><option>Red</option><option>White</option><option>Blue</option></select></div></div>
          <div class="f"><label>Weight (kg)</label><div class="field"><input class="ctl num" id="bwWeight" placeholder="0.0"></div></div>
        </div>
      </div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-bag"></i> Bags</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Barcode</th><th>Colour</th><th class="num">Kg</th><th>Handover</th></tr></thead>
            <tbody id="bwBags">${emptyRow(4, 'Loading…')}</tbody>
          </table></div></div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-file-earmark-bar-graph"></i> Form-IV Summary</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Colour</th><th class="num">Bags</th><th class="num">Total Kg</th></tr></thead>
            <tbody id="bwFormIv">${emptyRow(3, '—')}</tbody>
          </table></div></div></div>
      </div>
    </div>`;
  }

  /* ====================== MLC (SRS §3.28) ========================= */
  function mlc() {
    return `<div class="screen">
      ${head('bi-shield-fill-exclamation', 'Medico-Legal Case (MLC)', 'Auto MLC no. · police intimation · chain-of-custody',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-plus-circle"></i> Create MLC <span class="fk">F9</span></button>`)}
      <div class="panel"><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Patient</label><div class="field with-btn"><input class="ctl" id="mlcPatient" data-lookup="patient" placeholder="F3 patient…"><button class="lk" data-lookup="patient">F3</button></div></div>
          <div class="f"><label>Police Station</label><div class="field"><input class="ctl" id="mlcPs" placeholder="e.g. Hazratganj PS"></div></div>
          <div class="f wide"><label>Injury Details</label><div class="field"><input class="ctl" id="mlcInjury" placeholder="Nature of injury / incident"></div></div>
        </div>
      </div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-journal-text"></i> MLC Register</div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>MLC No.</th><th>Patient</th><th>Police Station</th><th>Police Ack</th><th>Created</th><th></th></tr></thead>
          <tbody id="mlcList">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
    </div>`;
  }

  /* ====================== QUEUE (SRS §3.31) ======================= */
  function queue() {
    return `<div class="screen">
      ${head('bi-display', 'Queue &amp; Digital Signage', 'Token queues · counter calling',
        `<button class="btn btn--ghost btn--sm" data-act="refresh"><i class="bi bi-arrow-clockwise"></i> Refresh</button>`)}
      <div class="panel"><div class="panel__head"><i class="bi bi-ui-radios-grid"></i> Counters</div>
        <div class="panel__body" id="qCounters"><span class="muted">Loading…</span></div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-list-ol"></i> Live Board
        <span id="qLive" class="pill pill--warn" style="margin-left:auto"><i class="bi bi-broadcast"></i> Connecting…</span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Area</th><th>Counter</th><th>Token</th><th>Status</th></tr></thead>
          <tbody id="qBoard">${emptyRow(4, 'Loading…')}</tbody>
        </table></div></div></div>
    </div>`;
  }

  /* ====================== FEEDBACK & GRIEVANCE (SRS §3.30) ======== */
  function feedback() {
    return `<div class="screen">
      ${head('bi-chat-square-heart', 'Feedback &amp; Grievance', 'NABH surveys · grievance SLA',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-send"></i> Submit Survey <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-star"></i> Satisfaction Survey</div><div class="panel__body">
          <div class="form-grid one" style="gap:6px">
            <div class="f"><label>Patient</label><div class="field with-btn"><input class="ctl" id="fbPatient" data-lookup="patient" placeholder="F3 patient (optional)…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Score (1–5)</label><div class="field"><select class="ctl" id="fbScore"><option>5</option><option>4</option><option>3</option><option>2</option><option>1</option></select></div></div>
            <div class="f"><label>Comments</label><div class="field"><textarea class="ctl" id="fbComments"></textarea></div></div>
          </div>
          <div class="subhead mt12">Log Grievance</div>
          <div class="form-grid one" style="gap:6px">
            <div class="f"><label>Category</label><div class="field"><input class="ctl" id="grCategory" placeholder="e.g. Billing delay"></div></div>
          </div>
          <button class="btn mt8" id="btnLogGrievance" style="width:100%"><i class="bi bi-exclamation-circle"></i> Log Grievance (SLA from config)</button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-list-task"></i> Grievances</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Category</th><th>Status</th><th>Created</th></tr></thead>
            <tbody id="grList">${emptyRow(3, 'Loading…')}</tbody>
          </table></div></div></div>
      </div>
    </div>`;
  }

  /* ============================ PLACEHOLDER ========================= */
  HIS.placeholder = function (m) {
    const bullets = (HIS.srs[m.id] || ['Module screen scoped in SRS v2.0.', 'Detailed form &amp; workflow planned for the next build pass.'])
      .map(b => `<li><i class="bi bi-check2-circle"></i><span>${b}</span></li>`).join('');
    return `<div class="screen placeholder">
      ${head(m.icon, m.label, 'SRS module · navigable in this wireframe',
        `<button class="btn btn--ghost btn--sm" data-act="print"><i class="bi bi-printer"></i> Print</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-save"></i> Save <span class="fk">F9</span></button>`)}
      <span class="ribbon"><i class="bi bi-cone-striped"></i> Wireframe · detailed form planned for next build pass</span>
      <div class="panel"><div class="panel__head"><i class="bi bi-card-checklist"></i> Scope &amp; Capabilities (from SRS v2.0)</div>
        <div class="panel__body"><ul class="srs">${bullets}</ul></div></div>
    </div>`;
  };

  /* ============================ Registry ============================ */
  HIS.screens = { dashboard, registration, appointments, opd, ipd, billing, pharmacy, lab, cashless, pmjay, hr, payroll, occhealth, telemedicine, ambulance, bmwm, mlc, queue, feedback, compliance, ai };

  /* Per-screen Save handlers — invoked by the toolbar/F9 Save (see shell.js). */
  HIS.saveHandlers = HIS.saveHandlers || {};

  /* ====================== COMPLIANCE & AUDIT (SRS §3.22) ========= */
  function compliance() {
    return `<div class="screen">
      ${head('bi-shield-check', 'Compliance &amp; Audit', 'Immutable audit trail · every action logged (SRS §8.1)',
        `<button class="btn btn--ghost btn--sm" data-act="refresh"><i class="bi bi-arrow-clockwise"></i> Refresh <span class="fk">F5</span></button>
         <button class="btn btn--primary btn--sm" data-act="print"><i class="bi bi-printer"></i> Export <span class="fk">F12</span></button>`)}
      <div class="kpis" id="cmpKpis"><div class="muted" style="padding:12px">Loading…</div></div>
      <div class="panel mt12"><div class="panel__head"><i class="bi bi-list-columns-reverse"></i> Audit Trail — most recent
        <span id="cmpCount" class="pill pill--warn" style="margin-left:auto">—</span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Time (UTC)</th><th>User</th><th>Action</th><th>Entity</th><th>Ref</th><th>Result</th></tr></thead>
          <tbody id="cmpTrail">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
    </div>`;
  }
  async function loadCompliance(doc) {
    const tb = doc.querySelector('#cmpTrail'); if (!tb) return;
    try {
      const rows = await HIS.api.auditTrail(100);
      tb.innerHTML = rows.length ? rows.map(r => {
        const t = new Date(r.occurredUtc);
        const when = isNaN(t) ? r.occurredUtc : t.toISOString().slice(0, 19).replace('T', ' ');
        const ok = r.succeeded;
        return `<tr><td class="tnum">${when}</td><td>${r.user || '—'}</td><td>${r.action}</td><td>${r.entity}</td><td>${r.entityId || '—'}</td>
          <td><span class="pill ${ok ? 'pill--ok' : 'pill--danger'}">${ok ? 'OK' : 'Failed'}</span></td></tr>`;
      }).join('') : emptyRow(6, 'No audit entries yet');
      doc.querySelector('#cmpCount').textContent = `${rows.length} shown`;
      // Compliance summary KPIs derived from the trail.
      const total = rows.length, failed = rows.filter(r => !r.succeeded).length;
      const actors = new Set(rows.map(r => r.user).filter(Boolean)).size;
      doc.querySelector('#cmpKpis').innerHTML =
        `<div class="kpi"><div class="v tnum">${total}</div><div class="l">Entries shown</div></div>
         <div class="kpi"><div class="v tnum">${total - failed}</div><div class="l">Successful</div></div>
         <div class="kpi"><div class="v tnum">${failed}</div><div class="l">Failed / denied</div></div>
         <div class="kpi"><div class="v tnum">${actors}</div><div class="l">Distinct users</div></div>`;
    } catch (e) {
      tb.innerHTML = emptyRow(6, 'Audit API unavailable');
      doc.querySelector('#cmpKpis').innerHTML = '<div class="muted" style="padding:12px">Audit API unavailable</div>';
    }
  }

  /* ====================== AI — Clinical Risk (SRS §4.1) ========== */
  function ai() {
    return `<div class="screen">
      ${head('bi-cpu', 'AI Suite', 'Explainable AI assists (SRS §4) · risk · forecasting · claim pre-scrub')}
      <div class="itabs">
        <div class="itab active" data-tab="risk">Risk Prediction</div>
        <div class="itab" data-tab="fc">Inventory Forecast</div>
        <div class="itab" data-tab="ps">Claim Pre-Scrub</div>
      </div>

      <div data-pane="risk"><div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-pulse"></i> Vitals</div><div class="panel__body">
          <div class="form-grid" style="gap:8px">
            <div class="f"><label>Respiratory rate (/min)</label><div class="field"><input class="ctl" id="aiRr" type="number" value="28"></div></div>
            <div class="f"><label>SpO₂ (%)</label><div class="field"><input class="ctl" id="aiSpo2" type="number" value="90"></div></div>
            <div class="f"><label>Temperature (°C)</label><div class="field"><input class="ctl" id="aiTemp" type="number" step="0.1" value="39.5"></div></div>
            <div class="f"><label>Systolic BP (mmHg)</label><div class="field"><input class="ctl" id="aiSbp" type="number" value="88"></div></div>
            <div class="f"><label>Heart rate (bpm)</label><div class="field"><input class="ctl" id="aiHr" type="number" value="125"></div></div>
            <div class="f"><label>Consciousness</label><div class="field"><select class="ctl" id="aiCon"><option>Alert</option><option>Confused</option><option>Voice</option><option>Pain</option><option>Unresponsive</option></select></div></div>
          </div>
          <button class="btn btn--primary mt12" id="aiCompute" style="width:100%"><i class="bi bi-cpu"></i> Compute Risk Score</button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-activity"></i> Assessment</div><div class="panel__body">
          <div id="aiResult"><div class="muted" style="padding:12px">Enter vitals and compute.</div></div>
        </div></div>
      </div></div>

      <div data-pane="fc" hidden>
        <div class="panel"><div class="panel__head"><i class="bi bi-box-seam"></i> Demand Forecast &amp; Reorder
          <button class="btn btn--primary btn--sm" id="aiRunForecast" style="margin-left:auto"><i class="bi bi-graph-up-arrow"></i> Run Forecast</button></div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Code</th><th>Item</th><th class="num">Stock</th><th class="num">Avg/day</th><th class="num">Days cover</th><th class="num">Suggest order</th><th>Urgency</th></tr></thead>
            <tbody id="aiFcBody">${emptyRow(7, 'Run the forecast to project reorder needs')}</tbody>
          </table></div></div></div>
      </div>

      <div data-pane="ps" hidden><div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-check"></i> Claim Details</div><div class="panel__body">
          <div class="form-grid one" style="gap:8px">
            <div class="f"><label>Package code</label><div class="field"><input class="ctl" id="psPkg" value="CD-014" placeholder="e.g. CD-014"></div></div>
            <div class="f"><label>Claimed amount (₹)</label><div class="field"><input class="ctl" id="psAmt" type="number" value="70000"></div></div>
            <div class="f"><label>Patient UHID (optional)</label><div class="field"><input class="ctl" id="psUhid" placeholder="optional — enables policy checks"></div></div>
            <div class="f"><label>Documents attached</label><div class="field" id="psDocs" style="display:flex;flex-wrap:wrap;gap:10px;padding:6px 0">
              <label><input type="checkbox" value="Discharge Summary" checked> Discharge Summary</label>
              <label><input type="checkbox" value="Final Bill" checked> Final Bill</label>
              <label><input type="checkbox" value="ID Proof"> ID Proof</label>
              <label><input type="checkbox" value="Pre-Auth Approval"> Pre-Auth Approval</label>
            </div></div>
          </div>
          <button class="btn btn--primary mt12" id="aiRunPreScrub" style="width:100%"><i class="bi bi-shield-check"></i> Pre-Scrub Claim</button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-list-check"></i> Pre-Submission Checks</div><div class="panel__body">
          <div id="psResult"><div class="muted" style="padding:12px">Enter claim details and pre-scrub.</div></div>
        </div></div>
      </div></div>
    </div>`;
  }
  function initAi(doc) {
    const rb = doc.querySelector('#aiCompute'); if (rb) rb.addEventListener('click', () => computeRisk(doc));
    const fb = doc.querySelector('#aiRunForecast'); if (fb) fb.addEventListener('click', () => runForecast(doc));
    const pb = doc.querySelector('#aiRunPreScrub'); if (pb) pb.addEventListener('click', () => runPreScrub(doc));
  }
  async function runForecast(doc) {
    const tb = doc.querySelector('#aiFcBody');
    tb.innerHTML = emptyRow(7, 'Forecasting…');
    const band = { 'Critical': 'pill--danger', 'High': 'pill--warn', 'Monitor': 'pill--ok' };
    try {
      const rows = await HIS.api.aiForecast();
      tb.innerHTML = rows.length ? rows.map(r =>
        `<tr><td>${r.code}</td><td>${r.name}</td><td class="num">${r.stock}</td><td class="num">${r.avgDailyUse}</td>
          <td class="num">${r.daysOfCover}</td><td class="num"><b>${r.suggestedOrderQty}</b></td>
          <td><span class="pill ${band[r.urgency] || 'pill--muted'}">${r.urgency}</span></td></tr>`).join('') : emptyRow(7, 'No stock items');
    } catch (e) { tb.innerHTML = emptyRow(7, 'Forecast API error'); }
  }
  async function runPreScrub(doc) {
    const docs = Array.from(doc.querySelectorAll('#psDocs input:checked')).map(c => c.value);
    const input = { packageCode: val(doc, 'psPkg') || null, claimedAmount: Number(val(doc, 'psAmt')) || 0, patientUhid: val(doc, 'psUhid') || null, documents: docs };
    const host = doc.querySelector('#psResult');
    host.innerHTML = '<div class="muted" style="padding:12px">Scrubbing…</div>';
    const sev = { pass: 'pill--ok', warn: 'pill--warn', fail: 'pill--danger' };
    const verdictBand = { Clean: 'pill--ok', Review: 'pill--warn', Reject: 'pill--danger' };
    try {
      const r = await HIS.api.aiPreScrub(input);
      host.innerHTML =
        `<div class="kpis"><div class="kpi"><div class="v"><span class="pill ${verdictBand[r.verdict] || 'pill--muted'}" style="font-size:14px">${r.verdict}</span></div><div class="l">Verdict</div></div>
           <div class="kpi"><div class="v tnum">${r.passed}</div><div class="l">Passed</div></div>
           <div class="kpi"><div class="v tnum">${r.warnings}</div><div class="l">Warnings</div></div>
           <div class="kpi"><div class="v tnum">${r.failures}</div><div class="l">Failures</div></div></div>
         <div class="grid-wrap mt12" style="border:0"><table class="grid"><thead><tr><th>Result</th><th>Rule</th><th>Detail</th></tr></thead>
         <tbody>${r.checks.map(c => `<tr><td><span class="pill ${sev[c.severity] || 'pill--muted'}">${c.severity}</span></td><td>${c.rule}</td><td>${c.detail}</td></tr>`).join('')}</tbody></table></div>`;
    } catch (e) { host.innerHTML = '<div class="muted" style="padding:12px">Pre-scrub API error: ' + e.message + '</div>'; }
  }
  async function computeRisk(doc) {
    const num = id => { const v = doc.querySelector('#' + id).value; return v === '' ? null : Number(v); };
    const vitals = { respiratoryRate: num('aiRr'), spO2: num('aiSpo2'), temperatureC: num('aiTemp'), systolicBp: num('aiSbp'), heartRate: num('aiHr'), consciousness: doc.querySelector('#aiCon').value };
    const host = doc.querySelector('#aiResult');
    host.innerHTML = '<div class="muted" style="padding:12px">Scoring…</div>';
    try {
      const r = await HIS.api.aiRisk(vitals);
      const band = { 'High': 'pill--danger', 'Medium': 'pill--warn', 'Low-Medium': 'pill--info', 'Low': 'pill--ok' }[r.band] || 'pill--muted';
      host.innerHTML =
        `<div class="kpis"><div class="kpi"><div class="v tnum">${r.score}</div><div class="l">Aggregate score</div></div>
           <div class="kpi"><div class="v"><span class="pill ${band}" style="font-size:14px">${r.band}</span></div><div class="l">Risk band</div></div></div>
         <div class="mt12" style="padding:8px 2px"><b>Recommendation:</b> ${r.recommendation}</div>
         <div class="grid-wrap mt8" style="border:0"><table class="grid"><thead><tr><th>Parameter</th><th class="num">Points</th><th>Value</th></tr></thead>
         <tbody>${r.flags.length ? r.flags.map(f => `<tr><td>${f.parameter}</td><td class="num">${f.points}</td><td>${f.note}</td></tr>`).join('') : emptyRow(3, 'All parameters in normal range')}</tbody></table></div>
         <div class="muted mt8" style="font-size:11px">Model: ${r.model}</div>`;
    } catch (e) { host.innerHTML = '<div class="muted" style="padding:12px">Risk API error: ' + e.message + '</div>'; }
  }

  /* ============================ afterRender ========================= */
  HIS.afterRender = function (id, doc) {
    const map = { rxBody: TPL.rxBody, chargeBody: TPL.chargeBody, dispBody: TPL.dispBody, labResultsBody: TPL.labResultBody };
    Object.keys(map).forEach(tb => { const el = doc.querySelector('#' + tb); if (el) el.dataset.tpl = map[tb]; });

    if (id === 'billing') { wireBilling(doc); HIS.saveHandlers.billing = () => doCreateBill(doc);
      const cb = doc.querySelector('#btnCreateBill'); if (cb) cb.addEventListener('click', () => doCreateBill(doc));
      const cp = doc.querySelector('#btnCollectPay'); if (cp) cp.addEventListener('click', () => doCollectPayment(doc)); }
    if (id === 'dashboard') loadDashboard(doc);
    if (id === 'registration') { initRegistration(doc); HIS.saveHandlers.registration = () => doRegister(doc); }
    if (id === 'ipd') { loadBedBoard(doc); HIS.saveHandlers.ipd = () => doAdmit(doc); }
    if (id === 'appointments') { initAppointments(doc); HIS.saveHandlers.appointments = () => doBookAppointment(doc); }
    if (id === 'opd') { initOpd(doc); HIS.saveHandlers.opd = () => doSaveConsultation(doc); }
    if (id === 'lab') { initLab(doc); HIS.saveHandlers.lab = () => doEnterResults(doc); }
    if (id === 'pharmacy') { initPharmacy(doc); HIS.saveHandlers.pharmacy = () => doDispense(doc); }
    if (id === 'cashless') { initCashless(doc); HIS.saveHandlers.cashless = () => doSubmitPreAuth(doc); }
    if (id === 'pmjay') { initPmjay(doc); HIS.saveHandlers.pmjay = () => doSubmitTms(doc); }
    if (id === 'hr') { initHr(doc); HIS.saveHandlers.hr = () => doAddStaff(doc); }
    if (id === 'payroll') { initPayroll(doc); HIS.saveHandlers.payroll = () => doRunPayroll(doc); }
    if (id === 'occhealth') { initOccHealth(doc); HIS.saveHandlers.occhealth = () => doConductExam(doc); }
    if (id === 'telemedicine') { initTele(doc); HIS.saveHandlers.telemedicine = () => doScheduleTele(doc); }
    if (id === 'ambulance') { initAmbulance(doc); }
    if (id === 'bmwm') { initBmwm(doc); HIS.saveHandlers.bmwm = () => doGenerateBag(doc); }
    if (id === 'mlc') { initMlc(doc); HIS.saveHandlers.mlc = () => doCreateMlc(doc); }
    if (id === 'queue') { initQueue(doc); }
    if (id === 'feedback') { initFeedback(doc); HIS.saveHandlers.feedback = () => doSubmitSurvey(doc); }
    if (id === 'compliance') loadCompliance(doc);
    if (id === 'ai') initAi(doc);
  };

  /* ---- Phase 10: ambulance ------------------------------------------- */
  function initAmbulance(doc) {
    loadFleet(doc); loadDispatches(doc); initGps(doc);
    const b = doc.querySelector('#btnDispatch'); if (b) b.addEventListener('click', () => doDispatch(doc));
  }

  /* ---- Ambulance live GPS tracking over SignalR (task 0.9) ---- */
  let gpsHubDoc = null, gpsFleet = {}, gpsConn = null;
  function setGpsLive(doc, on) {
    const el = doc.querySelector('#gpsLive'); if (!el) return;
    el.className = 'pill ' + (on ? 'pill--ok' : 'pill--warn');
    el.innerHTML = `<i class="bi bi-broadcast"></i> ${on ? 'Live' : 'Reconnecting…'}`;
  }
  async function initGps(doc) {
    gpsHubDoc = doc;
    try { const fleet = await HIS.api.ambulances(); gpsFleet = {}; fleet.forEach(a => gpsFleet[a.ambulanceId] = a.vehicleNo); } catch (e) {}
    if (window.signalR && !gpsConn) {
      try {
        gpsConn = new signalR.HubConnectionBuilder()
          .withUrl((window.HIS_API_BASE || '') + '/hubs/gps').withAutomaticReconnect().build();
        gpsConn.on('ambulanceMoved', d => { if (gpsHubDoc && document.body.contains(gpsHubDoc)) plotGps(gpsHubDoc, d); });
        gpsConn.onreconnecting(() => gpsHubDoc && setGpsLive(gpsHubDoc, false));
        gpsConn.onreconnected(() => gpsHubDoc && setGpsLive(gpsHubDoc, true));
        gpsConn.start().then(() => gpsHubDoc && setGpsLive(gpsHubDoc, true)).catch(() => {});
      } catch (e) {}
    } else if (gpsConn && gpsConn.state === 'Connected') { setGpsLive(doc, true); }
    const sim = doc.querySelector('#gpsSim'); if (sim) sim.addEventListener('click', () => simulateGps(doc));
  }
  function plotGps(doc, d) {
    const map = doc.querySelector('#gpsMap'); if (!map) return;
    const hint = doc.querySelector('#gpsHint'); if (hint) hint.style.display = 'none';
    const label = gpsFleet[d.ambulanceId] || ('Amb #' + d.ambulanceId);
    let dot = map.querySelector(`[data-amb="${d.ambulanceId}"]`);
    if (!dot) {
      dot = document.createElement('div'); dot.dataset.amb = d.ambulanceId;
      dot.style.cssText = 'position:absolute;width:14px;height:14px;margin:-7px 0 0 -7px;border-radius:50%;background:var(--danger);box-shadow:0 0 0 4px rgba(220,53,69,.25);transition:left .8s linear,top .8s linear;z-index:2';
      const tag = document.createElement('span');
      tag.style.cssText = 'position:absolute;left:13px;top:-4px;white-space:nowrap;font-size:10px;font-weight:700;color:var(--ink)';
      tag.textContent = label; dot.appendChild(tag); map.appendChild(dot);
    }
    dot.style.left = d.x + '%'; dot.style.top = (100 - d.y) + '%';
    const tb = doc.querySelector('#gpsTable'); if (!tb) return;
    if (tb.querySelector('td[colspan]')) tb.innerHTML = '';
    let row = tb.querySelector(`tr[data-amb="${d.ambulanceId}"]`);
    if (!row) { row = document.createElement('tr'); row.dataset.amb = d.ambulanceId; tb.appendChild(row); }
    const t = new Date(d.ts); const hhmmss = isNaN(t) ? '' : t.toISOString().slice(11, 19);
    row.innerHTML = `<td>${label}</td><td class="num">${Number(d.lat).toFixed(4)}</td><td class="num">${Number(d.lng).toFixed(4)}</td><td class="num">${d.speedKmph != null ? d.speedKmph + ' km/h' : '—'}</td><td class="tnum">${hhmmss}</td>`;
  }
  async function simulateGps(doc) {
    const ids = Object.keys(gpsFleet).map(Number).slice(0, 3);
    if (!ids.length) { HIS.toast('No ambulances to track'); return; }
    HIS.toast('Simulating GPS movement…', 'bi-broadcast');
    const pos = {}; ids.forEach(id => { pos[id] = { lat: 26.70 + Math.random() * 0.25, lng: 80.85 + Math.random() * 0.25 }; });
    for (let step = 0; step < 8; step++) {
      await Promise.all(ids.map(id => {
        pos[id].lat = Math.min(26.95, Math.max(26.70, pos[id].lat + (Math.random() - 0.5) * 0.03));
        pos[id].lng = Math.min(81.10, Math.max(80.85, pos[id].lng + (Math.random() - 0.5) * 0.03));
        return HIS.api.ambLocation(id, { lat: +pos[id].lat.toFixed(5), lng: +pos[id].lng.toFixed(5), speedKmph: Math.round(20 + Math.random() * 40) }).catch(() => {});
      }));
      await new Promise(r => setTimeout(r, 900));
    }
  }
  async function loadFleet(doc) {
    const tb = doc.querySelector('#ambFleet'); if (!tb) return;
    try { const rows = await HIS.api.ambulances();
      tb.innerHTML = rows.length ? rows.map(a => `<tr><td>${a.vehicleNo}</td><td><span class="pill ${a.status === 'Available' ? 'pill--ok' : 'pill--warn'}">${a.status}</span></td></tr>`).join('') : emptyRow(2, 'No ambulances');
    } catch (e) { tb.innerHTML = emptyRow(2, 'API unavailable'); }
  }
  async function loadDispatches(doc) {
    const tb = doc.querySelector('#ambDispatches'); if (!tb) return;
    try { const rows = await HIS.api.ambDispatches();
      tb.innerHTML = rows.length ? rows.map(d => `<tr><td>${d.dispatchId}</td><td>${d.vehicle}</td><td>${d.logged}</td><td>${d.arrived || '—'}</td><td><span class="pill ${d.status === 'Arrived' ? 'pill--ok' : 'pill--warn'}">${d.status}</span></td><td>${d.status === 'Arrived' ? '✓' : `<button class="btn btn--sm" data-arrive="${d.dispatchId}">Arrived</button>`}</td></tr>`).join('') : emptyRow(6, 'No dispatches');
      tb.querySelectorAll('[data-arrive]').forEach(b => b.addEventListener('click', async () => { try { await HIS.api.ambArrive(b.dataset.arrive); HIS.toast('Marked arrived'); loadFleet(doc); loadDispatches(doc); } catch (e) { HIS.toast(e.message); } }));
    } catch (e) { tb.innerHTML = emptyRow(6, 'API unavailable'); }
  }
  async function doDispatch(doc) {
    try { const r = await HIS.api.ambDispatch({ pickupLat: null, pickupLng: null }); HIS.toast('Dispatched · ambulance #' + r.ambulanceId, 'bi-broadcast'); loadFleet(doc); loadDispatches(doc); }
    catch (e) { HIS.toast('Dispatch failed: ' + e.message); }
  }

  /* ---- Phase 10: BMWM ------------------------------------------------ */
  function initBmwm(doc) { loadBmwm(doc); }
  async function loadBmwm(doc) {
    try {
      const d = await HIS.api.bmwm();
      const bags = doc.querySelector('#bwBags');
      bags.innerHTML = d.bags.length ? d.bags.map(b => `<tr><td>${b.barcode}</td><td><span class="pill pill--info">${b.colour}</span></td><td class="num">${b.weightKg ?? '—'}</td><td>${b.handedOver ? '<span class="pill pill--ok">CBWTF</span>' : '<span class="pill pill--muted">In store</span>'}</td></tr>`).join('') : emptyRow(4, 'No bags');
      const fi = doc.querySelector('#bwFormIv');
      fi.innerHTML = d.formIv.length ? d.formIv.map(f => `<tr><td>${f.colour}</td><td class="num">${f.bags}</td><td class="num">${f.weight}</td></tr>`).join('') : emptyRow(3, '—');
    } catch (e) { doc.querySelector('#bwBags').innerHTML = emptyRow(4, 'API unavailable'); }
  }
  async function doGenerateBag(doc) {
    const bc = val(doc, 'bwBarcode');
    if (!bc) { HIS.toast('Enter a barcode'); return; }
    try { await HIS.api.bmwmBag({ barcode: bc, colourCode: val(doc, 'bwColour'), weightKg: numOrNull(val(doc, 'bwWeight')) }); HIS.toast('Bag generated', 'bi-upc-scan'); doc.querySelector('#bwBarcode').value = ''; loadBmwm(doc); }
    catch (e) { HIS.toast('Generate failed: ' + e.message); }
  }

  /* ---- Phase 10: MLC ------------------------------------------------- */
  function initMlc(doc) { loadMlc(doc); }
  async function loadMlc(doc) {
    const tb = doc.querySelector('#mlcList'); if (!tb) return;
    try { const rows = await HIS.api.mlcList();
      tb.innerHTML = rows.length ? rows.map(m => `<tr><td><b>${m.mlcNo}</b></td><td>${m.patient || '—'}</td><td>${m.policeStation || '—'}</td><td>${m.policeAck || '<span class="pill pill--warn">pending</span>'}</td><td>${m.created}</td><td>${m.policeAck ? '✓' : `<button class="btn btn--sm" data-intimate="${m.mlcId}">Police Ack</button>`}</td></tr>`).join('') : emptyRow(6, 'No MLC cases');
      tb.querySelectorAll('[data-intimate]').forEach(b => b.addEventListener('click', async () => { const ack = prompt('Police acknowledgement ref (e.g. FIR no.):'); if (!ack) return; try { await HIS.api.mlcIntimate(b.dataset.intimate, ack); HIS.toast('Police intimated'); loadMlc(doc); } catch (e) { HIS.toast(e.message); } }));
    } catch (e) { tb.innerHTML = emptyRow(6, 'API unavailable'); }
  }
  async function doCreateMlc(doc) {
    try { const r = await HIS.api.mlcCreate({ patientUhid: val(doc, 'mlcPatient') || null, policeStation: val(doc, 'mlcPs') || null, injuryDetails: val(doc, 'mlcInjury') || null }); HIS.toast('MLC created · ' + r.mlcNo, 'bi-shield-fill-exclamation'); loadMlc(doc); }
    catch (e) { HIS.toast('Create failed: ' + e.message); }
  }

  /* ---- Phase 10: Queue — live board over SignalR (task 0.9) ----------- */
  let queueLiveDoc = null;          // the currently-open queue screen
  let opdLiveDoc = null;            // the currently-open OPD lobby screen
  function ensureQueueHub() {
    if (HIS._queueHub || !window.signalR) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl((window.HIS_API_BASE || '') + '/hubs/queue')
      .withAutomaticReconnect()
      .build();
    // A token issued/called on ANY screen pushes "queueChanged" → re-fetch the board.
    conn.on('queueChanged', () => {
      if (queueLiveDoc && document.body.contains(queueLiveDoc)) {
        loadBoard(queueLiveDoc);
        setQueueLive(queueLiveDoc, true);
      }
    });
    // OPD vitals-done / called / completed → refresh the doctor lobby live.
    conn.on('opdChanged', () => {
      if (opdLiveDoc && document.body.contains(opdLiveDoc)) loadOpdLobby(opdLiveDoc);
    });
    conn.onreconnecting(() => queueLiveDoc && setQueueLive(queueLiveDoc, false));
    conn.onreconnected(() => queueLiveDoc && setQueueLive(queueLiveDoc, true));
    conn.start().then(() => queueLiveDoc && setQueueLive(queueLiveDoc, true)).catch(() => {});
    HIS._queueHub = conn;
  }
  function setQueueLive(doc, on) {
    const el = doc.querySelector('#qLive'); if (!el) return;
    el.className = 'pill ' + (on ? 'pill--ok' : 'pill--warn');
    el.innerHTML = `<i class="bi bi-broadcast"></i> ${on ? 'Live' : 'Reconnecting…'}`;
  }

  async function initQueue(doc) {
    queueLiveDoc = doc;
    ensureQueueHub();
    try {
      const counters = await HIS.api.queueCounters();
      const host = doc.querySelector('#qCounters');
      host.innerHTML = counters.map(c => `<div class="flex gap6 aic" style="margin-bottom:6px"><b style="min-width:120px">${c.area} · ${c.counterName}</b>
        <button class="btn btn--sm" data-issue="${c.counterId}"><i class="bi bi-ticket"></i> Issue Token</button>
        <button class="btn btn--sm btn--primary" data-call="${c.counterId}"><i class="bi bi-megaphone"></i> Call Next</button></div>`).join('') || '<span class="muted">No counters</span>';
      host.querySelectorAll('[data-issue]').forEach(b => b.addEventListener('click', async () => { try { const t = await HIS.api.queueToken(b.dataset.issue, null); HIS.toast('Token ' + t + ' issued', 'bi-ticket'); loadBoard(doc); } catch (e) { HIS.toast(e.message); } }));
      host.querySelectorAll('[data-call]').forEach(b => b.addEventListener('click', async () => { try { const t = await HIS.api.queueCallNext(b.dataset.call); HIS.toast(t ? 'Now calling ' + t : 'Queue empty', 'bi-megaphone'); loadBoard(doc); } catch (e) { HIS.toast(e.message); } }));
    } catch (e) { doc.querySelector('#qCounters').innerHTML = '<span class="muted">API unavailable</span>'; }
    loadBoard(doc);
  }
  async function loadBoard(doc) {
    const tb = doc.querySelector('#qBoard'); if (!tb) return;
    try { const rows = await HIS.api.queueBoard();
      tb.innerHTML = rows.length ? rows.map(r => `<tr><td>${r.area}</td><td>${r.counter}</td><td><b>${r.tokenNo}</b></td><td><span class="pill ${r.status === 'Called' ? 'pill--ok' : 'pill--warn'}">${r.status}</span></td></tr>`).join('') : emptyRow(4, 'No tokens today');
    } catch (e) { tb.innerHTML = emptyRow(4, 'API unavailable'); }
  }

  /* ---- Phase 10: Feedback & Grievance -------------------------------- */
  function initFeedback(doc) {
    loadGrievances(doc);
    const b = doc.querySelector('#btnLogGrievance'); if (b) b.addEventListener('click', () => doLogGrievance(doc));
  }
  async function loadGrievances(doc) {
    const tb = doc.querySelector('#grList'); if (!tb) return;
    try { const rows = await HIS.api.grievances();
      tb.innerHTML = rows.length ? rows.map(g => `<tr><td>${g.category || '—'}</td><td><span class="pill ${g.status === 'Resolved' ? 'pill--ok' : 'pill--warn'}">${g.status}</span></td><td>${g.created}</td></tr>`).join('') : emptyRow(3, 'No grievances');
    } catch (e) { tb.innerHTML = emptyRow(3, 'API unavailable'); }
  }
  async function doSubmitSurvey(doc) {
    try { await HIS.api.submitSurvey({ patientUhid: val(doc, 'fbPatient') || null, score: parseInt(val(doc, 'fbScore'), 10), comments: val(doc, 'fbComments') || null }); HIS.toast('Survey submitted', 'bi-star-fill'); }
    catch (e) { HIS.toast('Submit failed: ' + e.message); }
  }
  async function doLogGrievance(doc) {
    const cat = val(doc, 'grCategory');
    if (!cat) { HIS.toast('Enter a category'); return; }
    try { await HIS.api.logGrievance({ patientUhid: val(doc, 'fbPatient') || null, category: cat }); HIS.toast('Grievance logged (SLA set)', 'bi-exclamation-circle'); doc.querySelector('#grCategory').value = ''; loadGrievances(doc); }
    catch (e) { HIS.toast('Log failed: ' + e.message); }
  }

  /* ---- Phase 9: occupational health ---------------------------------- */
  async function initOccHealth(doc) {
    const d = doc.querySelector('#ohDate'); if (d) d.value = new Date().toISOString().slice(0, 10);
    try {
      const contracts = await HIS.api.occContracts();
      const sel = doc.querySelector('#ohContract');
      if (sel) contracts.forEach(c => sel.insertAdjacentHTML('beforeend', `<option value="${c.contractId}">${c.companyName} (${c.contractType || ''})</option>`));
    } catch (e) { /* ignore */ }
    loadExams(doc); loadInjuries(doc);
    const ib = doc.querySelector('#btnRecordInjury'); if (ib) ib.addEventListener('click', () => doRecordInjury(doc));
  }
  async function loadExams(doc) {
    const tb = doc.querySelector('#ohExams'); if (!tb) return;
    try {
      const rows = await HIS.api.occExams();
      const pill = f => f === 'Unfit' ? 'pill--danger' : f === 'Fit-with-conditions' ? 'pill--warn' : 'pill--ok';
      tb.innerHTML = rows.length ? rows.map(e =>
        `<tr><td>${e.patient}</td><td>${e.company || '—'}</td><td>${e.examType}</td><td>${e.examDate}</td><td>${e.fitness ? `<span class="pill ${pill(e.fitness)}">${e.fitness}</span>` : '—'}</td></tr>`
      ).join('') : emptyRow(5, 'No examinations yet');
    } catch (e) { tb.innerHTML = emptyRow(5, 'Exams API unavailable'); }
  }
  async function doConductExam(doc) {
    const uhid = val(doc, 'ohPatient');
    if (!uhid) { HIS.toast('Select a worker (F3)'); return; }
    const cid = val(doc, 'ohContract');
    try {
      await HIS.api.conductExam({
        patientUhid: uhid, contractId: cid ? parseInt(cid, 10) : null, examType: val(doc, 'ohType'),
        examDate: val(doc, 'ohDate'), fitnessResult: val(doc, 'ohFit') || null,
        audiometry: val(doc, 'ohAudio') || null, spirometry: val(doc, 'ohSpiro') || null,
        vision: val(doc, 'ohVision') || null, vaccinationNotes: val(doc, 'ohVacc') || null
      });
      HIS.toast('Examination saved', 'bi-clipboard2-check'); loadExams(doc);
    } catch (e) { HIS.toast('Save failed: ' + e.message); }
  }
  async function loadInjuries(doc) {
    const tb = doc.querySelector('#ohInjuries'); if (!tb) return;
    try {
      const rows = await HIS.api.occInjuries();
      tb.innerHTML = rows.length ? rows.map(i =>
        `<tr><td>${i.patient}</td><td>${i.injuryDate}</td><td>${i.mlcLinked ? '<span class="pill pill--danger">MLC</span>' : 'No'}</td></tr>`
      ).join('') : emptyRow(3, 'No injuries');
    } catch (e) { tb.innerHTML = emptyRow(3, '—'); }
  }
  async function doRecordInjury(doc) {
    const uhid = val(doc, 'injPatient');
    if (!uhid) { HIS.toast('Select a worker (F3)'); return; }
    try {
      await HIS.api.recordInjury({
        patientUhid: uhid, injuryDate: val(doc, 'injDate') || new Date().toISOString(),
        mlcLinked: val(doc, 'injMlc') === 'true', description: val(doc, 'injDesc') || null
      });
      HIS.toast('Injury recorded', 'bi-bandaid'); loadInjuries(doc);
    } catch (e) { HIS.toast('Record failed: ' + e.message); }
  }

  /* ---- Phase 9: telemedicine ----------------------------------------- */
  function initTele(doc) {
    loadTele(doc);
    const sb = doc.querySelector('#btnSchedTele'); if (sb) sb.addEventListener('click', () => doScheduleTele(doc));
  }
  async function loadTele(doc) {
    const tb = doc.querySelector('#tmList'); if (!tb) return;
    try {
      const rows = await HIS.api.teleList();
      const yn = b => b ? '<span class="pill pill--ok">✓</span>' : '<span class="pill pill--muted">—</span>';
      tb.innerHTML = rows.length ? rows.map(t => {
        let actions = '';
        if (t.status !== 'Completed') {
          if (!t.consent) actions += `<button class="btn btn--sm" data-tele-consent="${t.teleId}">Consent</button> `;
          else if (!t.signed) actions += `<button class="btn btn--sm" data-tele-sign="${t.teleId}">Sign e-Rx</button> `;
          actions += `<button class="btn btn--sm" data-tele-complete="${t.teleId}">Complete</button>`;
        } else actions = '✓';
        return `<tr><td>${t.teleId}</td><td>${t.patient}</td><td>${t.doctor || '—'}</td><td>${t.consultType || '—'}</td><td>${t.scheduled || '—'}</td>
          <td>${yn(t.consent)}</td><td>${yn(t.signed)}</td><td><span class="pill ${t.status === 'Completed' ? 'pill--purple' : 'pill--info'}">${t.status}</span></td><td>${actions}</td></tr>`;
      }).join('') : emptyRow(9, 'No sessions yet');
      tb.querySelectorAll('[data-tele-consent]').forEach(b => b.addEventListener('click', () => teleAct(doc, 'consent', b.dataset.teleConsent)));
      tb.querySelectorAll('[data-tele-sign]').forEach(b => b.addEventListener('click', () => teleAct(doc, 'sign', b.dataset.teleSign)));
      tb.querySelectorAll('[data-tele-complete]').forEach(b => b.addEventListener('click', () => teleAct(doc, 'complete', b.dataset.teleComplete)));
    } catch (e) { tb.innerHTML = emptyRow(9, 'Telemedicine API unavailable'); }
  }
  async function teleAct(doc, action, id) {
    try {
      if (action === 'consent') await HIS.api.teleConsent(id);
      else if (action === 'sign') await HIS.api.teleSign(id);
      else await HIS.api.teleComplete(id);
      HIS.toast(action + ' done', 'bi-check2'); loadTele(doc);
    } catch (e) { HIS.toast(action + ' failed: ' + e.message); }
  }
  async function doScheduleTele(doc) {
    const uhid = val(doc, 'tmPatient'), docCode = val(doc, 'tmDoctor');
    if (!uhid || !docCode) { HIS.toast('Patient and doctor are required'); return; }
    try {
      await HIS.api.teleSchedule({ patientUhid: uhid, doctorCode: docCode, consultType: val(doc, 'tmType'),
        scheduledUtc: val(doc, 'tmWhen') || new Date().toISOString(), toBranchId: null });
      HIS.toast('Teleconsult scheduled', 'bi-calendar-plus'); loadTele(doc);
    } catch (e) { HIS.toast('Schedule failed: ' + e.message); }
  }

  /* ---- Phase 8: HR — staff, attendance ------------------------------- */
  function initHr(doc) {
    const d = doc.querySelector('#attDate'); if (d) d.value = new Date().toISOString().slice(0, 10);
    loadStaff(doc);
    loadAttendance(doc);
    const mb = doc.querySelector('#btnMarkAtt'); if (mb) mb.addEventListener('click', () => doMarkAttendance(doc));
    const ad = doc.querySelector('#attDate'); if (ad) ad.addEventListener('change', () => loadAttendance(doc));
  }
  async function loadStaff(doc) {
    const tb = doc.querySelector('#hrStaff'); if (!tb) return;
    try {
      const rows = await HIS.api.hrStaff();
      tb.innerHTML = rows.length ? rows.map(s =>
        `<tr data-code="${s.employeeCode}" style="cursor:pointer"><td><b>${s.employeeCode}</b></td><td>${s.fullName}</td><td>${s.designation || '—'}</td><td>${s.department || '—'}</td></tr>`
      ).join('') : emptyRow(4, 'No staff yet');
      tb.querySelectorAll('[data-code]').forEach(tr => tr.addEventListener('click', () => {
        tb.querySelectorAll('tr').forEach(x => x.classList.remove('sel')); tr.classList.add('sel');
        const code = tr.dataset.code;
        const ac = doc.querySelector('#attCode'); if (ac) ac.value = code;
        const pr = doc.querySelector('#hrSel'); if (pr) pr.textContent = code + ' selected';
      }));
    } catch (e) { tb.innerHTML = emptyRow(4, 'Staff API unavailable'); }
  }
  async function doAddStaff(doc) {
    const cmd = { employeeCode: val(doc, 'hrCode'), fullName: val(doc, 'hrName'),
      designation: val(doc, 'hrDesig') || null, department: val(doc, 'hrDept') || null, dateOfJoining: val(doc, 'hrDoj') || null };
    if (!cmd.employeeCode || !cmd.fullName) { HIS.toast('Code and name are required'); return; }
    try { await HIS.api.addStaff(cmd); HIS.toast('Staff added · ' + cmd.employeeCode, 'bi-person-plus'); loadStaff(doc); }
    catch (e) { HIS.toast('Add failed: ' + e.message); }
  }
  async function loadAttendance(doc) {
    const tb = doc.querySelector('#attList'); if (!tb) return;
    try {
      const rows = await HIS.api.hrAttendance(val(doc, 'attDate'));
      tb.innerHTML = rows.length ? rows.map(r =>
        `<tr><td>${r.employeeCode}</td><td>${r.name}</td><td><span class="pill pill--info">${r.status}</span></td><td>${(r.inTime || '—') + ' - ' + (r.outTime || '—')}</td></tr>`
      ).join('') : emptyRow(4, 'No attendance for this date');
    } catch (e) { tb.innerHTML = emptyRow(4, 'Attendance API unavailable'); }
  }
  async function doMarkAttendance(doc) {
    const code = val(doc, 'attCode');
    if (!code) { HIS.toast('Enter/select an employee'); return; }
    try {
      await HIS.api.markAttendance({ employeeCode: code, workDate: val(doc, 'attDate'), status: val(doc, 'attStatus'),
        inTime: val(doc, 'attIn') || null, outTime: val(doc, 'attOut') || null });
      HIS.toast('Attendance marked', 'bi-check2'); loadAttendance(doc);
    } catch (e) { HIS.toast('Mark failed: ' + e.message); }
  }

  /* ---- Phase 8: Payroll — run, summary, approve ---------------------- */
  function initPayroll(doc) {
    loadPayroll(doc);
    const rb = doc.querySelector('#btnRunPayroll'); if (rb) rb.addEventListener('click', () => doRunPayroll(doc));
  }
  async function loadPayroll(doc) {
    const tb = doc.querySelector('#prList'); if (!tb) return;
    const year = parseInt(val(doc, 'prYear'), 10) || new Date().getFullYear();
    const month = parseInt(val(doc, 'prMonth'), 10) || (new Date().getMonth() + 1);
    try {
      const s = await HIS.api.payrollGet(year, month);
      doc.querySelector('#prPeriod').textContent = month + '/' + year;
      doc.querySelector('#prTotals').innerHTML = `<span class="pill pill--muted">OT ${s.totalOtHours}h · ₹${s.totalOtAmount}</span> <span class="pill pill--ok">Net ₹${s.totalNet}</span>`;
      tb.innerHTML = s.rows.length ? s.rows.map((r, i) =>
        `<tr><td>${r.employeeCode}</td><td>${r.name}</td><td class="num">${r.basic}</td><td class="num">${r.otHours}</td><td class="num">${r.otAmount}</td><td class="num">${r.net}</td>
          <td><span class="pill ${r.status === 'Approved' ? 'pill--ok' : 'pill--warn'}">${r.status}</span></td>
          <td class="center">${r.status === 'Approved' ? '✓' : `<button class="btn btn--sm" data-approve="${r.payrollId}">Approve OT</button>`}</td></tr>`
      ).join('') : emptyRow(8, 'No payroll runs for this period');
      tb.querySelectorAll('[data-approve]').forEach(b => b.addEventListener('click', () => doApproveOt(doc, b.dataset.approve)));
    } catch (e) { tb.innerHTML = emptyRow(8, 'Payroll API unavailable'); }
  }
  async function doRunPayroll(doc) {
    const cmd = { employeeCode: val(doc, 'prCode'), year: parseInt(val(doc, 'prYear'), 10),
      month: parseInt(val(doc, 'prMonth'), 10), basicPay: numOrNull(val(doc, 'prBasic')) || 0, overtimeHours: numOrNull(val(doc, 'prOt')) || 0 };
    if (!cmd.employeeCode) { HIS.toast('Enter employee code'); return; }
    try {
      const r = await HIS.api.payrollRun(cmd);
      HIS.toast('Payroll run · OT ₹' + r.overtimeAmount + ' · Net ₹' + r.netPay, 'bi-cash-coin');
      loadPayroll(doc);
    } catch (e) { HIS.toast('Payroll failed: ' + e.message); }
  }
  async function doApproveOt(doc, payrollId) {
    // ApprovedBy = the logged-in supervisor's user id (from context); dev fallback to 1.
    const approvedBy = (HIS.session && HIS.session.userId) || 1;
    try {
      await HIS.api.payrollApprove(payrollId, { approvedBy });
      HIS.toast('Overtime approved · payroll #' + payrollId, 'bi-check-circle');
      loadPayroll(doc);
    } catch (e) { HIS.toast('Approve failed: ' + e.message); }
  }

  /* ---- Phase 7: cashless eligibility + pre-auth + claim dashboard ----- */
  function initCashless(doc) {
    loadClaimsMis(doc);
    const cp = doc.querySelector('#btnCapturePolicy'); if (cp) cp.addEventListener('click', () => doCapturePolicy(doc));
    loadEligibility(doc);
  }
  async function loadEligibility(doc) {
    const p = HIS.mock.currentPatient; if (!p || !p.uhid) return;
    try {
      const pols = await HIS.api.eligibility(p.uhid);
      const note = doc.querySelector('#caEligNote');
      if (pols.length) {
        const pol = pols[0];
        const si = doc.querySelector('#caSumInsured'); if (si) si.value = pol.sumInsured ?? '';
        const cp = doc.querySelector('#caCopay'); if (cp) cp.value = pol.coPayPct ?? '';
        const py = doc.querySelector('#caPayer'); if (py && !py.value) py.value = pol.payer;
        if (note) note.innerHTML = `<span class="pill pill--ok"><i class="bi bi-check-circle-fill"></i> Eligible · ${pol.payer} · balance ₹${pol.availableBalance}</span>`;
      } else if (note) { note.innerHTML = '<span class="pill pill--muted">No policy on file — capture one</span>'; }
    } catch (e) { /* ignore */ }
  }
  async function doCapturePolicy(doc) {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded'); return; }
    const payer = val(doc, 'caPayer');
    if (!payer) { HIS.toast('Select a payer (F3)'); return; }
    try {
      await HIS.api.capturePolicy({
        patientUhid: p.uhid, payerCode: payer, policyNo: val(doc, 'caPolicy') || null,
        sumInsured: numOrNull(val(doc, 'caSumInsured')), coPayPct: numOrNull(val(doc, 'caCopay'))
      });
      HIS.toast('Policy captured', 'bi-shield-check');
      loadEligibility(doc);
    } catch (e) { HIS.toast('Capture failed: ' + e.message); }
  }
  async function doSubmitPreAuth(doc) {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded'); return; }
    const payer = val(doc, 'caPayer'), cost = numOrNull(val(doc, 'caCost'));
    if (!payer) { HIS.toast('Select a payer'); return; }
    if (!cost || cost <= 0) { HIS.toast('Enter estimated cost'); return; }
    try {
      const r = await HIS.api.createPreAuth({
        patientUhid: p.uhid, payerCode: payer, provisionalIcd10: val(doc, 'caDx') || null,
        preAuthAmount: cost, channel: 'NHCX', mandatoryDocs: ['Aadhaar', 'Insurance e-Card', 'Pre-Auth Form']
      });
      HIS.toast('Pre-Auth submitted · ' + r.claimNo, 'bi-send');
      loadClaimsMis(doc);
    } catch (e) { HIS.toast('Pre-Auth failed: ' + e.message); }
  }
  async function loadClaimsMis(doc) {
    const tb = doc.querySelector('#caClaims'); if (!tb) return;
    try {
      const mis = await HIS.api.claimsMis();
      const pill = s => ({ Settled: 'pill--purple', Approved: 'pill--ok', Denied: 'pill--danger', Query: 'pill--warn', Shortfall: 'pill--danger' }[s] || 'pill--info');
      tb.innerHTML = mis.claims.length ? mis.claims.map(r =>
        `<tr><td>${r.claimNo}</td><td>${r.patient}</td><td>${r.payer}</td><td class="num">${r.preAuth ?? '—'}</td><td class="num">${r.approved ?? '—'}</td><td><span class="pill ${pill(r.status)}">${r.status}</span></td></tr>`
      ).join('') : emptyRow(6, 'No claims yet');
    } catch (e) { tb.innerHTML = emptyRow(6, 'Claims API unavailable'); }
  }

  /* ---- Phase 7: PM-JAY verify + TMS submit --------------------------- */
  function initPmjay(doc) {
    const b = doc.querySelector('#btnPmVerify'); if (b) b.addEventListener('click', () => doVerifyBeneficiary(doc));
  }
  async function doVerifyBeneficiary(doc) {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded'); return; }
    const pmId = val(doc, 'pmId');
    if (!pmId) { HIS.toast('Enter PM-JAY ID'); return; }
    try {
      const r = await HIS.api.pmjayVerify({ patientUhid: p.uhid, pmjayId: pmId, familyFloater: 500000 });
      const note = doc.querySelector('#pmVerifyNote');
      if (note) note.innerHTML = '<span class="pill pill--ok"><i class="bi bi-check-circle-fill"></i> Beneficiary verified (BIS)</span>';
      HIS.toast('Beneficiary verified (BIS)', 'bi-fingerprint');
    } catch (e) { HIS.toast('Verify failed: ' + e.message); }
  }
  async function doSubmitTms(doc) {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded'); return; }
    const pkg = val(doc, 'pmPackage');
    if (!pkg) { HIS.toast('Select an HBP package (F3)'); return; }
    try {
      const r = await HIS.api.pmjayClaim({ patientUhid: p.uhid, packageCode: pkg, ayushmanMitra: val(doc, 'pmMitra') || null });
      const tms = doc.querySelector('#pmTms'); if (tms) tms.textContent = r.tmsCaseNo;
      const stage = doc.querySelector('#pmStage'); if (stage) { stage.textContent = 'Pre-Auth submitted'; stage.className = 'pill pill--info'; }
      HIS.toast('Submitted to TMS · ' + r.tmsCaseNo + ' · ₹' + r.packageRate, 'bi-send');
    } catch (e) { HIS.toast('TMS submit failed: ' + e.message); }
  }

  /* ---- Phase 4: pharmacy queue + alerts + dispense -------------------- */
  function initPharmacy(doc) {
    loadPharmaQueue(doc);
    loadPharmaAlerts(doc);
  }
  async function loadPharmaQueue(doc) {
    const tb = doc.querySelector('#pharmaQueue'); if (!tb) return;
    try {
      const rows = await HIS.api.pharmacyQueue();
      tb.innerHTML = rows.length ? rows.map(r =>
        `<tr data-rx="${r.prescriptionId}" style="cursor:pointer"><td><b>RX-${r.prescriptionId}</b></td><td>${r.patient}</td><td>${r.doctor}</td><td>${r.items}</td><td><span class="pill pill--warn">${r.status}</span></td></tr>`
      ).join('') : emptyRow(5, 'No pending prescriptions');
      tb.querySelectorAll('[data-rx]').forEach(tr => tr.addEventListener('click', () => {
        tb.querySelectorAll('tr').forEach(x => x.classList.remove('sel'));
        tr.classList.add('sel'); doc.dataset.rx = tr.dataset.rx;
        const s = doc.querySelector('#pharmaSel'); if (s) s.textContent = 'RX-' + tr.dataset.rx + ' selected';
      }));
    } catch (e) { tb.innerHTML = emptyRow(5, 'Queue API unavailable'); }
  }
  async function loadPharmaAlerts(doc) {
    const host = doc.querySelector('#pharmaAlerts'); if (!host) return;
    try {
      const rows = await HIS.api.lowStock();
      host.innerHTML = rows.length ? rows.map(r =>
        `<div class="aitem"><i class="ai-ico ico-danger bi bi-capsule"></i><div class="a-txt"><b>${r.name}</b><span>${r.stock} left · reorder level ${r.reorderLevel}</span></div></div>`
      ).join('') : '<div class="aitem"><i class="ai-ico ico-ok bi bi-check-circle"></i><div class="a-txt"><b>All stock above reorder level</b></div></div>';
    } catch (e) { host.innerHTML = '<div class="aitem"><div class="a-txt"><b>Inventory API unavailable</b></div></div>'; }
  }
  async function doDispense(doc) {
    const lines = Array.from(doc.querySelectorAll('#dispBody tr')).map(tr => {
      const i = tr.querySelectorAll('input,select');
      return { drugCode: i[0] ? i[0].value.trim() : '', batchNo: i[1] ? i[1].value.trim() : '', qty: i[3] ? intOrNull(i[3].value) : null };
    }).filter(l => l.drugCode && l.batchNo && l.qty);
    if (!lines.length) { HIS.toast('Add at least one drug + batch + qty'); return; }
    const rx = doc.dataset.rx ? parseInt(doc.dataset.rx, 10) : null;
    try {
      const r = await HIS.api.dispense({ prescriptionId: rx, isNdps: false, lines });
      HIS.toast('Dispensed · ₹' + r.total + ' · #' + r.dispenseId, 'bi-bag-check');
      loadPharmaQueue(doc); loadPharmaAlerts(doc);
    } catch (e) { HIS.toast('Dispense failed: ' + e.message); }
  }

  /* ---- Phase 2.3: admit patient (POST /api/ipd/admit) ----------------- */
  async function doAdmit(doc) {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded — F3 to select'); return; }
    const bed = val(doc, 'ipdBed');
    if (!bed) { HIS.toast('Select a ward/bed (F3)'); return; }
    const cmd = {
      patientUhid: p.uhid,
      bedLabel: bed,
      consultantCode: val(doc, 'ipdConsultant') || null,
      provisionalIcd10: val(doc, 'ipdDx') || null,
      admissionType: val(doc, 'ipdAdmType') || null,
      paymentClass: val(doc, 'ipdPayClass') || null,
      estStayDays: null
    };
    try {
      const r = await HIS.api.admitPatient(cmd);
      const el = doc.querySelector('#ipdAdmNo'); if (el) el.value = r.admissionNo;
      HIS.toast('Admitted · ' + r.admissionNo + ' · Bed ' + r.bedNo, 'bi-hospital');
      loadBedBoard(doc);
    } catch (e) { HIS.toast('Admit failed: ' + e.message); }
  }

  /* ---- Phase 3: LIS worklist + create order + enter results ----------- */
  function initLab(doc) {
    loadLabWorklist(doc);
    const create = () => doCreateLabOrder(doc);
    ['btnLabOrder', 'btnLabOrder2'].forEach(id => {
      const b = doc.querySelector('#' + id); if (b) b.addEventListener('click', create);
    });
  }
  async function loadLabWorklist(doc) {
    const tb = doc.querySelector('#labWorklist'); if (!tb) return;
    try {
      const rows = await HIS.api.labWorklist();
      tb.innerHTML = rows.length ? rows.map(r =>
        `<tr data-order="${r.labOrderId}" style="cursor:pointer"><td><b>${r.barcode}</b></td><td>${r.patient}</td><td>${r.test}</td>
          <td><span class="pill ${r.status === 'Released' ? 'pill--ok' : 'pill--warn'}">${r.status}</span></td></tr>`
      ).join('') : emptyRow(4, 'No lab orders yet — create one above');
      tb.querySelectorAll('[data-order]').forEach(tr => tr.addEventListener('click', () => {
        tb.querySelectorAll('tr').forEach(x => x.classList.remove('sel'));
        tr.classList.add('sel'); doc.dataset.labOrder = tr.dataset.order;
        const s = doc.querySelector('#labSel'); if (s) s.textContent = 'order #' + tr.dataset.order + ' selected';
        HIS.toast('Order #' + tr.dataset.order + ' selected — enter results');
      }));
    } catch (e) { tb.innerHTML = emptyRow(4, 'Worklist API unavailable'); }
  }
  async function doCreateLabOrder(doc) {
    const uhid = val(doc, 'labPatient'), test = val(doc, 'labTest');
    if (!uhid || !test) { HIS.toast('Patient and test are required'); return; }
    try {
      const r = await HIS.api.createLabOrder({ patientUhid: uhid, testName: test });
      HIS.toast('Order created · ' + r.barcode, 'bi-upc-scan');
      const t = doc.querySelector('#labTest'); if (t) t.value = '';
      loadLabWorklist(doc);
    } catch (e) { HIS.toast('Create order failed: ' + e.message); }
  }
  async function doEnterResults(doc) {
    const orderId = doc.dataset.labOrder;
    if (!orderId) { HIS.toast('Select an order in the worklist first'); return; }
    const results = Array.from(doc.querySelectorAll('#labResultsBody tr')).map(tr => {
      const i = tr.querySelectorAll('input,select');
      return { parameter: i[0] ? i[0].value.trim() : '', resultValue: i[1] ? i[1].value.trim() : '',
        unit: i[2] ? i[2].value.trim() : '', referenceRange: i[3] ? i[3].value.trim() : '', flag: i[4] ? i[4].value : '' };
    }).filter(r => r.parameter);
    if (!results.length) { HIS.toast('Enter at least one parameter'); return; }
    try {
      await HIS.api.enterLabResults({ labOrderId: parseInt(orderId, 10), results });
      HIS.toast('Results validated & released', 'bi-check2-all');
      loadLabWorklist(doc);
    } catch (e) { HIS.toast('Release failed: ' + e.message); }
  }

  const val = (doc, id) => { const el = doc.querySelector('#' + id); return el ? el.value.trim() : ''; };
  const numOrNull = v => { const n = parseFloat(v); return isNaN(n) ? null : n; };
  const intOrNull = v => { const n = parseInt(v, 10); return isNaN(n) ? null : n; };

  /* ---- Phase 1: register patient (POST /api/patients) ----------------- */
  async function doRegister(doc) {
    const editUhid = doc.dataset.editUhid;
    const base = {
      fullName: val(doc, 'regName'),
      guardianName: val(doc, 'regGuardian') || null,
      ageYears: intOrNull(val(doc, 'regAge')),
      sex: val(doc, 'regSex'),
      bloodGroup: val(doc, 'regBlood') || null,
      mobile: val(doc, 'regMobile'),
      email: val(doc, 'regEmail') || null
    };
    if (!base.fullName || !base.sex || !base.mobile) { HIS.toast('Name, sex and mobile are required'); return; }
    try {
      if (editUhid) {
        const ep = doc._editPatient || {};
        await HIS.api.updatePatient(Object.assign({ uhid: editUhid, address: ep.address ?? null, city: ep.city ?? null }, base));
        HIS.toast('Updated · ' + editUhid, 'bi-check-circle-fill');
        clearRegForm(doc);
      } else {
        const r = await HIS.api.registerPatient(base);
        const u = doc.querySelector('#regUhid'); if (u) u.value = r.uhid;
        HIS.toast('Registered · UHID ' + r.uhid, 'bi-check-circle-fill');
        clearRegForm(doc, true);
      }
      loadPatients(doc);
    } catch (e) { HIS.toast((editUhid ? 'Update' : 'Registration') + ' failed: ' + e.message); }
  }
  function clearRegForm(doc, keepUhid) {
    delete doc.dataset.editUhid; doc._editPatient = null;
    ['regName', 'regGuardian', 'regAge', 'regBlood', 'regMobile', 'regEmail'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
    const sx = doc.querySelector('#regSex'); if (sx) sx.value = '';
    if (!keepUhid) { const u = doc.querySelector('#regUhid'); if (u) u.value = '(auto on save)'; }
    const btn = doc.querySelector('#regGrid [data-act="save"], [data-act="save"]');
    if (btn) btn.innerHTML = '<i class="bi bi-save"></i> Register &amp; Generate UHID <span class="fk">F9</span>';
  }
  /* ---- Registration: this hospital's patients + CRUD (tenant-scoped) ---- */
  async function loadPatients(doc) {
    const tb = doc.querySelector('#patientsBody'); if (!tb) return;
    try {
      const rows = await HIS.api.listPatients(val(doc, 'patSearch') || null);
      tb.innerHTML = rows.length ? rows.map(p =>
        `<tr><td><b>${p.uhid}</b></td><td>${p.fullName}</td><td>${p.ageYears ?? ''}/${(p.sex || '').slice(0, 1)}</td><td>${p.bloodGroup || ''}</td><td>${p.mobile || ''}</td><td>${(p.registeredAtUtc || '').slice(0, 10)}</td>
          <td style="white-space:nowrap"><button class="btn btn--sm" title="History" data-hist="${p.uhid}" data-name="${p.fullName}"><i class="bi bi-clock-history"></i></button>
          <button class="btn btn--sm" title="Edit" data-edit='${encodeURIComponent(JSON.stringify(p))}'><i class="bi bi-pencil"></i></button>
          <button class="btn btn--sm" title="Deactivate" data-deact="${p.uhid}" data-name="${p.fullName}"><i class="bi bi-person-x"></i></button></td></tr>`
      ).join('') : emptyRow(7, 'No patients yet — register one above');
      tb.querySelectorAll('[data-hist]').forEach(b => b.addEventListener('click', () => showPatientHistory(doc, b.dataset.hist, b.dataset.name)));
      tb.querySelectorAll('[data-edit]').forEach(b => b.addEventListener('click', () => editPatient(doc, JSON.parse(decodeURIComponent(b.dataset.edit)))));
      tb.querySelectorAll('[data-deact]').forEach(b => b.addEventListener('click', () => deactivatePatient(doc, b.dataset.deact, b.dataset.name)));
    } catch (e) { tb.innerHTML = emptyRow(7, 'Patients API unavailable'); }
  }
  function editPatient(doc, p) {
    doc.dataset.editUhid = p.uhid; doc._editPatient = p;
    const set = (id, v) => { const el = doc.querySelector('#' + id); if (el) el.value = (v == null ? '' : v); };
    set('regName', p.fullName); set('regGuardian', p.guardianName); set('regAge', p.ageYears);
    set('regSex', p.sex); set('regBlood', p.bloodGroup); set('regMobile', p.mobile); set('regEmail', p.email);
    const u = doc.querySelector('#regUhid'); if (u) u.value = p.uhid;
    const btn = doc.querySelector('[data-act="save"]'); if (btn) btn.innerHTML = '<i class="bi bi-save"></i> Update Patient <span class="fk">F9</span>';
    HIS.toast('Editing ' + p.fullName + ' — change fields and Save', 'bi-pencil');
    const nm = doc.querySelector('#regName'); if (nm) nm.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
  async function deactivatePatient(doc, uhid, name) {
    if (!confirm(`Deactivate patient ${name} (${uhid})?\nThe record is soft-deleted; clinical history is kept.`)) return;
    try { await HIS.api.setPatientActive(uhid, false); HIS.toast('Deactivated ' + uhid, 'bi-person-x'); loadPatients(doc); }
    catch (e) { HIS.toast('Deactivate failed: ' + e.message); }
  }
  // Consultation history (encounters + their structured department-template answers).
  async function showPatientHistory(doc, uhid, name) {
    const panel = doc.querySelector('#patientHistory'), body = doc.querySelector('#phBody'), who = doc.querySelector('#phWho');
    if (!panel || !body) return;
    if (who) who.textContent = name + ' · ' + uhid;
    panel.hidden = false; body.innerHTML = '<div class="muted" style="padding:10px">Loading…</div>';
    panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    try {
      const encs = await HIS.api.patientEncounters(uhid);
      if (!encs.length) { body.innerHTML = '<div class="muted" style="padding:10px">No consultations recorded yet.</div>'; return; }
      body.innerHTML = encs.map(e => {
        const dt = (e.dateUtc || '').replace('T', ' ').slice(0, 16);
        const ans = (e.answers || []).map(a => `<span class="pill pill--muted" style="margin:2px 4px 2px 0;display:inline-block">${a.label}: <b>${a.value}</b></span>`).join('');
        return `<div style="border:1px solid var(--line);border-radius:8px;padding:10px 12px;margin-bottom:8px">
          <div style="display:flex;justify-content:space-between;gap:10px;font-size:12.5px">
            <b><i class="bi bi-clipboard2-pulse"></i> ${dt}</b>
            <span class="muted">${e.doctor || ''}${e.department ? ' · ' + e.department : ''}</span></div>
          <div style="font-size:12.5px;margin-top:5px">${e.complaints ? '<b>Complaints:</b> ' + e.complaints + '&nbsp; ' : ''}${e.diagnosis ? '<b>Dx:</b> ' + e.diagnosis : ''}</div>
          ${ans ? `<div style="margin-top:6px">${ans}</div>` : ''}
        </div>`;
      }).join('');
    } catch (e) { body.innerHTML = '<div class="muted" style="padding:10px">History API unavailable</div>'; }
  }

  /* ---- Doctor directory + department → doctor filtering --------------- */
  // Cached list of {code, name, dept}; drives the Specialty/Department dropdowns.
  async function loadDoctorDirectory() {
    if (HIS._doctors) return HIS._doctors;
    try {
      const r = await HIS.api.lookup('doctor');
      HIS._doctors = (r.rows || []).map(row => ({ code: row[0], name: row[1], dept: row[2] || '' }));
    } catch (e) { HIS._doctors = []; }
    return HIS._doctors;
  }
  function departmentsOf(docs) { return [...new Set(docs.map(d => d.dept).filter(Boolean))].sort(); }
  function deptOfDoctor(code) { const d = (HIS._doctors || []).find(x => x.code === code); return d ? d.dept : ''; }
  // Fill a <select> with doctors, optionally filtered to one department.
  function fillDoctorSelect(sel, dept, keep) {
    const docs = (HIS._doctors || []).filter(d => !dept || d.dept === dept);
    sel.innerHTML = '<option value="">— select doctor —</option>' +
      docs.map(d => `<option value="${d.code}"${d.code === keep ? ' selected' : ''}>${d.name} · ${d.dept}</option>`).join('');
  }
  function fillDeptSelect(sel, keep) {
    sel.innerHTML = '<option value="">All departments</option>' +
      departmentsOf(HIS._doctors || []).map(x => `<option${x === keep ? ' selected' : ''}>${x}</option>`).join('');
  }

  // Department-specific clinical templates — admin-configurable per hospital, loaded from
  // the API (GET /api/opd/templates). Cached as { department: [label, …] }.
  async function loadDeptTemplates(force) {
    if (HIS._deptTemplates && !force) return HIS._deptTemplates;
    const map = {};
    try { (await HIS.api.opdTemplates()).forEach(t => { map[t.department] = t.fields || []; }); } catch (e) {}
    HIS._deptTemplates = map; return map;
  }

  /* ---- Phase 2: appointments queue + slots + booking ------------------ */
  function initAppointments(doc) {
    const d = doc.querySelector('#apptDate'); if (d) d.value = new Date().toISOString().slice(0, 10);
    loadQueue(doc);
    loadUpcoming(doc);
    const fu = doc.querySelector('#apptFuOnly'); if (fu) fu.addEventListener('change', () => loadUpcoming(doc));
    // Specialty/Department -> filtered doctor dropdown (select a dept to narrow the doctors).
    loadDoctorDirectory().then(() => {
      const dept = doc.querySelector('#apptDept'), docSel = doc.querySelector('#apptDoctor');
      if (dept) fillDeptSelect(dept);
      if (docSel) fillDoctorSelect(docSel, '');
      if (dept && docSel) dept.addEventListener('change', () => { fillDoctorSelect(docSel, dept.value); loadSlots(doc); });
    });
    const reload = () => loadSlots(doc);
    ['apptDoctor', 'apptDate'].forEach(id => {
      const el = doc.querySelector('#' + id);
      if (el) { el.addEventListener('change', reload); el.addEventListener('blur', reload); }
    });
    const dsel = doc.querySelector('#apptDoctor'); if (dsel) dsel.addEventListener('change', () => { loadQueue(doc); loadUpcoming(doc); });
    const vs = doc.querySelector('#vsSave'); if (vs) vs.addEventListener('click', () => doSaveVitals(doc));
    const vc = doc.querySelector('#vsCancel'); if (vc) vc.addEventListener('click', () => { doc.querySelector('#vitalsStation').hidden = true; });
  }
  async function loadQueue(doc) {
    const tb = doc.querySelector('#apptQueue'); if (!tb) return;
    try {
      const rows = await HIS.api.apptQueue(val(doc, 'apptDoctor') || null);
      tb.innerHTML = rows.length ? rows.map(r => {
        const cls = r.status === 'VitalsDone' ? 'pill--ok' : r.status === 'Completed' ? 'pill--muted' : '';
        const canVitals = r.status === 'Booked' || r.status === 'VitalsDone';
        const act = canVitals
          ? `<button class="btn btn--sm" data-vitals="${r.appointmentId}" data-token="${r.token}" data-patient="${r.patient}"><i class="bi bi-heart-pulse"></i> ${r.hasVitals ? 'Edit Vitals' : 'Take Vitals'}</button>`
          : '';
        return `<tr><td><b>${r.token}</b></td><td>${r.patient}</td><td>${r.doctor}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(5, 'No appointments today');
      tb.querySelectorAll('[data-vitals]').forEach(b => b.addEventListener('click', () => openVitalsStation(doc, b.dataset)));
    } catch (e) { tb.innerHTML = emptyRow(5, 'Queue API unavailable'); }
  }
  async function loadUpcoming(doc) {
    const tb = doc.querySelector('#apptUpcoming'); if (!tb) return;
    const fuOnly = !!(doc.querySelector('#apptFuOnly') && doc.querySelector('#apptFuOnly').checked);
    try {
      const rows = await HIS.api.upcomingAppts(val(doc, 'apptDoctor') || null, fuOnly);
      tb.innerHTML = rows.length ? rows.map(r => {
        const when = (r.slotStart || '').replace('T', ' ').slice(0, 16);
        const typeCls = r.visitType === 'Follow-up' ? 'pill--ok' : '';
        return `<tr><td>${when}</td><td><b>${r.token || ''}</b></td><td>${r.patient}</td><td>${r.doctor}</td><td>${r.department || ''}</td>`
          + `<td><span class="pill ${typeCls}">${r.visitType || '—'}</span></td><td><span class="pill pill--muted">${r.status}</span></td></tr>`;
      }).join('') : emptyRow(7, fuOnly ? 'No upcoming follow-ups' : 'No upcoming appointments');
    } catch (e) { tb.innerHTML = emptyRow(7, 'Upcoming API unavailable'); }
  }
  function openVitalsStation(doc, ds) {
    doc.dataset.vitalsAppt = ds.vitals;
    const who = doc.querySelector('#vsWho'); if (who) who.textContent = `Token ${ds.token} · ${ds.patient}`;
    const panel = doc.querySelector('#vitalsStation'); if (panel) { panel.hidden = false; panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
  }
  async function doSaveVitals(doc) {
    const apptId = doc.dataset.vitalsAppt; if (!apptId) { HIS.toast('Pick a patient from the queue first'); return; }
    const bp = (val(doc, 'vsBp') || '').split('/');
    const vitals = {
      tempF: numOrNull(val(doc, 'vsTemp')), pulse: intOrNull(val(doc, 'vsPulse')),
      bpSystolic: intOrNull(bp[0]), bpDiastolic: intOrNull(bp[1]),
      spo2: intOrNull(val(doc, 'vsSpo2')), respRate: intOrNull(val(doc, 'vsResp')),
      weightKg: numOrNull(val(doc, 'vsWeight')), heightCm: null, grbs: null
    };
    try {
      await HIS.api.recordVitals(apptId, vitals);
      HIS.toast('Vitals recorded — patient sent to the doctor lobby', 'bi-heart-pulse');
      doc.querySelector('#vitalsStation').hidden = true;
      ['vsTemp', 'vsPulse', 'vsBp', 'vsSpo2', 'vsResp', 'vsWeight'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      loadQueue(doc);
    } catch (e) { HIS.toast('Save vitals failed: ' + e.message); }
  }
  async function loadSlots(doc) {
    const host = doc.querySelector('#apptSlots'); if (!host) return;
    const docCode = val(doc, 'apptDoctor'), date = val(doc, 'apptDate');
    if (!docCode || !date) { host.innerHTML = '<span class="muted">pick a doctor &amp; date</span>'; return; }
    try {
      const slots = await HIS.api.apptSlots(docCode, date);
      doc.querySelector('#apptSlotHint').textContent = `${slots.filter(s => !s.booked).length} free`;
      host.innerHTML = slots.map(s =>
        `<button class="btn ${s.booked ? 'btn--ghost' : ''}" data-slot="${s.slotStart}" ${s.booked ? 'disabled style="opacity:.5"' : ''}
           style="justify-content:space-between">${s.time}
           <span class="pill ${s.booked ? 'pill--muted' : 'pill--ok'}">${s.booked ? 'Booked' : 'Free'}</span></button>`
      ).join('');
      host.querySelectorAll('[data-slot]').forEach(b => b.addEventListener('click', () => {
        host.querySelectorAll('[data-slot]').forEach(x => x.classList.remove('is-sel'));
        b.classList.add('is-sel'); doc.dataset.slot = b.dataset.slot;
        const t = (b.textContent || '').trim().replace(/\s+Free$/, '');
        const sel = doc.querySelector('#apptSelSlot');
        if (sel) { sel.innerHTML = '<i class="bi bi-check2-circle"></i> Selected ' + t + ' — click Book &amp; Generate Token'; sel.className = 'pill pill--ok'; }
        HIS.toast('Slot ' + t + ' selected — now click Book & Generate Token');
      }));
    } catch (e) { host.innerHTML = '<span class="muted">Slots API unavailable</span>'; }
  }
  async function doBookAppointment(doc) {
    const docCode = val(doc, 'apptDoctor');
    if (!docCode) { HIS.toast('Select a doctor'); return; }
    const slot = doc.dataset.slot || ((val(doc, 'apptDate') || new Date().toISOString().slice(0, 10)) + 'T09:00:00');
    const cmd = {
      doctorCode: docCode,
      patientUhid: val(doc, 'apptPatient') || null,
      department: val(doc, 'apptDept') || null,
      slotStart: slot,
      visitType: val(doc, 'apptVisit') || null,
      mode: val(doc, 'apptMode') || null
    };
    try {
      const r = await HIS.api.bookAppointment(cmd);
      HIS.toast('Booked · Token ' + r.tokenNo + ' — added to the appointments list', 'bi-ticket-detailed');
      delete doc.dataset.slot;
      const sel = doc.querySelector('#apptSelSlot');
      if (sel) { sel.innerHTML = '<i class="bi bi-clock"></i> No slot picked — defaults to 09:00'; sel.className = 'pill pill--muted'; }
      loadQueue(doc); loadSlots(doc); loadUpcoming(doc);
    } catch (e) { HIS.toast('Booking failed: ' + e.message); }
  }

  /* ---- OPD doctor lobby: patients whose vitals are done, waiting for this doctor ---- */
  function renderDeptTemplate(doc, dept) {
    const panel = doc.querySelector('#deptTplPanel'), host = doc.querySelector('#deptTplFields'), title = doc.querySelector('#deptTplTitle');
    if (!panel || !host) return;
    const fields = (HIS._deptTemplates || {})[dept] || [];
    if (!fields.length) { panel.hidden = true; host.innerHTML = ''; return; }
    if (title) title.textContent = dept + ' template';
    host.innerHTML = fields.map((f, i) => {
      const id = 'tplf' + i, t = f.fieldType || 'text';
      let ctrl;
      if (t === 'number') ctrl = `<input class="ctl" type="number" id="${id}">`;
      else if (t === 'checkbox') ctrl = `<label style="display:flex;align-items:center;gap:6px;height:34px"><input type="checkbox" id="${id}"> Yes</label>`;
      else if (t === 'select') ctrl = `<select class="ctl" id="${id}"><option value=""></option>${(f.options || []).map(o => `<option>${o}</option>`).join('')}</select>`;
      else ctrl = `<input class="ctl" id="${id}">`;
      return `<div class="f"><label>${f.label}</label><div class="field">${ctrl}</div></div>`;
    }).join('');
    panel.hidden = false;
  }
  function initOpd(doc) {
    const el = doc.querySelector('#opdLobbyDoctor');
    if (el) { el.addEventListener('change', () => loadOpdLobby(doc)); el.addEventListener('blur', () => loadOpdLobby(doc)); }
    opdLiveDoc = doc; ensureQueueHub();   // live refresh when vitals recorded / patient called / consult done
    loadOpdLobby(doc);
    // Specialty/Department -> filtered consultant + department-specific template (admin-configured).
    Promise.all([loadDoctorDirectory(), loadDeptTemplates()]).then(() => {
      const dept = doc.querySelector('#opdDept'), docSel = doc.querySelector('#opdDoctor');
      if (dept) fillDeptSelect(dept);
      if (docSel) fillDoctorSelect(docSel, '');
      if (dept && docSel) dept.addEventListener('change', () => { fillDoctorSelect(docSel, dept.value); renderDeptTemplate(doc, dept.value); });
      if (docSel) docSel.addEventListener('change', () => { const d = deptOfDoctor(docSel.value); if (dept) dept.value = d; renderDeptTemplate(doc, d); });
    });
  }
  async function loadOpdLobby(doc) {
    const tb = doc.querySelector('#opdLobby'); if (!tb) return;
    const docCode = val(doc, 'opdLobbyDoctor');
    try {
      const all = await HIS.api.apptQueue(docCode || null);
      const rows = all.filter(r => r.status === 'VitalsDone' || r.status === 'InConsultation');
      tb.innerHTML = rows.length ? rows.map(r => {
        const inRoom = r.status === 'InConsultation';
        const attrs = `data-uhid="${r.uhid}" data-patient="${r.patient}" data-token="${r.token}" data-doctor="${docCode || ''}"`;
        const act = inRoom
          ? `<button class="btn btn--sm" data-consult="${r.appointmentId}" ${attrs}><i class="bi bi-arrow-return-right"></i> Resume</button>`
          : `<button class="btn btn--sm btn--primary" data-call="${r.appointmentId}" ${attrs}><i class="bi bi-megaphone"></i> Call In</button>`;
        return `<tr><td><b>${r.token}</b></td><td>${r.patient}</td><td>${r.uhid}</td><td><span class="pill ${inRoom ? 'pill--warn' : 'pill--ok'}">${inRoom ? 'In consultation' : 'Waiting'}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(5, docCode ? 'No patients waiting' : 'Enter your doctor code above');
      tb.querySelectorAll('[data-call]').forEach(b => b.addEventListener('click', () => doCallIn(doc, b.dataset)));
      tb.querySelectorAll('[data-consult]').forEach(b => b.addEventListener('click', () => selectOpdPatient(doc, b.dataset)));
    } catch (e) { tb.innerHTML = emptyRow(5, 'Lobby API unavailable'); }
  }
  async function doCallIn(doc, ds) {
    try {
      await HIS.api.callNext(ds.call);
      HIS.toast('Calling ' + ds.patient + ' · token ' + (ds.token || ''), 'bi-megaphone');
      await selectOpdPatient(doc, { consult: ds.call, uhid: ds.uhid, patient: ds.patient, token: ds.token, doctor: ds.doctor });
      loadOpdLobby(doc);
    } catch (e) { HIS.toast('Call failed: ' + e.message); }
  }
  async function selectOpdPatient(doc, ds) {
    doc.dataset.opdAppt = ds.consult;
    doc.dataset.opdUhid = ds.uhid;
    const b = doc.querySelector('#opdBanner');
    if (b) b.innerHTML = `<div class="banner"><div class="meta"><span>Consulting <b>${ds.patient}</b></span><span>UHID <b>${ds.uhid}</b></span><span>Token <b>${ds.token || ''}</b></span></div></div>`;
    await loadDoctorDirectory(); await loadDeptTemplates();
    const dc = doc.querySelector('#opdDoctor'); if (dc && ds.doctor) dc.value = ds.doctor;
    const dept = deptOfDoctor(ds.doctor); const dptSel = doc.querySelector('#opdDept'); if (dptSel) dptSel.value = dept;
    renderDeptTemplate(doc, dept);
    try {
      const v = await HIS.api.apptVitals(ds.consult);
      if (v) {
        const set = (id, x) => { const el = doc.querySelector('#' + id); if (el) el.value = (x == null ? '' : x); };
        set('opdTemp', v.tempF); set('opdPulse', v.pulse);
        set('opdBp', (v.bpSystolic == null ? '' : v.bpSystolic) + (v.bpDiastolic == null ? '' : '/' + v.bpDiastolic));
        set('opdSpo2', v.spo2); set('opdResp', v.respRate); set('opdWeight', v.weightKg);
      }
    } catch (e) {}
    HIS.toast('Loaded ' + ds.patient + ' — vitals preloaded, proceed to diagnosis', 'bi-person-check');
  }

  /* ---- Phase 2: save OPD consultation (POST /api/encounters/consultation) */
  async function doSaveConsultation(doc) {
    const uhid = doc.dataset.opdUhid || (HIS.mock.currentPatient && HIS.mock.currentPatient.uhid);
    if (!uhid) { HIS.toast('Select a patient from the waiting lobby'); return; }
    const docCode = val(doc, 'opdDoctor');
    if (!docCode) { HIS.toast('Select a consultant'); return; }
    const apptId = doc.dataset.opdAppt ? parseInt(doc.dataset.opdAppt, 10) : null;

    const bp = val(doc, 'opdBp').split('/');
    const rx = Array.from(doc.querySelectorAll('#rxBody tr')).map(tr => {
      const inputs = tr.querySelectorAll('input,select');
      return {
        drugCode: inputs[0] ? inputs[0].value.trim() : '',
        dose: inputs[1] ? inputs[1].value.trim() : '',
        frequency: inputs[2] ? inputs[2].value : '',
        days: inputs[3] ? intOrNull(inputs[3].value) : null,
        route: inputs[4] ? inputs[4].value : '',
        qty: inputs[5] ? intOrNull(inputs[5].value) : null
      };
    }).filter(l => l.drugCode);

    const cmd = {
      patientUhid: uhid,
      doctorCode: docCode,
      // Vitals taken at the station are already linked via appointmentId — don't resend.
      // A walk-in (no appointment) may still capture vitals here.
      vitals: apptId ? null : {
        tempF: numOrNull(val(doc, 'opdTemp')), pulse: intOrNull(val(doc, 'opdPulse')),
        bpSystolic: intOrNull(bp[0]), bpDiastolic: intOrNull(bp[1]),
        spo2: intOrNull(val(doc, 'opdSpo2')), respRate: intOrNull(val(doc, 'opdResp')),
        weightKg: numOrNull(val(doc, 'opdWeight')), heightCm: null, grbs: null
      },
      complaints: val(doc, 'opdComplaints') || null,
      history: val(doc, 'opdHistory') || null,
      advice: val(doc, 'opdAdvice') || null,
      followUpDate: val(doc, 'opdFollowup') || null,
      diagnosisCodes: [val(doc, 'opdDx1'), val(doc, 'opdDx2')].filter(Boolean),
      prescription: rx,
      labTests: Array.from(doc.querySelectorAll('[data-pane="ord"] input[type="checkbox"]:checked'))
        .map(cb => (cb.closest('label') ? cb.closest('label').textContent.trim() : '')).filter(Boolean),
      appointmentId: apptId
    };
    // Department-template answers -> persisted as structured rows on the encounter (queryable).
    const answers = Array.from(doc.querySelectorAll('#deptTplFields input, #deptTplFields select')).map(el => {
      const l = el.closest('.f'); const label = l ? l.querySelector('label').textContent.trim() : el.id;
      const value = el.type === 'checkbox' ? (el.checked ? 'Yes' : '') : (el.value || '').trim();
      const fieldType = el.type === 'checkbox' ? 'checkbox' : (el.tagName === 'SELECT' ? 'select' : (el.type === 'number' ? 'number' : 'text'));
      return { label, fieldType, value };
    }).filter(a => a.value);
    cmd.department = val(doc, 'opdDept') || null;
    if (answers.length) cmd.templateAnswers = answers;
    try {
      const r = await HIS.api.saveConsultation(cmd);
      HIS.toast('Consultation saved · Encounter #' + r.encounterId + (apptId ? ' · token closed' : ''), 'bi-check-circle-fill');
      // Follow-up appointment issued right after the consultation (SRS 3.2).
      const fuBadge = doc.querySelector('#opdFollowupResult');
      if (r.followUpToken) {
        const when = (r.followUpDate || cmd.followUpDate || '').slice(0, 10);
        HIS.toast('Follow-up booked · Token ' + r.followUpToken + (when ? ' · ' + when : ''), 'bi-ticket-detailed');
        if (fuBadge) { fuBadge.innerHTML = '<i class="bi bi-ticket-detailed"></i> Follow-up token <b>' + r.followUpToken + '</b>' + (when ? ' for ' + when : ''); fuBadge.style.display = 'inline-block'; }
      } else if (fuBadge) { fuBadge.style.display = 'none'; }
      if (apptId) { delete doc.dataset.opdAppt; delete doc.dataset.opdUhid; loadOpdLobby(doc); }
    } catch (e) { HIS.toast('Save failed: ' + e.message); }
  }

  /* ---- dashboard: live KPIs + service activity from /api/dashboard ---- */
  async function loadDashboard(doc) {
    try {
      const d = await HIS.api.dashboard();
      const kpis = doc.querySelector('#kpis');
      kpis.innerHTML = (d.kpis || []).map(k => {
        const dir = (k.trend || '').startsWith('up') ? 'up' : (k.trend || '').startsWith('down') ? 'down' : '';
        return `<div class="kpi"><div class="v tnum">${k.value}</div><div class="l">${k.label}</div>
          <div class="d ${dir}">${k.trend || ''}</div></div>`;
      }).join('') || '<div class="muted" style="padding:12px">No KPIs</div>';

      const tb = doc.querySelector('#svcActivity');
      tb.innerHTML = (d.activity || []).map(a =>
        `<tr><td>${a.service}</td><td class="num">${a.count.toLocaleString('en-IN')}</td><td class="num">${a.revenue.toLocaleString('en-IN')}</td></tr>`
      ).join('') || emptyRow(3, 'No activity');
      doc.querySelector('#svcCount').textContent = (d.totalCount || 0).toLocaleString('en-IN');
      doc.querySelector('#svcRevenue').textContent = (d.totalRevenue || 0).toLocaleString('en-IN');
    } catch (e) {
      doc.querySelector('#kpis').innerHTML = '<div class="muted" style="padding:12px">Dashboard API unavailable</div>';
    }
    loadAlerts(doc);
  }

  /* ---- dashboard: live alerts feed from /api/dashboard/alerts (Phase 12.1) ---- */
  async function loadAlerts(doc) {
    const host = doc.querySelector('#alerts');
    if (!host) return;
    const sev = { danger: 'ico-danger', warn: 'ico-warn', info: 'ico-info', ok: 'ico-ok' };
    try {
      const alerts = await HIS.api.dashboardAlerts();
      host.innerHTML = (alerts || []).map(a =>
        `<div class="aitem"><i class="ai-ico ${sev[a.severity] || 'ico-info'} bi ${a.icon || 'bi-info-circle'}"></i>
          <div class="a-txt"><b>${a.title}</b><span>${a.detail || ''}</span></div></div>`
      ).join('') || '<div class="aitem"><i class="ai-ico ico-ok bi bi-check-circle"></i><div class="a-txt"><b>All clear</b><span>No active alerts.</span></div></div>';
    } catch (e) {
      host.innerHTML = '<div class="aitem"><i class="ai-ico ico-warn bi bi-exclamation-triangle"></i><div class="a-txt"><b>Alerts unavailable</b><span>Could not reach the alerts API.</span></div></div>';
    }
  }

  /* ---- FHIR R4 export: download the current patient as a FHIR resource (0.10) ---- */
  HIS.exportFhir = async function () {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded — F3 to select first'); return; }
    try {
      const res = await HIS.api.fhirPatient(p.uhid);
      const blob = new Blob([JSON.stringify(res, null, 2)], { type: 'application/fhir+json' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `Patient-${p.uhid}.fhir.json`;
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
      HIS.toast(`FHIR R4 Patient exported (${p.uhid})`, 'bi-diagram-3');
    } catch (e) { HIS.toast('FHIR export failed: ' + e.message); }
  };

  /* ---- registration: stamp date, load history if a patient is current ---- */
  function initRegistration(doc) {
    const dt = doc.querySelector('#regDate');
    if (dt) dt.value = new Date().toLocaleString();
    const p = HIS.mock.currentPatient;
    const tb = doc.querySelector('#visitHistory');
    if (tb && p && p.visits && p.visits.length) {
      tb.innerHTML = p.visits.map(v =>
        `<tr><td>${v.date}</td><td>${v.branch}</td><td>${v.type}</td><td>${v.doctor}</td><td>${v.diagnosis}</td><td>${v.payer}</td></tr>`
      ).join('');
    }
    // This hospital's patient list (tenant-scoped) + CRUD.
    loadPatients(doc);
    const s = doc.querySelector('#patSearch');
    if (s) { let t; s.addEventListener('input', () => { clearTimeout(t); t = setTimeout(() => loadPatients(doc), 250); }); }
    const clr = doc.querySelector('#regClear'); if (clr) clr.addEventListener('click', () => clearRegForm(doc));
  }

  /* ---- ipd: render the live bed board (with occupants) --------------- */
  async function loadBedBoard(doc) {
    const host = doc.querySelector('#bedboard');
    try {
      const beds = await HIS.api.bedBoard();
      const map = { occ: 'OCC', free: 'FREE', clean: 'CLN', block: 'BLK' };
      host.innerHTML = (beds || []).map(b => {
        const cls = ['occ', 'free', 'clean', 'block'].includes(b.status) ? b.status : 'free';
        const who = b.occupant || (cls === 'free' ? 'Available' : cls === 'clean' ? 'Cleaning' : cls === 'block' ? 'Blocked' : '—');
        return `<div class="bed ${cls}" data-bed="${b.bedNo}" tabindex="0" data-focusable>
          <div class="bn">${b.bedNo} <span class="tag">${map[cls] || ''}</span></div>
          <div class="bp">${who}</div>
          ${cls === 'clean' ? `<button class="bed-ready" data-ready="${b.bedNo}" title="Housekeeping: return this cleaned bed to the available pool">Mark ready</button>` : ''}</div>`;
      }).join('') || '<div class="muted">No beds configured</div>';
      host.querySelectorAll('.bed-ready').forEach(btn => btn.addEventListener('click', async (e) => {
        e.stopPropagation();
        const bedNo = btn.getAttribute('data-ready');
        try { await HIS.api.markBedReady(bedNo); HIS.toast('Bed ' + bedNo + ' ready for admission', 'bi-hospital'); loadBedBoard(doc); }
        catch (err) { HIS.toast('Mark ready failed: ' + err.message); }
      }));
      if (HIS.wireScreenFragment) HIS.wireScreenFragment(host);
    } catch (e) {
      host.innerHTML = '<div class="muted">Bed board API unavailable</div>';
    }
  }

  function wireBilling(doc) {
    const recalc = () => {
      let gross = 0;
      doc.querySelectorAll('#chargeBody tr').forEach(tr => {
        const q = parseFloat((tr.querySelector('.qty') ? tr.querySelector('.qty').value : (tr.children[1] && tr.children[1].textContent))) || 0;
        const r = parseFloat((tr.querySelector('.rate') ? tr.querySelector('.rate').value : (tr.children[2] && tr.children[2].textContent) || '0').toString().replace(/,/g, '')) || 0;
        const amtCell = tr.querySelector('.amt');
        const amt = q * r; if (amtCell) amtCell.textContent = amt.toFixed(2);
        gross += amt;
      });
      const gt = doc.querySelector('#grossTot'); if (gt) gt.textContent = gross.toLocaleString('en-IN', { minimumFractionDigits: 2 });
      const sg = doc.querySelector('#sumGross'); if (sg) sg.value = gross.toFixed(2);
      const disc = parseFloat(val(doc, 'billDiscount')) || 0;
      const ins = parseFloat(val(doc, 'billInsurance')) || 0;
      const payable = Math.max(0, gross - disc - ins);
      const pb = doc.querySelector('#billPayable'); if (pb) pb.value = payable.toFixed(2);
      const pa = doc.querySelector('#payAmount'); if (pa && !doc.dataset.billId) pa.value = payable.toFixed(2);
    };
    doc.addEventListener('input', e => {
      if (e.target.closest('#chargeBody') || ['billDiscount', 'billInsurance'].includes(e.target.id)) recalc();
    });
    recalc();
  }

  /* ---- Phase 6: create bill from charge lines (rates from tariff master) */
  async function doCreateBill(doc) {
    const p = HIS.mock.currentPatient;
    if (!p || !p.uhid) { HIS.toast('No patient loaded — F3 to select'); return; }
    const lines = Array.from(doc.querySelectorAll('#chargeBody tr')).map(tr => {
      const svc = tr.querySelector('.svc') ? tr.querySelector('.svc').value.trim() : (tr.children[0] ? tr.children[0].textContent.trim() : '');
      const qty = parseFloat(tr.querySelector('.qty') ? tr.querySelector('.qty').value : '1') || 1;
      const rate = parseFloat((tr.querySelector('.rate') ? tr.querySelector('.rate').value : '0').replace(/,/g, '')) || 0;
      const hasCode = svc.includes('—');
      return { tariffCode: hasCode ? svc : null, description: hasCode ? '' : svc, qty, rate };
    }).filter(l => l.tariffCode || l.description);
    if (!lines.length) { HIS.toast('Add at least one charge line'); return; }
    const cmd = {
      patientUhid: p.uhid,
      discountAmount: parseFloat(val(doc, 'billDiscount')) || 0,
      insurancePays: parseFloat(val(doc, 'billInsurance')) || 0,
      lines
    };
    try {
      const r = await HIS.api.createBill(cmd);
      doc.dataset.billId = r.billId;
      const ref = doc.querySelector('#payBillRef'); if (ref) ref.textContent = r.billNo;
      const pa = doc.querySelector('#payAmount'); if (pa) pa.value = r.patientPays.toFixed(2);
      HIS.toast('Bill ' + r.billNo + ' · payable ₹' + r.patientPays, 'bi-receipt');
      await renderBill(doc, r.billId);
    } catch (e) { HIS.toast('Bill failed: ' + e.message); }
  }
  async function renderBill(doc, billId) {
    try {
      const b = await HIS.api.getBill(billId);
      const tb = doc.querySelector('#chargeBody');
      if (tb) tb.innerHTML = b.lines.map(l =>
        `<tr><td>${l.description}</td><td class="center">${l.qty}</td><td class="num">${l.rate.toFixed(2)}</td><td class="num amt">${l.amount.toFixed(2)}</td><td class="center">—</td></tr>`).join('');
      const gt = doc.querySelector('#grossTot'); if (gt) gt.textContent = b.gross.toFixed(2);
      const sg = doc.querySelector('#sumGross'); if (sg) sg.value = b.gross.toFixed(2);
      const pb = doc.querySelector('#billPayable'); if (pb) pb.value = b.patientPays.toFixed(2);
    } catch (e) { /* keep entered grid on error */ }
  }
  async function doCollectPayment(doc) {
    const p = HIS.mock.currentPatient;
    const billId = doc.dataset.billId ? parseInt(doc.dataset.billId, 10) : null;
    if (!billId) { HIS.toast('Create the bill first'); return; }
    const amount = parseFloat(val(doc, 'payAmount')) || 0;
    if (amount <= 0) { HIS.toast('Enter an amount'); return; }
    try {
      const r = await HIS.api.collectPayment({ billId, patientUhid: p.uhid, mode: val(doc, 'payMode'), amount });
      HIS.toast('Paid via ' + r.provider + ' · ' + r.reference + (r.billSettled ? ' · BILL SETTLED' : ''), 'bi-check2-circle');
      if (r.billSettled) { const ref = doc.querySelector('#payBillRef'); if (ref) ref.textContent += ' · Paid'; }
    } catch (e) { HIS.toast('Payment failed: ' + e.message); }
  }

})();
