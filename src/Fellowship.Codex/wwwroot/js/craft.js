const ROWS = {
    RandomStat: (card, at) => card.querySelector(`.stats > li[data-rolls~="${at}"]`),
    RandomRolls: (card, at) => card.querySelectorAll('.grp.mods > .mod')[at],
    GemSocket: card => card.querySelector('.sock'),
    TraitTree: card => card.querySelector('.sec.imbued'),
};

function card(layer) {
    return layer.matches?.('.cx-craft-tip') ? layer : layer.querySelector('.cx-craft-tip');
}

function box(layer, hit) {
    const drawn = card(layer);
    if (!drawn) return null;

    const find = ROWS[hit.dataset.craft];
    return find ? find(drawn, Number(hit.dataset.at ?? 0)) : null;
}

export function anchor(layer) {
    if (!layer) return;

    const frame = layer.getBoundingClientRect();

    for (const hit of layer.querySelectorAll('.cx-craft-hit')) {
        const row = box(layer, hit);

        if (!row) {
            hit.hidden = true;
            continue;
        }

        const at = row.getBoundingClientRect();

        hit.hidden = false;
        hit.style.setProperty('--x', `${Math.round(at.left - frame.left)}px`);
        hit.style.setProperty('--y', `${Math.round(at.top - frame.top)}px`);
        hit.style.setProperty('--w', `${Math.round(at.width)}px`);
        hit.style.setProperty('--h', `${Math.round(at.height)}px`);
    }
}

const watched = new WeakMap();

export function watch(layer) {
    if (!layer || watched.has(layer)) {
        anchor(layer);
        return;
    }

    const again = () => anchor(layer);
    const observer = new ResizeObserver(again);

    observer.observe(layer);
    window.addEventListener('resize', again);

    watched.set(layer, () => {
        observer.disconnect();
        window.removeEventListener('resize', again);
    });

    anchor(layer);
}

export function unwatch(layer) {
    const stop = watched.get(layer);

    if (stop) {
        stop();
        watched.delete(layer);
    }
}

export function show(dialog) {
    if (dialog && !dialog.open) dialog.showModal();
}

export function copy(text) {
    return navigator.clipboard?.writeText(text);
}
