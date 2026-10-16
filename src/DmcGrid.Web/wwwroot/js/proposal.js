'use strict';
// Agent: internal deal sheet, commercials and the final time-wise customer itinerary.
const proposal = window.proposalModel;
const finalDays = proposal.days;      // [{city, items:[{time,type,details,reference}]}]
const dealCosts = proposal.costs;     // [{type, desc, cost, charge}]
const itemFields = ['time', 'type', 'details', 'reference'];
const itemTypes = ['Flight', 'Local Cab', 'Transfer', 'Hotel', 'Activity', 'Meal', 'Other'];
let finalEditDay = 0, finalEditing = false;
let approval = proposal.approvalStatus;

function renderFinal() {
  $('dayEditTabs').innerHTML = finalDays.map((d, i) => `<button class="${i === finalEditDay ? 'active' : ''}" data-fday="${i}">Day ${i + 1}<br><span style="font-weight:500">${esc(d.city)}</span></button>`).join('');
  const d = finalDays[finalEditDay];
  if (!d) { $('dayEditBody').innerHTML = '<div class="dayMode">No itinerary days. Choose an itinerary from the compare screen.</div>'; return; }
  $('dayEditBody').innerHTML = `<div class="dayMode"><b>Day ${finalEditDay + 1} · ${esc(d.city)}</b> · ${d.items.length} time-wise items ${finalEditing ? '· Editing enabled' : '· Review mode'}</div>
 <div class="dayTimeline"><div class="tlRow head"><div>↕</div><div>TIME</div><div>TYPE</div><div>DETAILS</div><div>REFERENCE</div><div></div></div>
 ${d.items.map((x, i) => `<div class="tlRow ${finalEditing ? 'dragRow' : ''}" ${finalEditing ? `draggable="true" data-dragindex="${i}"` : ''}><div>${finalEditing ? '<span class="dragHandle">☰</span>' : '•'}</div><div>${finalEditing ? `<input data-fi="${i}" data-ff="0" maxlength="5" value="${esc(x.time)}">` : esc(x.time)}</div><div>${finalEditing ? `<select data-fi="${i}" data-ff="1">${itemTypes.map(v => `<option ${v === x.type ? 'selected' : ''}>${v}</option>`).join('')}</select>` : `<span class="tlType">${esc(x.type)}</span>`}</div><div>${finalEditing ? `<input data-fi="${i}" data-ff="2" maxlength="500" value="${esc(x.details)}">` : esc(x.details)}</div><div>${finalEditing ? `<input data-fi="${i}" data-ff="3" maxlength="300" value="${esc(x.reference)}">` : esc(x.reference)}</div><div>${finalEditing ? `<div class="rowActions"><button class="moveBtn" data-fup="${i}" title="Move up">↑</button><button class="moveBtn" data-fdown="${i}" title="Move down">↓</button><button class="timeDelete" data-fdel="${i}" title="Delete this time-wise line">🗑 Delete</button></div>` : ''}</div></div>`).join('')}</div>
 ${finalEditing ? `<div class="tlAdd"><button class="btn primary" data-fadd="Activity">+ Add Time-wise Detail</button><button class="btn" id="sortDayTime">↕ Sort by Time</button><button class="btn" data-fadd="Flight">+ Flight</button><button class="btn" data-fadd="Local Cab">+ Local Cab</button><button class="btn" data-fadd="Transfer">+ Transfer</button><button class="btn" data-fadd="Hotel">+ Hotel</button><button class="btn" data-fadd="Meal">+ Meal</button></div><div class="sub" style="margin-top:6px">On mobile use ↑ / ↓ to move a row. On desktop you can also drag ☰. Changing TIME automatically re-sorts the day; Sort by Time is also available.</div>` : ''}`;
  $('editDayBtn').textContent = finalEditing ? '✓ Done Editing' : `✎ Edit Day ${finalEditDay + 1}`;
}

