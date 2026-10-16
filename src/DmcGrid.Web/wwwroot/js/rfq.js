'use strict';
// Create / edit RFQ: AI copilot mode and tap-style tabs both edit the same RFQ object.
const rfq = window.rfqModel;
rfq.food = rfq.foodPrefs ? rfq.foodPrefs.split(',') : [];
rfq.ai = new Set(rfq.aiFields ? rfq.aiFields.split(',') : []);

const app = { rfqStep: 0 };
const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const rfqTabNames = ['Travel', 'Flights', 'Hotel', 'Vehicle', 'Food', 'Budget'];
// RFQ field → key used by the copilot to flag AI-filled values.
const aiKeys = { travelYear: 'year', travelMonth: 'month', hotelCategory: 'hotel', roomType: 'room', mealPlan: 'meal', foodPrefs: 'food' };
const aiKey = k => aiKeys[k] || k;
const dateOnly = v => (v ? String(v).slice(0, 10) : '');
const todayIso = isoDate(new Date().getFullYear(), new Date().getMonth(), new Date().getDate());

const cal = { year: rfq.travelYear, month: Math.max(0, monthNames.indexOf(rfq.travelMonth)), start: dateOnly(rfq.startDate), end: dateOnly(rfq.endDate), pickingEnd: false };

function aiMark(k) { return rfq.ai.has(aiKey(k)) ? ' <span class="aimark">✨ AI</span>' : ''; }
function isSel(k, v) { return k === 'foodPrefs' ? rfq.food.includes(v) : rfq[k] === v; }
function choices(k, arr) {
  return '<div class="choiceGrid">' + arr.map(v => `<button class="choice ${isSel(k, v) ? 'on' : ''}" data-choice="${k}" data-val="${esc(v)}">${esc(v)}${isSel(k, v) ? aiMark(k) : ''}</button>`).join('') + '</div>';
}
function yearOptions() {
  const base = new Date().getFullYear(), years = [base, base + 1, base + 2];
  if (rfq.travelYear && !years.includes(rfq.travelYear)) years.unshift(rfq.travelYear);
  return years;
}

function renderExtracts() {
  const none = 'Not specified';
  const flights = [rfq.stops, rfq.cabin, rfq.flightTime].filter(Boolean).join(' · ');
  const nights = rfq.nights ? ` · ${rfq.nights} nights` : '';
  $('extracts').innerHTML = [
    ['Destination', rfq.destination],
    ['Travel', `${rfq.travelMonth} ${rfq.travelYear} · ${rfq.adults} adults${nights}`],
    ['Hotel', rfq.hotelCategory], ['Vehicle', rfq.vehicle], ['Food', rfq.food.join(' + ')],
    ['Budget', rfq.budget], ['Style', rfq.priorities], ['Flights', flights]
  ].map(x => `<div class="extract"><b>${x[0]}</b>${esc(x[1] || none)}</div>`).join('');
}

