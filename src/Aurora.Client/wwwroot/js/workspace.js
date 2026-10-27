window.auroraWorkspace = (() => {
    let popup, onFocus, onStorage;
    // Native dialogs handle Escape through their cancel event. Older overlay
    // dialogs use their close button so Escape follows the same busy guards.
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || event.defaultPrevented || event.repeat) return;
        const nativeDialog = [...document.querySelectorAll('dialog[open]')].at(-1);
        const scope = nativeDialog ?? document;
        const overlays = [...scope.querySelectorAll('.modal-backdrop [role="dialog"]')]
            .filter(dialog => dialog.getClientRects().length > 0);
        const overlay = overlays.at(-1);
        if (!overlay) return;
        const close = overlay.querySelector(':scope > .modal-header .modal-close');
        if (!close) return;
        event.preventDefault();
        event.stopPropagation();
        if (!close.disabled) close.click();
    });
    return {
        showEditor(dialog, dotnet) {
            dialog.addEventListener('cancel', event => { event.preventDefault(); dotnet.invokeMethodAsync('Close').catch(() => {}); });
            if (!dialog.open) dialog.showModal();
        },
        closeEditor(dialog) { if (dialog?.open) dialog.close(); },
        scrollOption(id) { document.getElementById(id)?.scrollIntoView({ block: 'nearest' }); },
        open() { popup = window.open('about:blank', '_blank', 'popup,width=1350,height=920,resizable=yes,scrollbars=yes'); },
        navigate(url) { if (popup && !popup.closed) popup.location.replace(new URL(url, window.location.origin).href); },
        cancel() { if (popup && !popup.closed && popup.location.href === 'about:blank') popup.close(); },
        utc(value) { return new Date(value).toISOString(); },
        changed() { localStorage.setItem('aurora-manifests-updated', Date.now().toString()); },
        reset() {
            for (const key of Object.keys(sessionStorage)) {
                if (key.startsWith('aurora-planning-session')) sessionStorage.removeItem(key);
            }
            this.changed();
        },
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

// Each window keeps its own Blazor and Leaflet events; only route data is shared.
window.auroraMapPopout = (() => {
    let snapshot = null, version = 0, serialized = '', popup, timer, busy = false;
    const name = 'aurora-map-' + crypto.randomUUID();
    return {
        publish(value) {
            const json = JSON.stringify(value);
            if (json !== serialized) { serialized = json; snapshot = value; version++; }
        },
        read() { return { version, snapshot }; },
        open() {
            if (popup && !popup.closed) { popup.focus(); return; }
            popup = window.open('/aurora/planning?mapOnly=true', name, 'popup,width=1200,height=900,resizable=yes,scrollbars=yes');
            if (!popup) window.alert('Allow popups for Aurora to open the map panel.');
        },
        listen(dotnet) {
            clearInterval(timer);
            let last = -1;
            const sync = async () => {
                if (busy) return;
                busy = true;
                try {
                    const state = window.opener && !window.opener.closed ? window.opener.auroraMapPopout?.read() : null;
                    const next = state?.version ?? -2;
                    if (next !== last) { await dotnet.invokeMethodAsync('ReceiveMapSnapshot', state?.snapshot ?? null); last = next; }
                } catch { clearInterval(timer); }
                finally { busy = false; }
            };
            timer = setInterval(sync, 1000); sync();
        },
        stop(child) { if(child) clearInterval(timer); else {snapshot=null;serialized='';version++;} }
    };
})();
