/*
 * PDF report - pagination.
 *
 * Splits content that overflows a dynamic page onto continuation pages (tables
 * are split row by row, other elements are moved). The DOM builders and measuring
 * helpers used here live in pagination-builders.js.
 */

/**
 * Paginates every dynamic page (`.page-wrapper.dynamic`) of the report.
 * @returns {void}
 */
function splitAllTables() {
    const pagesContainer = document.getElementById('pagesContainer');
    if (!pagesContainer) return;

    const originalPageWrappers = Array.from(pagesContainer.querySelectorAll('.page-wrapper.dynamic'));
    originalPageWrappers.forEach(pageWrapper => paginateDynamicPage(pageWrapper, pagesContainer));
}

/**
 * Repeatedly splits a dynamic page while its content overflows, chaining onto each
 * newly created continuation page until nothing overflows (bounded by MAX_PAGINATION_ITERATIONS).
 * @param {HTMLElement} startWrapper The `.page-wrapper` to paginate.
 * @param {HTMLElement} pagesContainer The `#pagesContainer` element.
 * @returns {void}
 */
function paginateDynamicPage(startWrapper, pagesContainer) {
    let currentWrapper = startWrapper;
    let guard = 0;

    while (currentWrapper && guard++ < MAX_PAGINATION_ITERATIONS) {
        const page = currentWrapper.querySelector('.page');
        if (!page) return;

        const margins = getDynamicMargins(currentWrapper);
        const overflowElement = getFirstOverflowElement(page, margins.defaultMarginBottom);
        if (!overflowElement) return;

        const isFirstContent = Array.from(page.children)
            .find(child => !child.classList.contains('report-ignore')) === overflowElement;

        const isTable = overflowElement.tagName.toLowerCase() === 'table';
        const draggableTable = !isTable && overflowElement.classList.contains('element')
            ? overflowElement.querySelector('table')
            : null;

        let nextWrapper;
        if (isTable) {
            nextWrapper = splitOverflowingTable(overflowElement, page, currentWrapper, pagesContainer, margins);
        } else if (draggableTable) {
            nextWrapper = splitOverflowingTable(draggableTable, page, currentWrapper, pagesContainer, margins, overflowElement);
        } else if (isFirstContent) {
            // Moving the only/first content would just leave an empty page behind.
            return;
        } else {
            nextWrapper = moveOverflowingElementsToNextPage(overflowElement, page, currentWrapper, pagesContainer);
        }

        if (!nextWrapper || nextWrapper === currentWrapper) {
            return;
        }

        currentWrapper = nextWrapper;
    }
}

/**
 * Returns the first laid-out child whose bottom edge crosses the page's usable area
 * (ignoring `report-ignore` decorations such as spacers, headers and page numbers).
 * @param {HTMLElement} page The page to inspect.
 * @param {number} [bottomMargin=0] Bottom margin in px that is not usable.
 * @returns {HTMLElement|undefined} The overflowing element, if any.
 */
function getFirstOverflowElement(page, bottomMargin = 0) {
    const pageRect = page.getBoundingClientRect();
    const maxBottom = pageRect.top + pageRect.height - bottomMargin;

    return Array.from(page.children)
        .filter(child => !child.classList.contains('report-ignore'))
        .find(child => child.getBoundingClientRect().bottom > maxBottom + 0.5);
}

/**
 * Splits an overflowing table: keeps the rows that fit on the current page and moves
 * the rest (plus any following siblings) onto a continuation page with a repeated header.
 * Falls back to moving the whole table when it cannot be split meaningfully.
 * @param {HTMLTableElement} table The overflowing table.
 * @param {HTMLElement} page The page containing the table.
 * @param {HTMLElement} currentWrapper The `.page-wrapper` of the page.
 * @param {HTMLElement} pagesContainer The `#pagesContainer` element.
 * @param {{defaultMarginTop: number, defaultMarginBottom: number}} margins Page margins (see getDynamicMargins).
 * @param {HTMLElement|null} [container=null] Draggable (.element) holding the table; the continuation
 *        table is then placed into a copy of the draggable (same x, y reset to 0).
 * @returns {HTMLElement} The `.page-wrapper` to continue paginating with (the new continuation
 *          page, or currentWrapper when nothing could be moved).
 */
