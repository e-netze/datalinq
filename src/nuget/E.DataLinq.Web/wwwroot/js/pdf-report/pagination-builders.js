/*
 * PDF report - pagination builders and measuring helpers.
 *
 * DOM factories for continuation pages/tables/draggables plus small layout
 * measurement helpers used by pagination.js and page-structure.js.
 */

/**
 * Builds an empty continuation page that mirrors the source page's format (orientation,
 * paper size, template, ignored decorations) and re-applies the configured top margin.
 * @param {HTMLElement} sourcePage The `.page` being continued.
 * @param {HTMLElement} sourceWrapper The `.page-wrapper` of the source page.
 * @returns {HTMLElement} The new (not yet inserted) `.page-wrapper`.
 */
function createContinuationPageWrapper(sourcePage, sourceWrapper) {
    const newPageWrapper = document.createElement('div');
    newPageWrapper.className = 'page-wrapper';

    if (sourceWrapper.classList.contains('dynamic')) {
        newPageWrapper.classList.add('dynamic');
    }

    if (sourceWrapper.classList.contains('dynamic-use-template')) {
        newPageWrapper.classList.add('dynamic-use-template');
    }

    const margins = getDynamicMargins(sourceWrapper);
    newPageWrapper.setAttribute('data-dynamic-margin-top', margins.defaultMarginTop.toString());
    newPageWrapper.setAttribute('data-dynamic-margin-bottom', margins.defaultMarginBottom.toString());

    if (sourceWrapper.getAttribute('data-dynamic-repeat-header') === 'false') {
        newPageWrapper.setAttribute('data-dynamic-repeat-header', 'false');
    }

    const newPage = document.createElement('div');
    newPage.className = 'page';

    if (sourcePage.classList.contains('horizontal')) {
        newPage.classList.add('horizontal');
    }

    const paperSizeClass = [...sourcePage.classList].find(c => /^size-a[1-6]$/i.test(c));
    if (paperSizeClass) {
        newPage.classList.add(paperSizeClass);
    }

    const template = sourcePage.getAttribute('datalinq-pdfreport-template');
    if (template && sourceWrapper.classList.contains('dynamic-use-template')) {
        newPage.setAttribute('datalinq-pdfreport-template', template);
    }

    Array.from(sourcePage.children)
        .filter(child => child.classList.contains('report-ignore') && !child.classList.contains('dynamic-top-margin-spacer'))
        .forEach(child => newPage.appendChild(child.cloneNode(true)));

    if (margins.defaultMarginTop > 0) {
        const topSpacer = document.createElement('div');
        topSpacer.className = 'report-ignore dynamic-top-margin-spacer';
        topSpacer.style.height = `${margins.defaultMarginTop}px`;
        newPage.appendChild(topSpacer);
    }

    newPageWrapper.appendChild(newPage);
    return newPageWrapper;
}

/**
 * Clones the original table (minus its id) so the moved rows keep the same styling,
 * repeating the header (thead or a th header row) on the continuation page.
 * @param {HTMLTableElement} originalTable Table being split.
 * @param {HTMLElement|null} thead The table's thead, if any.
 * @param {HTMLTableRowElement|null} headerRow A th header row used when there is no thead.
 * @param {HTMLTableRowElement[]} rows Rows to move into the new table.
 * @param {boolean} [repeatHeader=true] False to omit the header on the continuation table.
 * @returns {HTMLTableElement} The continuation table.
 */
function createContinuationTable(originalTable, thead, headerRow, rows, repeatHeader = true) {
    const newTable = document.createElement('table');

    Array.from(originalTable.attributes).forEach(attr => {
        if (attr.name !== 'id') {
            newTable.setAttribute(attr.name, attr.value);
        }
    });

    if (thead && repeatHeader) {
        newTable.appendChild(thead.cloneNode(true));
    }

    const newTbody = document.createElement('tbody');

    if (headerRow && !thead && repeatHeader) {
        newTbody.appendChild(headerRow.cloneNode(true));
    }

    rows.forEach(row => {
        row.classList.remove('hidden');
        newTbody.appendChild(row);
    });

    newTable.appendChild(newTbody);
    return newTable;
}

/**
 * Builds a copy of the draggable (and the wrappers between it and the table) holding
 * only the continuation table. Keeps the horizontal offset, resets the vertical one.
 * @param {HTMLElement} draggable The original draggable (.element).
 * @param {HTMLTableElement} table The original table inside the draggable.
 * @param {HTMLTableElement} continuationTable The table to place into the copy.
 * @returns {HTMLElement} The new draggable.
 */
