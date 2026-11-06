(() => {
    'use strict';
    function initialize() {
        const viewport = document.getElementById('docTableViewport');
        const toolbar = document.getElementById('docScrollToolbar');
        const top = document.getElementById('docTopScroll');
        const track = document.getElementById('docTopScrollTrack');
        if (!viewport || !toolbar || !top || !track) return;
        const table = viewport.querySelector('table');
        const left = document.getElementById('docScrollLeft');
        const right = document.getElementById('docScrollRight');
        function updateButtons() {
            left.disabled = viewport.scrollLeft <= 1;
            right.disabled = viewport.scrollLeft >= viewport.scrollWidth - viewport.clientWidth - 1;
        }
        function resize() {
            const range = Math.max(0, viewport.scrollWidth - viewport.clientWidth);
            toolbar.hidden = range <= 1 || viewport.clientWidth === 0;
            if (!toolbar.hidden) {
                track.style.width = (top.clientWidth + range) + 'px';
                top.scrollLeft = viewport.scrollLeft;
            }
            updateButtons();
        }
        viewport.addEventListener('scroll', () => {
            if (Math.abs(top.scrollLeft - viewport.scrollLeft) > 1) top.scrollLeft = viewport.scrollLeft;
            updateButtons();
        }, { passive: true });
        top.addEventListener('scroll', () => {
            if (Math.abs(viewport.scrollLeft - top.scrollLeft) > 1) viewport.scrollLeft = top.scrollLeft;
            updateButtons();
        }, { passive: true });
        const move = direction => viewport.scrollBy({ left: direction * Math.max(180, viewport.clientWidth * 0.7), behavior: 'auto' });
        left.addEventListener('click', () => move(-1));
        right.addEventListener('click', () => move(1));
        if (typeof ResizeObserver !== 'undefined') {
            const observer = new ResizeObserver(resize);
            observer.observe(viewport);
            observer.observe(top);
            if (table) observer.observe(table);
        }
        window.addEventListener('resize', resize, { passive: true });
        document.fonts?.ready.then(resize);
        resize();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
    else initialize();
})();
