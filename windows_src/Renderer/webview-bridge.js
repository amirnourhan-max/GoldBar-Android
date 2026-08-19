(() => {
  'use strict';

  function loadEnhancements() {
    if (document.querySelector('script[data-goldbar-enhancements]')) return;
    const script = document.createElement('script');
    script.src = 'enhancements.js';
    script.dataset.goldbarEnhancements = '1';
    document.body.appendChild(script);
  }

  if (!window.chrome?.webview) {
    window.addEventListener('DOMContentLoaded', () => setTimeout(loadEnhancements, 0), { once: true });
    return;
  }

  let seq = 0;
  const pending = new Map();
  const listeners = new Map();

  function request(action, payload = null) {
    const id = `r${++seq}`;
    return new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
      window.chrome.webview.postMessage({ kind: 'request', id, action, payload });
      setTimeout(() => {
        const p = pending.get(id);
        if (!p) return;
        pending.delete(id);
        reject(new Error(`Timeout: ${action}`));
      }, 10000);
    });
  }

  function on(event, callback) {
    if (!listeners.has(event)) listeners.set(event, new Set());
    listeners.get(event).add(callback);
  }

  window.chrome.webview.addEventListener('message', event => {
    const msg = event.data;
    if (!msg || typeof msg !== 'object') return;
    if (msg.kind === 'response') {
      const p = pending.get(msg.id);
      if (!p) return;
      pending.delete(msg.id);
      msg.ok === false ? p.reject(new Error(msg.error || 'Host error')) : p.resolve(msg.data);
      return;
    }
    if (msg.kind === 'event') {
      listeners.get(msg.event)?.forEach(cb => {
        try { cb(msg.data); } catch (e) { console.error(e); }
      });
    }
  });

  window.goldbar = {
    minimize: () => request('window:minimize'),
    maximizeToggle: () => request('window:maximizeToggle'),
    close: () => request('window:close'),
    getSettings: () => request('settings:get'),
    saveSettings: settings => request('settings:save', settings),
    resetSettings: () => request('settings:reset'),
    connectScale: () => request('scale:connect'),
    disconnectScale: () => request('scale:disconnect'),
    readScale: () => request('scale:read'),
    onWeight: cb => on('scale:weight', cb),
    onScaleStatus: cb => on('scale:status', cb),
    onScaleError: cb => on('scale:error', cb)
  };

  // app.js assigns the final quick-action ids and listeners synchronously.
  // Replace only the clear button after initialization so "پاک کردن همه"
  // clears the quick-entry fields and can never delete saved melt records.
  setTimeout(() => {
    const oldButton = document.querySelector('#quickClearAll');
    if (oldButton?.parentNode) {
      const button = oldButton.cloneNode(true);
      oldButton.parentNode.replaceChild(button, oldButton);
      button.addEventListener('click', () => {
        const weight = document.querySelector('#weightInput');
        const assay = document.querySelector('#purityInput');
        const description = document.querySelector('#descriptionInput');
        if (weight) weight.value = '';
        if (assay) assay.value = '';
        if (description) description.value = '';
        weight?.focus();
      });
    }
    loadEnhancements();
  }, 0);
})();
