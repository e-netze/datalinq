/**
 * PDF editing tool: dynamic table (table-dynamic)
 *
 * Source template and source actions of dynamic tables added/edited in the PDF preview
 * (see E.DataLinq.Web pdf-report/tools/table-dynamic.js). A dynamic table renders one row per record
 * of a data source (Model.Records or a query variable of the data tools) with @foreach:
 *
 *   <table class="pdf-table-dynamic" data-dl-dynamic="{config}" ...>
 *       <thead><tr><th>#</th><th>Header</th></tr></thead>
 *       <tbody>
 *           @foreach (var (dlRow, dlIndex) in source.Select((r, i) => (r, i + 1)))
 *           {
 *               <tr><td>@dlIndex</td><td>@dlRow["Field"]</td></tr>
 *           }
 *           @if (!source.Any())
 *           {
 *               <tr><td colspan="2">No data available.</td></tr>
 *           }
 *       </tbody>
 *   </table>
 *
 * Config: { source, columns: [{ header, field }], index: bool, empty: bool, emptyText }
 * (stored html encoded in data-dl-dynamic, the table is regenerated on every change).
 * Actions: table.dynamic.get | .set
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/table-source.js
 */
(function () {
    var CLASS_NAME = 'pdf-table-dynamic';
    var CONFIG_ATTRIBUTE = 'data-dl-dynamic';
    var SOURCE = /^(?:Model\.Records|[A-Za-z_][A-Za-z0-9_]{0,63})$/;
    var MAX_COLUMNS = 30;
    var MAX_FIELD_LENGTH = 128;
    var MAX_TEXT_LENGTH = 500;
    var LENGTH_LIMITS = {
        width: { px: [20, 3000], mm: [5, 800] },
        rowHeight: { px: [1, 1000], mm: [0.5, 300] }
    };

    var encodeAttribute = function (text) {
        return String(text)
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/@/g, '&#64;');
    };

    var decodeAttribute = function (text) {
        return String(text)
            .replace(/&quot;/g, '"')
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>')
            .replace(/&#64;/g, '@')
            .replace(/&amp;/g, '&');
    };

    var csharpString = function (text) {
        return '"' + String(text).replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
    };

    /**
     * Validates a config from the preview.
     * @returns {object|string} normalized config or an error message
     */
    var normalize = function (o) {
        if (!o || typeof o !== 'object') return 'Invalid table settings.';
        var source = String(o.source || '').trim();
        if (!SOURCE.test(source)) return 'Invalid data source (Model.Records or a variable name).';
        if (!Array.isArray(o.columns) || o.columns.length === 0) return 'At least one column is required.';
        if (o.columns.length > MAX_COLUMNS) return 'At most ' + MAX_COLUMNS + ' columns.';

        var columns = [];
        for (var i = 0; i < o.columns.length; i++) {
            var c = o.columns[i] || {};
            var field = String(c.field || '').trim();
            var header = String(c.header === undefined || c.header === null ? '' : c.header);
            if (!field || field.length > MAX_FIELD_LENGTH || /[\r\n]/.test(field)) return 'Invalid field name in column ' + (i + 1) + '.';
            if (header.length > MAX_TEXT_LENGTH) return 'The header of column ' + (i + 1) + ' is too long.';
            columns.push({ header: header, field: field });
        }

        var emptyText = String(o.emptyText || '');
        if (emptyText.length > MAX_TEXT_LENGTH) return 'The text for no records is too long.';

        var width = normalizeLength(o.width, 'width');
        var rowHeight = normalizeLength(o.rowHeight, 'rowHeight');
        if (width === undefined || rowHeight === undefined) return 'Invalid width or row height.';
        var border = o.border ? pdfTableSource.normalizeBorder(o.border) : null;
        if (o.border && !border) return 'Invalid border.';
        var count = columns.length + (o.index === true ? 1 : 0);
        var style = normalizeStyle(o.style);
        if (typeof style === 'string') return style;

        return {
            source: source,
            columns: columns,
            index: o.index === true,
            empty: o.empty === true,
            emptyText: o.empty === true ? emptyText : '',
            width: width,
            rowHeight: rowHeight,
            border: border,
            style: style,
            widths: pdfTableSource.normalizeWidths(Array.isArray(o.widths) ? o.widths.map(Number) : [], count)
        };
    };

    var BORDER_MODES = ['all', 'outer', 'horizontal', 'outer-horizontal', 'header'];
    var ALIGNS = ['left', 'center', 'right'];
    var VALIGNS = ['top', 'middle', 'bottom'];
    // font size in px (default 11)
    var DEFAULT_FONT_SIZE = 11;
    var fontSize = function (v) {
        var n = Number(v);
        return Number.isFinite(n) && n > 0 ? Math.min(200, Math.max(4, Math.round(n * 10) / 10)) : DEFAULT_FONT_SIZE;
    };
    var COLOR = /^#[0-9a-f]{6}$/i;

    // { borderMode, headAlign, bodyAlign, headBold, headBackground, headColor, oddBackground, evenBackground }
    // colors: '#rrggbb' or null (none)
    var normalizeStyle = function (s) {
        s = s && typeof s === 'object' ? s : {};
        var color = function (v) {
            if (v === null || v === undefined || v === '') return null;
            return COLOR.test(String(v)) ? String(v).toLowerCase() : undefined;
        };
        var result = {
            borderMode: BORDER_MODES.indexOf(s.borderMode) >= 0 ? s.borderMode : 'all',
            headAlign: ALIGNS.indexOf(s.headAlign) >= 0 ? s.headAlign : 'left',
            bodyAlign: ALIGNS.indexOf(s.bodyAlign) >= 0 ? s.bodyAlign : 'left',
            headVAlign: VALIGNS.indexOf(s.headVAlign) >= 0 ? s.headVAlign : 'top',
            bodyVAlign: VALIGNS.indexOf(s.bodyVAlign) >= 0 ? s.bodyVAlign : 'top',
            headFontSize: fontSize(s.headFontSize),
            bodyFontSize: fontSize(s.bodyFontSize),
            headBold: s.headBold !== false,
            headBackground: color(s.headBackground),
            headColor: color(s.headColor),
            oddBackground: color(s.oddBackground),
            evenBackground: color(s.evenBackground)
        };
        for (var k in result) if (result[k] === undefined) return 'Invalid color.';
        return result;
    };

    var styleVars = function (attributes, s) {
        attributes = pdfTableSource.setAttr(attributes, 'data-dl-border', s.borderMode === 'all' ? null : s.borderMode);
        attributes = pdfTableSource.setStyle(attributes, '--dl-head-align', s.headAlign);
        attributes = pdfTableSource.setStyle(attributes, '--dl-body-align', s.bodyAlign);
        attributes = pdfTableSource.setStyle(attributes, '--dl-head-valign', s.headVAlign);
        attributes = pdfTableSource.setStyle(attributes, '--dl-body-valign', s.bodyVAlign);
        attributes = pdfTableSource.setStyle(attributes, '--dl-head-size', s.headFontSize ? s.headFontSize + 'px' : null);
        attributes = pdfTableSource.setStyle(attributes, '--dl-body-size', s.bodyFontSize ? s.bodyFontSize + 'px' : null);
        attributes = pdfTableSource.setStyle(attributes, '--dl-head-weight', s.headBold ? 'bold' : 'normal');
        attributes = pdfTableSource.setStyle(attributes, '--dl-head-bg', s.headBackground);
        attributes = pdfTableSource.setStyle(attributes, '--dl-head-color', s.headColor);
        attributes = pdfTableSource.setStyle(attributes, '--dl-odd-bg', s.oddBackground);
        return pdfTableSource.setStyle(attributes, '--dl-even-bg', s.evenBackground);
    };

    // { value, unit: 'px'|'mm' } | null (auto, value 0) | undefined (invalid)
    var normalizeLength = function (o, kind) {
        if (o === null || o === undefined) return null;
        var n = Number(o.value);
        var unit = o.unit === 'mm' ? 'mm' : o.unit === 'px' || !o.unit ? 'px' : null;
        if (!unit || !Number.isFinite(n) || n < 0) return undefined;
        if (n === 0) return null;
        var limits = LENGTH_LIMITS[kind][unit];
        return { value: Math.min(limits[1], Math.max(limits[0], Math.round(n * 100) / 100)), unit: unit };
    };

    var lengthText = function (length) {
        return length ? pdfDraggableSource.formatNumber(length.value) + length.unit : null;
    };

    /**
     * @param {object} config normalized config
     * @param {string} attributes attributes of the <table> tag (without class/config, these are set here)
     * @param {string} indent indent of the <table> line
     * @param {string} nl line break
     * @returns {string}
     */
    var build = function (config, attributes, indent, nl) {
        var step = '    ';
        var i1 = indent + step, i2 = i1 + step, i3 = i2 + step;
        var esc = pdfDraggableSource.escapeText;
        var count = config.columns.length + (config.index ? 1 : 0);

        attributes = pdfTableSource.setAttr(attributes, 'class', pdfTableSource.attrValue(attributes, 'class') || CLASS_NAME);
        attributes = pdfTableSource.setAttr(attributes, CONFIG_ATTRIBUTE, null);
        attributes = pdfTableSource.setAttr(attributes, 'data-row-height', lengthText(config.rowHeight));
        attributes = pdfTableSource.setStyle(attributes, 'width', lengthText(config.width));
        attributes = pdfTableSource.setStyle(attributes, '--dl-table-border',
            config.border ? config.border.width + 'px ' + config.border.style + ' ' + config.border.color : null);
        attributes = styleVars(attributes, config.style);
        attributes += ' ' + CONFIG_ATTRIBUTE + '="' + encodeAttribute(JSON.stringify(config)) + '"';
        var tr = config.rowHeight ? '<tr style="height: ' + lengthText(config.rowHeight) + '">' : '<tr>';

        var headCells = (config.index ? '<th>#</th>' : '') + config.columns.map(function (c) {
            return '<th>' + esc(c.header) + '</th>';
        }).join('');
        var bodyCells = (config.index ? '<td>@dlIndex</td>' : '') + config.columns.map(function (c) {
            return '<td>@dlRow[' + csharpString(c.field) + ']</td>';
        }).join('');
        var loop = config.index
            ? '@foreach (var (dlRow, dlIndex) in ' + config.source + '.Select((r, i) => (r, i + 1)))'
            : '@foreach (var dlRow in ' + config.source + ')';

        var lines = [
            '<table' + attributes + '>',
            i1 + '<colgroup>' + config.widths.map(function (w) {
                return '<col style="width: ' + pdfDraggableSource.formatNumber(w) + '%" />';
            }).join('') + '</colgroup>',
            i1 + '<thead>',
            i2 + tr + headCells + '</tr>',
            i1 + '</thead>',
            i1 + '<tbody>',
            i2 + loop,
            i2 + '{',
            i3 + tr + bodyCells + '</tr>',
            i2 + '}'
        ];
        if (config.empty) {
            lines.push(
                i2 + '@if (!' + config.source + '.Any())',
                i2 + '{',
                i3 + tr + '<td colspan="' + count + '">' + esc(config.emptyText) + '</td></tr>',
                i2 + '}');
        }
        lines.push(i1 + '</tbody>', indent + '</table>');
        return lines.join(nl);
    };

    pdfDraggableSource.registerElementTemplate('table-dynamic', {
        // text: config (json) from the preview
        content: function (text, o) {
            var config;
            try {
                config = normalize(JSON.parse(text));
            } catch (e) {
                config = 'Invalid table settings.';
            }
            if (typeof config === 'string') {
                return '@* dynamic table: ' + config.replace(/\*@/g, '') + ' *@';
            }
            var attributes = ' class="' + CLASS_NAME + '"';
            return build(config, attributes, o && o.indent ? o.indent : '', o && o.newLine ? o.newLine : '\n');
        },
        // dynamic tables are edited with the table.dynamic.* actions (never matches plain text)
        textPattern: /(?!)()/
    });

    var validArgs = function (args) {
        return args && Number.isInteger(args.index) && typeof args.originalX === 'number' && typeof args.originalY === 'number';
    };

    var load = function (source, args) {
        if (!validArgs(args)) return { message: 'Invalid arguments.' };
        var found = pdfTableSource.find(source, args, CLASS_NAME);
        if (!found) return { message: 'The table was not found in the source (it may have been edited since the last run).' };
        var open = /^<table\b([^<>]*)>/i.exec(found.html);
        var stored = open ? pdfTableSource.attrValue(open[1], CONFIG_ATTRIBUTE) : null;
        var config = null;
        try {
            config = stored ? normalize(JSON.parse(decodeAttribute(stored))) : null;
        } catch (e) {
            config = null;
        }
        if (!config || typeof config === 'string') return { message: 'The settings of the dynamic table could not be read (' + CONFIG_ATTRIBUTE + ' missing or changed manually).' };
        return { found: found, attributes: open[1], config: config };
    };

    //   args { index, originalX, originalY } -> data: { config, + the fields of the static table actions
    //   (columns, headRows, bodyRows, rowHeight, rowHeightUnit, width, widthUnit, border, widths) used by table-editing.js }
    pdfDraggableSource.registerAction('table.dynamic.get', function (source, args) {
        var t = load(source, args);
        if (t.message) return t;
        var c = t.config;
        return {
            data: {
                config: c,
                columns: c.widths.length,
                headRows: 1,
                bodyRows: 1,
                rowHeight: c.rowHeight ? c.rowHeight.value : 0,
                rowHeightUnit: c.rowHeight ? c.rowHeight.unit : 'px',
                width: c.width ? c.width.value : 0,
                widthUnit: c.width ? c.width.unit : 'px',
                border: c.border,
                widths: c.widths
            }
        };
    });

    var replace = function (source, t, config) {
        return {
            start: t.found.start,
            end: t.found.end,
            text: build(config, t.attributes, t.found.indent, pdfDraggableSource.newLineOf(source))
        };
    };

    //   args { ..., config } -> regenerates the table from the settings modal
    //   args { ..., table: { rowHeight, rowHeightUnit, width, widthUnit, border } } -> style only (table-editing.js,
    //   e.g. dragging the table width; columns/rows are defined by the settings)
    pdfDraggableSource.registerAction('table.dynamic.set', function (source, args) {
        var t = load(source, args);
        if (t.message) return t;
        var input;
        if (args.config && typeof args.config === 'object') {
            input = args.config;
        } else if (args.table && typeof args.table === 'object') {
            var o = args.table;
            input = Object.assign({}, t.config, {
                width: { value: o.width, unit: o.widthUnit },
                rowHeight: { value: o.rowHeight, unit: o.rowHeightUnit },
                border: o.border || null
            });
        } else {
            return { message: 'Invalid arguments.' };
        }
        var config = normalize(input);
        if (typeof config === 'string') return { message: config };
        return replace(source, t, config);
    });

    //   args { ..., widths: [%] }
    pdfDraggableSource.registerAction('table.dynamic.widths', function (source, args) {
        if (!args || !Array.isArray(args.widths)) return { message: 'Invalid arguments.' };
        var t = load(source, args);
        if (t.message) return t;
        if (args.widths.length !== t.config.widths.length) return { message: 'The number of columns has changed, reload the preview.' };
        var config = normalize(Object.assign({}, t.config, { widths: args.widths.map(Number) }));
        if (typeof config === 'string') return { message: config };
        return replace(source, t, config);
    });
})();
