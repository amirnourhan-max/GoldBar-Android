(() => {
  'use strict';

  const ENTRY_KEY = 'goldbar.windows.entries.v2';
  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];
  let recalculating = false;

  function normalizeDigits(value) {
    const fa = '۰۱۲۳۴۵۶۷۸۹';
    const ar = '٠١٢٣٤٥٦٧٨٩';
    return String(value ?? '')
      .replace(/[۰-۹]/g, d => String(fa.indexOf(d)))
      .replace(/[٠-٩]/g, d => String(ar.indexOf(d)))
      .replace(/,/g, '.');
  }

  function parseNumber(value) {
    const n = Number(normalizeDigits(value));
    return Number.isFinite(n) ? n : NaN;
  }

  function formatNumber(value, digits = 3) {
    const n = Number(value);
    if (!Number.isFinite(n)) return '0';
    return n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: digits });
  }

  function readEntries() {
    try {
      const parsed = JSON.parse(localStorage.getItem(ENTRY_KEY) || '[]');
      return Array.isArray(parsed)
        ? parsed.filter(e => Number(e.weight) > 0 && Number(e.assay) > 0)
        : [];
    } catch {
      return [];
    }
  }

  function summarize(list = readEntries()) {
    let weight = 0;
    let weightedSum = 0;
    let count = 0;
    for (const e of list) {
      const w = Number(e.weight);
      const a = Number(e.assay);
      if (!(w > 0) || !(a > 0)) continue;
      count++;
      weight += w;
      weightedSum += w * a;
    }
    return { count, weight, weightedSum, averageAssay: weight > 0 ? weightedSum / weight : NaN };
  }

  function roundDownTowardZero(value, digits) {
    if (!Number.isFinite(value)) return NaN;
    const factor = 10 ** digits;
    const scaled = value * factor;
    const truncated = scaled >= 0 ? Math.floor(scaled) : Math.ceil(scaled);
    return truncated / factor;
  }

  function requiredHighAssayBar(summary, castingAssay, barAssay) {
    if (!(summary.weight > 0) || !Number.isFinite(summary.averageAssay)) {
      return { assayDifference: NaN, denominator: NaN, requiredBar: NaN };
    }
    const assayDifference = castingAssay - summary.averageAssay;
    const denominator = barAssay - castingAssay;
    const requiredBar = denominator === 0
      ? NaN
      : roundDownTowardZero(summary.weight * assayDifference / denominator, 1);
    return { assayDifference, denominator, requiredBar };
  }

  function requiredAlloy(summary, castingAssay, silverPercent, globalWeight = summary.weight) {
    if (!(summary.weight > 0) || !Number.isFinite(summary.averageAssay) || castingAssay === 0) {
      return {
        totalAlloyRequired: NaN, silverRequired: NaN, nonSilverRequired: NaN,
        fourPerThousand: NaN, finalOtherAlloy: NaN, totalAfterAlloy: NaN
      };
    }
    const totalAlloyRequired = summary.weight * summary.averageAssay / castingAssay - summary.weight;
    const silverRequired = (silverPercent / 100) * totalAlloyRequired;
    const nonSilverRequired = totalAlloyRequired - silverRequired;
    const fourPerThousand = globalWeight * 0.004;
    const finalOtherAlloy = totalAlloyRequired - silverRequired - fourPerThousand;
    const totalAfterAlloy = summary.weight + totalAlloyRequired;
    return { totalAlloyRequired, silverRequired, nonSilverRequired, fourPerThousand, finalOtherAlloy, totalAfterAlloy };
  }

  function setText(el, value) {
    if (el && el.textContent !== value) el.textContent = value;
  }

  function recalculateCards() {
    if (recalculating) return;
    recalculating = true;
    try {
      const summary = summarize();
      const cards = $$('.calc-card');

      if (cards[0]) {
        const inputs = [...cards[0].querySelectorAll('input')];
        const stats = [...cards[0].querySelectorAll('.mini-stats b')];
        const target = parseNumber(inputs[0]?.value);
        const barAssay = parseNumber(inputs[1]?.value);
        const result = requiredHighAssayBar(summary, target, barAssay);
        setText(stats[0], Number.isFinite(result.assayDifference) ? formatNumber(result.assayDifference, 3) : '0');
        setText(stats[1], Number.isFinite(result.requiredBar) ? formatNumber(result.requiredBar, 1) : '0');
        const footer = cards[0].querySelector('.calc-footer');
        if (footer) {
          const message = !Number.isFinite(result.requiredBar) || result.requiredBar <= 0
            ? 'افزایش عیار لازم نیست'
            : `شمش مورد نیاز: ${formatNumber(result.requiredBar, 1)} g`;
          setText(footer, message);
        }
      }

      let totalAlloy = NaN;
      if (cards[1]) {
        const inputs = [...cards[1].querySelectorAll('input')];
        const stats = [...cards[1].querySelectorAll('.mini-stats b')];
        const required = cards[1].querySelector('.wide-stat b');
        const castingAssay = parseNumber(inputs[0]?.value);
        const silverPercent = parseNumber(inputs[1]?.value);
        const result = requiredAlloy(summary, castingAssay, silverPercent, summary.weight);
        const pureGold = summary.weight > 0 && Number.isFinite(summary.averageAssay)
          ? summary.weight * summary.averageAssay / 1000
          : 0;
        setText(stats[0], formatNumber(pureGold, 3));
        setText(stats[1], formatNumber(summary.weight || 0, 3));
        totalAlloy = result.totalAlloyRequired;
        setText(required, Number.isFinite(totalAlloy) ? formatNumber(totalAlloy, 3) : '0');
      }

      const summaryCards = $$('.summary-card .metric-value');
      if (summaryCards[3]) {
        const shown = Number.isFinite(totalAlloy) ? Math.max(0, totalAlloy) : 0;
        setText(summaryCards[3], formatNumber(shown, 3));
      }
    } finally {
      recalculating = false;
    }
  }

  function sanitizeInteger(el, min, max) {
    let value = normalizeDigits(el.value).replace(/\D/g, '');
    if (value !== '') {
      let n = Number(value);
      if (Number.isFinite(min)) n = Math.max(min, n);
      if (Number.isFinite(max)) n = Math.min(max, n);
      value = String(n);
    }
    el.value = value;
  }

  function bindStrictSettingsNumbers() {
    const interval = $('#readInterval');
    const decimals = $('#decimals');
    if (interval) {
      interval.classList.add('numeric-input', 'integer-input');
      interval.addEventListener('input', () => sanitizeInteger(interval, 100, 10000));
      interval.addEventListener('paste', () => setTimeout(() => sanitizeInteger(interval, 100, 10000), 0));
      interval.addEventListener('drop', e => e.preventDefault());
    }
    if (decimals) {
      decimals.classList.add('numeric-input', 'integer-input');
      decimals.addEventListener('input', () => sanitizeInteger(decimals, 0, 6));
      decimals.addEventListener('paste', () => setTimeout(() => sanitizeInteger(decimals, 0, 6), 0));
      decimals.addEventListener('drop', e => e.preventDefault());
    }
  }

  function bindCalculationInputs() {
    $$('.calc-card input').forEach(input => {
      input.addEventListener('input', () => setTimeout(recalculateCards, 0));
      input.addEventListener('change', recalculateCards);
    });
    $('#quickSave')?.addEventListener('click', () => setTimeout(recalculateCards, 0));
    $('#quickClearAll')?.addEventListener('click', () => setTimeout(recalculateCards, 0));
    $('#purityInput')?.addEventListener('keydown', e => {
      if (e.key === 'Enter') setTimeout(recalculateCards, 0);
    });
    $('#descriptionInput')?.addEventListener('keydown', e => {
      if (e.key === 'Enter') setTimeout(recalculateCards, 0);
    });

    const summary = $('.summary-grid');
    if (summary) {
      const observer = new MutationObserver(() => setTimeout(recalculateCards, 0));
      observer.observe(summary, { subtree: true, childList: true, characterData: true });
    }
  }

  function updateReleaseLabel() {
    const version = $('.version');
    if (version) version.textContent = 'GOLD BAR v2.0.0-r2';
  }

  function layoutProbe() {
    const cssScale = Number(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale'));
    const expectedScale = Math.max(0.35, Math.min(window.innerWidth / 1536, window.innerHeight / 1024));
    const root = document.querySelector('.design-root');
    const rect = root?.getBoundingClientRect();
    const epsilon = 3;
    const fits = Boolean(rect && rect.width <= window.innerWidth + epsilon && rect.height <= window.innerHeight + epsilon);
    return {
      ok: Number.isFinite(cssScale) && Math.abs(cssScale - expectedScale) < 0.002 && fits,
      width: window.innerWidth,
      height: window.innerHeight,
      cssScale,
      expectedScale,
      rootWidth: rect?.width ?? 0,
      rootHeight: rect?.height ?? 0,
      fits
    };
  }

  function calculationProbe() {
    const sample = [
      { weight: 84.38, assay: 749 }, { weight: 86.69, assay: 750 },
      { weight: 14, assay: 749 }, { weight: 23.48, assay: 778 },
      { weight: 36.26, assay: 977 }, { weight: 66.07, assay: 749 },
      { weight: 42.23, assay: 757 }
    ];
    const s = summarize(sample);
    const a = requiredHighAssayBar(s, 747, 995);
    const x = requiredAlloy(s, 747, 45, s.weight);
    const near = (v, e, eps = 1e-8) => Number.isFinite(v) && Math.abs(v - e) <= eps;
    const ok = near(s.weight, 353.11)
      && near(s.averageAssay, 775.5433717538444)
      && near(a.requiredBar, -40.6)
      && near(x.totalAlloyRequired, 13.492570281124529)
      && near(x.silverRequired, 6.071656626506038)
      && near(x.nonSilverRequired, 7.420913654618491)
      && near(x.fourPerThousand, 1.41244)
      && near(x.finalOtherAlloy, 6.0084736546184905);
    return { ok, summary: s, adjustment: a, alloy: x };
  }

  bindStrictSettingsNumbers();
  bindCalculationInputs();
  updateReleaseLabel();
  recalculateCards();

  window.__goldbarLayoutProbe = layoutProbe;
  window.__goldbarCalculationProbe = calculationProbe;
  window.__goldbarRecalculate = recalculateCards;
})();
