import { createRpcnYaml } from './rpcn-account.mjs';

// Delegation and observation keep this form working after tutorial AJAX navigation.
function initialize() {
    for (const form of document.querySelectorAll('[data-rpcn-generator]:not([data-rpcn-ready])')) {
        form.dataset.rpcnReady = 'true';
        form.querySelector('fieldset').disabled = false;
    }
}
initialize();
new MutationObserver(initialize).observe(document.documentElement, { childList: true, subtree: true });

document.addEventListener('submit', async event => {
    const form = event.target;
    if (!form.matches('[data-rpcn-generator]')) return;
    event.preventDefault();
    const fieldset = form.querySelector('fieldset');
    if (fieldset.disabled || !form.reportValidity()) return;
    const status = form.querySelector('[data-rpcn-status]');
    const password = form.querySelector('[data-rpcn-password]');
    fieldset.disabled = true;
    status.textContent = form.dataset.working;
    try {
        const yaml = await createRpcnYaml({
            username: form.querySelector('[data-rpcn-username]').value,
            password: password.value,
            token: form.querySelector('[data-rpcn-token]').value
        });
        const url = URL.createObjectURL(new Blob([yaml], { type: 'application/yaml;charset=utf-8' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = 'rpcn.yml';
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 30000);
        password.value = '';
        status.textContent = form.dataset.success;
    } catch {
        status.textContent = form.dataset.error;
    } finally {
        fieldset.disabled = false;
    }
});
