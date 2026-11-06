(()=>{
const events=JSON.parse(document.getElementById('hsEvents').textContent);
const drawer=document.getElementById('hsDayDrawer');
const groups=['Capacitaciones programadas','Capacitaciones próximas a vencer','Capacitaciones vencidas','Inspecciones y auditorías','Otras actividades H&S','Vencimientos vigentes'];
function node(tag,text,cls){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;}
document.querySelectorAll('[data-day]').forEach(b=>b.addEventListener('click',()=>{
 const day=b.dataset.day, body=document.getElementById('hsDayBody');body.replaceChildren();
 document.getElementById('hsDayTitle').textContent=new Date(day+'T12:00:00').toLocaleDateString('es-NI',{weekday:'long',day:'numeric',month:'long',year:'numeric'});
 const items=events.filter(x=>x.Date.slice(0,10)===day);
 groups.forEach(group=>{
  body.append(node('h5',group));const rows=items.filter(x=>x.Group===group);
  if(!rows.length){body.append(node('p','Sin registros.','text-muted small'));return;}
  rows.forEach(x=>{const card=node('article',undefined,'hs-detail hs-tone-'+x.Tone);card.append(node('strong',x.Title));const dl=node('dl');
   [['Conductor',x.Driver],['Código',x.Code],['Realización',x.Performed],['Vencimiento',x.Days!==null?new Date(x.Date.slice(0,10)+'T12:00:00').toLocaleDateString('es-NI'):null],['Días restantes',x.Days],['Horario',x.AllDay?'Todo el día':x.Date.slice(11,16)],['Estado',x.StatusLabel],['Sede',x.Site]].forEach(([label,value])=>{if(value===null||value===undefined||value==='')return;dl.append(node('dt',label),node('dd',String(value)));});
   card.append(dl);if(x.Url){const a=node('a','Consultar registro','btn btn-sm btn-outline-dark');a.href=x.Url;card.append(a);}if(x.Days!==null && drawer.dataset.canEdit==='true'){const edit=node('a','Actualizar documentos','btn btn-sm btn-primary ms-2');edit.href='/Operaciones/ControlDocumentos';card.append(edit);}body.append(card);
  });
 });bootstrap.Offcanvas.getOrCreateInstance(drawer).show();
}));
})();
