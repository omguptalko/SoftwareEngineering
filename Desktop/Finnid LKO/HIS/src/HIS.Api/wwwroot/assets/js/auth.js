/* ============================================================================
   HIS ERP — Client auth/session (L1.2.7)
   Holds the JWT + profile returned by POST /api/auth/login. The token is sent
   as a Bearer header by api.js; the app shell guards on it. "Remember this
   device" persists to localStorage, otherwise the session lives in
   sessionStorage (cleared when the tab closes). Nothing here is hardcoded —
   the realm/identity all come from the server response.
   ========================================================================== */
window.HIS = window.HIS || {};

HIS.auth = (function () {
  const KEY = 'his_auth';

  /** Persist the login profile. remember=true → localStorage (survives restart). */
  function save(profile, remember) {
    const target = remember ? localStorage : sessionStorage;
    const other  = remember ? sessionStorage : localStorage;
    try { other.removeItem(KEY); } catch (e) {}
    try { target.setItem(KEY, JSON.stringify(profile)); } catch (e) {}
  }

  /** Current profile (sessionStorage takes precedence), or null. */
  function get() {
    try {
      const raw = sessionStorage.getItem(KEY) || localStorage.getItem(KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (e) { return null; }
  }

  function token() { const p = get(); return p ? p.token : null; }

  /** True when there is no session, or the JWT expiry has passed. */
  function isExpired() {
    const p = get();
    if (!p || !p.token) return true;
    if (!p.expiresUtc) return false;
    return new Date(p.expiresUtc).getTime() <= Date.now();
  }

  function clear() {
    try { sessionStorage.removeItem(KEY); } catch (e) {}
    try { localStorage.removeItem(KEY); } catch (e) {}
  }

  /** Redirect to the login page when there is no valid session. Returns false if it redirected. */
  function requireSession(loginUrl) {
    if (isExpired()) { clear(); window.location.href = loginUrl || 'login.html'; return false; }
    return true;
  }

  return { save, get, token, isExpired, clear, requireSession };
})();
