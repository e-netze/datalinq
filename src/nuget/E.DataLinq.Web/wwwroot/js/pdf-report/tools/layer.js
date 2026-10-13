/**
 * PDF editing tool: element layer
 *
 * Right click on an element -> "Layer..." sets the layer of the draggable
 * (@PDF.NewDraggable(x, y, layer)). Higher layers are displayed above lower layers.
 * Source actions: 'element.layer.get' / 'element.layer.set' (E.DataLinq.Code pdf-editing/tools/layer.js)
 */
(function () {
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

    const editLayer = async function (element) {
        if (busy || !isEditableElement(element)) return;
        busy = true;
        try {
            const current = await pdfEditActions.request('element.layer.get', locate(element));
            if (!current.success || !current.data) {
                pdfEditActions.notify(current.message || 'The layer could not be read.');
                return;
            }

            const values = await pdfEditDialog.open({
                title: 'Element layer',
                submitLabel: 'Apply',
                steps: [
                    {
                        title: 'Layer',
                        fields: [
                            { name: 'layer', label: 'Layer (higher = in front)', type: 'number', value: current.data.layer || 0, step: 1, min: -1000000, max: 1000000 }
                        ]
                    }
                ]
            });
            if (!values || !element.isConnected) return;

            const layer = Math.round(Number(values.layer));
            if (!Number.isFinite(layer)) {
                pdfEditActions.notify('Invalid layer (integer expected).');
                return;
            }

            const result = await pdfEditActions.request('element.layer.set', Object.assign(locate(element), { layer: layer }));
            if (!result.success) {
                pdfEditActions.notify(result.message || 'The layer could not be changed.');
                return;
            }

            const page = pdfEditActions.pageOf(element.closest('.page'));
            pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
        } finally {
            busy = false;
        }
    };

    pdfEditContextMenu.register({
        id: 'element-layer',
        label: 'Layer',
        order: 50,
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<path d="M12 3 2 8l10 5 10-5Z"/><path d="m2 13 10 5 10-5"/><path d="m2 18 10 5 10-5"/></svg>',
        when: isEditableElement,
        run: editLayer
    });
})();
