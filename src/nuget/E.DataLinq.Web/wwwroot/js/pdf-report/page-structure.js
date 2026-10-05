/*
 * PDF report - page structure.
 *
 * Prepares the page DOM before pagination: flattens finished includes and applies
 * manual page breaks.
 */

/**
 * Replaces every finished `div.datalinq-include` wrapper inside a page with its
 * children, so included content takes part in pagination like regular content.
 * @returns {void}
 */
function unwrapFinishedIncludes() {
    const pages = Array.from(document.querySelectorAll('.page'));

    pages.forEach(page => {
        const wrappers = Array.from(page.querySelectorAll('div.datalinq-include.finished'));

        wrappers.forEach(wrapper => {
            const parent = wrapper.parentNode;
            if (!parent) return;

            while (wrapper.firstChild) {
                parent.insertBefore(wrapper.firstChild, wrapper);
            }

            parent.removeChild(wrapper);
        });
    });
}

/**
 * Manual page breaks: every `.page-break` marker (emitted by @PDF.PageBreak between
 * NewPage/EndPage) starts a fresh continuation page. Everything after the marker on the
 * same page is moved onto a new page that mirrors the parent page's metadata (paper size,
 * orientation, template, dynamic table options and margins) via createContinuationPageWrapper.
 * Runs before splitAllTables so the resulting pages still participate in dynamic pagination.
 * @returns {void}
 */
function applyManualPageBreaks() {
    const pagesContainer = document.getElementById('pagesContainer');
    if (!pagesContainer) return;

    let guard = 0;
    let pageBreak = pagesContainer.querySelector('.page-break');

    while (pageBreak && guard++ < MAX_PAGINATION_ITERATIONS) {
        const page = pageBreak.closest('.page');
        const wrapper = pageBreak.closest('.page-wrapper');

        if (!page || !wrapper) {
            pageBreak.remove();
            pageBreak = pagesContainer.querySelector('.page-break');
            continue;
        }

        // A break nested inside other markup (e.g. a draggable) acts as a break after
        // the top-level element of the page that contains it.
        let breakAnchor = pageBreak;
        while (breakAnchor.parentElement && breakAnchor.parentElement !== page) {
            breakAnchor = breakAnchor.parentElement;
        }

        const firstToMove = breakAnchor === pageBreak ? pageBreak.nextElementSibling : breakAnchor.nextElementSibling;
        pageBreak.remove();

        let hasContentToMove = false;
        for (let el = firstToMove; el; el = el.nextElementSibling) {
            if (!el.classList.contains('report-ignore')) {
                hasContentToMove = true;
                break;
            }
        }

        if (!hasContentToMove) {
            pageBreak = pagesContainer.querySelector('.page-break');
            continue;
        }

        const nextWrapper = createContinuationPageWrapper(page, wrapper);
        const nextPage = nextWrapper.querySelector('.page');

        let current = firstToMove;
        let firstMovedElement = null;

        while (current) {
            const next = current.nextElementSibling;

            if (!current.classList.contains('report-ignore')) {
                nextPage.appendChild(current);

                if (!firstMovedElement) {
                    firstMovedElement = current;
                }
            }

            current = next;
        }

        normalizeFirstMovedElement(firstMovedElement);
        shiftMovedDraggablesToTop(nextPage);
        insertPageAfter(wrapper, nextWrapper, pagesContainer);

        pageBreak = pagesContainer.querySelector('.page-break');
    }
}

/**
 * Draggables moved by a manual page break still carry the vertical offset they had on
 * the original page. Shifts them up (keeping their relative distances) so the topmost
 * one starts at the top of the new page.
 * @param {HTMLElement} page The newly created page.
 * @returns {void}
 */
function shiftMovedDraggablesToTop(page) {
    const draggables = Array.from(page.children).filter(child => child.classList.contains('element'));
    if (draggables.length === 0) return;

    const getY = el => {
        const y = parseFloat(el.getAttribute('data-y'));
        return Number.isFinite(y) ? y : new DOMMatrix(getComputedStyle(el).transform).m42 || 0;
    };
    const getX = el => {
        const x = parseFloat(el.getAttribute('data-x'));
        return Number.isFinite(x) ? x : new DOMMatrix(getComputedStyle(el).transform).m41 || 0;
    };

    const minY = Math.min(...draggables.map(getY));
    if (!(minY > 0)) return;

    draggables.forEach(el => {
        const x = getX(el);
        const y = getY(el) - minY;
        el.setAttribute('data-x', x.toString());
        el.setAttribute('data-y', y.toString());
        el.style.transform = `translate(${x}px, ${y}px)`;
    });
}
