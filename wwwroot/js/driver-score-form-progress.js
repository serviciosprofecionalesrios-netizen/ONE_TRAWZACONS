(function (root) {
    function calculate(values) {
        const filled = values.filter(value => String(value || '').trim().length > 0).length;
        return { filled, total: values.length, percentage: values.length ? Math.round(50 * filled / values.length) : 0 };
    }
    root.DriverScoreFormProgress = { calculate };
    if (typeof document === 'undefined') return;
    const form = document.getElementById('driverScoreCreateForm');
    if (!form) return;
    const fields = [
        ['EventAt', 'Fecha y hora'], ['EventType', 'Tipo de infracción'],
        ['Driver', 'Conductor'], ['Vehicle', 'Unidad'], ['Group', 'Grupo'],
        ['Location', 'Ubicación'], ['Observation', 'Descripción u observación'],
        ['Site', 'Sitio'], ['Responsible', 'Responsable']
    ];
    const alert = document.getElementById('driverScoreMissingFields');
    const progress = document.getElementById('driverScoreFormProgress');
    const label = document.getElementById('driverScoreProgressLabel');
    form.noValidate = true;
    function update() {
        const required = fields.map(([name, title]) => ({ field: form.elements.namedItem(name), title }));
        const coaching = form.querySelector('#CoachingCompleted');
        if (coaching?.checked) required.push({ field: form.elements.namedItem('CoachingEvidence'), title: 'Evidencia del coaching' });
        required.forEach(({ field }) => { if (field) field.required = true; });
        const evidence = form.elements.namedItem('CoachingEvidence');
        if (evidence) evidence.required = !!coaching?.checked;
        const state = calculate(required.map(({ field }) => field?.value));
        const missing = required.filter(({ field }) => !field || !String(field.value || '').trim() || !field.checkValidity());
        progress.querySelector('.progress-bar').style.width = state.percentage + '%';
        progress.querySelector('.progress-bar').textContent = state.percentage + '%';
        progress.setAttribute('aria-valuenow', state.percentage);
        label.textContent = missing.length ? 'Paso 1: ' + state.filled + ' de ' + state.total + ' campos completos. Paso 2 pendiente.' : 'Paso 1 listo para registrar. Al guardar queda bloqueado; paso 2 pendiente.';
        alert.hidden = missing.length === 0;
        alert.textContent = missing.length ? 'Completa antes de guardar: ' + missing.map(x => x.title).join(', ') + '.' : '';
        return missing;
    }
    form.addEventListener('input', update);
    form.addEventListener('change', update);
    form.addEventListener('submit', event => {
        const missing = update();
        if (!missing.length) return;
        event.preventDefault();
        alert.scrollIntoView({ behavior: 'smooth', block: 'center' });
        missing[0].field?.focus();
    });
    update();
})(globalThis);
