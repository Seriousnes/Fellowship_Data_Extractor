const SHOW = 150;
const HIDE = 100;
const LINGER = 260;
const DESKTOP = '(min-width: 900px)';
const HOVER = '(hover: hover)';

const BASE = `
:host { display: block }
`;

function paint(host, html) {
    const root = host.shadowRoot ?? host.attachShadow({ mode: 'open' });
    root.innerHTML = `<style>${BASE}</style>${html}`;
    return root;
}

function wipe(host) {
    if (host?.shadowRoot) host.shadowRoot.replaceChildren();
}

const responses = new Map();

function fragment(url, request) {
    const key = `${url}\n${request ?? ''}`;

    if (!responses.has(key)) {
        responses.set(key, fetch(url, {
            method: 'QUERY',
            headers: request
                ? { accept: 'text/html', 'content-type': 'application/json' }
                : { accept: 'text/html' },
            body: request ?? undefined,
        })
            .then(async response => ({ ok: response.ok, body: await response.text() }))
            .catch(() => null));
    }

    return responses.get(key);
}

const isDesktop = () => window.matchMedia(DESKTOP).matches;
const canHover = () => window.matchMedia(HOVER).matches;

function lock(held) {
    document.documentElement.style.overflow = held ? 'hidden' : '';
}

let waiting = 0;
let clearing = 0;
let sequence = 0;
let anchored = null;
let pointer = null;
let modal = false;

const layer = () => document.getElementById('cx-tip');

function place(link) {
    const box = layer();
    if (!box || !link.isConnected) return;

    const at = link.getBoundingClientRect();
    const width = box.offsetWidth;
    const height = box.offsetHeight;
    const mouse = pointer?.link === link ? { x: at.left + pointer.x, y: at.top + pointer.y } : null;

    let x = (mouse?.x ?? at.right) + 14;
    if (x + width > window.innerWidth - 8) x = (mouse?.x ?? at.left) - width - 14;
    if (x < 8) x = Math.max(8, window.innerWidth - width - 8);

    let y = mouse ? mouse.y + 14 : at.top - 4;
    if (y + height > window.innerHeight - 8) y = window.innerHeight - height - 8;
    if (y < 8) y = 8;

    box.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}

function draw(link) {
    const url = link.dataset.tip;
    if (!url || modal) return;

    window.clearTimeout(clearing);
    if (anchored === link) return;

    window.clearTimeout(waiting);
    waiting = window.setTimeout(async () => {
        const mine = ++sequence;
        const response = await fragment(url, link.dataset.tipRequest);

        if (mine !== sequence || !response?.ok || modal) return;

        const box = layer();
        if (!box) return;

        paint(box, response.body);
        box.hidden = false;
        anchored = link;
        place(link);
    }, SHOW);
}

function undraw() {
    if (modal) return;

    window.clearTimeout(waiting);
    sequence++;

    window.clearTimeout(clearing);
    clearing = window.setTimeout(() => {
        const box = layer();
        if (box) {
            box.hidden = true;
            wipe(box);
        }
        anchored = null;
    }, HIDE);
}

function hide() {
    window.clearTimeout(waiting);
    window.clearTimeout(clearing);
    sequence++;

    const box = layer();
    if (box) {
        box.hidden = true;
        box.removeAttribute('data-modal');
        box.style.transform = '';
        wipe(box);
    }

    anchored = null;

    if (modal) {
        modal = false;
        lock(false);
    }
}

function withMobile(request) {
    return JSON.stringify({ ...JSON.parse(request || '{}'), mobile: true });
}

async function present(link) {
    const url = link.dataset.tip;
    if (!url) return;

    hide();

    const mine = ++sequence;
    const response = await fragment(url, withMobile(link.dataset.tipRequest));

    if (mine !== sequence || !response?.ok) return;

    const box = layer();
    if (!box) return;

    const root = paint(box, response.body);
    const open = root.querySelector('.fx-modal .open');
    if (open && link.href) open.href = link.href;

    modal = true;
    box.setAttribute('data-modal', '');
    box.hidden = false;
    lock(true);

    root.querySelector('.fx-modal .shut')?.focus();
}

const menus = () => document.querySelector('.cx-menus');

