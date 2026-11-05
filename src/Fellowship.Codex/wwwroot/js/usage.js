// Usage dashboard: draws the two charts from the report's JSON and refreshes the report in place.

const host = document.getElementById('cx-usage-report');
const ranges = document.getElementById('cx-usage-ranges');
const status = document.getElementById('cx-usage-status');
const clear = document.getElementById('cx-usage-clear');
const charts = document.querySelector('.cx-usage-charts');

const css = name => getComputedStyle(charts).getPropertyValue(name).trim();

const compact = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 });
const whole = new Intl.NumberFormat();
const clock = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' });
const hourly = new Intl.DateTimeFormat(undefined, { weekday: 'short', hour: '2-digit', minute: '2-digit' });
const daily = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' });
const stamp = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' });

const formats = { minute: clock, hour: hourly, day: daily };

let timer = 0;
let drawn = {};

function data() {
    const node = host.querySelector('#cx-usage-data');

    try {
        return node ? JSON.parse(node.textContent) : null;
    } catch {
        return null;
    }
}

function options() {
    const ink = css('--ink');
    const muted = css('--muted');
    const grid = css('--line-soft');

    return {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        interaction: { mode: 'index', intersect: false },
        scales: {
            x: {
                grid: { display: false },
                border: { color: grid },
                ticks: { color: muted, maxRotation: 0, autoSkip: true, maxTicksLimit: 8 },
            },
            y: {
                beginAtZero: true,
                grid: { color: grid, lineWidth: 1 },
                border: { display: false },
                ticks: {
                    color: muted,
                    precision: 0,
                    callback: value => (value >= 10000 ? compact : whole).format(value),
                },
            },
        },
        plugins: {
            legend: {
                position: 'top',
                align: 'start',
                labels: { color: muted, usePointStyle: true, pointStyle: 'line', boxWidth: 18, padding: 14 },
            },
            tooltip: {
                backgroundColor: css('--raised'),
                borderColor: css('--line'),
                borderWidth: 1,
                titleColor: ink,
                bodyColor: muted,
                padding: 10,
                usePointStyle: true,
                callbacks: { label: item => ` ${item.dataset.label}: ${whole.format(item.parsed.y)}` },
            },
        },
    };
}

function dataset(series) {
    const colour = css(`--series-${series.slot}`);

    return {
        label: series.name,
        data: series.values,
        borderColor: colour,
        backgroundColor: colour,
        borderWidth: 2,
        borderJoinStyle: 'round',
        borderCapStyle: 'round',
        pointRadius: 0,
        pointHoverRadius: 5,
        pointHoverBorderWidth: 2,
        pointHoverBorderColor: css('--sunken'),
        tension: 0,
    };
}

// Series are matched by name, so a series hidden from the legend stays hidden after a refresh.
function draw(id, chart, unit) {
    const canvas = document.getElementById(id);
    const format = formats[unit] ?? clock;
    const labels = chart.labels.map(at => format.format(new Date(at * 1000)));

    if (!drawn[id]) {
        drawn[id] = new Chart(canvas, {
            type: 'line',
            data: { labels, datasets: chart.series.map(dataset) },
            options: options(),
        });
        legend(drawn[id]);
        drawn[id].update('none');

        return;
    }

    const target = drawn[id];
    const named = new Map(target.data.datasets.map(set => [set.label, set]));

    target.data.labels = labels;
    target.data.datasets = chart.series.map(series => {
        const existing = named.get(series.name);

        if (!existing) return dataset(series);

        existing.data = series.values;
        return existing;
    });
    legend(target);
    target.update('none');
}

// A single series has no legend box: the panel head names what is plotted, and the tooltip names the series.
function legend(chart) {
    chart.options.plugins.legend.display = chart.data.datasets.length > 1;
}

function render() {
    const current = data();

    if (!current) return;

    draw('cx-usage-live', current.live, 'minute');
    draw('cx-usage-range', current.range, current.range.unit);

    const head = document.getElementById('cx-usage-range').closest('.cx-panel')?.querySelector('.cx-panel-head');
    if (head) head.textContent = current.range.head;

    status.textContent = `Updated ${stamp.format(new Date(current.generated * 1000))}`;
    schedule(current.interval);
}

function schedule(seconds) {
    clearTimeout(timer);
    timer = setTimeout(refresh, Math.max(5, seconds) * 1000);
}

async function refresh() {
    if (document.hidden) return;

    const days = new URLSearchParams(location.search).get('days');
    const url = days ? `${host.dataset.report}?days=${encodeURIComponent(days)}` : host.dataset.report;

    try {
        const response = await fetch(url, { headers: { accept: 'text/html' }, redirect: 'manual', cache: 'no-store' });

        if (response.type === 'opaqueredirect' || response.status === 401 || response.status === 403) {
            clearTimeout(timer);
            status.textContent = 'Signed out. Reload the page to sign in.';
            return;
        }

        if (!response.ok) throw new Error(`${response.status}`);

        host.innerHTML = await response.text();
        render();
    } catch (failure) {
        status.textContent = `Refresh failed (${failure.message}). Retrying.`;
        schedule(data()?.interval ?? 10);
    }
}

ranges.addEventListener('click', event => {
    const link = event.target.closest('a[data-days]');

    if (!link) return;

    event.preventDefault();
    history.replaceState(null, '', link.href);

    for (const each of ranges.querySelectorAll('a[data-days]')) {
        if (each === link) each.setAttribute('aria-current', 'page');
        else each.removeAttribute('aria-current');
    }

    refresh();
});

clear?.addEventListener('submit', event => {
    const days = clear.querySelector('input[type=number]').value;

    if (!confirm(`Delete every count older than ${days} days? This cannot be undone.`)) event.preventDefault();
});

document.addEventListener('visibilitychange', () => {
    if (!document.hidden) refresh();
});

Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
render();
