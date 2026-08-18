let backend=null;
let appState=null;
let toastTimer=null;
const $=id=>document.getElementById(id);
const asNum=v=>{const n=Number(String(v??'').replace(',','.'));return Number.isFinite(n)?n:null};
const fmt=(v,d=3)=>{if(v===null||v===undefined||!Number.isFinite(Number(v)))return '—';return Number(v).toFixed(d).replace(/\.0+$|(?<=\.[0-9]*?)0+$/,'').replace(/\.$/,'')};
const esc=s=>String(s??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));

function showToast(type,message){
  const t=$('toast'); t.className='toast '+(type||''); t.textContent=message||'';
  requestAnimationFrame(()=>t.classList.add('show'));
  clearTimeout(toastTimer); toastTimer=setTimeout(()=>t.classList.remove('show'),2600);
}

function parseResult(raw){try{return JSON.parse(raw)}catch{return {ok:false,message:'پاسخ نامعتبر از برنامه'}}}

function setPage(page){
  if(page==='settings'){openSettings();return}
  if(page==='register'){page='dashboard';setTimeout(()=>$('quickRegisterCard').scrollIntoView({behavior:'smooth',block:'start'}),80)}
  if(page==='calculations'){page='dashboard';setTimeout(()=>document.querySelector('.dashboard-lower').scrollIntoView({behavior:'smooth',block:'start'}),80)}
  document.querySelectorAll('.page').forEach(p=>p.classList.remove('active-page'));
  const el=$('page-'+page)||$('page-dashboard');el.classList.add('active-page');
  document.querySelectorAll('.nav-item').forEach(n=>n.classList.toggle('active',n.dataset.page===page));
  const map={dashboard:['داشبورد','نمای کلی وزن، عیار و عملیات پرکاربرد'],entries:['آبشده‌ها','مدیریت ثبت‌های انجام‌شده'],quick:['محاسبه سریع','تقسیم و اصلاح وزن'],reports:['گزارش‌ها','ذخیره خروجی تاریخ‌دار']};
  const m=map[page]||map.dashboard;$('pageTitle').textContent=m[0];$('pageSubtitle').textContent=m[1];
}

function openSettings(){
  if(appState)fillSettings(appState.settings||{});
  $('settingsDrawer').classList.add('open');
  document.querySelectorAll('.nav-item').forEach(n=>n.classList.toggle('active',n.dataset.page==='settings'));
}
function closeSettings(){$('settingsDrawer').classList.remove('open');setPage('dashboard')}

function renderState(state){
  appState=state;
  $('version').textContent=state.version||'1.6.0';
  const s=state.summary||{};
  $('metricWeight').textContent=fmt(s.weight,3);
  $('metricAssay').textContent=fmt(s.average,3);
  $('metricCount').textContent=String(s.count??0);
  $('metricAlloy').textContent=fmt(state.totalAlloy,3);
  const sc=state.scale||{};
  $('sidebarWeight').textContent=fmt(sc.weight,3);
  $('scaleDot').classList.toggle('ok',!!sc.connected);
  $('reportPath').textContent=(state.settings||{}).ReportFolder||'—';
  $('drawerReportPath').textContent=(state.settings||{}).ReportFolder||'—';
  renderRecent(state.recent||[]);
  renderEntries(state.entries||[]);
  recalcAll();
}