function shutMenus() {
    for (const opened of document.querySelectorAll('.cx-menu[open], .cx-menu-fly[open]')) {
        opened.removeAttribute('open');
        opened.removeAttribute('data-flip');
    }
}

function shutDrawer() {
    const nav = menus();
    if (!nav?.hasAttribute('data-open')) return;

    nav.removeAttribute('data-open');
    nav.querySelector('.cx-menus-toggle')?.setAttribute('aria-expanded', 'false');
    lock(false);
}

function openDrawer() {
    const nav = menus();
    if (!nav) return;

    nav.setAttribute('data-open', '');
    nav.querySelector('.cx-menus-toggle')?.setAttribute('aria-expanded', 'true');
    lock(true);
    nav.querySelector('.cx-menus-shut')?.focus();
}

function shutAll() {
    shutMenus();
    shutDrawer();
}

function flip(fly) {
    fly.removeAttribute('data-flip');

    const panel = fly.querySelector(':scope > .cx-menu-fly-panel');
    if (!panel) return;

    if (panel.getBoundingClientRect().right > window.innerWidth - 8) fly.setAttribute('data-flip', '');
}

function nudge(menu) {
    const panel = menu.querySelector(':scope > .cx-menu-panel');
    if (!panel) return;

    panel.style.transform = '';

    const at = panel.getBoundingClientRect();
    const over = at.right - (window.innerWidth - 8);

    if (over > 0) panel.style.transform = `translateX(${Math.round(-Math.min(over, at.left - 8))}px)`;
}

function openMenu(menu) {
    for (const other of document.querySelectorAll('.cx-menu[open]')) {
        if (other !== menu) other.removeAttribute('open');
    }

    menu.setAttribute('open', '');
    if (isDesktop()) nudge(menu);
}

function shutFlies(keep) {
    for (const other of document.querySelectorAll('.cx-menu-fly[open]')) {
        if (!other.contains(keep)) other.removeAttribute('open');
    }
}

function openFly(fly) {
    shutFlies(fly);

    fly.setAttribute('open', '');
    if (isDesktop()) flip(fly);
}

let lingering = 0;

function linger() {
    window.clearTimeout(lingering);
    lingering = window.setTimeout(shutMenus, LINGER);
}

function stay() {
    window.clearTimeout(lingering);
}

function siblings(link) {
    const list = link.closest('ul');
    return list ? [...list.querySelectorAll(':scope > li > a')] : [link];
}

function coarse(event) {
    return event.pointerType ? event.pointerType !== 'mouse' : !canHover();
}

function kindsOf(select) {
    select.kinds ??= new Map([...select.querySelectorAll('optgroup')]
        .map(group => [group.querySelector('option')?.value.split('-')[0], group.label]));
    return select.kinds;
}

function offer(select) {
    const kinds = kindsOf(select);
    const panel = select.closest('.cx-panel');

    for (const group of select.querySelectorAll('optgroup')) group.remove();

    let current = null;

    for (const row of panel.querySelectorAll('[data-more][hidden]')) {
        const label = kinds.get(row.dataset.more.split('-')[0]);

        if (!current || current.label !== label) {
            current = document.createElement('optgroup');
            current.label = label;
            select.append(current);
        }

        const option = document.createElement('option');
        option.value = row.dataset.more;
        option.textContent = row.querySelector('summary').firstChild.textContent.trim();
        current.append(option);
    }
}

function reveal(select) {
    const row = select.closest('.cx-panel')?.querySelector(`[data-more="${CSS.escape(select.value)}"]`);
    select.value = '';

    if (!row) return;

    row.hidden = false;
    row.open = true;
    offer(select);
    row.querySelector('a, input')?.focus();
}

function conceal(button) {
    const row = button.closest('[data-more]');
    const select = row.closest('.cx-panel').querySelector('.cx-more');

    row.hidden = true;
    for (const input of row.querySelectorAll('input')) input.value = '';
    offer(select);
    select.focus();
}

function fold(toggle) {
    const panel = toggle.closest('.cx-panel');
    toggle.setAttribute('aria-expanded', String(panel.toggleAttribute('data-open')));
}

document.addEventListener('change', event => {
    const select = event.target;
    if (select instanceof HTMLSelectElement && select.matches('.cx-more') && select.value) reveal(select);
});

