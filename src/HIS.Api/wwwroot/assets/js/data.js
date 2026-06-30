/* ============================================================================
   HIS ERP — runtime data layer (NO static business data)
   The module registry, F3 lookups, dashboard and the current patient all come
   from the Web API now. This file only holds runtime caches + a bootstrap
   loader. The hardcoded arrays that used to live here have been removed and
   moved into the database (see db/seed/0100_seed_reference.sql).
   ========================================================================== */
window.HIS = window.HIS || {};

/* Runtime caches — filled by HIS.bootstrap() from the API, not hardcoded. */
HIS.groups  = [];   // loaded from /api/meta/registry
HIS.modules = [];   // loaded from /api/meta/registry
HIS.lookups = {};   // per-type cache, filled on demand from /api/lookups/{type}
HIS.mock    = {};   // { currentPatient } loaded from /api/patients/default

/* SRS scope bullets shown on not-yet-built placeholder screens.
   These are descriptive scope text (documentation), not business data. */
HIS.srs = {
  queue:['Token-based queue management for OPD, pharmacy and billing','Real-time display boards and voice/visual calling','Counter load-balancing and wait-time analytics'],
  feedback:['Patient feedback and satisfaction surveys (NABH-aligned)','Grievance logging with category, SLA and resolution TAT','Escalation matrix and trend analytics'],
  icu:['Triage system for critical patients','Emergency admission workflow and ICU monitoring support','Emergency billing integration'],
  ot:['Surgery scheduling and OT resource allocation','Surgeon / staff assignment','Post-operative notes'],
  nursing:['Vital-signs monitoring and medication administration record','Shift handover notes','Patient diet and care plans'],
  telemedicine:['Secure video / audio teleconsultation (Telemedicine Practice Guidelines 2020)','Cross-branch specialist and second-opinion consultation','e-Prescription with digital signature','Patient consent capture and session audit log','Tele-ICU and tele-radiology support'],
  certificates:['Generate Birth, Death, Referral, Fitness, Medical certificates and Discharge Summary','Workflow: Patient Record → Doctor Approval → Certificate Print / PDF'],
  radiology:['X-Ray, MRI, CT scheduling and imaging report upload','Doctor review integration','PC-PNDT regulated record controls'],
  inventory:['Medicine and equipment stock monitoring with low-stock alerts','Purchase-order generation and supplier management','Branch stock transfer'],
  bloodbank:['Blood group-wise inventory tracking','Emergency blood request and donor alert system','Branch-to-branch blood transfer'],
  esic:['Insured Person (IP) number and Pehchan card verification','Dependent eligibility validation','Empanelled / tie-up hospital credit billing under ESI entitlement','Super-Specialty Treatment (SST) referral workflow','e-Bill submission and reimbursement / settlement tracking'],
  cghs:['CGHS beneficiary ID and card verification','CGHS-approved package rate master and credit billing','Referral / permission-letter capture and claim/bill submission'],
  echs:['ECHS card and referral verification for ex-servicemen and dependents','Empanelled-hospital billing via online Bill Processing Agency (BPA)','Referral, emergency and claim-submission handling'],
  statescheme:['Configurable scheme master for State Arogya / health-assurance schemes','Beneficiary verification, package rates and scheme-specific claim workflow','Multi-scheme handling with coordination-of-benefits and priority rules'],
  ambulance:['Emergency call logging and nearest-ambulance dispatch','Live GPS tracking and hospital arrival notification'],
  occhealth:['Pre-Employment (PEME) & Periodic (PME) Medical Examinations','Fitness-for-duty certification (fit / unfit / fit-with-conditions)','Occupational disease & hazard-exposure tracking (noise, dust, chemical, vision)','Audiometry, spirometry, vision and vaccination records','Workplace injury / accident register with MLC linkage','Employer / company-wise health contracts and corporate billing'],
  diet:['Doctor-ordered therapeutic and routine diet plans','Ward-wise diet indents and kitchen production schedule','Diet costing and IPD billing integration'],
  bmwm:['Colour-coded waste categorisation (BMWM Rules 2016)','Barcoded bag tracking from generation point to CBWTF handover','Daily quantity logging and statutory Form-IV annual reporting','Authorisation, training and incident records'],
  mortuary:['Body register, storage allocation and release workflow','Death certificate and death-summary linkage','Police / MLC intimation where applicable'],
  mlc:['MLC register with auto-generated MLC number','Mandatory police-station intimation and acknowledgement capture','Injury documentation, evidence and chain-of-custody log','Linkage to emergency, trauma and mortuary modules'],
  consent:['Digital consent forms (surgery, anaesthesia, high-risk, data sharing)','e-Signature / thumb-impression capture and versioning','Multilingual consent templates and audit trail'],
  hr:['Staff master records and attendance tracking','Duty-roster scheduling and leave management'],
  payroll:['Salary processing automation and overtime-hours logging','Supervisor approval workflow; overtime in salary slip','Monthly overtime summary reports'],
  assets:['Equipment tracking (ventilators, MRI, ICU monitors)','Maintenance scheduling, AMC monitoring and breakdown alerts'],
  multibranch:['Centralised patient database and unified EMR across branches','Branch hospital integration and patient-transfer workflow'],
  compliance:['NABH compliance reporting and audit logs for all actions','Government reporting automation and data-security compliance'],
  abdm:['ABHA (Health ID) creation and linkage via Aadhaar / mobile','Scan & Share QR-based OPD registration','HFR & HPR onboarding','Consent-based health-record sharing (HIP/HIU)','EHR Standards 2016 and FHIR R4 record formats'],
  ai:['AI patient risk prediction','AI smart scheduling','24×7 AI chatbot support','AI inventory forecasting','AI fraud detection','AI claim pre-scrubbing'],
  paymentgw:['UPI, Card, NetBanking and QR payments','Razorpay / Stripe / PayU / Cashfree integration','Auto receipt generation','Refund and settlement management','Patient deposit / advance top-up'],
};

