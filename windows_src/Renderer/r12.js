(() => {
  'use strict';

  const BASE_W = 1536;
  const BASE_H = 1024;
  const MIN_SCALE = 0.35;
  const $ = s => document.querySelector(s);
  let layoutProbeWrapped = false;
  let versionObserver = null;

  function installStyles() {
    if ($('#goldbarR12Styles')) return;
    const style = document.createElement('style');
    style.id = 'goldbarR12Styles';
    style.textContent = `
      /* r12: true adaptive viewport. The virtual canvas expands on the non-limiting
         axis, so the interface fills the WebView without letterbox/black margins. */
      .viewport{align-items:flex-start!important;justify-content:flex-start!important;background:#050709!important}
      .design-root{transform-origin:top left!important;flex:0 0 auto!important}
      .design-root .app-shell{width:100%!important;height:100%!important;margin:0!important}

      /* Let the approved dashboard consume the extra virtual width instead of leaving
         it unused around the fixed 1536px reference canvas. */
      .workspace-body{grid-template-columns:minmax(886px,1fr) 287px!important}
      .workspace-body.full-center{grid-template-columns:1fr!important}
      .center{width:100%!important;max-width:none!important}
      .summary-grid{grid-template-columns:repeat(4,minmax(0,1fr))!important}
      .bottom-grid{grid-template-columns:minmax(250px,1fr) minmax(250px,.98fr) minmax(290px,1.18fr)!important}

      /* Left scale card: r5 adds test/result rows dynamically. Keep all of those rows
         inside the card and above the version/status area at every supported scale. */
      .sidebar-bottom{height:342px!important;flex:0 0 342px!important;padding:0 18px 11px!important}
      .scale-card{height:244px!important;min-height:244px!important;padding:12px 14px!important;overflow:hidden!important}
      .scale-weight{height:52px!important}
      .scale-card .ghost-btn{height:36px!important}
      .r5-scale-test{margin-top:6px!important;margin-bottom:0!important}
      .r5-scale-result{min-height:28px!important;max-height:44px!important;margin-top:5px!important;padding:5px 7px!important;overflow-y:auto!important;line-height:1.35!important}
      #scaleDisconnect{margin-top:5px!important}
      .version{margin:8px 0 5px 9px!important}
      .fresh{margin-left:9px!important}

      @media(max-width:1100px){
        .workspace-body{grid-template-columns:minmax(820px,1fr) 275px!important;column-gap:18px!important}
        .bottom-grid{grid-template-columns:minmax(235px,1fr) minmax(235px,.98fr) minmax(275px,1.15fr)!important}
      }
    `;
    document.head.appendChild(style);
  }

  function fitAdaptiveViewport() {
    const w = Math.max(1, window.innerWidth);
    const h = Math.max(1, window.innerHeight);
    const scale = Math.max(MIN_SCALE, Math.min(w / BASE_W, h / BASE_H));
    const virtualW = Math.max(BASE_W, w / scale);
    const virtualH = Math.max(BASE_H, h / scale);
    const root = $('.design-root');
    if (!root) return;

    // app.js owns --ui-scale too; using the same formula keeps both listeners coherent.
    document.documentElement.style.setProperty('--ui-scale', String(scale));
    root.style.width = `${virtualW}px`;
    root.style.height = `${virtualH}px`;
    root.style.transformOrigin = 'top left';
  }

  function scaleCardProbe() {
    const card = $('.scale-card');
    const version = $('.version');
    const sidebar = $('.sidebar');
    if (!card || !version || !sidebar) return { ok: false, reason: 'scale-elements-missing' };
    const cardRect = card.getBoundingClientRect();
    const versionRect = version.getBoundingClientRect();
    const sidebarRect = sidebar.getBoundingClientRect();
    const childRects = [...card.children].map(el => el.getBoundingClientRect()).filter(r => r.width > 0 || r.height > 0);
    const childBottom = childRects.length ? Math.max(...childRects.map(r => r.bottom)) : cardRect.top;
    const childrenInside = childBottom <= cardRect.bottom + 2;
    const versionBelow = cardRect.bottom <= versionRect.top + 2;
    const sidebarContains = versionRect.bottom <= sidebarRect.bottom + 2;
    return { ok: childrenInside && versionBelow && sidebarContains, childrenInside, versionBelow, sidebarContains, childBottom, cardBottom: cardRect.bottom, versionTop: versionRect.top };
  }

  function viewportProbe() {
    const root = $('.design-root');
    if (!root) return { ok: false, reason: 'root-missing' };
    const rect = root.getBoundingClientRect();
    const eps = 3;
    const fillsWidth = Math.abs(rect.width - window.innerWidth) <= eps && Math.abs(rect.left) <= eps;
    const fillsHeight = Math.abs(rect.height - window.innerHeight) <= eps && Math.abs(rect.top) <= eps;
    const scaleCard = scaleCardProbe();
    return {
      ok: fillsWidth && fillsHeight && scaleCard.ok,
      fillsWidth,
      fillsHeight,
      viewportWidth: window.innerWidth,
      viewportHeight: window.innerHeight,
      rootLeft: rect.left,
      rootTop: rect.top,
      rootWidth: rect.width,
      rootHeight: rect.height,
      scaleCard
    };
  }

  function wrapLayoutProbe(attempt = 0) {
    if (layoutProbeWrapped) return;
    const previous = window.__goldbarLayoutProbe;
    if (typeof previous !== 'function') {
      if (attempt < 60) setTimeout(() => wrapLayoutProbe(attempt + 1), 100);
      return;
    }
    const wrapped = () => {
      const base = previous();
      const r12 = viewportProbe();
      return { ...base, r12, fillsViewport: r12.fillsWidth && r12.fillsHeight, scaleCardNoOverlap: r12.scaleCard?.ok === true, ok: Boolean(base?.ok && r12.ok) };
    };
    wrapped.__r12Wrapped = true;
    window.__goldbarLayoutProbe = wrapped;
    layoutProbeWrapped = true;
  }

  function keepVersionLabel() {
    const version = $('.version');
    if (!version) return false;
    const desired = 'GOLD BAR v2.0.0-r12';
    if (version.textContent !== desired) version.textContent = desired;
    if (!versionObserver) {
      versionObserver = new MutationObserver(() => {
        if (version.textContent !== desired) version.textContent = desired;
      });
      versionObserver.observe(version, { childList: true, characterData: true, subtree: true });
    }
    return true;
  }

  function init(attempt = 0) {
    installStyles();
    fitAdaptiveViewport();
    window.addEventListener('resize', () => requestAnimationFrame(fitAdaptiveViewport), { passive: true });
    window.__goldbarR12Probe = viewportProbe;
    wrapLayoutProbe();
    keepVersionLabel();
    setTimeout(keepVersionLabel, 400);
    setTimeout(keepVersionLabel, 1200);

    if ((!$('.scale-card') || !$('.workspace-body')) && attempt < 40)
      setTimeout(() => init(attempt + 1), 100);
  }

  init();
})();
