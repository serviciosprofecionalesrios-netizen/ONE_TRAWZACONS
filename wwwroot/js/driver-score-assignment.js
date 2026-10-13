document.querySelectorAll('.driver-score-assignment').forEach(panel => {
    const site = panel.querySelector('.assignment-site');
    const responsible = panel.querySelector('.assignment-responsible');
    function update() {
        for (const option of responsible.options) {
            if (!option.value) continue;
            const available = (option.dataset.sites || '').split('|').includes(site.value);
            option.hidden = !available;
            option.disabled = !available;
            if (!available && option.selected) responsible.value = '';
        }
        responsible.disabled = !site.value;
    }
    site.addEventListener('change', update);
    update();
});
