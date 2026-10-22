(() => {
    const form = document.getElementById('site-activity-token');
    const token = form?.querySelector('[name="__RequestVerificationToken"]')?.value;
    if (!form || !token) return;
    let lastInput = Date.now();
    let pending = false;
    let expired = false;
    const interaction = () => { lastInput = Date.now(); };
    ['pointerdown', 'pointermove', 'keydown', 'scroll', 'touchstart', 'wheel'].forEach(name => window.addEventListener(name, interaction, { passive: true, capture: true }));
    async function pulse() {
        if (pending || expired) return;
        pending = true;
        const body = new URLSearchParams({ __RequestVerificationToken: token,
            visible: String(document.visibilityState === 'visible'), recentActivity: String(Date.now() - lastInput < 120000) });
        try {
            const response = await fetch(form.dataset.pulseUrl, { method: 'POST', body, credentials: 'same-origin', keepalive: true, cache: 'no-store' });
            if (response.status === 401 || response.status === 403 || response.redirected) expired = true;
        } catch { /* Presence is best effort; never interrupt the user's work. */ }
        finally { pending = false; }
    }
    document.addEventListener('visibilitychange', pulse);
    window.addEventListener('pagehide', pulse);
    window.setInterval(pulse, 30000);
    pulse();
})();
