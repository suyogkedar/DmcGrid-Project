'use strict';
// DMC quote builder: package options, each with a day-wise, time-wise itinerary.
const model = window.builderModel;
let packages = model.packages;
const app = { pkg: 0, day: 0 };
let dmcItinEditing = false;

const basics = [['name', 'Option name', 150], ['netPrice', 'Net price', 0], ['hotel', 'Hotel', 200], ['room', 'Room', 100], ['meal', 'Meal plan', 100], ['vehicle', 'Vehicle', 100], ['food', 'Food', 200], ['statusNote', 'Note to agent', 300]];

function newActivity(p) { return { from: '10:00', to: '12:00', city: '', desc: '', transfer: p.vehicle, inc: 'Yes', notes: '' }; }

function renderBuilder() {
  const p = packages[app.pkg];
  if (app.day >= p.days.length) app.day = 0;
  const ro = dmcItinEditing ? '' : 'readonly';

  $('pkgTabs').innerHTML = packages.map((x, i) => `<button class="pkgTab ${i === app.pkg ? 'on' : ''}" data-pkg="${i}">${esc(x.name)}${x.versionNo > 1 ? ` · v${x.versionNo}` : ''}</button>`).join('');
  $('pkgBasics').innerHTML = '<span class="eyebrow">PACKAGE OPTION</span>' + basics.map(([k, label, max]) =>
    `<div class="field"><label>${label}</label><input data-pfield="${k}" ${k === 'netPrice' ? 'type="number" min="0" inputmode="numeric"' : `maxlength="${max}"`} value="${esc(k === 'netPrice' && !p[k] ? '' : p[k])}"></div>`).join('');
  $('dayTabs').innerHTML = p.days.map((d, i) => `<button class="dayTab ${i === app.day ? 'on' : ''}" data-day="${i}">Day ${i + 1}</button>`).join('');

  const acts = p.days[app.day];
  $('activityArea').innerHTML = `<div class="activityWrap"><table class="activity"><thead><tr><th>FROM</th><th>TO</th><th>CITY</th><th>ACTIVITY / DESCRIPTION</th><th>TRANSFER</th><th>INCLUDED</th><th>NOTES</th><th></th></tr></thead><tbody>${acts.map((a, i) => `<tr><td><input ${ro} maxlength="5" data-af="${i}:from" value="${esc(a.from)}"></td><td><input ${ro} maxlength="5" data-af="${i}:to" value="${esc(a.to)}"></td><td><input ${ro} maxlength="100" data-af="${i}:city" value="${esc(a.city)}"></td><td><input ${ro} maxlength="500" class="desc" data-af="${i}:desc" value="${esc(a.desc)}"></td><td><input ${ro} maxlength="100" data-af="${i}:transfer" value="${esc(a.transfer)}"></td><td><select ${dmcItinEditing ? '' : 'disabled'} data-af="${i}:inc"><option ${a.inc === 'Yes' ? 'selected' : ''}>Yes</option><option ${a.inc === 'No' ? 'selected' : ''}>No</option></select></td><td><input ${ro} maxlength="300" data-af="${i}:notes" value="${esc(a.notes)}"></td><td>${dmcItinEditing ? `<button class="btn" data-delact="${i}">🗑</button>` : ''}</td></tr>`).join('')}</tbody></table></div>${acts.length ? '' : '<div class="sub" style="margin-top:7px">No activities for this day yet.</div>'}${dmcItinEditing ? '<button class="btn" id="addAct" style="margin-top:7px">+ Add Activity / Time Slot</button>' : ''}`;

  $('editDmcItin').style.display = dmcItinEditing ? 'none' : '';
  $('saveDmcItin').style.display = dmcItinEditing ? '' : 'none';
  $('dmcEditHint').textContent = dmcItinEditing ? 'Editing ON · change any field, add or delete a line, then Save Day.' : 'Tap Edit Day Itinerary to change time, city, activity, transfer, inclusions or notes.';
}

function clean() {
  return packages.map(p => ({ ...p, netPrice: +p.netPrice || 0 }));
}

let busy = false;
async function save(submit) {
  if (busy) return false;
  busy = true;
  const r = await call('/Quote/Save', { rfqId: model.rfqId, packages: clean(), submit });
  busy = false;
  if (!r.ok) return false;
  packages = r.data;
  app.pkg = Math.min(app.pkg, packages.length - 1);
  return true;
}

function addPackage(p) {
  packages.push(p);
  app.pkg = packages.length - 1;
  app.day = 0;
  dmcItinEditing = false;
  renderBuilder();
}