function panel() {
  const s = app.rfqStep;
  if (s === 0) return `<div class="formGrid"><div class="field wide"><label>Year${aiMark('travelYear')}</label><div class="chips yearTabs">${yearOptions().map(x => `<button type="button" class="chip ${rfq.travelYear === x ? 'on' : ''}" data-year="${x}">${x}</button>`).join('')}</div></div><div class="field wide"><label>Month${aiMark('travelMonth')}</label><div class="chips monthTabs">${monthNames.map((x, i) => `<button type="button" class="chip ${x === rfq.travelMonth ? 'on' : ''}" data-month="${i}">${x.slice(0, 3)}</button>`).join('')}</div></div><div class="field wide"><label>Destination${aiMark('destination')}</label><input data-rfield="destination" maxlength="100" value="${esc(rfq.destination)}"></div><div class="field"><label>Adults${aiMark('adults')}</label><div class="counter"><button type="button" data-count="adults" data-delta="-1">−</button><b>${rfq.adults}</b><button type="button" data-count="adults" data-delta="1">+</button></div></div><div class="field"><label>Children &lt;12</label><div class="counter"><button type="button" data-count="children" data-delta="-1">−</button><b>${rfq.children}</b><button type="button" data-count="children" data-delta="1">+</button></div></div><div class="field wide"><label>Preferred travel dates</label><div class="calendarBox"><div class="calendarHead"><button class="btn" type="button" id="calPrev">‹</button><b id="calTitle"></b><button class="btn" type="button" id="calNext">›</button></div><div class="week"><span>MON</span><span>TUE</span><span>WED</span><span>THU</span><span>FRI</span><span>SAT</span><span>SUN</span></div><div id="calendarGrid" class="calendarGrid"></div><div class="dateSummary" id="dateSummary"></div><div class="sub" style="margin-top:5px">Tap a start date, then tap an end date. Tap a new date to start again.</div></div></div></div>`;
  if (s === 1) return `<div class="field"><label>Flight timing${aiMark('flightTime')}</label>${choices('flightTime', ['Morning', 'Afternoon', 'Evening', 'Overnight', 'No Preference'])}</div><div class="field"><label>Stops${aiMark('stops')}</label>${choices('stops', ['Direct', '1 stop', 'No Preference'])}</div><div class="field"><label>Cabin${aiMark('cabin')}</label>${choices('cabin', ['Economy', 'Premium Economy', 'Business', 'First Class', 'No Preference'])}</div><div class="calc">Flight preferences the AI could not infer remain unselected.</div>`;
  if (s === 2) return `<div class="fastSection"><div class="fastRow"><label>Hotel category${aiMark('hotelCategory')}</label>${choices('hotelCategory', ['3★', '4★', '5★', '5★ Premium', 'Private Villa', 'Boutique'])}</div><div class="fastRow"><label>Room type${aiMark('roomType')}</label>${choices('roomType', ['Deluxe', 'Premium', 'Suite', 'Family', 'Villa', 'No Preference'])}</div><div class="fastRow"><label>Meal plan${aiMark('mealPlan')}</label>${choices('mealPlan', ['Room Only', 'Breakfast', 'Half Board', 'Full Board', 'All Inclusive'])}</div><div class="fastRow"><label>Multi-city hotels</label><input data-rfield="cities" maxlength="300" value="${esc(rfq.cities)}" placeholder="Bangkok / Pattaya / Phuket"></div></div>`;
  if (s === 3) return `<div class="field"><label>Vehicle preference${aiMark('vehicle')}</label>${choices('vehicle', ['Private', 'Luxury', 'Premium SUV', 'SIC / Shared', 'Self Drive', 'No Preference'])}</div>`;
  if (s === 4) return `<div class="field"><label>Food preferences${aiMark('foodPrefs')}</label>${choices('foodPrefs', ['Vegetarian', 'Jain', 'Non-Veg', 'Vegan', 'Halal', 'Indian', 'Local'])}</div><div class="field"><label>Allergies / restrictions</label><input data-rfield="allergies" maxlength="300" value="${esc(rfq.allergies)}" placeholder="None specified"></div>`;
  return `<div class="field"><label>Trip budget${aiMark('budget')}</label>${choices('budget', ['₹1–2L', '₹2–3L', '₹3–5L', '₹5–10L', '₹10L+', 'Prefer not to say'])}</div><div class="field"><label>Priorities${aiMark('priorities')}</label><textarea data-rfield="priorities" maxlength="1000">${esc(rfq.priorities)}</textarea></div>`;
}

function renderRfqTabs() {
  $('rfqTabs').innerHTML = rfqTabNames.map((x, i) => `<button class="tab ${i === app.rfqStep ? 'on' : ''} ${i < app.rfqStep ? 'done' : ''}" data-rfqtab="${i}">${i < app.rfqStep ? '✓ ' : ''}${x}</button>`).join('');
  $('rfqPanel').innerHTML = `<div class="rfqProgress"><span><b>${app.rfqStep + 1}/6</b> · ${rfqTabNames[app.rfqStep]}</span><span>Jump to any section above</span></div>` + panel();
  $('prev').disabled = app.rfqStep === 0;
  $('next').textContent = app.rfqStep === 5 ? 'Review ✓' : 'Next →';
  if (app.rfqStep === 0) renderCalendar();
}

function setMode(m) {
  $('aiMode').classList.toggle('hidden', m !== 'ai');
  $('tabMode').classList.toggle('hidden', m !== 'tabs');
  $$('[data-mode]').forEach(b => b.classList.toggle('on', b.dataset.mode === m));
  if (m === 'tabs') renderRfqTabs();
}

