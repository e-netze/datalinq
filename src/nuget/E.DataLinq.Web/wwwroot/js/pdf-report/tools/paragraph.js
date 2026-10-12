/*
 * PDF report editing tool: paragraph
 *
 * Registered element type for add-elements.js; the source template lives in
 * DataLinq Code editor/pdf-editing/tools/paragraph.js (same type id).
 */
(function () {
    pdfEditElementTypes.register({
        type: 'paragraph',
        label: 'Paragraph',
        // pilcrow (paragraph mark)
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<path d="M13 4v16"/><path d="M17 4v16"/><path d="M19 4H9.5a4.5 4.5 0 0 0 0 9H13"/></svg>',
        defaultText: 'New paragraph',
        create: function (text) {
            const p = document.createElement('p');
            p.textContent = text;
            return p;
        },
        textTarget: function (element) {
            const children = element.children;
            return children.length === 1 && children[0].tagName === 'P' &&
                Array.from(children[0].children).every(c => c.tagName === 'BR')
                ? children[0]
                : null;
        }
    });
})();
