/*
 * PDF report - view state.
 *
 * View state (zoom + position) is kept per report in sessionStorage, so reloads
 * (e.g. the live preview after saving code) return to the same zoom and page.
 */

const PDF_VIEW_STATE_KEY = 'datalinq-pdf-view:' + window.location.pathname;
const PDF_VIEW_FOCUS_KEY = 'datalinq-pdf-focus:' + window.location.pathname;

/**
 * Makes the next restoreViewState scroll to the given source page (data-dl-edit-page, zero based).
 * @param {number} sourcePage
 * @returns {void}
 */
function requestViewFocus(sourcePage) {
    try {
        sessionStorage.setItem(PDF_VIEW_FOCUS_KEY, JSON.stringify({ sourcePage: Math.max(0, sourcePage | 0) }));
    } catch { }
}

/**
 * Saves zoom and scroll position to sessionStorage. The position is stored as page
 * index + relative offset of the viewport center within that page, so it survives
 * zoom changes and small layout differences.
 * @param {HTMLElement[]} [pageList] Pages of the report (defaults to all `.page` elements).
 * @returns {void}
 */
function saveViewState(pageList) {
    pageList = pageList || [...document.querySelectorAll('.page')];
    if (pageList.length === 0) return;

    const centerY = window.innerHeight / 2;
    let pageIndex = 0, bestDistance = Infinity, offset = 0;
    pageList.forEach((page, i) => {
        const r = page.getBoundingClientRect();
        const distance = centerY < r.top ? r.top - centerY : centerY > r.bottom ? centerY - r.bottom : 0;
        if (distance < bestDistance) {
            bestDistance = distance;
            pageIndex = i;
            offset = r.height > 0 ? Math.min(Math.max((centerY - r.top) / r.height, 0), 1) : 0;
        }
    });

    try {
        sessionStorage.setItem(PDF_VIEW_STATE_KEY, JSON.stringify({ zoom: pdfZoomLevel, page: pageIndex, offset: offset }));
    } catch { }
}

/**
 * Restores zoom and scroll position previously saved by saveViewState.
 * @param {HTMLElement[]} pageList Pages of the report.
 * @returns {void}
 */
function restoreViewState(pageList) {
    let state = null, focus = null;
    try {
        state = JSON.parse(sessionStorage.getItem(PDF_VIEW_STATE_KEY));
        // one-shot target page requested before a reload (e.g. a page inserted in editing mode)
        focus = JSON.parse(sessionStorage.getItem(PDF_VIEW_FOCUS_KEY));
        sessionStorage.removeItem(PDF_VIEW_FOCUS_KEY);
    } catch { }
    if (focus && Number.isInteger(focus.sourcePage)) {
        // first rendered page of the requested source page (NewPage index), fallback: last page
        let index = pageList.findIndex(p => parseInt(p.getAttribute('data-dl-edit-page'), 10) === focus.sourcePage);
        if (index < 0) index = pageList.length - 1;
        state = Object.assign({}, state, { page: index, offset: 0.5 });
    }
    if (!state) return;

    if (typeof state.zoom === 'number' && state.zoom !== 1) {
        setZoomLevel(state.zoom);
    }

    const page = pageList[Math.min(Math.max(0, state.page | 0), pageList.length - 1)];
    if (!page) return;

    const r = page.getBoundingClientRect();
    const target = r.top + (state.offset || 0) * r.height;
    window.scrollBy({ top: target - window.innerHeight / 2, behavior: 'auto' });

    if (pdfZoomLevel > 1) {
        const root = document.scrollingElement || document.documentElement;
        root.scrollLeft = (root.scrollWidth - root.clientWidth) / 2;
    }
}