function isoDate(y, m, d) { return `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`; }
function prettyISO(v, withYear = true) {
  return new Date(v + 'T00:00:00').toLocaleDateString('en-GB', { day: '2-digit', month: 'short', ...(withYear ? { year: 'numeric' } : {}) });
}
function renderCalendar() {
  const grid = $('calendarGrid'), title = $('calTitle');
  if (!grid || !title) return;
  const first = new Date(cal.year, cal.month, 1), days = new Date(cal.year, cal.month + 1, 0).getDate();
  const lead = (first.getDay() + 6) % 7, cells = [];
  title.textContent = first.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
  for (let i = 0; i < lead; i++) cells.push('<span></span>');
  for (let d = 1; d <= days; d++) {
    const iso = isoDate(cal.year, cal.month, d), past = iso < todayIso;
    let cls = 'calDay';
    if (cal.start && iso === cal.start) cls += ' start';
    else if (cal.end && iso === cal.end) cls += ' end';
    else if (cal.start && cal.end && iso > cal.start && iso < cal.end) cls += ' range';
    if (past) cls += ' muted';
    if (iso === todayIso) cls += ' today';
    cells.push(`<button type="button" class="${cls}" data-caldate="${iso}" ${past ? 'disabled' : ''}>${d}</button>`);
  }
  grid.innerHTML = cells.join('');
  const ds = $('dateSummary');
  if (cal.start && cal.end) {
    const n = Math.round((new Date(cal.end + 'T00:00:00') - new Date(cal.start + 'T00:00:00')) / 86400000) + 1;
    ds.textContent = `${prettyISO(cal.start, false)} → ${prettyISO(cal.end, true)} · ${n} Days / ${n - 1} Nights`;
  } else if (cal.start) ds.textContent = `Start: ${prettyISO(cal.start, true)} · now choose end date`;
  else ds.textContent = 'No travel dates selected yet';
}
function pickDate(iso) {
  if (!cal.start || !cal.pickingEnd) {
    cal.start = iso; cal.end = ''; cal.pickingEnd = true;
    // The chosen start date defines the travel month.
    rfq.travelYear = +iso.slice(0, 4);
    rfq.travelMonth = monthNames[+iso.slice(5, 7) - 1];
    rfq.ai.delete('year'); rfq.ai.delete('month');
  } else {
    if (iso < cal.start) { cal.end = cal.start; cal.start = iso; } else cal.end = iso;
    cal.pickingEnd = false;
    rfq.ai.delete('duration');
  }
  renderRfqTabs();
}

function toModel() {
  const m = { ...rfq, naturalText: $('natural').value, foodPrefs: rfq.food.join(','), aiFields: [...rfq.ai].join(','), startDate: cal.start || null, endDate: cal.end || null };
  delete m.food; delete m.ai;
  return m;
}

let saving = false;
async function save(publish) {
  if (saving) return;
  saving = true;
  const r = await api('/Rfq/Save', { rfq: toModel(), publish });
  saving = false;
  if (!r.ok) { toast(r.message); return; }
  if (publish) { toastNext(r.message); location.href = '/Agent'; return; }
  rfq.rfqId = r.data.id;
  history.replaceState(null, '', '/Rfq/Create/' + rfq.rfqId);
  toast(r.message);
}

// Copilot field key → RFQ property it fills.
const structured = { destination: 'destination', year: 'travelYear', month: 'travelMonth', adults: 'adults', children: 'children', duration: 'nights', hotel: 'hotelCategory', meal: 'mealPlan', vehicle: 'vehicle', food: 'foodPrefs', budget: 'budget', stops: 'stops', cabin: 'cabin', priorities: 'priorities' };

