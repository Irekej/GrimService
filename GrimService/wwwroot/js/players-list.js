(() => {
    'use strict';
    const t = window.GrimI18n.t, container = document.getElementById('forecastContainer');
    function add(tag, text, parent, className) {
        const el = document.createElement(tag); el.textContent = text;
        if (className) el.className = className;
        parent.append(el); return el;
    }
    fetch('/playersforecast').then(response => {
        if (!response.ok) throw new Error('HTTP ' + response.status);
        return response.json();
    }).then(data => {
        container.replaceChildren();
        const players = data[0]?.connectedPlayers ?? [];
        const card = add('div', '', container, 'col-12 card card-body');
        add('h2', t('Gracze online:') + ' ' + (data[0]?.playersNumber ?? 0), card, 'h4');
        const list = add('ul', '', card, 'list-group list-group-flush');
        for (const player of players) {
            const row = add('li', '', list, 'list-group-item d-flex justify-content-between');
            add('span', player.psnUserId || '', row);
            add('span', (player.country || '—') + ' · ' + (player.platform || '—'), row, 'text-muted');
        }
        if (!players.length) add('li', t('Brak aktywnych graczy.'), list, 'list-group-item text-muted');
    }).catch(() => { container.replaceChildren(); add('p', t('Nie udało się pobrać listy graczy.'), container, 'text-danger'); });
})();
