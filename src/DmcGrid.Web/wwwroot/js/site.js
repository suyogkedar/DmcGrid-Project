'use strict';
// Shared helpers for every DMCGrid screen.
const $ = id => document.getElementById(id), $$ = q => [...document.querySelectorAll(q)];

function toast(t) {
  const n = document.createElement('div');
  n.className = 'toast';
  n.textContent = t;
  document.body.appendChild(n);
  setTimeout(() => n.remove(), 2200);
}

// Show a toast on the next page after a redirect/reload.
function toastNext(t) { try { sessionStorage.setItem('dmcgrid.toast', t); } catch { /* storage unavailable */ } }
(function () {
  try {
    const t = sessionStorage.getItem('dmcgrid.toast');
    if (t) { sessionStorage.removeItem('dmcgrid.toast'); toast(t); }
  } catch { /* storage unavailable */ }
})();

// All data shown on screen is user-entered: escape before placing it in HTML.
function esc(v) {
  return String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function money(n) { return '₹' + Math.round(+n || 0).toLocaleString('en-IN'); }

// POST JSON (or FormData) with the antiforgery token. Resolves to {ok, message, data}.
async function api(url, body) {
  const isForm = body instanceof FormData;
  const headers = { 'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content };
  if (!isForm) headers['Content-Type'] = 'application/json';
  try {
    const res = await fetch(url, { method: 'POST', headers, body: isForm ? body : JSON.stringify(body) });
    if (res.redirected || res.status === 401 || res.status === 403) {
      location.href = '/Account/Login?returnUrl=' + encodeURIComponent(location.pathname);
      return { ok: false, message: 'Session expired — please log in again' };
    }
    if (!res.ok) return { ok: false, message: 'Something went wrong. Please try again.' };
    return await res.json();
  } catch {
    return { ok: false, message: 'Network error. Please check your connection.' };
  }
}

// api() plus a toast for the outcome. Returns the response.
async function call(url, body) {
  const r = await api(url, body);
  if (r.message) toast(r.message);
  return r;
}
