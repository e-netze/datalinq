/**
 * PDF editing tool: paragraph
 *
 * Source template for paragraphs added/edited in the PDF preview (see E.DataLinq.Web pdf-report/add-elements.js).
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    pdfDraggableSource.registerElementTemplate('paragraph', {
        content: function (text) {
            return '<p>' + pdfDraggableSource.escapeText(text) + '</p>';
        },
        textPattern: /<p(?:\s[^>]*)?>((?:[^<@]|@@|<br\s*\/?>)*)<\/p>/
    });
})();
