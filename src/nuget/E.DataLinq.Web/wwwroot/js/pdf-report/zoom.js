/*
 * PDF report - zoom.
 *
 * Zoom controls: scales the pages container via CSS zoom and keeps the current page centered.
 */

const PDF_ZOOM_STEPS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3];

// Current zoom level (1 = 100%).
let pdfZoomLevel = 1;

// Zoom level used by the reset button / Ctrl+0 (1 = 100%; the mobile view uses fit-to-width).
let pdfZoomResetLevel = 1;

// Applies a zoom level without scrolling; replaced by initializeZoom once the controls exist.
let setZoomLevel = () => { };

// Applies a zoom level and keeps the current page centered; replaced by initializeZoom.
let applyZoomLevel = () => { };

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

    applyZoomLevel = apply;

    const step = (dir) => {
        const steps = [...new Set([...PDF_ZOOM_STEPS, pdfZoomResetLevel])].sort((a, b) => a - b);
        let next;
        if (dir > 0) next = steps.find(z => z > pdfZoomLevel + 0.001) ?? steps[steps.length - 1];
        else next = [...steps].reverse().find(z => z < pdfZoomLevel - 0.001) ?? steps[0];
        apply(next);
    };

    nav.querySelector('.pdf-zoom-in').addEventListener('click', () => step(1));
    nav.querySelector('.pdf-zoom-out').addEventListener('click', () => step(-1));
    resetBtn.addEventListener('click', () => apply(pdfZoomResetLevel));

    window.addEventListener('keydown', (e) => {
        if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
        if (e.key === '+' || e.key === '=') { e.preventDefault(); step(1); }
        else if (e.key === '-') { e.preventDefault(); step(-1); }
        else if (e.key === '0') { e.preventDefault(); apply(pdfZoomResetLevel); }
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
