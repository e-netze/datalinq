/**
 * PDF editing tool: duplicate element
 *
 * Right click on an element -> "Duplicate" inserts a copy of the draggable into the source
 * (slightly moved) and reloads the preview.
 * Source action: 'element.duplicate' (E.DataLinq.Code pdf-editing/tools/duplicate.js)
 */
(function () {
    const OFFSET = 10;
    const bridge = () => window.dataLinqEditBridge;

    const isEditableElement = element =>
        !!(bridge() && bridge().isConnected() &&
            !element.classList.contains('pdf-edit-pending') &&
            Number.isInteger(parseInt(element.getAttribute('data-dl-edit-index'), 10)));

    const locate = element => ({
        index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
        originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
        originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
    });

    let busy = false;

    const duplicateElement = async function (element) {
        if (busy || !isEditableElement(element)) return;
        busy = true;
        try {
            const result = await pdfEditActions.request('element.duplicate', Object.assign(locate(element), { offset: OFFSET }));
            if (!result.success) {
                pdfEditActions.notify(result.message || 'The element could not be duplicated.');
                return;
            }

            const page = pdfEditActions.pageOf(element.closest('.page'));
            pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
        } finally {
            busy = false;
        }
    };

    pdfEditContextMenu.register({
        id: 'duplicate-element',
        label: 'Duplicate',
        order: 40,
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v1"/></svg>',
        when: isEditableElement,
        run: duplicateElement
    });
})();
