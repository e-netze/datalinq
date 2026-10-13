/*
 * PDF report editing tool: new page
 *
 * Toolbar action asking (modal, see edit-dialog.js) whether to insert the page before or after
 * the page currently in view and for the @PDF.NewPage options. The source is changed by
 * DataLinq Code (editor/pdf-editing/tools/page.js, action 'page.insert').
 */
(function () {
    const icon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"/><path d="M14 3v6h6"/><path d="M12 12v6"/><path d="M9 15h6"/></svg>';

    const DEFAULT_OPTIONS = {
        paperSize: 'A4', landscape: false, usePageTemplate: false, templateId: '',
        dynamic: false, dynamicUseTemplate: false, defaultMarginTop: 10, defaultMarginBottom: 10, repeatHeader: true
    };

    const optionFields = function (current) {
        const o = Object.assign({}, DEFAULT_OPTIONS, current || {});
        const dynamic = values => values.dynamic;

        return [
            { type: 'heading', label: 'Format' },
            { name: 'paperSize', label: 'Paper size', type: 'select', value: o.paperSize, options: ['A4', 'A1', 'A2', 'A3', 'A5', 'A6'] },
            { name: 'landscape', label: 'Landscape', type: 'checkbox', value: o.landscape },

            { type: 'heading', label: 'Page template' },
            { name: 'usePageTemplate', label: 'Use page template', type: 'checkbox', value: o.usePageTemplate },
            {
                name: 'templateId', label: 'Template id', type: 'text', value: o.templateId, placeholder: 'endpoint@query@view?param={@var}',
                pattern: /^[\w\-@.]{0,200}(\?[^"\\\r\n]{0,400})?$/, showIf: v => v.usePageTemplate
            },

            { type: 'heading', label: 'Dynamic table' },
            { name: 'dynamic', label: 'Dynamic', type: 'checkbox', value: o.dynamic },
            { name: 'dynamicUseTemplate', label: 'Use template on overflow pages', type: 'checkbox', value: o.dynamicUseTemplate, showIf: dynamic },
            { name: 'defaultMarginTop', label: 'Margin top', type: 'number', value: o.defaultMarginTop, min: 0, step: 0.5, showIf: dynamic },
            { name: 'defaultMarginBottom', label: 'Margin bottom', type: 'number', value: o.defaultMarginBottom, min: 0, step: 0.5, showIf: dynamic },
            { name: 'repeatHeader', label: 'Repeat table header', type: 'checkbox', value: o.repeatHeader, showIf: dynamic }
        ];
    };

    const run = async function () {
        const page = pdfEditActions.currentPage();
        if (!page) {
            if (document.querySelector('.main') && !document.querySelector('.page')) {
                appendFirstPage();
                return;
            }
            pdfEditActions.notify('No editable page found.');
            return;
        }

        const values = await pdfEditDialog.open({
            title: 'New page',
            submitLabel: 'Insert page',
            steps: [
                {
                    title: 'Position',
                    description: 'Current page: ' + page.number,
                    fields: [
                        {
                            name: 'position', label: 'Insert', type: 'choice', value: 'after',
                            options: [
                                { value: 'before', label: 'Before page ' + page.number },
                                { value: 'after', label: 'After page ' + page.number }
                            ]
                        }
                    ]
                },
                {
                    title: 'Page options',
                    fields: optionFields()
                }
            ]
        });
        if (!values) return;

        const { position, ...options } = values;
        const result = await pdfEditActions.request('page.insert', {
            page: page.sourceIndex,
            position: position,
            options: options
        });

        if (!result.success) {
            pdfEditActions.notify(result.message || 'The page could not be inserted.');
            return;
        }

        // save -> live preview re-runs and shows the new page
        pdfEditActions.saveAndReload(position === 'before' ? page.sourceIndex : page.sourceIndex + 1);
    };

    const deletePage = async function (pageElement) {
        const page = pdfEditActions.pageOf(pageElement);
        if (!page) return;

        const confirmed = await pdfEditDialog.open({
            title: 'Delete page',
            submitLabel: 'Delete page',
            steps: [
                {
                    description: 'Delete page ' + page.number + ' with all its content (NewPage ... EndPage)? This can be undone in the code editor.',
                    fields: []
                }
            ]
        });
        if (!confirmed) return;

        const result = await pdfEditActions.request('page.delete', { page: page.sourceIndex });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The page could not be deleted.');
            return;
        }

        pdfEditActions.saveAndReload(Math.max(0, page.sourceIndex - 1));
    };

    const editPageOptions = async function (pageElement) {
        const page = pdfEditActions.pageOf(pageElement);
        if (!page) return;

        const current = await pdfEditActions.request('page.options.get', { page: page.sourceIndex });
        if (!current.success) {
            pdfEditActions.notify(current.message || 'The page options could not be read.');
            return;
        }

        const values = await pdfEditDialog.open({
            title: 'Page options',
            submitLabel: 'Apply',
            steps: [
                {
                    title: 'Page ' + page.number,
                    fields: optionFields(current.data)
                }
            ]
        });
        if (!values) return;

        const result = await pdfEditActions.request('page.options.set', { page: page.sourceIndex, options: values });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The page options could not be changed.');
            return;
        }

        pdfEditActions.saveAndReload(page.sourceIndex);
    };

    const sourcePageCount = function () {
        let max = -1;
        document.querySelectorAll('.page[data-dl-edit-page]').forEach(p => {
            const value = parseInt(p.getAttribute('data-dl-edit-page'), 10);
            if (Number.isInteger(value) && value > max) max = value;
        });
        return max + 1;
    };

    const movePage = async function (pageElement, direction) {
        const page = pdfEditActions.pageOf(pageElement);
        if (!page) return;

        const result = await pdfEditActions.request('page.move', { page: page.sourceIndex, direction: direction });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The page could not be moved.');
            return;
        }

        pdfEditActions.saveAndReload(direction === 'up' ? page.sourceIndex - 1 : page.sourceIndex + 1);
    };

    const duplicatePage = async function (pageElement) {
        const page = pdfEditActions.pageOf(pageElement);
        if (!page) return;

        const result = await pdfEditActions.request('page.duplicate', { page: page.sourceIndex });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The page could not be duplicated.');
            return;
        }

        pdfEditActions.saveAndReload(page.sourceIndex + 1);
    };

    const connectedPage = element => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected() && pdfEditActions.pageOf(element));

    pdfEditContextMenu.register({
        id: 'page-options',
        label: 'Page options...',
        target: 'page',
        when: connectedPage,
        run: editPageOptions
    });

    pdfEditContextMenu.register({
        id: 'move-page-up',
        label: 'Move page up',
        target: 'page',
        when: element => connectedPage(element) && pdfEditActions.pageOf(element).sourceIndex > 0,
        run: element => movePage(element, 'up')
    });

    pdfEditContextMenu.register({
        id: 'move-page-down',
        label: 'Move page down',
        target: 'page',
        when: element => connectedPage(element) && pdfEditActions.pageOf(element).sourceIndex < sourcePageCount() - 1,
        run: element => movePage(element, 'down')
    });

    pdfEditContextMenu.register({
        id: 'duplicate-page',
        label: 'Duplicate page',
        target: 'page',
        when: connectedPage,
        run: duplicatePage
    });

    const appendFirstPage = async function () {
        const values = await pdfEditDialog.open({
            title: 'New page',
            submitLabel: 'Insert page',
            steps: [
                {
                    title: 'Page options',
                    fields: optionFields()
                }
            ]
        });
        if (!values) return;

        const result = await pdfEditActions.request('page.append', { options: values });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The page could not be inserted.');
            return;
        }

        pdfEditActions.saveAndReload(0);
    };

    pdfEditContextMenu.register({
        id: 'append-page',
        label: 'New page...',
        target: 'report',
        when: () => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected() &&
            document.querySelector('.main') && !document.querySelector('.page')),
        run: appendFirstPage
    });

    pdfEditContextMenu.register({
        id: 'delete-page',
        label: 'Delete page',
        target: 'page',
        when: element => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected() && pdfEditActions.pageOf(element)),
        run: deletePage
    });

    pdfEditActions.register({
        id: 'new-page',
        label: 'New page',
        title: 'Insert a new page before/after the current page',
        icon: icon,
        run: run
    });
})();
