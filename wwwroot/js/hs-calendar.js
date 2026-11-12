(() => {
 'use strict';
 const read = id => JSON.parse(document.getElementById(id).textContent);
 const events = read('hsEvents'), alerts = read('hsAlerts'), expiries = read('hsExpiries');
 const drawer = document.getElementById('hsDayDrawer'), body = document.getElementById('hsDayBody');
 const title = document.getElementById('hsDayTitle');
 const groups = ['Capacitaciones programadas','Capacitaciones próximas a vencer','Capacitaciones vencidas','Inspecciones y auditorías','Otras actividades H&S','Vencimientos vigentes'];
 const date = value => new Date(value.slice(0,10) + 'T12:00:00').toLocaleDateString('es-NI');
 function node(tag, text, cls) { const n = document.createElement(tag); if (text !== undefined) n.textContent = text; if (cls) n.className = cls; return n; }
 function show() { bootstrap.Offcanvas.getOrCreateInstance(drawer).show(); }
 function card(x, showDriver = true) {
  const el = node('article', undefined, 'hs-detail');
  el.append(node('div', x.Title, 'hs-detail-title'));
  if (showDriver && x.Driver) el.append(node('div', x.Driver + (x.Code ? ' · ' + x.Code : ''), 'hs-detail-person'));
  el.append(node('span', x.UrgencyLabel, 'hs-detail-urgency hs-tone-' + x.Tone));
  const dl = node('dl');
  [['Realización',x.Performed],['Vencimiento',x.Days !== null ? date(x.Date) : null],['Horario',x.AllDay ? 'Todo el día' : x.Date.slice(11,16)],['Estado',x.StatusLabel],['Sede',x.Site]].forEach(([label,value]) => {
   if (value !== null && value !== undefined && value !== '') dl.append(node('dt',label),node('dd',String(value)));
  });
  el.append(dl); const actions = node('div',undefined,'hs-detail-actions');
  if (x.Url) { const a=node('a','Consultar registro','btn btn-sm btn-outline-primary'); a.href=x.Url; actions.append(a); }
  if (x.Days !== null && drawer.dataset.canEdit === 'true') { const a=node('a','Actualizar documentos','btn btn-sm btn-primary'); a.href='/Operaciones/ControlDocumentos'; actions.append(a); }
  el.append(actions); return el;
 }
 function list(heading, rows) {
  title.textContent=heading; body.replaceChildren();
  body.append(node('p',rows.length+' registros · Agrupados por conductor','hs-drawer-note'));
  if (!rows.length) body.append(node('p','Sin registros para los filtros seleccionados.','hs-empty-panel'));
  const byDriver = new Map();
  const unassigned = [];
  for (const event of rows.slice().sort((a, b) => a.Date.localeCompare(b.Date))) {
   if (!event.Driver) {
    unassigned.push(event);
    continue;
   }
   const key = event.DriverId ? 'id:' + event.DriverId : event.Code ? 'code:' + event.Code : 'name:' + event.Driver;
   if (!byDriver.has(key)) byDriver.set(key, []);
   byDriver.get(key).push(event);
  }
  for (const items of byDriver.values()) {
   const { Driver, Code } = items[0];
   const group = node('details', undefined, 'hs-driver-group border rounded bg-white p-2 mb-2');
   const summary = node('summary', Driver + (Code ? ' · ' + Code : ''), 'fw-semibold small');
   summary.append(node('span', String(items.length), 'badge rounded-pill text-bg-primary ms-2'));
   const records = node('div', undefined, 'mt-2');
   for (const event of items) records.append(card(event, false));
   group.append(summary, records);
   body.append(group);
  }
  unassigned.forEach(event => body.append(card(event)));
  show();
 }
 function dayDetails(day) {
  title.textContent=new Date(day+'T12:00:00').toLocaleDateString('es-NI',{weekday:'long',day:'numeric',month:'long',year:'numeric'});
  body.replaceChildren(); const rows=events.filter(x=>x.Date.slice(0,10)===day);
  if (!rows.length) body.append(node('p','No hay actividades ni vencimientos para esta fecha.','hs-empty-panel'));
  groups.forEach(group=>{const items=rows.filter(x=>x.Group===group);if(!items.length)return;body.append(node('h3',group+' · '+items.length));items.forEach(x=>body.append(card(x)));}); show();
 }
 document.getElementById('hsMonth').addEventListener('change', event => {
  if (event.target.value) window.location.assign(event.target.value);
 });
 document.querySelectorAll('[data-day]').forEach(b=>b.addEventListener('click',()=>dayDetails(b.dataset.day)));
 document.querySelectorAll('[data-alert-group]').forEach(b=>b.addEventListener('click',()=>{
  const group=b.dataset.alertGroup;
  const rows=alerts.filter(x=>group==='all'||(group==='expired'&&x.Days<=0)||(group==='urgent'&&x.Days>0&&x.Days<=15)||(group==='soon'&&x.Days>=16&&x.Days<=30));
  const names={all:'Todas las alertas de capacitación',expired:'Capacitaciones vencidas / hoy',urgent:'Vencen en 1–15 días',soon:'Vencen en 16–30 días'};
  list(names[group],rows);
 }));
 document.querySelectorAll('[data-kpi]').forEach(b=>b.addEventListener('click',()=>{
  const key=b.dataset.kpi;
  if (key==='drivers') { const field=document.getElementById('hsDriver');field.scrollIntoView({block:'center',behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'});field.focus({preventScroll:true});field.classList.add('hs-highlight');field.addEventListener('blur',()=>field.classList.remove('hs-highlight'),{once:true});return; }
  const rows=expiries.filter(x=>key==='valid'?x.Days>30:key==='due'?x.Days>0&&x.Days<=30:x.Days<=0);
  list(key==='valid'?'Capacitaciones vigentes':key==='due'?'Próximas a vencer · 30 días':'Capacitaciones vencidas / hoy',rows);
 }));
})();
