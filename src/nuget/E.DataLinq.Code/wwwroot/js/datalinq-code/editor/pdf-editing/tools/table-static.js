/**
 * PDF editing tool: static table (table-static)
 *
 * Source template and source actions of static tables added/edited in the PDF preview
 * (see E.DataLinq.Web pdf-report/tools/table-static.js and pdf-report/table-editing.js).
 * Static tables have the class pdf-table-static: they are never shortened in editing mode.
 * Actions: table.static.get | .set | .widths | .cell (see pdf-editing/table-source.js)
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/rich-text.js, pdf-editing/table-source.js
 */
(function () {
    var CLASS_NAME = 'pdf-table-static';
    var DEFAULT_ROW_HEIGHT = 24;
    var DEFAULT_WIDTH = 300;

    pdfDraggableSource.registerElementTemplate('table-static', {
        // default: 1 column, 1 header row, 1 body row (one line, re-formatted on the first table edit)
        content: function (text) {
            var model = pdfTableSource.create({
                attributes: ' class="' + CLASS_NAME + '"',
                columns: 1,
                headRows: 1,
                bodyRows: 1,
                rowHeight: DEFAULT_ROW_HEIGHT,
                width: DEFAULT_WIDTH,
                border: { width: 1, style: 'solid', color: '#000000' },
                head: [pdfDraggableSource.escapeText(text || 'Header')],
                body: ['Cell']
            });
            return pdfTableSource.serialize(model, '', '');
        },
        // cells are edited with the table.static.cell action (never matches plain text)
        textPattern: /(?!)()/
    });

    pdfTableSource.registerActions('table.static', CLASS_NAME);
})();
