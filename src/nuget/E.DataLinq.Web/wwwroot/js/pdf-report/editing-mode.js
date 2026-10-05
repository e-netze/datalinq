/*
 * PDF report - editing mode helpers.
 *
 * Detects whether the report is opened in editing mode and shortens tables to a
 * small preview so draggable elements stay manageable while positioning them.
 */

/**
 * Checks whether editing mode is active (the editing script has been included by the view).
 * @returns {boolean} True when the PDF report editing script is present on the page.
 */
function isPdfReportEditingMode() {
    return !!document.querySelector('script[src*="pdf-report/editing.js"]');
}

// In editing mode only a preview of each table is rendered (header + first rows) so
// tables stay small enough to be dragged around and are not split across pages.
const EDITING_MAX_TABLE_ROWS = 1;
const EDITING_MORE_ROWS_TEXT = 'More data available outside of editing mode';

/**
 * Reduces every report table to EDITING_MAX_TABLE_ROWS data rows and appends a
 * "more rows available" hint row.
 * @param {Document|Element} [root=document] Element whose `.page table` descendants are processed.
 * @param {boolean} [removeRows=true] True to remove surplus rows, false to only hide them temporarily.
 * @returns {Function} Restore callback that undoes hiding (only meaningful when removeRows is false).
 */
function limitTableRowsForEditing(root = document, removeRows = true) {
    const restore = [];

    root.querySelectorAll('.page table').forEach(table => {
        const thead = table.querySelector(':scope > thead');
        const bodyRows = Array.from(table.querySelectorAll(':scope > tbody > tr, :scope > tr'));

        let dataRows = bodyRows;
        if (!thead && bodyRows.length > 0 && bodyRows[0].querySelector(':scope > th')) {
            dataRows = bodyRows.slice(1);
        }

        if (dataRows.length <= EDITING_MAX_TABLE_ROWS) return;

        const columnCount = Math.max(1, ...Array.from(table.rows).map(row =>
            Array.from(row.cells).reduce((sum, cell) => sum + (cell.colSpan || 1), 0)));

        const removedRows = dataRows.slice(EDITING_MAX_TABLE_ROWS);
        const lastKeptRow = dataRows[EDITING_MAX_TABLE_ROWS - 1];

        if (removeRows) {
            removedRows.forEach(row => row.remove());
        } else {
            removedRows.forEach(row => {
                restore.push({ row, display: row.style.display });
                row.style.display = 'none';
            });
        }

        const moreRow = document.createElement('tr');
        moreRow.className = 'pdf-editing-more-rows';
        const cell = document.createElement('td');
        cell.colSpan = columnCount;
        cell.textContent = `${EDITING_MORE_ROWS_TEXT} (${removedRows.length} more rows)`;
        cell.style.textAlign = 'center';
        cell.style.fontStyle = 'italic';
        cell.style.opacity = '0.7';
        moreRow.appendChild(cell);

        lastKeptRow.after(moreRow);
        if (!removeRows) {
            restore.push({ moreRow });
        }
    });

    return () => restore.forEach(entry => {
        if (entry.moreRow) {
            entry.moreRow.remove();
        } else {
            entry.row.style.display = entry.display;
        }
    });
}
