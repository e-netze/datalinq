/*
 * PDF report - zoom.
 *
 * Zoom controls: scales the pages container via CSS zoom and keeps the current page centered.
 */

const PDF_ZOOM_STEPS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3];

// Current zoom level (1 = 100%).
let pdfZoomLevel = 1;

// Applies a zoom level without scrolling; replaced by initializeZoom once the controls exist.
let setZoomLevel = () => { };

/**
 * Wires up the zoom buttons and Ctrl/Cmd +, -, 0 keyboard shortcuts.
 * @param {HTMLElement} nav Navigator element containing the zoom buttons.
 * @param {Function} getCurrentPage Returns the page that should stay centered after zooming.
 * @param {Function} [onChange] Called after every zoom change (e.g. to save the view state).
 * @returns {void}
 */
function initializeZoom(nav, getCurrentPage, onChange) {
    const container = document.getElementById('pagesContainer');
    if (!container) return;

    const resetBtn = nav.querySelector('.pdf-zoom-reset');

    setZoomLevel = (level) => {
        pdfZoomLevel = level;
        container.style.zoom = level;
        resetBtn.textContent = `${Math.round(level * 100)}%`;
    };

    const apply = (level) => {
        const page = getCurrentPage();
        setZoomLevel(level);
        if (page) page.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'center' });
        if (onChange) onChange();
    };

    const step = (dir) => {
        const idx = PDF_ZOOM_STEPS.findIndex(z => z >= pdfZoomLevel - 0.001);
        const next = PDF_ZOOM_STEPS[Math.min(Math.max(0, (idx < 0 ? PDF_ZOOM_STEPS.length - 1 : idx) + dir), PDF_ZOOM_STEPS.length - 1)];
        apply(next);
    };

    nav.querySelector('.pdf-zoom-in').addEventListener('click', () => step(1));
    nav.querySelector('.pdf-zoom-out').addEventListener('click', () => step(-1));
    resetBtn.addEventListener('click', () => apply(1));

    window.addEventListener('keydown', (e) => {
        if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
        if (e.key === '+' || e.key === '=') { e.preventDefault(); step(1); }
        else if (e.key === '-') { e.preventDefault(); step(-1); }
        else if (e.key === '0') { e.preventDefault(); apply(1); }
    });
}

/**
 * Temporarily resets zoom to 100% (e.g. while rasterizing pages).
 * @returns {Function} Restore callback that re-applies the previous zoom level.
 */
function suspendZoom() {
    const container = document.getElementById('pagesContainer');
    if (!container || pdfZoomLevel === 1) return () => { };
    container.style.zoom = 1;
    return () => { container.style.zoom = pdfZoomLevel; };
}