async function structure() {
  const r = await call('/Rfq/Structure', { text: $('natural').value });
  if (!r.ok) return;
  const found = r.data.aiFields ? r.data.aiFields.split(',') : [];
  found.forEach(k => { rfq[structured[k]] = r.data[structured[k]]; rfq.ai.add(k); });
  if (found.includes('food')) rfq.food = rfq.foodPrefs.split(',');
  if (found.includes('month')) {
    cal.year = rfq.travelYear; cal.month = monthNames.indexOf(rfq.travelMonth);
    if (cal.start && !cal.start.startsWith(isoDate(cal.year, cal.month, 1).slice(0, 7))) { cal.start = ''; cal.end = ''; cal.pickingEnd = false; }
  }
  renderExtracts();
  const flights = found.includes('stops') || found.includes('cabin');
  $('aiReply').textContent = found.length
    ? `Structured successfully — ${found.length} field${found.length > 1 ? 's' : ''} extracted.${flights ? '' : ' Flights were not specified, so I left those fields blank.'}${cal.start ? '' : ' Pick the exact travel dates in the Travel tab.'}`
    : 'I could not find structured details in that description. Use the tabs to build the RFQ.';
  setTimeout(() => setMode('tabs'), 450);
}

function bindVoice() {
  const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  let rec = null;
  $('voice').onclick = () => {
    if (!Recognition) { toast('Voice input is not supported in this browser'); return; }
    if (rec) { rec.stop(); return; }
    rec = new Recognition();
    rec.lang = 'en-IN';
    rec.onresult = e => { const t = $('natural'); t.value = (t.value.trim() + ' ' + e.results[0][0].transcript).trim(); toast('Voice transcribed into the same RFQ ✓'); };
    rec.onerror = () => toast('Could not capture voice — check microphone permission');
    rec.onend = () => { rec = null; $('voice').textContent = '🎙 Voice'; };
    $('voice').textContent = '■ Listening…';
    rec.start();
  };
}

function bind() {
  $$('[data-mode]').forEach(b => b.onclick = () => setMode(b.dataset.mode));
  $('structure').onclick = structure;
  bindVoice();
  $('rfqTabs').onclick = e => { const b = e.target.closest('[data-rfqtab]'); if (b) { app.rfqStep = +b.dataset.rfqtab; renderRfqTabs(); } };
  $('prev').onclick = () => { if (app.rfqStep > 0) { app.rfqStep--; renderRfqTabs(); } };
  $('next').onclick = () => { if (app.rfqStep < 5) { app.rfqStep++; renderRfqTabs(); } else toast('RFQ reviewed ✓ — publish when ready'); };
  $('saveDraft').onclick = () => save(false);
  $('publish').onclick = () => save(true);
  $('rfqPanel').oninput = e => { const k = e.target.dataset.rfield; if (k) { rfq[k] = e.target.value; rfq.ai.delete(aiKey(k)); } };
  $('rfqPanel').onclick = e => {
    if (e.target.dataset.year) { rfq.travelYear = +e.target.dataset.year; rfq.ai.delete('year'); cal.year = rfq.travelYear; cal.start = ''; cal.end = ''; cal.pickingEnd = false; renderRfqTabs(); return; }
    if (e.target.dataset.month !== undefined) { const mi = +e.target.dataset.month; rfq.travelMonth = monthNames[mi]; rfq.ai.delete('month'); cal.month = mi; cal.year = rfq.travelYear; cal.start = ''; cal.end = ''; cal.pickingEnd = false; renderRfqTabs(); return; }
    const count = e.target.closest('[data-count]');
    if (count) { const k = count.dataset.count; rfq[k] = Math.min(k === 'adults' ? 99 : 20, Math.max(k === 'adults' ? 1 : 0, (+rfq[k] || 0) + +count.dataset.delta)); rfq.ai.delete(k); renderRfqTabs(); return; }
    const c = e.target.closest('[data-choice]');
    if (c) {
      const k = c.dataset.choice, v = c.dataset.val;
      if (k === 'foodPrefs') rfq.food = rfq.food.includes(v) ? rfq.food.filter(x => x !== v) : [...rfq.food, v];
      else rfq[k] = rfq[k] === v ? '' : v;
      rfq.ai.delete(aiKey(k)); renderRfqTabs(); return;
    }
    const d = e.target.closest('[data-caldate]');
    if (d) { pickDate(d.dataset.caldate); return; }
    if (e.target.closest('#calPrev')) { cal.month--; if (cal.month < 0) { cal.month = 11; cal.year--; } renderCalendar(); return; }
    if (e.target.closest('#calNext')) { cal.month++; if (cal.month > 11) { cal.month = 0; cal.year++; } renderCalendar(); }
  };
}

if (rfq.ai.size) renderExtracts();
renderRfqTabs();
bind();
if (rfq.rfqId && !rfq.naturalText) setMode('tabs');
