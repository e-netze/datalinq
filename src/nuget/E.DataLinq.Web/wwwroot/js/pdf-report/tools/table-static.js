/*
 * PDF report editing tool: static table
 *
 * Toolbar button "Table": adds a static table (1 column, 1 header row, 1 body row).
 * Static tables have the class pdf-table-static and are never shortened in editing mode (editing-mode.js).
 * Editing (see table-editing.js): double click a cell = rich text, drag a column border = column widths,
 * context menu "Table properties..." = columns, rows, row height, width.
 * The source template and actions live in DataLinq Code editor/pdf-editing/tools/table-static.js
 * (same type id, actions table.static.*).
 */
(function () {
    const CLASS_NAME = 'pdf-table-static';
    const DEFAULT_ROW_HEIGHT = 24;
    const DEFAULT_WIDTH = 300;

    pdfEditElementTypes.register({
        type: 'table-static',
        label: 'Table',
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<rect x="3" y="4" width="18" height="16" rx="1"/><path d="M3 10h18"/><path d="M3 15h18"/><path d="M12 10v10"/></svg>',
        defaultText: 'Header',
        // same structure as the source template (one header cell, one body cell)
        create: function (text) {
            return pdfEditTable.createTable({
                className: CLASS_NAME,
                width: DEFAULT_WIDTH,
                rowHeight: DEFAULT_ROW_HEIGHT,
                border: { width: 1, style: 'solid', color: '#000000' },
                head: [[text]],
                body: [['Cell']]
            });
        },
        // cells are edited by table-editing.js
        textTarget: () => null
    });

    pdfEditTable.register({
        selector: 'table.' + CLASS_NAME,
        actionPrefix: 'table.static',
        label: 'Table'
    });
})();
