(() => {
  'use strict';

  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];
  let observer = null;

  function normalizeDigits(value) {
    const fa = '۰۱۲۳۴۵۶۷۸۹';
    const ar = '٠١٢٣٤٥٦٧٨٩';
    return String(value ?? '')
      .replace(/[۰-۹]/g, d => String(fa.indexOf(d)))
      .replace(/[٠-٩]/g, d => String(ar.indexOf(d)))
      .replace(/,/g, '.');
  }

  function sanitizeDecimal(input) {
    let value = normalizeDigits(input.value).replace(/[^0-9.]/g, '');
    const dot = value.indexOf('.');
    if (dot >= 0) value = value.slice(0, dot + 1) + value.slice(dot + 1).replace(/\./g, '');
    input.value = value;
  }

  function installStyles() {
    if ($('#goldbarR13CostStyles')) return;
    const style = document.createElement('style');
    style.id = 'goldbarR13CostStyles';
    style.textContent = `
      .r13-cost-card{
        grid-column:1/-1;
        position:relative;
        overflow:hidden;
        border:1px solid rgba(240,185,28,.28);
        background:linear-gradient(145deg,rgba(25,27,29,.98),rgba(15,17,19,.98));
        border-radius:16px;
        padding:18px 20px 20px;
        box-shadow:0 12px 28px rgba(0,0,0,.22);
        direction:rtl;
      }
      .r13-cost-card:before{
        content:"";
        position:absolute;
        top:-1px;
        right:34px;
        width:95px;
        height:1px;
        background:linear-gradient(90deg,transparent,#f2c45b,transparent);
      }
      .r13-cost-head{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;margin-bottom:16px}
      .r13-cost-title-wrap h3{margin:0 0 5px;color:#f2c45b;font-size:18px;font-weight:900}
      .r13-cost-title-wrap p{margin:0;color:#8f97a4;font-size:11px;font-weight:800;line-height:1.7}
      .r13-cost-badge{display:flex;align-items:center;justify-content:center;min-width:78px;height:30px;padding:0 10px;border:1px solid rgba(242,196,91,.25);border-radius:15px;background:rgba(242,196,91,.07);color:#f2c45b;font-size:10px;font-weight:900;white-space:nowrap}
      .r13-cost-fields{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;direction:rtl}
      .r13-cost-field label{display:block;margin:0 0 6px;color:#c3c8d0;font-size:11px;font-weight:900}
      .r13-cost-field input{width:100%;height:44px;box-sizing:border-box;border:1px solid #3a3e40;background:#0d1012;color:#f4f1e9;border-radius:10px;padding:0 12px;font:900 14px Tahoma,"Segoe UI",Arial,sans-serif;direction:ltr;text-align:center;outline:none}
      .r13-cost-field input::placeholder{color:#59606a;opacity:1}
      .r13-cost-field input:focus{border-color:rgba(242,196,91,.72);box-shadow:0 0 0 2px rgba(242,196,91,.08)}
      .r13-cost-note{margin-top:11px;color:#6f7782;font-size:10px;font-weight:800;text-align:right}
      @media(max-width:1150px){.r13-cost-fields{grid-template-columns:repeat(2,minmax(0,1fr))}}
    `;
    document.head.appendChild(style);
  }

  function bindNumericInputs(card) {
    card.querySelectorAll('input').forEach(input => {
      if (input.dataset.r13Numeric === '1') return;
      input.dataset.r13Numeric = '1';
      input.addEventListener('input', () => sanitizeDecimal(input));
      input.addEventListener('paste', () => setTimeout(() => sanitizeDecimal(input), 0));
      input.addEventListener('drop', e => e.preventDefault());
    });
  }

  function patchQuickCalc() {
    const title = $('.dash-title span:last-child')?.textContent?.trim();
    if (title !== 'محاسبه سریع') return false;
    const host = $('#pageHost');
    const tools = host?.querySelector('.canonical-tools');
    if (!tools) return false;

    let card = $('#r13CostAssayCard');
    if (!card) {
      card = document.createElement('section');
      card.id = 'r13CostAssayCard';
      card.className = 'r13-cost-card';
      card.innerHTML = `
        <div class="r13-cost-head">
          <div class="r13-cost-title-wrap">
            <h3>هزینه عیار</h3>
            <p>ورودی‌های قیمت و اختلاف شمش — در این مرحله فقط رابط کاربری ساخته شده است.</p>
          </div>
          <div class="r13-cost-badge">مرحله اول</div>
        </div>
        <div class="r13-cost-fields">
          <div class="r13-cost-field"><label for="r13GoldQuote">مظنه طلا</label><input id="r13GoldQuote" inputmode="decimal" autocomplete="off" placeholder="0"></div>
          <div class="r13-cost-field"><label for="r13SilverQuote">مظنه نقره</label><input id="r13SilverQuote" inputmode="decimal" autocomplete="off" placeholder="0"></div>
          <div class="r13-cost-field"><label for="r13BarDifference">درصد فرق شمش</label><input id="r13BarDifference" inputmode="decimal" autocomplete="off" placeholder="0"></div>
          <div class="r13-cost-field"><label for="r13AlloyPrice">قیمت بار</label><input id="r13AlloyPrice" inputmode="decimal" autocomplete="off" placeholder="0"></div>
        </div>
        <div class="r13-cost-note">فرمول محاسبه را در مرحله بعد، بعد از تأیید ظاهر این بخش اضافه می‌کنیم.</div>`;
      tools.appendChild(card);
    }
    bindNumericInputs(card);
    return true;
  }

  function installNavigationHook() {
    const nav = $$('.nav-item').find(btn => btn.querySelector('span')?.textContent?.trim() === 'محاسبه سریع');
    if (!nav || nav.dataset.r13CostHook === '1') return;
    nav.dataset.r13CostHook = '1';
    nav.addEventListener('click', () => {
      setTimeout(patchQuickCalc, 30);
      setTimeout(patchQuickCalc, 120);
      setTimeout(patchQuickCalc, 350);
    }, true);
  }

  function startObserver() {
    if (observer) return;
    observer = new MutationObserver(() => {
      if ($('.dash-title span:last-child')?.textContent?.trim() === 'محاسبه سریع')
        requestAnimationFrame(patchQuickCalc);
    });
    observer.observe(document.body, { childList:true, subtree:true });
  }

  function init(attempt = 0) {
    installStyles();
    installNavigationHook();
    startObserver();
    patchQuickCalc();
    if (!$('.nav-item') && attempt < 40) setTimeout(() => init(attempt + 1), 100);
  }

  init();
})();