function bind() {
  $('pkgTabs').onclick = e => { const b = e.target.closest('[data-pkg]'); if (b) { app.pkg = +b.dataset.pkg; app.day = 0; dmcItinEditing = false; renderBuilder(); } };
  $('dayTabs').onclick = e => { const b = e.target.closest('[data-day]'); if (b) { app.day = +b.dataset.day; renderBuilder(); } };
  $('pkgBasics').oninput = e => {
    const k = e.target.dataset.pfield;
    if (!k) return;
    packages[app.pkg][k] = e.target.value;
    if (k === 'name') $('pkgTabs').querySelector('.pkgTab.on').textContent = e.target.value;
  };
  $('activityArea').oninput = e => { if (e.target.dataset.af) { const [i, k] = e.target.dataset.af.split(':'); packages[app.pkg].days[app.day][+i][k] = e.target.value; } };
  $('activityArea').onchange = $('activityArea').oninput;
  $('activityArea').onclick = e => {
    if (e.target.closest('#addAct')) { const p = packages[app.pkg]; p.days[app.day].push(newActivity(p)); renderBuilder(); return; }
    const b = e.target.closest('[data-delact]');
    if (b) { packages[app.pkg].days[app.day].splice(+b.dataset.delact, 1); renderBuilder(); }
  };
  $('editDmcItin').onclick = () => { dmcItinEditing = true; renderBuilder(); toast('DMC itinerary editing enabled ✓'); };
  $('saveDmcItin').onclick = async () => { if (await save(false)) { dmcItinEditing = false; renderBuilder(); } };
  $('addDay').onclick = () => { const p = packages[app.pkg]; p.days.push([]); app.day = p.days.length - 1; renderBuilder(); };
  $('removePkg').onclick = () => {
    if (packages.length <= 1) { toast('At least one package option is required'); return; }
    if (model.connected && packages[app.pkg].packageId) { toast('Submitted versions are retained after the agent connects'); return; }
    packages.splice(app.pkg, 1); app.pkg = Math.max(0, app.pkg - 1); app.day = 0; renderBuilder(); toast('Package option removed · Submit Quote to apply');
  };
  $('addPkg').onclick = () => { addPackage({ packageId: 0, name: `Option ${packages.length + 1} · New Package`, netPrice: 0, hotel: '4★', room: 'Deluxe', meal: 'Breakfast', vehicle: 'Private', food: '', statusNote: '', versionNo: 1, days: [[]] }); toast('Package option added ✓'); };
  $('aiQuote').onclick = async () => { const r = await call('/Quote/Draft', { rfqId: model.rfqId }); if (r.ok) addPackage({ ...r.data, name: `Option ${packages.length + 1} · ${r.data.name}` }); };
  $('submitQuote').onclick = async () => { if (await save(true)) { toastNext(`${packages.length} package option${packages.length > 1 ? 's' : ''} submitted ✓`); location.href = '/Dmc'; } };

  $('reviseConnected').onclick = () => {
    const p = packages[app.pkg];
    if (!p.packageId) { toast('Submit this option first — only submitted options can be revised'); return; }
    addPackage({ ...structuredClone(p), packageId: 0, parentPackageId: p.packageId, versionNo: p.versionNo + 1, name: p.name.replace(/ Revised$/, '') + ' Revised', statusNote: '' });
    toast('Revision started — original quote version will be retained ✓');
  };
  $('newConnectedOption').onclick = () => { $('addPkg').click(); toast('New itinerary option added for connected agent ✓'); };

  // Quotation document import
  const file = $('docFile'), read = $('readDoc'), status = $('docStatus');
  $('chooseDoc').onclick = () => file.click();
  file.onchange = () => {
    const f = file.files[0];
    if (!f) return;
    $('docName').textContent = f.name;
    read.disabled = false;
    status.className = 'docStatus show';
    status.innerHTML = `Ready to read <b>${esc(f.name)}</b>. AI will map its content to DMCGrid fields.`;
  };
  read.onclick = async () => {
    const f = file.files[0];
    if (!f) return;
    read.disabled = true;
    status.innerHTML = '<b>AI reading quotation…</b> Identifying package, price, hotels, transfers, inclusions and itinerary.';
    const form = new FormData();
    form.append('rfqId', model.rfqId);
    form.append('file', f);
    const r = await api('/Quote/Import', form);
    read.disabled = false;
    if (!r.ok) { status.textContent = r.message; return; }
    const current = packages[app.pkg], imported = r.data.package;
    packages[app.pkg] = { ...imported, packageId: current.packageId, parentPackageId: current.parentPackageId, versionNo: current.versionNo, statusNote: current.statusNote };
    app.day = 0;
    renderBuilder();
    status.textContent = r.data.message;
    $('docExtract').className = 'docExtract show';
    $('docExtract').innerHTML = `<div><small>CONFIDENCE</small><b>${esc(r.data.confidence)}</b></div><div><small>PACKAGE</small><b>${esc(imported.name)}</b></div><div><small>PRICE FOUND</small><b>${imported.netPrice ? money(imported.netPrice) : 'Not found'}</b></div><div><small>ITINERARY</small><b>${imported.days.length} days ${r.data.mapped ? 'mapped' : 'drafted'}</b></div>`;
    toast(r.data.mapped ? 'DMC document mapped into itinerary builder ✓' : 'Draft prepared — review every field');
  };
}

renderBuilder();
bind();
