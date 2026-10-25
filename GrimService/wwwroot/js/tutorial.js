(() => {
    'use strict';
    if (!document.querySelector('[data-tutorial-root]') || !window.fetch || !window.AbortController) return;
    const t = source => window.GrimI18n ? window.GrimI18n.t(source) : source;
    let pending;
    let sequence = 0;
    const scrollState = () => ({ ...(history.state || {}), tutorialScroll: window.scrollY });
    history.replaceState(scrollState(), '', location.href);
    if ('scrollRestoration' in history) history.scrollRestoration = 'manual';

    async function navigate(url, focus, fromHistory = false) {
        const target = new URL(url, location.href);
        if (target.origin !== location.origin) return;
        target.hash = ''; // The fragment is only a no-JavaScript fallback.
        const scrollY = fromHistory ? (history.state?.tutorialScroll ?? window.scrollY) : window.scrollY;
        if (!fromHistory) history.replaceState(scrollState(), '', location.href);
        if (pending) pending.abort();
        pending = new AbortController();
        const current = ++sequence;
        const status = document.getElementById('tutorial-status');
        status.textContent = t('Aktualizowanie poradnika…');
        try {
            const response = await fetch(target.href, { signal: pending.signal, credentials: 'same-origin' });
            if (!response.ok || new URL(response.url).pathname !== target.pathname) throw new Error('Unexpected tutorial response');
            const html = new DOMParser().parseFromString(await response.text(), 'text/html');
            const next = html.querySelector('[data-tutorial-root]');
            if (!next) throw new Error('Missing tutorial content');
            if (current !== sequence) return;
            document.querySelector('[data-tutorial-root]').replaceWith(document.importNode(next, true));
            if (!fromHistory) history.pushState({ tutorialScroll: scrollY }, '', target.href);
            document.title = html.title;
            if (focus) {
                const control = [...document.querySelectorAll('[data-tutorial-root] button, [data-tutorial-root] select')]
                    .find(element => element.name === focus.name && element.value === focus.value);
                if (control) control.focus({ preventScroll: true });
            }
            window.scrollTo(0, scrollY);
            status.textContent = t('Wybór został zaktualizowany.');
        } catch (error) {
            if (current !== sequence || error.name === 'AbortError') return;
            // Preserve working GET navigation when scripting/network parsing fails.
            if (fromHistory) location.reload();
            else location.assign(url);
        }
    }

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.matches('[data-tutorial-form]')) return;
        event.preventDefault();
        const data = new FormData(form);
        if (event.submitter?.name) data.set(event.submitter.name, event.submitter.value);
        const url = new URL(form.action, location.href);
        url.search = new URLSearchParams(data).toString();
        const control = event.submitter?.name ? event.submitter : form.querySelector('[data-tutorial-auto-submit]');
        navigate(url.href, control ? { name: control.name, value: control.value } : null);
    });
    document.addEventListener('change', event => {
        if (event.target.matches('[data-tutorial-auto-submit]')) event.target.form.requestSubmit();
    });
    document.addEventListener('click', event => {
        const link = event.target.closest('a[data-tutorial-nav]');
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        navigate(link.href);
    });
    window.addEventListener('popstate', () => navigate(location.href, null, true));
})();
