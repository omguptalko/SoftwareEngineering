/* ============================================================================
   HIS ERP — Global keyboard engine (the "desktop feel")
   - Function keys F1..F12 mapped to toolbar actions
   - Enter advances to next field, Shift+Enter goes back (classic data entry)
   - Alt + letter opens menus by accesskey
   - Esc closes the top-most overlay
   - Ctrl shortcuts for tabs / open / save / logout
   - Caps Lock / Insert indicators in the status bar
   These call into HIS.action(), HIS.menu*(), HIS.lookupOpen (defined in shell.js)
   ========================================================================== */
window.HIS = window.HIS || {};

(function () {
  // Function-key -> action map (action handled by shell.js HIS.action)
  const FKEYS = {
    F1: 'cheat', F2: 'new', F3: 'find', F4: 'edit', F5: 'refresh',
    F6: 'savenew', F8: 'delete', F9: 'save', F12: 'print'
  };
  // Alt + accesskey letter -> menu button label (first letter underlined)
  const ALT_MENU = { f:0, e:1, v:2, p:3, c:4, i:5, r:6, t:7, h:8 };

  function isTypingTarget(el) {
    return el && (el.tagName === 'INPUT' || el.tagName === 'SELECT' || el.tagName === 'TEXTAREA' || el.isContentEditable);
  }

  /* ---- Enter advances to the next field --------------------------------- */
  function focusables(scope) {
    return Array.from(scope.querySelectorAll(
      'input:not([type=hidden]):not([disabled]):not([tabindex="-1"]), select:not([disabled]):not([tabindex="-1"]), textarea:not([disabled]), button.lk, [data-focusable]'
    )).filter(el => el.offsetParent !== null);
  }
  function advanceField(dir) {
    const doc = document.querySelector('.doc:not([hidden])') || document;
    const list = focusables(doc);
    const i = list.indexOf(document.activeElement);
    if (i === -1) { if (list[0]) list[0].focus(); return; }
    const next = list[i + dir];
    if (next) { next.focus(); if (next.select) try { next.select(); } catch (e) {} }
  }

  /* ---- Status-bar Caps / Insert indicators ------------------------------ */
  function updateIndicators(e) {
    const caps = document.getElementById('capsInd');
    const ins = document.getElementById('insInd');
    if (caps && e.getModifierState) caps.style.opacity = e.getModifierState('CapsLock') ? '1' : '.35';
    if (ins) { if (e.key === 'Insert') { HIS._ins = !HIS._ins; ins.style.opacity = HIS._ins ? '1' : '.35'; } }
  }

  document.addEventListener('keydown', function (e) {
    updateIndicators(e);

    // If a lookup or cheat overlay is open, let its own handler manage keys
    if (HIS.lookupIsOpen && HIS.lookupIsOpen()) return;

    const k = e.key;

    /* Esc — close top-most overlay / menu */
    if (k === 'Escape') {
      if (HIS.menuAnyOpen && HIS.menuAnyOpen()) { HIS.menuCloseAll(); e.preventDefault(); return; }
      if (HIS.cheatIsOpen && HIS.cheatIsOpen()) { HIS.action('cheat'); e.preventDefault(); return; }
      if (isTypingTarget(document.activeElement)) { document.activeElement.blur(); return; }
      HIS.action('escape'); e.preventDefault(); return;
    }

    /* Function keys */
    if (FKEYS[k]) {
      e.preventDefault();
      HIS.action(FKEYS[k]);
      HIS.flash && HIS.flash(FKEYS[k]);
      return;
    }

    /* Alt + accesskey -> open menu */
    if (e.altKey && !e.ctrlKey && ALT_MENU[k.toLowerCase()] !== undefined) {
      e.preventDefault();
      HIS.menuOpenByIndex && HIS.menuOpenByIndex(ALT_MENU[k.toLowerCase()]);
      return;
    }

    /* Ctrl shortcuts */
    if (e.ctrlKey && !e.altKey) {
      const lk = k.toLowerCase();
      if (k === 'Tab')      { e.preventDefault(); HIS.action('nexttab'); return; }
      if (lk === 'w')       { e.preventDefault(); HIS.action('closetab'); return; }
      if (lk === 'o')       { e.preventDefault(); HIS.openModule('registration'); return; }
      if (lk === 's')       { e.preventDefault(); HIS.action('save'); return; }
      if (lk === 'q')       { e.preventDefault(); HIS.action('logout'); return; }
    }

    /* Enter / Shift+Enter advance fields (inside form controls, not buttons/textarea) */
    if (k === 'Enter' && isTypingTarget(document.activeElement)) {
      const t = document.activeElement;
      if (t.tagName === 'TEXTAREA') return;           // textarea keeps newline
      if (t.dataset.submit === 'true') return;         // explicit submit fields
      e.preventDefault();
      advanceField(e.shiftKey ? -1 : 1);
      return;
    }
  });

  // Expose for shell to reuse
  HIS.advanceField = advanceField;
  HIS.focusables = focusables;
  HIS._ins = true;
})();
