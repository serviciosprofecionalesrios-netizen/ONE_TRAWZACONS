function initializeFleetBoard() {
    updateFleetBreakdownTimes();
    if (!window.bootstrap?.Dropdown) return;
    document.querySelectorAll('.fleet-board [data-bs-toggle="dropdown"]').forEach(toggle => {
        bootstrap.Dropdown.getOrCreateInstance(toggle, {
            boundary: 'viewport', offset: [0, 8],
            popperConfig: config => ({ ...config, strategy: 'fixed' })
        });
    });
}
document.addEventListener('DOMContentLoaded', initializeFleetBoard);
document.addEventListener('toneladas:updated', initializeFleetBoard);

function fleetElapsedLabel(reportedAt) {
    var minutes = Math.max(0, Math.floor((Date.now() - Number(reportedAt)) / 60000));
    return Math.floor(minutes / 1440) + ' d ' + Math.floor(minutes % 1440 / 60) + ' h ' + minutes % 60 + ' min';
}

function updateFleetBreakdownTimes() {
    document.querySelectorAll('.fleet-board [data-breakdown-reported-at]').forEach(function (element) {
        element.textContent = fleetElapsedLabel(element.getAttribute('data-breakdown-reported-at'));
    });
    document.querySelectorAll('.fleet-board [data-breakdown-events]').forEach(function (button) {
        try {
            var events = JSON.parse(button.getAttribute('data-breakdown-events') || '[]');
            button.title = (button.getAttribute('data-tooltip-base') || '') + events.map(function (event) {
                return '\n\n' + event.details + '\nTiempo transcurrido: ' + fleetElapsedLabel(event.reportedAt);
            }).join('');
        } catch (_) { /* Keep the server-rendered tooltip if data is unavailable. */ }
    });
}

setInterval(updateFleetBreakdownTimes, 60000);
document.addEventListener('mouseover', function (event) {
    if (event.target.closest && event.target.closest('.fleet-board .equipo-chip')) updateFleetBreakdownTimes();
});