document.addEventListener('pointerover', event => {
    if (event.pointerType && event.pointerType !== 'mouse') return;

    const nav = event.target.closest?.('.cx-menus');

    if (nav && isDesktop()) {
        stay();

        const opener = event.target.closest('.cx-menu-open');
        const menu = opener?.closest('.cx-menu');

        if (menu && document.querySelector('.cx-menu[open]') && !menu.hasAttribute('open')) openMenu(menu);

        const row = event.target.closest('.cx-menu-row');
        const fly = row?.querySelector(':scope > .cx-menu-fly');

        if (row && fly && !fly.hasAttribute('open')) openFly(fly);

        if (row && !fly) shutFlies(row);
    }

    const link = event.target.closest?.('[data-tip]');
    if (link) draw(link);
});

document.addEventListener('pointermove', event => {
    if (event.pointerType && event.pointerType !== 'mouse') return;

    const link = event.target.closest?.('[data-tip]');
    if (!link || anchored === link) return;

    const at = link.getBoundingClientRect();
    pointer = { link, x: event.clientX - at.left, y: event.clientY - at.top };
}, { passive: true });

document.addEventListener('pointerout', event => {
    if (event.pointerType && event.pointerType !== 'mouse') return;

    if (event.target.closest?.('[data-tip]')) undraw();

    const nav = event.target.closest?.('.cx-menus');

    if (nav && isDesktop() && !nav.contains(event.relatedTarget)) linger();
});

document.addEventListener('focusin', event => {
    if (!canHover()) return;

    const link = event.target.closest?.('[data-tip]');
    if (link) draw(link);
});

document.addEventListener('focusout', event => {
    if (event.target.closest?.('[data-tip]')) undraw();
});

document.addEventListener('toggle', event => {
    const target = event.target;
    if (!(target instanceof HTMLDetailsElement) || !target.open || !isDesktop()) return;

    if (target.matches('.cx-menu')) nudge(target);
    if (target.matches('.cx-menu-fly')) flip(target);
}, true);

document.addEventListener('click', event => {
    const path = event.composedPath();

    if (path[0] instanceof Element && path[0].hasAttribute('data-dismiss')) {
        event.preventDefault();
        event.stopPropagation();
        hide();
        return;
    }

    if (modal && path.some(node => node instanceof Element && node.matches?.('.fx-modal .open'))) {
        hide();
        return;
    }

    const link = event.target.closest?.('[data-tip]');

    if (link && coarse(event) && !event.target.closest('.cx-menus, .cx-sel, [data-hover-tip]')) {
        event.preventDefault();
        event.stopPropagation();
        present(link);
    }
}, true);

document.addEventListener('click', event => {
    const copier = event.target.closest?.('[data-copy]');

    if (copier) {
        navigator.clipboard?.writeText(copier.dataset.copy || window.location.href);
        return;
    }

    const folder = event.target.closest?.('.cx-panel-toggle');

    if (folder) {
        fold(folder);
        return;
    }

    const remover = event.target.closest?.('button.cx-facet-off');

    if (remover) {
        event.preventDefault();
        conceal(remover);
        return;
    }

    const toggle = event.target.closest?.('.cx-menus-toggle');

    if (toggle) {
        if (menus()?.hasAttribute('data-open')) shutDrawer(); else openDrawer();
        return;
    }

    if (event.target.closest?.('.cx-menus-shut')) {
        shutAll();
        return;
    }

    const nav = event.target.closest?.('.cx-menus');

    if (!nav) {
        shutMenus();
        return;
    }

    if (event.target === nav) {
        shutAll();
        return;
    }

    if (event.target.closest('a[href]')) shutAll();
});

