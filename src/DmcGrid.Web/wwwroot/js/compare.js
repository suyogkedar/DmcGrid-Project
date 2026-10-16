'use strict';
// Agent: compare DMC package options, connect with up to two DMCs, then choose the itinerary to take forward.
const model = window.compareModel;
const pk = model.packages, req = model.requested;
const MAX_DMCS = 2;
const selected = new Set();                       // package ids
const connected = new Set(model.connectedDmcIds); // DMC user ids
let chosen = model.chosenPackageId;
let closed = model.closed;

const label = p => `${p.dmcAlias} · ${p.name}`;
const byId = id => pk.find(p => p.packageId === id);
const plural = (n, word) => `${n} ${word}${n === 1 ? '' : 's'}`;

function renderMatrix() {
  const el = $('matrix');
  if (!pk.length) {
    el.style.display = 'block'; el.style.minWidth = '0';
    el.innerHTML = `<div class="cell" style="border:0"><b>No quotes received yet</b><div class="sub">${closed ? 'This RFQ is closed to new quotes.' : 'Matched DMCs have been notified. Their package options will appear here for comparison.'}</div></div>`;
    return;
  }
  el.style.gridTemplateColumns = `100px 1.05fr repeat(${pk.length},1fr)`;
  el.style.minWidth = `${250 + pk.length * 150}px`;
  const pair = (a, b) => `<b>${esc(a)}</b><div class="sub">${esc(b)}</div>`;
  const text = v => esc(v || '—');
  const rows = [
    ['RFQ FIELD', 'AGENT REQUESTED', ...pk.map(p => {
      const state = connected.has(p.dmcUserId)
        ? '<span class="tableSelect selected" style="cursor:default;text-align:center">Connected ✓</span>'
        : `<button class="tableSelect ${selected.has(p.packageId) ? 'selected' : ''}" data-mselect="${p.packageId}">${selected.has(p.packageId) ? 'Selected ✓' : 'Select option'}</button>`;
      return esc(label(p).toUpperCase()) + state;
    })],
    ['SUMMARY', pair(req.summary, req.people), ...pk.map(p => pair(money(p.netPrice), `${p.matchPct}% match`))],
    ['Hotel', text(req.hotel), ...pk.map(p => text(p.hotel))],
    ['Vehicle', text(req.vehicle), ...pk.map(p => text(p.vehicle))],
    ['Food', text(req.food), ...pk.map(p => text(p.food))],
    ['Meal plan', text(req.meal), ...pk.map(p => text(p.meal))],
    ['Budget', text(req.budget), ...pk.map(() => '—')],
    ['Priority', text(req.priority), ...pk.map(p => text(p.matchNote))]
  ];
  el.innerHTML = rows.flatMap((r, ri) => r.map((v, ci) =>
    `<div class="cell ${ri === 0 ? 'headcell' : ''} ${ci === 1 ? 'requested' : ''} ${ri === 1 && ci > 1 ? 'summary' : ''}">${ci === 0 ? esc(v) : v}</div>`)).join('');
}

function updateSelection() {
  const picked = [...selected].map(byId);
  const dmcs = new Set(picked.map(p => p.dmcUserId));
  const full = connected.size >= MAX_DMCS;
  $('selectionText').textContent = selected.size
    ? `${plural(selected.size, 'option')} selected from ${plural(dmcs.size, 'DMC')}`
    : connected.size ? `Connected to ${connected.size} of ${MAX_DMCS} DMCs` : 'Select package options below';
  $('connectionHint').textContent = full ? 'Connected to 2 DMCs · connection complete.'
    : connected.size === 1 ? 'Connected to 1 DMC. One DMC connection remains available.'
    : dmcs.size === 1 ? '1 DMC connection remains available for this RFQ.'
    : 'Maximum 2 distinct DMCs. Unlimited options within those DMCs.';
  $('connect').disabled = !selected.size || full;
  $('connect').textContent = full ? 'Connected ✓' : 'Connect selected DMCs';
  $('billing').disabled = connected.size > 0;
}

function renderConnectedWorkspace() {
  const box = $('connectedWorkspace');
  box.style.display = connected.size ? 'block' : 'none';
  if (!connected.size) return;
  $('connectedOptions').innerHTML = pk.filter(p => connected.has(p.dmcUserId)).map(p => {
    const on = chosen === p.packageId;
    return `<div class="connectedOption ${on ? 'selectedFinal' : ''}"><div class="meta"><b>${esc(label(p))}</b><span class="versionTag">${esc(p.versionLabel)}</span></div><div class="sub">${esc(p.dmcOrg)}</div><h3 style="margin:7px 0">${money(p.netPrice)}</h3><div class="updateBox"><b>${esc(p.versionStatus)}</b><br>${esc(p.statusNote || p.matchNote)}</div><button class="btn ${on ? 'primary' : ''}" style="margin-top:8px" data-finalconnected="${p.packageId}">${on ? 'Chosen ✓' : 'Choose this itinerary'}</button></div>`;
  }).join('');
  $('quoteTimeline').innerHTML = '<b>Quote history</b><div>Original quote(s) → Agent connected</div><div>DMC may submit a revision → old version retained</div><div>DMC may add a new option → agent chooses which itinerary to take forward</div>';
}

