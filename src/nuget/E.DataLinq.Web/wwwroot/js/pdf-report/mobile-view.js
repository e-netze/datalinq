/*
 * PDF report - mobile view.
 *
 * On phones/tablets the pages are scaled to fit the screen width, the page navigator
 * becomes a compact bottom toolbar, the page indicator opens a
 * "go to page" sheet and two-finger pinch zooms the pages (the report layout disables
 * native viewport zoom). Force on/off for testing with ?_mobileView=true|false.
 */

// Horizontal space (CSS px) kept free next to the pages in the mobile view.
const PDF_MOBILE_SIDE_PADDING = 16;

// Pinch zoom limits relative to the fit-to-width level.
const PDF_MOBILE_MIN_ZOOM_FACTOR = 0.5;
const PDF_MOBILE_MAX_ZOOM = 4;

/**
 * Detects phones and tablets: User-Agent Client Hints where available, the classic
 * UA tokens, iPadOS (reports as "Macintosh" but has multi-touch) and finally a
 * touch-only primary pointer.
 * @returns {boolean} True when the mobile view should be used.
 */
function isPdfMobileDevice() {
    const forced = typeof urlParams !== 'undefined' ? urlParams.get('_mobileView') : null;
    if (forced === 'true') return true;
    if (forced === 'false') return false;

    if (navigator.userAgentData && navigator.userAgentData.mobile === true) return true;

    const ua = navigator.userAgent || '';
    if (/Android|iPhone|iPad|iPod|Mobi|Silk|Kindle/i.test(ua)) return true;
    if (/Macintosh/i.test(ua) && navigator.maxTouchPoints > 1) return true;

    return window.matchMedia('(pointer: coarse) and (hover: none)').matches;
}

/**
 * Turns the page navigator into the mobile toolbar and wires up fit-to-width,
 * pinch zoom and the go-to-page sheet. No-op on desktop.
 * @param {HTMLElement} nav The page navigator element.
 * @param {HTMLElement[]} pageList Pages of the report.
 * @param {Function} goTo Navigates to a 1-based page number.
 * @param {Function} getCurrent Returns the current 1-based page number.
 * @returns {boolean} True when the mobile view was activated.
 */
