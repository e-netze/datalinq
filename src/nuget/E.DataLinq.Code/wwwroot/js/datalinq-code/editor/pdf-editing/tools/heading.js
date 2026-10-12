/**
 * PDF editing tool: heading (h1 - h6)
 *
 * Source templates for headings added/edited in the PDF preview (see E.DataLinq.Web pdf-report/tools/heading.js).
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    [1, 2, 3, 4, 5, 6].forEach(function (level) {
        var tag = 'h' + level;
        pdfDraggableSource.registerElementTemplate('heading-' + level, {
            content: function (text) {
                return '<' + tag + '>' + pdfDraggableSource.escapeText(text) + '</' + tag + '>';
            },
            textPattern: new RegExp('<' + tag + '(?:\\s[^>]*)?>((?:[^<@]|@@|<br\\s*\\/?>)*)<\\/' + tag + '>', 'i')
        });
    });
})();