const extraCost = () => dealCosts.reduce((a, x) => a + x.cost, 0);
const extraCharge = () => dealCosts.reduce((a, x) => a + x.charge, 0);
const num = id => Math.max(0, +$(id).value || 0);
const packageMargin = () => ($('marginType').value === 'Fixed' ? num('margin') : num('net') * num('margin') / 100);

function renderDealFinance() {
  $('dealCostTable').innerHTML = '<div class="costRow head"><div>TYPE</div><div>DESCRIPTION</div><div>AGENT COST</div><div>CUSTOMER CHARGE</div><div>MARGIN</div><div></div></div>' + dealCosts.map((x, i) => `<div class="costRow"><div><b>${esc(x.type)}</b></div><div>${esc(x.desc)}</div><div>${money(x.cost)}</div><div>${money(x.charge)}</div><div class="gain">${money(x.charge - x.cost)}</div><div><button class="iconBtn" data-rmcost="${i}" title="Remove service">×</button></div></div>`).join('');
  renderProfit();
}

function renderProfit() {
  const n = num('net'), mar = packageMargin();
  const ancillaryMargin = extraCharge() - extraCost();
  const grossProfit = mar + ancillaryMargin;
  const paid = num('tdsPaid'), payable = num('tdsPayable');
  $('profitSummary').innerHTML = `<span class="eyebrow">DEAL PROFITABILITY</span>
 <div class="pRow"><span>DMC package cost</span><b>${money(n)}</b></div>
 <div class="pRow"><span>Ancillary agent cost</span><b>${money(extraCost())}</b></div>
 <div class="pRow"><span>Package margin</span><b>${money(mar)}</b></div>
 <div class="pRow"><span>Ancillary margin</span><b>${money(ancillaryMargin)}</b></div>
 <div class="pRow"><span>Gross deal profit</span><b>${money(grossProfit)}</b></div>
 <div class="pRow"><span>TDS paid / deducted</span><b>${money(paid)}</b></div>
 <div class="pRow"><span>TDS payable</span><b>${money(payable)}</b></div>
 <div class="pRow total"><span>Cash profit after tracked TDS</span><b>${money(grossProfit - paid - payable)}</b></div>
 <div class="sub">Management view; not the statutory tax computation.</div>`;
}

// Mirrors DmcGrid.Core PricingCalculator; the server recomputes the customer price from saved values.
function calc() {
  const n = num('net'), mar = packageMargin(), extras = extraCharge(), sub = n + mar + extras;
  const g = sub * num('gst') / 100, t = (sub + g) * num('tcs') / 100, total = sub + g + t;
  $('breakdown').innerHTML = `<span>DMC net package</span><b>${money(n)}</b><span>Agent margin</span><b>${money(mar)}</b><span>Agent-arranged services</span><b>${money(extras)}</b><span>GST</span><b>${money(g)}</b><span>TCS</span><b>${money(t)}</b><span>Customer price</span><b class="total">${money(total)}</b>`;
  renderProfit();
  return total;
}

function renderApproval() {
  const pending = approval === 'Pending', approved = approval === 'Approved';
  $('approvalStatus').textContent = approved ? 'Manager approved ✓' : pending ? 'Pending manager approval' : 'Draft · Not sent to manager';
  $('sendManager').textContent = pending || approved ? 'Sent ✓' : 'Send to Manager →';
  $('sendManager').disabled = pending || approved;
  $('managerApprove').style.display = pending || approved ? 'inline-flex' : 'none';
  $('managerApprove').textContent = approved ? 'Approved ✓' : 'Manager Approve ✓';
  $('managerApprove').disabled = approved;
  $('approvalHint').textContent = approved
    ? 'Approved by manager · proposal is ready to be treated as final and sent to the customer.'
    : 'You can preview the customer proposal now. Manager approval is required only before marking/sending the proposal as final.';
}

