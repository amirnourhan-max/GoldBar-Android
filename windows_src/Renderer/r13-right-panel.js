(() => {
  'use strict';

  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];
  let initialized = false;

  function normalizeDigits(value) {
    const fa = '۰۱۲۳۴۵۶۷۸۹';
    const ar = '٠١٢٣٤٥٦٧٨٩';
    return String(value ?? '')
      .replace(/[۰-۹]/g, d => String(fa.indexOf(d)))
      .replace(/[٠-٩]/g, d => String(ar.indexOf(d)))
      .replace(/,/g, '.');
  }

  function sanitize(input) {
    let v = normalizeDigits(input.value).replace(/[^0-9.]/g, '');
    const dot = v.indexOf('.');
    if (dot >= 0) v = v.slice(0, dot + 1) + v.slice(dot + 1).replace(/\./g, '');
    input.value = v;
  }

  function num(value) {
    const n = Number(normalizeDigits(value));
    return Number.isFinite(n) ? n : NaN;
  }

  function fmt(value, digits = 3) {
    const n = Number(value);
    if (!Number.isFinite(n)) return '0';
    return n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: digits });
  }

  function installStyles() {
    if ($('#goldbarR13RightStyles')) return;
    const style = document.createElement('style');
    style.id = 'goldbarR13RightStyles';
    style.textContent = `
      .settings{overflow:hidden!important;display:flex!important;flex-direction:column!important}
      .r13-side-tabs{display:grid;grid-template-columns:1fr 1fr;gap:7px;margin:0 0 10px;direction:rtl;flex:0 0 auto}
      .r13-side-tab{height:36px;border:1px solid rgba(255,255,255,.08);background:#0d1012;color:#9da4af;border-radius:9px;font:900 11px Tahoma,"Segoe UI",Arial,sans-serif;cursor:pointer;transition:.15s}
      .r13-side-tab.active{color:#f2c45b;border-color:rgba(242,196,91,.48);background:rgba(242,196,91,.075);box-shadow:inset 0 0 18px rgba(242,196,91,.025)}
      .r13-side-pane{min-height:0;flex:1 1 auto;overflow-y:auto;overflow-x:hidden;padding-inline-end:3px;scrollbar-gutter:stable}
      .r13-side-pane[hidden]{display:none!important}
      .r13-side-pane::-webkit-scrollbar{width:6px}.r13-side-pane::-webkit-scrollbar-track{background:rgba(255,255,255,.02);border-radius:8px}.r13-side-pane::-webkit-scrollbar-thumb{background:rgba(242,196,91,.30);border-radius:8px}
      .r13-quick-side{direction:rtl;padding:1px 1px 8px}
      .r13-q-card{border:1px solid rgba(255,255,255,.075);background:rgba(255,255,255,.025);border-radius:11px;padding:11px;margin-bottom:9px}
      .r13-q-card.gold{border-color:rgba(242,196,91,.28);background:linear-gradient(145deg,rgba(242,196,91,.055),rgba(255,255,255,.018))}
      .r13-q-title{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:9px;color:#f2c45b;font-size:13px;font-weight:900}
      .r13-q-title small{font-size:9px;color:#79818d;font-weight:800}
      .r13-q-grid{display:grid;grid-template-columns:1fr 1fr;gap:7px}
      .r13-q-field label{display:block;color:#aab1bc;font-size:9px;font-weight:900;margin:0 0 4px}
      .r13-q-field input{width:100%;height:34px;box-sizing:border-box;border:1px solid #363b3e;background:#0b0e10;color:#f4f1e9;border-radius:8px;padding:0 7px;font:900 11px Tahoma,"Segoe UI",Arial,sans-serif;text-align:center;direction:ltr;outline:none}
      .r13-q-field input:focus{border-color:rgba(242,196,91,.62);box-shadow:0 0 0 2px rgba(242,196,91,.06)}
      .r13-q-result-row{display:grid;grid-template-columns:1fr 1fr;gap:7px;margin-top:8px}
      .r13-q-result{background:#0b0e10;border:1px solid rgba(255,255,255,.055);border-radius:8px;padding:7px;text-align:center}
      .r13-q-result span{display:block;color:#858d99;font-size:8px;font-weight:800;margin-bottom:3px}.r13-q-result b{display:block;color:#f2c45b;font-size:12px;font-weight:900;direction:ltr}
      .r13-q-note{color:#68717d;font-size:8.5px;font-weight:800;line-height:1.55;margin-top:7px}
      .r13-q-open{width:100%;height:36px;border:1px solid rgba(242,196,91,.34);background:rgba(242,196,91,.07);color:#f2c45b;border-radius:9px;font:900 11px Tahoma,"Segoe UI",Arial,sans-serif;cursor:pointer;margin-top:1px}
      .r13-q-open:hover{background:rgba(242,196,91,.12)}
      @media(max-height:760px){.r13-side-tabs{margin-bottom:7px}.r13-q-card{padding:9px;margin-bottom:7px}.r13-q-field input{height:31px}}
    `;
    document.head.appendChild(style);
  }

  function buildQuickPane() {
    const pane = document.createElement('div');
    pane.id = 'r13QuickSidePane';
    pane.className = 'r13-side-pane r13-quick-side';
    pane.hidden = true;
    pane.innerHTML = `
      <section class="r13-q-card gold">
        <div class="r13-q-title"><span>هزینه عیار</span><small>مرحله اول</small></div>
        <div class="r13-q-grid">
          <div class="r13-q-field"><label>مظنه طلا</label><input id="r13SideGoldQuote" inputmode="decimal" placeholder="0"></div>
          <div class="r13-q-field"><label>مظنه نقره</label><input id="r13SideSilverQuote" inputmode="decimal" placeholder="0"></div>
          <div class="r13-q-field"><label>درصد فرق شمش</label><input id="r13SideBarDiff" inputmode="decimal" placeholder="0"></div>
          <div class="r13-q-field"><label>قیمت بار</label><input id="r13SideAlloyPrice" inputmode="decimal" placeholder="0"></div>
        </div>
        <div class="r13-q-note">فعلاً فقط ورودی‌ها ساخته شده‌اند؛ فرمول هزینه را بعد از تأیید شما اضافه می‌کنیم.</div>
      </section>

      <section class="r13-q-card">
        <div class="r13-q-title"><span>طلای 995 / طلای 750</span><small>تقسیم سریع</small></div>
        <div class="r13-q-field"><label>عدد پایه</label><input id="r13SideSplitBase" inputmode="decimal" value="800"></div>
        <div class="r13-q-grid" style="margin-top:7px">
          <div class="r13-q-field"><label>درصد طلای 995</label><input id="r13SidePct995" inputmode="decimal" value="36.79"></div>
          <div class="r13-q-field"><label>درصد طلای 750</label><input id="r13SidePct750" inputmode="decimal" value="63.21"></div>
        </div>
        <div class="r13-q-result-row">
          <div class="r13-q-result"><span>طلای 995</span><b id="r13SideGold995">0</b></div>
          <div class="r13-q-result"><span>طلای 750</span><b id="r13SideGold750">0</b></div>
        </div>
      </section>

      <section class="r13-q-card">
        <div class="r13-q-title"><span>اصلاح وزن برای افت</span><small>محاسبه سریع</small></div>
        <div class="r13-q-grid">
          <div class="r13-q-field"><label>وزن پایه</label><input id="r13SideCorrWeight" inputmode="decimal" value="250"></div>
          <div class="r13-q-field"><label>عیار پایه</label><input id="r13SideCorrAssay" inputmode="decimal" value="750"></div>
          <div class="r13-q-field"><label>افت عیار</label><input id="r13SideCorrDrop" inputmode="decimal" value="1"></div>
          <div class="r13-q-result"><span>بار افزوده (g)</span><b id="r13SideCorrAdd">0</b></div>
        </div>
        <div class="r13-q-result-row"><div class="r13-q-result" style="grid-column:1/-1"><span>جمع وزن (g)</span><b id="r13SideCorrTotal">0</b></div></div>
      </section>
      <button class="r13-q-open" id="r13OpenFullQuick">باز کردن صفحه کامل محاسبه سریع</button>`;
    return pane;
  }

  function recalc() {
    const engine = window.__goldbarAssayEngineV2 || window.__goldbarFormulaEngine;

    const base = num($('#r13SideSplitBase')?.value);
    const p995 = num($('#r13SidePct995')?.value);
    const p750 = num($('#r13SidePct750')?.value);
    let g995 = NaN, g750 = NaN;
    if (engine?.splitByPercent) {
      const r = engine.splitByPercent(base, p995, p750);
      g995 = r?.gold995; g750 = r?.gold750;
    } else if (Number.isFinite(base) && Number.isFinite(p995) && Number.isFinite(p750)) {
      g995 = base * p995 / 100;
      g750 = base * p750 / 100;
    }
    if ($('#r13SideGold995')) $('#r13SideGold995').textContent = fmt(g995, 3);
    if ($('#r13SideGold750')) $('#r13SideGold750').textContent = fmt(g750, 3);

    const w = num($('#r13SideCorrWeight')?.value);
    const a = num($('#r13SideCorrAssay')?.value);
    const d = num($('#r13SideCorrDrop')?.value);
    let add = NaN;
    if (engine?.correctionAddition) add = engine.correctionAddition(w, a, d);
    else if (Number.isFinite(w) && Number.isFinite(a) && Number.isFinite(d) && a !== d) add = w * a / (a - d) - w;
    if ($('#r13SideCorrAdd')) $('#r13SideCorrAdd').textContent = fmt(add, 3);
    if ($('#r13SideCorrTotal')) $('#r13SideCorrTotal').textContent = fmt(Number.isFinite(add) && Number.isFinite(w) ? w + add : NaN, 3);
  }

  function bindPane(pane) {
    pane.querySelectorAll('input').forEach(input => {
      input.addEventListener('input', () => { sanitize(input); recalc(); });
      input.addEventListener('paste', () => setTimeout(() => { sanitize(input); recalc(); }, 0));
      input.addEventListener('drop', e => e.preventDefault());
    });
    $('#r13OpenFullQuick')?.addEventListener('click', () => {
      const nav = $$('.nav-item').find(btn => btn.querySelector('span')?.textContent?.trim() === 'محاسبه سریع');
      nav?.click();
    });
    recalc();
  }

  function setTab(which) {
    const scale = $('#r13ScaleSidePane');
    const quick = $('#r13QuickSidePane');
    const scaleBtn = $('#r13TabScale');
    const quickBtn = $('#r13TabQuick');
    if (!scale || !quick) return;
    const isQuick = which === 'quick';
    scale.hidden = isQuick;
    quick.hidden = !isQuick;
    scaleBtn?.classList.toggle('active', !isQuick);
    quickBtn?.classList.toggle('active', isQuick);
    if (isQuick) recalc();
  }

  function install() {
    if (initialized) return true;
    const settings = $('.settings');
    const head = settings?.querySelector('.settings-head');
    if (!settings || !head) return false;
    installStyles();

    const tabs = document.createElement('div');
    tabs.className = 'r13-side-tabs';
    tabs.id = 'r13SideTabs';
    tabs.innerHTML = '<button id="r13TabScale" class="r13-side-tab active">تنظیمات ترازو</button><button id="r13TabQuick" class="r13-side-tab">محاسبه سریع</button>';
    head.insertAdjacentElement('afterend', tabs);

    const scalePane = document.createElement('div');
    scalePane.id = 'r13ScaleSidePane';
    scalePane.className = 'r13-side-pane';
    let node = tabs.nextSibling;
    const toMove = [];
    while (node) { toMove.push(node); node = node.nextSibling; }
    toMove.forEach(n => scalePane.appendChild(n));
    settings.appendChild(scalePane);

    const quickPane = buildQuickPane();
    settings.appendChild(quickPane);
    bindPane(quickPane);

    $('#r13TabScale')?.addEventListener('click', () => setTab('scale'));
    $('#r13TabQuick')?.addEventListener('click', () => setTab('quick'));
    initialized = true;

    window.__goldbarR13RightProbe = () => {
      const rect = settings.getBoundingClientRect();
      const tabsRect = tabs.getBoundingClientRect();
      setTab('quick');
      const paneRect = quickPane.getBoundingClientRect();
      const fits = tabsRect.left >= rect.left - 2 && tabsRect.right <= rect.right + 2 &&
        paneRect.left >= rect.left - 2 && paneRect.right <= rect.right + 2 &&
        paneRect.bottom <= rect.bottom + 2;
      setTab('scale');
      return { ok: fits && Boolean($('#r13SideGold995')) && Boolean($('#r13SideCorrAdd')), fits, settingsWidth: rect.width, paneWidth: paneRect.width };
    };
    return true;
  }

  function init(attempt = 0) {
    if (install()) return;
    if (attempt < 60) setTimeout(() => init(attempt + 1), 100);
  }
  init();
})();
