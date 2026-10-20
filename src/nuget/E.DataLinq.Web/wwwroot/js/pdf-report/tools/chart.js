/*
 * PDF report editing tool: chart (own toolbar button)
 *
 * A chart always combines a statistics helper (DLH.StatisticsGroupBy | StatisticsGroupByDerived |
 * StatisticsGroupByTime | StatisticsTime -> JavaScript variable) with DLH.Chart (renders the variable).
 * Data source: Model.Records or a query variable added with the data tools (same as dynamic tables).
 * Config (sent as element text, stored as data-dl-chart on the chart wrapper):
 *   { variable, source, statistic, field, categoryField, valueField, datetimeField, statType, orderField,
 *     secondsInterval, chartType, label, colors: ['r,g,b'], locale, width: { value, unit }, height: { value, unit } }
 * Context menu "Chart settings..." reopens the modal (actions chart.get | .set).
 * The source template and actions live in DataLinq Code editor/pdf-editing/tools/chart.js.
 */
window.pdfEditChart = (function () {
    const CLASS_NAME = 'pdf-chart';
    const LENGTH_UNITS = ['px', 'mm'];
    const COLOR = /^\d{1,3},\d{1,3},\d{1,3}$/;

    const icon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M3 3v18h18"/><rect x="7" y="12" width="3" height="6"/><rect x="12" y="8" width="3" height="10"/><rect x="17" y="5" width="3" height="13"/></svg>';

    const STATISTICS = [
        { value: 'groupBy', label: 'Count per group', description: 'number of records per field value' },
        { value: 'groupByDerived', label: 'Value per group', description: 'sum/mean/min/max/count of a value field per category' },
        { value: 'groupByTime', label: 'Count per time interval', description: 'number of records per time interval' },
        { value: 'time', label: 'Time series', description: 'values over time' }
    ];
    const STAT_TYPES = ['Sum', 'Mean', 'Min', 'Max', 'Count'];
    const ORDER_FIELDS = [
        { value: 'Non', label: 'None' },
        { value: 'NameAsc', label: 'Name ascending' },
        { value: 'NameDesc', label: 'Name descending' },
        { value: 'ValueAsc', label: 'Value ascending' },
        { value: 'ValueDesc', label: 'Value descending' }
    ];
    const CHART_TYPES = ['Bar', 'Line', 'Pie', 'Doughnut', 'Scatter'];
    const LOCALES = [{ value: 'None', label: 'None' }, { value: 'DE', label: 'German (1.234,5)' }, { value: 'US', label: 'US (1,234.5)' }];

    const connected = () => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected());

    const elementArgs = element => ({
        index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
        originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
        originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
    });

    const loadSources = async function () {
        const sources = ['Model.Records'];
        if (!connected() || !window.pdfEditActions) return sources;
        const result = await pdfEditActions.request('data.list', {});
        const blocks = result.success && result.data && Array.isArray(result.data.blocks) ? result.data.blocks : [];
        blocks.filter(b => b.type === 'query' && b.name).forEach(b => sources.push(b.name));
        return sources;
    };

    const newVariable = () => 'dlChart_' + Math.random().toString(36).substring(2, 10);

    const is = (...statistics) => v => statistics.indexOf(v.statistic || 'groupBy') >= 0;

    /**
     * Opens the settings modal.
     * @param {object|null} existing current config (edit) or null (new chart)
     * @returns {Promise<object|null>} config or null if cancelled
     */
    const configure = async function (existing) {
        const c = existing || {};
        const sources = await loadSources();
        if (c.source && sources.indexOf(c.source) < 0) sources.push(c.source);

        const values = await pdfEditDialog.open({
            title: existing ? 'Chart settings' : 'Add chart',
            submitLabel: existing ? 'Apply' : 'Place',
            steps: [
                {
                    title: 'Data source',
                    description: 'Records used for the statistics. Add further sources with "Add query data" in the data tools.',
                    fields: [
                        { name: 'source', label: 'Records', type: 'select', value: c.source || sources[0], options: sources }
                    ]
                },
                {
                    title: 'Statistics',
                    fields: [
                        { name: 'statistic', label: 'Function', type: 'choice', value: c.statistic || 'groupBy', options: STATISTICS }
                    ]
                },
                {
                    title: 'Fields',
                    description: 'Field names of the records.',
                    fields: [
                        { name: 'field', label: 'Group by field', type: 'text', value: c.field || '', required: true, showIf: is('groupBy') },
                        { name: 'datetimeField', label: 'Date/time field', type: 'text', value: c.datetimeField || '', required: true, showIf: is('groupByTime', 'time') },
                        { name: 'categoryField', label: 'Category field', type: 'text', value: c.categoryField || '', required: true, showIf: is('groupByDerived') },
                        { name: 'categoryFieldOptional', label: 'Category field (optional)', type: 'text', value: c.categoryField || '', showIf: is('groupByTime', 'time') },
                        { name: 'valueField', label: 'Value field', type: 'text', value: c.valueField || '', required: true, showIf: is('groupByDerived', 'time') },
                        { name: 'statType', label: 'Aggregation', type: 'select', value: c.statType || 'Sum', options: STAT_TYPES, showIf: is('groupByDerived') },
                        { name: 'secondsInterval', label: 'Interval in seconds (0 = auto)', type: 'number', value: c.secondsInterval || 0, min: 0, step: 1, showIf: is('groupByTime') },
                        { name: 'orderField', label: 'Order', type: 'select', value: c.orderField || 'Non', options: ORDER_FIELDS, showIf: is('groupBy', 'groupByDerived', 'groupByTime') }
                    ]
                },
                {
                    title: 'Chart',
                    fields: [
                        { name: 'chartType', label: 'Chart type', type: 'select', value: c.chartType || 'Bar', options: CHART_TYPES },
                        { name: 'label', label: 'Label', type: 'text', value: c.label || '' },
                        {
                            name: 'colors', label: 'Colors (R,G,B per line)', type: 'textarea', rows: 3,
                            value: (c.colors || []).join('\n'), placeholder: '0,155,20\n160,0,25',
                            validate: value => splitColors(value).some(x => !COLOR.test(x)) ? 'Colors must be R,G,B values, e.g. 0,155,20.' : null
                        },
                        { name: 'locale', label: 'Number format', type: 'select', value: c.locale || 'None', options: LOCALES }
                    ]
                },
                {
                    title: 'Size',
                    fields: [
                        { name: 'width', label: 'Width (0 = auto)', type: 'number', value: c.width ? c.width.value : 400, min: 0, max: 3000, step: 'any' },
                        { name: 'widthUnit', label: 'Width unit', type: 'select', value: c.width ? c.width.unit : 'px', options: LENGTH_UNITS },
                        { name: 'height', label: 'Height (0 = auto)', type: 'number', value: c.height ? c.height.value : 250, min: 0, max: 3000, step: 'any' },
                        { name: 'heightUnit', label: 'Height unit', type: 'select', value: c.height ? c.height.unit : 'px', options: LENGTH_UNITS }
                    ]
                }
            ]
        });
        if (!values) return null;

        const statistic = values.statistic || 'groupBy';
        const length = (v, unit) => ({ value: Math.round((Number(v) || 0) * 10) / 10, unit: unit });
        return {
            variable: c.variable || newVariable(),
            source: values.source,
            statistic: statistic,
            field: statistic === 'groupBy' ? (values.field || '').trim() : '',
            categoryField: ((statistic === 'groupByDerived' ? values.categoryField : statistic === 'groupBy' ? '' : values.categoryFieldOptional) || '').trim(),
            valueField: statistic === 'groupByDerived' || statistic === 'time' ? (values.valueField || '').trim() : '',
            datetimeField: statistic === 'groupByTime' || statistic === 'time' ? (values.datetimeField || '').trim() : '',
            statType: values.statType,
            orderField: values.orderField,
            secondsInterval: Math.max(0, Math.round(Number(values.secondsInterval) || 0)),
            chartType: values.chartType,
            label: values.label || '',
            colors: splitColors(values.colors),
            locale: values.locale,
            width: length(values.width, values.widthUnit),
            height: length(values.height, values.heightUnit)
        };
    };

    const splitColors = text => String(text || '').split(/[\r\n|;]+/).map(x => x.replace(/\s/g, '')).filter(x => x !== '');

    pdfEditElementTypes.register({
        type: 'chart',
        label: 'Chart',
        icon: icon,
        saveAfterAdd: true,     // render the real chart
        prompt: function () {
            return configure(null).then(config => config ? JSON.stringify(config) : null);
        },
        // preview until the source is saved
        create: function () {
            const box = document.createElement('div');
            box.className = 'pdf-edit-image-placeholder';
            box.innerHTML = icon + '<span></span>';
            box.querySelector('span').textContent = 'Chart';
            return box;
        },
        textTarget: () => null
    });

    const editSettings = async function (element) {
        const args = elementArgs(element);
        const current = await pdfEditActions.request('chart.get', args);
        if (!current.success || !current.data) {
            pdfEditActions.notify(current.message || 'The chart could not be read.');
            return;
        }
        const config = await configure(current.data.config || current.data);
        if (!config || !element.isConnected) return;

        const result = await pdfEditActions.request('chart.set', Object.assign(args, { config: config }));
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The chart could not be changed.');
            return;
        }
        const page = pdfEditActions.pageOf(element.closest('.page'));
        pdfEditActions.saveAndReload(page ? page.sourceIndex : undefined);
    };

    pdfEditContextMenu.register({
        id: 'chart-settings',
        label: 'Chart settings',
        order: 10,
        icon: icon,
        when: element => connected() && !!element.querySelector('.' + CLASS_NAME) && !element.classList.contains('pdf-edit-pending') &&
            Number.isInteger(elementArgs(element).index),
        run: editSettings
    });

    return {
        configure: configure
    };
})();
