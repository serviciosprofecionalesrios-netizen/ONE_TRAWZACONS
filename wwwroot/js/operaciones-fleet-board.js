document.addEventListener('DOMContentLoaded', () => {
    if (!window.bootstrap?.Dropdown) return;
    document.querySelectorAll('.fleet-board [data-bs-toggle="dropdown"]').forEach(toggle => {
        bootstrap.Dropdown.getOrCreateInstance(toggle, {
            boundary: 'viewport', offset: [0, 8],
            popperConfig: config => ({ ...config, strategy: 'fixed' })
        });
    });
});
