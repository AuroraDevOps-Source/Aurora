// Browser-owned credentials stay in the native form until the user submits it.
// Do not cache credentials or copy them to cookies, localStorage or sessionStorage.
window.auroraLogin = Object.freeze({
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
