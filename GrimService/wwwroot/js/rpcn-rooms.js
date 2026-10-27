(() => {
    'use strict';
    const game = document.getElementById('rpcn-game');
    const button = document.getElementById('rpcn-refresh');
    const status = document.getElementById('rpcn-status');
    const updated = document.getElementById('rpcn-updated');
    const list = document.getElementById('rpcn-list');
    const language = document.documentElement.lang;
    const index = { pl: 0, en: 1, es: 2, fr: 3, pt: 4, ja: 5 }[language] ?? 1;
    const t = text => window.GrimI18nData?.[text]?.[index] || text;
    let snapshot = null;
    let busy = false;
    function node(tag, text, className) {
        const element = document.createElement(tag);
        if (text != null) element.textContent = text;
        if (className) element.className = className;
        return element;
    }
    function render() {
        list.replaceChildren();
        if (!snapshot) return;
        const messages = {
            disabled: 'Podgląd RPCN nie został jeszcze skonfigurowany.',
            configuration: 'Podgląd RPCN wymaga poprawienia konfiguracji serwera.',
            connecting: 'Łączenie z RPCN…',
            'account-in-use': 'Konto podglądu jest używane w innym połączeniu. Użyj osobnego konta RPCN.',
            rejected: 'RPCN odrzuciło logowanie lub zapytanie. Administrator musi sprawdzić konfigurację.',
            unavailable: 'RPCN jest chwilowo niedostępne. Odczyt zostanie ponowiony automatycznie.'
        };
        status.textContent = t(messages[snapshot.status] || 'Lista pokoi RPCN jest aktualna.');
        if (snapshot.updatedUtc) {
            updated.textContent = t(snapshot.status === 'ready' ? 'Ostatnia aktualizacja' : 'Ostatni poprawny odczyt — dane mogą być nieaktualne')
                + ': ' + new Date(snapshot.updatedUtc).toLocaleString(language);
        } else updated.textContent = '';
        const rooms = snapshot.rooms.filter(room => room.game === game.value);
        if (!rooms.length && snapshot.status === 'ready') list.append(node('p', t('Brak publicznych pokoi dla wybranej gry.')));
        if (snapshot.truncated) list.append(node('p', t('Lista została ograniczona do limitu odczytu.'), 'text-warning'));
        for (const room of rooms) {
            const column = node('div', null, 'col-lg-6');
            const card = node('article', null, 'card h-100');
            const body = node('div', null, 'card-body');
            body.append(node('h2', room.name || t('Pokój') + ' · ' + room.owner, 'h5'));
            const fields = node('dl', null, 'row mb-0');
            function field(label, value) {
                fields.append(node('dt', t(label), 'col-sm-4'), node('dd', value, 'col-sm-8'));
            }
            field('ID pokoju', room.id);
            field('Host', room.owner);
            field('Gracze', room.players + ' / ' + room.capacity);
            field('Trasa', room.track || (room.trackCode != null ? t('Kod trasy') + ': ' + room.trackCode : t('Brak danych')));
            field('Stały host', t(room.fixedHost ? 'Tak' : 'Nie'));
            body.append(fields);
            const members = node('details', null, 'mt-2');
            members.append(node('summary', t('Lista graczy') + ' (' + room.members.length + ')'));
            if (!room.membersAvailable) members.append(node('p', t('Lista graczy jest chwilowo niedostępna.')));
            else {
                const names = node('ul', null, 'mt-2');
                for (const name of room.members) names.append(node('li', name));
                members.append(names);
            }
            body.append(members);
            card.append(body); column.append(card); list.append(column);
        }
    }
    async function refresh() {
        if (busy) return;
        busy = true; button.disabled = true;
        const abort = new AbortController();
        const timeout = setTimeout(() => abort.abort(), 10000);
        try {
            const response = await fetch('/api/rpcn/rooms', { cache: 'no-store', signal: abort.signal });
            if (!response.ok) throw new Error('HTTP ' + response.status);
            snapshot = await response.json(); render();
        } catch {
            status.textContent = t('Nie udało się pobrać listy pokoi. Spróbuj ponownie.');
        } finally { clearTimeout(timeout); busy = false; button.disabled = false; }
    }
    game.addEventListener('change', render);
    button.addEventListener('click', refresh);
    setInterval(() => { if (!document.hidden) refresh(); }, 45000);
    refresh();
})();
