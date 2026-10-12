// Load the app in parallel while keeping the branded splash visible for demos.
(async () => {
    const splash = document.getElementById("aurora-startup");
    const logo = splash.querySelector("img");
    const minimumDisplay = (async () => {
        try { await logo.decode(); } catch { /* Still start if the image cannot load. */ }
        // Begin timing after the browser has had a chance to paint the logo.
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        await new Promise(resolve => setTimeout(resolve, 3000));
    })();
    try {
        await Promise.all([Blazor.start(), minimumDisplay]);
        splash.remove();
    } catch (error) {
        console.error("Aurora could not start.", error);
        splash.remove();
        document.getElementById("blazor-error-ui").style.display = "block";
    }
})();
