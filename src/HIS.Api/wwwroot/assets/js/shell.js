/* ============================================================================
   HIS ERP — Shell: tree, MDI tabs, module loader, menus, F3 lookup, actions
   ========================================================================== */
window.HIS = window.HIS || {};

(function () {
  const $  = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const modById = id => HIS.modules.find(m => m.id === id);

  /* ===================== Session / context ============================== */
  // Returns false if the guard redirected to login (no/expired session).
  function initSession() {
    if (!HIS.auth || !HIS.auth.requireSession('login.html')) return false;
    const p = HIS.auth.get() || {};
    const role = p.isSuperAdmin ? 'Super Admin' : ((p.roles && p.roles[0]) || '—');
    $('#ctxUser').textContent = p.displayName || p.userName || '—';
    $('#ctxRole').textContent = role;
    // Reveal the admin console link: full "Platform Admin" for superadmins,
    // a tenant-scoped "Manage Users" for tenant admins (role 'admin'), L1.3.4 / L1.7.4.
    const isTenantAdmin = !p.isSuperAdmin && Array.isArray(p.roles) && p.roles.includes('admin');
    if (p.isSuperAdmin || isTenantAdmin) {
      const al = $('#adminLink');
      if (al) { al.hidden = false; if (isTenantAdmin) al.innerHTML = '<i class="bi bi-people"></i> Manage Users'; }
    }
    // Branch is assigned server-side (JWT claim / dev fallback); show the working branch label.
    const branchLabel = p.branchLabel || $('#sbBranch').textContent || '—';
    $('#ctxBranch').textContent = branchLabel;
    return true;
  }

  function logout() {
    if (HIS.auth) HIS.auth.clear();
    window.location.href = 'login.html';
  }

  /* ===================== Clock ========================================== */
  function tick() {
    const d = new Date();
    const p = n => String(n).padStart(2, '0');
    $('#clock').textContent = `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
  }

  /* ===================== Sidebar tree =================================== */
  function renderTree() {
    const tree = $('#tree');
    tree.innerHTML = '';
    const allow = HIS.menuAllowed;          // null = show all (superadmin); else a Set of granted module ids
    let visibleCount = 0;
    HIS.groups.forEach(g => {
      let mods = HIS.modules.filter(m => m.group === g.id);
      if (allow) mods = mods.filter(m => allow.has(m.id));
      if (!mods.length) return;             // hide groups with no granted modules
      visibleCount += mods.length;
      const grp = document.createElement('div');
      grp.className = 'tree__grp';
      grp.dataset.grp = g.id;
      grp.innerHTML =
        `<div class="tree__grp-h"><i class="bi bi-chevron-down chev"></i><i class="bi ${g.icon} gi"></i> ${g.label}</div>` +
        `<div class="tree__children">` +
        mods.map(m =>
          `<div class="tree__item" data-mod="${m.id}" title="${m.label}">
             <i class="bi ${m.icon}"></i><span class="t">${m.label}</span>
             ${m.badge ? `<span class="nb">${m.badge}</span>` : ''}
           </div>`).join('') +
        `</div>`;
      tree.appendChild(grp);
    });
    $('#modCount').textContent = allow ? visibleCount : HIS.modules.length;
    // group collapse
    $$('.tree__grp-h', tree).forEach(h => h.addEventListener('click', () => h.parentElement.classList.toggle('collapsed')));
    // item open
    $$('.tree__item', tree).forEach(it => it.addEventListener('click', () => openModule(it.dataset.mod)));
  }
  function setTreeActive(id) {
    $$('.tree__item').forEach(i => i.classList.toggle('active', i.dataset.mod === id));
  }
  function initModFilter() {
    $('#modFilter').addEventListener('input', function () {
      const q = this.value.toLowerCase();
      $$('.tree__item').forEach(i => {
        const hit = i.dataset.mod.includes(q) || i.textContent.toLowerCase().includes(q);
        i.style.display = hit ? '' : 'none';
      });
      $$('.tree__grp').forEach(g => {
        const any = $$('.tree__item', g).some(i => i.style.display !== 'none');
        g.style.display = any ? '' : 'none';
        if (q && any) g.classList.remove('collapsed');
      });
    });
  }

  /* ===================== MDI tabs + loader ============================== */
  const docarea = () => $('#docarea');
  const tabstrip = () => $('#tabstrip');
  let openTabs = [];

  function openModule(id) {
    const m = modById(id);
    if (!m) return;
    if (openTabs.includes(id)) { activateTab(id); return; }
    openTabs.push(id);

    // tab
    const tab = document.createElement('div');
    tab.className = 'mtab';
    tab.dataset.tab = id;
    tab.innerHTML = `<i class="bi ${m.icon} ti"></i><span class="tl">${m.label}</span><span class="x" title="Close (Ctrl+W)"><i class="bi bi-x"></i></span>`;
    tab.addEventListener('click', e => { if (e.target.closest('.x')) closeTab(id); else activateTab(id); });
    tabstrip().appendChild(tab);

    // doc
    const doc = document.createElement('div');
    doc.className = 'doc';
    doc.dataset.tab = id;
    doc.innerHTML = (HIS.screens && HIS.screens[id]) ? HIS.screens[id]() : HIS.placeholder(m);
    docarea().appendChild(doc);

    activateTab(id);
    if (HIS.afterRender) HIS.afterRender(id, doc);
    wireScreen(doc);
    focusFirst(doc);
  }

  function activateTab(id) {
    $('#emptyWs') && ($('#emptyWs').style.display = 'none');
    openTabs.forEach(t => {
      const isOn = t === id;
      const doc = $(`.doc[data-tab="${t}"]`); if (doc) doc.hidden = !isOn;
      const tab = $(`.mtab[data-tab="${t}"]`); if (tab) tab.classList.toggle('active', isOn);
    });
    setTreeActive(id);
    const m = modById(id);
    if (m) { setStatus(`${m.label}`); setLegend(id); }
    HIS.activeTab = id;
  }

  function closeTab(id) {
    const idx = openTabs.indexOf(id);
    if (idx === -1) return;
    openTabs.splice(idx, 1);
    const doc = $(`.doc[data-tab="${id}"]`); if (doc) doc.remove();
    const tab = $(`.mtab[data-tab="${id}"]`); if (tab) tab.remove();
    if (openTabs.length) activateTab(openTabs[Math.max(0, idx - 1)]);
    else { $('#emptyWs').style.display = 'grid'; setTreeActive(null); setStatus('Ready'); HIS.activeTab = null; }
  }
  function nextTab() {
    if (openTabs.length < 2) return;
    const i = openTabs.indexOf(HIS.activeTab);
    activateTab(openTabs[(i + 1) % openTabs.length]);
  }
  function focusFirst(doc) {
    const f = HIS.focusables ? HIS.focusables(doc)[0] : doc.querySelector('input,select,textarea,button');
    if (f) { try { f.focus(); f.select && f.select(); } catch (e) {} }
  }

  /* ===================== Per-screen wiring ============================= */
  function wireScreen(doc) {
    // internal tabs (.itabs)
    $$('.itabs', doc).forEach(group => {
      const tabs = $$('.itab', group);
      tabs.forEach(t => t.addEventListener('click', () => {
        tabs.forEach(x => x.classList.remove('active'));
        t.classList.add('active');
        const panes = group.parentElement;
        $$('[data-pane]', panes).forEach(p => p.hidden = p.dataset.pane !== t.dataset.tab);
      }));
    });
    // lookup buttons / fields
    $$('.lk', doc).forEach(btn => btn.addEventListener('click', () => {
      const inp = btn.closest('.with-btn') ? btn.closest('.with-btn').querySelector('.ctl') : null;
      openLookupFor(inp, btn.dataset.lookup || (inp && inp.dataset.lookup));
    }));
    // editable-grid "add row" buttons
    $$('[data-addrow]', doc).forEach(b => b.addEventListener('click', () => addGridRow(b.dataset.addrow)));
    // generic data-act buttons inside screens
    $$('[data-act]', doc).forEach(b => {
      if (b.closest('.menubar') || b.closest('.toolbar')) return;
      b.addEventListener('click', () => HIS.action(b.dataset.act, b));
    });
    // editable-grid row delete
    $$('.row-del', doc).forEach(b => b.addEventListener('click', () => {
      const tr = b.closest('tr'); if (tr) { tr.remove(); toast('Row removed'); }
    }));
    // bed click demo
    $$('.bed', doc).forEach(b => b.addEventListener('click', () => {
      const occ = b.classList.contains('occ');
      toast(occ ? `Bed ${b.dataset.bed} — open patient chart` : `Bed ${b.dataset.bed} — admit patient (F2)`);
    }));
  }

  function addGridRow(tableId) {
    const tb = $(`#${tableId} tbody`);
    if (!tb || !tb.dataset.tpl) return;
    const tr = document.createElement('tr');
    tr.innerHTML = tb.dataset.tpl;
    tb.appendChild(tr);
    wireScreen(tr);          // wire only the new row to avoid double-binding
    const first = tr.querySelector('input,select'); if (first) first.focus();
    toast('Row added — type, F3 to look up');
  }

  // Expose wireScreen so screens that inject content asynchronously (e.g. the
  // IPD bed board loaded from the API in afterRender) can wire their new nodes.
  HIS.wireScreenFragment = wireScreen;

  /* ===================== F3 Lookup modal =============================== */
  let lkOpen = false, lkRows = [], lkHi = 0, lkPick = null, lkType = null, lkOnKey = null;

  function openLookupFor(inputEl, type) {
    type = type || 'patient';
    openLookup(type, row => {
      if (!inputEl) return;
      const cols = HIS.lookups[type].cols.length;
      inputEl.value = cols > 1 ? `${row[0]} — ${row[1]}` : row[0];
      inputEl.classList.add('is-ok');
      // fill companion field if declared: data-for points to an element id to receive col index data-col
      const comp = document.getElementById(inputEl.dataset.fill || '');
      if (comp) comp.value = row[inputEl.dataset.fillCol || 1];
      // Notify screen listeners (banners, pending charges, etc.) — a programmatic
      // .value set does not fire input/change on its own.
      inputEl.dispatchEvent(new Event('change', { bubbles: true }));
      toast(`Selected: ${row[1] || row[0]}`);
      HIS.advanceField && HIS.advanceField(1);
    });
  }

  async function openLookup(type, onPick) {
    let data;
    try { data = await HIS.loadLookup(type); }
    catch (e) { toast('Lookup service unavailable'); return; }
    if (!data || !data.cols) return;
    lkType = type; lkPick = onPick; lkRows = data.rows.slice(); lkHi = 0;
    $('#lkTitle').textContent = data.title;
    $('#lkSearch').value = '';
    renderLkList(data, lkRows);
    $('#overlay').classList.add('show');
    $('#lookup').classList.add('show');
    lkOpen = true;
    setTimeout(() => $('#lkSearch').focus(), 0);

    const onSearch = () => {
      const q = $('#lkSearch').value.toLowerCase();
      lkRows = data.rows.filter(r => r.join(' ').toLowerCase().includes(q));
      lkHi = 0; renderLkList(data, lkRows);
    };
    $('#lkSearch').oninput = onSearch;

    lkOnKey = function (e) {
      if (!lkOpen) return;
      if (e.key === 'ArrowDown') { e.preventDefault(); lkHi = Math.min(lkHi + 1, lkRows.length - 1); paintHi(); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); lkHi = Math.max(lkHi - 1, 0); paintHi(); }
      else if (e.key === 'Enter') { e.preventDefault(); pick(); }
      else if (e.key === 'Escape') { e.preventDefault(); closeLookup(); }
    };
    document.addEventListener('keydown', lkOnKey, true);
  }
  function renderLkList(data, rows) {
    $('#lkCount').textContent = `${rows.length} item${rows.length === 1 ? '' : 's'}`;
    $('#lkList').innerHTML =
      `<table><thead><tr>${data.cols.map(c => `<th>${c}</th>`).join('')}</tr></thead><tbody>` +
      rows.map((r, i) => `<tr data-i="${i}" class="${i === lkHi ? 'hi' : ''}">${r.map(c => `<td>${c}</td>`).join('')}</tr>`).join('') +
      `</tbody></table>`;
    $$('#lkList tr[data-i]').forEach(tr => {
      tr.addEventListener('mouseenter', () => { lkHi = +tr.dataset.i; paintHi(); });
      tr.addEventListener('click', () => { lkHi = +tr.dataset.i; pick(); });
    });
  }
  function paintHi() {
    $$('#lkList tr[data-i]').forEach(tr => tr.classList.toggle('hi', +tr.dataset.i === lkHi));
    const el = $(`#lkList tr[data-i="${lkHi}"]`); if (el) el.scrollIntoView({ block: 'nearest' });
  }
  function pick() {
    const row = lkRows[lkHi]; if (!row) return;
    closeLookup();
    if (lkPick) lkPick(row);
  }
  function closeLookup() {
    lkOpen = false;
    $('#overlay').classList.remove('show');
    $('#lookup').classList.remove('show');
    if (lkOnKey) document.removeEventListener('keydown', lkOnKey, true);
  }
  $('#overlay').addEventListener('click', closeLookup);
  $('[data-lk-close]').addEventListener('click', closeLookup);

  HIS.lookupIsOpen = () => lkOpen;
  HIS.openLookup = openLookup;
  HIS.openLookupFor = openLookupFor;

  /* ===================== Menus ========================================= */
  const menus = () => $$('#menubar .menu');
  function menuCloseAll() { menus().forEach(m => m.classList.remove('open')); }
  function menuAnyOpen() { return menus().some(m => m.classList.contains('open')); }
  function menuOpenByIndex(i) {
    const m = menus()[i]; if (!m) return;
    const wasOpen = m.classList.contains('open');
    menuCloseAll();
    if (!wasOpen) m.classList.add('open');
  }
  function initMenus() {
    menus().forEach((m, i) => {
      const btn = m.querySelector('button');
      btn.addEventListener('click', e => { e.stopPropagation(); menuOpenByIndex(i); });
      btn.addEventListener('mouseenter', () => { if (menuAnyOpen()) { menuCloseAll(); m.classList.add('open'); } });
      $$('.menu__item', m).forEach(it => it.addEventListener('click', () => {
        menuCloseAll();
        const act = it.dataset.act, mod = it.dataset.mod;
        if (act === 'open' && mod) openModule(mod);
        else if (act) HIS.action(act, it);
      }));
    });
    document.addEventListener('click', menuCloseAll);
  }
  HIS.menuAnyOpen = menuAnyOpen;
  HIS.menuCloseAll = menuCloseAll;
  HIS.menuOpenByIndex = menuOpenByIndex;

  /* ===================== Cheat sheet =================================== */
  function toggleCheat() { $('#cheat').classList.toggle('show'); }
  HIS.cheatIsOpen = () => $('#cheat').classList.contains('show');
  $('[data-cheat-close]').addEventListener('click', () => $('#cheat').classList.remove('show'));

  /* ===================== Toast ======================================== */
  let toastT;
  function toast(msg, icon) {
    $('#toastMsg').textContent = msg;
    $('#toast').querySelector('i').className = 'bi ' + (icon || 'bi-check-circle-fill');
    $('#toast').classList.add('show');
    clearTimeout(toastT);
    toastT = setTimeout(() => $('#toast').classList.remove('show'), 1900);
  }
  HIS.toast = toast;

  /* ===================== Styled confirm dialog ======================== */
  // HIS.confirm({title, message, confirmLabel, cancelLabel, danger, icon}) -> Promise<bool>
  HIS.confirm = function (opts) {
    opts = opts || {};
    return new Promise(resolve => {
      const ov = document.createElement('div'); ov.className = 'overlay show'; ov.style.zIndex = '970';
      const dlg = document.createElement('div');
      dlg.className = 'confirm-dialog' + (opts.danger ? ' confirm-dialog--danger' : '');
      dlg.setAttribute('role', 'dialog'); dlg.setAttribute('aria-modal', 'true');
      const icon = opts.icon || (opts.danger ? 'bi-exclamation-triangle-fill' : 'bi-question-circle-fill');
      dlg.innerHTML =
        `<div class="cd-head"><i class="bi ${icon}"></i> ${opts.title || 'Please confirm'}</div>
         <div class="cd-body">${opts.message || 'Are you sure?'}</div>
         <div class="cd-foot">
           <button class="btn" data-cd-cancel>${opts.cancelLabel || 'Cancel'}</button>
           <button class="btn ${opts.danger ? 'btn--danger' : 'btn--primary'}" data-cd-ok>${opts.confirmLabel || 'Confirm'}</button>
         </div>`;
      document.body.appendChild(ov); document.body.appendChild(dlg);
      const close = val => { document.removeEventListener('keydown', onKey); ov.remove(); dlg.remove(); resolve(val); };
      const onKey = e => { if (e.key === 'Escape') close(false); else if (e.key === 'Enter') close(true); };
      dlg.querySelector('[data-cd-ok]').addEventListener('click', () => close(true));
      dlg.querySelector('[data-cd-cancel]').addEventListener('click', () => close(false));
      ov.addEventListener('click', () => close(false));
      document.addEventListener('keydown', onKey);
      dlg.querySelector('[data-cd-ok]').focus();
    });
  };

  /* ===================== Status bar ==================================== */
  function setStatus(txt) { $('#sbRec').textContent = txt; }
  function setLegend(id) {
    // contextual legend per screen type (kept generic for the wireframe)
    const L = $('#fkeyLegend');
    L.innerHTML =
      '<span><kbd>F1</kbd> <b>Help</b></span>' +
      '<span><kbd>F2</kbd> New</span>' +
      '<span><kbd>F3</kbd> Find</span>' +
      '<span><kbd>Enter</kbd> Next</span>' +
      '<span><kbd>F9</kbd> Save</span>' +
      '<span><kbd>F12</kbd> Print</span>' +
      '<span><kbd>Esc</kbd> Close</span>';
  }

  /* ===================== Toolbar flash ================================= */
  HIS.flash = function (act) {
    const b = $(`.toolbar .tbtn[data-act="${act}"]`);
    if (!b) return; b.classList.add('flash'); setTimeout(() => b.classList.remove('flash'), 160);
  };

  /* ===================== Toolbar click ================================= */
  $$('.toolbar .tbtn').forEach(b => b.addEventListener('click', () => HIS.action(b.dataset.act)));

  /* ===================== Action dispatcher ============================ */
  HIS.action = function (act, el) {
    switch (act) {
      case 'cheat':   toggleCheat(); break;
      case 'new':     clearActive(); toast('New record — form cleared'); break;
      case 'edit':    toast('Edit mode enabled', 'bi-pencil-square'); break;
      case 'refresh': toast('Refreshed', 'bi-arrow-clockwise'); break;
      case 'save':
        if (HIS.saveHandlers && HIS.saveHandlers[HIS.activeTab]) { HIS.saveHandlers[HIS.activeTab](); flashSaved(); break; }
        toast('Saved successfully', 'bi-check-circle-fill'); flashSaved(); break;
      case 'savenew': toast('Saved — new record started', 'bi-check-circle-fill'); clearActive(); break;
      case 'delete':  toast('Record deleted', 'bi-trash3'); break;
      case 'print':   window.print(); break;
      case 'find':    contextFind(); break;
      case 'closetab':if (HIS.activeTab) closeTab(HIS.activeTab); break;
      case 'nexttab': nextTab(); break;
      case 'logout':  logout(); break;
      case 'fhir-export': HIS.exportFhir && HIS.exportFhir(); break;
      case 'about':   toast('Finnid HIS ERP — interactive wireframe (SRS v2.0)', 'bi-info-circle'); break;
      case 'escape':  break;
      default: if (act) toast(act);
    }
  };

  function clearActive() {
    const doc = $('.doc:not([hidden])'); if (!doc) return;
    $$('input:not([readonly]), textarea', doc).forEach(i => { if (i.type !== 'checkbox' && i.type !== 'radio') i.value = ''; i.classList.remove('is-ok', 'is-bad'); });
    focusFirst(doc);
  }
  function flashSaved() {
    const b = $(`.toolbar .tbtn[data-act="save"]`); if (b) { b.classList.add('flash'); setTimeout(() => b.classList.remove('flash'), 160); }
  }
  function contextFind() {
    const a = document.activeElement;
    if (a && a.dataset && a.dataset.lookup) { openLookupFor(a, a.dataset.lookup); return; }
    // else open the active screen's primary lookup if declared
    const doc = $('.doc:not([hidden])');
    const prim = doc && doc.querySelector('[data-lookup]');
    if (prim) openLookupFor(prim, prim.dataset.lookup);
    else openLookup('patient', r => toast(`Selected patient: ${r[1]}`));
  }

  HIS.openModule = openModule;

  /* ===================== Real-time emergency alerts (task 0.9) ========= */
  // Hospital-wide: any open screen pops a critical-arrival alert pushed by the
  // server when a triage is registered. Reuses the @microsoft/signalr client.
  function initAlertsHub() {
    if (!window.signalR || HIS._alertsHub) return;
    try {
      const conn = new signalR.HubConnectionBuilder()
        .withUrl((window.HIS_API_BASE || '') + '/hubs/alerts')
        .withAutomaticReconnect()
        .build();
      conn.on('emergencyAlert', a => {
        const who = a.patient ? ' · ' + a.patient : '';
        const mlc = a.isMlc ? ' · MLC' : '';
        toast(`🚨 EMERGENCY · ${a.category} triage${mlc}${who}`, 'bi-exclamation-octagon-fill');
      });
      conn.start().catch(() => {});
      HIS._alertsHub = conn;
    } catch (e) { /* alerts are best-effort */ }
  }

  /* ===================== Boot ========================================= */
  (async function boot() {
    if (!initSession()) return;     // no/expired session → redirected to login; stop boot
    await HIS.bootstrap();          // load module registry + current patient from API
    initAlertsHub();                // subscribe to hospital-wide emergency alerts
    renderTree();
    initModFilter();
    initMenus();
    tick(); setInterval(tick, 1000);
    if (HIS.bootError) toast(HIS.bootError, 'bi-exclamation-triangle-fill');
    openModule(HIS.defaultModule || 'dashboard');   // first granted module (RBAC), else dashboard
  })();
})();
