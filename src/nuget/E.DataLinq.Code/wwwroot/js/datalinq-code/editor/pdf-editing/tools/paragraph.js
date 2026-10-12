/**
 * PDF editing tool: paragraph
 *
 * Source template for paragraphs added/edited in the PDF preview (see E.DataLinq.Web pdf-report/add-elements.js).
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/rich-text.js
 */
(function () {
    pdfDraggableSource.registerElementTemplate('paragraph', {
        content: function (text) {
            return '<p>' + pdfDraggableSource.escapeText(text) + '</p>';
        },
        textPattern: /<p(?:\s[^>]*)?>((?:[^<@]|@@|<br\s*\/?>)*)<\/p>/,
        // formatted text (see pdf-editing/rich-text.js): 1 = tag, 2 = attributes, 3 = content
        formattedPattern: new RegExp('<(p)(\\s[^<>@]*)?>(' + pdfRichText.innerPatternSource + ')<\\/p>')
    });
})();
