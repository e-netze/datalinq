/*
 * PDF report editing tool: heading (h1 - h6)
 *
 * Registers one element type per level (heading-1 .. heading-6), grouped into one toolbar
 * button with a level selector. The source templates live in
 * DataLinq Code editor/pdf-editing/tools/heading.js (same type ids).
 */
(function () {
    const icon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M6 4v16"/><path d="M18 4v16"/><path d="M6 12h12"/></svg>';

    for (let level = 1; level <= 6; level++) {
        const tag = 'H' + level;

        pdfEditElementTypes.register({
            type: 'heading-' + level,
            group: 'heading',
            groupLabel: 'Heading',
            label: 'Heading ' + level,
            variantLabel: 'H' + level,
            icon: icon,
            defaultText: 'New heading',
            // rich text toolbar while editing (see text-format.js)
            formatting: { inline: true, block: true },
            // item of the level picker: "Heading 1" rendered at (scaled) heading size
            preview: function () {
                const span = document.createElement('span');
                span.textContent = 'Heading ' + level;
                span.style.fontWeight = '600';
                span.style.fontSize = [20, 17, 15, 13, 12, 11][level - 1] + 'px';
                return span;
            },
            create: function (text) {
                const h = document.createElement(tag);
                h.textContent = text;
                return h;
            },
            textTarget: function (element) {
                const children = element.children;
                return children.length === 1 && children[0].tagName === tag &&
                    pdfEditTextFormat.isFormattedTextNode(children[0])
                    ? children[0]
                    : null;
            }
        });
    }
})();
