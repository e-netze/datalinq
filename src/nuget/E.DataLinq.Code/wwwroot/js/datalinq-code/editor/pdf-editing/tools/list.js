/**
 * PDF editing tool: list (list-unordered / list-ordered)
 *
 * Source templates for lists added/edited in the PDF preview (see E.DataLinq.Web pdf-report/tools/list.js).
 * Edits always send formatted html (args.listTag, see editor-frame.js): the whole <ul|ol>..</ul|ol>
 * is replaced, so switching ul <-> ol is possible.
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/rich-text.js
 */
(function () {
    // greedy: nested lists are included, the match ends at the last closing tag before Razor code (@PDF.EndDraggable)
    var formattedPattern = new RegExp('<(ul|ol)(\\s[^<>@]*)?>(' + pdfRichText.listInnerPatternSource + ')<\\/\\1>', 'i');

    [['list-unordered', 'ul'], ['list-ordered', 'ol']].forEach(function (d) {
        var tag = d[1];
        pdfDraggableSource.registerElementTemplate(d[0], {
            content: function (text) {
                var items = String(text || '').split(/\r?\n/).map(function (line) {
                    return '<li>' + pdfDraggableSource.escapeText(line) + '</li>';
                });
                return '<' + tag + '>' + items.join('') + '</' + tag + '>';
            },
            // lists are only edited as formatted text (never matches plain text)
            textPattern: /(?!)()/,
            // 1 = tag, 2 = attributes, 3 = content
            formattedPattern: formattedPattern
        });
    });
})();