function createContinuationDraggable(draggable, table, continuationTable) {
    let inner = continuationTable;
    let ancestor = table.parentElement;

    while (ancestor && ancestor !== draggable) {
        const shell = ancestor.cloneNode(false);
        shell.removeAttribute('id');
        shell.appendChild(inner);
        inner = shell;
        ancestor = ancestor.parentElement;
    }

    const copy = draggable.cloneNode(false);
    copy.removeAttribute('id');

    const x = parseFloat(draggable.getAttribute('data-x')) || new DOMMatrix(getComputedStyle(draggable).transform).m41 || 0;
    copy.setAttribute('data-x', x.toString());
    copy.setAttribute('data-y', '0');
    copy.style.transform = `translate(${x}px, 0px)`;
    copy.style.marginTop = '0px';

    copy.appendChild(inner);
    return copy;
}

/**
 * Moves all siblings after referenceElement to the end of targetPage.
 * @param {HTMLElement} referenceElement Element whose following siblings are moved.
 * @param {HTMLElement} targetPage Destination page.
 * @param {boolean} [normalizeFirst=true] Reset top margin/padding of the first moved element.
 * @returns {void}
 */
function moveFollowingSiblings(referenceElement, targetPage, normalizeFirst = true) {
    let current = referenceElement.nextElementSibling;
    let firstMovedElement = null;

    while (current) {
        const next = current.nextElementSibling;
        targetPage.appendChild(current);

        if (!firstMovedElement) {
            firstMovedElement = current;
        }

        current = next;
    }

    if (normalizeFirst) {
        normalizeFirstMovedElement(firstMovedElement);
    }
}

/**
 * Inserts newPageWrapper directly after currentWrapper.
 * @param {HTMLElement} currentWrapper Existing `.page-wrapper`.
 * @param {HTMLElement} newPageWrapper Wrapper to insert.
 * @param {HTMLElement} pagesContainer The `#pagesContainer` element.
 * @returns {void}
 */
function insertPageAfter(currentWrapper, newPageWrapper, pagesContainer) {
    const nextElement = currentWrapper.nextElementSibling;
    if (nextElement) {
        pagesContainer.insertBefore(newPageWrapper, nextElement);
    } else {
        pagesContainer.appendChild(newPageWrapper);
    }
}

/**
 * Counts how many consecutive rows fit within availableHeight, never returning fewer
 * than minRows so pagination always makes forward progress.
 * @param {HTMLTableRowElement[]} rows All candidate rows.
 * @param {number} startIndex Index of the first row to check.
 * @param {number} availableHeight Available height in px.
 * @param {number} rowGap Vertical gap between rows in px.
 * @param {number} [minRows=1] Minimum result.
 * @returns {number} Number of rows that fit.
 */
function countRowsThatFit(rows, startIndex, availableHeight, rowGap, minRows = 1) {
    let usedHeight = 0;
    let count = 0;

    for (let i = startIndex; i < rows.length; i++) {
        const rowHeight = getRowOuterHeight(rows[i]) + rowGap;

        if (usedHeight + rowHeight > availableHeight) {
            break;
        }

        usedHeight += rowHeight;
        count++;
    }

    return Math.max(count, minRows);
}

/**
 * Returns the rendered height of a row including its vertical margins.
 * @param {HTMLElement} row The row.
 * @returns {number} Height in px.
 */
function getRowOuterHeight(row) {
    const styles = window.getComputedStyle(row);
    return row.getBoundingClientRect().height
        + (parseFloat(styles.marginTop) || 0)
        + (parseFloat(styles.marginBottom) || 0);
}

/**
 * Extracts the vertical component of a CSS `border-spacing` value (used as the row gap).
 * @param {string} borderSpacing Computed `border-spacing` value, e.g. "2px 4px".
 * @returns {number} Vertical spacing in px (0 if not set).
 */
function getRowGap(borderSpacing) {
    if (!borderSpacing) return 0;

    const spacing = borderSpacing.toString().trim().split(/\s+/);
    if (spacing.length === 0) return 0;

    const verticalSpacing = spacing.length > 1 ? spacing[1] : spacing[0];
    return parseFloat(verticalSpacing) || 0;
}

/**
 * Reads the dynamic page margins from `data-dynamic-margin-top/bottom`.
 * @param {HTMLElement} pageWrapper The `.page-wrapper`.
 * @returns {{defaultMarginTop: number, defaultMarginBottom: number}} Margins in px (PDF_DEFAULT_MARGIN if unset).
 */
function getDynamicMargins(pageWrapper) {
    const defaultMarginTop = parseFloat(pageWrapper.getAttribute('data-dynamic-margin-top'));
    const defaultMarginBottom = parseFloat(pageWrapper.getAttribute('data-dynamic-margin-bottom'));

    return {
        defaultMarginTop: Number.isFinite(defaultMarginTop) ? defaultMarginTop : PDF_DEFAULT_MARGIN,
        defaultMarginBottom: Number.isFinite(defaultMarginBottom) ? defaultMarginBottom : PDF_DEFAULT_MARGIN,
    };
}

/**
 * Removes the top margin/padding of the first element on a continuation page.
 * @param {HTMLElement|null} element The element (ignored when null).
 * @returns {void}
 */
function normalizeFirstMovedElement(element) {
    if (!element) return;

    element.style.marginTop = '0';
    element.style.paddingTop = '0';
}
