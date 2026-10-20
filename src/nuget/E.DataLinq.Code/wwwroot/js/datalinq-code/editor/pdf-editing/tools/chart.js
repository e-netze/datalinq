/**
 * PDF editing tool: chart
 *
 * Source template and source actions of charts added/edited in the PDF preview
 * (see E.DataLinq.Web pdf-report/tools/chart.js). A chart always combines one statistics helper
 * (writes a JavaScript variable) with DLH.Chart (renders the variable):
 *
 *   <div class="pdf-chart" data-dl-chart="{config}" style="width: 400px; height: 250px">
 *       @DLH.StatisticsGroupBy(Model.Records, "dlChart_ab12cd", "Field", OrderField.NameAsc)
 *       @DLH.Chart(ChartType.Bar, "dlChart_ab12cd", "Label", new { style = "width:100%;height:100%" }, new string[] { "0,155,20" }, "", ChartLocale.DE)
 *   </div>
 *
 * Config: { variable, source, statistic: 'groupBy'|'groupByDerived'|'groupByTime'|'time',
 *           field, categoryField, valueField, datetimeField, statType, orderField, secondsInterval,
 *           chartType, label, colors: ['r,g,b'], locale, width: { value, unit }, height: { value, unit } }
 * (stored html encoded in data-dl-chart, the chart is regenerated on every change).
 * Actions: chart.get | chart.set  { index, originalX, originalY [, config] }
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var CLASS_NAME = 'pdf-chart';
    var CONFIG_ATTRIBUTE = 'data-dl-chart';
    var SOURCE = /^(?:Model\.Records|[A-Za-z_][A-Za-z0-9_]{0,63})$/;
    var VARIABLE = /^dlChart_[A-Za-z0-9]{1,16}$/;
    var COLOR = /^\d{1,3},\d{1,3},\d{1,3}$/;
    var STATISTICS = ['groupBy', 'groupByDerived', 'groupByTime', 'time'];
    var STAT_TYPES = ['Sum', 'Mean', 'Min', 'Max', 'Count'];
    var ORDER_FIELDS = ['Non', 'NameAsc', 'NameDesc', 'ValueAsc', 'ValueDesc'];
    var CHART_TYPES = ['Bar', 'Line', 'Pie', 'Doughnut', 'Scatter'];
    var LOCALES = ['None', 'DE', 'US'];
    var MAX_FIELD_LENGTH = 128;
    var MAX_TEXT_LENGTH = 500;
    var MAX_COLORS = 30;
    var LENGTH_LIMITS = { px: [20, 3000], mm: [5, 800] };

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

    var one = function (v, list) {
        return list.indexOf(v) >= 0 ? v : list[0];
    };

    var field = function (v) {
        var f = String(v === undefined || v === null ? '' : v).trim();
        return f.length <= MAX_FIELD_LENGTH && !/[\r\n]/.test(f) ? f : null;
    };

    // { value, unit } | null (auto) | undefined (invalid)
    var normalizeLength = function (o) {
        if (o === null || o === undefined) return null;
        var n = Number(o.value);
        var unit = o.unit === 'mm' ? 'mm' : o.unit === 'px' || !o.unit ? 'px' : null;
        if (!unit || !Number.isFinite(n) || n < 0) return undefined;
        if (n === 0) return null;
        var limits = LENGTH_LIMITS[unit];
        return { value: Math.min(limits[1], Math.max(limits[0], Math.round(n * 100) / 100)), unit: unit };
    };

    /**
     * Validates a config from the preview.
     * @returns {object|string} normalized config or an error message
     */
    var normalize = function (o) {
        if (!o || typeof o !== 'object') return 'Invalid chart settings.';
        var variable = String(o.variable || '');
        if (!VARIABLE.test(variable)) return 'Invalid chart variable.';
        var source = String(o.source || '').trim();
        if (!SOURCE.test(source)) return 'Invalid data source (Model.Records or a variable name).';
        var statistic = one(o.statistic, STATISTICS);

        var fields = {
            field: field(o.field),
            categoryField: field(o.categoryField),
            valueField: field(o.valueField),
            datetimeField: field(o.datetimeField)
        };
        for (var k in fields) if (fields[k] === null) return 'Invalid field name.';

        var required = {
            groupBy: ['field'],
            groupByDerived: ['categoryField', 'valueField'],
            groupByTime: ['datetimeField'],
            time: ['datetimeField', 'valueField']
        }[statistic];
        for (var i = 0; i < required.length; i++) {
            if (!fields[required[i]]) return 'The field "' + required[i] + '" is required.';
        }

        var secondsInterval = Math.round(Number(o.secondsInterval) || 0);
        if (secondsInterval < 0 || secondsInterval > 100000000) return 'Invalid time interval.';

        var label = String(o.label || '');
        if (label.length > MAX_TEXT_LENGTH) return 'The label is too long.';

        var colors = (Array.isArray(o.colors) ? o.colors : []).map(function (c) { return String(c).replace(/\s/g, ''); }).filter(function (c) { return c !== ''; });
        if (colors.length > MAX_COLORS || colors.some(function (c) { return !COLOR.test(c); })) return 'Invalid colors (R,G,B).';

        var width = normalizeLength(o.width);
        var height = normalizeLength(o.height);
        if (width === undefined || height === undefined) return 'Invalid width or height.';

        return {
            variable: variable,
            source: source,
            statistic: statistic,
            field: fields.field,
            categoryField: fields.categoryField,
            valueField: fields.valueField,
            datetimeField: fields.datetimeField,
            statType: one(o.statType, STAT_TYPES),
            orderField: one(o.orderField, ORDER_FIELDS),
            secondsInterval: secondsInterval,
            chartType: one(o.chartType, CHART_TYPES),
            label: label,
            colors: colors,
            locale: one(o.locale, LOCALES),
            width: width,
            height: height
        };
    };

    var lengthText = function (length) {
        return length ? pdfDraggableSource.formatNumber(length.value) + length.unit : null;
    };

    var statisticsCall = function (c) {
        var v = csharpString(c.variable);
        switch (c.statistic) {
            case 'groupByDerived':
                return 'DLH.StatisticsGroupByDerived(' + c.source + ', ' + v + ', ' + csharpString(c.categoryField) + ', ' +
                    csharpString(c.valueField) + ', StatType.' + c.statType + ', OrderField.' + c.orderField + ')';
            case 'groupByTime':
                return 'DLH.StatisticsGroupByTime(' + c.source + ', ' + v + ', ' + csharpString(c.datetimeField) + ', ' +
                    csharpString(c.categoryField) + ', ' + c.secondsInterval + ', null, OrderField.' + c.orderField + ')';
            case 'time':
                return 'DLH.StatisticsTime(' + c.source + ', ' + v + ', ' + csharpString(c.datetimeField) + ', ' +
                    csharpString(c.valueField) + ', ' + csharpString(c.categoryField) + ')';
            default:
                return 'DLH.StatisticsGroupBy(' + c.source + ', ' + v + ', ' + csharpString(c.field) + ', OrderField.' + c.orderField + ')';
        }
    };

    var chartCall = function (c) {
        var colors = c.colors.length
            ? 'new string[] { ' + c.colors.map(csharpString).join(', ') + ' }'
            : 'null';
        // the label is rendered by the wrapper (build), the chart gets the remaining height
        return 'DLH.Chart(ChartType.' + c.chartType + ', ' + csharpString(c.variable) + ', ""' +
            ', new { style = "width:100%;flex:1 1 auto;min-height:0;position:relative" }, ' + colors + ', "", ChartLocale.' + c.locale + ')';
    };

    var build = function (config, indent, nl) {
        var style = [];
        if (config.width) style.push('width: ' + lengthText(config.width));
        if (config.height) style.push('height: ' + lengthText(config.height));
        var i1 = indent + '    ';
        var lines = [
            '<div class="' + CLASS_NAME + '"' + (style.length ? ' style="' + style.join('; ') + '"' : '') +
            ' ' + CONFIG_ATTRIBUTE + '="' + encodeAttribute(JSON.stringify(config)) + '">',
            i1 + '@(' + statisticsCall(config) + ')'
        ];
        if (config.label) lines.push(i1 + '<div class="datalinq-chart-label">' + pdfDraggableSource.escapeText(config.label) + '</div>');
        lines.push(i1 + '@(' + chartCall(config) + ')', indent + '</div>');
        return lines.join(nl);
    };

    pdfDraggableSource.registerElementTemplate('chart', {
        // text: config (json) from the preview
        content: function (text, o) {
            var config;
            try {
                config = normalize(JSON.parse(text));
            } catch (e) {
                config = 'Invalid chart settings.';
            }
            if (typeof config === 'string') {
                return '@* chart: ' + config.replace(/\*@/g, '') + ' *@';
            }
            return build(config, o && o.indent ? o.indent : '', o && o.newLine ? o.newLine : '\n');
        },
        // charts are edited with the chart.* actions (never matches plain text)
        textPattern: /(?!)()/
    });

    var validArgs = function (args) {
        return args && Number.isInteger(args.index) && typeof args.originalX === 'number' && typeof args.originalY === 'number';
    };

    // the <div class="pdf-chart" ...> ... </div> inside the draggable
    var load = function (source, args) {
        if (!validArgs(args)) return { message: 'Invalid arguments.' };
        var element = pdfDraggableSource.findElement(source, args);
        if (!element) return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };

        var inner = source.substring(element.start, element.end);
        var open = /<div\b[^<>]*\bclass="pdf-chart"[^<>]*>/i.exec(inner);
        if (!open) return { message: 'The draggable does not contain a chart.' };
        // matching </div> (the wrapper contains nested divs, e.g. the label)
        var tags = /<(\/?)div\b[^<>]*>/gi, depth = 1, close = -1, t;
        tags.lastIndex = open.index + open[0].length;
        while ((t = tags.exec(inner))) {
            if (t[1]) {
                if (--depth === 0) { close = t.index; break; }
            } else if (!/\/>$/.test(t[0])) {
                depth++;
            }
        }
        if (close < 0) return { message: 'The chart could not be parsed.' };

        var stored = new RegExp('\\b' + CONFIG_ATTRIBUTE + '="([^"]*)"').exec(open[0]);
        var config = null;
        try {
            config = stored ? normalize(JSON.parse(decodeAttribute(stored[1]))) : null;
        } catch (e) {
            config = null;
        }
        if (!config || typeof config === 'string') {
            return { message: 'The settings of the chart could not be read (' + CONFIG_ATTRIBUTE + ' missing or changed manually).' };
        }

        var start = element.start + open.index;
        var lineStart = source.lastIndexOf('\n', start - 1) + 1;
        var indent = /^[ \t]*$/.test(source.substring(lineStart, start)) ? source.substring(lineStart, start) : '';
        return { start: start, end: element.start + close + '</div>'.length, indent: indent, config: config };
    };

    pdfDraggableSource.registerAction('chart.get', function (source, args) {
        var t = load(source, args);
        return t.message ? t : { data: { config: t.config } };
    });

    pdfDraggableSource.registerAction('chart.set', function (source, args) {
        var config = normalize(args && args.config);
        if (typeof config === 'string') return { message: config };
        var t = load(source, args);
        if (t.message) return t;
        return { start: t.start, end: t.end, text: build(config, t.indent, pdfDraggableSource.newLineOf(source)) };
    });
})();
