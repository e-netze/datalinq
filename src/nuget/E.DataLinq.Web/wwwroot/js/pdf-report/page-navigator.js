/*
 * PDF report - page navigator.
 *
 * Floating toolbar with prev/next buttons, an editable "page X / Y" input, zoom
 * controls and the download/print buttons. Tracks the currently visible page.
 */

// Markup of the navigator controls (page navigation + zoom).
const PAGE_NAVIGATOR_HTML =
    '<div class="pdf-nav-controls">' +
    '<button type="button" class="pdf-nav-prev" title="Previous page">&#9650;</button>' +
    '<input type="number" class="pdf-nav-input" min="1" />' +
    '<span class="pdf-nav-total"></span>' +
    '<button type="button" class="pdf-nav-next" title="Next page">&#9660;</button>' +
    '</div>' +
    '<div class="pdf-nav-controls pdf-zoom-controls">' +
    '<button type="button" class="pdf-zoom-out" title="Zoom out">&minus;</button>' +
    '<button type="button" class="pdf-zoom-reset" title="Reset zoom">100%</button>' +
    '<button type="button" class="pdf-zoom-in" title="Zoom in">+</button>' +
    '</div>';

/**
 * Creates a toolbar button styled like the other PDF buttons.
 * @param {string} id Element id of the button.
 * @param {string} html Inner HTML (icon + label).
 * @param {Function} onClick Click handler.
 * @returns {HTMLButtonElement} The new button.
 */
function createPdfToolbarButton(id, html, onClick) {
    const button = document.createElement('button');
    button.type = 'button';
    button.id = id;
    button.innerHTML = html;
    button.classList.add('datalinq-button-pdf');
    button.addEventListener('click', onClick);
    return button;
}

/**
 * Builds the floating navigator, wires up page navigation, zoom and view-state
 * persistence and appends it to the body. Does nothing in auto-download mode or
 * when the report has no pages.
 * @returns {void}
 */
function initializePageNavigator() {
    if (urlParams.get('_autoDownload') === 'true') return;

    const pages = () => [...document.querySelectorAll('.page')];
    if (pages().length === 0) return;

    const nav = document.createElement('div');
    nav.className = 'pdf-page-navigator';
    nav.innerHTML = PAGE_NAVIGATOR_HTML;

    // The download button is only added here when the view requested a container
    // for it and did not render its own button.
    if (document.getElementById('downloadBtnContainer') && !document.getElementById('downloadBtn')) {
        nav.appendChild(createPdfToolbarButton('downloadBtn', DOWNLOAD_BUTTON_HTML, onDownloadButtonClick));
    }

    nav.appendChild(createPdfToolbarButton('printBtn', PRINT_BUTTON_HTML, () => window.print()));

    document.body.appendChild(nav);

    const input = nav.querySelector('.pdf-nav-input');
    const totalSpan = nav.querySelector('.pdf-nav-total');
    let current = 0;
    let pageList = pages();
    const visibleHeights = new Map();

    totalSpan.textContent = `/ ${pageList.length}`;
    input.max = pageList.length;

    const setCurrent = (n) => {
        if (n === current) return;
        current = n;
        if (document.activeElement !== input) input.value = current;
    };

    const update = () => {
        if (lockedToTarget) return;
        let best = current || 1, bestVisible = 0, bestDistance = Infinity;
        const centerY = window.innerHeight / 2;
        visibleHeights.forEach((h, idx) => {
            if (h < bestVisible - 1) return;
            // tie-break (e.g. several fully visible pages when zoomed out): closest to viewport center
            const r = pageList[idx].getBoundingClientRect();
            const distance = Math.abs((r.top + r.bottom) / 2 - centerY);
            if (h > bestVisible + 1 || distance < bestDistance) {
                bestVisible = h; bestDistance = distance; best = idx + 1;
            }
        });
        setCurrent(best);
    };

    // After a button/input jump, keep the chosen page as current until the user scrolls
    // manually; otherwise, with several pages fully visible (zoomed out) or at the end of
    // the document, the counter would snap back and next/prev would get stuck.
    let lockedToTarget = false;
    const unlock = () => { lockedToTarget = false; };
    ['wheel', 'touchstart', 'mousedown'].forEach(evt =>
        window.addEventListener(evt, (e) => { if (!nav.contains(e.target)) unlock(); }, { passive: true }));
    window.addEventListener('keydown', (e) => {
        if (!nav.contains(e.target) && ['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' '].includes(e.key)) unlock();
    });

    // IntersectionObserver avoids measuring every page on each scroll frame.
    const thresholds = Array.from({ length: 21 }, (_, i) => i / 20);
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            const idx = pageList.indexOf(entry.target);
            if (idx < 0) return;
            if (entry.isIntersecting) visibleHeights.set(idx, entry.intersectionRect.height);
            else visibleHeights.delete(idx);
        });
        update();
    }, { threshold: thresholds });
    pageList.forEach(p => observer.observe(p));

    const goTo = (n) => {
        const target = Math.min(Math.max(1, n), pageList.length);
        const distance = Math.abs(target - (current || 1));
        pageList[target - 1].scrollIntoView({ behavior: distance > 2 ? 'auto' : 'smooth', block: 'center' });
        lockedToTarget = true;
        current = target;
        input.value = target;
    };

    nav.querySelector('.pdf-nav-prev').addEventListener('click', () => goTo(current - 1));
    nav.querySelector('.pdf-nav-next').addEventListener('click', () => goTo(current + 1));

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            const n = parseInt(input.value);
            if (!isNaN(n)) goTo(n);
            input.blur();
        } else if (e.key === 'Escape') {
            input.value = current;
            input.blur();
        }
    });
    input.addEventListener('focus', () => input.select());
    input.addEventListener('blur', () => { input.value = current; });

    setCurrent(1);

    initializeZoom(nav, () => pageList[(current || 1) - 1], saveViewState);

    restoreViewState(pageList);

    let saveTimer = null;
    window.addEventListener('scroll', () => {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => saveViewState(pageList), 200);
    }, { passive: true });
    window.addEventListener('pagehide', () => saveViewState(pageList));
}
