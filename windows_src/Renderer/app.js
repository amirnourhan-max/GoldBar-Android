(() => {
  'use strict';

  const DEFAULTS = {
    port: 'COM4', baudRate: 2400, dataBits: 7, parity: 'Even', stopBits: 2,
    flowControl: 'None', autoRead: true, readIntervalMs: 800, decimals: 3,
    requestCommand: 'P', keyboardRead: true
  };

  const $ = s => document.querySelector(s);
  const $$ = s => [...document.querySelectorAll(s)];
  let mockStore = { ...DEFAULTS };
  const bridge = window.goldbar || {
    async getSettings() { return { ...mockStore }; },
    async saveSettings(v) { mockStore = { ...DEFAULTS, ...v }; return { ...mockStore }; },
    async resetSettings() { mockStore = { ...DEFAULTS }; return { ...mockStore }; },
    async readScale() { setWeight(214.373); return { ok: true, mock: true }; },
    async connectScale() { setScaleStatus(true, 'متصل'); return { ok: true, mock: true }; },
    async disconnectScale() { setScaleStatus(false, 'قطع'); return { ok: true, mock: true }; },
    minimize() {}, maximizeToggle() {}, close() {},
    onWeight() {}, onScaleStatus() {}, onScaleError() {}
  };

  let settings = { ...DEFAULTS };
  let connected = true;

  function fitToViewport() {
    const w = Math.max(1, window.innerWidth);
    const h = Math.max(1, window.innerHeight);
    const scale = Math.min(w / 1536, h / 1024);
    document.documentElement.style.setProperty('--ui-scale', String(Math.max(0.35, scale)));
  }

  function normalizeDigits(value) {
    const fa = '۰۱۲۳۴۵۶۷۸۹';
    const ar = '٠١٢٣٤٥٦٧٨٩';
    return String(value ?? '')
      .replace(/[۰-۹]/g, d => String(fa.indexOf(d)))
      .replace(/[٠-٩]/g, d => String(ar.indexOf(d)))
      .replace(/,/g, '.');
  }

  function sanitizeNumeric(el) {
    const isInteger = el.classList.contains('integer-input');
    let value = normalizeDigits(el.value);
    value = isInteger ? value.replace(/\D/g, '') : value.replace(/[^0-9.]/g, '');
    if (!isInteger) {
      const dot = value.indexOf('.');
      if (dot >= 0) value = value.slice(0, dot + 1) + value.slice(dot + 1).replace(/\./g, '');
    }
    if (el.id === 'purityInput') {
      const n = Number(value);
      if (Number.isFinite(n) && n > 1000) value = '1000';
    }
    el.value = value;
  }

  function bindNumericOnly() {
    $$('.numeric-input').forEach(el => {
      el.addEventListener('input', () => sanitizeNumeric(el));
      el.addEventListener('paste', () => setTimeout(() => sanitizeNumeric(el), 0));
      el.addEventListener('drop', e => e.preventDefault());
    });
  }

  function setWeight(value) {
    if (!Number.isFinite(Number(value))) return;
    const fixed = Number(value).toFixed(Number(settings.decimals ?? 3));
    $$('[data-weight-value]').forEach(el => { if ('value' in el) el.value = fixed; else el.textContent = fixed; });
    const weightInput = $('#weightInput'); if (weightInput) weightInput.value = fixed;
    const result = $('#testResult b');
    if (result) result.textContent = `${fixed} g`;
  }

  function setScaleStatus(isConnected, message = '') {
    connected = Boolean(isConnected);
    const box = $('#scaleStatus');
    if (!box) return;
    box.classList.toggle('offline', !connected);
    const text = box.querySelector('span:last-child');
    if (text) text.textContent = connected ? 'متصل' : (message || 'قطع');
    const btn = $('#scaleDisconnect');
    if (btn) btn.textContent = connected ? 'قطع اتصال' : 'اتصال';
  }

  function setToggle(el, on) {
    if (!el) return;
    el.classList.toggle('on', Boolean(on));
    el.setAttribute('aria-pressed', String(Boolean(on)));
  }

  function populate(s) {
    settings = { ...DEFAULTS, ...s };
    $('#portSelect').value = settings.port;
    $('#baudSelect').value = String(settings.baudRate);
    $('#dataBitsSelect').value = String(settings.dataBits);
    $('#paritySelect').value = settings.parity;
    $('#stopBitsSelect').value = String(settings.stopBits);
    $('#flowSelect').value = settings.flowControl;
    $('#readInterval').value = String(settings.readIntervalMs);
    $('#decimals').value = String(settings.decimals);
    $('#requestCommand').value = settings.requestCommand ?? '';
    setToggle($('#autoReadToggle'), settings.autoRead);
    setToggle($('#keyboardReadToggle'), settings.keyboardRead);
  }

  function collect() {
    return {
      port: $('#portSelect').value,
      baudRate: Number($('#baudSelect').value),
      dataBits: Number($('#dataBitsSelect').value),
      parity: $('#paritySelect').value,
      stopBits: Number($('#stopBitsSelect').value),
      flowControl: $('#flowSelect').value,
      autoRead: $('#autoReadToggle').classList.contains('on'),
      readIntervalMs: Math.max(100, Number($('#readInterval').value) || 800),
      decimals: Math.max(0, Math.min(6, Number($('#decimals').value) || 0)),
      requestCommand: $('#requestCommand').value,
      keyboardRead: $('#keyboardReadToggle').classList.contains('on')
    };
  }

  function flashButton(btn, text) {
    if (!btn) return;
    const old = btn.textContent;
    btn.textContent = text;
    btn.classList.add('saved');
    setTimeout(() => { btn.textContent = old; btn.classList.remove('saved'); }, 1000);
  }

  async function readScale() {
    const result = $('#testResult');
    if (result) result.classList.add('waiting');
    try { await bridge.readScale(); }
    catch (e) { setScaleStatus(false, e?.message || 'خطای ترازو'); }
    finally { setTimeout(() => result?.classList.remove('waiting'), 400); }
  }

  fitToViewport();
  window.addEventListener('resize', fitToViewport, { passive: true });
  bindNumericOnly();

  $('#winMin')?.addEventListener('click', () => bridge.minimize());
  $('#winMax')?.addEventListener('click', () => bridge.maximizeToggle());
  $('#winClose')?.addEventListener('click', () => bridge.close());

  $$('.nav-item').forEach(btn => btn.addEventListener('click', () => {
    $$('.nav-item').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
  }));

  $('#readScale')?.addEventListener('click', readScale);
  $('#testScale')?.addEventListener('click', readScale);
  $('#autoReadToggle')?.addEventListener('click', e => setToggle(e.currentTarget, !e.currentTarget.classList.contains('on')));
  $('#keyboardReadToggle')?.addEventListener('click', e => setToggle(e.currentTarget, !e.currentTarget.classList.contains('on')));

  $('#scaleDisconnect')?.addEventListener('click', async () => {
    if (connected) {
      await bridge.disconnectScale();
      setScaleStatus(false, 'قطع');
    } else {
      const r = await bridge.connectScale();
      setScaleStatus(Boolean(r?.ok), r?.reason || 'خطا');
    }
  });

  $('#saveSettings')?.addEventListener('click', async e => {
    settings = await bridge.saveSettings(collect());
    populate(settings);
    flashButton(e.currentTarget, 'ذخیره شد ✓');
  });

  $('#resetSettings')?.addEventListener('click', async () => {
    settings = await bridge.resetSettings();
    populate(settings);
  });

  document.addEventListener('keydown', e => {
    if (e.key === 'ArrowUp' && $('#keyboardReadToggle')?.classList.contains('on')) {
      const tag = document.activeElement?.tagName;
      if (tag !== 'INPUT' && tag !== 'SELECT' && tag !== 'TEXTAREA') {
        e.preventDefault();
        readScale();
      }
    }
  });

  bridge.onWeight?.(payload => {
    if (payload && Number.isFinite(Number(payload.value))) setWeight(Number(payload.value));
  });
  bridge.onScaleStatus?.(payload => setScaleStatus(Boolean(payload?.connected), payload?.message || ''));
  bridge.onScaleError?.(payload => setScaleStatus(false, payload?.message || 'خطای ترازو'));

  bridge.getSettings().then(populate).catch(() => populate(DEFAULTS));
})();
