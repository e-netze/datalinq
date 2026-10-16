/*
 * PDF report editing tool: dynamic table
 *
 * Placed via the "Table" toolbar button (table-static.js, choose: Static | Dynamic).
 * A dynamic table renders one row per record of a data source (Model.Records or a query variable
 * added with the data tools) with @foreach. Only allowed on dynamic pages (dynamic table splitting),
 * where editing mode shows a single row (editing-mode.js).
 * Config (sent as element text, stored as data-dl-dynamic on the table):
 *   { source, columns: [{ header, field }], index: bool, empty: bool, emptyText,
 *     width: { value, unit } | null, rowHeight: { value, unit } | null, border: { width, style, color } | null, widths: [%] }
 * Context menu "Dynamic table settings..." reopens the modal (actions table.dynamic.get | .set).
 * The source template and actions live in DataLinq Code editor/pdf-editing/tools/table-dynamic.js.
 */
window.pdfEditDynamicTable = (function () {
    const CLASS_NAME = 'pdf-table-dynamic';
    const CONFIG_ATTRIBUTE = 'data-dl-dynamic';
    const DEFAULT_WIDTH = 500;
    const DEFAULT_FONT_SIZE = 11;
    const DEFAULT_BORDER = { width: 1, style: 'solid', color: '#000000' };
    const BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    const LENGTH_UNITS = ['px', 'mm'];
    const MAX_COLUMNS = 30;
    const MAX_FIELD_LENGTH = 128;
    const DEFAULT_EMPTY_TEXT = 'No data available.';

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

    const length = (v, fallback) => v && Number(v.value) > 0
        ? { value: Number(v.value), unit: v.unit === 'mm' ? 'mm' : 'px' }
        : fallback;

    const normalize = function (config) {
        const c = config && typeof config === 'object' ? config : {};
        return {
            source: typeof c.source === 'string' ? c.source : 'Model.Records',
            columns: (Array.isArray(c.columns) ? c.columns : [])
                .map(col => ({ header: String(col.header ?? ''), field: String(col.field ?? '').trim() }))
                .filter(col => col.field !== ''),
            index: c.index === true,
            empty: c.empty === true,
            emptyText: typeof c.emptyText === 'string' ? c.emptyText : '',
            width: 'width' in c ? length(c.width, null) : { value: DEFAULT_WIDTH, unit: 'px' },
            rowHeight: length(c.rowHeight, null),
            border: 'border' in c ? (c.border || null) : { ...DEFAULT_BORDER },
            style: normalizeStyle(c.style),
            widths: Array.isArray(c.widths) ? c.widths.map(Number) : []
        };
    };

    const BORDER_MODES = [
        { value: 'all', label: 'All cells' },
        { value: 'outer', label: 'Outside only' },
        { value: 'horizontal', label: 'Horizontal lines only' },
        { value: 'outer-horizontal', label: 'Outside + horizontal lines' },
        { value: 'header', label: 'Below header only' }
    ];
    const ALIGNS = [{ value: 'left', label: 'Left' }, { value: 'center', label: 'Center' }, { value: 'right', label: 'Right' }];
    const VALIGNS = [{ value: 'top', label: 'Top' }, { value: 'middle', label: 'Middle' }, { value: 'bottom', label: 'Bottom' }];
    const color = v => /^#[0-9a-f]{6}$/i.test(v || '') ? v.toLowerCase() : null;

    const normalizeStyle = function (s) {
        s = s && typeof s === 'object' ? s : {};
        const one = (v, list) => list.some(o => o.value === v) ? v : list[0].value;
        return {
            borderMode: one(s.borderMode, BORDER_MODES),
            headAlign: one(s.headAlign, ALIGNS),
            bodyAlign: one(s.bodyAlign, ALIGNS),
            headVAlign: one(s.headVAlign, VALIGNS),
            bodyVAlign: one(s.bodyVAlign, VALIGNS),
            headFontSize: Number(s.headFontSize) > 0 ? Number(s.headFontSize) : DEFAULT_FONT_SIZE,
            bodyFontSize: Number(s.bodyFontSize) > 0 ? Number(s.bodyFontSize) : DEFAULT_FONT_SIZE,
            headBold: s.headBold !== false,
            headBackground: color(s.headBackground),
            headColor: color(s.headColor),
            oddBackground: color(s.oddBackground),
            evenBackground: color(s.evenBackground)
        };
    };

    // same attributes/css variables as the source template (elements.css)
    const applyStyle = function (table, s) {
        if (s.borderMode !== 'all') table.setAttribute('data-dl-border', s.borderMode);
        const vars = {
            '--dl-head-align': s.headAlign, '--dl-body-align': s.bodyAlign, '--dl-head-weight': s.headBold ? 'bold' : 'normal',
            '--dl-head-valign': s.headVAlign, '--dl-body-valign': s.bodyVAlign,
            '--dl-head-size': s.headFontSize ? s.headFontSize + 'px' : null, '--dl-body-size': s.bodyFontSize ? s.bodyFontSize + 'px' : null,
            '--dl-head-bg': s.headBackground, '--dl-head-color': s.headColor,
            '--dl-odd-bg': s.oddBackground, '--dl-even-bg': s.evenBackground
        };
        Object.keys(vars).forEach(k => { if (vars[k]) table.style.setProperty(k, vars[k]); });
    };

    const parse = function (text) {
        try {
            return normalize(JSON.parse(text));
        } catch {
            return null;
        }
    };

    /**
     * Opens the settings modal.
     * @param {object|null} existing current config (edit) or null (new table)
     * @returns {Promise<object|null>} config or null if cancelled
     */
    const configure = async function (existing) {
        const config = existing ? normalize(existing) : null;
        const style = config || normalize(null);
        const border = style.border || DEFAULT_BORDER;
        const s = style.style;
        const sources = await loadSources();
        if (config && config.source && sources.indexOf(config.source) < 0) sources.push(config.source);

        const values = await pdfEditDialog.open({
            title: config ? 'Dynamic table settings' : 'Add dynamic table',
            submitLabel: config ? 'Apply' : 'Place',
            steps: [
                {
                    title: 'Data source',
                    description: 'One table row is rendered per record (@foreach). Add further sources with "Add query data" in the data tools.',
                    fields: [
                        { name: 'source', label: 'Records', type: 'select', value: config ? config.source : sources[0], options: sources }
                    ]
                },
                {
                    title: 'Columns',
                    description: 'Header text and field name of the record, e.g. "Area" -> @row["Area"]. Rows are used in this order.',
                    fields: [
                        {
                            name: 'columns', label: 'Columns', type: 'keyvalue',
                            value: config ? config.columns.map(c => ({ key: c.header, value: c.field })) : [{ key: '', value: '' }],
                            keyPlaceholder: 'Header', valuePlaceholder: 'Field', addLabel: 'Column', emptyText: 'No columns',
                            validate: value => {
                                const columns = (value || []).filter(p => p.value.trim() !== '');
                                if (!columns.length) return 'Add at least one column with a field name.';
                                if (columns.length > MAX_COLUMNS) return 'At most ' + MAX_COLUMNS + ' columns.';
                                if ((value || []).some(p => p.key !== '' && p.value.trim() === '')) return 'Every column needs a field name.';
                                if (columns.some(p => p.value.trim().length > MAX_FIELD_LENGTH || /[\r\n]/.test(p.value))) return 'Invalid field name.';
                                return null;
                            }
                        }
                    ]
                },
                {
                    title: 'Options',
                    fields: [
                        { name: 'index', label: 'Row number column (#)', type: 'checkbox', value: config ? config.index : false },
                        { name: 'empty', label: 'Show a row if there are no records', type: 'checkbox', value: config ? config.empty : true },
                        {
                            name: 'emptyText', label: 'Text', type: 'text',
                            value: config && config.emptyText ? config.emptyText : DEFAULT_EMPTY_TEXT,
                            showIf: v => v.empty
                        }
                    ]
                },
                {
                    title: 'Size',
                    fields: [
                        { name: 'rowHeight', label: 'Row height (0 = auto)', type: 'number', value: style.rowHeight ? style.rowHeight.value : 0, min: 0, max: 1000, step: 'any' },
                        { name: 'rowHeightUnit', label: 'Row height unit', type: 'select', value: style.rowHeight ? style.rowHeight.unit : 'px', options: LENGTH_UNITS },
                        { name: 'width', label: 'Table width (0 = auto)', type: 'number', value: style.width ? style.width.value : 0, min: 0, max: 3000, step: 'any' },
                        { name: 'widthUnit', label: 'Table width unit', type: 'select', value: style.width ? style.width.unit : 'px', options: LENGTH_UNITS }
                    ]
                },
                {
                    title: 'Border',
                    fields: [
                        { name: 'useBorder', label: 'Custom border', type: 'checkbox', value: !!style.border },
                        { name: 'borderWidth', label: 'Width (px)', type: 'number', value: border.width, min: 0, max: 20, step: 1, showIf: v => v.useBorder },
                        { name: 'borderStyle', label: 'Style', type: 'select', value: border.style, options: BORDER_STYLES, showIf: v => v.useBorder },
                        { name: 'borderColor', label: 'Color', type: 'color', value: border.color, showIf: v => v.useBorder },
                        { name: 'borderMode', label: 'Lines', type: 'select', value: s.borderMode, options: BORDER_MODES, showIf: v => v.useBorder }
                    ]
                },
                {
                    title: 'Header row',
                    fields: [
                        { name: 'headAlign', label: 'Alignment', type: 'select', value: s.headAlign, options: ALIGNS },
                        { name: 'headVAlign', label: 'Vertical alignment', type: 'select', value: s.headVAlign, options: VALIGNS },
                        { name: 'headFontSize', label: 'Font size (px)', type: 'number', value: s.headFontSize, min: 4, max: 200, step: 'any' },
                        { name: 'headBold', label: 'Bold', type: 'checkbox', value: s.headBold },
                        { name: 'useHeadBackground', label: 'Background color', type: 'checkbox', value: !!s.headBackground },
                        { name: 'headBackground', label: 'Background', type: 'color', value: s.headBackground || '#e0e0e0', showIf: v => v.useHeadBackground },
                        { name: 'useHeadColor', label: 'Text color', type: 'checkbox', value: !!s.headColor },
                        { name: 'headColor', label: 'Text', type: 'color', value: s.headColor || '#000000', showIf: v => v.useHeadColor }
                    ]
                },
                {
                    title: 'Rows',
                    fields: [
                        { name: 'bodyAlign', label: 'Alignment', type: 'select', value: s.bodyAlign, options: ALIGNS },
                        { name: 'bodyVAlign', label: 'Vertical alignment', type: 'select', value: s.bodyVAlign, options: VALIGNS },
                        { name: 'bodyFontSize', label: 'Font size (px)', type: 'number', value: s.bodyFontSize, min: 4, max: 200, step: 'any' },
                        { name: 'useOddBackground', label: 'Background of odd rows (1, 3, ...)', type: 'checkbox', value: !!s.oddBackground },
                        { name: 'oddBackground', label: 'Odd rows', type: 'color', value: s.oddBackground || '#ffffff', showIf: v => v.useOddBackground },
                        { name: 'useEvenBackground', label: 'Background of even rows (2, 4, ...)', type: 'checkbox', value: !!s.evenBackground },
                        { name: 'evenBackground', label: 'Even rows', type: 'color', value: s.evenBackground || '#f2f2f2', showIf: v => v.useEvenBackground }
                    ]
                }
            ]
        });
        if (!values) return null;

        const columns = (values.columns || []).map(p => ({ header: p.key, field: p.value }));
        const count = columns.filter(c => c.field.trim() !== '').length + (values.index ? 1 : 0);
        return normalize({
            source: values.source,
            columns: columns,
            index: values.index,
            empty: values.empty,
            emptyText: values.empty ? (values.emptyText || DEFAULT_EMPTY_TEXT) : '',
            width: { value: Math.round((Number(values.width) || 0) * 10) / 10, unit: values.widthUnit },
            rowHeight: { value: Math.round((Number(values.rowHeight) || 0) * 10) / 10, unit: values.rowHeightUnit },
            border: values.useBorder
                ? { width: Math.round(values.borderWidth), style: values.borderStyle, color: values.borderColor }
                : null,
            style: {
                borderMode: values.borderMode,
                headAlign: values.headAlign,
                bodyAlign: values.bodyAlign,
                headVAlign: values.headVAlign,
                bodyVAlign: values.bodyVAlign,
                headFontSize: Number(values.headFontSize) || null,
                bodyFontSize: Number(values.bodyFontSize) || null,
                headBold: values.headBold,
                headBackground: values.useHeadBackground ? values.headBackground : null,
                headColor: values.useHeadColor ? values.headColor : null,
                oddBackground: values.useOddBackground ? values.oddBackground : null,
                evenBackground: values.useEvenBackground ? values.evenBackground : null
            },
            // column widths
            widths: style.widths.length === count ? style.widths : []
        });
    };

    pdfEditElementTypes.register({
        type: 'table-dynamic',
        label: 'Dynamic table',
        hidden: true,           // placed via the "Table" button (table-static.js)
        saveAfterAdd: true,     // render the real records
        // preview until the source is saved: header + one placeholder row
        create: function (text) {
            const config = parse(text) || normalize(null);
            const head = (config.index ? ['#'] : []).concat(config.columns.map(c => c.header));
            const body = (config.index ? ['1'] : []).concat(config.columns.map(c => '@row["' + c.field + '"]'));
            const table = pdfEditTable.createTable({
                className: CLASS_NAME,
                border: config.border,
                head: [head],
                body: [body]
            });
            const css = v => v ? v.value + v.unit : '';
            if (config.width) table.style.width = css(config.width);
            applyStyle(table, config.style);
            if (config.rowHeight) {
                table.setAttribute('data-row-height', css(config.rowHeight));
                table.querySelectorAll('tr').forEach(tr => tr.style.height = css(config.rowHeight));
            }
            const cols = table.querySelectorAll(':scope > colgroup > col');
            if (config.widths.length === cols.length) cols.forEach((col, i) => col.style.width = config.widths[i] + '%');
            table.setAttribute(CONFIG_ATTRIBUTE, JSON.stringify(config));
            return table;
        },
        canPlace: page => page.closest('.page-wrapper.dynamic')
            ? null
            : 'Dynamic tables can only be placed on dynamic pages (dynamic table splitting).',
        textTarget: () => null
    });

    const tableOf = element => element.querySelector('table.' + CLASS_NAME);

    const editSettings = async function (element) {
        const args = elementArgs(element);
        const current = await pdfEditActions.request('table.dynamic.get', args);
        if (!current.success || !current.data) {
            pdfEditActions.notify(current.message || 'The table could not be read.');
            return;
        }
        const config = await configure(current.data.config || current.data);
        if (!config) return;

        const result = await pdfEditActions.request('table.dynamic.set', Object.assign(args, { config: config }));
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The table could not be changed.');
            return;
        }
        const page = pdfEditActions.pageOf(element.closest('.page'));
        pdfEditActions.saveAndReload(page ? page.sourceIndex : undefined);
    };

    pdfEditContextMenu.register({
        id: 'table-dynamic-settings',
        label: 'Dynamic table settings',
        order: 10,
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<rect x="3" y="4" width="18" height="16" rx="1"/><path d="M3 9h18"/><path d="M9 4v16"/>' +
            '<path d="M13 13h5"/><path d="M13 16.5h5"/></svg>',
        when: element => connected() && !!tableOf(element) && !element.classList.contains('pdf-edit-pending') &&
            Number.isInteger(elementArgs(element).index),
        run: editSettings
    });

    // column borders / outer right border can be dragged (table-editing.js), cells and structure come from the settings
    pdfEditTable.register({
        selector: 'table.' + CLASS_NAME,
        actionPrefix: 'table.dynamic',
        label: 'Dynamic table',
        cells: false,
        properties: false
    });

    return {
        configure: configure
    };
})();
