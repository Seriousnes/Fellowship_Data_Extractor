// Data grid for the usage dashboard. It draws only the rows in view, so every row a report sends fits.
// Filters, sort and scroll position belong to the grid, and a refresh hands it new rows.

const rowHeight = 29;
const overscan = 12;
const widthSample = 400;
const widestColumn = 460;
const charWidth = 7.4;
const cellPadding = 24;

const count = new Intl.NumberFormat();
const stamp = new Intl.DateTimeFormat(undefined, {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit',
});

function element(tag, properties, ...children) {
    const node = Object.assign(document.createElement(tag), properties);

    node.append(...children);
    return node;
}

const payload = root => root.previousElementSibling.textContent;

// A column's filter: `!text` excludes, a comparison reads the cell's number, and any other text is a substring.
function matcher(query, keyed) {
    const text = query.trim().toLowerCase();

    if (!text) return null;

    const compared = keyed && /^(>=|<=|>|<|=)\s*(-?[\d.,]+)$/.exec(text);

    if (compared) {
        const [, operator, digits] = compared;
        const limit = Number(digits.replaceAll(',', ''));
        const tests = {
            '>': key => key > limit,
            '<': key => key < limit,
            '>=': key => key >= limit,
            '<=': key => key <= limit,
            '=': key => key === limit,
        };

        return row => row.key !== null && tests[operator](row.key);
    }

    if (text.startsWith('!') && text.length > 1) {
        const excluded = text.slice(1);

        return row => !row.text.includes(excluded);
    }

    return row => row.text.includes(text);
}

function ascending(left, right) {
    if (left === right) return 0;
    if (left === null || left === undefined) return 1;
    if (right === null || right === undefined) return -1;

    return left < right ? -1 : 1;
}

export class Grid {
    constructor(root, text) {
        this.root = root;
        this.columns = [];
        this.rows = [];
        this.shown = [];
        this.filters = new Map();
        this.sort = null;
        this.drawn = '';
        this.selected = null;
        this.offset = [0, 0];
        this.held = null;

        this.adopt(text);
        this.build();
        this.apply();
    }

    // A new root is from the circuit's first render, or a render that added the grid again. The filters and sort move to it.
    move(root) {
        this.root = root;
        root.replaceChildren(this.scroll, this.foot, this.detail);
    }

    // Leaving the document clears the scroll offset and the focus, so both come from what was last recorded.
    settle() {
        [this.scroll.scrollTop, this.scroll.scrollLeft] = this.offset;

        if (this.held) {
            const { input, start, end } = this.held;

            input.focus({ preventScroll: true });
            input.setSelectionRange(start, end);
        }

        this.drawn = '';
        this.paint();
    }

    hold({ target }) {
        if (target instanceof HTMLInputElement) {
            this.held = { input: target, start: target.selectionStart, end: target.selectionEnd };
        }
    }

    update(text) {
        const before = this.visible;

        this.adopt(text);

        if (before.map(column => column.head).join() === this.visible.map(column => column.head).join()) {
            this.visible.forEach((column, at) => Object.assign(column, {
                heading: before[at].heading,
                input: before[at].input,
            }));
        } else {
            this.head.replaceChildren(this.headings(), this.inputs());
            this.size();

            if (!this.columns.some(column => column.head === this.sort?.column.head)) this.sort = null;
        }

        this.apply();
    }

    adopt(text) {
        const { columns, rows } = JSON.parse(text);

        this.text = text;
        this.columns = columns.map((column, index) => ({ ...column, index }));
        this.visible = this.columns.filter(column => !column.hidden);

        this.rows = rows.map((cells, at) => {
            const keys = this.columns.map(column => column.keys?.[at] ?? null);
            const texts = cells.map((cell, index) =>
                this.columns[index].time && keys[index] !== null ? stamp.format(new Date(keys[index])) : cell);

            return { texts, keys, lower: texts.map(text => (text ?? '').toLowerCase()) };
        });
    }

