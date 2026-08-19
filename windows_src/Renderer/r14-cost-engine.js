(() => {
  'use strict';

  const MESGHAL_GRAMS = 4.3318;
  const ENTRY_KEY = 'goldbar.windows.entries.v2';
  const COST_STATE_KEY = 'goldbar.windows.r14.costState';
  const ASSAY_STATE_KEY = 'goldbar.windows.r14.assayContext';
  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];
  let observer = null;
  let wrappedProbe = false;
  let recalcWrapped = false;

  const fieldIds = {
    goldQuote: ['r13GoldQuote', 'r13SideGoldQuote'],
    silverQuote: ['r13SilverQuote', 'r13SideSilverQuote'],
    barDifference: ['r13BarDifference', 'r13SideBarDiff'],
    alloyPrice: ['r13AlloyPrice', 'r13SideAlloyPrice']
  };

  function normalizeDigits(value) {
    const fa = '۰۱۲۳۴۵۶۷۸۹';
    const ar = '٠١٢٣٤٥٦٧٨٩';
    return String(value ?? '')
      .replace(/[۰-۹]/g, d => String(fa.indexOf(d)))
      .replace(/[٠-٩]/g, d => String(ar.indexOf(d)));
  }

  function cleanNumeric(value) {
    let v = normalizeDigits(value).replace(/,/g, '').replace(/[^0-9.]/g, '');
    const dot = v.indexOf('.');
    if (dot >= 0) v = v.slice(0, dot + 1) + v.slice(dot + 1).replace(/\./g, '');
    return v;
  }

  function parseNumber(value) {
    const n = Number(cleanNumeric(value));
    return Number.isFinite(n) ? n : NaN;
  }

  function formatGrouped(value) {
    const clean = cleanNumeric(value);
    if (!clean) return '';
    const [intRaw, decRaw] = clean.split('.');
    const intPart = (intRaw || '0').replace(/^0+(?=\d)/, '');
    const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return decRaw !== undefined ? `${grouped}.${decRaw}` : grouped;
  }

  function formatNumber(value, digits = 3) {
    const n = Number(value);
    if (!Number.isFinite(n)) return '—';
    return n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: digits });
  }

  function readEntries() {
    try {
      const parsed = JSON.parse(localStorage.getItem(ENTRY_KEY) || '[]');
      return Array.isArray(parsed)
        ? parsed.filter(x => Number(x?.weight) > 0 && Number(x?.assay) > 0 && Number(x?.assay) <= 1000)
        : [];
    } catch { return []; }
  }

  function readCostState() {
    try {
      return { goldQuote:'', silverQuote:'', barDifference:'', alloyPrice:'', ...JSON.parse(sessionStorage.getItem(COST_STATE_KEY) || '{}') };
    } catch {
      return { goldQuote:'', silverQuote:'', barDifference:'', alloyPrice:'' };
    }
  }

  function writeCostState(state) {
    sessionStorage.setItem(COST_STATE_KEY, JSON.stringify(state));
  }

  function readAssayState() {
    try { return JSON.parse(sessionStorage.getItem(ASSAY_STATE_KEY) || '{}'); }
    catch { return {}; }
  }

  function writeAssayState(targetAssay, silverPercent) {
    const target = Number(targetAssay);
    const silver = Number(silverPercent);
    if (Number.isFinite(target) && target > 0 && Number.isFinite(silver) && silver >= 0) {
      sessionStorage.setItem(ASSAY_STATE_KEY, JSON.stringify({ targetAssay: target, silverPercent: silver }));
    }
  }

  function detectAssayContext() {
    const customTarget = parseNumber($('#r7AlloyTarget')?.value);
    const customSilver = parseNumber($('#r7SilverPercent')?.value);
    if (Number.isFinite(customTarget) && customTarget > 0 && Number.isFinite(customSilver)) {
      writeAssayState(customTarget, customSilver);
      return { targetAssay: customTarget, silverPercent: customSilver };
    }

    const card = $$('.calc-card')[1];
    const inputs = card ? [...card.querySelectorAll('input')] : [];
    const target = parseNumber(inputs[0]?.value);
    const silver = parseNumber(inputs[1]?.value);
    if (Number.isFinite(target) && target > 0 && Number.isFinite(silver)) {
      writeAssayState(target, silver);
      return { targetAssay: target, silverPercent: silver };
    }

    const saved = readAssayState();
    return {
      targetAssay: Number(saved.targetAssay),
      silverPercent: Number(saved.silverPercent)
    };
  }

  function calculateCost({ goldQuote, silverQuote, barDifference, alloyPrice, entries, targetAssay, silverPercent }) {
    const engine = window.__goldbarAssayEngineV2;
    const gq = Number(goldQuote);
    const sq = Number(silverQuote);
    const diff = Number(barDifference);
    const ap = Number(alloyPrice);
    const target = Number(targetAssay);
    const silverPct = Number(silverPercent);

    const invalid = {
      ok: false,
      goldPricePerGram: NaN,
      silverRequired: NaN,
      nonSilverRequired: NaN,
      highAssayWeight: NaN,
      silverEquivalentGold: NaN,
      barDifferenceGold: NaN,
      alloyEquivalentGold: NaN,
      totalGoldCost: NaN
    };

    if (!engine || !(gq > 0) || !Number.isFinite(sq) || !Number.isFinite(diff) || !Number.isFinite(ap) ||
        !(target > 0) || !Number.isFinite(silverPct)) return invalid;

    const list = Array.isArray(entries) ? entries : [];
    const summary = engine.summarize(list);
    if (!(summary.weight > 0) || !Number.isFinite(summary.averageAssay)) return invalid;

    const alloy = engine.alloyForTarget(summary, target, silverPct, summary.weight);
    if (!Number.isFinite(alloy.totalAlloyRequired)) return invalid;

    const goldPricePerGram = gq / MESGHAL_GRAMS;
    if (!(goldPricePerGram > 0)) return invalid;

    const silverRequired = Math.max(0, Number(alloy.silverRequired) || 0);
    const nonSilverRequired = Math.max(0, Number(alloy.nonSilverRequired) || 0);
    const highAssayWeight = list.reduce((sum, item) => {
      const w = Number(item?.weight);
      const a = Number(item?.assay);
      return sum + (Number.isFinite(w) && w > 0 && Number.isFinite(a) && a > 900 ? w : 0);
    }, 0);

    // Cost components are converted into equivalent grams of gold using the current
    // gold price per gram. Difference on high-assay bars is already a percentage of
    // their physical gold weight, so it is directly expressed in grams.
    const silverEquivalentGold = silverRequired * sq / goldPricePerGram;
    const barDifferenceGold = highAssayWeight * diff / 100;
    const alloyEquivalentGold = nonSilverRequired * ap / goldPricePerGram;
    const totalGoldCost = silverEquivalentGold + barDifferenceGold + alloyEquivalentGold;

    return {
      ok: [silverEquivalentGold, barDifferenceGold, alloyEquivalentGold, totalGoldCost].every(Number.isFinite),
      goldPricePerGram,
      silverRequired,
      nonSilverRequired,
      highAssayWeight,
      silverEquivalentGold,
      barDifferenceGold,
      alloyEquivalentGold,
      totalGoldCost
    };
  }

  function installStyles() {
    if ($('#goldbarR14CostStyles')) return;
    const style = document.createElement('style');
    style.id = 'goldbarR14CostStyles';
    style.textContent = `
      .r14-hidden-quick-nav{display:none!important}
      .r14-cost-results{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:9px;margin-top:13px;direction:rtl}
      .r14-cost-result{background:rgba(255,255,255,.028);border:1px solid rgba(255,255,255,.055);border-radius:10px;padding:9px;text-align:center;min-width:0}
      .r14-cost-result span{display:block;color:#8f97a4;font-size:9px;font-weight:800;margin-bottom:4px;white-space:normal}
      .r14-cost-result b{display:block;color:#f4f1e9;font-size:13px;font-weight:900;direction:ltr;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
      .r14-cost-total{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-top:10px;padding:12px 14px;border:1px solid rgba(242,196,91,.36);background:linear-gradient(90deg,rgba(242,196,91,.045),rgba(242,196,91,.10));border-radius:11px}
      .r14-cost-total span{color:#f2c45b;font-size:13px;font-weight:900}.r14-cost-total b{color:#f7cf62;font-size:20px;font-weight:900;direction:ltr}
      .r14-cost-status{margin-top:8px;color:#747d89;font-size:9px;font-weight:800;line-height:1.55;text-align:right}
      .r14-side-results{display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-top:8px}
      .r14-side-result{border:1px solid rgba(255,255,255,.05);background:#0b0e10;border-radius:8px;padding:6px;text-align:center}
      .r14-side-result span{display:block;color:#7f8793;font-size:8px;font-weight:800;margin-bottom:3px}.r14-side-result b{display:block;color:#f2c45b;font-size:11px;font-weight:900;direction:ltr}
      .r14-side-total{margin-top:7px;padding:8px;border:1px solid rgba(242,196,91,.32);background:rgba(242,196,91,.065);border-radius:8px;text-align:center}
      .r14-side-total span{display:block;color:#9da5b0;font-size:8px;font-weight:800;margin-bottom:3px}.r14-side-total b{display:block;color:#f2c45b;font-size:14px;font-weight:900;direction:ltr}
      @media(max-width:1150px){.r14-cost-results{grid-template-columns:repeat(2,minmax(0,1fr))}}
    `;
    document.head.appendChild(style);
  }

  function hideLeftQuickNav() {
    const nav = $$('.nav-item').find(btn => btn.querySelector('span')?.textContent?.trim() === 'محاسبه سریع');
    if (!nav) return false;
    nav.classList.add('r14-hidden-quick-nav');
    nav.dataset.r14Hidden = '1';
    return true;
  }

  function costResultHtml(side = false) {
    if (side) {
      return `<div class="r14-side-results" id="r14SideBreakdown">
        <div class="r14-side-result"><span>نقره معادل طلا</span><b id="r14SideSilverEq">—</b></div>
        <div class="r14-side-result"><span>فرق شمش</span><b id="r14SideDiffEq">—</b></div>
        <div class="r14-side-result"><span>بار معادل طلا</span><b id="r14SideAlloyEq">—</b></div>
        <div class="r14-side-result"><span>طلای بالای 900</span><b id="r14SideHighWeight">—</b></div>
      </div><div class="r14-side-total"><span>جمع هزینه عیار</span><b id="r14SideTotal">—</b></div>`;
    }
    return `<div class="r14-cost-results" id="r14CostBreakdown">
      <div class="r14-cost-result"><span>قیمت هر گرم طلا (تومان)</span><b id="r14GoldGramPrice">—</b></div>
      <div class="r14-cost-result"><span>هزینه نقره معادل طلا (g)</span><b id="r14SilverEq">—</b></div>
      <div class="r14-cost-result"><span>فرق شمش بالای 900 (g)</span><b id="r14DiffEq">—</b></div>
      <div class="r14-cost-result"><span>هزینه بار معادل طلا (g)</span><b id="r14AlloyEq">—</b></div>
    </div><div class="r14-cost-total"><span>جمع هزینه عیار</span><b id="r14TotalCost">—</b></div>
    <div class="r14-cost-status" id="r14CostStatus">مظنه طلا بر اساس مثقال 4.3318 گرمی به قیمت هر گرم تبدیل می‌شود.</div>`;
  }

  function patchFullCard() {
    const card = $('#r13CostAssayCard');
    if (!card) return false;
    const desc = card.querySelector('.r13-cost-title-wrap p');
    if (desc) desc.textContent = 'محاسبه هزینه عیار به معادل گرم طلا بر اساس مظنه‌ها، فرق شمش و آبشده‌های ثبت‌شده.';
    const badge = card.querySelector('.r13-cost-badge');
    if (badge) badge.textContent = 'محاسبه واقعی';
    const labels = card.querySelectorAll('.r13-cost-field label');
    if (labels[0]) labels[0].textContent = 'مظنه طلا — هر مثقال (تومان)';
    if (labels[1]) labels[1].textContent = 'مظنه نقره — هر گرم (تومان)';
    if (labels[2]) labels[2].textContent = 'درصد فرق شمش (%)';
    if (labels[3]) labels[3].textContent = 'قیمت بار — هر گرم (تومان)';
    const note = card.querySelector('.r13-cost-note');
    if (note) note.textContent = 'فرق شمش فقط روی مجموع وزن آبشده‌های با عیار بالاتر از 900 محاسبه می‌شود.';
    if (!$('#r14CostBreakdown')) card.insertAdjacentHTML('beforeend', costResultHtml(false));
    bindCostInputs(card);
    renderCost();
    return true;
  }

  function patchSideCard() {
    const pane = $('#r13QuickSidePane');
    const firstCard = pane?.querySelector('.r13-q-card.gold');
    if (!firstCard) return false;
    const titleSmall = firstCard.querySelector('.r13-q-title small');
    if (titleSmall) titleSmall.textContent = 'معادل گرم طلا';
    const labels = firstCard.querySelectorAll('.r13-q-field label');
    if (labels[0]) labels[0].textContent = 'مظنه طلا / مثقال';
    if (labels[1]) labels[1].textContent = 'مظنه نقره / گرم';
    if (labels[2]) labels[2].textContent = 'درصد فرق شمش';
    if (labels[3]) labels[3].textContent = 'قیمت بار / گرم';
    const note = firstCard.querySelector('.r13-q-note');
    if (note) note.textContent = 'نتیجه بر اساس آبشده‌های ثبت‌شده و تنظیمات عیار محاسبه می‌شود.';
    if (!$('#r14SideBreakdown')) firstCard.insertAdjacentHTML('beforeend', costResultHtml(true));
    bindCostInputs(firstCard);
    renderCost();
    return true;
  }

  function stateKeyForInput(input) {
    for (const [key, ids] of Object.entries(fieldIds)) if (ids.includes(input.id)) return key;
    return null;
  }

  function syncInputs(state, source = null) {
    for (const [key, ids] of Object.entries(fieldIds)) {
      ids.forEach(id => {
        const el = document.getElementById(id);
        if (!el || el === source) return;
        const raw = state[key] ?? '';
        el.value = key === 'barDifference' ? cleanNumeric(raw) : formatGrouped(raw);
      });
    }
  }

  function bindCostInputs(root) {
    const state = readCostState();
    root.querySelectorAll('input').forEach(input => {
      const key = stateKeyForInput(input);
      if (!key || input.dataset.r14CostBound === '1') return;
      input.dataset.r14CostBound = '1';
      input.value = key === 'barDifference' ? cleanNumeric(state[key]) : formatGrouped(state[key]);
      input.addEventListener('input', () => {
        const next = readCostState();
        const clean = cleanNumeric(input.value);
        next[key] = clean;
        writeCostState(next);
        input.value = key === 'barDifference' ? clean : formatGrouped(clean);
        syncInputs(next, input);
        renderCost();
      });
      input.addEventListener('paste', () => setTimeout(() => input.dispatchEvent(new Event('input', { bubbles:true })), 0));
      input.addEventListener('drop', e => e.preventDefault());
    });
    syncInputs(state);
  }

  function currentCostResult() {
    const state = readCostState();
    const assay = detectAssayContext();
    return calculateCost({
      goldQuote: parseNumber(state.goldQuote),
      silverQuote: parseNumber(state.silverQuote),
      barDifference: parseNumber(state.barDifference),
      alloyPrice: parseNumber(state.alloyPrice),
      entries: readEntries(),
      targetAssay: assay.targetAssay,
      silverPercent: assay.silverPercent
    });
  }

  function setText(id, text) {
    const el = document.getElementById(id);
    if (el && el.textContent !== text) el.textContent = text;
  }

  function renderCost() {
    const result = currentCostResult();
    const state = readCostState();
    const entries = readEntries();
    const assay = detectAssayContext();

    setText('r14GoldGramPrice', result.ok ? formatNumber(result.goldPricePerGram, 0) : '—');
    setText('r14SilverEq', result.ok ? `${formatNumber(result.silverEquivalentGold, 3)} g` : '—');
    setText('r14DiffEq', result.ok ? `${formatNumber(result.barDifferenceGold, 3)} g` : '—');
    setText('r14AlloyEq', result.ok ? `${formatNumber(result.alloyEquivalentGold, 3)} g` : '—');
    setText('r14TotalCost', result.ok ? `${formatNumber(result.totalGoldCost, 3)} g` : '—');
    setText('r14SideSilverEq', result.ok ? `${formatNumber(result.silverEquivalentGold, 3)} g` : '—');
    setText('r14SideDiffEq', result.ok ? `${formatNumber(result.barDifferenceGold, 3)} g` : '—');
    setText('r14SideAlloyEq', result.ok ? `${formatNumber(result.alloyEquivalentGold, 3)} g` : '—');
    setText('r14SideHighWeight', result.ok ? `${formatNumber(result.highAssayWeight, 3)} g` : '—');
    setText('r14SideTotal', result.ok ? `${formatNumber(result.totalGoldCost, 3)} g` : '—');

    let status = 'مظنه طلا بر اساس مثقال 4.3318 گرمی به قیمت هر گرم تبدیل می‌شود.';
    if (!(parseNumber(state.goldQuote) > 0)) status = 'مظنه طلا را وارد کنید.';
    else if (!Number.isFinite(parseNumber(state.silverQuote)) || !Number.isFinite(parseNumber(state.barDifference)) || !Number.isFinite(parseNumber(state.alloyPrice))) status = 'هر چهار فیلد هزینه عیار را تکمیل کنید.';
    else if (!entries.length) status = 'ابتدا حداقل یک آبشده ثبت یا گزارش را وارد کنید.';
    else if (!(Number(assay.targetAssay) > 0) || !Number.isFinite(Number(assay.silverPercent))) status = 'ابتدا عیار هدف و درصد نقره را در بخش عیار مشخص کنید.';
    else if (result.ok) status = `نقره مورد نیاز: ${formatNumber(result.silverRequired,3)} g | بار بدون نقره: ${formatNumber(result.nonSilverRequired,3)} g | طلای بالای 900: ${formatNumber(result.highAssayWeight,3)} g`;
    setText('r14CostStatus', status);
  }

  function bindAssayInputs() {
    const ids = ['r7AlloyTarget','r7SilverPercent'];
    ids.forEach(id => {
      const el = document.getElementById(id);
      if (!el || el.dataset.r14AssayBound === '1') return;
      el.dataset.r14AssayBound = '1';
      el.addEventListener('input', () => { detectAssayContext(); renderCost(); });
      el.addEventListener('change', () => { detectAssayContext(); renderCost(); });
    });
    const card = $$('.calc-card')[1];
    card?.querySelectorAll('input').forEach(el => {
      if (el.dataset.r14AssayBound === '1') return;
      el.dataset.r14AssayBound = '1';
      el.addEventListener('input', () => { detectAssayContext(); renderCost(); });
      el.addEventListener('change', () => { detectAssayContext(); renderCost(); });
    });
  }

  function wrapRecalculate() {
    if (recalcWrapped) return;
    const original = window.__goldbarRecalculate;
    if (typeof original !== 'function') return;
    if (original.__r14Wrapped) { recalcWrapped = true; return; }
    const wrapped = (...args) => {
      const value = original(...args);
      setTimeout(renderCost, 0);
      return value;
    };
    wrapped.__r14Wrapped = true;
    window.__goldbarRecalculate = wrapped;
    recalcWrapped = true;
  }

  function workbookReferenceProbe() {
    const entries = [
      { weight:84.38, assay:749 }, { weight:86.69, assay:750 }, { weight:14, assay:749 },
      { weight:23.48, assay:778 }, { weight:36.26, assay:977 }, { weight:66.07, assay:749 },
      { weight:42.23, assay:757 }
    ];
    const r = calculateCost({
      goldQuote: 40000000,
      silverQuote: 800000,
      barDifference: 1,
      alloyPrice: 1500000,
      entries,
      targetAssay: 747,
      silverPercent: 45
    });
    const close = (a,b,eps=1e-9) => Number.isFinite(a) && Math.abs(a-b) <= eps;
    const checks = {
      goldPricePerGram: close(r.goldPricePerGram, 9234036.659125537, 1e-6),
      silverEquivalent: close(r.silverEquivalentGold, 0.5260240434939749, 1e-9),
      highAssayWeight: close(r.highAssayWeight, 36.26),
      barDifference: close(r.barDifferenceGold, 0.3626),
      alloyEquivalent: close(r.alloyEquivalentGold, 1.2054717663403591, 1e-9),
      total: close(r.totalGoldCost, 2.094095809834334, 1e-9),
      grouped: formatGrouped('40000000') === '40,000,000'
    };
    return { ok: r.ok && Object.values(checks).every(Boolean), checks, result:r };
  }

  function wrapUiProbe(attempt = 0) {
    if (wrappedProbe) return;
    const previous = window.__goldbarR3Probe;
    if (typeof previous !== 'function') {
      if (attempt < 50) setTimeout(() => wrapUiProbe(attempt + 1), 100);
      return;
    }
    const wrapper = () => {
      const base = previous();
      hideLeftQuickNav();
      patchSideCard();
      const engineProbe = workbookReferenceProbe();
      const quickNav = $$('.nav-item').find(btn => btn.querySelector('span')?.textContent?.trim() === 'محاسبه سریع');
      const r14 = {
        engine: engineProbe.ok,
        leftQuickHidden: quickNav?.dataset.r14Hidden === '1' && getComputedStyle(quickNav).display === 'none',
        rightQuickTab: Boolean($('#r13TabQuick') && $('#r13QuickSidePane')),
        groupedFieldsReady: Boolean($('#r13SideGoldQuote')?.dataset.r14CostBound === '1')
      };
      r14.ok = Object.values(r14).every(Boolean);
      return { ...base, r14, ok: Boolean(base?.ok && r14.ok) };
    };
    wrapper.__r14Wrapped = true;
    window.__goldbarR3Probe = wrapper;
    wrappedProbe = true;
  }

  function keepVersion() {
    const version = $('.version');
    if (version) version.textContent = 'GOLD BAR v2.0.0-r14';
  }

  function init(attempt = 0) {
    installStyles();
    hideLeftQuickNav();
    patchFullCard();
    patchSideCard();
    bindAssayInputs();
    wrapRecalculate();
    renderCost();
    keepVersion();
    wrapUiProbe();

    if (!observer) {
      observer = new MutationObserver(() => {
        requestAnimationFrame(() => {
          hideLeftQuickNav();
          patchFullCard();
          patchSideCard();
          bindAssayInputs();
          renderCost();
          keepVersion();
        });
      });
      observer.observe(document.body, { childList:true, subtree:true });
    }

    // r12 owns a version MutationObserver. Replacing the element after its delayed
    // passes releases that old observer without touching the adaptive-resolution code.
    if (attempt === 0) {
      setTimeout(() => {
        const old = $('.version');
        if (old && old.textContent !== 'GOLD BAR v2.0.0-r14') {
          const clone = old.cloneNode(true);
          clone.textContent = 'GOLD BAR v2.0.0-r14';
          old.replaceWith(clone);
        }
        keepVersion();
      }, 1450);
    }

    if ((!window.__goldbarAssayEngineV2 || !$('.nav-item')) && attempt < 60)
      setTimeout(() => init(attempt + 1), 100);
  }

  window.__goldbarCostEngineV14 = { calculateCost, formatGrouped, workbookReferenceProbe };
  init();
})();