/* ---- Bootstrap: load registry + current patient from the API --------------
   Called by shell.js before the UI renders. Falls back gracefully if the API
   is unreachable so the shell still loads (with an empty, honest state). */
HIS.bootstrap = async function () {
  try {
    const reg = await HIS.api.registry();
    HIS.groups  = reg.groups  || [];
    HIS.modules = reg.modules || [];
  } catch (e) {
    console.error('Failed to load module registry from API', e);
    HIS.bootError = 'Could not reach the API for the module registry.';
  }
  try {
    HIS.mock.currentPatient = await HIS.api.defaultPatient();
  } catch (e) {
    console.warn('No default patient available', e);
    HIS.mock.currentPatient = null;
  }

  /* ---- RBAC sidebar scoping (L1.3.5) -------------------------------------
     Superadmin sees every module (bypasses RBAC by design → null = show all).
     Any other user sees only the modules their roles grant, from GET /api/menu;
     menu codes are aliased to the wireframe screen ids. On error we fail OPEN
     (show all) so a menu hiccup never locks a user out of navigation. */
  HIS.menuAllowed = null;       // null = no filter (all modules)
  HIS.defaultModule = 'dashboard';
  try {
    const prof = (HIS.auth && HIS.auth.get()) || {};
    if (!prof.isSuperAdmin) {
      const menu = await HIS.api.menu();                 // [{ code, label, icon, pages }]
      const alias = { emergency: 'icu', telemed: 'telemedicine', admin: null };
      const allow = new Set();
      (menu || []).forEach(m => {
        const id = (m.code in alias) ? alias[m.code] : m.code;
        if (id) allow.add(id);
      });
      HIS.menuAllowed = allow;
      // Default-open the first granted module that has a real screen.
      const first = (HIS.modules || []).find(m => allow.has(m.id));
      if (first) HIS.defaultModule = first.id;
    }
  } catch (e) {
    console.warn('Effective menu unavailable — showing all modules', e);
    HIS.menuAllowed = null;
  }
};

/* Fetch a lookup dataset on demand and cache it (shape matches the F3 modal). */
HIS.loadLookup = async function (type, q) {
  const data = await HIS.api.lookup(type, q);
  HIS.lookups[type] = data;   // cache last result for this type
  return data;
};
