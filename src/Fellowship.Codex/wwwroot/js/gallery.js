// A gallery's dialog: its stacked preview opens it, the chevrons and arrow keys turn its images, and it never turns on its own.

const tips = () => document.getElementById('cx-tip');

let home = null;

function slides(dialog) {
    return [...dialog.querySelectorAll('[data-gallery-slide]')];
}

function turn(dialog, by) {
    const all = slides(dialog);
    if (all.length === 0) return;

    const at = Math.max(0, all.findIndex(slide => !slide.hidden));
    const next = (at + by + all.length) % all.length;

    all.forEach((slide, index) => { slide.hidden = index !== next; });

    const title = dialog.querySelector('[data-gallery-title]');
    if (title) title.textContent = all[next].dataset.name ?? '';

    const position = dialog.querySelector('[data-gallery-at]');
    if (position) position.textContent = `${next + 1} / ${all.length}`;
}

function open(gallery) {
    const dialog = gallery.querySelector('dialog');
    if (!dialog || dialog.open) return;

    turn(dialog, -Math.max(0, slides(dialog).findIndex(slide => !slide.hidden)));

    // A modal dialog sits above every other layer, so the tooltip layer moves inside it while it is open.
    const layer = tips();
    if (layer) {
        home = { parent: layer.parentNode, next: layer.nextSibling };
        dialog.append(layer);
    }

    dialog.showModal();
}

document.addEventListener('click', event => {
    const opener = event.target.closest?.('[data-gallery-open]');

    if (opener) {
        open(opener.closest('.cx-gallery'));
        return;
    }

    const dialog = event.target.closest?.('.cx-gallery-dialog');
    if (!dialog) return;

    const turner = event.target.closest('[data-gallery-turn]');

    if (turner) {
        turn(dialog, Number(turner.dataset.galleryTurn));
        return;
    }

    if (event.target.closest('[data-gallery-close]') || event.target === dialog) dialog.close();
});

document.addEventListener('keydown', event => {
    const dialog = document.querySelector('.cx-gallery-dialog[open]');
    if (!dialog) return;

    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
        event.preventDefault();
        turn(dialog, event.key === 'ArrowLeft' ? -1 : 1);
    }
});

document.addEventListener('close', event => {
    if (!event.target.matches?.('.cx-gallery-dialog')) return;

    const layer = tips();
    if (!layer || !event.target.contains(layer)) return;

    (home?.parent ?? document.body).insertBefore(layer, home?.next ?? null);
    home = null;
}, true);
