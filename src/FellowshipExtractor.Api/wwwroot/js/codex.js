/*
  FELLOWSHIP CODEX - the three things the pages cannot do server side.

  The mega menu, the omnibox, and the entity page's live tooltip. Everything else on every page is
  rendered by the server, so this file never builds a row, a filter or a count: it opens a panel,
  lists what /api/search answered, and swaps in the fragment /api/tooltip returns.

  The tooltip is deliberately fetched rather than rendered into the page. It is the same endpoint any
  other caller uses, so a reader changing the rung or the hero sees exactly what an embedder would
  get, and the page cannot drift from the API by drawing its own copy.
*/

(() => {
  'use strict';

  /* ---- mega menu ------------------------------------------------------------------------ */

  const groups = [...document.querySelectorAll('.mega-group')];

  function close(except) {
    for (const group of groups) {
      if (group === except) continue;
      group.removeAttribute('data-open');
      group.querySelector('.mega-open')?.setAttribute('aria-expanded', 'false');
    }
  }

  for (const group of groups) {
    const button = group.querySelector('.mega-open');

    button.addEventListener('click', () => {
      const open = group.hasAttribute('data-open');
      close(group);
      if (open) {
        group.removeAttribute('data-open');
        button.setAttribute('aria-expanded', 'false');
      } else {
        group.setAttribute('data-open', '');
        button.setAttribute('aria-expanded', 'true');
      }
    });

    group.addEventListener('mouseenter', () => {
      // Hover only takes over once something is already open, so passing the cursor over the bar on
      // the way to the search box does not throw a full-width panel across the page.
      if (!groups.some(other => other.hasAttribute('data-open'))) return;
      close(group);
      group.setAttribute('data-open', '');
      button.setAttribute('aria-expanded', 'true');
    });
  }

  document.addEventListener('click', event => {
    if (!event.target.closest('.mega')) close(null);
  });

  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') close(null);
  });

  /* ---- omnibox -------------------------------------------------------------------------- */

  const box = document.getElementById('omnibox');
  const drop = document.getElementById('omnibox-results');
  const clear = document.querySelector('.omnibox-clear');

  if (box && drop) {
    let timer = 0;
    let sequence = 0;
    let scope = '';
    let active = -1;

    const escape = text => String(text).replace(/[&<>"']/g, c =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // The typed words are marked in the name the same way the index matched them: by prefix, on any
    // word of the name. Marking a bare substring would underline the "art" inside "Departure".
    function mark(name, words) {
      let html = '';
      const pattern = /[\p{L}\p{N}]+|[^\p{L}\p{N}]+/gu;

      for (const [run] of name.matchAll(pattern)) {
        const lower = run.toLowerCase();
        const hit = words.find(word => lower.startsWith(word));
        html += hit
          ? '<mark>' + escape(run.slice(0, hit.length)) + '</mark>' + escape(run.slice(hit.length))
          : escape(run);
      }

      return html;
    }

    function render(payload) {
      const words = payload.query.toLowerCase().split(/[^\p{L}\p{N}]+/u).filter(Boolean);
      const hits = payload.results;

      let html = '';

      if (payload.groups.length > 1) {
        html += '<div class="omnibox-chips">';
        html += `<button type="button" class="omnibox-chip" data-scope="" aria-pressed="${scope === ''}">All (${payload.total})</button>`;
        for (const group of payload.groups) {
          html += `<button type="button" class="omnibox-chip" data-scope="${escape(group.table)}" ` +
            `aria-pressed="${scope === group.table}">${escape(group.label)} (${group.count})</button>`;
        }
        html += '</div>';
      }

      if (hits.length === 0) {
        html += '<p class="omnibox-none">Nothing in the build is named that.</p>';
      }

      for (const hit of hits) {
        const name = hit.displayName ?? hit.name;
        html += `<a class="omnibox-hit" href="/entity/${encodeURIComponent(hit.table)}/${hit.netId}">` +
          `<img alt="" loading="lazy" src="${escape(hit.iconUrl)}">` +
          `<span class="t"><span class="nm">${mark(name, words)}</span>` +
          (hit.displayName ? `<span class="sub">${escape(hit.name)}</span>` : '') +
          `</span><span class="ty">${escape(hit.typeLabel)}</span></a>`;
      }

      if (payload.total > hits.length) {
        const where = '/browse?q=' + encodeURIComponent(payload.query) +
          (scope ? '&type=' + encodeURIComponent(scope) : '');
        html += `<a class="omnibox-all" href="${where}">View all ${payload.total} results</a>`;
      }

      drop.innerHTML = html;
      drop.hidden = false;
      box.setAttribute('aria-expanded', 'true');
      active = -1;

      for (const chip of drop.querySelectorAll('.omnibox-chip')) {
        chip.addEventListener('click', () => {
          scope = chip.dataset.scope;
          run(true);
        });
      }
    }

    async function run(immediate) {
      const text = box.value.trim();
      clear.hidden = text.length === 0;

      if (text.length < 2) {
        drop.hidden = true;
        box.setAttribute('aria-expanded', 'false');
        return;
      }

      const mine = ++sequence;
      const where = '/api/search?limit=12&q=' + encodeURIComponent(text) +
        (scope ? '&type=' + encodeURIComponent(scope) : '');

      try {
        const response = await fetch(where, { headers: { accept: 'application/json' } });
        if (!response.ok || mine !== sequence) return;
        render(await response.json());
      } catch {
        // A dropped request leaves the previous answer on screen rather than an error state; the
        // next keystroke asks again.
      }

      void immediate;
    }

    box.addEventListener('input', () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(run, 120);
    });

    box.addEventListener('focus', () => {
      if (drop.innerHTML && box.value.trim().length >= 2) {
        drop.hidden = false;
        box.setAttribute('aria-expanded', 'true');
      }
    });

    box.addEventListener('keydown', event => {
      const hits = [...drop.querySelectorAll('.omnibox-hit, .omnibox-all')];

      if (event.key === 'Escape') {
        drop.hidden = true;
        box.setAttribute('aria-expanded', 'false');
        return;
      }

      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp' && event.key !== 'Enter') return;
      if (hits.length === 0) return;

      if (event.key === 'Enter') {
        if (active < 0) return;
        event.preventDefault();
        hits[active].click();
        return;
      }

      event.preventDefault();
      hits[active]?.removeAttribute('data-active');
      active = event.key === 'ArrowDown'
        ? (active + 1) % hits.length
        : (active <= 0 ? hits.length - 1 : active - 1);
      hits[active].setAttribute('data-active', '');
      hits[active].scrollIntoView({ block: 'nearest' });
    });

    clear?.addEventListener('click', () => {
      box.value = '';
      clear.hidden = true;
      drop.hidden = true;
      box.focus();
    });

    document.addEventListener('click', event => {
      if (!event.target.closest('.omnibox')) {
        drop.hidden = true;
        box.setAttribute('aria-expanded', 'false');
      }
    });

    document.addEventListener('keydown', event => {
      if (event.key === '/' && document.activeElement !== box && !/^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName ?? '')) {
        event.preventDefault();
        box.focus();
        box.select();
      }
    });
  }

  /* ---- entity page: the live tooltip ---------------------------------------------------- */

  const stage = document.querySelector('[data-tooltip-for]');

  if (stage) {
    const knobs = document.querySelectorAll('[data-tooltip-knob]');

    async function draw() {
      const parameters = new URLSearchParams();

      for (const knob of knobs) {
        const value = knob.dataset.value ?? knob.value;
        if (value) parameters.set(knob.dataset.tooltipKnob, value);
      }

      const where = stage.dataset.tooltipFor + (parameters.size ? '?' + parameters : '');
      stage.setAttribute('aria-busy', 'true');

      try {
        const response = await fetch(where, { headers: { accept: 'text/html' } });
        const body = await response.text();

        if (response.ok) {
          stage.innerHTML = body;
        } else {
          let detail = body;
          try { detail = JSON.parse(body).detail ?? body; } catch { /* not a problem document */ }
          stage.innerHTML = '<p class="empty">' + detail.replace(/[<>&]/g, '') + '</p>';
        }
      } catch (error) {
        stage.innerHTML = '<p class="empty">The tooltip service did not answer.</p>';
      } finally {
        stage.removeAttribute('aria-busy');
      }
    }

    for (const knob of knobs) {
      const event = knob.tagName === 'BUTTON' ? 'click' : 'change';

      knob.addEventListener(event, () => {
        if (knob.tagName === 'BUTTON') {
          const family = [...knobs].filter(other => other.dataset.tooltipKnob === knob.dataset.tooltipKnob);
          const already = knob.getAttribute('aria-pressed') === 'true';
          for (const other of family) other.setAttribute('aria-pressed', 'false');
          knob.setAttribute('aria-pressed', already ? 'false' : 'true');
          knob.dataset.value = already ? '' : knob.dataset.choice;
          for (const other of family) if (other !== knob) other.dataset.value = '';
        }

        draw();
      });
    }

    draw();
  }
})();