// ----- Day-wise and time-wise comparison -----
const baseSlots = [
  { label: '06:00–09:00', from: 360, to: 540 },
  { label: '09:00–13:00', from: 540, to: 780 },
  { label: '13:00–17:00', from: 780, to: 1020 },
  { label: '17:00–21:00', from: 1020, to: 1260 }
];
function mins(t) { const m = /^(\d{1,2}):(\d{2})$/.exec(t || ''); return m ? +m[1] * 60 + +m[2] : NaN; }
function inSlot(a, slot) {
  const from = mins(a.from), to = Number.isNaN(mins(a.to)) ? from + 1 : mins(a.to);
  return from < slot.to && to > slot.from;
}
function renderTimeComparison(day) {
  const el = $('timeCompareTable');
  el.style.setProperty('grid-template-columns', `100px 180px repeat(${pk.length},minmax(180px,1fr))`, 'important');
  let h = ['BASE SLOT', 'AGENT REQUESTED', ...pk.map(label)].map((x, i) => `<div class="timeCell head ${i === 1 ? 'req' : ''}">${esc(x)}</div>`).join('');
  baseSlots.forEach((slot, si) => {
    h += `<div class="timeCell"><b>${slot.label}</b></div><div class="timeCell req">${si === 1 && req.intent ? `<div>${esc(req.intent)}</div>` : '<span class="sub">—</span>'}</div>`;
    pk.forEach(p => {
      const items = (p.days[day] || []).filter(a => inSlot(a, slot));
      h += `<div class="timeCell">${items.length ? items.map(a => `<div style="margin-bottom:5px">• ${esc(a.from)}–${esc(a.to)} · ${esc(a.desc)}</div>`).join('') : '<span class="sub">—</span>'}</div>`;
    });
  });
  el.innerHTML = h;
}
function renderCompareDay(n = 0) {
  const dayCount = Math.max(...pk.map(p => p.days.length));
  $('compareDays').innerHTML = Array.from({ length: dayCount }, (_, i) => `<button class="dayTab ${i === n ? 'on' : ''}" data-cday="${i}">Day ${i + 1}</button>`).join('');
  $('itinCards').style.gridTemplateColumns = `repeat(${pk.length + 1},minmax(145px,1fr))`;
  const cards = [['AGENT REQUESTED', req.intent || req.summary, 'Requested intent'],
    ...pk.map(p => [label(p), (p.days[n] || []).map(a => a.desc).filter(Boolean).join(' · ') || '—', 'Proposed itinerary'])];
  $('itinCards').innerHTML = cards.map(c => `<div class="mini"><span class="sub">${esc(c[0].toUpperCase())}</span><b style="display:block;margin:6px 0;line-height:1.45">${esc(c[1])}</b><span class="sub">${c[2]}</span></div>`).join('');
  renderTimeComparison(n);
}

function bind() {
  $('matrix').onclick = e => {
    const b = e.target.closest('[data-mselect]');
    if (!b) return;
    const id = +b.dataset.mselect, next = new Set(selected);
    next.has(id) ? next.delete(id) : next.add(id);
    const dmcs = new Set([...connected, ...[...next].map(x => byId(x).dmcUserId)]);
    if (dmcs.size > MAX_DMCS) { toast('Maximum 2 distinct DMCs per RFQ'); return; }
    selected.clear(); next.forEach(x => selected.add(x));
    renderMatrix(); updateSelection();
  };
  $('compareDays').onclick = e => { const b = e.target.closest('[data-cday]'); if (b) renderCompareDay(+b.dataset.cday); };

  $('connect').onclick = async () => {
    $('connect').disabled = true;
    const r = await api('/Compare/Connect', { rfqId: model.rfqId, packageIds: [...selected], billing: $('billing').value });
    if (!r.ok) { toast(r.message); updateSelection(); return; }
    // Reload so DMC identities, balances and the connected workspace come from the server.
    toastNext(r.message);
    location.hash = 'connectedWorkspace';
    location.reload();
  };

  $('closeRfq').onclick = async () => {
    const r = await call('/Compare/ToggleClose', { rfqId: model.rfqId });
    if (!r.ok) return;
    closed = r.data.closed;
    $('closeRfq').textContent = closed ? 'Reactivate RFQ' : 'Close RFQ';
    if (!pk.length) renderMatrix();
  };

  $('connectedWorkspace').onclick = async e => {
    const b = e.target.closest('[data-finalconnected]');
    if (!b) return;
    const id = +b.dataset.finalconnected;
    if (chosen && chosen !== id && !confirm('Replace the current final itinerary with this option? Day-wise edits made in the proposal will be replaced.')) return;
    if (chosen === id) { location.href = $('continueFinal').href; return; }
    const r = await call('/Compare/Choose', { rfqId: model.rfqId, packageId: id });
    if (!r.ok) return;
    chosen = id;
    renderConnectedWorkspace();
    $('continueFinal').style.display = '';
  };
}

renderMatrix();
updateSelection();
renderConnectedWorkspace();
if (pk.length) renderCompareDay(); else $('itinCard').style.display = 'none';
bind();