    build() {
        this.scroll = element('div', { className: 'cx-grid-scroll', tabIndex: 0 });
        this.head = element('thead');
        this.body = element('tbody');
        this.table = element('table', {}, element('colgroup'), this.head, this.body);
        this.foot = element('div', { className: 'cx-grid-foot', role: 'status' });
        this.detail = element('dl', { className: 'cx-grid-detail', hidden: true });

        this.scroll.append(this.table);
        this.root.replaceChildren(this.scroll, this.foot, this.detail);
        this.head.append(this.headings(), this.inputs());
        this.size();

        this.scroll.addEventListener('scroll', () => {
            this.offset = [this.scroll.scrollTop, this.scroll.scrollLeft];
            this.paint();
        }, { passive: true });

        for (const kind of ['focusin', 'input', 'keyup', 'pointerup']) {
            this.head.addEventListener(kind, event => this.hold(event));
        }

        // Chrome fires focusout on an input while removing it, so focus is kept for an input that left the document.
        this.head.addEventListener('focusout', ({ target }) => queueMicrotask(() => {
            if (target.isConnected && document.activeElement !== target) this.held = null;
        }));

        this.body.addEventListener('click', event => this.open(event.target.closest('tr.cx-grid-row')));
        this.body.addEventListener('keydown', event => {
            if (event.key === 'Enter') this.open(event.target.closest('tr.cx-grid-row'));
        });
    }

    headings() {
        return element('tr', {}, ...this.visible.map(column => {
            const button = element('button', { type: 'button', textContent: column.head });
            const heading = element('th', { scope: 'col', className: column.numeric ? 'cx-numeric' : '' }, button);

            button.addEventListener('click', () => this.order(column));
            column.heading = heading;
            return heading;
        }));
    }

    inputs() {
        return element('tr', { className: 'cx-grid-filters' }, ...this.visible.map(column => {
            const input = element('input', {
                type: 'search',
                placeholder: column.numeric ? '>100' : 'Filter',
                title: 'Text matches anywhere. !text excludes. On a number, >, <, >=, <= and = compare.',
                ariaLabel: `Filter ${column.head}`,
                value: this.filters.get(column.head) ?? '',
            });

            input.addEventListener('input', () => {
                this.filters.set(column.head, input.value);
                this.apply();
            });
            column.input = input;
            return element('th', { scope: 'col' }, input);
        }));
    }

    // Columns are as wide as their longest sampled text, so the widths hold still as rows arrive.
    size() {
        const widths = this.visible.map(column => {
            let longest = column.head.length + 3;

            for (const row of this.rows.slice(0, widthSample)) {
                longest = Math.max(longest, (row.texts[column.index] ?? '').length + (column.unit ? 3 : 0));
            }

            return Math.min(widestColumn, Math.round(longest * charWidth) + cellPadding);
        });

        this.table.querySelector('colgroup').replaceChildren(
            ...widths.map(width => element('col', { style: `width:${width}px` })));
        this.table.style.width = `max(100%, ${widths.reduce((sum, width) => sum + width, 0)}px)`;
        this.sized = this.rows.length > 0;
    }

    order(column) {
        const first = column.keys ? 'descending' : 'ascending';
        const second = column.keys ? 'ascending' : 'descending';
        const same = this.sort?.column.head === column.head;
        const direction = !same ? first : this.sort.direction === first ? second : null;

        this.sort = direction ? { column, direction } : null;
        this.apply();
    }

    apply() {
        if (!this.sized) this.size();

        const tests = this.visible.flatMap(column => {
            const test = matcher(this.filters.get(column.head) ?? '', Boolean(column.keys));

            return test ? [{ index: column.index, test }] : [];
        });

        const probe = { key: null, text: '' };
        const kept = this.rows.filter(row => tests.every(({ index, test }) => {
            probe.key = row.keys[index];
            probe.text = row.lower[index];
            return test(probe);
        }));

        if (this.sort) {
            const { column, direction } = this.sort;
            const index = this.columns.findIndex(each => each.head === column.head);
            const keyed = Boolean(this.columns[index]?.keys);
            const value = row => (keyed ? row.keys[index] : row.lower[index]);
            const sign = direction === 'ascending' ? 1 : -1;

            kept.sort((left, right) => {
                const [one, two] = [value(left), value(right)];

                if (one === null || two === null) return ascending(one, two);

                return sign * ascending(one, two);
            });
        }

        this.shown = kept;
        this.marks();
        this.summarise();
        this.drawn = '';
        this.paint();
    }

