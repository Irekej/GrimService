(() => {
    'use strict';
    const messages = window.GrimI18nData || {};
    const language = document.documentElement.lang;
    const index = ({pl:0,en:1,es:2,fr:3,pt:4,ja:5})[language] ?? 1;
    function t(source) {
        if (typeof source !== 'string') return source;
        const value = messages[source.trim()];
        if (!value) {
            const error = /^Event ([0-9]+)(: .+)$/.exec(source);
            if (error && messages[error[2]]) return t('Event') + ' ' + error[1] + t(error[2]);
            return source;
        }
        return source.match(/^\s*/)[0] + (value[index] || value[1] || source.trim()) + source.match(/\s*$/)[0];
    }
    window.GrimI18n = { t, language };
})();
