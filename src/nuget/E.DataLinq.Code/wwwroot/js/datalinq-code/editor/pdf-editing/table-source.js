/**
 * PDF editing: reusable source model of html tables inside draggables (static tables, later dynamic tables).
 *
 *   pdfTableSource.find(source, args, className)  -> { start, end, indent, html } of the <table>..</table>
 *   pdfTableSource.parse(html)                    -> model | null (null: loops/Razor blocks, nested tables)
 *   pdfTableSource.serialize(model, indent, nl)   -> table source (nl === '': one line, e.g. for templates)
 *   pdfTableSource.registerActions(prefix, className)
 *       registers the source actions <prefix>.get | .set | .widths | .cell (see E.DataLinq.Web pdf-report/table-editing.js)
 *
 * Model: { attributes, width, rowHeight, widths: [%], head: [row], body: [row] }
 *        width/rowHeight: { value, unit: 'px'|'mm' } | null (auto)
 *        row: { attributes, cells: [{ tag, attributes, html }] }  (html = raw cell source)
 * Column widths are stored as <colgroup><col style="width: n%" />, the row height as data-row-height on the
 * table and as style height on every row, the border as css variable --dl-table-border on the table
 * (applied to table and cells, see E.DataLinq.Web pdf-report/elements.css).
 * model.border: { width (px), style, color } | null
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/rich-text.js
 */
