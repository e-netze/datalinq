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
        // static or dynamic table (dynamic: see table-dynamic.js, only on dynamic pages)
        choose: async function () {
            const dynamicAvailable = !!document.querySelector('.page-wrapper.dynamic') && !!window.pdfEditDynamicTable;
            const values = await pdfEditDialog.open({
                title: 'Add table',
                submitLabel: 'Next',
                steps: [{
                    title: 'Table type',
                    fields: [{
                        name: 'kind', label: 'Type', type: 'choice', value: 'static',
                        options: [
                            { value: 'static', label: 'Static', description: 'Fixed rows and cells, edited directly in the preview.' },
                            {
                                value: 'dynamic', label: 'Dynamic',
                                description: dynamicAvailable
                                    ? 'One row per record of a data source (@foreach).'
                                    : 'Only available on dynamic pages (dynamic table splitting).'
                            }
                        ],
                        validate: value => value === 'dynamic' && !dynamicAvailable
                            ? 'Dynamic tables require a dynamic page (page option "Dynamic").'
                            : null
                    }]
                }]
            });
            if (!values) return null;
            if (values.kind !== 'dynamic') return 'Header';
            const config = await pdfEditDynamicTable.configure(null);
            return config ? { type: 'table-dynamic', text: JSON.stringify(config) } : null;
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
