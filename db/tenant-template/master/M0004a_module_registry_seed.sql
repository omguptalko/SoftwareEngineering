/* M0004a - seed the sidebar module registry (master.ModuleGroup + master.Module).
   M0004 creates these tables but the row data lived only in a DEV-only backfill,
   so freshly-onboarded tenants got an EMPTY sidebar. This seeds the full registry
   as part of the template. Runs after M0004 (tables), before M0011. Idempotent. */
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId=N'front')
    INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES (N'front', N'Registration & Front Office', N'bi-person-badge', 1);
IF NOT EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId=N'clinical')
    INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES (N'clinical', N'Clinical Services', N'bi-clipboard2-pulse', 2);
IF NOT EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId=N'diag')
    INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES (N'diag', N'Diagnostics & Pharmacy', N'bi-capsule', 3);
IF NOT EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId=N'ins')
    INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES (N'ins', N'Insurance & Schemes', N'bi-shield-plus', 4);
IF NOT EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId=N'support')
    INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES (N'support', N'Support Services', N'bi-life-preserver', 5);
IF NOT EXISTS (SELECT 1 FROM master.ModuleGroup WHERE GroupId=N'admin')
    INSERT master.ModuleGroup (GroupId, Label, Icon, SortOrder) VALUES (N'admin', N'Admin / HR / Compliance', N'bi-sliders', 6);
GO

IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'registration')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'registration', N'front', N'bi-person-vcard', N'Patient Registration & UHID', 1, NULL, 1, N'A3.1');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'appointments')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'appointments', N'front', N'bi-calendar-check', N'Appointment & Token', 1, NULL, 2, N'A3.2');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'vitals')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'vitals', N'front', N'bi-heart-pulse', N'Vitals Station', 1, NULL, 3, N'3.2');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'queue')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'queue', N'front', N'bi-display', N'Queue & Digital Signage', 0, N'NEW', 3, N'A3.31');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'feedback')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'feedback', N'front', N'bi-chat-square-heart', N'Feedback & Grievance', 0, N'NEW', 4, N'A3.30');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'opd')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'opd', N'clinical', N'bi-clipboard2-pulse', N'OPD Consultation', 1, NULL, 5, N'A3.3');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'ipd')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'ipd', N'clinical', N'bi-hospital', N'IPD Admission & Bed Board', 1, NULL, 6, N'A3.4');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'icu')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'icu', N'clinical', N'bi-activity', N'ICU Monitoring', 1, NULL, 7, N'A3.5');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'emergency')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'emergency', N'clinical', N'bi-truck-front', N'Emergency & Trauma', 1, NULL, 8, N'3.5');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'ot')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'ot', N'clinical', N'bi-scissors', N'Operation Theatre (OT)', 0, NULL, 8, N'A3.12');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'nursing')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'nursing', N'clinical', N'bi-clipboard2-heart', N'Nursing & Patient Care', 0, NULL, 9, N'A3.13');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'telemedicine')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'telemedicine', N'clinical', N'bi-camera-video', N'Telemedicine', 0, N'NEW', 10, N'A3.24');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'certificates')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'certificates', N'clinical', N'bi-file-earmark-medical', N'Certificates & Documents', 0, NULL, 11, N'A3.16');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'lab')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'lab', N'diag', N'bi-eyedropper', N'Laboratory (LIS)', 1, NULL, 12, N'A3.8');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'radiology')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'radiology', N'diag', N'bi-radioactive', N'Radiology & Imaging', 0, NULL, 13, N'A3.9');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'pharmacy')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'pharmacy', N'diag', N'bi-capsule', N'Pharmacy Management', 1, NULL, 14, N'A3.10');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'inventory')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'inventory', N'diag', N'bi-box-seam', N'Inventory & Store', 0, NULL, 15, N'A3.11');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'bloodbank')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'bloodbank', N'diag', N'bi-droplet-half', N'Blood Bank', 0, NULL, 16, N'A3.7');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'cashless')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'cashless', N'ins', N'bi-credit-card-2-front', N'Cashless / TPA Claims', 1, NULL, 17, N'A3.15');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'pmjay')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'pmjay', N'ins', N'bi-bank2', N'AB PM-JAY (BIS/TMS)', 1, N'NEW', 18, N'A7.3');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'esic')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'esic', N'ins', N'bi-building-check', N'ESIC', 0, N'NEW', 19, N'A7.4');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'cghs')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'cghs', N'ins', N'bi-shield-plus', N'CGHS', 0, N'NEW', 20, N'A7.5');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'echs')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'echs', N'ins', N'bi-shield-shaded', N'ECHS', 0, N'NEW', 21, N'A7.6');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'statescheme')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'statescheme', N'ins', N'bi-map', N'State Health Schemes', 0, N'NEW', 22, N'A7.7');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'claimsmis')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'claimsmis', N'ins', N'bi-graph-up', N'Claims MIS & Reconciliation', 0, N'NEW', 23, N'A7.8');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'ambulance')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'ambulance', N'support', N'bi-truck-front', N'Ambulance & GPS', 0, NULL, 24, N'A3.6');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'occhealth')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'occhealth', N'support', N'bi-hospital', N'Occupational Health', 0, N'NEW', 25, N'A3.23');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'diet')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'diet', N'support', N'bi-egg-fried', N'Diet & Kitchen', 0, N'NEW', 26, N'A3.26');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'bmwm')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'bmwm', N'support', N'bi-trash3', N'Bio-Medical Waste', 0, N'NEW', 27, N'A3.25');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'mortuary')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'mortuary', N'support', N'bi-file-earmark-x', N'Mortuary & Death', 0, N'NEW', 28, N'A3.27');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'mlc')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'mlc', N'support', N'bi-shield-fill-exclamation', N'Medico-Legal Case (MLC)', 0, N'NEW', 29, N'A3.28');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'consent')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'consent', N'support', N'bi-pen', N'Consent & e-Documents', 0, N'NEW', 30, N'A3.29');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'dashboard')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'dashboard', N'admin', N'bi-speedometer2', N'Admin Dashboard & Analytics', 1, NULL, 31, N'A3.20');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'billing')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'billing', N'admin', N'bi-receipt', N'Billing & Revenue Cycle', 1, NULL, 32, N'A3.14');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'hr')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'hr', N'admin', N'bi-people', N'HR Management', 0, NULL, 33, N'A3.17');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'payroll')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'payroll', N'admin', N'bi-cash-coin', N'Payroll & Overtime', 0, NULL, 34, N'A3.18');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'assets')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'assets', N'admin', N'bi-tools', N'Asset & Equipment', 0, NULL, 35, N'A3.19');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'multibranch')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'multibranch', N'admin', N'bi-diagram-3', N'Multi-Branch Sync', 0, NULL, 36, N'A3.21');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'compliance')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'compliance', N'admin', N'bi-shield-check', N'Compliance & Audit', 0, NULL, 37, N'A3.22');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'abdm')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'abdm', N'admin', N'bi-fingerprint', N'ABDM / ABHA Console', 1, N'NEW', 38, N'A6.2');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'ai')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'ai', N'admin', N'bi-cpu', N'AI Suite', 0, NULL, 39, N'A4');
IF NOT EXISTS (SELECT 1 FROM master.Module WHERE ModuleId=N'paymentgw')
    INSERT master.Module (ModuleId, GroupId, Icon, Label, Built, Badge, SortOrder, SrsRef) VALUES (N'paymentgw', N'admin', N'bi-wallet2', N'Payment Gateway', 1, NULL, 40, N'A5');
GO
