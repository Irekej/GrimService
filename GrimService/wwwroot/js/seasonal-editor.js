/* No HTML from event XML is interpreted as markup. Unknown XML fields are preserved. */
(() => {
    'use strict';
    const t = window.GrimI18n.t;
    const $ = id => document.getElementById(id);
    let current, eventDoc, listingDoc, catalog = { options: {}, sections: {} }, dirty = false, validated = null;
    let choiceId = 0;
    let selectedEventId, listData = [], listScope = '', reference = { cars: [], tracks: [], tires: [] };
    const serializer = new XMLSerializer();
    const token = document.querySelector('#seasonal-editor input[name="__RequestVerificationToken"]').value;
    function node(tag, text, parent) {
        const n = document.createElement(tag);
        if (text !== undefined) n.textContent = text;
        if (parent) parent.append(n);
        return n;
    }
    function message(text, error = false) { $('message').textContent = t(text); $('message').className = error ? 'error' : 'success'; }
    async function api(path, body) {
        const response = await fetch('/admin/events/' + path, body ? { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, body: JSON.stringify(body) } : {});
        if (response.redirected) throw new Error(t('Sesja wygasła. Zaloguj się ponownie.'));
        const data = await response.json();
        if (!response.ok) throw new Error(data.error || t('Nie udało się wykonać operacji.'));
        return data;
    }
    const query = () => new URLSearchParams({ game: $('game').value, server: $('server').value });
    const guard = action => async () => { try { await action(); } catch (e) { message(e.message, true); } };
    function parse(xml) {
        if (/<!DOCTYPE/i.test(xml)) throw new Error(t('DOCTYPE nie jest dozwolony.'));
        const d = new DOMParser().parseFromString(xml, 'application/xml');
        const error = d.querySelector('parsererror');
        if (error) throw new Error(error.textContent);
        return d;
    }
    function changed() { dirty = true; validated = null; $('publish').disabled = true; $('withdraw').disabled = true; }
    function sync() {
        current.xml = serializer.serializeToString(eventDoc);
        current.listingXml = serializer.serializeToString(listingDoc);
        $('event-xml').value = current.xml;
        $('listing-xml').value = current.listingXml;
    }
    function labelFor(el) {
        const labels = { GameParameter: t('Grupa wyścigów'), events: t('Wyścigi'), event: t('Event'), information: t('Opisy i tłumaczenia'), race: t('Zasady wyścigu / pogoda'), track: t('Trasa'), regulation: t('Regulamin samochodów'), constraint: t('Ograniczenia i asysty'), reward: t('Nagrody'), entry_set: t('Uczestnicy i AI'), play_style: t('A-Spec / B-Spec'), ranking: t('Ranking'), eval_condition: t('Warunki medali'), failure_condition: t('Warunki porażki'), drift_condition: t('Drift'), license_condition: t('Licencja'), stage_data: t('Etapy'), arcade_style_setting: t('Arcade') };
        let suffix = '';
        if (el.tagName === 'event' && el.parentElement?.tagName === 'events') suffix = ' #' + (el.querySelector(':scope > event_id')?.getAttribute('value') || '?');
        return (labels[el.tagName] || el.tagName) + suffix;
    }
    function field(el, attr, parent, caption) {
        const value = attr ? el.getAttribute(attr) : el.textContent;
        const label = node('label', caption || el.tagName + (attr && attr !== 'value' ? ' / ' + attr : ''), parent);
        const key = el.parentElement?.tagName + '>' + el.tagName + '@' + attr;
        const options = [...(catalog.options[key] || [])];
        if (el.tagName === 'game_mode') ['EVENT_RACE', 'ONLINE_SINGLE_RACE', 'ONLINE_TIME_ATTACK', 'ONLINE_DRIFT_ATTACK', 'ARCADE_STYLE_RACE'].forEach(v => { if (!options.includes(v)) options.push(v); });
        let input;
        // Enumerated text values use a select; editable suggestions retain custom labels/numbers.
        if (options.length > 1 && options.length < 45 && attr === 'value' && options.every(v => /^[A-Z][A-Z0-9_]*$/.test(v))) {
            input = node('select', undefined, label);
            [...new Set([value, ...options])].forEach(v => { const o = node('option', v, input); o.value = v; });
        } else {
            input = node(value.length > 180 ? 'textarea' : 'input', undefined, label);
            if (input.tagName === 'INPUT' && options.length) {
                const id = 'choices-' + (++choiceId);
                input.setAttribute('list', id);
                const list = node('datalist', undefined, label); list.id = id;
                options.forEach(v => { const o = node('option', undefined, list); o.value = v; });
            }
        }
        input.value = value;
        if (el.tagName === 'folder_id' || (el.tagName === 'id' && el.ownerDocument === listingDoc)) input.disabled = true;
        input.addEventListener('input', () => { if (attr) el.setAttribute(attr, input.value); else el.textContent = input.value; changed(); sync(); });
    }
    function button(parent, text, handler) { const b = node('button', text, parent); b.type = 'button'; b.addEventListener('click', guard(handler)); return b; }
    function regenerateIds(copy) {
        let id = Math.max(catalog.nextId || 1, ...[...eventDoc.querySelectorAll('event_id,board_id')].map(e => (Number(e.getAttribute('value')) || 0) + 1));
        const events = copy.tagName === 'event' ? [copy] : [...copy.querySelectorAll('events > event')];
        events.forEach(e => {
            e.querySelector(':scope > event_id')?.setAttribute('value', String(id));
            e.querySelectorAll('ranking > board_id').forEach(b => b.setAttribute('value', String(id)));
            id++;
        });
    }
    function actions(el, parent) {
        const tools = node('div', undefined, parent); tools.className = 'node-actions';
        if (el.parentElement && !['folder_id', 'id', 'event_id', 'event_id_list'].includes(el.tagName)) {
            button(tools, t('Kopiuj element'), () => { const copy = el.cloneNode(true); regenerateIds(copy); el.after(copy); changed(); render(); });
            button(tools, t('Usuń element'), () => { if (confirm(t('Usunąć ') + el.tagName + '?')) { el.remove(); changed(); render(); } });
        }
        if (el.tagName === 'event' && el.parentElement?.tagName === 'events') {
            const select = node('select', undefined, tools);
            Object.keys(catalog.sections).filter(k => ![...el.children].some(e => e.tagName === k)).forEach(k => { const o = node('option', k, select); o.value = k; });
            button(tools, t('Dodaj sekcję z szablonu'), () => {
                if (!select.value) return;
                el.append(el.ownerDocument.importNode(parse(catalog.sections[select.value]).documentElement, true)); changed(); render();
            });
        }
        button(tools, t('+ Pole / element XML'), () => {
            const name = prompt(t('Nazwa nowego elementu (dokładna nazwa z formatu gry):'));
            if (!name) return;
            if (!/^[A-Za-z_][A-Za-z0-9_.-]*$/.test(name)) throw new Error(t('Nieprawidłowa nazwa.'));
            const child = el.ownerDocument.createElement(name);
            const value = prompt(t('Wartość atrybutu value (Anuluj = pusty element):'));
            if (value !== null) child.setAttribute('value', value);
            el.append(child); changed(); render();
        });
    }
    function build(el, parent, depth = 0) {
        const details = node('details', undefined, parent);
        node('summary', labelFor(el), details);
        let built = false;
        function content() {
            if (built) return; built = true;
            const fields = node('div', undefined, details); fields.className = 'fields';
            [...el.attributes].forEach(a => field(el, a.name, fields));
            if (!el.children.length && !el.attributes.length) field(el, null, fields);
            for (const child of el.children) {
                if (!child.children.length) {
                    const group = node('div', undefined, fields);
                    if (child.attributes.length) [...child.attributes].forEach(a => field(child, a.name, group));
                    else field(child, null, group);
                } else build(child, details, depth + 1);
            }
            actions(el, details);
        }
        details.addEventListener('toggle', () => { if (details.open) content(); });
        if (depth < 2) { details.open = true; content(); }
    }
    function render() {
        $('event-form').replaceChildren(); $('listing-form').replaceChildren();
        quickForm();
        build(eventDoc.documentElement, $('event-form'));
        build(listingDoc.documentElement, $('listing-form'));
        sync();
    }
    const val = (e, path, fallback = '') => e.querySelector(':scope > ' + path)?.getAttribute('value') ?? fallback;
    function ensure(e, path) {
        for (const name of path.split(' > ')) {
            let child = [...e.children].find(n => n.tagName === name);
            if (!child) { child = e.ownerDocument.createElement(name); e.append(child); }
            e = child;
        }
        return e;
    }
    function set(e, path, value) { ensure(e, path).setAttribute('value', String(value)); }
    const categoryOf = e => val(e, 'game_mode') === 'ONLINE_BATTLE' ? 'quickmatch'
        : val(e, 'play_style > bspec_type') === 'ONLY_B' || val(e, 'game_mode') === 'ARENA' ? 'bspec' : 'seasonal';
    const kindOf = e => val(e, 'game_mode').includes('DRIFT') ? 'drift' : val(e, 'game_mode').includes('TIME_ATTACK') ? 'time' : 'race';
    function panel(parent, title) { const p = node('section', undefined, parent); p.className = 'config-panel'; node('h3', title, p); return p; }
    function selectField(parent, caption, value, options, change) {
        const label = node('label', caption, parent), input = node('select', undefined, label);
        const choices = [...options];
        if (!choices.some(x => String(x[0]) === String(value))) choices.unshift([value, t('Obecne: ') + value]);
        choices.forEach(([v, text]) => { const o = node('option', text, input); o.value = v; });
        input.value = value;
        input.onchange = guard(() => { change(input.value); changed(); sync(); });
        return input;
    }
    function numberField(parent, caption, value, min, max, change) {
        const label = node('label', caption, parent), input = node('input', undefined, label);
        input.type = 'number'; input.min = min; input.max = max; input.step = '1'; input.required = true; input.value = value;
        input.oninput = () => { changed(); if (input.checkValidity()) { change(Number(input.value)); sync(); } };
        return input;
    }
    function toggle(parent, caption, value, change) {
        return selectField(parent, caption, value ? 'yes' : 'no', [['no', t('Nie')], ['yes', t('Tak')]], x => change(x === 'yes'));
    }
    function picker(parent, caption, value, records, change) {
        const wrap = node('label', caption, parent);
        const search = node('input', undefined, wrap); search.type = 'search'; search.placeholder = t('Szukaj po nazwie lub identyfikatorze…');
        const select = node('select', undefined, wrap);
        const unique = [...new Map(records.map(r => [r.label, r])).values()];
        if (!unique.some(r => r.label === value) && value) unique.unshift({ label: value, name: t('Obecna / własna pozycja') });
        const draw = () => {
            const selected = select.value || value;
            select.replaceChildren();
            const filtered = unique.filter(r => r.label === selected || (r.name + ' ' + r.label + ' ' + (r.code ?? '')).toLowerCase().includes(search.value.toLowerCase()));
            node('option', t('— Wybierz —'), select).value = '';
            filtered.forEach(r => { const o = node('option', (r.name || r.label) + ' · ' + r.label + (r.code == null ? '' : ' [' + r.code + ']'), select); o.value = r.label; });
            select.value = selected;
        };
        draw(); select.value = value || '';
        search.oninput = draw;
        select.onchange = guard(() => { if (!select.value) return; change(select.value); value = select.value; changed(); sync(); });
        return select;
    }
    function carRecords() {
        const known = new Set(Object.entries(catalog.options).filter(([k]) => k.endsWith('>car@label')).flatMap(([, v]) => v));
        return reference.cars.map(c => ({ ...c, name: c.name + (current.game === 'GT5' && !known.has(c.label) ? t(' · GT5 niepotwierdzone') : '') }));
    }
    function languageEditor(parent, target, title) {
        const box = panel(parent, title);
        const languages = [...new Set(['PL', 'GB', 'US', 'JP', 'FR', 'DE', 'IT', 'ES', 'PT', 'NL', 'RU', 'KR', 'TW', 'EL', 'TR', 'CZ', 'HU', 'BP', 'MS', 'FI',
            ...[...target.querySelectorAll(':scope > title > *, :scope > description > *')].map(n => n.tagName)])];
        let lang = 'PL';
        const body = node('div', undefined, box); body.className = 'fields';
        const draw = () => {
            body.replaceChildren();
            for (const [key, caption] of [['title', t('Tytuł')], ['description', t('Opis')]]) {
                const label = node('label', caption, body), input = node(key === 'title' ? 'input' : 'textarea', undefined, label);
                input.value = target.querySelector(':scope > ' + key + ' > ' + lang)?.textContent || '';
                input.oninput = () => {
                    ensure(target, key + ' > ' + lang).textContent = input.value;
                    if (key === 'title' && target.tagName === 'information') ensure(target, 'one_line_title > ' + lang).textContent = input.value;
                    if (target.tagName === 'information' && eventDoc.querySelectorAll('events > event').length === 1)
                        ensure(listingDoc.documentElement, key + ' > ' + lang).textContent = input.value;
                    changed(); sync();
                };
            }
        };
        const select = selectField(box, t('Język'), lang, languages.map(x => [x, x]), x => { lang = x; draw(); });
        // Changing the editing language does not modify XML.
        select.onchange = () => { lang = select.value; draw(); };
        box.insertBefore(select.parentElement, body);
        button(box, t('Skopiuj ten język do pozostałych'), () => {
            if (!confirm(t('Zastąpić wszystkie tłumaczenia tytułu i opisu tekstem z ') + lang + '?')) return;
            for (const key of ['title', 'description', ...(target.tagName === 'information' ? ['one_line_title'] : [])]) {
                const text = target.querySelector(':scope > ' + key + ' > ' + lang)?.textContent || '';
                languages.forEach(l => {
                    ensure(target, key + ' > ' + l).textContent = text;
                    if (target.tagName === 'information' && key !== 'one_line_title' && eventDoc.querySelectorAll('events > event').length === 1)
                        ensure(listingDoc.documentElement, key + ' > ' + l).textContent = text;
                });
            }
            changed(); sync();
        });
        draw();
    }
    function listingCategory() {
        const entries = [...eventDoc.querySelectorAll('events > event')];
        const category = categoryOf(entries[0]);
        let type = current.game === 'GT6' ? (category === 'quickmatch' ? 1200 : 2000)
            : category === 'bspec' ? (val(entries[0], 'game_mode') === 'ARENA' ? 2 : 1)
            : entries.every(e => kindOf(e) === 'time') ? 3 : entries.every(e => kindOf(e) === 'drift') ? 4 : 0;
        ensure(listingDoc.documentElement, 'event_type').textContent = type;
    }
    function configure(e, category, kind) {
        const trial = kind !== 'race', quick = category === 'quickmatch', bspec = category === 'bspec';
        const previousKind = kindOf(e);
        // Only explicit mode changes reset incompatible race-specific settings.
        for (const name of ['entry_set', 'drift_condition', 'license_condition', 'failure_condition', 'stage_data', 'eval_condition'])
            e.querySelector(':scope > ' + name)?.remove();
        set(e, 'game_mode', quick ? 'ONLINE_BATTLE' : kind === 'time' ? 'ONLINE_TIME_ATTACK'
            : kind === 'drift' ? (current.game === 'GT5' ? 'DRIFT_ATTACK' : 'ONLINE_DRIFT_ATTACK') : 'EVENT_RACE');
        set(e, 'event_type', 'RACE');
        set(e, 'play_style > bspec_type', bspec ? 'ONLY_B' : 'ONLY_A');
        set(e, 'race > race_type', kind === 'time' ? 'TIMEATTACK' : kind === 'drift' ? 'DRIFTATTACK' : 'COMPETITION');
        set(e, 'race > complete_type', trial ? 'NONE' : 'BYLAPS');
        set(e, 'race > race_limit_laps', trial ? 0 : 5); set(e, 'race > race_limit_minute', 0);
        set(e, 'race > entry_max', trial ? 1 : 16); set(e, 'race > racers_max', trial ? 1 : 16);
        set(e, 'race > online_on', quick ? 1 : 0);
        set(e, 'race > start_type', quick ? 'GRID_FLYING' : kind === 'drift' ? 'COURSEINFO_ROLLING' : trial ? 'ATTACK' : 'GRID');
        set(e, 'race > grid_sort_type', trial ? 'NONE' : 'RANDOM');
        set(e, 'race > ghost_type', kind === 'time' ? 'ONELAP' : kind === 'drift' ? 'FULL' : 'NONE');
        set(e, 'race > immediate_finish', 0);
        set(e, 'race > enable_pit', 0); set(e, 'race > consume_fuel', 0); set(e, 'race > consume_tire', 0);
        if (kind === 'drift') {
            set(e, 'constraint > drift_type', -1);
        } else set(e, 'constraint > drift_type', -1);
        if (!trial && !quick) {
            const ai = ensure(e, 'entry_set > entry_generate');
            set(ai, 'entry_num', 16); set(ai, 'player_pos', 15); set(ai, 'generate_type', 'ONE_MAKE');
            set(ai, 'ai_skill', 80);
        }
        if (e.querySelector(':scope > ranking')) {
            if (quick) e.querySelector(':scope > ranking').remove();
            else {
                set(e, 'ranking > type', kind === 'drift' ? 'DRIFT' : 'TIME');
                if (previousKind !== kind) set(e, 'ranking > board_id', Math.max(catalog.nextId || 1,
                    ...[...eventDoc.querySelectorAll('event_id,board_id')].map(n => Number(n.getAttribute('value')) + 1)));
            }
        }
        listingCategory();
    }
    function quickForm() {
        const root = $('quick-form'); root.replaceChildren();
        const entries = [...eventDoc.querySelectorAll('events > event')];
        if (!entries.length) { node('p', t('Brak eventów. Zaimportuj XML.'), root); return; }
        let index = entries.findIndex(e => val(e, 'event_id') === String(selectedEventId));
        if (index < 0) index = 0;
        const navigation = panel(root, t('Edytujesz event'));
        const select = selectField(navigation, t('Event w tym pliku'), String(index), entries.map((e, i) =>
            [String(i), '#' + val(e, 'event_id') + ' — ' + (e.querySelector('information > title > PL')?.textContent || e.querySelector('information > title > GB')?.textContent || '')]), () => {});
        select.onchange = () => { selectedEventId = val(entries[Number(select.value)], 'event_id'); quickForm(); };
        const e = entries[index]; selectedEventId = val(e, 'event_id');
        const category = categoryOf(e), kind = kindOf(e), isRemote = val(e, 'game_mode') === 'ARENA';
        node('p', t('ID eventu: ') + selectedEventId + t(' · Folder: ') + current.folder + ' · ' + current.game, navigation);
        button(navigation, t('Powiel ten event (nowe ID)'), () => {
            if (category === 'quickmatch' && entries.length >= 5) throw new Error(t('Zestaw Quick Match może mieć najwyżej 5 pozycji w tym konfiguratorze.'));
            const gp = e.parentElement.parentElement.cloneNode(true);
            const events = gp.querySelector(':scope > events'); events.replaceChildren(e.cloneNode(true)); regenerateIds(gp);
            e.parentElement.parentElement.after(gp); selectedEventId = val(events.firstElementChild, 'event_id'); changed(); render();
        });
        button(navigation, t('Usuń ten event ze szkicu'), () => {
            if (entries.length === 1) throw new Error(t('Ostatni event wycofaj przyciskiem „Wycofaj z listy” po sprawdzeniu pliku.'));
            if (!confirm(t('Usunąć event #') + selectedEventId + t('? Usunięcie trafi do gry dopiero po publikacji.'))) return;
            const gp = e.parentElement.parentElement; e.remove();
            if (!gp.querySelector('events > event')) gp.remove();
            selectedEventId = null; listingCategory(); changed(); render();
        });
        const basic = panel(root, t('1. Rodzaj zawodów i trasa')), fields = node('div', undefined, basic); fields.className = 'fields';
        const cats = [['seasonal', t('Event sezonowy')], current.game === 'GT6' ? ['quickmatch', 'Quick Match'] : ['bspec', 'B-Spec']];
        const c = selectField(fields, t('Gdzie jest widoczny?'), category, cats, x => {
            if (!confirm(t('Zmiana kategorii przebuduje zasady, AI i wypożyczane auta. Kontynuować?'))) { quickForm(); return; }
            configure(e, x, x === 'seasonal' ? kind : 'race'); render();
        });
        // Category belongs to the file/list entry, not just one event in that file.
        c.disabled = entries.length > 1 || category === 'quickmatch' || (current.game === 'GT6');
        if (c.disabled) node('small', t('Kategorię nowego zestawu wybierzesz w „Utwórz nowy event”. Istniejący wspólny zestaw zachowuje kategorię.'), basic);
        const k = selectField(fields, t('Rodzaj konkurencji'), kind,
            category === 'seasonal' ? [['race', t('Wyścig z AI')], ['time', t('Próba czasowa')], ['drift', t('Drift')]] : [['race', category === 'quickmatch' ? t('Wyścig online') : t('Wyścig B-Spec')]], x => {
                if (!confirm(t('Przebudować zasady dla nowej konkurencji? AI, wypożyczone auta i warunki poprzedniej konkurencji zostaną zastąpione.'))) { quickForm(); return; }
                configure(e, category, x); render();
            });
        k.disabled = isRemote;
        const track = e.querySelector(':scope > track > course_code');
        picker(fields, t('Trasa'), track?.getAttribute('label') || '', reference.tracks.filter(t => current.game === 'GT6' || t.game === 'GT5'), x => {
            const t = ensure(e, 'track > course_code'); t.setAttribute('label', x);
            if (kind === 'drift') { e.querySelector(':scope > drift_condition')?.remove(); set(e, 'constraint > drift_type', -1); }
        });
        if (kind === 'race') {
            numberField(fields, t('Liczba okrążeń'), val(e, 'race > race_limit_laps', 5), 1, 999, x => {
                set(e, 'race > race_limit_laps', x); set(e, 'race > race_limit_minute', 0); set(e, 'race > complete_type', 'BYLAPS');
            });
            if (category === 'quickmatch' || isRemote) numberField(fields, t('Maksymalnie graczy / kierowców'), val(e, 'race > racers_max', 16), 2, 16, x => {
                set(e, 'race > racers_max', x); set(e, 'race > entry_max', x);
            });
            else {
                numberField(fields, t('Liczba przeciwników AI (bez gracza)'), Math.max(0, Number(val(e, 'entry_set > entry_generate > entry_num', val(e, 'race > entry_max', 1))) - 1), 0, 15, x => {
                    set(e, 'entry_set > entry_generate > entry_num', x + 1); set(e, 'entry_set > entry_generate > player_pos', x);
                    set(e, 'race > entry_max', x + 1); set(e, 'race > racers_max', x + 1);
                    if (!val(e, 'entry_set > entry_generate > generate_type')) set(e, 'entry_set > entry_generate > generate_type', 'ONE_MAKE');
                });
                numberField(fields, t('Umiejętności AI'), val(e, 'entry_set > entry_generate > ai_skill', 80), 0, 120, x => {
                    for (const key of ['ai_skill', 'ai_skill_accelerating', 'ai_skill_breaking', 'ai_skill_cornering']) set(e, 'entry_set > entry_generate > ' + key, x);
                });
            }
        }
        const cars = panel(root, t('2. Samochody i opony'));
        const tireOptions = reference.tires.map(tire => [String(tire.id), t(tire.name)]);
        selectField(cars, t('Opony (jednakowe z przodu i z tyłu)'), val(e, 'constraint > limit_tire_f', '-1'), [['-1', t('Bez ograniczeń')], ...tireOptions], x => {
            for (const section of ['constraint', 'regulation']) for (const prefix of ['limit', 'need']) for (const axis of ['f', 'r']) set(e, section + ' > ' + prefix + '_tire_' + axis, x);
            for (const axis of ['f', 'r']) set(e, 'constraint > suggest_tire_' + axis, x);
            const tire = reference.tires.find(t => String(t.id) === x);
            if (tire) e.querySelectorAll('entry_base').forEach(b => { set(b, 'tire_f', tire.label); set(b, 'tire_r', tire.label); });
        });
        function pool(caption, path, rental = false) {
            const box = node('details', undefined, cars); node('summary', caption, box);
            const list = node('div', undefined, box);
            const draw = () => {
                list.replaceChildren();
                const container = e.querySelector(':scope > ' + path);
                const items = rental ? [...(container?.children || [])].filter(n => n.tagName === 'entry' && n.querySelector('entry_base > car, :scope > car'))
                    : [...(container?.children || [])].filter(n => n.tagName === 'car');
                items.forEach(item => {
                    const car = rental ? item.querySelector('entry_base > car, :scope > car') : item;
                    const row = node('div', undefined, list); row.className = 'car-row';
                    const label = car.getAttribute('label'), info = reference.cars.find(c => c.label === label);
                    node('span', (info?.name || label) + ' · ' + label, row);
                    button(row, t('Usuń'), () => {
                        item.remove();
                        if (rental) { const first = e.querySelector('entry_set > entry'); if (first) set(first, 'player_no', 0); }
                        if (path.includes('entry_generate') && !container.querySelector('car')) set(e, 'entry_set > entry_generate > generate_type', 'ONE_MAKE');
                        changed(); sync(); draw();
                    });
                });
                if (!items.length) node('small', rental ? t('Brak aut w tej puli wypożyczania.') : path.includes('entry_generate')
                    ? t('Brak prostej puli AI. Bez zmian zachowujemy generator oryginału; w nowym evencie ONE_MAKE dobiera auto gracza. Dodanie modelu przełącza AI na tę listę.')
                    : t('Pusta lista = brak ograniczenia do konkretnych modeli.'), list);
            };
            picker(box, t('Dodaj samochód'), '', carRecords(), label => {
                const container = ensure(e, path);
                if ([...container.querySelectorAll('car')].some(c => c.getAttribute('label') === label)) return;
                if (rental) {
                    const entry = eventDoc.createElement('entry'); container.append(entry); set(entry, 'player_no', 0);
                    const base = ensure(entry, 'entry_base'); ensure(base, 'car').setAttribute('label', label); ensure(base, 'car').setAttribute('color', '0');
                    const tire = reference.tires.find(t => String(t.id) === val(e, 'constraint > limit_tire_f'));
                    if (tire) { set(base, 'tire_f', tire.label); set(base, 'tire_r', tire.label); }
                } else { const car = eventDoc.createElement('car'); car.setAttribute('label', label); container.append(car); }
                if (path.includes('entry_generate')) set(e, 'entry_set > entry_generate > generate_type', 'SHUFFLE');
                draw();
            });
            draw();
        }
        pool(t('Dozwolone auta gracza — lista modeli'), 'regulation > cars');
        pool(t('Auta do wypożyczenia na czas eventu'), 'entry_set', true);
        if (kind === 'race' && category !== 'quickmatch' && !isRemote) pool(t('Modele aut przeciwników AI'), 'entry_set > entry_generate > cars');
        const rules = panel(root, t('3. Zasady'));
        const ruleFields = node('div', undefined, rules); ruleFields.className = 'fields';
        selectField(ruleFields, t('Kary'), val(e, 'race > penalty_level', '0'), [['0', t('Wyłączone')], ['1', t('Słabe')], ['2', t('Silne')]], x => set(e, 'race > penalty_level', x));
        selectField(ruleFields, t('Tuning'), val(e, 'regulation > tuning', '-1'), [['-1', t('Bez ograniczeń')], ['0', t('Zabroniony')], ['1', t('Dozwolony')]], x => set(e, 'regulation > tuning', x));
        numberField(ruleFields, t('Limit PP (-1 = brak)'), val(e, 'regulation > limit_pp', '-1'), -1, 2000, x => set(e, 'regulation > limit_pp', x));
        if (kind === 'race') {
            toggle(ruleFields, t('Pit stopy dostępne'), val(e, 'race > enable_pit') === '1', x => set(e, 'race > enable_pit', x ? 1 : 0));
            numberField(ruleFields, t('Zużycie paliwa (10 = ×1, 0 = wyłączone)'), val(e, 'race > consume_fuel', 0), 0, 1000, x => set(e, 'race > consume_fuel', x));
            numberField(ruleFields, t('Zużycie opon (10 = ×1, 0 = wyłączone)'), val(e, 'race > consume_tire', 0), 0, 1000, x => set(e, 'race > consume_tire', x));
        }
        const ranking = panel(root, t('4. Ranking i harmonogram'));
        if (category === 'quickmatch') node('p', t('Quick Match nie korzysta tutaj z rankingu sezonowego.'), ranking);
        else toggle(ranking, t('Publikuj ranking'), !!e.querySelector(':scope > ranking'), enabled => {
            if (!enabled) e.querySelector(':scope > ranking')?.remove();
            else {
                const r = ensure(e, 'ranking'); set(r, 'board_id', Math.max(catalog.nextId || 1, ...[...eventDoc.querySelectorAll('event_id,board_id')].map(n => Number(n.getAttribute('value')) + 1)));
                set(r, 'type', kind === 'drift' ? 'DRIFT' : 'TIME'); set(r, 'is_local', 0); set(r, 'registration', 0);
                set(r, 'display_rank_limit', -1); set(r, 'replay_rank_limit', 0);
                for (const key of ['begin_date', 'end_date']) ensure(r, key).textContent = e.querySelector(':scope > ' + key)?.textContent || '';
            }
            changed(); render();
        });
        if (e.querySelector(':scope > ranking')) node('p', t('ID rankingu: ') + val(e, 'ranking > board_id') + ' · ' + val(e, 'ranking > type') + t('. Usunięcie rankingu z eventu nie kasuje zapisanych wyników z bazy.'), ranking);
        for (const [key, caption] of [['begin_date', t('Dostępny od (UTC)')], ['end_date', t('Dostępny do (UTC)')]]) {
            const label = node('label', caption, ranking), input = node('input', undefined, label); input.type = 'datetime-local';
            const text = e.querySelector(':scope > ' + key)?.textContent || '';
            const date = new Date(text.replaceAll('/', '-').replace(' ', 'T') + (text ? 'Z' : ''));
            input.value = Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 16);
            input.onchange = () => {
                if (!input.value) return;
                const v = input.value.replaceAll('-', '/').replace('T', ' ') + ':00';
                ensure(e, key).textContent = v;
                if (e.querySelector(':scope > ranking')) ensure(e, 'ranking > ' + key).textContent = v;
                if (entries.length === 1) ensure(listingDoc.documentElement, key).textContent = input.value + ':00+00:00';
                changed(); sync();
            };
        }
        if (current.game === 'GT6') {
            const artwork = panel(root, t('Wygląd kafelka GT6'));
            const choices = [
                ['', t('Domyślny (bez nadpisania)')],
                ['1', t('1 / A — auto, logo trasy i PP')],
                ['2', t('2 / B — obraz bez auta i logo trasy')],
                ['3', t('3 / C — obraz i limit PP')],
                ['4', t('4 / D — obraz, auto, logo trasy i PP')]
            ];
            const layout = selectField(artwork, t('Układ kafelka'), val(e, 'information > logo_image_layout', ''), choices, value => {
                if (value === '') e.querySelector(':scope > information > logo_image_layout')?.remove();
                else set(e, 'information > logo_image_layout', value);
            });
            layout.disabled = category === 'quickmatch';
            if (category === 'quickmatch') node('p', t('Quick Match ma własny układ; wykorzystuje tylko pole obrazu poniżej.'), artwork);
            const label = node('label', t('Nazwa obrazu na serwerze (.png / .img)'), artwork);
            const image = node('input', undefined, label);
            image.type = 'text'; image.maxLength = 128;
            image.placeholder = 'gt6_event_1000996_v1.png';
            const imageField = e.querySelector(':scope > information > logo_image_path');
            image.value = imageField?.getAttribute('value') ?? imageField?.textContent?.trim() ?? '';
            image.onchange = guard(() => {
                const value = image.value.trim();
                if (value && !/^[a-zA-Z0-9_-]+\.(png|img)$/.test(value))
                    throw new Error(t('Wpisz samą nazwę pliku PNG/IMG, bez adresu URL i katalogów.'));
                if (!value) e.querySelector(':scope > information > logo_image_path')?.remove();
                else {
                    const field = ensure(e, 'information > logo_image_path');
                    field.textContent = ''; field.setAttribute('value', value);
                }
                changed(); sync();
            });
            node('p', t('Obraz umieść w Resources/event/{serwer}/. Puste pole zachowuje grafikę układu. Zmieniaj nazwę pliku przy aktualizacji obrazu, aby ominąć pamięć podręczną gry.'), artwork);
            node('p', t('Układy B/C/D wymagają sprawdzenia z własnym obrazem na konsoli. Zmiany dotyczą tylko wybranego eventu i trafiają do gry po publikacji.'), artwork);
        }
        languageEditor(root, ensure(e, 'information'), t('5. Tytuł i opis eventu'));
        const meta = node('details', undefined, root); node('summary', t('Nazwa i opis całej grupy na liście w grze'), meta);
        languageEditor(meta, listingDoc.documentElement, t('Tłumaczenia grupy'));
    }
    async function open(data, eventId) {
        selectedEventId = eventId;
        current = data; validated = null; dirty = false; $('publish').disabled = true; $('withdraw').disabled = true;
        $('workspace').hidden = false; $('editor-title').textContent = `${current.game} / ${current.server} / event_${current.folder}.xml`;
        $('event-xml').value = data.xml; $('listing-xml').value = data.listingXml;
        $('event-form').replaceChildren(); $('listing-form').replaceChildren();
        catalog = await api('catalog?' + new URLSearchParams({ game: data.game, server: data.server }));
        try { eventDoc = parse(data.xml); listingDoc = parse(data.listingXml); render(); }
        catch (e) { eventDoc = null; listingDoc = null; message(t('Plik wymaga naprawy w sekcji Zaawansowane: ') + e.message, true); }
        $('workspace').scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    function updateCategories() {
        for (const id of ['category-filter', 'new-category']) {
            const select = $(id), previous = select.value;
            select.replaceChildren();
            const options = [...(id === 'category-filter' ? [['all', t('Wszystkie')]] : []), ['seasonal', t('Eventy sezonowe')],
                $('game').value === 'GT6' ? ['quickmatch', 'Quick Match'] : ['bspec', 'B-Spec']];
            options.forEach(([v, text]) => { const o = node('option', text, select); o.value = v; });
            if (options.some(([v]) => v === previous)) select.value = previous;
        }
        updateNewKind();
    }
    function updateNewKind() {
        const category = $('new-category').value, previous = $('new-kind').value;
        const choices = category === 'quickmatch' ? [['race', t('Wyścig online')]]
            : category === 'bspec' ? [['race', t('Sezonowy wyścig B-Spec')], ['remote', t('Remote Race (udostępnieni kierowcy)')]]
            : [['race', t('Wyścig z AI')], ['time', t('Próba czasowa')], ['drift', t('Drift')]];
        $('new-kind').replaceChildren();
        choices.forEach(([v, t]) => { const o = node('option', t, $('new-kind')); o.value = v; });
        $('new-kind').disabled = choices.length === 1;
        if (choices.some(([v]) => v === previous)) $('new-kind').value = previous;
    }
    function drawList() {
        $('season-list').replaceChildren();
        const search = $('event-search').value.toLowerCase(), category = $('category-filter').value;
        const cards = node('div', undefined, $('season-list')); cards.className = 'event-cards';
        let count = 0;
        for (const folder of listData) for (const entry of folder.entries.length ? folder.entries : [{ id: '?', title: folder.title, description: folder.error || '', track: '', category: 'seasonal' }]) {
            if (category !== 'all' && category !== entry.category) continue;
            if (![entry.id, entry.title, entry.description, entry.track, String(folder.id)].join(' ').toLowerCase().includes(search)) continue;
            count++;
            const card = node('article', undefined, cards); card.className = 'event-card';
            node('small', (entry.category === 'quickmatch' ? 'QUICK MATCH' : entry.category === 'bspec' ? 'B-SPEC' : t('SEZONOWY')) + ' · ' + (folder.published ? t('Na liście') : t('Poza listą')), card);
            node('h3', entry.title, card);
            node('p', entry.description || t('Brak opisu'), card).className = 'event-description';
            const track = reference.tracks.find(t => t.label === entry.track);
            node('p', (track?.name || entry.track || t('Brak trasy')) + t(' · ID ') + entry.id + t(' · Folder ') + folder.id, card);
            const tools = node('div', undefined, card); tools.className = 'node-actions';
            const choose = async mode => {
                if (dirty && !confirm(t('Porzucić niezapisane zmiany?'))) return;
                await open(await api(mode + '?' + listScope + '&folder=' + folder.id), mode === 'load' ? entry.id : null);
                if (mode === 'template') { changed(); message(t('Utworzono kopię grupy z nowymi ID. Sprawdź daty i opublikuj po akceptacji.')); }
            };
            button(tools, folder.error ? t('Napraw XML') : t('Edytuj event'), () => choose('load'));
            if (entry.category !== 'quickmatch') button(tools, t('Kopiuj grupę'), () => choose('template')).disabled = !!folder.error;
        }
        if (!count) node('p', t('Nie znaleziono eventów dla tego filtra.'), cards);
    }
    async function refresh() {
        if (dirty && !confirm(t('Porzucić niezapisane zmiany?'))) return;
        $('workspace').hidden = true; current = null; dirty = false;
        const scope = query().toString();
        const data = await api('list?' + scope);
        if (scope !== query().toString()) return;
        listScope = scope; listData = data;
        drawList();
        message(t('Wybierz event lub utwórz nowy. Publikacja dotyczy całej grupy w pliku XML.'));
    }
    $('refresh').onclick = guard(refresh);
    $('category-filter').onchange = $('event-search').oninput = drawList;
    $('new-category').onchange = updateNewKind;
    $('game').onchange = guard(async () => { updateCategories(); await refresh(); });
    $('server').onchange = guard(refresh);
    $('create-event').onclick = guard(async () => {
        if (dirty && !confirm(t('Porzucić niezapisane zmiany?'))) return;
        const category = $('new-category').value, kind = $('new-kind').value;
        const data = await api('create?' + query() + '&category=' + category + '&kind=' + kind);
        await open(data);
        const entries = [...eventDoc.querySelectorAll('events > event')], e = entries[entries.length - 1];
        selectedEventId = val(e, 'event_id');
        // Quick Match preserves the working room configuration of its template.
        if (category !== 'quickmatch' && kind !== 'remote') configure(e, category, kind);
        listingCategory();
        changed(); render();
        message(t('Nowy szkic gotowy. Ustaw tytuł, trasę i samochody, a następnie sprawdź i opublikuj.'));
    });
    $('apply-xml').onclick = guard(() => { const e = parse($('event-xml').value), l = parse($('listing-xml').value); eventDoc = e; listingDoc = l; changed(); render(); message(t('Zastosowano XML. Sprawdź zmiany przed publikacją.')); });
    $('event-xml').oninput = $('listing-xml').oninput = changed;
    $('validate').onclick = guard(async () => {
        if (!current) return;
        for (const input of $('quick-form').querySelectorAll('input[type="number"]'))
            if (!input.reportValidity()) throw new Error(t('Popraw zaznaczoną wartość liczbową.'));
        current.xml = $('event-xml').value; current.listingXml = $('listing-xml').value;
        const result = await api('validate', current);
        eventDoc = parse(result.xml); listingDoc = parse(result.listingXml); current = result; render();
        validated = JSON.stringify(current);
        $('preview-text').textContent = `${current.game} / ${current.server} / event_${current.folder}.xml\n\n` + [...eventDoc.querySelectorAll('events > event')].map(e =>
            '#' + val(e, 'event_id') + ' — ' + (e.querySelector('information > title > PL')?.textContent || e.querySelector('information > title > GB')?.textContent || '')
            + t('\nTrasa: ') + (e.querySelector('track > course_code')?.getAttribute('label') || '')
            + ' · ' + val(e, 'game_mode') + t(' · Okrążenia: ') + val(e, 'race > race_limit_laps')
            + t('\nRanking: ') + (val(e, 'ranking > board_id') || t('wyłączony'))
        ).join('\n\n') + t('\n\nCały XML jest dostępny w sekcji Zaawansowane. Publikacja zastąpi ten plik i jego wpis na liście; kopia zostanie zachowana.');
        $('preview').open = true; $('publish').disabled = false; $('withdraw').disabled = false;
        message(t('Struktura i daty poprawne; lista ID zsynchronizowana. Sprawdź podgląd. Walidacja nie zastępuje testu zgodności na konsoli.'));
    });
    $('publish').onclick = guard(async () => {
        // The explicitly labelled second button is the approval, after validation/preview.
        if (!validated) return;
        $('publish').disabled = true;
        const result = await api('publish', { ...JSON.parse(validated), confirm: true });
        await open(result.document); message(t('Opublikowano. Kopia poprzedniej wersji: ') + result.backup);
    });
    $('withdraw').onclick = guard(async () => {
        if (!validated || !confirm(t('Wycofać ten folder z listy sezonów? Plik eventu i kopia poprzedniej listy zostaną zachowane.'))) return;
        const result = await api('publish', { ...JSON.parse(validated), confirm: true, withdraw: true });
        await open(result.document); message(t('Wycofano sezon z listy. Kopia: ') + result.backup + t('. Plik eventu pozostaje dostępny.'));
    });
    $('import').onchange = guard(async () => {
        const file = $('import').files[0]; if (!file) return;
        if (file.size > 4 * 1024 * 1024) throw new Error(t('Maksymalny rozmiar: 4 MB.'));
        const d = parse(await file.text());
        d.querySelectorAll('folder_id').forEach(n => n.setAttribute('value', current.folder));
        eventDoc = d; listingDoc = parse($('listing-xml').value); changed(); render();
        message(t('Zaimportowano szkic. Sprawdź ID eventów/rankingów: import nie zmienia ich automatycznie.'));
    });
    $('export').onclick = guard(() => {
        const url = URL.createObjectURL(new Blob([$('event-xml').value], { type: 'application/xml' }));
        const a = node('a'); a.href = url; a.download = `event_${current.folder}.xml`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
    const draftKey = () => 'grim-season:' + current.server + ':' + current.game + ':' + current.folder;
    $('save-draft').onclick = guard(() => { localStorage.setItem(draftKey(), JSON.stringify({ ...current, xml: $('event-xml').value, listingXml: $('listing-xml').value })); message(t('Szkic zapisany tylko w tej przeglądarce; gra nadal używa opublikowanych plików.')); });
    $('load-draft').onclick = guard(async () => {
        const draft = localStorage.getItem(draftKey()); if (!draft) throw new Error(t('Brak szkicu dla tego folderu.'));
        if (!confirm(t('Zastąpić bieżący formularz szkicem z tej przeglądarki?'))) return;
        await open(JSON.parse(draft)); changed();
    });
    window.addEventListener('beforeunload', e => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });
    guard(async () => {
        const game = new URLSearchParams(location.search).get('game'); if (game === 'GT5' || game === 'GT6') $('game').value = game;
        updateCategories();
        const catalogResponse = await fetch('/data/event-catalog.json');
        if (!catalogResponse.ok) throw new Error(t('Nie udało się wczytać katalogu aut i tras.'));
        reference = await catalogResponse.json();
        const servers = await api('servers'); servers.forEach(s => { const o = node('option', s, $('server')); o.value = s; });
        if (servers.includes('dev')) $('server').value = 'dev';
        if (servers.length) await refresh(); else message(t('Nie znaleziono zasobów eventów.'), true);
    })();
})();
