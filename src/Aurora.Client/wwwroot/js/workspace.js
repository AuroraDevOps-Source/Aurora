window.auroraWorkspace = (() => {
    let popup, onFocus, onStorage;
    return {
        open() { popup = window.open('about:blank', '_blank', 'popup,width=1350,height=920,resizable=yes,scrollbars=yes'); },
        navigate(url) { if (popup && !popup.closed) popup.location.replace(new URL(url, window.location.origin).href); },
        cancel() { if (popup && !popup.closed && popup.location.href === 'about:blank') popup.close(); },
        utc(value) { return new Date(value).toISOString(); },
        changed() { localStorage.setItem('aurora-manifests-updated', Date.now().toString()); },
        finish() {
            try { this.changed(); } catch { /* Storage may be disabled; still close after a confirmed save. */ }
            try { if (window.opener && !window.opener.closed) window.opener.focus(); } catch { }
            // A directly opened tab may not be script-closable. Keep a useful fallback.
            window.setTimeout(() => window.location.assign('/aurora/manifests'), 150);
            window.close();
        },
        watch(dotnet) {
            this.unwatch();
            onFocus = () => dotnet.invokeMethodAsync('WorkspaceChanged').catch(() => {});
            onStorage = e => { if(e.key === 'aurora-manifests-updated') onFocus(); };
            window.addEventListener('focus', onFocus); window.addEventListener('storage', onStorage);
        },
        unwatch() { if(onFocus) window.removeEventListener('focus',onFocus); if(onStorage) window.removeEventListener('storage',onStorage); onFocus=onStorage=null; }
    };
})();
