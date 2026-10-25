(() => {
    'use strict';
    const $ = id => document.getElementById(id), t = window.GrimI18n.t;
    let entries = [], selected = null, page = 1, catalogVersion = 0, rankingVersion = 0;
    async function get(path) {
        const response = await fetch('/api/seasonals/' + path, {signal: AbortSignal.timeout(15000), headers: {Accept: 'application/json'}});
        if (!response.ok) throw new Error(String(response.status));
        return response.json();
    }
    function filter() {
        const query = $('season-search').value.trim().toLowerCase();
        $('season-event').replaceChildren();
        entries.forEach((e, i) => {
            if (![e.title, e.id, e.course, e.boardId].join(' ').toLowerCase().includes(query)) return;
            const option = new Option(`${i + 1}. ${e.title || e.id}`, String(i));
            $('season-event').add(option);
        });
        $('season-event').selectedIndex = -1;
    }
    async function load() {
        const version = ++catalogVersion; ++rankingVersion;
        selected = null; entries = []; filter(); $('season-detail').hidden = true;
        $('season-status').textContent = t('Ładowanie eventów…');
        try {
            const game = $('season-game').value;
            const result = await get(game + ($('season-archive').checked ? '/rankings' : ''));
            if (version !== catalogVersion) return;
            entries = result; filter();
            $('season-status').textContent = `${t('Eventy')}: ${entries.length}`;
        } catch (_) { if (version === catalogVersion) $('season-status').textContent = t('Nie udało się pobrać eventów. Spróbuj ponownie.'); }
    }
    function choose() {
        if ($('season-event').value === '') return;
        selected = entries[Number($('season-event').value)]; page = 1;
        $('season-detail').hidden = false;
        $('season-title').textContent = selected.title || selected.id;
        // Render user-authored XML descriptions as plain text, never as HTML.
        $('season-description').textContent = (selected.description || '').replace(/<[^>]*>/g, '');
        $('season-parameters').textContent = (selected.parameters || []).join('\n');
        $('season-meta').replaceChildren();
        for (const [label, value] of [['ID', selected.id], ['Trasa', selected.course], ['Tryb', selected.mode], ['Początek', selected.begin], ['Koniec', selected.end]]) {
            const term = document.createElement('dt'), detail = document.createElement('dd');
            term.className = 'col-sm-3'; detail.className = 'col-sm-9';
            term.textContent = t(label); detail.textContent = value || '—';
            $('season-meta').append(term, detail);
        }
        rank();
    }
    async function rank() {
        const version = ++rankingVersion;
        $('season-ranking').replaceChildren();
        $('ranking-prev').disabled = true; $('ranking-next').disabled = true;
        if (!selected?.boardId || selected.boardId === '0') { $('ranking-status').textContent = t('Ten event nie ma rankingu.'); return; }
        $('ranking-status').textContent = t('Ładowanie rankingu…');
        try {
            const rows = await get(`${$('season-game').value}/${selected.folder}/${encodeURIComponent(selected.id)}/ranking?page=${page}`);
            if (version !== rankingVersion) return;
            for (const r of rows) {
                const row = document.createElement('tr');
                const country = String(r.country || '').toUpperCase().replace(/^UK$/, 'GB');
                const flag = /^[A-Z]{2}$/.test(country) ? String.fromCodePoint(...[...country].map(c => 127397 + c.charCodeAt(0))) + ' ' + country : '—';
                for (const value of [r.position, r.username, flag, r.formattedScore]) {
                    const cell = document.createElement('td'); cell.textContent = value; row.append(cell);
                }
                $('season-ranking').append(row);
            }
            $('ranking-status').textContent = rows.length ? `${t('Strona')}: ${page}` : t('Brak wyników na tej stronie.');
            $('ranking-prev').disabled = page <= 1; $('ranking-next').disabled = rows.length < 20;
        } catch (_) { if (version === rankingVersion) { $('ranking-status').textContent = t('Nie udało się pobrać rankingu.'); $('ranking-prev').disabled = page <= 1; } }
    }
    $('season-game').addEventListener('change', load);
    $('season-archive').addEventListener('change', load);
    $('season-search').addEventListener('input', filter);
    $('season-event').addEventListener('change', choose);
    $('ranking-prev').addEventListener('click', () => { page = Math.max(1, page - 1); rank(); });
    $('ranking-next').addEventListener('click', () => { ++page; rank(); });
    load();
})();