var pdfTableSource = new function () {
    var self = this;
    var LOOP = /@(?:foreach|for|while|do|if|switch)\b|@\{/;
    var ROW = /<tr\b([^<>]*)>([\s\S]*?)<\/tr\s*>/gi;
    var CELL = /<(td|th)\b([^<>]*)>([\s\S]*?)<\/\1\s*>/gi;
    var COL = /<col\b([^<>]*?)\/?>/gi;

    var LIMITS = { columns: [1, 30], headRows: [0, 10], bodyRows: [1, 200] };
    // lengths (table width, row height) in px or mm
    var LENGTH_LIMITS = {
        width: { px: [20, 3000], mm: [5, 800] },
        rowHeight: { px: [1, 1000], mm: [0.5, 300] }
    };

    var parseLength = function (text) {
        var m = /^\s*(\d+(?:\.\d+)?)\s*(px|mm)?\s*$/i.exec(String(text || ''));
        return m && parseFloat(m[1]) > 0 ? { value: parseFloat(m[1]), unit: (m[2] || 'px').toLowerCase() } : null;
    };

    var lengthText = function (length) {
        return length ? fmt(length.value) + length.unit : null;
    };

    /**
     * Validates a length from the preview (value 0 = auto -> null).
     * @param {number} value
     * @param {string} unit 'px' | 'mm'
     * @param {string} kind 'width' | 'rowHeight'
     * @returns {{value:number, unit:string}|null|undefined} undefined if invalid
     */
    var normalizeLength = function (value, unit, kind) {
        var n = Number(value);
        if (!Number.isFinite(n) || n < 0) return undefined;
        if (n === 0) return null;
        var limits = LENGTH_LIMITS[kind][unit === 'mm' ? 'mm' : unit === 'px' || !unit ? 'px' : null];
        if (!limits) return undefined;
        return { value: Math.min(limits[1], Math.max(limits[0], Math.round(n * 100) / 100)), unit: unit || 'px' };
    };

    // numbers (px) or { value, unit }
    var toLength = function (v) {
        return typeof v === 'number' ? (v > 0 ? { value: v, unit: 'px' } : null) : (v && v.value > 0 ? v : null);
    };
    var MIN_WIDTH = 2;   // %
    var BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    var BORDER_COLOR = /^#[0-9a-f]{6}$/i;
    var BORDER_PROP = '--dl-table-border';

    var parseBorder = function (value) {
        var m = /^(\d{1,2})px\s+(\w+)\s+(#[0-9a-f]{6})$/i.exec(String(value || '').trim());
        return m && BORDER_STYLES.indexOf(m[2].toLowerCase()) >= 0
            ? { width: parseInt(m[1], 10), style: m[2].toLowerCase(), color: m[3].toLowerCase() }
            : null;
    };

    var borderText = function (border) {
        return border ? border.width + 'px ' + border.style + ' ' + border.color : null;
    };

    /**
     * Validates a border from the preview.
     * @param {{width:number, style:string, color:string}} o
     * @returns {{width:number, style:string, color:string}|null}
     */
    this.normalizeBorder = function (o) {
        if (!o || typeof o !== 'object') return null;
        var width = Math.round(Number(o.width));
        var style = String(o.style || '').toLowerCase();
        if (!Number.isFinite(width) || width < 0 || width > 20 || BORDER_STYLES.indexOf(style) < 0 || !BORDER_COLOR.test(String(o.color))) return null;
        return { width: width, style: width === 0 ? 'none' : style, color: String(o.color).toLowerCase() };
    };
    var MAX_HTML_LENGTH = 20000;

    var fmt = function (v) {
        return pdfDraggableSource.formatNumber(v);
    };

    var attrPattern = function (name) {
        return new RegExp('\\s' + name + '\\s*=\\s*("([^"]*)"|\'([^\']*)\')', 'i');
    };

    var attrValue = function (attributes, name) {
        var m = attrPattern(name).exec(attributes || '');
        return m ? (m[2] !== undefined ? m[2] : m[3]) : null;
    };

    var setAttr = function (attributes, name, value) {
        attributes = attributes || '';
        var pattern = attrPattern(name);
        if (value === null || value === undefined || value === '') return attributes.replace(pattern, '');
        var text = ' ' + name + '="' + String(value).replace(/"/g, '\'') + '"';
        return pattern.test(attributes) ? attributes.replace(pattern, text) : attributes + text;
    };

    var styleValue = function (attributes, prop) {
        var style = attrValue(attributes, 'style') || '';
        var m = new RegExp('(?:^|;)\\s*' + prop + '\\s*:\\s*([^;]+)', 'i').exec(style);
        return m ? m[1].trim() : null;
    };

    var setStyle = function (attributes, prop, value) {
        var kept = (attrValue(attributes, 'style') || '').split(';')
            .map(function (d) { return d.trim(); })
            .filter(function (d) { return d && d.split(':')[0].trim().toLowerCase() !== prop; });
        if (value) kept.push(prop + ': ' + value);
        return setAttr(attributes, 'style', kept.join('; '));
    };

    var clamp = function (value, limits) {
        var n = Math.round(Number(value));
        return Number.isFinite(n) ? Math.min(limits[1], Math.max(limits[0], n)) : null;
    };

    this.attrValue = attrValue;
    this.setAttr = setAttr;
    this.setStyle = setStyle;

    var parseRows = function (html) {
        var rows = [], r, rowRe = new RegExp(ROW.source, 'gi');
        while ((r = rowRe.exec(html)) !== null) {
            var cells = [], c, cellRe = new RegExp(CELL.source, 'gi');
            while ((c = cellRe.exec(r[2])) !== null) {
                cells.push({ tag: c[1].toLowerCase(), attributes: c[2], html: c[3].trim() });
            }
            rows.push({ attributes: r[1], cells: cells });
        }
        return rows;
    };

    /**
     * Normalizes column widths (%): count entries, min. width, sum 100.
     * @param {number[]} widths
     * @param {number} count
     * @returns {number[]}
     */
    this.normalizeWidths = function (widths, count) {
        var valid = Array.isArray(widths) && widths.length === count &&
            widths.every(function (w) { return typeof w === 'number' && Number.isFinite(w) && w > 0; });
        var list = valid ? widths.map(function (w) { return Math.max(MIN_WIDTH, w); }) : [];
        while (list.length < count) list.push(100 / count);
        var sum = list.reduce(function (s, w) { return s + w; }, 0);
        return list.map(function (w) { return Math.round(w / sum * 10000) / 100; });
    };

    /**
     * Finds the table inside a draggable.
     * @param {string} source view source
     * @param {{index:number, originalX:number, originalY:number}} args
     * @param {string} [className] required class of the table
     * @returns {{start:number, end:number, indent:string, html:string}|null}
     */
    this.find = function (source, args, className) {
        var range = pdfDraggableSource.findElement(source, args);
        if (!range) return null;

        var body = source.substring(range.start, range.end);
        var open = /<table\b([^<>]*)>/i.exec(body);
        if (!open) return null;

        var classes = ' ' + (attrValue(open[1], 'class') || '').replace(/\s+/g, ' ') + ' ';
        if (className && classes.indexOf(' ' + className + ' ') < 0) return null;

        var close = body.toLowerCase().lastIndexOf('</table>');
        if (close < open.index) return null;

        var start = range.start + open.index;
        var end = range.start + close + '</table>'.length;
        var lineStart = source.lastIndexOf('\n', start - 1) + 1;
        return {
            start: start,
            end: end,
            indent: /^[ \t]*/.exec(source.substring(lineStart, start))[0],
            html: source.substring(start, end)
        };
    };

    /**
     * @param {string} html <table>..</table> source
     * @returns {object|null} model, null if the table can not be edited (loops, Razor blocks, nested tables)
     */
    this.parse = function (html) {
        if (LOOP.test(html)) return null;
        var open = /^<table\b([^<>]*)>/i.exec(html);
        if (!open) return null;

        var inner = html.substring(open[0].length, html.length - '</table>'.length);
        if (/<table\b/i.test(inner)) return null;

        var thead = /<thead\b[^<>]*>([\s\S]*?)<\/thead\s*>/i.exec(inner);
        var tbody = /<tbody\b[^<>]*>([\s\S]*?)<\/tbody\s*>/i.exec(inner);
        var head = thead ? parseRows(thead[1]) : [];
        var body = parseRows(tbody ? tbody[1] : inner.replace(/<thead\b[\s\S]*?<\/thead\s*>/i, ''));

        var columns = Math.max(1, Math.max.apply(null, head.concat(body).map(function (r) { return r.cells.length; }).concat([0])));
        var pad = function (rows, tag) {
            rows.forEach(function (r) {
                while (r.cells.length < columns) r.cells.push({ tag: tag, attributes: '', html: '' });
            });
        };
        pad(head, 'th');
        pad(body, 'td');

        var widths = [], c, colRe = new RegExp(COL.source, 'gi');
        while ((c = colRe.exec(inner)) !== null) {
            var w = /^([\d.]+)\s*%$/.exec(styleValue(c[1], 'width') || attrValue(c[1], 'width') || '');
            widths.push(w ? parseFloat(w[1]) : NaN);
        }

        var width = parseLength(styleValue(open[1], 'width'));
        var rowHeight = parseLength(attrValue(open[1], 'data-row-height'));

        return {
            attributes: open[1],
            width: width,
            rowHeight: rowHeight,
            border: parseBorder(styleValue(open[1], BORDER_PROP)),
            widths: self.normalizeWidths(widths, columns),
            head: head,
            body: body
        };
    };

    /**
     * @param {object} model
     * @param {string} indent indent of the <table> line
     * @param {string} nl new line ('' = everything on one line)
     * @returns {string}
     */
    this.serialize = function (model, indent, nl) {
        var compact = !nl;
        var step = compact ? '' : '    ';
        var i1 = compact ? '' : indent + step, i2 = i1 + step;
        var columns = model.widths.length;

        var attributes = setAttr(model.attributes, 'data-row-height', lengthText(model.rowHeight));
        attributes = setStyle(attributes, 'width', lengthText(model.width));
        attributes = setStyle(attributes, BORDER_PROP, borderText(model.border));

        var row = function (r) {
            var attrs = setStyle(r.attributes, 'height', lengthText(model.rowHeight));
            return i2 + '<tr' + attrs + '>' + r.cells.slice(0, columns).map(function (c) {
                return '<' + c.tag + c.attributes + '>' + c.html + '</' + c.tag + '>';
            }).join('') + '</tr>';
        };
        var section = function (tag, rows) {
            if (!rows.length) return [];
            return [i1 + '<' + tag + '>'].concat(rows.map(row), [i1 + '</' + tag + '>']);
        };

        var lines = ['<table' + attributes + '>']
            .concat([i1 + '<colgroup>' + model.widths.map(function (w) {
                return '<col style="width: ' + fmt(w) + '%" />';
            }).join('') + '</colgroup>'])
            .concat(section('thead', model.head))
            .concat(section('tbody', model.body))
            .concat([(compact ? '' : indent) + '</table>']);
        return lines.join(compact ? '' : nl);
    };

    /**
     * Creates an empty model.
     * @param {{attributes:string, columns:number, headRows:number, bodyRows:number, rowHeight:number, width:number, head:string[], body:string[]}} o
     *   head/body: optional default texts of the first column
     * @returns {object}
     */
    this.create = function (o) {
        var model = {
            attributes: o.attributes || '',
            width: toLength(o.width),
            rowHeight: toLength(o.rowHeight),
            border: self.normalizeBorder(o.border),
            widths: [],
            head: [],
            body: []
        };
        self.resize(model, { columns: o.columns || 1, headRows: o.headRows || 0, bodyRows: o.bodyRows || 1 });
        (o.head || []).forEach(function (t, i) { if (model.head[0] && model.head[0].cells[i]) model.head[0].cells[i].html = t; });
        (o.body || []).forEach(function (t, i) { if (model.body[0] && model.body[0].cells[i]) model.body[0].cells[i].html = t; });
        return model;
    };

    /**
     * Adds/removes columns and rows (at the end).
     * @param {object} model
     * @param {{columns:number, headRows:number, bodyRows:number}} size
     */
    this.resize = function (model, size) {
        var columns = size.columns;
        var widths = model.widths.slice(0, columns);
        while (widths.length < columns) {
            widths.push(widths.length ? 100 / columns : 100);
        }
        model.widths = self.normalizeWidths(widths, columns);

        var fit = function (rows, count, tag) {
            while (rows.length > count) rows.pop();
            while (rows.length < count) rows.push({ attributes: '', cells: [] });
            rows.forEach(function (r) {
                r.cells = r.cells.slice(0, columns);
                while (r.cells.length < columns) r.cells.push({ tag: tag, attributes: '', html: '' });
            });
        };
        fit(model.head, size.headRows, 'th');
        fit(model.body, size.bodyRows, 'td');
    };

    var validArgs = function (args) {
        return args && Number.isInteger(args.index) && typeof args.originalX === 'number' && typeof args.originalY === 'number';
    };

    /**
     * Registers the source actions of a table type.
     * @param {string} prefix e.g. 'table.static'
     * @param {string} className required class of the table, e.g. 'pdf-table-static'
     */
    this.registerActions = function (prefix, className) {
        var load = function (source, args) {
            if (!validArgs(args)) return { message: 'Invalid arguments.' };
            var found = self.find(source, args, className);
            if (!found) return { message: 'The table was not found in the source (it may have been edited since the last run).' };
            var model = self.parse(found.html);
            if (!model) return { message: 'The table contains Razor code that can not be edited here.' };
            return { found: found, model: model };
        };

        var edit = function (source, args, change) {
            var t = load(source, args);
            if (t.message) return t;
            var error = change(t.model);
            if (error) return { message: error };
            return {
                start: t.found.start,
                end: t.found.end,
                text: self.serialize(t.model, t.found.indent, pdfDraggableSource.newLineOf(source))
            };
        };

        //   args { index, originalX, originalY } -> data { columns, headRows, bodyRows, rowHeight, width, widths }
        pdfDraggableSource.registerAction(prefix + '.get', function (source, args) {
            var t = load(source, args);
            if (t.message) return t;
            return {
                data: {
                    columns: t.model.widths.length,
                    headRows: t.model.head.length,
                    bodyRows: t.model.body.length,
                    rowHeight: t.model.rowHeight ? t.model.rowHeight.value : 0,
                    rowHeightUnit: t.model.rowHeight ? t.model.rowHeight.unit : 'px',
                    width: t.model.width ? t.model.width.value : 0,
                    widthUnit: t.model.width ? t.model.width.unit : 'px',
                    border: t.model.border,
                    widths: t.model.widths
                }
            };
        });

        //   args { ..., table: { columns, headRows, bodyRows, rowHeight (0 = auto), rowHeightUnit ('px'|'mm'), width (0 = auto), widthUnit, border: { width, style, color } | null } }
        pdfDraggableSource.registerAction(prefix + '.set', function (source, args) {
            var o = args && args.table;
            if (!o || typeof o !== 'object') return { message: 'Invalid arguments.' };
            var size = {
                columns: clamp(o.columns, LIMITS.columns),
                headRows: clamp(o.headRows, LIMITS.headRows),
                bodyRows: clamp(o.bodyRows, LIMITS.bodyRows)
            };
            if (size.columns === null || size.headRows === null || size.bodyRows === null) return { message: 'Invalid table size.' };
            var rowHeight = normalizeLength(o.rowHeight, o.rowHeightUnit, 'rowHeight');
            var width = normalizeLength(o.width, o.widthUnit, 'width');
            if (rowHeight === undefined || width === undefined) return { message: 'Invalid width or row height.' };
            var border = o.border ? self.normalizeBorder(o.border) : null;
            if (o.border && !border) return { message: 'Invalid border.' };

            return edit(source, args, function (model) {
                self.resize(model, size);
                model.rowHeight = rowHeight;
                model.width = width;
                model.border = border;
            });
        });

        //   args { ..., widths: [%] }
        pdfDraggableSource.registerAction(prefix + '.widths', function (source, args) {
            if (!args || !Array.isArray(args.widths)) return { message: 'Invalid arguments.' };
            return edit(source, args, function (model) {
                if (args.widths.length !== model.widths.length) return 'The number of columns has changed, reload the preview.';
                model.widths = self.normalizeWidths(args.widths.map(Number), model.widths.length);
            });
        });

        //   args { ..., section: 'head'|'body', row, col, html (formatted text, see rich-text.js) }
        pdfDraggableSource.registerAction(prefix + '.cell', function (source, args) {
            if (!args || (args.section !== 'head' && args.section !== 'body') || !Number.isInteger(args.row) ||
                !Number.isInteger(args.col) || typeof args.html !== 'string' || args.html.length > MAX_HTML_LENGTH ||
                (args.background && !/^#[0-9a-f]{6}$/i.test(args.background))) {
                return { message: 'Invalid arguments.' };
            }
            return edit(source, args, function (model) {
                var row = model[args.section][args.row];
                var cell = row ? row.cells[args.col] : null;
                if (!cell) return 'The cell was not found in the source (reload the preview).';
                cell.html = pdfRichText.toSource(args.html);
                cell.attributes = setStyle(cell.attributes, 'background-color', args.background || '');
            });
        });
    };
}();