function collect() {
  return {
    rfqId: proposal.rfqId, netCost: num('net'), marginType: $('marginType').value, margin: num('margin'), gstPct: num('gst'), tcsPct: num('tcs'),
    travellers: Math.max(1, Math.round(num('travellers'))), validity: $('validity').value, brandName: $('brand').value, title: $('title').value,
    gstNumber: $('gstNumber').value, contact: $('contact').value, inclusions: $('inclusions').value, exclusions: $('exclusions').value,
    tdsPaid: num('tdsPaid'), tdsPayable: num('tdsPayable'), days: finalDays, costs: dealCosts
  };
}

// Saves are serialised so a later save can never be overtaken by an earlier one.
let saveChain = Promise.resolve(true);
function save(quiet = true) {
  saveChain = saveChain.then(async () => {
    const r = await api('/Proposal/Save', collect());
    if (!r.ok) { toast(r.message); return false; }
    if (approval !== r.data.approvalStatus) {
      if (approval !== 'Draft') toast('Deal changed after submission · manager approval reset to draft');
      approval = r.data.approvalStatus;
      renderApproval();
    } else if (!quiet) toast(r.message);
    return true;
  });
  return saveChain;
}

function sortCurrentDay() {
  const m = v => { const x = String(v || '').match(/^(\d{1,2}):(\d{2})$/); return x ? (+x[1] * 60 + +x[2]) : 9999; };
  finalDays[finalEditDay].items.sort((a, b) => m(a.time) - m(b.time));
  renderFinal();
}
function moveTimeline(from, to) {
  const a = finalDays[finalEditDay].items;
  if (to < 0 || to >= a.length || from === to) return;
  a.splice(to, 0, a.splice(from, 1)[0]);
  renderFinal();
}
// Tidies customer-facing wording only; times, types and references are untouched.
function polishText(s) {
  let t = String(s || '').trim().replace(/\s*·\s*/g, ', ').replace(/\s*→\s*/g, ' to ').replace(/\s+/g, ' ');
  if (!t) return t;
  t = t[0].toUpperCase() + t.slice(1);
  return /[.!?]$/.test(t) ? t : t + '.';
}

