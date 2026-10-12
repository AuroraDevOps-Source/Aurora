export function open(dialog, receiver) {
    dialog.addEventListener('close', () => receiver.invokeMethodAsync('OrderDialogClosed').catch(() => {}), { once: true });
    dialog.showModal();
}
export function close(dialog) { dialog.close(); }