document.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
        const opened = document.querySelector('.cx-menu[open] > .cx-menu-open');

        shutAll();
        hide();

        if (opened instanceof HTMLElement) opened.focus();
        return;
    }

    const nav = event.target.closest?.('.cx-menus');

    if (nav && isDesktop()) {
        const opener = event.target.closest('.cx-menu-open');

        if (opener) {
            const menu = opener.closest('.cx-menu');
            const all = [...nav.querySelectorAll('.cx-menu-open')];
            const at = all.indexOf(opener);

            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                openMenu(menu);
                const first = menu.querySelector('.cx-menu-panel a');
                if (first instanceof HTMLElement) first.focus();
                return;
            }

            if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
                event.preventDefault();
                const next = all[(at + (event.key === 'ArrowRight' ? 1 : all.length - 1)) % all.length];
                const wasOpen = menu.hasAttribute('open');
                next.focus();
                if (wasOpen) openMenu(next.closest('.cx-menu'));
                return;
            }

            return;
        }

        const link = event.target.closest('a');
        if (!link) return;

        const row = link.closest('.cx-menu-row');
        const fly = row?.querySelector(':scope > .cx-menu-fly');
        const inside = link.closest('.cx-menu-fly-panel');

        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            const list = siblings(link);
            const at = list.indexOf(link);
            const next = list[(at + (event.key === 'ArrowDown' ? 1 : list.length - 1)) % list.length];
            next?.focus();
            return;
        }

        if (event.key === 'ArrowRight' && fly && link === row.querySelector(':scope > a')) {
            event.preventDefault();
            openFly(fly);
            const first = fly.querySelector('.cx-menu-fly-panel a');
            if (first instanceof HTMLElement) first.focus();
            return;
        }

        if (event.key === 'ArrowLeft' && inside) {
            event.preventDefault();
            const owner = inside.closest('.cx-menu-fly');
            owner?.removeAttribute('open');
            const back = owner?.closest('.cx-menu-row')?.querySelector(':scope > a');
            if (back instanceof HTMLElement) back.focus();
            return;
        }

        return;
    }

    if (event.key !== '/') return;

    const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName ?? '');
    if (typing) return;

    const box = document.getElementById('cx-omnibox');
    if (!box) return;

    event.preventDefault();
    box.focus();
    box.select();
});

// The omnibox is prerendered, and the circuit replaces the prerendered input when it starts. Text typed
// into the prerendered input is held here until the interactive omnibox adopts it.
const adopted = new WeakSet();
let carried = null;

function hold(box) {
    if (!(box instanceof HTMLInputElement) || box.id !== 'cx-omnibox' || adopted.has(box)) return;
    carried = box.value ? { text: box.value, focused: document.activeElement === box } : null;
}

hold(document.getElementById('cx-omnibox'));

document.addEventListener('input', event => hold(event.target));

export function adopt(box) {
    adopted.add(box);
    const taken = carried;
    carried = null;
    return taken;
}

window.addEventListener('scroll', () => { if (anchored) place(anchored); }, { passive: true, capture: true });

window.addEventListener('resize', () => {
    if (!modal) hide();
    if (isDesktop()) shutDrawer();
});

const KATEX = 'https://cdnjs.cloudflare.com/ajax/libs/KaTeX/0.16.11/';
const KATEX_SCRIPT = 'sha512-UPFOadao2nEorjtjl0xUTtN3w20Ja143UPEU6EyJ1mi5MB2bDtMkiWmqGDqi47xNLB5z1dy31GKJDEWhjUJFiQ==';
const KATEX_STYLE = 'sha512-aOFyMMzZF7l7ei3vOKgQiRhZnYqk9YC/uMzl4T0j5N5D3Kul+QAFU8ssENDRMAqr/lxDOjMF69dSYJ8HYqY+WQ==';

let katex = null;

function fetched(tag, attributes) {
    return new Promise((resolve, reject) => {
        const node = Object.assign(document.createElement(tag), attributes, { crossOrigin: 'anonymous' });
        node.addEventListener('load', resolve, { once: true });
        node.addEventListener('error', reject, { once: true });
        document.head.append(node);
    });
}

function typesetter() {
    katex ??= Promise.all([
        fetched('link', { rel: 'stylesheet', href: `${KATEX}katex.min.css`, integrity: KATEX_STYLE }),
        fetched('script', { src: `${KATEX}katex.min.js`, integrity: KATEX_SCRIPT }),
    ]).then(() => window.katex).catch(() => (katex = null));

    return katex;
}

async function typeset() {
    const formulas = [...document.querySelectorAll('[data-latex]')].filter(node => node.dataset.latex);
    if (formulas.length === 0) return;

    const renderer = await typesetter();
    if (!renderer) return;

    for (const node of formulas) {
        renderer.render(node.dataset.latex, node, { displayMode: true, throwOnError: false });
    }
}

function arrived() {
    hide();
    shutAll();
    typeset();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', arrived);
} else {
    arrived();
}

window.Blazor?.addEventListener('enhancedload', arrived);
