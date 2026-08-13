const SHOW = 150;
const HIDE = 100;

const BASE = `
:host { display: block }
.said { margin: 0; padding: 10px 2px; color: var(--muted); font: 400 13px/1.5 var(--sans) }
`;

function paint(host, html) {
    const root = host.shadowRoot ?? host.attachShadow({ mode: 'open' });
    root.innerHTML = `<style>${BASE}</style>${html}`;
    return root;
}

function wipe(host) {
    if (host?.shadowRoot) host.shadowRoot.replaceChildren();
}

const answers = new Map();

function fragment(url) {
    if (!answers.has(url)) {
        answers.set(url, fetch(url, { headers: { accept: 'text/html' } })
            .then(async response => ({ ok: response.ok, body: await response.text() }))
            .catch(() => null));
    }

    return answers.get(url);
}

let waiting = 0;
let clearing = 0;
let sequence = 0;
let anchored = null;

const layer = () => document.getElementById('cx-tip');

function place(link) {
    const box = layer();
    if (!box || !link.isConnected) return;

    const at = link.getBoundingClientRect();
    const width = box.offsetWidth;
    const height = box.offsetHeight;

    let x = at.right + 14;
    if (x + width > window.innerWidth - 8) x = at.left - width - 14;
    if (x < 8) x = Math.max(8, window.innerWidth - width - 8);

    let y = at.top - 4;
    if (y + height > window.innerHeight - 8) y = window.innerHeight - height - 8;
    if (y < 8) y = 8;

    box.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}

function draw(link) {
    const url = link.dataset.tip;
    if (!url) return;

    window.clearTimeout(clearing);
    if (anchored === link) return;

    window.clearTimeout(waiting);
    waiting = window.setTimeout(async () => {
        const mine = ++sequence;
        const answer = await fragment(url);

        if (mine !== sequence || !answer?.ok) return;

        const box = layer();
        if (!box) return;

        paint(box, answer.body);
        box.hidden = false;
        anchored = link;
        place(link);
    }, SHOW);
}

function undraw() {
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
        wipe(box);
    }
    anchored = null;
}

const groups = () => [...document.querySelectorAll('.cx-mega-group')];

function shut(except) {
    for (const group of groups()) {
        if (group === except) continue;
        group.removeAttribute('data-open');
        group.querySelector('.cx-mega-open')?.setAttribute('aria-expanded', 'false');
    }
}

function open(group) {
    shut(group);
    group.setAttribute('data-open', '');
    group.querySelector('.cx-mega-open')?.setAttribute('aria-expanded', 'true');
}

document.addEventListener('pointerover', event => {
    if (event.pointerType && event.pointerType !== 'mouse') return;

    const group = event.target.closest?.('.cx-mega-group');

    if (group && document.querySelector('.cx-mega-group[data-open]')) open(group);

    const link = event.target.closest?.('[data-tip]');
    if (link) draw(link);
});

document.addEventListener('pointerout', event => {
    if (event.target.closest?.('[data-tip]')) undraw();
});

document.addEventListener('focusin', event => {
    const link = event.target.closest?.('[data-tip]');
    if (link) draw(link);
});

document.addEventListener('focusout', event => {
    if (event.target.closest?.('[data-tip]')) undraw();
});

document.addEventListener('click', event => {
    const opener = event.target.closest?.('.cx-mega-open');

    if (opener) {
        const group = opener.closest('.cx-mega-group');
        if (group.hasAttribute('data-open')) shut(null); else open(group);
        return;
    }

    if (!event.target.closest?.('.cx-mega')) shut(null);
});

document.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
        shut(null);
        hide();
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

window.addEventListener('scroll', () => { if (anchored) place(anchored); }, { passive: true });
window.addEventListener('resize', hide);

function arrived() {
    hide();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', arrived);
} else {
    arrived();
}

window.Blazor?.addEventListener('enhancedload', arrived);
