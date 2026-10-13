/*
 * PDF report editing tool: list (unordered <ul> / ordered <ol>)
 *
 * Registers list-unordered and list-ordered, grouped into one toolbar button with a type selector.
 * Editing (see add-elements.js, text-format.js): Enter = new item, Enter on an empty last item = finish,
 * Shift+Enter = line break, Backspace on an empty item = remove, Tab/Shift+Tab = indent/outdent
 * (max. 3 levels), toolbar: ul/ol, add/remove item, indent/outdent, marker style and color.
 * The source templates live in DataLinq Code editor/pdf-editing/tools/list.js (same type ids).
 */
(function () {
    const icon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M9 6h11"/><path d="M9 12h11"/><path d="M9 18h11"/><circle cx="4" cy="6" r="1"/><circle cx="4" cy="12" r="1"/><circle cx="4" cy="18" r="1"/></svg>';

    [
        { type: 'list-unordered', tag: 'UL', label: 'Bulleted list' },
        { type: 'list-ordered', tag: 'OL', label: 'Numbered list' }
    ].forEach(d => {
        pdfEditElementTypes.register({
            type: d.type,
            group: 'list',
            groupLabel: 'List',
            label: d.label,
            variantLabel: d.label,
            icon: icon,
            defaultText: 'New item',
            // rich text toolbar incl. list commands while editing (see text-format.js)
            formatting: { inline: true, block: true, list: true },
            // item of the type picker: small sample list
            preview: function () {
                const list = document.createElement(d.tag);
                list.style.margin = '0';
                list.style.paddingLeft = '18px';
                list.style.textAlign = 'left';
                ['Item', 'Item'].forEach(t => {
                    const li = document.createElement('li');
                    li.textContent = t;
                    list.appendChild(li);
                });
                return list;
            },
            // one item per line
            create: function (text) {
                const list = document.createElement(d.tag);
                String(text || '').split('\n').forEach(line => {
                    const li = document.createElement('li');
                    li.textContent = line;
                    list.appendChild(li);
                });
                return list;
            },
            textTarget: function (element) {
                const children = element.children;
                return children.length === 1 && children[0].tagName === d.tag &&
                    pdfEditTextFormat.isFormattedTextNode(children[0])
                    ? children[0]
                    : null;
            }
        });
    });
})();