function bind() {
  $('addDealCost').onclick = () => {
    const desc = $('costDesc').value.trim(), cost = +$('costRetail').value, charge = +$('costCharge').value;
    if (!desc || $('costRetail').value === '' || $('costCharge').value === '' || !(cost >= 0) || !(charge >= 0)) { toast('Enter description, agent cost and customer charge'); return; }
    dealCosts.push({ type: $('costType').value, desc, cost, charge });
    $('costDesc').value = ''; $('costRetail').value = ''; $('costCharge').value = '';
    renderDealFinance(); calc(); save(); toast('Service added ✓');
  };
  $('dealCostTable').onclick = e => { const b = e.target.closest('[data-rmcost]'); if (!b) return; dealCosts.splice(+b.dataset.rmcost, 1); renderDealFinance(); calc(); save(); };
  ['tdsPaid', 'tdsPayable'].forEach(id => { $(id).oninput = renderProfit; $(id).onchange = () => save(); });
  ['net', 'margin', 'gst', 'tcs', 'marginType'].forEach(id => $(id).addEventListener(id === 'marginType' ? 'change' : 'input', calc));

  $('recalc').onclick = async () => {
    const total = calc();
    if (!await save()) return;
    $('recalc').textContent = 'Updated ✓ ' + money(total);
    setTimeout(() => $('recalc').textContent = 'Recalculate Proposal', 1200);
    toast('Proposal recalculated and customer price updated ✓');
  };
  $('sendManager').onclick = async () => {
    if (!await save()) return;
    const r = await call('/Proposal/SendToManager', { rfqId: proposal.rfqId });
    if (r.ok) { approval = 'Pending'; renderApproval(); }
  };
  $('managerApprove').onclick = async () => {
    const r = await call('/Proposal/Approve', { rfqId: proposal.rfqId });
    if (r.ok) { approval = 'Approved'; renderApproval(); }
  };
  $('polish').onclick = () => {
    finalDays.forEach(d => d.items.forEach(x => { x.details = polishText(x.details); }));
    renderFinal(); save();
    toast('AI polished copy without changing confirmed services ✓');
  };
  $('generate').onclick = async () => {
    if (!finalDays.some(d => d.items.length)) { toast('Add at least one itinerary item'); return; }
    if (!await save()) return;
    toastNext(approval === 'Approved' ? 'Final customer proposal generated ✓' : 'Customer proposal preview generated · pending manager approval');
    location.href = '/Proposal/Customer/' + proposal.rfqId;
  };

  $('dayEditTabs').onclick = e => { const b = e.target.closest('[data-fday]'); if (!b) return; if (finalEditing) save(); finalEditDay = +b.dataset.fday; finalEditing = false; renderFinal(); };
  $('editDayBtn').onclick = () => { if (!finalDays.length) return; finalEditing = !finalEditing; renderFinal(); if (!finalEditing) save(false); };

  const body = $('dayEditBody');
  let draggedTimelineIndex = null;
  const setField = x => { finalDays[finalEditDay].items[+x.dataset.fi][itemFields[+x.dataset.ff]] = x.value; };
  body.addEventListener('input', e => { if (e.target.dataset.fi !== undefined) setField(e.target); });
  body.addEventListener('change', e => {
    const x = e.target;
    if (x.dataset.fi === undefined) return;
    setField(x);
    if (x.dataset.ff === '0') { sortCurrentDay(); toast('Timeline automatically sorted by time ✓'); }
  });
  body.addEventListener('click', e => {
    const items = finalDays[finalEditDay].items;
    let b = e.target.closest('[data-fadd]');
    if (b) { items.push({ time: '10:00', type: b.dataset.fadd, details: 'Enter ' + b.dataset.fadd.toLowerCase() + ' details', reference: 'Add details' }); sortCurrentDay(); toast(`${b.dataset.fadd} added at 10:00 and placed by time ✓`); return; }
    b = e.target.closest('[data-fdel]');
    if (b) { const i = +b.dataset.fdel, item = items[i]; if (confirm(`Delete ${item.time} · ${item.type} · ${item.details}?`)) { items.splice(i, 1); renderFinal(); toast('Time-wise line deleted ✓'); } return; }
    b = e.target.closest('[data-fup]'); if (b) { moveTimeline(+b.dataset.fup, +b.dataset.fup - 1); return; }
    b = e.target.closest('[data-fdown]'); if (b) { moveTimeline(+b.dataset.fdown, +b.dataset.fdown + 1); return; }
    if (e.target.closest('#sortDayTime')) { sortCurrentDay(); toast('Day sorted chronologically ✓'); }
  });
  body.addEventListener('dragstart', e => { const r = e.target.closest('[data-dragindex]'); if (!r) return; draggedTimelineIndex = +r.dataset.dragindex; r.classList.add('dragging'); if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'; });
  body.addEventListener('dragend', () => { $$('.dragRow').forEach(r => r.classList.remove('dragging', 'dragOver')); draggedTimelineIndex = null; });
  body.addEventListener('dragover', e => { const r = e.target.closest('[data-dragindex]'); if (!r || draggedTimelineIndex === null) return; e.preventDefault(); $$('.dragRow').forEach(x => x.classList.remove('dragOver')); r.classList.add('dragOver'); });
  body.addEventListener('drop', e => { const r = e.target.closest('[data-dragindex]'); if (!r || draggedTimelineIndex === null) return; e.preventDefault(); moveTimeline(draggedTimelineIndex, +r.dataset.dragindex); toast('Timeline reordered ✓'); });
}

renderFinal();
renderDealFinance();
calc();
renderApproval();
bind();
