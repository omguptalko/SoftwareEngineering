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
    poBody: `
      <td><input class="gi" placeholder="Item name / drug"></td>
      <td class="num"><input class="gi num" placeholder="qty" style="width:56px"></td>
      <td class="num"><input class="gi num" placeholder="0.00" style="width:80px"></td>
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
    return `<div class="screen">
      ${head('bi-clipboard2-pulse', 'OPD Consultation', 'Doctor waiting lobby · consultation &amp; prescription')}
      <div class="panel"><div class="panel__head"><i class="bi bi-people"></i> Waiting Lobby — vitals done
        <span class="ph-right"><select class="ctl" id="opdLobbyDoctor" style="width:220px;display:inline-block"><option value="">— select your doctor —</option></select></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Token</th><th>Patient</th><th>UHID</th><th>Status</th><th></th></tr></thead>
          <tbody id="opdLobby">${emptyRow(5, 'Enter your doctor code to load your queue')}</tbody>
        </table></div></div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-calendar-check"></i> Today's Schedule — all my appointments
        <span class="ph-right muted" id="opdSchedCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Time</th><th>Token</th><th>Patient</th><th>UHID</th><th>Status</th><th>Vitals</th></tr></thead>
          <tbody id="opdSchedule">${emptyRow(6, 'Enter your doctor code to load your schedule')}</tbody>
        </table></div></div></div>
      <div class="panel"><div class="panel__body" style="padding:10px 12px">
        <div class="flex gap6" style="align-items:center;flex-wrap:wrap">
          <span class="muted"><i class="bi bi-person-walking"></i> Walk-in (no appointment):</span>
          <div class="field with-btn" style="max-width:300px;margin:0"><input class="ctl" id="opdWalkin" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div>
          <button class="btn btn--sm" id="opdWalkinBtn"><i class="bi bi-person-plus"></i> Start walk-in consult</button>
          <span class="muted" style="font-size:12px">— or Call In a patient from the waiting lobby above.</span>
        </div>
      </div></div>
      <div id="opdBanner"><div class="pbanner selectable"><div class="av">—</div>
        <div><div class="nm">No patient selected</div>
        <div class="meta"><span>Click <b>Call In</b> on a lobby patient, or pick a <b>walk-in</b> above to start their consultation</span></div></div></div></div>
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
      <div class="panel"><div class="panel__head"><i class="bi bi-clock-history"></i> Consultation History
        <span class="ph-right muted" id="opdHistWho"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Date</th><th>Doctor</th><th>Department</th><th>Complaints</th><th>Diagnosis</th></tr></thead>
          <tbody id="opdHistBody">${emptyRow(5, 'Select a patient (Call In / Resume / walk-in) to see their history')}</tbody>
        </table></div></div></div>
    </div>`;
  }

  /* ============================ IPD =================================== */
  function ipd() {
    return `<div class="screen">
      ${head('bi-hospital', 'IPD Admission &amp; Bed Board', 'Ward management · transfers · discharge',
        `<button class="btn btn--ghost btn--sm" id="ipdTransferBtn"><i class="bi bi-arrow-left-right"></i> Transfer</button>
         <button class="btn btn--ghost btn--sm" id="ipdDischargeBtn"><i class="bi bi-box-arrow-right"></i> Discharge</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-save"></i> Admit <span class="fk">F9</span></button>`)}
      <div id="ipdBanner"><div class="pbanner selectable"><div class="av">—</div>
        <div><div class="nm">No patient selected</div>
        <div class="meta"><span>Pick a <b>Patient (F3)</b> in the Admission form below to admit</span></div></div></div></div>
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
            <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="ipdPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Ward / Bed <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="ipdBed" data-lookup="ward" placeholder="F3 bed…"><button class="lk" data-lookup="ward">F3</button></div></div>
            <div class="f"><label>Consultant <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="ipdConsultant" data-lookup="doctor" placeholder="F3 consultant…"><button class="lk" data-lookup="doctor">F3</button></div></div>
            <div class="f"><label>Provisional Dx</label><div class="field with-btn"><input class="ctl" id="ipdDx" data-lookup="icd10" placeholder="F3 ICD-10…"><button class="lk" data-lookup="icd10">F3</button></div></div>
            <div class="f"><label>Admission Type</label><div class="field"><select class="ctl" id="ipdAdmType"><option>Planned</option><option>Emergency</option><option>Day Care</option><option>Transfer-in</option></select></div></div>
            <div class="f"><label>Payment Class</label><div class="field"><select class="ctl" id="ipdPayClass"><option>Cashless / Insurance</option><option>PM-JAY</option><option>ESIC</option><option>Cash</option><option>Corporate</option></select></div></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-hospital"></i> Confirm Admission <span class="fk">F9</span></button>
        </div></div>
      </div>
      <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-heart"></i> Admitted Patients — who is in which bed
        <span class="ph-right muted" id="ipdAdmCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Admission No.</th><th>Patient</th><th>UHID</th><th>Ward / Room</th><th>Bed</th><th>Consultant</th><th>Admitted</th><th></th></tr></thead>
          <tbody id="ipdAdmitted">${emptyRow(8, 'Loading…')}</tbody>
        </table></div></div></div>
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
              <div class="flex gap6 mt8" style="padding:8px 12px;align-items:center;flex-wrap:wrap">
                <button class="btn btn--primary" data-act="save"><i class="bi bi-bag-check"></i> Dispense &amp; Bill <span class="fk">F9</span></button>
                <span style="font-weight:600">Total: <span id="dispTotal">₹0.00</span></span>
                <span class="hintline">Pick a drug (F3) — batch/expiry/MRP auto-fill (FEFO) · enter qty · Amount = Qty × MRP live.</span>
              </div>
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
            <div class="flex gap6 mt8" style="padding:8px 12px;align-items:center;flex-wrap:wrap">
              <button class="btn btn--primary" data-act="save"><i class="bi bi-check2-all"></i> Validate &amp; Release <span class="fk">F9</span></button>
              <span class="hintline">1) click an order in the worklist · 2) enter parameters · 3) Validate &amp; Release to save.</span>
            </div>
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
    return `<div class="screen">
      ${head('bi-credit-card-2-front', 'Cashless / TPA Claims', 'Pre-auth → query → enhancement → final bill → settlement',
        `<button class="btn btn--ghost btn--sm"><i class="bi bi-cpu"></i> AI Pre-Scrub</button>
         <button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-send"></i> Submit Pre-Auth <span class="fk">F9</span></button>`)}
      <div id="caBanner">${banner(null)}</div>
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
            <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="caPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Payer / TPA <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="caPayer" data-lookup="payer" placeholder="F3 payer…"><button class="lk" data-lookup="payer">F3</button></div></div>
            <div class="f"><label>Policy / Member ID</label><div class="field"><input class="ctl code" id="caPolicy" placeholder="Policy / member no."></div></div>
            <div class="f"><label>Sum Insured</label><div class="field with-unit"><input class="ctl num" id="caSumInsured" placeholder="—"><span class="unit">₹</span></div></div>
            <div class="f"><label>Co-pay</label><div class="field with-unit"><input class="ctl num" id="caCopay" placeholder="—"><span class="unit">%</span></div></div>
          </div>
          <div class="mt8" id="caEligNote"></div>
          <div class="grid-wrap mt8" style="border:0"><table class="grid">
            <thead><tr><th>Payer / TPA</th><th>Policy No</th><th class="num">Sum Insured ₹</th><th class="num">Co-pay %</th><th class="num">Balance ₹</th></tr></thead>
            <tbody id="caPolicies">${emptyRow(5, 'Pick a patient (F3) to see captured policies')}</tbody>
          </table></div>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-file-medical"></i> Pre-Authorisation</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Provisional Dx</label><div class="field with-btn"><input class="ctl" id="caDx" data-lookup="icd10" placeholder="F3 ICD-10…"><button class="lk" data-lookup="icd10">F3</button></div></div>
            <div class="f"><label>Est. Cost</label><div class="field with-unit"><input class="ctl num" id="caCost" placeholder="0"><span class="unit">₹</span></div></div>
            <div class="f wide"><label>Clinical Notes</label><div class="field"><textarea class="ctl" id="caNotes" placeholder="Clinical justification for admission…"></textarea></div></div>
          </div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" data-act="save"><i class="bi bi-send"></i> Submit Pre-Auth <span class="fk">F9</span></button><span class="hintline">Patient + Payer + Est. Cost bharo → Submit → claim niche Dashboard me aayega.</span></div>
        </div></div>
      </div>
      <div class="panel"><div class="panel__head"><i class="bi bi-kanban"></i> Claim Tracking Dashboard <span class="ph-right hintline" id="caCount"></span></div>
        <div class="panel__body tight">
          <div class="flex gap6 mb8" style="flex-wrap:wrap;align-items:center;padding:4px 0">
            <div class="field" style="max-width:240px"><input class="ctl" id="caqText" placeholder="Search claim # / patient / payer…"></div>
            <select class="ctl" id="caqStatus" style="max-width:160px"><option value="">All statuses</option></select>
            <div class="field with-unit"><input class="ctl" id="caqFrom" type="date"><span class="unit">from</span></div>
            <div class="field with-unit"><input class="ctl" id="caqTo" type="date"><span class="unit">to</span></div>
            <button class="btn btn--sm" id="caqClear"><i class="bi bi-x-circle"></i> Clear</button>
          </div>
          <div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Claim #</th><th>Date</th><th>Patient</th><th>Payer</th><th class="num">Pre-Auth ₹</th><th class="num">Approved ₹</th><th>Status</th></tr></thead>
          <tbody id="caClaims">${emptyRow(7, 'Loading…')}</tbody>
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
              <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="pmPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
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
      <div class="panel"><div class="panel__head"><i class="bi bi-list-check"></i> Submitted TMS Claims <span class="ph-right hintline" id="pmCount"></span></div>
        <div class="panel__body tight">
          <div class="flex gap6 mb8" style="flex-wrap:wrap;align-items:center;padding:4px 0">
            <div class="field" style="max-width:240px"><input class="ctl" id="pmqText" placeholder="Search TMS # / claim # / patient / package…"></div>
            <select class="ctl" id="pmqStatus" style="max-width:160px"><option value="">All statuses</option></select>
            <div class="field with-unit"><input class="ctl" id="pmqFrom" type="date"><span class="unit">from</span></div>
            <div class="field with-unit"><input class="ctl" id="pmqTo" type="date"><span class="unit">to</span></div>
            <button class="btn btn--sm" id="pmqClear"><i class="bi bi-x-circle"></i> Clear</button>
          </div>
          <div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>TMS Case #</th><th>Claim #</th><th>Date</th><th>Patient</th><th>Package</th><th class="num">Amount ₹</th><th>Status</th></tr></thead>
          <tbody id="pmCases">${emptyRow(7, 'Loading…')}</tbody>
        </table></div></div>
      </div>
    </div>`;
  }

  /* ============ ESIC / CGHS / ECHS / State schemes (SRS §7.4–7.7) ==== */
  const SCHEMES = {
    esic:        { type: 'ESIC',  title: 'ESIC',                icon: 'bi-hospital',  full: 'Employees’ State Insurance Corporation', ref: 'IP Number' },
    cghs:        { type: 'CGHS',  title: 'CGHS',                icon: 'bi-bank2',     full: 'Central Government Health Scheme',        ref: 'CGHS Card No.' },
    echs:        { type: 'ECHS',  title: 'ECHS',                icon: 'bi-shield-shaded', full: 'Ex-Servicemen Contributory Health Scheme', ref: 'ECHS Card No.' },
    statescheme: { type: 'State', title: 'State Health Schemes', icon: 'bi-geo-alt', full: 'State government health scheme',           ref: 'Scheme Card No.' },
  };
  function schemeScreen(sid) {
    const s = SCHEMES[sid];
    return `<div class="screen">
      ${head(s.icon, s.title, `${s.full} · membership verification · package tariff`,
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-patch-check"></i> Verify Membership <span class="fk">F9</span></button>`)}
      <div id="schBanner">${banner(null)}</div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-person-vcard"></i> Membership Verification</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="schPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>${s.title} Member No <span class="req">*</span></label><div class="field"><input class="ctl code" id="schMember" placeholder="Member / card number"></div></div>
            <div class="f"><label>${s.ref}</label><div class="field"><input class="ctl code" id="schRef" placeholder="Secondary reference"></div></div>
          </div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" data-act="save"><i class="bi bi-patch-check"></i> Verify Membership <span class="fk">F9</span></button><span class="hintline">Patient (F3) + Member No bharo → Verify → niche list me aayega.</span></div>
          <div class="mt8" id="schVerifyNote"></div>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-card-list"></i> Package Tariff <span class="ph-right"><input class="ctl" id="schPkgSearch" placeholder="Search package…" style="max-width:180px"></span></div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Code</th><th>Package</th><th class="num">Rate ₹</th></tr></thead>
            <tbody id="schPkgs">${emptyRow(3, 'Loading…')}</tbody>
          </table></div></div>
        </div>
      </div>
      <div class="panel"><div class="panel__head"><i class="bi bi-people"></i> Verified ${s.title} Memberships <span class="ph-right hintline" id="schCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Patient</th><th>Member No</th><th>${s.ref}</th><th>Status</th></tr></thead>
          <tbody id="schMembers">${emptyRow(4, 'Loading…')}</tbody>
        </table></div></div>
      </div>
    </div>`;
  }
  function esic()        { return schemeScreen('esic'); }
  function cghs()        { return schemeScreen('cghs'); }
  function echs()        { return schemeScreen('echs'); }
  function statescheme() { return schemeScreen('statescheme'); }

  function initScheme(doc, sid) {
    const s = SCHEMES[sid];
    doc.dataset.schemeType = s.type;
    loadSchemePackages(doc, s.type);
    loadSchemeMemberships(doc, s.type);
    const pf = doc.querySelector('#schPatient');
    if (pf) { pf.addEventListener('change', () => showSchPatient(doc)); pf.addEventListener('blur', () => showSchPatient(doc)); }
    const sr = doc.querySelector('#schPkgSearch');
    if (sr) sr.addEventListener('input', () => loadSchemePackages(doc, s.type));
  }
  async function showSchPatient(doc) {
    const uhid = pickedUhid(doc, 'schPatient'); const b = doc.querySelector('#schBanner');
    if (!uhid) { if (b) b.innerHTML = banner(null); return; }
    try { const p = await HIS.api.patientByUhid(uhid); if (p && b) b.innerHTML = banner(p); } catch (e) {}
  }
  async function loadSchemePackages(doc, type) {
    const tb = doc.querySelector('#schPkgs'); if (!tb) return;
    try {
      const rows = await HIS.api.schemePackages(type, val(doc, 'schPkgSearch'));
      tb.innerHTML = rows.length ? rows.map(p =>
        `<tr><td class="code">${p.code}</td><td>${p.name}</td><td class="num">${p.rate}</td></tr>`
      ).join('') : emptyRow(3, 'No packages for this scheme');
    } catch (e) { tb.innerHTML = emptyRow(3, 'Package API unavailable'); }
  }
  async function loadSchemeMemberships(doc, type) {
    const tb = doc.querySelector('#schMembers'); if (!tb) return;
    try {
      const rows = await HIS.api.schemeMemberships(type);
      tb.innerHTML = rows.length ? rows.map(m =>
        `<tr><td>${m.patient || '—'}</td><td class="code">${m.memberNo || '—'}</td><td>${m.secondaryRef || '—'}</td>
          <td><span class="pill ${m.verified ? 'pill--ok' : 'pill--muted'}">${m.verified ? 'Verified' : 'Pending'}</span></td></tr>`
      ).join('') : emptyRow(4, 'No memberships yet');
      const cnt = doc.querySelector('#schCount'); if (cnt) cnt.textContent = rows.length ? `${rows.length} member(s)` : '';
    } catch (e) { tb.innerHTML = emptyRow(4, 'Membership API unavailable'); }
  }
  async function doVerifyScheme(doc) {
    const type = doc.dataset.schemeType;
    const uhid = pickedUhid(doc, 'schPatient');
    if (!uhid) { HIS.toast('Select a patient (F3) first'); return; }
    const member = val(doc, 'schMember');
    if (!member) { HIS.toast('Enter the member number'); return; }
    try {
      await HIS.api.schemeVerify({ patientUhid: uhid, schemeType: type, memberNo: member, secondaryRef: val(doc, 'schRef') || null });
      const note = doc.querySelector('#schVerifyNote');
      if (note) note.innerHTML = '<span class="pill pill--ok"><i class="bi bi-check-circle-fill"></i> Membership verified &amp; saved</span>';
      HIS.toast('Membership verified · ' + member, 'bi-patch-check');
      // Clear the form for the next entry (data is now safe in the list below).
      ['schPatient', 'schMember', 'schRef'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      const b = doc.querySelector('#schBanner'); if (b) b.innerHTML = banner(null);
      loadSchemeMemberships(doc, type);
    } catch (e) { HIS.toast('Verify failed: ' + e.message); }
  }

  /* ============ Claims MIS & Reconciliation (SRS §7.8) ============== */
  function claimsmis() {
    return `<div class="screen">
      ${head('bi-clipboard-data', 'Claims MIS &amp; Reconciliation', 'Claim analytics · status workflow · bank settlement matching',
        `<button class="btn btn--ghost btn--sm" id="cmRefresh"><i class="bi bi-arrow-clockwise"></i> Refresh</button>`)}
      <div class="kpis" id="cmKpis"><div class="muted" style="padding:12px">Loading…</div></div>
      <div class="panel mt12"><div class="panel__head"><i class="bi bi-kanban"></i> Claims <span class="ph-right hintline" id="cmCount"></span></div>
        <div class="panel__body tight">
          <div class="flex gap6 mb8" style="flex-wrap:wrap;align-items:center;padding:4px 0">
            <div class="field" style="max-width:240px"><input class="ctl" id="cmqText" placeholder="Search claim # / patient / payer…"></div>
            <select class="ctl" id="cmqStatus" style="max-width:160px"><option value="">All statuses</option></select>
            <div class="field with-unit"><input class="ctl" id="cmqFrom" type="date"><span class="unit">from</span></div>
            <div class="field with-unit"><input class="ctl" id="cmqTo" type="date"><span class="unit">to</span></div>
            <button class="btn btn--sm" id="cmqClear"><i class="bi bi-x-circle"></i> Clear</button>
          </div>
          <div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Claim #</th><th>Date</th><th>Patient</th><th>Payer</th><th class="num">Pre-Auth ₹</th><th class="num">Approved ₹</th><th>Status</th></tr></thead>
          <tbody id="cmClaims">${emptyRow(7, 'Loading…')}</tbody>
        </table></div></div>
      </div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-arrow-repeat"></i> Claim Workflow <span class="ph-right muted" id="cmSel">select a claim above</span></div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Action</label><div class="field"><select class="ctl" id="cmEvent">
              <option>Approval</option><option>Query</option><option>Shortfall</option><option>Enhancement</option><option>FinalBill</option><option>Denial</option><option>Settlement</option><option>Appeal</option>
            </select></div></div>
            <div class="f"><label>Amount</label><div class="field with-unit"><input class="ctl num" id="cmAmt" placeholder="0"><span class="unit">₹</span></div></div>
            <div class="f wide"><label>Notes</label><div class="field"><input class="ctl" id="cmNotes" placeholder="Remark (optional)"></div></div>
          </div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" data-act="save"><i class="bi bi-send"></i> Post Update <span class="fk">F9</span></button><span class="hintline">Approval/FinalBill/Settlement me amount bharo.</span></div>
          <div class="mt8" id="cmEventNote"></div>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-bank"></i> Settlement Reconciliation</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Claim</label><div class="field"><input class="ctl code" id="cmReconClaim" placeholder="select a claim" readonly></div></div>
            <div class="f"><label>Bank UTR <span class="req">*</span></label><div class="field"><input class="ctl code" id="cmUtr" placeholder="UTR / NEFT ref"></div></div>
            <div class="f"><label>Bank Amount <span class="req">*</span></label><div class="field with-unit"><input class="ctl num" id="cmBank" placeholder="0"><span class="unit">₹</span></div></div>
          </div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" id="cmReconBtn"><i class="bi bi-check2-square"></i> Reconcile</button><span class="hintline">Bank amount, claim ke settled amount se match hona chahiye.</span></div>
          <div class="mt8" id="cmReconNote"></div>
        </div></div>
      </div>
    </div>`;
  }
  function initClaimsMis(doc) {
    loadClaimsMisFull(doc);
    const rf = doc.querySelector('#cmRefresh'); if (rf) rf.addEventListener('click', () => loadClaimsMisFull(doc));
    ['cmqText', 'cmqStatus', 'cmqFrom', 'cmqTo'].forEach(id => {
      const el = doc.querySelector('#' + id); if (el) el.addEventListener('input', () => renderCmClaims(doc));
    });
    const clr = doc.querySelector('#cmqClear');
    if (clr) clr.addEventListener('click', () => {
      ['cmqText', 'cmqStatus', 'cmqFrom', 'cmqTo'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      renderCmClaims(doc);
    });
    // Row-click selects a claim for the workflow + reconciliation panels.
    const tb = doc.querySelector('#cmClaims');
    if (tb) tb.addEventListener('click', (e) => {
      const tr = e.target.closest('tr'); if (!tr || !tr.dataset.cid) return;
      selectCmClaim(doc, tr.dataset.cid, tr.dataset.cno);
      doc.querySelectorAll('#cmClaims tr').forEach(r => r.classList.remove('is-sel'));
      tr.classList.add('is-sel');
    });
    const rb = doc.querySelector('#cmReconBtn'); if (rb) rb.addEventListener('click', () => doReconcile(doc));
  }
  function selectCmClaim(doc, cid, cno) {
    doc.dataset.cmClaimId = cid;
    const sel = doc.querySelector('#cmSel'); if (sel) sel.textContent = cno || ('#' + cid);
    const rc = doc.querySelector('#cmReconClaim'); if (rc) rc.value = cno || ('#' + cid);
  }
  async function loadClaimsMisFull(doc) {
    const tb = doc.querySelector('#cmClaims'); if (!tb) return;
    try {
      const mis = await HIS.api.claimsMis();
      doc._cmClaims = mis.claims || [];
      // KPI tiles from status counts
      const kp = doc.querySelector('#cmKpis');
      if (kp) {
        const total = doc._cmClaims.length;
        const tiles = (mis.counts || []).map(c => `<div class="kpi"><div class="v tnum">${c.count}</div><div class="l">${c.status}</div></div>`).join('');
        kp.innerHTML = `<div class="kpi"><div class="v tnum">${total}</div><div class="l">Total claims</div></div>` + tiles;
      }
      // status filter options
      const ssel = doc.querySelector('#cmqStatus');
      if (ssel && ssel.options.length <= 1) {
        [...new Set(doc._cmClaims.map(c => c.status))].sort().forEach(s => {
          const o = document.createElement('option'); o.value = s; o.textContent = s; ssel.appendChild(o);
        });
      }
      renderCmClaims(doc);
    } catch (e) { doc._cmClaims = []; tb.innerHTML = emptyRow(7, 'Claims API unavailable'); }
  }
  function renderCmClaims(doc) {
    const tb = doc.querySelector('#cmClaims'); if (!tb) return;
    const all = doc._cmClaims || [];
    const q = (val(doc, 'cmqText') || '').toLowerCase();
    const st = val(doc, 'cmqStatus') || '';
    const from = val(doc, 'cmqFrom') || '', to = val(doc, 'cmqTo') || '';
    const rows = all.filter(r => {
      if (q && !`${r.claimNo} ${r.patient} ${r.payer}`.toLowerCase().includes(q)) return false;
      if (st && r.status !== st) return false;
      const d = r.submittedUtc || '';
      if (from && (!d || d < from)) return false;
      if (to && (!d || d > to)) return false;
      return true;
    });
    const pill = s => ({ Settled: 'pill--purple', Approved: 'pill--ok', Denied: 'pill--danger', Query: 'pill--warn', Shortfall: 'pill--danger' }[s] || 'pill--info');
    tb.innerHTML = rows.length ? rows.map(r =>
      `<tr data-cid="${r.claimId}" data-cno="${r.claimNo}" style="cursor:pointer"><td>${r.claimNo}</td><td>${r.submittedUtc ?? '—'}</td><td>${r.patient}</td><td>${r.payer}</td><td class="num">${r.preAuth ?? '—'}</td><td class="num">${r.approved ?? '—'}</td><td><span class="pill ${pill(r.status)}">${r.status}</span></td></tr>`
    ).join('') : emptyRow(7, 'No matching claims');
    const cnt = doc.querySelector('#cmCount'); if (cnt) cnt.textContent = `showing ${rows.length} of ${all.length} · click a row to action`;
  }
  async function doPostClaimEvent(doc) {
    const cid = doc.dataset.cmClaimId;
    if (!cid) { HIS.toast('Select a claim from the table first'); return; }
    try {
      await HIS.api.claimEvent(cid, { eventType: val(doc, 'cmEvent'), amount: numOrNull(val(doc, 'cmAmt')), notes: val(doc, 'cmNotes') || null });
      const note = doc.querySelector('#cmEventNote');
      if (note) note.innerHTML = `<span class="pill pill--ok"><i class="bi bi-check-circle-fill"></i> ${val(doc, 'cmEvent')} posted</span>`;
      HIS.toast('Claim updated · ' + val(doc, 'cmEvent'), 'bi-send');
      loadClaimsMisFull(doc);
    } catch (e) { HIS.toast('Update failed: ' + e.message); }
  }
  async function doReconcile(doc) {
    const cid = doc.dataset.cmClaimId;
    if (!cid) { HIS.toast('Select a claim from the table first'); return; }
    const utr = val(doc, 'cmUtr'); const bank = numOrNull(val(doc, 'cmBank'));
    if (!utr) { HIS.toast('Enter the bank UTR'); return; }
    if (!bank || bank <= 0) { HIS.toast('Enter the bank amount'); return; }
    try {
      const r = await HIS.api.reconcile({ claimId: Number(cid), utr, bankAmount: bank });
      const note = doc.querySelector('#cmReconNote');
      const ok = r.matched;
      if (note) note.innerHTML = `<span class="pill ${ok ? 'pill--ok' : 'pill--danger'}"><i class="bi ${ok ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill'}"></i> ${r.status}${ok ? '' : ' — bank amount ≠ settled amount'}</span>`;
      HIS.toast('Reconciliation: ' + r.status, ok ? 'bi-check2-square' : 'bi-exclamation-triangle');
    } catch (e) { HIS.toast('Reconcile failed: ' + e.message); }
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
        <div class="panel"><div class="panel__head"><i class="bi bi-truck-front"></i> Fleet
          <span class="ph-right"><input class="ctl code" id="ambNewVeh" placeholder="e.g. UP32-AB-1200" style="max-width:150px">
          <button class="btn btn--sm btn--primary" id="btnAddAmb"><i class="bi bi-plus-lg"></i> Add</button></span></div>
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

  /* ====================== DIET & KITCHEN (SRS §3.26) =============== */
  function diet() {
    return `<div class="screen">
      ${head('bi-egg-fried', 'Diet &amp; Kitchen', 'Therapeutic diet orders for admitted patients · kitchen worklist',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-clipboard2-check"></i> Order Diet <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-pulse"></i> Order Diet</div><div class="panel__body">
          <div class="form-grid">
            <div class="f wide"><label>Admitted Patient <span class="req">*</span></label><div class="field"><select class="ctl" id="dtAdmission"><option value="">Loading admitted patients…</option></select></div></div>
            <div class="f"><label>Diet Type <span class="req">*</span></label><div class="field"><select class="ctl" id="dtType">
              <option>Normal</option><option>Diabetic</option><option>Renal</option><option>Cardiac / Low-salt</option><option>Soft</option><option>Liquid</option><option>High-protein</option><option>Low-fat</option><option>NPO (Nil by mouth)</option>
            </select></div></div>
            <div class="f"><label>Cost / day</label><div class="field with-unit"><input class="ctl num" id="dtCost" placeholder="0"><span class="unit">₹</span></div></div>
          </div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" data-act="save"><i class="bi bi-clipboard2-check"></i> Order Diet <span class="fk">F9</span></button><span class="hintline">Admitted patient chuno → diet type + cost → Order. Kitchen worklist me aa jayega.</span></div>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-egg-fried"></i> Kitchen Worklist <span class="ph-right hintline" id="dtCount"></span></div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>#</th><th>Patient</th><th>Diet Type</th><th class="num">Cost ₹</th></tr></thead>
            <tbody id="dtOrders">${emptyRow(4, 'Loading…')}</tbody>
          </table></div></div>
        </div>
      </div>
    </div>`;
  }

  /* ====================== MORTUARY & DEATH (SRS §3.27) =========== */
  function mortuary() {
    return `<div class="screen">
      ${head('bi-file-earmark-x', 'Mortuary &amp; Death', 'Body intake · storage · MLC / police intimation · release',
        `<button class="btn btn--primary btn--sm" data-act="save"><i class="bi bi-box-arrow-in-down"></i> Admit Body <span class="fk">F9</span></button>`)}
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-plus"></i> Body Intake</div><div class="panel__body">
          <div class="form-grid">
            <div class="f wide"><label>Deceased (Patient)</label><div class="field with-btn"><input class="ctl" id="moPatient" data-lookup="patient" placeholder="F3 patient / UHID… (blank = unidentified)"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Storage / Freezer No <span class="req">*</span></label><div class="field"><input class="ctl code" id="moStorage" placeholder="e.g. MF-04"></div></div>
            <div class="f"><label>MLC Linked</label><div class="field"><select class="ctl" id="moMlc"><option value="false">No</option><option value="true">Yes — medico-legal</option></select></div></div>
            <div class="f"><label>Police Intimated</label><div class="field"><select class="ctl" id="moPolice"><option value="false">No</option><option value="true">Yes</option></select></div></div>
          </div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" data-act="save"><i class="bi bi-box-arrow-in-down"></i> Admit Body <span class="fk">F9</span></button><span class="hintline">Storage no. zaroori. MLC ho to police intimation mark karo.</span></div>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-snow"></i> Mortuary Register <span class="ph-right hintline" id="moCount"></span></div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>#</th><th>Deceased</th><th>Storage</th><th>Admitted</th><th>MLC</th><th>Status</th><th></th></tr></thead>
            <tbody id="moRegister">${emptyRow(7, 'Loading…')}</tbody>
          </table></div></div>
        </div>
      </div>
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

  /* ====================== EMERGENCY & TRAUMA — Triage (SRS §3.5) ========= */
  function emergency() {
    return `<div class="screen">
      ${head('bi-truck-front', 'Emergency &amp; Trauma — Triage', 'Arrival · 5-level colour triage · disposition', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-pulse"></i> Triage Board — live
        <span class="ph-right legend-row">
          <span><i class="sw" style="background:#e5484d"></i>1 Red</span>
          <span><i class="sw" style="background:#f5820e"></i>2 Orange</span>
          <span><i class="sw" style="background:#f2c94c"></i>3 Yellow</span>
          <span><i class="sw" style="background:#37a35e"></i>4 Green</span>
          <span><i class="sw" style="background:#3b82f6"></i>5 Blue</span>
        </span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Acuity</th><th>Patient</th><th>Complaint</th><th>Arrived</th><th>Mode</th><th>MLC</th><th>Status</th><th></th></tr></thead>
          <tbody id="erBoard">${emptyRow(8, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-plus-square"></i> New Arrival + Triage</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Patient</label><div class="field with-btn"><input class="ctl" id="erPatient" data-lookup="patient" placeholder="F3 patient (blank = unidentified)…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Arrival Mode</label><div class="field"><select class="ctl" id="erMode"><option>Ambulance</option><option>Walk-in</option><option>Referral</option><option>Police</option><option>BroughtDead</option></select></div></div>
            <div class="f wide"><label>Chief Complaint</label><div class="field"><input class="ctl" id="erComplaint" placeholder="e.g. RTA polytrauma"></div></div>
            <div class="f"><label>Acuity (colour)</label><div class="field"><select class="ctl" id="erColour"><option value="Red">1 · Red · Resuscitation</option><option value="Orange">2 · Orange · Emergent</option><option value="Yellow" selected>3 · Yellow · Urgent</option><option value="Green">4 · Green · Less urgent</option><option value="Blue">5 · Blue · Non-urgent</option></select></div></div>
            <div class="f"><label>Attending Doctor</label><div class="field with-btn"><input class="ctl" id="erDoctor" data-lookup="doctor" placeholder="F3 doctor…"><button class="lk" data-lookup="doctor">F3</button></div></div>
            <div class="f"><label style="display:flex;align-items:center;gap:6px;height:34px"><input type="checkbox" id="erMlc"> Medico-legal (MLC)</label></div>
          </div>
          <div class="subhead mt12">Triage Vitals</div>
          <div class="form-grid three">
            <div class="f"><label>Temp °F</label><div class="field"><input class="ctl" id="erTemp"></div></div>
            <div class="f"><label>Pulse</label><div class="field"><input class="ctl" id="erPulse"></div></div>
            <div class="f"><label>BP</label><div class="field"><input class="ctl" id="erBp" placeholder="120/80"></div></div>
            <div class="f"><label>SpO₂</label><div class="field"><input class="ctl" id="erSpo2"></div></div>
            <div class="f"><label>Resp</label><div class="field"><input class="ctl" id="erResp"></div></div>
            <div class="f"><label>GRBS</label><div class="field"><input class="ctl" id="erGrbs"></div></div>
            <div class="f"><label>GCS (3–15)</label><div class="field"><input class="ctl" id="erGcs"></div></div>
            <div class="f"><label>Pain (0–10)</label><div class="field"><input class="ctl" id="erPain"></div></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-clipboard-check"></i> Register &amp; Triage <span class="fk">F9</span></button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-box-arrow-right"></i> Disposition <span class="ph-right muted" id="erDispWho">select a board row</span></div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Disposition</label><div class="field"><select class="ctl" id="erDisp"><option>AdmitICU</option><option>AdmitWard</option><option>Discharge</option><option>Refer</option><option>LAMA</option><option>Expired</option></select></div></div>
            <div class="f"><label>Bed (for admit)</label><div class="field with-btn"><input class="ctl" id="erBed" data-lookup="ward" placeholder="F3 free bed…"><button class="lk" data-lookup="ward">F3</button></div></div>
            <div class="f"><label>Consultant</label><div class="field with-btn"><input class="ctl" id="erDispDoctor" data-lookup="doctor" placeholder="F3 doctor…"><button class="lk" data-lookup="doctor">F3</button></div></div>
          </div>
          <button class="btn mt8" style="width:100%" id="erDisposeBtn"><i class="bi bi-check2-circle"></i> Confirm Disposition</button>
        </div></div>
      </div>
    </div>`;
  }

  /* ====================== ICU MONITORING (SRS §3.6) ========= */
  function icu() {
    return `<div class="screen">
      ${head('bi-activity', 'ICU Monitoring', 'Critical-care census · monitoring flowsheet', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-people"></i> ICU Census <span class="ph-right muted" id="icuCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Patient</th><th>UHID</th><th>Ward</th><th>Bed</th><th>Consultant</th><th></th></tr></thead>
          <tbody id="icuCensus">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
      <div id="icuBanner"><div class="pbanner selectable"><div class="av">—</div>
        <div><div class="nm">No ICU patient selected</div>
        <div class="meta"><span>Pick a patient from the ICU census above to monitor</span></div></div></div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-heart-pulse"></i> Record Observation</div><div class="panel__body">
          <div class="form-grid three">
            <div class="f"><label>HR</label><div class="field"><input class="ctl" id="icuHr"></div></div>
            <div class="f"><label>BP</label><div class="field"><input class="ctl" id="icuBp" placeholder="120/80"></div></div>
            <div class="f"><label>SpO₂</label><div class="field"><input class="ctl" id="icuSpo2"></div></div>
            <div class="f"><label>Resp</label><div class="field"><input class="ctl" id="icuResp"></div></div>
            <div class="f"><label>Temp °F</label><div class="field"><input class="ctl" id="icuTemp"></div></div>
            <div class="f"><label>GCS</label><div class="field"><input class="ctl" id="icuGcs"></div></div>
            <div class="f"><label>FiO₂ %</label><div class="field"><input class="ctl" id="icuFio2"></div></div>
            <div class="f"><label>Vent Mode</label><div class="field"><select class="ctl" id="icuVent"><option value=""></option><option>RoomAir</option><option>CPAP</option><option>SIMV</option><option>AC</option></select></div></div>
            <div class="f"><label>Urine (ml)</label><div class="field"><input class="ctl" id="icuUrine"></div></div>
          </div>
          <div class="f wide" style="margin-top:6px"><label>Notes</label><div class="field"><input class="ctl" id="icuNotes"></div></div>
          <button class="btn btn--primary mt8" style="width:100%" id="icuSaveBtn"><i class="bi bi-check2-circle"></i> Record Observation</button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-graph-up"></i> Flowsheet</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Time</th><th>HR</th><th>BP</th><th>MAP</th><th>SpO₂</th><th>RR</th><th>Temp</th><th>GCS</th><th>FiO₂</th><th>Urine</th><th>Vent</th></tr></thead>
            <tbody id="icuFlow">${emptyRow(11, 'Select an ICU patient above')}</tbody>
          </table></div></div></div>
      </div>
    </div>`;
  }

  /* ====================== VITALS STATION (SRS §3.2 — dedicated desk) ========= */
  function vitals() {
    return `<div class="screen">
      ${head('bi-heart-pulse', 'Vitals Station', 'Record vitals for booked patients — sends them to the doctor lobby', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-list-check"></i> Vitals Worklist — today
        <span class="ph-right"><select class="ctl" id="vstDoctor" style="width:210px;display:inline-block"><option value="">— all doctors —</option></select></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Token</th><th>Patient</th><th>UHID</th><th>Doctor</th><th>Status</th><th></th></tr></thead>
          <tbody id="vstQueue">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="panel" id="vstStation" hidden><div class="panel__head"><i class="bi bi-heart-pulse"></i> Record Vitals <span class="ph-right muted" id="vstWho"></span></div>
        <div class="panel__body">
          <div class="form-grid three">
            <div class="f"><label>Temp</label><div class="field with-unit"><input class="ctl" id="vstTemp"><span class="unit">°F</span></div></div>
            <div class="f"><label>Pulse</label><div class="field with-unit"><input class="ctl" id="vstPulse"><span class="unit">/min</span></div></div>
            <div class="f"><label>BP</label><div class="field with-unit"><input class="ctl" id="vstBp" placeholder="120/80"><span class="unit">mmHg</span></div></div>
            <div class="f"><label>SpO₂</label><div class="field with-unit"><input class="ctl" id="vstSpo2"><span class="unit">%</span></div></div>
            <div class="f"><label>Resp. Rate</label><div class="field with-unit"><input class="ctl" id="vstResp"><span class="unit">/min</span></div></div>
            <div class="f"><label>Weight</label><div class="field with-unit"><input class="ctl" id="vstWeight"><span class="unit">kg</span></div></div>
          </div>
          <div class="flex gap6 mt8"><button class="btn btn--primary" id="vstSave"><i class="bi bi-check2-circle"></i> Save Vitals &amp; Send to Doctor</button><button class="btn" id="vstCancel">Cancel</button></div>
        </div></div>
    </div>`;
  }

  /* ====================== OPERATION THEATRE (SRS §3.12) ========= */
  function ot() {
    return `<div class="screen">
      ${head('bi-scissors', 'Operation Theatre (OT)', 'Schedule surgery · theatre board · post-op', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-calendar2-week"></i> OT Board <span class="ph-right muted" id="otCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Scheduled</th><th>Patient</th><th>Procedure</th><th>Surgeon</th><th>Theatre</th><th>Status</th><th></th></tr></thead>
          <tbody id="otBoard">${emptyRow(7, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-plus-square"></i> Schedule Surgery</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="otPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Surgeon</label><div class="field with-btn"><input class="ctl" id="otSurgeon" data-lookup="doctor" placeholder="F3 surgeon…"><button class="lk" data-lookup="doctor">F3</button></div></div>
            <div class="f"><label>Theatre</label><div class="field"><select class="ctl" id="otTheatre"><option>OT-1</option><option>OT-2</option><option>OT-3</option><option>Emergency OT</option></select></div></div>
            <div class="f"><label>Date &amp; Time <span class="req">*</span></label><div class="field"><input class="ctl" id="otWhen" type="datetime-local"></div></div>
            <div class="f wide"><label>Procedure</label><div class="field"><input class="ctl" id="otProcedure" placeholder="e.g. Laparoscopic appendectomy"></div></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-calendar-plus"></i> Schedule Surgery <span class="fk">F9</span></button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-clipboard2-check"></i> Post-op / Complete <span class="ph-right muted" id="otPostWho">select a case</span></div><div class="panel__body">
          <div class="f wide"><label>Post-op Notes</label><div class="field"><textarea class="ctl" id="otPostNotes" placeholder="Findings, complications, recovery plan…"></textarea></div></div>
          <button class="btn mt8" style="width:100%" id="otCompleteBtn"><i class="bi bi-check2-circle"></i> Complete Surgery</button>
        </div></div>
      </div>
    </div>`;
  }

  /* ====================== NURSING & PATIENT CARE (SRS §3.13) ========= */
  function nursing() {
    return `<div class="screen">
      ${head('bi-clipboard2-heart', 'Nursing &amp; Patient Care', 'Admitted patients · nursing notes (vitals / MAR / handover / care-plan)', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-people"></i> Admitted Patients <span class="ph-right muted" id="nrCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Patient</th><th>UHID</th><th>Ward</th><th>Bed</th><th>Consultant</th><th></th></tr></thead>
          <tbody id="nrCensus">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
      <div id="nrBanner"><div class="pbanner selectable"><div class="av">—</div>
        <div><div class="nm">No patient selected</div>
        <div class="meta"><span>Pick an admitted patient above to record nursing notes</span></div></div></div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-journal-plus"></i> Add Nursing Note</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Note Type</label><div class="field"><select class="ctl" id="nrType"><option>Vitals</option><option>MAR</option><option>Handover</option><option>CarePlan</option></select></div></div>
            <div class="f wide"><label>Note</label><div class="field"><textarea class="ctl" id="nrNote" placeholder="e.g. BP 120/80, PR 78, comfortable · Inj Ceftriaxone 1g IV given 10:00"></textarea></div></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" id="nrSaveBtn"><i class="bi bi-check2-circle"></i> Save Note</button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-clock-history"></i> Notes Timeline</div>
          <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
            <thead><tr><th>Time</th><th>Type</th><th>Note</th></tr></thead>
            <tbody id="nrNotes">${emptyRow(3, 'Select an admitted patient above')}</tbody>
          </table></div></div></div>
      </div>
    </div>`;
  }

  /* ====================== RADIOLOGY & IMAGING (SRS §3.9) ========= */
  function radiology() {
    return `<div class="screen">
      ${head('bi-radioactive', 'Radiology &amp; Imaging', 'Order studies · worklist · report', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-card-list"></i> Radiology Worklist <span class="ph-right muted" id="radCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Order</th><th>Patient</th><th>Modality</th><th>Study</th><th>Status</th><th></th></tr></thead>
          <tbody id="radWorklist">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-plus-square"></i> Order Study</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="radPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
            <div class="f"><label>Modality <span class="req">*</span></label><div class="field"><select class="ctl" id="radModality"><option>X-Ray</option><option>CT</option><option>MRI</option><option>USG</option><option>ECG</option><option>Mammography</option></select></div></div>
            <div class="f wide"><label>Study</label><div class="field"><input class="ctl" id="radStudy" placeholder="e.g. Chest PA · CT Brain plain"></div></div>
            <div class="f"><label style="display:flex;align-items:center;gap:6px;height:34px"><input type="checkbox" id="radPcpndt"> PC-PNDT regulated (USG)</label></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-plus-lg"></i> Order Study <span class="fk">F9</span></button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-file-earmark-medical"></i> File Report <span class="ph-right muted" id="radRepWho">select an order</span></div><div class="panel__body">
          <div class="f wide"><label>Report / findings</label><div class="field"><textarea class="ctl" id="radReport" placeholder="Impression / findings, or a link to the PACS/DICOM report…"></textarea></div></div>
          <button class="btn mt8" style="width:100%" id="radReportBtn"><i class="bi bi-check2-circle"></i> File Report</button>
        </div></div>
      </div>
    </div>`;
  }

  /* ====================== CERTIFICATES & DOCUMENTS (SRS §3.16) ========= */
  function certificates() {
    return `<div class="screen">
      ${head('bi-file-earmark-text', 'Certificates &amp; Documents', 'Issue medical certificates · doctor approval', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-card-checklist"></i> Issued Certificates <span class="ph-right muted" id="certCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Cert #</th><th>Type</th><th>Patient</th><th>Status</th><th></th></tr></thead>
          <tbody id="certList">${emptyRow(5, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="cols-side">
        <div class="panel"><div class="panel__head"><i class="bi bi-file-earmark-plus"></i> Issue Certificate</div><div class="panel__body">
          <div class="form-grid">
            <div class="f"><label>Certificate Type <span class="req">*</span></label><div class="field"><select class="ctl" id="certTemplate"><option value="">— select —</option></select></div></div>
            <div class="f"><label>Patient <span class="req">*</span></label><div class="field with-btn"><input class="ctl" id="certPatient" data-lookup="patient" placeholder="F3 patient / UHID…"><button class="lk" data-lookup="patient">F3</button></div></div>
          </div>
          <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-file-earmark-plus"></i> Issue Certificate <span class="fk">F9</span></button>
        </div></div>
        <div class="panel"><div class="panel__head"><i class="bi bi-patch-check"></i> Approve (doctor sign) <span class="ph-right muted" id="certApWho">select a certificate</span></div><div class="panel__body">
          <div class="f"><label>Approving Doctor</label><div class="field with-btn"><input class="ctl" id="certDoctor" data-lookup="doctor" placeholder="F3 doctor…"><button class="lk" data-lookup="doctor">F3</button></div></div>
          <button class="btn mt8" style="width:100%" id="certApproveBtn"><i class="bi bi-check2-circle"></i> Approve Certificate</button>
        </div></div>
      </div>
    </div>`;
  }

  /* ====================== DRUG MASTER (pharmacy catalogue) ========= */
  function drugmaster() {
    return `<div class="screen">
      ${head('bi-capsule-pill', 'Drug Master', 'Manage the pharmacy drug catalogue', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-card-list"></i> Drugs <span class="ph-right muted" id="dmCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Code</th><th>Name</th><th>Form</th><th class="num">Stock</th><th class="num">Reorder</th><th>Status</th><th></th></tr></thead>
          <tbody id="dmList">${emptyRow(7, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-capsule"></i> <span id="dmFormTitle">Add Drug</span></div><div class="panel__body">
        <div class="form-grid three">
          <div class="f"><label>Code <span class="req">*</span></label><div class="field"><input class="ctl code" id="dmCode" placeholder="e.g. PARA"></div></div>
          <div class="f"><label>Name <span class="req">*</span></label><div class="field"><input class="ctl" id="dmName" placeholder="e.g. Paracetamol 500mg"></div></div>
          <div class="f"><label>Form <span class="req">*</span></label><div class="field"><select class="ctl" id="dmForm"><option>TAB</option><option>CAP</option><option>INJ</option><option>IVF</option><option>SYP</option><option>OINT</option><option>DROP</option></select></div></div>
          <div class="f"><label>Reorder Level</label><div class="field"><input class="ctl num" id="dmReorder" placeholder="0"></div></div>
        </div>
        <div class="flex gap6 mt8"><button class="btn btn--primary" data-act="save"><i class="bi bi-check2-circle"></i> Save Drug <span class="fk">F9</span></button><button class="btn" id="dmReset"><i class="bi bi-arrow-counterclockwise"></i> New / Reset</button></div>
      </div></div>
    </div>`;
  }

  /* ====================== INVENTORY & STORE (SRS §3.11) ========= */
  function inventory() {
    return `<div class="screen">
      ${head('bi-boxes', 'Inventory &amp; Store', 'Stock levels · reorder · purchase orders', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-box-seam"></i> Stock Levels <span class="ph-right muted" id="invCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Code</th><th>Item</th><th class="num">Stock</th><th class="num">Reorder</th><th>Status</th></tr></thead>
          <tbody id="invStock">${emptyRow(5, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-receipt"></i> Purchase Orders <span class="ph-right muted" id="poCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>PO No.</th><th>Supplier</th><th class="num">Items</th><th class="num">Total ₹</th><th>Status</th><th>Raised</th></tr></thead>
          <tbody id="poList">${emptyRow(6, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-cart-plus"></i> Create Purchase Order
        <span class="ph-right"><button class="btn btn--sm" data-addrow="poGrid"><i class="bi bi-plus-lg"></i> Add Line</button></span></div>
        <div class="panel__body">
          <div class="form-grid"><div class="f"><label>Supplier <span class="req">*</span></label><div class="field"><select class="ctl" id="poSupplier"><option value="">— select —</option></select></div></div></div>
          <div class="grid-wrap grid--editable" style="border:0;margin-top:8px"><table class="grid" id="poGrid">
            <thead><tr><th style="width:55%">Item</th><th class="num">Qty</th><th class="num">Unit Price ₹</th><th></th></tr></thead>
            <tbody id="poBody"><tr>${TPL.poBody}</tr></tbody>
          </table></div>
          <div class="flex gap6 mt8" style="padding:8px 0"><button class="btn btn--primary" data-act="save"><i class="bi bi-cart-check"></i> Create PO <span class="fk">F9</span></button><span class="hintline">Pick a supplier · add items + qty · Create PO (Draft).</span></div>
        </div></div>
    </div>`;
  }

  /* ====================== BLOOD BANK (SRS §3.7) ========= */
  function bloodbank() {
    return `<div class="screen">
      ${head('bi-droplet-half', 'Blood Bank', 'Stock by group · requests · donor alerts', '')}
      <div class="panel"><div class="panel__head"><i class="bi bi-droplet"></i> Blood Stock <span class="ph-right muted" id="bbCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Blood Group</th><th class="num">Units</th><th class="num">Safety Threshold</th><th>Status</th></tr></thead>
          <tbody id="bbStock">${emptyRow(4, 'Loading…')}</tbody>
        </table></div>
          <div class="flex gap6 mt8" style="padding:8px 12px;align-items:center;flex-wrap:wrap">
            <span class="muted"><i class="bi bi-plus-circle"></i> Add stock (donation / receipt):</span>
            <select class="ctl" id="bbAddGroup" style="width:74px;display:inline-block"><option>A+</option><option>A-</option><option>B+</option><option>B-</option><option>O+</option><option>O-</option><option>AB+</option><option>AB-</option></select>
            <input class="ctl num" id="bbAddUnits" placeholder="units" style="width:72px;display:inline-block">
            <button class="btn btn--sm btn--primary" id="bbAddBtn"><i class="bi bi-plus-lg"></i> Add Units</button>
          </div>
        </div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-receipt"></i> Blood Requests <span class="ph-right muted" id="bbReqCount"></span></div>
        <div class="panel__body tight"><div class="grid-wrap" style="border:0"><table class="grid">
          <thead><tr><th>Req #</th><th>Patient</th><th>Group</th><th class="num">Units</th><th>Priority</th><th>Status</th><th>Raised</th><th></th></tr></thead>
          <tbody id="bbReqList">${emptyRow(8, 'Loading…')}</tbody>
        </table></div></div></div>
      <div class="panel"><div class="panel__head"><i class="bi bi-clipboard-plus"></i> Raise Blood Request</div><div class="panel__body">
        <div class="form-grid">
          <div class="f"><label>Patient</label><div class="field with-btn"><input class="ctl" id="bbPatient" data-lookup="patient" placeholder="F3 patient (optional)…"><button class="lk" data-lookup="patient">F3</button></div></div>
          <div class="f"><label>Blood Group <span class="req">*</span></label><div class="field"><select class="ctl" id="bbGroup"><option>A+</option><option>A-</option><option>B+</option><option>B-</option><option>O+</option><option>O-</option><option>AB+</option><option>AB-</option></select></div></div>
          <div class="f"><label>Units <span class="req">*</span></label><div class="field"><input class="ctl num" id="bbUnits" placeholder="1"></div></div>
          <div class="f"><label style="display:flex;align-items:center;gap:6px;height:34px"><input type="checkbox" id="bbEmergency"> Emergency</label></div>
        </div>
        <button class="btn btn--primary mt8" style="width:100%" data-act="save"><i class="bi bi-droplet-half"></i> Raise Request <span class="fk">F9</span></button>
      </div></div>
    </div>`;
  }

  /* ============================ Registry ============================ */
  HIS.screens = { dashboard, registration, appointments, vitals, opd, ipd, emergency, icu, ot, nursing, radiology, certificates, drugmaster, inventory, bloodbank, billing, pharmacy, lab, cashless, pmjay, esic, cghs, echs, statescheme, claimsmis, hr, payroll, occhealth, telemedicine, ambulance, diet, mortuary, bmwm, mlc, queue, feedback, compliance, ai };

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
    const map = { rxBody: TPL.rxBody, chargeBody: TPL.chargeBody, dispBody: TPL.dispBody, labResultsBody: TPL.labResultBody, poBody: TPL.poBody };
    Object.keys(map).forEach(tb => { const el = doc.querySelector('#' + tb); if (el) el.dataset.tpl = map[tb]; });

    if (id === 'billing') { wireBilling(doc); HIS.saveHandlers.billing = () => doCreateBill(doc);
      const cb = doc.querySelector('#btnCreateBill'); if (cb) cb.addEventListener('click', () => doCreateBill(doc));
      const cp = doc.querySelector('#btnCollectPay'); if (cp) cp.addEventListener('click', () => doCollectPayment(doc)); }
    if (id === 'dashboard') loadDashboard(doc);
    if (id === 'registration') { initRegistration(doc); HIS.saveHandlers.registration = () => doRegister(doc); }
    if (id === 'vitals') { initVitals(doc); }
    if (id === 'ot') { initOt(doc); HIS.saveHandlers.ot = () => doScheduleSurgery(doc); }
    if (id === 'nursing') { initNursing(doc); }
    if (id === 'radiology') { initRadiology(doc); HIS.saveHandlers.radiology = () => doOrderStudy(doc); }
    if (id === 'certificates') { initCertificates(doc); HIS.saveHandlers.certificates = () => doIssueCertificate(doc); }
    if (id === 'drugmaster') { initDrugMaster(doc); HIS.saveHandlers.drugmaster = () => doSaveDrug(doc); }
    if (id === 'inventory') { initInventory(doc); HIS.saveHandlers.inventory = () => doCreatePo(doc); }
    if (id === 'bloodbank') { initBloodBank(doc); HIS.saveHandlers.bloodbank = () => doRaiseBloodRequest(doc); }
    if (id === 'emergency') { initEmergency(doc); HIS.saveHandlers.emergency = () => doRegisterTriage(doc); }
    if (id === 'icu') { initIcu(doc); }
    if (id === 'ipd') {
      loadBedBoard(doc); loadAdmissions(doc); HIS.saveHandlers.ipd = () => doAdmit(doc);
      const dbtn = doc.querySelector('#ipdDischargeBtn');
      if (dbtn) dbtn.addEventListener('click', () => {
        const t = doc.querySelector('#ipdAdmitted');
        if (t) t.scrollIntoView({ behavior: 'smooth', block: 'center' });
        HIS.toast('Pick a patient to discharge from the Admitted Patients list below', 'bi-box-arrow-right');
      });
      const tbtn = doc.querySelector('#ipdTransferBtn');
      if (tbtn) tbtn.addEventListener('click', () => HIS.toast('Transfer: admit flow handles bed moves — discharge & re-admit, or use the bed board'));
      // Show who is about to be admitted as soon as a patient is picked (F3).
      const pf = doc.querySelector('#ipdPatient');
      if (pf) { pf.addEventListener('change', () => showIpdPatient(doc)); pf.addEventListener('blur', () => showIpdPatient(doc)); }
    }
    if (id === 'appointments') { initAppointments(doc); HIS.saveHandlers.appointments = () => doBookAppointment(doc); }
    if (id === 'opd') { initOpd(doc); HIS.saveHandlers.opd = () => doSaveConsultation(doc); }
    if (id === 'lab') { initLab(doc); HIS.saveHandlers.lab = () => doEnterResults(doc); }
    if (id === 'pharmacy') { initPharmacy(doc); HIS.saveHandlers.pharmacy = () => doDispense(doc); }
    if (id === 'cashless') { initCashless(doc); HIS.saveHandlers.cashless = () => doSubmitPreAuth(doc); }
    if (id === 'pmjay') { initPmjay(doc); HIS.saveHandlers.pmjay = () => doSubmitTms(doc); }
    if (id === 'esic' || id === 'cghs' || id === 'echs' || id === 'statescheme') { initScheme(doc, id); HIS.saveHandlers[id] = () => doVerifyScheme(doc); }
    if (id === 'claimsmis') { initClaimsMis(doc); HIS.saveHandlers.claimsmis = () => doPostClaimEvent(doc); }
    if (id === 'hr') { initHr(doc); HIS.saveHandlers.hr = () => doAddStaff(doc); }
    if (id === 'payroll') { initPayroll(doc); HIS.saveHandlers.payroll = () => doRunPayroll(doc); }
    if (id === 'occhealth') { initOccHealth(doc); HIS.saveHandlers.occhealth = () => doConductExam(doc); }
    if (id === 'telemedicine') { initTele(doc); HIS.saveHandlers.telemedicine = () => doScheduleTele(doc); }
    if (id === 'ambulance') { initAmbulance(doc); }
    if (id === 'diet') { initDiet(doc); HIS.saveHandlers.diet = () => doOrderDiet(doc); }
    if (id === 'mortuary') { initMortuary(doc); HIS.saveHandlers.mortuary = () => doAdmitBody(doc); }
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
    const ab = doc.querySelector('#btnAddAmb'); if (ab) ab.addEventListener('click', () => doAddAmbulance(doc));
    const nv = doc.querySelector('#ambNewVeh'); if (nv) nv.addEventListener('keydown', e => { if (e.key === 'Enter') doAddAmbulance(doc); });
  }
  async function doAddAmbulance(doc) {
    const veh = val(doc, 'ambNewVeh');
    if (!veh) { HIS.toast('Enter a vehicle number'); return; }
    try {
      const a = await HIS.api.addAmbulance({ vehicleNo: veh });
      HIS.toast('Ambulance added · ' + a.vehicleNo, 'bi-truck-front');
      const nv = doc.querySelector('#ambNewVeh'); if (nv) nv.value = '';
      loadFleet(doc); initGps(doc);   // refresh fleet + GPS label map
    } catch (e) { HIS.toast('Add failed: ' + e.message); }
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
  /* ---- Diet & Kitchen ------------------------------------------------ */
  async function initDiet(doc) {
    loadDietOrders(doc);
    try {
      const rows = await HIS.api.admittedPatients();
      const sel = doc.querySelector('#dtAdmission');
      if (sel) sel.innerHTML = rows.length
        ? '<option value="">— select admitted patient —</option>' + rows.map(a =>
            `<option value="${a.admissionId}">${a.patient} · ${a.ward || ''} ${a.bedNo || ''}</option>`).join('')
        : '<option value="">No admitted patients</option>';
    } catch (e) { /* ignore */ }
  }
  async function loadDietOrders(doc) {
    const tb = doc.querySelector('#dtOrders'); if (!tb) return;
    try {
      const rows = await HIS.api.dietList();
      tb.innerHTML = rows.length ? rows.map(d =>
        `<tr><td>${d.dietOrderId}</td><td>${d.patient}</td><td><span class="pill pill--info">${d.dietType}</span></td><td class="num">${d.cost ?? '—'}</td></tr>`
      ).join('') : emptyRow(4, 'No diet orders yet');
      const cnt = doc.querySelector('#dtCount'); if (cnt) cnt.textContent = rows.length ? `${rows.length} order(s)` : '';
    } catch (e) { tb.innerHTML = emptyRow(4, 'Diet API unavailable'); }
  }
  async function doOrderDiet(doc) {
    const adm = val(doc, 'dtAdmission');
    if (!adm) { HIS.toast('Select an admitted patient'); return; }
    try {
      await HIS.api.orderDiet({ admissionId: parseInt(adm, 10), dietType: val(doc, 'dtType'), cost: numOrNull(val(doc, 'dtCost')) });
      HIS.toast('Diet ordered', 'bi-egg-fried');
      // Reset the form for the next order (data is now safe in the worklist).
      const a = doc.querySelector('#dtAdmission'); if (a) a.value = '';
      const t = doc.querySelector('#dtType'); if (t) t.selectedIndex = 0;
      const c = doc.querySelector('#dtCost'); if (c) c.value = '';
      loadDietOrders(doc);
    } catch (e) { HIS.toast('Order failed: ' + e.message); }
  }

  /* ---- Mortuary & Death ---------------------------------------------- */
  function initMortuary(doc) { loadMortuary(doc); }
  async function loadMortuary(doc) {
    const tb = doc.querySelector('#moRegister'); if (!tb) return;
    try {
      const rows = await HIS.api.mortuaryList();
      tb.innerHTML = rows.length ? rows.map(m => {
        const released = !!m.released;
        return `<tr><td>${m.recordId}</td><td>${m.patient || '<span class="muted">Unidentified</span>'}</td><td class="code">${m.storageNo || '—'}</td><td>${m.admitted}</td>
          <td>${m.mlc ? '<span class="pill pill--danger">MLC</span>' : 'No'}</td>
          <td>${released ? `<span class="pill pill--ok">Released</span>` : '<span class="pill pill--warn">In storage</span>'}</td>
          <td>${released ? '✓' : `<button class="btn btn--sm" data-release="${m.recordId}"><i class="bi bi-box-arrow-up"></i> Release</button>`}</td></tr>`;
      }).join('') : emptyRow(7, 'No records yet');
      const cnt = doc.querySelector('#moCount'); if (cnt) { const inStore = rows.filter(m => !m.released).length; cnt.textContent = `${rows.length} record(s) · ${inStore} in storage`; }
      tb.querySelectorAll('[data-release]').forEach(b => b.addEventListener('click', () => doReleaseBody(doc, b.dataset.release)));
    } catch (e) { tb.innerHTML = emptyRow(7, 'Mortuary API unavailable'); }
  }
  async function doAdmitBody(doc) {
    const storage = val(doc, 'moStorage');
    if (!storage) { HIS.toast('Enter a storage / freezer number'); return; }
    try {
      await HIS.api.admitBody({
        patientUhid: pickedUhid(doc, 'moPatient') || null, storageNo: storage,
        mlcLinked: val(doc, 'moMlc') === 'true', policeIntimated: val(doc, 'moPolice') === 'true'
      });
      HIS.toast('Body admitted · ' + storage, 'bi-box-arrow-in-down');
      ['moPatient', 'moStorage'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      const mlc = doc.querySelector('#moMlc'); if (mlc) mlc.selectedIndex = 0;
      const pol = doc.querySelector('#moPolice'); if (pol) pol.selectedIndex = 0;
      loadMortuary(doc);
    } catch (e) { HIS.toast('Admit failed: ' + e.message); }
  }
  async function doReleaseBody(doc, recordId) {
    try {
      await HIS.api.releaseBody(recordId);
      HIS.toast('Body released', 'bi-box-arrow-up');
      loadMortuary(doc);
    } catch (e) { HIS.toast('Release failed: ' + e.message); }
  }

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
  let vitalsLiveDoc = null;         // the currently-open Vitals Station screen
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
    // OPD vitals-done / called / completed → refresh the doctor lobby + vitals worklist live.
    conn.on('opdChanged', () => {
      if (opdLiveDoc && document.body.contains(opdLiveDoc)) loadOpdLobby(opdLiveDoc);
      if (vitalsLiveDoc && document.body.contains(vitalsLiveDoc)) loadVitalsWorklist(vitalsLiveDoc);
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
    const uhid = pickedUhid(doc, 'ohPatient');
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
    const uhid = pickedUhid(doc, 'injPatient');
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
    const uhid = pickedUhid(doc, 'tmPatient'), docCode = pickedUhid(doc, 'tmDoctor');
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
    const pf = doc.querySelector('#caPatient');
    if (pf) { pf.addEventListener('change', () => showCaPatient(doc)); pf.addEventListener('blur', () => showCaPatient(doc)); }
    // Claim Tracking Dashboard filters — search text + status + date range (client-side)
    ['caqText', 'caqStatus', 'caqFrom', 'caqTo'].forEach(id => {
      const el = doc.querySelector('#' + id); if (el) el.addEventListener('input', () => renderCaClaims(doc));
    });
    const clr = doc.querySelector('#caqClear');
    if (clr) clr.addEventListener('click', () => {
      ['caqText', 'caqStatus', 'caqFrom', 'caqTo'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      renderCaClaims(doc);
    });
  }
  // Resolve the F3-picked patient for the claim, show them in the banner, and load their policies.
  async function showCaPatient(doc) {
    const raw = val(doc, 'caPatient'); const b = doc.querySelector('#caBanner');
    if (!raw) { delete doc.dataset.caUhid; if (b) b.innerHTML = banner(null); return; }
    let i = raw.indexOf('—'); if (i < 0) i = raw.indexOf(' - ');
    const uhid = (i > 0 ? raw.slice(0, i) : raw).trim();
    try {
      const p = await HIS.api.patientByUhid(uhid);
      if (!p) { HIS.toast('Patient not found: ' + uhid); return; }
      doc.dataset.caUhid = p.uhid;
      if (b) b.innerHTML = banner(p);
      loadEligibility(doc);
    } catch (e) {}
  }
  async function loadEligibility(doc) {
    const uhid = doc.dataset.caUhid; if (!uhid) return;
    try {
      const pols = await HIS.api.eligibility(uhid);
      const note = doc.querySelector('#caEligNote');
      const tb = doc.querySelector('#caPolicies');
      if (tb) tb.innerHTML = pols.length ? pols.map(p =>
        `<tr><td>${p.payer}</td><td class="code">${p.policyNo ?? '—'}</td><td class="num">${p.sumInsured ?? '—'}</td><td class="num">${p.coPayPct ?? '—'}</td><td class="num">${p.availableBalance ?? '—'}</td></tr>`
      ).join('') : emptyRow(5, 'No policy on file — capture one above');
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
    const uhid = doc.dataset.caUhid;
    if (!uhid) { HIS.toast('Select a patient (F3) first'); return; }
    const payer = val(doc, 'caPayer');
    if (!payer) { HIS.toast('Select a payer (F3)'); return; }
    try {
      await HIS.api.capturePolicy({
        patientUhid: uhid, payerCode: payer, policyNo: val(doc, 'caPolicy') || null,
        sumInsured: numOrNull(val(doc, 'caSumInsured')), coPayPct: numOrNull(val(doc, 'caCopay'))
      });
      HIS.toast('Policy captured', 'bi-shield-check');
      loadEligibility(doc);
    } catch (e) { HIS.toast('Capture failed: ' + e.message); }
  }
  async function doSubmitPreAuth(doc) {
    const uhid = doc.dataset.caUhid;
    if (!uhid) { HIS.toast('Select a patient (F3) first'); return; }
    const payer = val(doc, 'caPayer'), cost = numOrNull(val(doc, 'caCost'));
    if (!payer) { HIS.toast('Select a payer'); return; }
    if (!cost || cost <= 0) { HIS.toast('Enter estimated cost'); return; }
    try {
      const r = await HIS.api.createPreAuth({
        patientUhid: uhid, payerCode: payer, provisionalIcd10: val(doc, 'caDx') || null,
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
      doc._caClaims = mis.claims || [];
      // populate the status dropdown once from the data present
      const sel = doc.querySelector('#caqStatus');
      if (sel && sel.options.length <= 1) {
        [...new Set(doc._caClaims.map(c => c.status))].sort().forEach(s => {
          const o = document.createElement('option'); o.value = s; o.textContent = s; sel.appendChild(o);
        });
      }
      renderCaClaims(doc);
    } catch (e) { doc._caClaims = []; tb.innerHTML = emptyRow(7, 'Claims API unavailable'); }
  }
  // Apply search + status + date-range filters over the loaded claims (no reload).
  function renderCaClaims(doc) {
    const tb = doc.querySelector('#caClaims'); if (!tb) return;
    const all = doc._caClaims || [];
    const q = (val(doc, 'caqText') || '').toLowerCase();
    const st = val(doc, 'caqStatus') || '';
    const from = val(doc, 'caqFrom') || '', to = val(doc, 'caqTo') || '';
    const rows = all.filter(r => {
      if (q && !`${r.claimNo} ${r.patient} ${r.payer}`.toLowerCase().includes(q)) return false;
      if (st && r.status !== st) return false;
      const d = r.submittedUtc || '';
      if (from && (!d || d < from)) return false;
      if (to && (!d || d > to)) return false;
      return true;
    });
    const pill = s => ({ Settled: 'pill--purple', Approved: 'pill--ok', Denied: 'pill--danger', Query: 'pill--warn', Shortfall: 'pill--danger' }[s] || 'pill--info');
    tb.innerHTML = rows.length ? rows.map(r =>
      `<tr><td>${r.claimNo}</td><td>${r.submittedUtc ?? '—'}</td><td>${r.patient}</td><td>${r.payer}</td><td class="num">${r.preAuth ?? '—'}</td><td class="num">${r.approved ?? '—'}</td><td><span class="pill ${pill(r.status)}">${r.status}</span></td></tr>`
    ).join('') : emptyRow(7, 'No matching claims');
    const cnt = doc.querySelector('#caCount'); if (cnt) cnt.textContent = `showing ${rows.length} of ${all.length}`;
  }

  /* ---- Phase 7: PM-JAY verify + TMS submit --------------------------- */
  function initPmjay(doc) {
    const b = doc.querySelector('#btnPmVerify'); if (b) b.addEventListener('click', () => doVerifyBeneficiary(doc));
    loadPmjayCases(doc);
    ['pmqText', 'pmqStatus', 'pmqFrom', 'pmqTo'].forEach(id => {
      const el = doc.querySelector('#' + id); if (el) el.addEventListener('input', () => renderPmCases(doc));
    });
    const clr = doc.querySelector('#pmqClear');
    if (clr) clr.addEventListener('click', () => {
      ['pmqText', 'pmqStatus', 'pmqFrom', 'pmqTo'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      renderPmCases(doc);
    });
  }
  async function loadPmjayCases(doc) {
    const tb = doc.querySelector('#pmCases'); if (!tb) return;
    try {
      doc._pmCases = await HIS.api.pmjayCases() || [];
      const sel = doc.querySelector('#pmqStatus');
      if (sel && sel.options.length <= 1) {
        [...new Set(doc._pmCases.map(c => c.status))].sort().forEach(s => {
          const o = document.createElement('option'); o.value = s; o.textContent = s; sel.appendChild(o);
        });
      }
      renderPmCases(doc);
    } catch (e) { doc._pmCases = []; tb.innerHTML = emptyRow(7, 'TMS claims API unavailable'); }
  }
  // Apply search + status + date-range filters over the loaded TMS claims.
  function renderPmCases(doc) {
    const tb = doc.querySelector('#pmCases'); if (!tb) return;
    const all = doc._pmCases || [];
    const q = (val(doc, 'pmqText') || '').toLowerCase();
    const st = val(doc, 'pmqStatus') || '';
    const from = val(doc, 'pmqFrom') || '', to = val(doc, 'pmqTo') || '';
    const rows = all.filter(r => {
      if (q && !`${r.tmsCaseNo || ''} ${r.claimNo} ${r.patient} ${r.package || ''}`.toLowerCase().includes(q)) return false;
      if (st && r.status !== st) return false;
      const d = r.submittedUtc || '';
      if (from && (!d || d < from)) return false;
      if (to && (!d || d > to)) return false;
      return true;
    });
    const pill = s => ({ Settled: 'pill--purple', Approved: 'pill--ok', Denied: 'pill--danger', Query: 'pill--warn' }[s] || 'pill--info');
    tb.innerHTML = rows.length ? rows.map(r =>
      `<tr><td class="code">${r.tmsCaseNo || '—'}</td><td>${r.claimNo}</td><td>${r.submittedUtc ?? '—'}</td><td>${r.patient}</td><td>${r.package || '—'}</td><td class="num">${r.amount ?? '—'}</td><td><span class="pill ${pill(r.status)}">${r.status}</span></td></tr>`
    ).join('') : emptyRow(7, 'No matching TMS claims');
    const cnt = doc.querySelector('#pmCount'); if (cnt) cnt.textContent = all.length ? `showing ${rows.length} of ${all.length}` : '';
  }
  // Extract the UHID from an F3-filled "UHID — Name" field (split on em-dash only; UHIDs have hyphens).
  function pickedUhid(doc, id) {
    const raw = val(doc, id); if (!raw) return '';
    let i = raw.indexOf('—'); if (i < 0) i = raw.indexOf(' - ');
    return (i > 0 ? raw.slice(0, i) : raw).trim();
  }
  async function doVerifyBeneficiary(doc) {
    const uhid = pickedUhid(doc, 'pmPatient');
    if (!uhid) { HIS.toast('Select a patient (F3) first'); return; }
    const pmId = val(doc, 'pmId');
    if (!pmId) { HIS.toast('Enter PM-JAY ID'); return; }
    try {
      const r = await HIS.api.pmjayVerify({ patientUhid: uhid, pmjayId: pmId, familyFloater: 500000 });
      const note = doc.querySelector('#pmVerifyNote');
      if (note) note.innerHTML = '<span class="pill pill--ok"><i class="bi bi-check-circle-fill"></i> Beneficiary verified (BIS)</span>';
      HIS.toast('Beneficiary verified (BIS)', 'bi-fingerprint');
    } catch (e) { HIS.toast('Verify failed: ' + e.message); }
  }
  async function doSubmitTms(doc) {
    const uhid = pickedUhid(doc, 'pmPatient');
    if (!uhid) { HIS.toast('Select a patient (F3) first'); return; }
    const pkg = val(doc, 'pmPackage');
    if (!pkg) { HIS.toast('Select an HBP package (F3)'); return; }
    try {
      const r = await HIS.api.pmjayClaim({ patientUhid: uhid, packageCode: pkg, ayushmanMitra: val(doc, 'pmMitra') || null });
      const tms = doc.querySelector('#pmTms'); if (tms) tms.textContent = r.tmsCaseNo;
      const stage = doc.querySelector('#pmStage'); if (stage) { stage.textContent = 'Pre-Auth submitted'; stage.className = 'pill pill--info'; }
      HIS.toast('Submitted to TMS · ' + r.tmsCaseNo + ' · ₹' + r.packageRate, 'bi-send');
      loadPmjayCases(doc);
    } catch (e) { HIS.toast('TMS submit failed: ' + e.message); }
  }

  /* ---- Phase 4: pharmacy queue + alerts + dispense -------------------- */
  function initPharmacy(doc) {
    loadPharmaQueue(doc);
    loadPharmaAlerts(doc);
    // When a drug is picked (F3) in a dispense row, auto-fill batch/expiry/MRP (FEFO).
    const grid = doc.querySelector('#dispBody');
    if (grid) {
      const onDrug = (e) => { const el = e.target; if (el && el.matches && el.matches('[data-lookup="drug"]')) fillDrugBatch(doc, el.closest('tr')); };
      grid.addEventListener('change', onDrug); grid.addEventListener('blur', onDrug, true);
      // Live line Amount = Qty x MRP as either is typed.
      grid.addEventListener('input', (e) => { const el = e.target; if (el && el.classList && el.classList.contains('num')) recalcDispRow(el.closest('tr'), doc); });
    }
  }
  function recalcDispRow(tr, doc) {
    if (!tr) return;
    const i = tr.querySelectorAll('input');
    const qty = parseFloat(i[3] ? i[3].value : '') || 0;
    const mrp = parseFloat(i[4] ? i[4].value : '') || 0;
    const cell = tr.children[5]; if (cell) cell.textContent = (qty * mrp).toFixed(2);
    // Grand total across all lines.
    if (doc) {
      let tot = 0;
      doc.querySelectorAll('#dispBody tr').forEach(r => {
        const x = r.querySelectorAll('input');
        tot += (parseFloat(x[3] ? x[3].value : '') || 0) * (parseFloat(x[4] ? x[4].value : '') || 0);
      });
      const t = doc.querySelector('#dispTotal'); if (t) t.textContent = '₹' + tot.toFixed(2);
    }
  }
  async function fillDrugBatch(doc, tr) {
    if (!tr) return;
    const i = tr.querySelectorAll('input');
    const raw = i[0] ? i[0].value.trim() : '';
    if (!raw) return;
    let k = raw.indexOf('—'); if (k < 0) k = raw.indexOf(' - ');
    let code = (k > 0 ? raw.slice(0, k) : raw).trim();
    try {
      let batches = await HIS.api.drugBatches(code);
      // Smart resolve: if a name/partial was typed (not an F3 pick), match a drug in the master.
      if ((!batches || !batches.length) && k < 0) {
        const found = await HIS.api.lookup('drug', raw);
        if (found && found.rows && found.rows.length) {
          const row = found.rows[0];          // [Code, Name, Form, Stock]
          code = row[0];
          if (i[0]) i[0].value = row[0] + ' — ' + row[1];   // auto-correct to "CODE — Name"
          batches = await HIS.api.drugBatches(code);
        }
      }
      if (!batches || !batches.length) { HIS.toast('"' + raw + '" not found in Drug Master — pick via F3 or add it in Drug Master'); return; }
      const b = batches.find(x => (x.qtyOnHand || 0) > 0) || batches[0];   // FEFO: first in-stock batch
      if (i[1] && !i[1].value) i[1].value = b.batchNo || '';
      if (i[2] && !i[2].value) i[2].value = b.expiry || '';
      if (i[4] && !i[4].value && b.mrp != null) i[4].value = b.mrp;
      recalcDispRow(tr, doc);
      HIS.toast('Batch ' + (b.batchNo || '') + ' (exp ' + (b.expiry || '') + ', ' + (b.qtyOnHand || 0) + ' in stock) — enter qty', 'bi-box-seam');
    } catch (e) {}
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
    } catch (e) {
      const m = e.message || '';
      if (/Unknown drug/i.test(m)) HIS.toast('That drug is not in the Drug Master — pick a drug via F3, or add it in Drug Master first', 'bi-exclamation-triangle');
      else if (/batch|stock|Insufficient/i.test(m)) HIS.toast('Batch/stock issue: ' + m + ' — pick a drug via F3 so the batch auto-fills', 'bi-exclamation-triangle');
      else HIS.toast('Dispense failed: ' + m);
    }
  }

  /* ---- Phase 2.3: admit patient (POST /api/ipd/admit) ----------------- */
  // Resolve the F3-picked patient and show them in the IPD banner.
  async function showIpdPatient(doc) {
    const raw = val(doc, 'ipdPatient'); const b = doc.querySelector('#ipdBanner'); if (!b) return;
    if (!raw) { b.innerHTML = banner(null); return; }
    let i = raw.indexOf('—'); if (i < 0) i = raw.indexOf(' - ');
    const uhid = (i > 0 ? raw.slice(0, i) : raw).trim();
    try { const p = await HIS.api.patientByUhid(uhid); b.innerHTML = banner(p); }
    catch (e) { /* leave as-is */ }
  }
  async function doAdmit(doc) {
    // Require an explicitly-picked patient (no silent default) so nobody is admitted by accident.
    const uhid = val(doc, 'ipdPatient');
    if (!uhid) { HIS.toast('Select a patient (F3) to admit — none is selected'); return; }
    const bed = val(doc, 'ipdBed');
    if (!bed) { HIS.toast('Select a ward/bed (F3)'); return; }
    const cmd = {
      patientUhid: uhid,
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
      HIS.toast('Admitted · ' + r.admissionNo + ' · Bed ' + r.bedNo + ' — added to the list', 'bi-hospital');
      const pf = doc.querySelector('#ipdPatient'); if (pf) pf.value = '';
      const bf = doc.querySelector('#ipdBed'); if (bf) bf.value = '';
      const bn = doc.querySelector('#ipdBanner'); if (bn) bn.innerHTML = banner(null);
      loadBedBoard(doc); loadAdmissions(doc);
    } catch (e) { HIS.toast('Admit failed: ' + e.message); }
  }
  // Who is admitted in which bed/room — tenant/branch-scoped.
  async function loadAdmissions(doc) {
    const tb = doc.querySelector('#ipdAdmitted'); if (!tb) return;
    try {
      const rows = await HIS.api.admittedPatients();
      const cnt = doc.querySelector('#ipdAdmCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' admitted' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const when = (r.admittedUtc || '').replace('T', ' ').slice(0, 16);
        const act = `<button class="btn btn--sm" data-discharge="${r.admissionId}" data-patient="${r.patient}" data-bed="${r.bedNo || ''}"><i class="bi bi-box-arrow-right"></i> Discharge</button>`;
        return `<tr><td><b>${r.admissionNo}</b></td><td>${r.patient}</td><td>${r.uhid}</td><td>${r.ward || ''}</td>`
          + `<td><span class="pill pill--warn">${r.bedNo || ''}</span></td><td>${r.consultant || '—'}</td><td>${when}</td><td>${act}</td></tr>`;
      }).join('') : emptyRow(8, 'No patients currently admitted');
      tb.querySelectorAll('[data-discharge]').forEach(b => b.addEventListener('click', () => doDischarge(doc, b.dataset)));
    } catch (e) { tb.innerHTML = emptyRow(8, 'Admissions API unavailable'); }
  }
  async function doDischarge(doc, ds) {
    const win = doc.defaultView || window;
    if (!win.confirm('Discharge ' + ds.patient + (ds.bed ? ' from bed ' + ds.bed : '') + '?')) return;
    const summary = win.prompt('Discharge summary (optional):', 'Recovered, advised rest. Follow-up in 1 week.');
    try {
      await HIS.api.dischargePatient({ admissionId: parseInt(ds.discharge, 10), dischargeSummary: summary || null });
      HIS.toast('Discharged ' + ds.patient + (ds.bed ? ' · bed ' + ds.bed + ' now cleaning — Mark ready to free it' : ''), 'bi-box-arrow-right');
      loadBedBoard(doc); loadAdmissions(doc);
    } catch (e) { HIS.toast('Discharge failed: ' + e.message); }
  }

  /* ---- Vitals Station: dedicated desk that records vitals for booked patients ---- */
  function initVitals(doc) {
    vitalsLiveDoc = doc; ensureQueueHub();   // live: new bookings / vitals-done refresh the worklist
    loadDoctorDirectory().then(() => { const d = doc.querySelector('#vstDoctor'); if (d) fillDoctorSelect(d, ''); });
    const dsel = doc.querySelector('#vstDoctor'); if (dsel) dsel.addEventListener('change', () => loadVitalsWorklist(doc));
    const s = doc.querySelector('#vstSave'); if (s) s.addEventListener('click', () => doSaveVitalsStation(doc));
    const c = doc.querySelector('#vstCancel'); if (c) c.addEventListener('click', () => { doc.querySelector('#vstStation').hidden = true; });
    loadVitalsWorklist(doc);
  }
  async function loadVitalsWorklist(doc) {
    const tb = doc.querySelector('#vstQueue'); if (!tb) return;
    try {
      const rows = await HIS.api.apptQueue(val(doc, 'vstDoctor') || null);
      const work = rows.filter(r => r.status === 'Booked' || r.status === 'VitalsDone');
      tb.innerHTML = work.length ? work.map(r => {
        const cls = r.status === 'VitalsDone' ? 'pill--ok' : '';
        const act = `<button class="btn btn--sm" data-vst="${r.appointmentId}" data-token="${r.token}" data-patient="${r.patient}"><i class="bi bi-heart-pulse"></i> ${r.hasVitals ? 'Edit Vitals' : 'Take Vitals'}</button>`;
        return `<tr><td><b>${r.token}</b></td><td>${r.patient}</td><td>${r.uhid}</td><td>${r.doctor}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(6, 'No patients waiting for vitals');
      tb.querySelectorAll('[data-vst]').forEach(b => b.addEventListener('click', () => openVst(doc, b.dataset)));
    } catch (e) { tb.innerHTML = emptyRow(6, 'Worklist API unavailable'); }
  }
  function openVst(doc, ds) {
    doc.dataset.vstAppt = ds.vst;
    const who = doc.querySelector('#vstWho'); if (who) who.textContent = `Token ${ds.token} · ${ds.patient}`;
    const p = doc.querySelector('#vstStation'); if (p) { p.hidden = false; p.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
  }
  async function doSaveVitalsStation(doc) {
    const apptId = doc.dataset.vstAppt; if (!apptId) { HIS.toast('Pick a patient from the worklist first'); return; }
    const bp = (val(doc, 'vstBp') || '').split('/');
    const vitals = {
      tempF: numOrNull(val(doc, 'vstTemp')), pulse: intOrNull(val(doc, 'vstPulse')),
      bpSystolic: intOrNull(bp[0]), bpDiastolic: intOrNull(bp[1]),
      spo2: intOrNull(val(doc, 'vstSpo2')), respRate: intOrNull(val(doc, 'vstResp')),
      weightKg: numOrNull(val(doc, 'vstWeight')), heightCm: null, grbs: null
    };
    try {
      await HIS.api.recordVitals(apptId, vitals);
      HIS.toast('Vitals recorded — patient sent to the doctor lobby', 'bi-heart-pulse');
      doc.querySelector('#vstStation').hidden = true;
      ['vstTemp', 'vstPulse', 'vstBp', 'vstSpo2', 'vstResp', 'vstWeight'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      delete doc.dataset.vstAppt;
      loadVitalsWorklist(doc);
    } catch (e) { HIS.toast('Save vitals failed: ' + e.message); }
  }

  /* ---- ICU & Emergency Trauma (SRS §3.5/§3.6) ------------------------- */
  const TRIAGE_COLOUR = { 1: '#e5484d', 2: '#f5820e', 3: '#f2c94c', 4: '#37a35e', 5: '#3b82f6' };
  function initEmergency(doc) {
    loadTriageBoard(doc);
    const b = doc.querySelector('#erDisposeBtn'); if (b) b.addEventListener('click', () => doDispose(doc));
  }
  async function loadTriageBoard(doc) {
    const tb = doc.querySelector('#erBoard'); if (!tb) return;
    try {
      const rows = await HIS.api.triageBoard();
      tb.innerHTML = rows.length ? rows.map(r => {
        const lvl = r.triageLevel || '';
        const dot = `<span class="sw" style="background:${TRIAGE_COLOUR[r.triageLevel] || '#999'};display:inline-block;width:12px;height:12px;border-radius:50%;margin-right:6px"></span>`;
        const when = (r.arrivedUtc || '').replace('T', ' ').slice(11, 16);
        const done = r.status !== 'Waiting' && r.status !== 'InTreatment';
        const act = done ? '' : `<button class="btn btn--sm" data-dispo="${r.triageId}" data-uhid="${r.uhid || ''}" data-patient="${r.patient || 'Unidentified'}"><i class="bi bi-box-arrow-right"></i> Dispose</button>`;
        return `<tr><td>${dot}<b>${lvl ? 'L' + lvl : ''}</b> ${r.category}</td><td>${r.patient || '<i>Unidentified</i>'}</td><td>${r.chiefComplaint || ''}</td>`
          + `<td>${when}</td><td>${r.arrivalMode || ''}</td><td>${r.isMlc ? '<span class="pill pill--warn">MLC</span>' : ''}</td>`
          + `<td><span class="pill ${done ? 'pill--muted' : 'pill--ok'}">${r.status}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(8, 'No arrivals today');
      tb.querySelectorAll('[data-dispo]').forEach(b => b.addEventListener('click', () => {
        doc.dataset.erTriage = b.dataset.dispo; doc.dataset.erUhid = b.dataset.uhid;
        const who = doc.querySelector('#erDispWho'); if (who) who.textContent = b.dataset.patient + (b.dataset.uhid ? ' · ' + b.dataset.uhid : '');
        tb.querySelectorAll('tr').forEach(tr => { tr.style.background = ''; });
        const row = b.closest('tr'); if (row) row.style.background = '#eef6ff';
        // Bring the Disposition panel into view so the next step is obvious.
        const db = doc.querySelector('#erDisposeBtn'); if (db && db.closest('.panel')) db.closest('.panel').scrollIntoView({ behavior: 'smooth', block: 'center' });
        HIS.toast('Selected ' + b.dataset.patient + ' → set Disposition below, then Confirm', 'bi-box-arrow-right');
      }));
    } catch (e) { tb.innerHTML = emptyRow(8, 'Triage board API unavailable'); }
  }
  async function doRegisterTriage(doc) {
    const bp = (val(doc, 'erBp') || '').split('/');
    const cmd = {
      patientUhid: val(doc, 'erPatient') || null,
      category: val(doc, 'erColour') || 'Yellow',
      isMlc: !!(doc.querySelector('#erMlc') && doc.querySelector('#erMlc').checked),
      chiefComplaint: val(doc, 'erComplaint') || null,
      arrivalMode: val(doc, 'erMode') || null,
      attendingDoctorCode: val(doc, 'erDoctor') || null,
      painScore: intOrNull(val(doc, 'erPain')), gcsTotal: intOrNull(val(doc, 'erGcs')),
      tempF: numOrNull(val(doc, 'erTemp')), pulse: intOrNull(val(doc, 'erPulse')),
      bpSystolic: intOrNull(bp[0]), bpDiastolic: intOrNull(bp[1]),
      spo2: intOrNull(val(doc, 'erSpo2')), respRate: intOrNull(val(doc, 'erResp')), grbs: intOrNull(val(doc, 'erGrbs'))
    };
    try {
      const r = await HIS.api.registerTriage(cmd);
      HIS.toast('Triaged · ' + r.category + ' (L' + (r.triageLevel || '?') + ') · #' + r.triageId, 'bi-clipboard-check');
      ['erComplaint', 'erTemp', 'erPulse', 'erBp', 'erSpo2', 'erResp', 'erGrbs', 'erGcs', 'erPain', 'erPatient', 'erDoctor'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      const mlc = doc.querySelector('#erMlc'); if (mlc) mlc.checked = false;
      loadTriageBoard(doc);
    } catch (e) { HIS.toast('Triage failed: ' + e.message); }
  }
  async function doDispose(doc) {
    const triageId = doc.dataset.erTriage;
    if (!triageId) { HIS.toast('Click "Dispose" on a triage row first to select a patient'); return; }
    const disp = val(doc, 'erDisp');
    const admits = disp === 'AdmitICU' || disp === 'AdmitWard';
    if (admits && !val(doc, 'erBed')) { HIS.toast('Pick a free bed (F3) for ' + disp); return; }
    if (admits && !doc.dataset.erUhid) { HIS.toast('Unidentified patient — register them first, or choose Discharge/Refer'); return; }
    const cmd = { triageId: parseInt(triageId, 10), disposition: disp,
      patientUhid: doc.dataset.erUhid || null, bedLabel: val(doc, 'erBed') || null, consultantCode: val(doc, 'erDispDoctor') || null };
    try {
      const r = await HIS.api.disposeTriage(cmd);
      HIS.toast('Disposed · ' + disp + (r.admissionNo ? ' · ' + r.admissionNo + ' (bed ' + r.bedNo + ')' : ''), 'bi-check2-circle');
      delete doc.dataset.erTriage; delete doc.dataset.erUhid;
      const who = doc.querySelector('#erDispWho'); if (who) who.textContent = 'select a board row';
      const eb = doc.querySelector('#erBed'); if (eb) eb.value = '';
      loadTriageBoard(doc);
    } catch (e) { HIS.toast('Disposition failed: ' + e.message); }
  }

  /* ---- Operation Theatre: schedule -> start -> complete (SRS §3.12) ---- */
  function initOt(doc) {
    loadOtBoard(doc);
    const w = doc.querySelector('#otWhen'); if (w) w.value = new Date(Date.now() + 3600000).toISOString().slice(0, 16);
    const b = doc.querySelector('#otCompleteBtn'); if (b) b.addEventListener('click', () => doCompleteSurgery(doc));
  }
  async function loadOtBoard(doc) {
    const tb = doc.querySelector('#otBoard'); if (!tb) return;
    try {
      const rows = await HIS.api.otBoard();
      const cnt = doc.querySelector('#otCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' cases' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const when = (r.scheduledUtc || '').replace('T', ' ').slice(0, 16);
        const cls = r.status === 'Completed' ? 'pill--muted' : r.status === 'InProgress' ? 'pill--warn' : 'pill--ok';
        const label = r.status === 'InProgress' ? 'In progress' : r.status;
        let act = '';
        if (r.status === 'Scheduled') act = `<button class="btn btn--sm btn--primary" data-otstart="${r.otId}" data-patient="${r.patient}"><i class="bi bi-play-fill"></i> Start</button>`;
        else if (r.status === 'InProgress') act = `<button class="btn btn--sm" data-otcomplete="${r.otId}" data-patient="${r.patient}"><i class="bi bi-check2-circle"></i> Complete</button>`;
        return `<tr><td>${when}</td><td>${r.patient}</td><td>${r.procedure || ''}</td><td>${r.surgeon || '—'}</td><td>${r.theatre || ''}</td><td><span class="pill ${cls}">${label}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(7, 'No cases scheduled');
      tb.querySelectorAll('[data-otstart]').forEach(b => b.addEventListener('click', () => doStartSurgery(doc, b.dataset.otstart, b.dataset.patient)));
      tb.querySelectorAll('[data-otcomplete]').forEach(b => b.addEventListener('click', () => {
        doc.dataset.otId = b.dataset.otcomplete;
        const who = doc.querySelector('#otPostWho'); if (who) who.textContent = b.dataset.patient;
        const cb = doc.querySelector('#otCompleteBtn'); if (cb && cb.closest('.panel')) cb.closest('.panel').scrollIntoView({ behavior: 'smooth', block: 'center' });
        HIS.toast('Selected ' + b.dataset.patient + ' — add post-op notes, then Complete', 'bi-clipboard2-check');
      }));
    } catch (e) { tb.innerHTML = emptyRow(7, 'OT board API unavailable'); }
  }
  async function doScheduleSurgery(doc) {
    const uhid = val(doc, 'otPatient');
    if (!uhid) { HIS.toast('Select a patient (F3) to schedule'); return; }
    const when = val(doc, 'otWhen');
    if (!when) { HIS.toast('Pick a date & time'); return; }
    const cmd = { patientUhid: uhid, surgeonCode: val(doc, 'otSurgeon') || null, theatre: val(doc, 'otTheatre') || null,
      scheduledUtc: new Date(when).toISOString(), procedure: val(doc, 'otProcedure') || null };
    try {
      const r = await HIS.api.scheduleSurgery(cmd);
      HIS.toast('Surgery scheduled · OT #' + r.otId, 'bi-calendar-plus');
      ['otPatient', 'otSurgeon', 'otProcedure'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      loadOtBoard(doc);
    } catch (e) { HIS.toast('Schedule failed: ' + e.message); }
  }
  async function doStartSurgery(doc, otId, patient) {
    try {
      await HIS.api.startSurgery(parseInt(otId, 10));
      HIS.toast('Surgery started · ' + patient + ' (In progress)', 'bi-play-fill');
      loadOtBoard(doc);
    } catch (e) { HIS.toast('Start failed: ' + e.message); }
  }
  async function doCompleteSurgery(doc) {
    const otId = doc.dataset.otId;
    if (!otId) { HIS.toast('Pick an in-progress case (Complete button) first'); return; }
    try {
      await HIS.api.completeSurgery(parseInt(otId, 10), val(doc, 'otPostNotes') || null);
      HIS.toast('Surgery completed · post-op saved', 'bi-check2-circle');
      delete doc.dataset.otId;
      const el = doc.querySelector('#otPostNotes'); if (el) el.value = '';
      const who = doc.querySelector('#otPostWho'); if (who) who.textContent = 'select a case';
      loadOtBoard(doc);
    } catch (e) { HIS.toast('Complete failed: ' + e.message); }
  }

  /* ---- Nursing & Patient Care: notes against an admission (SRS §3.13) ---- */
  function initNursing(doc) {
    loadNrCensus(doc);
    const b = doc.querySelector('#nrSaveBtn'); if (b) b.addEventListener('click', () => doAddNursingNote(doc));
  }
  async function loadNrCensus(doc) {
    const tb = doc.querySelector('#nrCensus'); if (!tb) return;
    try {
      const rows = await HIS.api.admittedPatients();
      const cnt = doc.querySelector('#nrCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' admitted' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const active = doc.dataset.nrAdm === String(r.admissionId);
        return `<tr${active ? ' style="background:#eef6ff"' : ''}><td>${r.patient}</td><td>${r.uhid}</td><td>${r.ward || ''}</td><td><span class="pill pill--warn">${r.bedNo || ''}</span></td><td>${r.consultant || '—'}</td>`
          + `<td><button class="btn btn--sm ${active ? '' : 'btn--primary'}" data-nr="${r.admissionId}" data-patient="${r.patient}" data-uhid="${r.uhid}" data-bed="${r.ward} ${r.bedNo}"><i class="bi bi-clipboard2-heart"></i> ${active ? 'Selected' : 'Care'}</button></td></tr>`;
      }).join('') : emptyRow(6, 'No patients currently admitted');
      tb.querySelectorAll('[data-nr]').forEach(b => b.addEventListener('click', () => selectNrPatient(doc, b.dataset)));
      if (rows.length && !doc.dataset.nrAdm) {
        const r0 = rows[0];
        selectNrPatient(doc, { nr: String(r0.admissionId), patient: r0.patient, uhid: r0.uhid, bed: r0.ward + ' ' + r0.bedNo });
      }
    } catch (e) { tb.innerHTML = emptyRow(6, 'Census API unavailable'); }
  }
  function selectNrPatient(doc, ds) {
    doc.dataset.nrAdm = ds.nr;
    const b = doc.querySelector('#nrBanner');
    if (b) b.innerHTML = `<div class="pbanner selectable"><div class="av">${initials(ds.patient)}</div><div><div class="nm">${ds.patient}</div><div class="meta"><span>UHID <b>${ds.uhid}</b></span><span>Bed <b>${ds.bed}</b></span></div></div></div>`;
    loadNrNotes(doc, ds.nr);
  }
  async function loadNrNotes(doc, admId) {
    const tb = doc.querySelector('#nrNotes'); if (!tb) return;
    try {
      const rows = await HIS.api.nursingNotes(admId);
      tb.innerHTML = rows.length ? rows.map(n => {
        const t = (n.recordedUtc || '').replace('T', ' ').slice(0, 16);
        return `<tr><td>${t}</td><td><span class="pill pill--info">${n.noteType || ''}</span></td><td>${n.note || ''}</td></tr>`;
      }).join('') : emptyRow(3, 'No notes recorded yet');
    } catch (e) { tb.innerHTML = emptyRow(3, 'Notes API unavailable'); }
  }
  async function doAddNursingNote(doc) {
    const admId = doc.dataset.nrAdm;
    if (!admId) { HIS.toast('Pick an admitted patient from the census first'); return; }
    try {
      await HIS.api.addNursingNote({ admissionId: parseInt(admId, 10), noteType: val(doc, 'nrType'), note: val(doc, 'nrNote') || null });
      HIS.toast('Nursing note saved', 'bi-check2-circle');
      const el = doc.querySelector('#nrNote'); if (el) el.value = '';
      loadNrNotes(doc, admId);
    } catch (e) { HIS.toast('Save failed: ' + e.message); }
  }

  /* ---- Radiology & Imaging: order -> report (SRS §3.9) ---- */
  function initRadiology(doc) {
    loadRadWorklist(doc);
    const b = doc.querySelector('#radReportBtn'); if (b) b.addEventListener('click', () => doReportRadiology(doc));
  }
  async function loadRadWorklist(doc) {
    const tb = doc.querySelector('#radWorklist'); if (!tb) return;
    try {
      const rows = await HIS.api.radWorklist();
      const cnt = doc.querySelector('#radCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' orders' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const cls = r.status === 'Reported' ? 'pill--ok' : 'pill--warn';
        const act = r.status !== 'Reported' ? `<button class="btn btn--sm" data-radrep="${r.radOrderId}" data-patient="${r.patient}" data-study="${r.modality}${r.study ? ' ' + r.study : ''}"><i class="bi bi-file-earmark-medical"></i> Report</button>` : '';
        return `<tr><td>#${r.radOrderId}</td><td>${r.patient}</td><td>${r.modality}</td><td>${r.study || ''}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(6, 'No radiology orders');
      tb.querySelectorAll('[data-radrep]').forEach(b => b.addEventListener('click', () => {
        doc.dataset.radOrder = b.dataset.radrep;
        const who = doc.querySelector('#radRepWho'); if (who) who.textContent = '#' + b.dataset.radrep + ' · ' + b.dataset.study + ' · ' + b.dataset.patient;
        const rb = doc.querySelector('#radReportBtn'); if (rb && rb.closest('.panel')) rb.closest('.panel').scrollIntoView({ behavior: 'smooth', block: 'center' });
        HIS.toast('Selected order #' + b.dataset.radrep + ' — enter report, then File', 'bi-file-earmark-medical');
      }));
    } catch (e) { tb.innerHTML = emptyRow(6, 'Worklist API unavailable'); }
  }
  async function doOrderStudy(doc) {
    const uhid = val(doc, 'radPatient');
    if (!uhid) { HIS.toast('Select a patient (F3) to order'); return; }
    const cmd = { patientUhid: uhid, modality: val(doc, 'radModality'), studyName: val(doc, 'radStudy') || null,
      isPcPndtRegulated: !!(doc.querySelector('#radPcpndt') && doc.querySelector('#radPcpndt').checked) };
    try {
      const id = await HIS.api.createRadOrder(cmd);
      HIS.toast('Study ordered · #' + id, 'bi-radioactive');
      ['radPatient', 'radStudy'].forEach(x => { const el = doc.querySelector('#' + x); if (el) el.value = ''; });
      const pc = doc.querySelector('#radPcpndt'); if (pc) pc.checked = false;
      loadRadWorklist(doc);
    } catch (e) { HIS.toast('Order failed: ' + e.message); }
  }
  async function doReportRadiology(doc) {
    const oid = doc.dataset.radOrder;
    if (!oid) { HIS.toast('Pick an order (Report button) from the worklist first'); return; }
    try {
      await HIS.api.reportRadiology(parseInt(oid, 10), val(doc, 'radReport') || null);
      HIS.toast('Report filed · order #' + oid + ' Reported', 'bi-check2-circle');
      delete doc.dataset.radOrder;
      const el = doc.querySelector('#radReport'); if (el) el.value = '';
      const who = doc.querySelector('#radRepWho'); if (who) who.textContent = 'select an order';
      loadRadWorklist(doc);
    } catch (e) { HIS.toast('Report failed: ' + e.message); }
  }

  /* ---- Certificates & Documents: issue -> approve (SRS §3.16) ---- */
  function initCertificates(doc) {
    loadCertificates(doc);
    loadCertTemplates(doc);
    const b = doc.querySelector('#certApproveBtn'); if (b) b.addEventListener('click', () => doApproveCertificate(doc));
  }
  async function loadCertTemplates(doc) {
    const sel = doc.querySelector('#certTemplate'); if (!sel) return;
    try {
      const t = await HIS.api.certTemplates();
      sel.innerHTML = '<option value="">— select —</option>' + t.map(x => `<option value="${x.templateId}">${x.certType} · ${x.title}</option>`).join('');
    } catch (e) {}
  }
  async function loadCertificates(doc) {
    const tb = doc.querySelector('#certList'); if (!tb) return;
    try {
      const rows = await HIS.api.certificates();
      const cnt = doc.querySelector('#certCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' certificates' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const cls = r.status === 'Approved' ? 'pill--ok' : 'pill--warn';
        const act = r.status !== 'Approved' ? `<button class="btn btn--sm" data-certap="${r.certId}" data-type="${r.certType}" data-patient="${r.patient}"><i class="bi bi-patch-check"></i> Approve</button>` : '';
        return `<tr><td>#${r.certId}</td><td>${r.certType}</td><td>${r.patient}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${act}</td></tr>`;
      }).join('') : emptyRow(5, 'No certificates issued');
      tb.querySelectorAll('[data-certap]').forEach(b => b.addEventListener('click', () => {
        doc.dataset.certId = b.dataset.certap;
        const who = doc.querySelector('#certApWho'); if (who) who.textContent = '#' + b.dataset.certap + ' · ' + b.dataset.type + ' · ' + b.dataset.patient;
        const ab = doc.querySelector('#certApproveBtn'); if (ab && ab.closest('.panel')) ab.closest('.panel').scrollIntoView({ behavior: 'smooth', block: 'center' });
        HIS.toast('Selected cert #' + b.dataset.certap + ' — pick a doctor, then Approve', 'bi-patch-check');
      }));
    } catch (e) { tb.innerHTML = emptyRow(5, 'Certificates API unavailable'); }
  }
  async function doIssueCertificate(doc) {
    const tid = val(doc, 'certTemplate');
    if (!tid) { HIS.toast('Select a certificate type'); return; }
    const uhid = val(doc, 'certPatient');
    if (!uhid) { HIS.toast('Select a patient (F3)'); return; }
    try {
      const id = await HIS.api.issueCertificate({ templateId: parseInt(tid, 10), patientUhid: uhid });
      HIS.toast('Certificate issued · #' + id, 'bi-file-earmark-plus');
      const el = doc.querySelector('#certPatient'); if (el) el.value = '';
      loadCertificates(doc);
    } catch (e) { HIS.toast('Issue failed: ' + e.message); }
  }
  async function doApproveCertificate(doc) {
    const cid = doc.dataset.certId;
    if (!cid) { HIS.toast('Pick a certificate (Approve button) from the list first'); return; }
    const docCode = val(doc, 'certDoctor');
    if (!docCode) { HIS.toast('Pick the approving doctor (F3)'); return; }
    try {
      await HIS.api.approveCertificate(parseInt(cid, 10), docCode);
      HIS.toast('Certificate #' + cid + ' approved', 'bi-check2-circle');
      delete doc.dataset.certId;
      const who = doc.querySelector('#certApWho'); if (who) who.textContent = 'select a certificate';
      const cd = doc.querySelector('#certDoctor'); if (cd) cd.value = '';
      loadCertificates(doc);
    } catch (e) { HIS.toast('Approve failed: ' + e.message); }
  }

  /* ---- Drug Master: CRUD over master.Drug ---- */
  function initDrugMaster(doc) {
    loadDrugMaster(doc);
    const r = doc.querySelector('#dmReset'); if (r) r.addEventListener('click', () => resetDrugForm(doc));
  }
  async function loadDrugMaster(doc) {
    const tb = doc.querySelector('#dmList'); if (!tb) return;
    try {
      const rows = await HIS.api.drugMaster();
      const cnt = doc.querySelector('#dmCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' drugs' : '';
      tb.innerHTML = rows.length ? rows.map(d => {
        const st = d.isActive ? '<span class="pill pill--ok">Active</span>' : '<span class="pill pill--muted">Inactive</span>';
        const nm = (d.name || '').replace(/"/g, '&quot;');
        const toggle = d.isActive ? `<button class="btn btn--sm" data-dmoff="${d.drugId}">Deactivate</button>` : `<button class="btn btn--sm" data-dmon="${d.drugId}">Restore</button>`;
        return `<tr><td><b>${d.code}</b></td><td>${d.name}</td><td>${d.form}</td><td class="num">${d.stockQty}</td><td class="num">${d.reorderLevel}</td><td>${st}</td>`
          + `<td class="flex gap6"><button class="btn btn--sm" data-dmedit="${d.drugId}" data-code="${d.code}" data-name="${nm}" data-form="${d.form}" data-reorder="${d.reorderLevel}"><i class="bi bi-pencil"></i> Edit</button>${toggle}</td></tr>`;
      }).join('') : emptyRow(7, 'No drugs — add one below');
      tb.querySelectorAll('[data-dmedit]').forEach(b => b.addEventListener('click', () => editDrug(doc, b.dataset)));
      tb.querySelectorAll('[data-dmoff]').forEach(b => b.addEventListener('click', () => toggleDrug(doc, b.dataset.dmoff, false)));
      tb.querySelectorAll('[data-dmon]').forEach(b => b.addEventListener('click', () => toggleDrug(doc, b.dataset.dmon, true)));
    } catch (e) { tb.innerHTML = emptyRow(7, 'Drug master API unavailable'); }
  }
  function editDrug(doc, ds) {
    doc.dataset.dmEditId = ds.dmedit;
    const set = (id, v) => { const el = doc.querySelector('#' + id); if (el) el.value = v; };
    set('dmCode', ds.code); set('dmName', ds.name); set('dmForm', ds.form); set('dmReorder', ds.reorder);
    const code = doc.querySelector('#dmCode'); if (code) code.disabled = true;   // code immutable on edit
    const t = doc.querySelector('#dmFormTitle'); if (t) t.textContent = 'Edit Drug · ' + ds.code;
    const nm = doc.querySelector('#dmName'); if (nm) nm.focus();
  }
  function resetDrugForm(doc) {
    delete doc.dataset.dmEditId;
    ['dmCode', 'dmName', 'dmReorder'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
    const code = doc.querySelector('#dmCode'); if (code) code.disabled = false;
    const t = doc.querySelector('#dmFormTitle'); if (t) t.textContent = 'Add Drug';
  }
  async function doSaveDrug(doc) {
    const code = val(doc, 'dmCode'), name = val(doc, 'dmName'), form = val(doc, 'dmForm');
    if (!code || !name) { HIS.toast('Code and Name are required'); return; }
    const cmd = { drugId: doc.dataset.dmEditId ? parseInt(doc.dataset.dmEditId, 10) : null, code, name, form, reorderLevel: intOrNull(val(doc, 'dmReorder')) || 0 };
    try {
      await HIS.api.saveDrug(cmd);
      HIS.toast(doc.dataset.dmEditId ? 'Drug updated · ' + code : 'Drug added · ' + code, 'bi-capsule');
      resetDrugForm(doc); loadDrugMaster(doc);
    } catch (e) { HIS.toast('Save failed: ' + e.message); }
  }
  async function toggleDrug(doc, id, active) {
    try { await HIS.api.setDrugActive(parseInt(id, 10), active); HIS.toast(active ? 'Drug restored' : 'Drug deactivated'); loadDrugMaster(doc); }
    catch (e) { HIS.toast('Failed: ' + e.message); }
  }

  /* ---- Inventory & Store: stock levels + purchase orders (SRS §3.11) ---- */
  function initInventory(doc) {
    loadInvStock(doc);
    loadPoSuppliers(doc);
    loadPurchaseOrders(doc);
  }
  async function loadPurchaseOrders(doc) {
    const tb = doc.querySelector('#poList'); if (!tb) return;
    try {
      const rows = await HIS.api.purchaseOrders();
      const cnt = doc.querySelector('#poCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' orders' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const when = (r.createdUtc || '').replace('T', ' ').slice(0, 16);
        const cls = r.status === 'Received' ? 'pill--ok' : r.status === 'Cancelled' ? 'pill--muted' : 'pill--warn';
        return `<tr><td><b>${r.poNo}</b></td><td>${r.supplier || '—'}</td><td class="num">${r.lines}</td><td class="num">${(r.total || 0).toFixed(2)}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${when}</td></tr>`;
      }).join('') : emptyRow(6, 'No purchase orders yet');
    } catch (e) { tb.innerHTML = emptyRow(6, 'PO list API unavailable'); }
  }
  async function loadInvStock(doc) {
    const tb = doc.querySelector('#invStock'); if (!tb) return;
    try {
      const rows = await HIS.api.inventoryStock();
      const cnt = doc.querySelector('#invCount');
      if (cnt) { const low = rows.filter(r => r.belowReorder).length; cnt.textContent = rows.length + ' items' + (low ? ' · ' + low + ' below reorder' : ''); }
      tb.innerHTML = rows.length ? rows.map(r => {
        const st = r.belowReorder ? '<span class="pill pill--warn">Reorder</span>' : '<span class="pill pill--ok">OK</span>';
        return `<tr${r.belowReorder ? ' style="background:#fdf3f2"' : ''}><td><b>${r.code}</b></td><td>${r.name}</td><td class="num">${r.stock}</td><td class="num">${r.reorderLevel}</td><td>${st}</td></tr>`;
      }).join('') : emptyRow(5, 'No stock items');
    } catch (e) { tb.innerHTML = emptyRow(5, 'Stock API unavailable'); }
  }
  async function loadPoSuppliers(doc) {
    const sel = doc.querySelector('#poSupplier'); if (!sel) return;
    try {
      const s = await HIS.api.inventorySuppliers();
      sel.innerHTML = '<option value="">— select —</option>' + s.map(x => `<option value="${x.supplierId}">${x.name}${x.gstin ? ' · ' + x.gstin : ''}</option>`).join('');
    } catch (e) {}
  }
  async function doCreatePo(doc) {
    const sid = val(doc, 'poSupplier');
    if (!sid) { HIS.toast('Select a supplier'); return; }
    const lines = Array.from(doc.querySelectorAll('#poBody tr')).map(tr => {
      const i = tr.querySelectorAll('input');
      return { itemName: i[0] ? i[0].value.trim() : '', qty: i[1] ? intOrNull(i[1].value) : null, unitPrice: i[2] ? numOrNull(i[2].value) : null };
    }).filter(l => l.itemName && l.qty);
    if (!lines.length) { HIS.toast('Add at least one item + qty'); return; }
    try {
      const r = await HIS.api.createPurchaseOrder({ supplierId: parseInt(sid, 10), lines });
      HIS.toast('Purchase Order created · ' + r.poNo + ' — see the Purchase Orders list', 'bi-cart-check');
      // reset the line grid to a single empty row
      const body = doc.querySelector('#poBody'); if (body) body.innerHTML = '<tr>' + (body.dataset.tpl || TPL.poBody) + '</tr>';
      if (HIS.wireScreenFragment) HIS.wireScreenFragment(doc.querySelector('#poGrid'));
      loadPurchaseOrders(doc); loadInvStock(doc);
    } catch (e) { HIS.toast('Create PO failed: ' + e.message); }
  }

  /* ---- Blood Bank: stock + raise request (SRS §3.7) ---- */
  function initBloodBank(doc) {
    loadBloodStock(doc);
    loadBloodRequests(doc);
    const a = doc.querySelector('#bbAddBtn'); if (a) a.addEventListener('click', () => doAddBloodStock(doc));
  }
  async function doAddBloodStock(doc) {
    const group = val(doc, 'bbAddGroup'), units = intOrNull(val(doc, 'bbAddUnits'));
    if (!units || units < 1) { HIS.toast('Enter units to add (>= 1)'); return; }
    try {
      await HIS.api.addBloodStock({ bloodGroup: group, units });
      HIS.toast('Added ' + units + ' unit(s) of ' + group + ' to stock', 'bi-plus-circle');
      const u = doc.querySelector('#bbAddUnits'); if (u) u.value = '';
      loadBloodStock(doc);
    } catch (e) { HIS.toast('Add stock failed: ' + e.message); }
  }
  async function doIssueBlood(doc, id) {
    try {
      await HIS.api.issueBlood(parseInt(id, 10));
      HIS.toast('Blood issued · request #' + id + ' fulfilled — stock deducted', 'bi-droplet-half');
      loadBloodStock(doc); loadBloodRequests(doc);
    } catch (e) { HIS.toast('Issue failed: ' + e.message); }
  }
  async function loadBloodRequests(doc) {
    const tb = doc.querySelector('#bbReqList'); if (!tb) return;
    try {
      const rows = await HIS.api.bloodRequests();
      const cnt = doc.querySelector('#bbReqCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' requests' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const when = (r.requestedUtc || '').replace('T', ' ').slice(0, 16);
        const pr = r.isEmergency ? '<span class="pill pill--warn">Emergency</span>' : '<span class="pill pill--muted">Routine</span>';
        const cls = r.status === 'Fulfilled' ? 'pill--ok' : 'pill--warn';
        const act = r.status === 'Fulfilled' ? '' : `<button class="btn btn--sm btn--primary" data-issue="${r.requestId}"><i class="bi bi-droplet-half"></i> Issue</button>`;
        return `<tr><td><b>#${r.requestId}</b></td><td>${r.patient || '—'}</td><td><b>${r.bloodGroup}</b></td><td class="num">${r.units}</td><td>${pr}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${when}</td><td>${act}</td></tr>`;
      }).join('') : emptyRow(8, 'No blood requests yet');
      tb.querySelectorAll('[data-issue]').forEach(b => b.addEventListener('click', () => doIssueBlood(doc, b.dataset.issue)));
    } catch (e) { tb.innerHTML = emptyRow(8, 'Requests API unavailable'); }
  }
  async function loadBloodStock(doc) {
    const tb = doc.querySelector('#bbStock'); if (!tb) return;
    try {
      const rows = await HIS.api.bloodStock();
      const cnt = doc.querySelector('#bbCount');
      if (cnt) { const low = rows.filter(r => r.belowThreshold).length; cnt.textContent = low ? low + ' group(s) below safety' : 'all groups OK'; }
      tb.innerHTML = rows.length ? rows.map(r => {
        const st = r.belowThreshold ? '<span class="pill pill--warn">Low</span>' : '<span class="pill pill--ok">OK</span>';
        return `<tr${r.belowThreshold ? ' style="background:#fdf3f2"' : ''}><td><b>${r.bloodGroup}</b></td><td class="num">${r.units}</td><td class="num">${r.safetyThreshold}</td><td>${st}</td></tr>`;
      }).join('') : emptyRow(4, 'No stock data');
    } catch (e) { tb.innerHTML = emptyRow(4, 'Stock API unavailable'); }
  }
  async function doRaiseBloodRequest(doc) {
    const units = intOrNull(val(doc, 'bbUnits'));
    if (!units || units < 1) { HIS.toast('Enter units (>= 1)'); return; }
    const cmd = { patientUhid: val(doc, 'bbPatient') || null, bloodGroup: val(doc, 'bbGroup'), units,
      isEmergency: !!(doc.querySelector('#bbEmergency') && doc.querySelector('#bbEmergency').checked) };
    try {
      const r = await HIS.api.raiseBloodRequest(cmd);
      HIS.toast('Blood request raised · #' + r.requestId + (r.donorAlert ? ' · donor alert — short stock' : '') + ' — see the Blood Requests list', r.donorAlert ? 'bi-exclamation-triangle' : 'bi-droplet-half');
      ['bbUnits', 'bbPatient'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      const em = doc.querySelector('#bbEmergency'); if (em) em.checked = false;
      loadBloodStock(doc); loadBloodRequests(doc);
    } catch (e) { HIS.toast('Request failed: ' + e.message); }
  }

  function initIcu(doc) {
    loadIcuCensus(doc);
    const b = doc.querySelector('#icuSaveBtn'); if (b) b.addEventListener('click', () => doRecordObs(doc));
  }
  async function loadIcuCensus(doc) {
    const tb = doc.querySelector('#icuCensus'); if (!tb) return;
    try {
      const rows = await HIS.api.icuAdmissions();
      const cnt = doc.querySelector('#icuCount'); if (cnt) cnt.textContent = rows.length ? rows.length + ' in ICU/HDU' : '';
      tb.innerHTML = rows.length ? rows.map(r => {
        const active = doc.dataset.icuAdm === String(r.admissionId);
        return `<tr${active ? ' style="background:#eef6ff"' : ''}><td>${r.patient}</td><td>${r.uhid}</td><td>${r.ward}</td><td><span class="pill pill--warn">${r.bedNo}</span></td><td>${r.consultant || '—'}</td>`
        + `<td><button class="btn btn--sm ${active ? '' : 'btn--primary'}" data-mon="${r.admissionId}" data-patient="${r.patient}" data-uhid="${r.uhid}" data-bed="${r.ward} ${r.bedNo}"><i class="bi bi-activity"></i> ${active ? 'Monitoring' : 'Monitor'}</button></td></tr>`;
      }).join('') : emptyRow(6, 'No patients currently in ICU/HDU');
      tb.querySelectorAll('[data-mon]').forEach(b => b.addEventListener('click', () => selectIcuPatient(doc, b.dataset)));
      // Auto-select the first ICU patient so "Record Observation" is usable immediately.
      if (rows.length && !doc.dataset.icuAdm) {
        const r0 = rows[0];
        selectIcuPatient(doc, { mon: String(r0.admissionId), patient: r0.patient, uhid: r0.uhid, bed: r0.ward + ' ' + r0.bedNo });
      }
    } catch (e) { tb.innerHTML = emptyRow(6, 'ICU census API unavailable'); }
  }
  function selectIcuPatient(doc, ds) {
    doc.dataset.icuAdm = ds.mon;
    const b = doc.querySelector('#icuBanner');
    if (b) b.innerHTML = `<div class="pbanner selectable"><div class="av">${initials(ds.patient)}</div><div><div class="nm">${ds.patient}</div><div class="meta"><span>UHID <b>${ds.uhid}</b></span><span>Bed <b>${ds.bed}</b></span></div></div></div>`;
    loadIcuFlowsheet(doc, ds.mon);
    HIS.toast('Monitoring ' + ds.patient, 'bi-activity');
  }
  async function loadIcuFlowsheet(doc, admId) {
    const tb = doc.querySelector('#icuFlow'); if (!tb) return;
    try {
      const rows = await HIS.api.icuFlowsheet(admId);
      tb.innerHTML = rows.length ? rows.map(o => {
        const t = (o.recordedUtc || '').replace('T', ' ').slice(0, 16);
        const bp = (o.bpSystolic != null || o.bpDiastolic != null) ? `${o.bpSystolic ?? ''}/${o.bpDiastolic ?? ''}` : '—';
        return `<tr><td>${t}</td><td>${o.heartRate ?? '—'}</td><td>${bp}</td><td>${o.map ?? '—'}</td><td>${o.spo2 ?? '—'}</td><td>${o.respRate ?? '—'}</td><td>${o.tempF ?? '—'}</td><td>${o.gcsTotal ?? '—'}</td><td>${o.fio2 ?? '—'}</td><td>${o.urineOutputMl ?? '—'}</td><td>${o.ventMode || '—'}</td></tr>`;
      }).join('') : emptyRow(11, 'No observations yet');
    } catch (e) { tb.innerHTML = emptyRow(11, 'Flowsheet API unavailable'); }
  }
  async function doRecordObs(doc) {
    const admId = doc.dataset.icuAdm;
    if (!admId) { HIS.toast('Pick an ICU patient from the census first'); return; }
    const bp = (val(doc, 'icuBp') || '').split('/');
    const cmd = {
      heartRate: intOrNull(val(doc, 'icuHr')), bpSystolic: intOrNull(bp[0]), bpDiastolic: intOrNull(bp[1]),
      spo2: intOrNull(val(doc, 'icuSpo2')), respRate: intOrNull(val(doc, 'icuResp')), tempF: numOrNull(val(doc, 'icuTemp')),
      gcsTotal: intOrNull(val(doc, 'icuGcs')), fio2: intOrNull(val(doc, 'icuFio2')),
      urineOutputMl: intOrNull(val(doc, 'icuUrine')), ventMode: val(doc, 'icuVent') || null, notes: val(doc, 'icuNotes') || null
    };
    try {
      await HIS.api.recordIcuObs(parseInt(admId, 10), cmd);
      HIS.toast('Observation recorded', 'bi-check2-circle');
      ['icuHr', 'icuBp', 'icuSpo2', 'icuResp', 'icuTemp', 'icuGcs', 'icuFio2', 'icuUrine', 'icuNotes'].forEach(id => { const el = doc.querySelector('#' + id); if (el) el.value = ''; });
      loadIcuFlowsheet(doc, admId);
    } catch (e) { HIS.toast('Record failed: ' + e.message); }
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
      const lob = doc.querySelector('#opdLobbyDoctor'); if (lob) fillDoctorSelect(lob, '');
      if (dept) fillDeptSelect(dept);
      if (docSel) fillDoctorSelect(docSel, '');
      if (dept && docSel) dept.addEventListener('change', () => { fillDoctorSelect(docSel, dept.value); renderDeptTemplate(doc, dept.value); });
      if (docSel) docSel.addEventListener('change', () => { const d = deptOfDoctor(docSel.value); if (dept) dept.value = d; renderDeptTemplate(doc, d); });
    });
    const wb = doc.querySelector('#opdWalkinBtn'); if (wb) wb.addEventListener('click', () => startWalkIn(doc));
  }
  // Walk-in: consult a patient who has no appointment/token. Vitals are captured on
  // this form (no appointmentId), and the consultation saves against the picked patient.
  async function startWalkIn(doc) {
    const raw = val(doc, 'opdWalkin');
    if (!raw) { HIS.toast('Pick a walk-in patient (F3) first'); return; }
    // Lookups fill "UHID — Name"; split only on the em-dash (UHIDs contain hyphens).
    let i = raw.indexOf('—'); if (i < 0) i = raw.indexOf(' - ');
    const uhid = (i > 0 ? raw.slice(0, i) : raw).trim();
    try {
      const p = await HIS.api.patientByUhid(uhid);
      if (!p) { HIS.toast('Patient not found: ' + uhid); return; }
      clearConsultForm(doc);   // fresh form for this walk-in patient
      doc.dataset.opdUhid = p.uhid;
      delete doc.dataset.opdAppt;   // walk-in: no queued appointment/token to close
      const b = doc.querySelector('#opdBanner'); if (b) b.innerHTML = banner(p);
      loadOpdHistory(doc, p.uhid, p.name);
      HIS.toast('Walk-in consult started for ' + p.name + ' — select consultant, then Save', 'bi-person-plus');
    } catch (e) { HIS.toast('Could not load patient: ' + e.message); }
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
      renderOpdSchedule(doc, all, docCode);
    } catch (e) { tb.innerHTML = emptyRow(5, 'Lobby API unavailable'); }
  }
  // Doctor-facing full day: every appointment booked for this doctor today, any status.
  function renderOpdSchedule(doc, all, docCode) {
    const st = doc.querySelector('#opdSchedule'); if (!st) return;
    const cnt = doc.querySelector('#opdSchedCount');
    if (cnt) cnt.textContent = all.length ? all.length + ' booked today' : '';
    st.innerHTML = all.length ? all.map(r => {
      const cls = r.status === 'Completed' ? 'pill--muted' : r.status === 'InConsultation' ? 'pill--warn' : r.status === 'VitalsDone' ? 'pill--ok' : '';
      const vit = r.hasVitals ? '<span class="pill pill--ok">Done</span>' : '<span class="pill pill--muted">Pending</span>';
      const time = (r.slotStart || '').slice(11, 16) || '—';
      return `<tr><td><b>${time}</b></td><td>${r.token}</td><td>${r.patient}</td><td>${r.uhid}</td><td><span class="pill ${cls}">${r.status}</span></td><td>${vit}</td></tr>`;
    }).join('') : emptyRow(6, docCode ? 'No appointments today' : 'Enter your doctor code above');
  }
  async function doCallIn(doc, ds) {
    try {
      await HIS.api.callNext(ds.call);
      HIS.toast('Calling ' + ds.patient + ' · token ' + (ds.token || ''), 'bi-megaphone');
      await selectOpdPatient(doc, { consult: ds.call, uhid: ds.uhid, patient: ds.patient, token: ds.token, doctor: ds.doctor });
      loadOpdLobby(doc);
    } catch (e) { HIS.toast('Call failed: ' + e.message); }
  }
  // Reset every consultation field so each patient starts on a clean form (no stale/prev data).
  function clearConsultForm(doc) {
    ['opdComplaints', 'opdHistory', 'opdAdvice', 'opdDx1', 'opdDx2', 'opdFollowup',
     'opdTemp', 'opdPulse', 'opdBp', 'opdSpo2', 'opdResp', 'opdWeight'].forEach(id => {
      const el = doc.querySelector('#' + id); if (el) el.value = '';
    });
    doc.querySelectorAll('#rxBody input, #rxBody select').forEach(el => { if (el.type === 'checkbox') el.checked = false; else el.value = ''; });
    doc.querySelectorAll('[data-pane="ord"] input[type="checkbox"]').forEach(cb => { cb.checked = false; });
    doc.querySelectorAll('#deptTplFields input, #deptTplFields select').forEach(el => { if (el.type === 'checkbox') el.checked = false; else el.value = ''; });
    const fu = doc.querySelector('#opdFollowupResult'); if (fu) fu.style.display = 'none';
  }
  async function selectOpdPatient(doc, ds) {
    clearConsultForm(doc);   // fresh form for this patient
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
    loadOpdHistory(doc, ds.uhid, ds.patient);
    HIS.toast('Loaded ' + ds.patient + ' — vitals preloaded, proceed to diagnosis', 'bi-person-check');
  }
  // Selected patient's past consultations — shown below the OPD form; refreshed after each save.
  async function loadOpdHistory(doc, uhid, name) {
    const tb = doc.querySelector('#opdHistBody'); if (!tb) return;
    const who = doc.querySelector('#opdHistWho'); if (who) who.textContent = name ? (name + ' · ' + uhid) : (uhid || '');
    if (!uhid) { tb.innerHTML = emptyRow(5, 'Select a patient to see their history'); return; }
    tb.innerHTML = emptyRow(5, 'Loading…');
    try {
      const encs = await HIS.api.patientEncounters(uhid);
      tb.innerHTML = encs.length ? encs.map(e => {
        const dt = (e.dateUtc || '').replace('T', ' ').slice(0, 16);
        return `<tr><td>${dt}</td><td>${e.doctor || ''}</td><td>${e.department || ''}</td><td>${e.complaints || ''}</td><td>${e.diagnosis || '—'}</td></tr>`;
      }).join('') : emptyRow(5, 'No consultations recorded yet');
    } catch (e) { tb.innerHTML = emptyRow(5, 'History API unavailable'); }
  }

  /* ---- Phase 2: save OPD consultation (POST /api/encounters/consultation) */
  async function doSaveConsultation(doc) {
    const uhid = doc.dataset.opdUhid;
    if (!uhid) { HIS.toast('Call In a patient from the waiting lobby first — no patient is selected'); return; }
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
      loadOpdHistory(doc, uhid);   // show the just-saved consultation in the history table
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