function initializeMobileView(nav, pageList, goTo, getCurrent) {
    if (!isPdfMobileDevice()) return false;

    const container = document.getElementById('pagesContainer');
    if (!container) return false;

    document.body.classList.add('pdf-mobile-view');
    nav.classList.add('pdf-mobile-toolbar');

    const resetBtn = nav.querySelector('.pdf-zoom-reset');
    if (resetBtn) resetBtn.title = 'Fit to width';

    // --- Fit to width -------------------------------------------------------------

    const computeFitLevel = () => {
        const widest = Math.max(...pageList.map(p => p.offsetWidth), 1);
        const available = document.documentElement.clientWidth - PDF_MOBILE_SIDE_PADDING;
        return Math.max(0.1, Math.min(1, available / widest));
    };

    pdfZoomResetLevel = computeFitLevel();
    setZoomLevel(pdfZoomResetLevel);

    let resizeTimer = null;
    const onResize = () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            const wasFit = Math.abs(pdfZoomLevel - pdfZoomResetLevel) < 0.001;
            pdfZoomResetLevel = computeFitLevel();
            if (wasFit) applyZoomLevel(pdfZoomResetLevel);
        }, 150);
    };
    window.addEventListener('resize', onResize);
    window.addEventListener('orientationchange', onResize);

    // --- Pinch zoom -----------------------------------------------------------------

    const touchDistance = (t) => Math.hypot(t[0].clientX - t[1].clientX, t[0].clientY - t[1].clientY);
    let pinch = null;
    let pinchFrame = 0;

    container.addEventListener('touchstart', (e) => {
        if (e.touches.length !== 2) return;
        const root = document.scrollingElement || document.documentElement;
        const midX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
        const midY = (e.touches[0].clientY + e.touches[1].clientY) / 2;
        pinch = {
            distance: touchDistance(e.touches),
            zoom: pdfZoomLevel,
            midX, midY,
            // Document position under the fingers, in unzoomed units.
            docX: (root.scrollLeft + midX) / pdfZoomLevel,
            docY: (root.scrollTop + midY) / pdfZoomLevel
        };
    }, { passive: true });

    container.addEventListener('touchmove', (e) => {
        if (!pinch || e.touches.length !== 2) return;
        e.preventDefault();
        const ratio = touchDistance(e.touches) / pinch.distance;
        const level = Math.min(PDF_MOBILE_MAX_ZOOM,
            Math.max(pdfZoomResetLevel * PDF_MOBILE_MIN_ZOOM_FACTOR, pinch.zoom * ratio));
        cancelAnimationFrame(pinchFrame);
        pinchFrame = requestAnimationFrame(() => {
            setZoomLevel(level);
            window.scrollTo(pinch.docX * level - pinch.midX, pinch.docY * level - pinch.midY);
        });
    }, { passive: false });

    const endPinch = (e) => {
        if (!pinch || e.touches.length >= 2) return;
        pinch = null;
        // Snap back to fit when the user pinched just below it.
        if (pdfZoomLevel < pdfZoomResetLevel && pdfZoomLevel > pdfZoomResetLevel * 0.85) {
            applyZoomLevel(pdfZoomResetLevel);
        } else {
            saveViewState(pageList);
        }
    };
    container.addEventListener('touchend', endPinch, { passive: true });
    container.addEventListener('touchcancel', endPinch, { passive: true });

    // --- Go-to-page sheet -------------------------------------------------------------

    const input = nav.querySelector('.pdf-nav-input');
    const total = pageList.length;

    const sheet = document.createElement('div');
    sheet.className = 'pdf-mobile-sheet-backdrop';
    sheet.innerHTML =
        '<div class="pdf-mobile-sheet" role="dialog" aria-modal="true" aria-label="Go to page">' +
        '<div class="pdf-mobile-sheet-title">Go to page</div>' +
        '<form class="pdf-mobile-sheet-form">' +
        `<input type="number" inputmode="numeric" pattern="[0-9]*" min="1" max="${total}" class="pdf-mobile-sheet-input" />` +
        `<span class="pdf-mobile-sheet-total">/ ${total}</span>` +
        '<button type="submit" class="pdf-mobile-sheet-go">Go</button>' +
        '</form>' +
        '<div class="pdf-mobile-sheet-shortcuts">' +
        '<button type="button" data-page="first">First page</button>' +
        '<button type="button" data-page="last">Last page</button>' +
        '</div>' +
        '</div>';
    document.body.appendChild(sheet);

    const sheetInput = sheet.querySelector('.pdf-mobile-sheet-input');
    const closeSheet = () => {
        sheet.classList.remove('open');
        sheetInput.blur();
    };
    const openSheet = () => {
        sheetInput.value = getCurrent();
        sheet.classList.add('open');
        setTimeout(() => { sheetInput.focus(); sheetInput.select(); }, 50);
    };

    if (input) {
        input.readOnly = true;
        input.setAttribute('aria-label', 'Current page, tap to go to a page');
        input.addEventListener('click', (e) => { e.preventDefault(); openSheet(); });
    }

    sheet.addEventListener('click', (e) => { if (e.target === sheet) closeSheet(); });
    sheet.querySelector('.pdf-mobile-sheet-form').addEventListener('submit', (e) => {
        e.preventDefault();
        const n = parseInt(sheetInput.value);
        if (!isNaN(n)) goTo(n);
        closeSheet();
    });
    sheet.querySelector('[data-page="first"]').addEventListener('click', () => { goTo(1); closeSheet(); });
    sheet.querySelector('[data-page="last"]').addEventListener('click', () => { goTo(total); closeSheet(); });
    window.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeSheet(); });

    return true;
}
