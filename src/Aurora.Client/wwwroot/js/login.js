// Capture and erase launch secrets before loading the app or third-party scripts.
// A new launch must perform OIDC again, even if this browser had another Aurora user.
let pendingNovaLaunch = null;
if (window.location?.pathname === '/nova-launch') {
    const values = new URLSearchParams(window.location.hash.slice(1));
    window.history.replaceState(null, '', '/nova-launch');
    pendingNovaLaunch = { code: values.get('code'), databaseId: values.get('db'), ticket: values.get('ticket') };
    try {
        for (const storage of [window.sessionStorage, window.localStorage]) {
            if (!storage) continue;
            for (let i = storage.length - 1; i >= 0; i--) {
                const key = storage.key(i);
                if (key?.startsWith('oidc.user:') && key.endsWith(':aurora-spa')) storage.removeItem(key);
            }
        }
    } catch {
        pendingNovaLaunch = null;
    }
}
// Browser-owned credentials stay in the native form until the user submits it.
// Do not cache credentials or copy them to cookies, localStorage or sessionStorage.
window.auroraLogin = Object.freeze({
    takeNovaLaunch() {
        const { code, databaseId, ticket } = pendingNovaLaunch ?? {};
        pendingNovaLaunch = null;
        if (code != null) return /^[0-9a-f]{64}$/.test(code) ? { code } : null;
        if (!/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(databaseId ?? '') ||
            !/^[0-9a-f]{64}$/.test(ticket ?? '')) return null;
        return { databaseId, ticket };
    },
    readCredentials(form) {
        if (!(form instanceof HTMLFormElement) || form.id !== 'aurora-login-form') {
            throw new Error('Expected the Aurora sign-in form.');
        }
        const values = new FormData(form);
        return {
            userName: String(values.get('username') ?? '').trim(),
            password: String(values.get('password') ?? '')
        };
    }
});