    marks() {
        for (const column of this.visible) {
            if (this.sort?.column.head === column.head) column.heading.setAttribute('aria-sort', this.sort.direction);
            else column.heading.removeAttribute('aria-sort');
        }
    }

    summarise() {
        const totals = this.visible.filter(column => column.total).map(column => {
            const sum = this.shown.reduce((total, row) => total + (row.keys[column.index] ?? 0), 0);

            return element('span', {}, `${column.head} `, element('b', { textContent: count.format(sum) }));
        });

        const filtered = [...this.filters.values()].some(query => query.trim());
        const clear = element('button', { type: 'button', textContent: 'Clear filters', hidden: !filtered });

        clear.addEventListener('click', () => {
            this.filters.clear();
            for (const column of this.visible) column.input.value = '';
            this.apply();
        });

        const noun = this.rows.length === 1 ? 'row' : 'rows';
        const rows = filtered
            ? `${count.format(this.shown.length)} of ${count.format(this.rows.length)} ${noun}`
            : `${count.format(this.rows.length)} ${noun}`;

        this.foot.replaceChildren(element('span', { textContent: rows }), ...totals, clear);
    }

    paint() {
        const height = this.scroll.clientHeight || rowHeight * 20;
        const first = Math.max(0, Math.floor(this.scroll.scrollTop / rowHeight) - overscan);
        const last = Math.min(this.shown.length, first + Math.ceil(height / rowHeight) + overscan * 2);
        const range = `${first}:${last}`;

        if (range === this.drawn) return;

        this.drawn = range;
        this.body.replaceChildren(
            this.spacer(first * rowHeight),
            ...this.shown.slice(first, last).map(row => this.line(row)),
            this.spacer((this.shown.length - last) * rowHeight));
    }

    spacer(height) {
        const cell = element('td', { colSpan: this.visible.length, style: `height:${height}px` });

        return element('tr', { className: 'cx-grid-spacer', ariaHidden: 'true' }, cell);
    }

    line(row) {
        const line = element('tr', { className: 'cx-grid-row', tabIndex: -1 }, ...this.visible.map(column => {
            const text = row.texts[column.index] ?? '';
            const cell = element('td', { title: text, textContent: text });

            cell.className = [column.numeric ? 'cx-numeric' : '', column.mono ? 'cx-mono' : ''].join(' ').trim();

            if (column.unit && text) cell.append(' ', element('span', { className: 'cx-unit', textContent: column.unit }));

            return cell;
        }));

        line.row = row;
        if (row === this.selected) line.setAttribute('aria-selected', 'true');

        return line;
    }

    open(line) {
        if (!line) return;

        this.selected = line.row === this.selected ? null : line.row;
        this.drawn = '';
        this.paint();

        if (!this.selected) {
            this.detail.hidden = true;
            return;
        }

        const close = element('button', { type: 'button', textContent: 'Close' });

        close.addEventListener('click', () => this.open({ row: this.selected }));
        this.detail.replaceChildren(close, ...this.columns.flatMap(column => [
            element('dt', { textContent: column.head }),
            element('dd', { textContent: this.selected.texts[column.index] ?? '' }),
        ]));
        this.detail.hidden = false;
    }

}

// Draws each grid in a host, and hands a live grid the rows of its latest render.
export function mount(host, grids) {
    const roots = [...host.querySelectorAll('[data-grid]')];

    // Every grid moves before anything reads layout, because the page is short until the last one is back.
    const moved = roots.filter(root => {
        const live = grids.get(root.dataset.grid);

        if (!live || live.root === root) return false;

        live.move(root);
        return true;
    });

    for (const root of roots) {
        const live = grids.get(root.dataset.grid);
        const text = payload(root);

        if (!live) grids.set(root.dataset.grid, new Grid(root, text));
        else if (live.text !== text) live.update(text);
    }

    for (const root of moved) grids.get(root.dataset.grid).settle();
}