function splitOverflowingTable(table, page, currentWrapper, pagesContainer, margins, container = null) {
    const pageChild = container || table;

    const tbody = table.querySelector('tbody');
    if (!tbody) {
        return moveOverflowingElementsToNextPage(pageChild, page, currentWrapper, pagesContainer);
    }

    const rows = Array.from(tbody.querySelectorAll('tr'));
    if (rows.length === 0) {
        return moveOverflowingElementsToNextPage(pageChild, page, currentWrapper, pagesContainer);
    }

    const tableStyles = window.getComputedStyle(table);
    const bottomMargin = margins.defaultMarginBottom;

    let thead = table.querySelector('thead');
    let headerRow = null;
    let theadHeight = 0;
    let startRowIndex = 0;

    if (thead) {
        theadHeight = thead.getBoundingClientRect().height;
    } else {
        const firstRow = rows[0];
        if (firstRow && firstRow.querySelector('th')) {
            headerRow = firstRow;
            theadHeight = firstRow.getBoundingClientRect().height;
            startRowIndex = 1;
        }
    }

    const dataRows = rows.slice(startRowIndex);
    if (dataRows.length === 0) {
        return moveOverflowingElementsToNextPage(pageChild, page, currentWrapper, pagesContainer);
    }

    const rowGap = getRowGap(tableStyles.borderSpacing);
    const pageRect = page.getBoundingClientRect();
    const tableTop = table.getBoundingClientRect().top - pageRect.top;
    const availableHeight = pageRect.height - tableTop - bottomMargin - theadHeight - rowGap;

    const isFirstContent = Array.from(page.children)
        .find(child => !child.classList.contains('report-ignore')) === pageChild;

    // The first content of a page always keeps at least one row, otherwise moving
    // the table would just leave an empty page behind.
    const rowsInCurrentPage = countRowsThatFit(dataRows, 0, availableHeight, rowGap, isFirstContent ? 1 : 0);

    if (rowsInCurrentPage <= 0) {
        return moveOverflowingElementsToNextPage(pageChild, page, currentWrapper, pagesContainer);
    }

    if (rowsInCurrentPage >= dataRows.length) {
        if (isFirstContent) return currentWrapper;
        return moveOverflowingElementsToNextPage(pageChild, page, currentWrapper, pagesContainer);
    }

    const overflowRows = dataRows.slice(rowsInCurrentPage);
    overflowRows.forEach(row => row.remove());

    const nextWrapper = createContinuationPageWrapper(page, currentWrapper);
    const nextPage = nextWrapper.querySelector('.page');

    const repeatHeader = currentWrapper.getAttribute('data-dynamic-repeat-header') !== 'false';
    const continuationTable = createContinuationTable(table, thead, headerRow, overflowRows, repeatHeader);
    normalizeFirstMovedElement(continuationTable);

    if (container) {
        nextPage.appendChild(createContinuationDraggable(container, table, continuationTable));
        moveFollowingSiblings(container, nextPage, false);
    } else {
        nextPage.appendChild(continuationTable);
        moveFollowingSiblings(table, nextPage);
    }

    insertPageAfter(currentWrapper, nextWrapper, pagesContainer);
    return nextWrapper;
}

/**
 * Moves the overflowing element and every element after it onto a new continuation page.
 * @param {HTMLElement} startElement First element to move.
 * @param {HTMLElement} page The page containing the element.
 * @param {HTMLElement} currentWrapper The `.page-wrapper` of the page.
 * @param {HTMLElement} pagesContainer The `#pagesContainer` element.
 * @returns {HTMLElement} The newly inserted continuation `.page-wrapper`.
 */
function moveOverflowingElementsToNextPage(startElement, page, currentWrapper, pagesContainer) {
    const nextWrapper = createContinuationPageWrapper(page, currentWrapper);
    const nextPage = nextWrapper.querySelector('.page');

    let current = startElement;
    let firstMovedElement = null;

    while (current) {
        const next = current.nextElementSibling;
        nextPage.appendChild(current);

        if (!firstMovedElement) {
            firstMovedElement = current;
        }

        current = next;
    }

    normalizeFirstMovedElement(firstMovedElement);

    insertPageAfter(currentWrapper, nextWrapper, pagesContainer);
    return nextWrapper;
}
