/*
 * PDF report editing tool: report options
 *
 * Context menu on the report background (.main outside of pages) opening a modal (see edit-dialog.js)
 * with the @PDF.BeginReport options (page numbers, date/time, quality, download button, file name).
 * The source is changed by DataLinq Code (editor/pdf-editing/tools/report.js,
 * actions 'report.options.get' / 'report.options.set').
 */
(function () {
    const POSITIONS = [
        { value: '0', label: 'Left' },
        { value: '1', label: 'Center' },
        { value: '2', label: 'Right' }
    ];

    const editReportOptions = async function () {
        const current = await pdfEditActions.request('report.options.get', {});
        if (!current.success || !current.data) {
            pdfEditActions.notify(current.message || 'The report options could not be read.');
            return;
        }
        const o = current.data;
        const create = o.exists === false;
        const pageNumbers = v => v.usePageNumbers;
        const dateTime = v => v.usePageDateTime;

        const values = await pdfEditDialog.open({
            title: create ? 'New report' : 'Report options',
            submitLabel: create ? 'Create report' : 'Apply',
            steps: [
                {
                    title: 'Page numbers',
                    fields: [
                        { name: 'usePageNumbers', label: 'Show page numbers', type: 'checkbox', value: o.usePageNumbers === true },
                        { name: 'pageNumberPosition', label: 'Position', type: 'select', value: String(o.pageNumberPosition || 0), options: POSITIONS, showIf: pageNumbers },
                        {
                            name: 'pageNumberType', label: 'Format', type: 'select', value: String(o.pageNumberType || 0), showIf: pageNumbers,
                            options: [{ value: '0', label: 'n/total' }, { value: '1', label: 'n' }]
                        },
                        { name: 'pageNumberSkipPages', label: 'Skip first pages', type: 'number', value: o.pageNumberSkipPages || 0, min: 0, step: 1, showIf: pageNumbers },
                        { name: 'pageNumberOffsetX', label: 'Offset horizontal (px, - left / + right)', type: 'number', value: o.pageNumberOffsetX || 0, step: 1, showIf: pageNumbers },
                        { name: 'pageNumberOffsetY', label: 'Offset vertical (px, - up / + down)', type: 'number', value: o.pageNumberOffsetY || 0, step: 1, showIf: pageNumbers }
                    ]
                },
                {
                    title: 'Date/time',
                    fields: [
                        { name: 'usePageDateTime', label: 'Show date/time', type: 'checkbox', value: o.usePageDateTime === true },
                        { name: 'dateTimePosition', label: 'Position', type: 'select', value: String(o.dateTimePosition || 0), options: POSITIONS, showIf: dateTime },
                        {
                            name: 'dateTimeFormat', label: 'Format', type: 'text', value: o.dateTimeFormat || 'HH:mm dd.MM.yyyy',
                            placeholder: 'HH:mm dd.MM.yyyy', pattern: /^[^"\\{}\r\n]{1,100}$/, showIf: dateTime
                        },
                        { name: 'dateTimeSkipPages', label: 'Skip first pages', type: 'number', value: o.dateTimeSkipPages || 0, min: 0, step: 1, showIf: dateTime },
                        { name: 'dateTimeOffsetX', label: 'Offset horizontal (px, - left / + right)', type: 'number', value: o.dateTimeOffsetX || 0, step: 1, showIf: dateTime },
                        { name: 'dateTimeOffsetY', label: 'Offset vertical (px, - up / + down)', type: 'number', value: o.dateTimeOffsetY || 0, step: 1, showIf: dateTime }
                    ]
                },
                {
                    title: 'Export',
                    fields: [
                        { name: 'quality', label: 'Quality', type: 'select', value: o.quality || 'High', options: ['Best', 'High', 'Medium', 'Low', 'Preview'] },
                        { name: 'downloadButton', label: 'Show download button', type: 'checkbox', value: o.downloadButton === true },
                        { name: 'fileName', label: 'File name (without extension)', type: 'text', value: o.fileName || 'dataLinqPdfReport', pattern: /^[\w\-. ]{1,200}$/ }
                    ]
                }
            ]
        });
        if (!values) return;

        const options = Object.assign({}, values, {
            pageNumberPosition: parseInt(values.pageNumberPosition, 10) || 0,
            pageNumberType: parseInt(values.pageNumberType, 10) || 0,
            pageNumberSkipPages: Math.max(0, Math.round(values.pageNumberSkipPages) || 0),
            dateTimePosition: parseInt(values.dateTimePosition, 10) || 0,
            dateTimeSkipPages: Math.max(0, Math.round(values.dateTimeSkipPages) || 0),
            pageNumberOffsetX: Math.round(values.pageNumberOffsetX) || 0,
            pageNumberOffsetY: Math.round(values.pageNumberOffsetY) || 0,
            dateTimeOffsetX: Math.round(values.dateTimeOffsetX) || 0,
            dateTimeOffsetY: Math.round(values.dateTimeOffsetY) || 0
        });

        const result = await pdfEditActions.request(create ? 'report.create' : 'report.options.set', { options: options });
        if (!result.success) {
            pdfEditActions.notify(result.message || (create ? 'The report could not be created.' : 'The report options could not be changed.'));
            return;
        }

        const page = pdfEditActions.currentPage();
        pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
    };

    pdfEditContextMenu.register({
        id: 'report-options',
        label: 'Report options...',
        target: 'report',
        when: () => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected() && document.querySelector('.main')),
        run: editReportOptions
    });

    pdfEditContextMenu.register({
        id: 'new-report',
        label: 'New report...',
        target: 'report',
        when: () => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected() && !document.querySelector('.main')),
        run: editReportOptions
    });
})();