function renderRecent(items){
  const host=$('recentList');
  if(!items.length){host.innerHTML='<div class="empty">هنوز آبشده‌ای ثبت نشده است.</div>';return}
  host.innerHTML=items.map(e=>{
    let when='';try{when=e.CreatedAt?new Date(e.CreatedAt).toLocaleString('fa-IR',{month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit'}):''}catch{}
    return `<div class="recent-row"><div class="recent-main"><strong>${fmt(e.Weight,3)} g</strong><span>${fmt(e.Assay,0)} ‰</span></div><div class="recent-time">${esc(when)}</div></div>`
  }).join('');
}

function renderEntries(items){
  const host=$('entriesTable');
  if(!items.length){host.innerHTML='<div class="empty">هیچ آبشده‌ای ثبت نشده است.</div>';return}
  host.innerHTML=`<div class="entry-table-head"><span>#</span><span>وزن</span><span>عیار</span><span>توضیحات</span><span>عملیات</span></div>`+
  items.map((e,i)=>`<div class="entry-table-row"><span>${i+1}</span><strong>${fmt(e.Weight,3)} g</strong><strong>${fmt(e.Assay,0)} ‰</strong><span>${esc(e.Note||'—')}</span><button class="delete-row" data-delete="${i}">حذف</button></div>`).join('');
  host.querySelectorAll('[data-delete]').forEach(b=>b.addEventListener('click',()=>backend.deleteEntry(Number(b.dataset.delete),()=>{})));
}

function fillSettings(s){
  const set=(id,v)=>{if($(id))$(id).value=v??''};
  set('setPort',s.PortName||'COM1');set('setModel',s.ScaleModel||'A&D');set('setBaud',s.BaudRate||2400);set('setData',s.DataBits||7);set('setParity',s.Parity||'Even');set('setStop',s.StopBits||'Two');set('setFlow',s.Handshake||'None');
  $('setAuto').checked=!!s.AutoRead;$('setUp').checked=s.ReadOnUpArrow!==false;$('setPrint').checked=s.ReceivePrintKey!==false;$('setSendQuery').checked=s.SendQueryOnUpArrow!==false;
  set('setSamples',s.StableSampleCount||3);set('setTolerance',s.StableToleranceGrams??0.02);set('setQuery',s.QueryCommand||'Q');set('setEnding',s.QueryLineEnding||'CRLF');set('setTimeout',s.ReadTimeoutMs||1800);
  $('drawerReportPath').textContent=s.ReportFolder||'—';
}

function collectSettings(){return {
  PortName:$('setPort').value.trim()||'COM1',ScaleModel:$('setModel').value,BaudRate:Number($('setBaud').value)||2400,DataBits:Number($('setData').value)||7,Parity:$('setParity').value,StopBits:$('setStop').value,Handshake:$('setFlow').value,
  AutoRead:$('setAuto').checked,ReadOnUpArrow:$('setUp').checked,ReceivePrintKey:$('setPrint').checked,SendQueryOnUpArrow:$('setSendQuery').checked,StableAutoReadOnly:true,StableSampleCount:Number($('setSamples').value)||3,StableToleranceGrams:Number($('setTolerance').value)||0.02,QueryCommand:$('setQuery').value,QueryLineEnding:$('setEnding').value,ReadTimeoutMs:Number($('setTimeout').value)||1800,
  ReportFolder:(appState?.settings?.ReportFolder)||''
}}

function recalcAll(){recalcRaise();recalcLower();recalcSplit();recalcCorrection()}
function recalcRaise(){if(!backend)return;backend.calcRaise($('raiseTarget').value,$('barAssay').value,raw=>{const r=parseResult(raw);$('raiseDiff').textContent=fmt(r.difference,3);$('raiseNeed').textContent=fmt(r.required,3);$('raiseState').textContent=r.required===0?'افزایش عیار لازم نیست.':(r.required==null?'ابتدا آبشده معتبر ثبت کن.':`شمش مورد نیاز: ${fmt(r.required,3)} g`)})}
function recalcLower(){if(!backend)return;backend.calcLower($('lowerTarget').value,$('silverPercent').value,raw=>{const r=parseResult(raw);$('lowerTotal').textContent=fmt(r.total,3);$('lowerSilver').textContent=fmt(r.silver,3);$('lowerOther').textContent=fmt(r.other,3);$('lowerAfter').textContent=fmt(r.after,3)})}
function recalcSplit(){if(!backend)return;backend.calcSplit($('splitBase').value,raw=>{const r=parseResult(raw);$('splitA').textContent=fmt(r.a,3);$('splitB').textContent=fmt(r.b,3)})}
function recalcCorrection(){if(!backend)return;backend.calcCorrection($('corrWeight').value,$('corrTarget').value,$('corrDrop').value,raw=>{const r=parseResult(raw);$('corrAdd').textContent=fmt(r.add,3);$('corrTotal').textContent=fmt(r.total,3)})}

function resetEntry(){
  $('entryWeight').value='';$('entryAssay').value='750';$('entryNote').value='';$('entryWeight').focus();
}
function saveEntry(andNew=false){
  backend.saveEntry($('entryWeight').value,$('entryAssay').value,$('entryNote').value,raw=>{const r=parseResult(raw);showToast(r.ok?'success':'error',r.message||'');if(r.ok&&andNew)resetEntry();else if(r.ok)resetEntry()})
}

function bindUi(){
  document.querySelectorAll('.nav-item').forEach(b=>b.addEventListener('click',()=>setPage(b.dataset.page)));
  document.querySelectorAll('[data-nav]').forEach(b=>b.addEventListener('click',()=>setPage(b.dataset.nav)));
  $('closeSettings').addEventListener('click',closeSettings);
  $('instagram').addEventListener('click',()=>backend.openInstagram());
  $('readScale').addEventListener('click',()=>backend.requestWeight());$('sidebarRead').addEventListener('click',()=>backend.requestWeight());
  $('entryWeight').addEventListener('keydown',e=>{if(e.key==='ArrowUp'&&(appState?.settings?.ReadOnUpArrow!==false)){e.preventDefault();backend.requestWeight()}else if(e.key==='Enter'){e.preventDefault();$('entryAssay').focus();$('entryAssay').select()}});
  $('entryAssay').addEventListener('keydown',e=>{if(e.key==='Enter'){e.preventDefault();saveEntry(true)}});
  $('saveEntry').addEventListener('click',()=>saveEntry(true));$('resetEntry').addEventListener('click',()=>{saveEntry(true)});$('clearEntry').addEventListener('click',resetEntry);
  $('clearAllEntries').addEventListener('click',()=>{if(confirm('همه آبشده‌ها پاک شوند؟'))backend.clearEntries(()=>{})});
  ['raiseTarget','barAssay'].forEach(id=>$(id).addEventListener('input',recalcRaise));['lowerTarget','silverPercent'].forEach(id=>$(id).addEventListener('input',recalcLower));$('splitBase').addEventListener('input',recalcSplit);['corrWeight','corrTarget','corrDrop'].forEach(id=>$(id).addEventListener('input',recalcCorrection));
  $('saveSettings').addEventListener('click',()=>backend.saveSettings(JSON.stringify(collectSettings()),raw=>{const r=parseResult(raw);if(r.ok){showToast('success','تنظیمات ذخیره شد.');closeSettings()}else showToast('error',r.message||'ذخیره تنظیمات انجام نشد.')}));
  $('resetSettings').addEventListener('click',()=>{fillSettings({PortName:'COM1',ScaleModel:'A&D',BaudRate:2400,DataBits:7,Parity:'Even',StopBits:'Two',Handshake:'None',AutoRead:false,ReadOnUpArrow:true,ReceivePrintKey:true,SendQueryOnUpArrow:true,StableSampleCount:3,StableToleranceGrams:.02,QueryCommand:'Q',QueryLineEnding:'CRLF',ReadTimeoutMs:1800,ReportFolder:appState?.settings?.ReportFolder||''})});
  $('testScale').addEventListener('click',()=>{$('testStatus').textContent='در حال تست…';backend.testScale()});
  $('changeReportFolder').addEventListener('click',()=>backend.chooseReportFolder(path=>{if(path)showToast('success','مسیر گزارش تغییر کرد.')}));$('drawerChooseReport').addEventListener('click',()=>backend.chooseReportFolder(()=>{}));
  $('saveReport').addEventListener('click',()=>backend.saveReport(raw=>{const r=parseResult(raw);showToast(r.ok?'success':'error',r.ok?`گزارش ذخیره شد: ${r.path}`:(r.message||'خطا در ذخیره گزارش'))}));
}

new QWebChannel(qt.webChannelTransport,channel=>{
  backend=channel.objects.backend;
  bindUi();
  backend.stateChanged.connect(raw=>renderState(parseResult(raw)));
  backend.scaleChanged.connect(raw=>{const s=parseResult(raw);$('sidebarWeight').textContent=fmt(s.weight,3);$('scaleDot').classList.toggle('ok',!!s.connected);if(s.weight!==null&&s.weight!==undefined){$('entryWeight').value=fmt(s.weight,3)}$('testStatus').textContent=s.status||''});
  backend.toast.connect((type,msg)=>showToast(type,msg));
  backend.getState(raw=>renderState(parseResult(raw)));
});
