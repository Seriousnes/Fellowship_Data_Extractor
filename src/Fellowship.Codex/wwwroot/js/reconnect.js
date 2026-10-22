const SCHEDULE = [0, 0, 1000, 2000, 3000, 5000, 8000, 13000, 21000];
const CEILING = 30000;
const RETRIES = 120;

const LABELS = {
    show: 'Reconnecting',
    retrying: 'Reconnecting',
    paused: 'Paused. Tap to resume',
    failed: 'Connection lost. Tap to retry',
    'resume-failed': 'Connection lost. Tap to retry',
    rejected: 'Session expired. Tap to reload',
};

const DOWN = new Set(['show', 'retrying', 'paused', 'failed', 'resume-failed']);
const STOPPED = new Set(['paused', 'failed', 'resume-failed']);

let state = 'hide';
let rejoining = false;

const display = () => document.getElementById('components-reconnect-modal');

function interval(attempt) {
    return attempt < SCHEDULE.length ? SCHEDULE[attempt] : CEILING;
}

function reload() {
    if (document.visibilityState === 'visible') location.reload();
}

async function rejoin() {
    if (rejoining) return;
    rejoining = true;

    try {
        const rejoined = state === 'paused'
            ? await Blazor.resumeCircuit()
            : await Blazor.reconnect() || await Blazor.resumeCircuit();

        if (!rejoined) {
            state = 'rejected';
            reload();
        }
    } catch {
    } finally {
        rejoining = false;
    }
}

function label(element) {
    const button = element.querySelector('button');
    const text = LABELS[state];

    if (!button || !text) return;

    button.setAttribute('aria-label', text);
    button.title = text;
}

function changed(event) {
    state = event.detail.state;
    label(event.currentTarget);

    if (state === 'rejected') reload();
}

function act() {
    if (state === 'rejected') {
        location.reload();
        return;
    }

    if (DOWN.has(state)) rejoin();
}

document.addEventListener('visibilitychange', () => {
    if (document.visibilityState !== 'visible') return;

    if (state === 'rejected') {
        location.reload();
        return;
    }

    if (STOPPED.has(state)) rejoin();
});

window.addEventListener('online', () => {
    if (DOWN.has(state)) rejoin();
});

const element = display();

element?.addEventListener('components-reconnect-state-changed', changed);
element?.querySelector('button')?.addEventListener('click', act);

Blazor.start({
    circuit: {
        reconnectionOptions: {
            maxRetries: RETRIES,
            retryIntervalMilliseconds: interval,
        },
    },
});
