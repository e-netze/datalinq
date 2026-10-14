/**
 * PDF editing tool: input ("Inputs")
 *
 * Source template for form inputs added in the PDF preview (see E.DataLinq.Web pdf-report/tools/input.js).
 * The preview sends the settings as JSON text:
 *   { type: 'text'|'number'|'date'|'textarea'|'checkbox'|'radio'|'select'|'signature',
 *     name, label, labelPos: 'top'|'left', placeholder, required, options: [..], defaultValue,
 *     width, widthUnit, height, heightUnit, borderWidth, borderStyle, borderColor, background, color, fontSize, radius }
 *     -> <div class="dl-input dl-input-text" data-type="text" data-name="..." data-label-pos="top" data-default='...' style="...">
 *            <label class="dl-input-label">...</label><input class="dl-input-control" ...>
 *        </div>
 *
 * defaultValue (applied in the browser by pdf-report/form-inputs.js, not for signatures) is either plain text or one of
 *   @DLH.QueryParameter("key"), @DLH.GetConstant("key"), @SECURITY.GetSecret("key"), @variable(.Member...)
 * which is emitted as Razor expression: data-default='@(DLH.QueryParameter("key"))'
 *
 * Source actions (resize / settings in the preview):
 *   'input.get': { index, originalX, originalY } -> { data: { width, widthUnit, height, heightUnit, defaultValue } }
 *   'input.set': { index, originalX, originalY, settings } -> replaces the first .dl-input div in the draggable
 * All values are validated / escaped, nothing is copied verbatim. The markup must match the preview tool.
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var TYPES = ['text', 'number', 'date', 'textarea', 'checkbox', 'radio', 'select', 'signature'];
    var BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    var LABEL_POSITIONS = ['top', 'left'];
    var UNITS = ['px', 'mm', '%'];
    var MAX_SIZE = 5000;
    var MAX_OPTIONS = 50;
    var COLOR = /^#[0-9a-fA-F]{6}$/;
    var NAME = /^[A-Za-z][\w-]{0,63}$/;
    // any div open tag (quoted attribute values may contain @, e.g. the data-default Razor expression)
    var DIV_TAG = /<div\s(?:[^<>"']|"[^"]*"|'[^']*')*>/g;
    var INPUT_CLASS = /\bclass\s*=\s*"(?:[^"]*\s)?dl-input(?=[\s"])/;
    var DEFAULT_ATTR = /\sdata-default\s*=\s*'([^']*)'/;
    // default value expressions (emitted as Razor code, not as text)
    var EXPR_CALL = /^@?((?:DLH|IH|DataLinqHelper|IntegratorHelper)\.(?:QueryParameter|GetConstant)|SECURITY\.GetSecret)\(\s*("[^"\\\r\n]*")\s*\)$/;
    var EXPR_VAR = /^@([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)$/;

    var unit = function (v) { return UNITS.indexOf(v) >= 0 ? v : 'px'; };
    var clamp = function (v, min, max, fallback) {
        var n = Number(v);
        return typeof v === 'number' && isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
    };
    var num = function (v) { return pdfDraggableSource.formatNumber(v); };
    var text = function (v, max) { return typeof v === 'string' ? v.replace(/[\r\n]+/g, ' ').substring(0, max) : ''; };
    var color = function (v, fallback) { return COLOR.test(v) ? v.toLowerCase() : fallback; };

    // HTML attribute/text escaping for Razor source (@ -> @@)
    var esc = function (v) {
        return String(v)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/@/g, '@@');
    };

    // data-default attribute (single quoted, the Razor expression contains double quotes)
    var defaultAttr = function (value) {
        if (!value) return '';
        var v = value.trim(), m;
        if ((m = EXPR_CALL.exec(v))) return " data-default='@(" + m[1] + '(' + m[2] + "))'";
        if ((m = EXPR_VAR.exec(v))) return " data-default='@(" + m[1] + ")'";
        return " data-default='" + esc(value).replace(/'/g, '&#39;') + "'";
    };

    // data-default attribute value from the source -> setting (expressions with a leading @)
    var parseDefault = function (raw) {
        var m = /^@\((.*)\)$/.exec(raw);
        if (m) return '@' + m[1];
        return raw
            .replace(/@@/g, '@')
            .replace(/&#39;/g, "'")
            .replace(/&quot;/g, '"')
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>')
            .replace(/&amp;/g, '&');
    };

    var readSettings = function (raw) {
        if (!raw || typeof raw !== 'object') return null;
        var type = TYPES.indexOf(raw.type) >= 0 ? raw.type : 'text';
        var options = Array.isArray(raw.options)
            ? raw.options.map(function (o) { return text(o, 200).trim(); }).filter(function (o) { return o !== ''; }).slice(0, MAX_OPTIONS)
            : [];
        return {
            type: type,
            name: NAME.test(raw.name) ? raw.name : 'field_' + Math.random().toString(36).substring(2, 10),
            label: text(raw.label, 500),
            labelPos: LABEL_POSITIONS.indexOf(raw.labelPos) >= 0 ? raw.labelPos : 'top',
            placeholder: text(raw.placeholder, 500),
            required: raw.required === true,
            options: options,
            defaultValue: type === 'signature' ? '' : text(raw.defaultValue, 500),
            width: clamp(raw.width, 0.1, MAX_SIZE, 200),
            widthUnit: unit(raw.widthUnit),
            height: clamp(raw.height, 0.1, MAX_SIZE, 30),
            heightUnit: unit(raw.heightUnit),
            borderWidth: Math.round(clamp(raw.borderWidth, 0, 20, 1)),
            borderStyle: BORDER_STYLES.indexOf(raw.borderStyle) >= 0 ? raw.borderStyle : 'solid',
            borderColor: color(raw.borderColor, '#c8ccd2'),
            background: raw.background === 'transparent' ? 'transparent' : color(raw.background, '#ffffff'),
            color: color(raw.color, '#1f2328'),
            fontSize: clamp(raw.fontSize, 6, 72, 12),
            radius: clamp(raw.radius, 0, 100, 4)
        };
    };

    var buildInput = function (s) {
        var style = [
            'box-sizing:border-box',
            'width:' + num(s.width) + s.widthUnit,
            'height:' + num(s.height) + s.heightUnit,
            '--dl-bw:' + s.borderWidth + 'px',
            '--dl-bs:' + s.borderStyle,
            '--dl-bc:' + s.borderColor,
            '--dl-bg:' + s.background,
            '--dl-fg:' + s.color,
            '--dl-fs:' + num(s.fontSize) + 'px',
            '--dl-r:' + num(s.radius) + 'px'
        ];
        var req = s.required ? ' required' : '';
        var name = ' name="' + s.name + '"';
        var placeholder = s.placeholder ? ' placeholder="' + esc(s.placeholder) + '"' : '';
        var html = '<div class="dl-input dl-input-' + s.type + '" data-type="' + s.type + '" data-name="' + s.name + '"' +
            ' data-label-pos="' + s.labelPos + '"' + defaultAttr(s.defaultValue) + ' style="' + style.join(';') + '">';

        if (s.label && s.type !== 'checkbox') {
            html += '<label class="dl-input-label">' + esc(s.label) + '</label>';
        }

        switch (s.type) {
            case 'textarea':
                html += '<textarea class="dl-input-control"' + name + placeholder + req + '></textarea>';
                break;
            case 'checkbox':
                html += '<label class="dl-input-control dl-input-check"><input type="checkbox"' + name + req + ' /><span>' + esc(s.label) + '</span></label>';
                break;
            case 'radio':
                html += '<div class="dl-input-control dl-input-options">' +
                    s.options.map(function (o) {
                        return '<label class="dl-input-check"><input type="radio"' + name + ' value="' + esc(o) + '"' + req + ' /><span>' + esc(o) + '</span></label>';
                    }).join('') +
                    '</div>';
                break;
            case 'select':
                html += '<select class="dl-input-control"' + name + req + '><option value="">' + esc(s.placeholder) + '</option>' +
                    s.options.map(function (o) { return '<option value="' + esc(o) + '">' + esc(o) + '</option>'; }).join('') +
                    '</select>';
                break;
            case 'signature':
                html += '<div class="dl-input-control dl-signature"><canvas class="dl-signature-canvas"></canvas>' +
                    '<button type="button" class="dl-signature-clear" data-noprint="true" title="Clear">&times;</button></div>';
                break;
            default:
                html += '<input class="dl-input-control" type="' + s.type + '"' + name + placeholder + req + ' />';
                break;
        }
        return html + '</div>';
    };

    pdfDraggableSource.registerElementTemplate('input', {
        content: function (json) {
            var settings;
            try {
                settings = readSettings(JSON.parse(json));
            } catch (e) {
                settings = null;
            }
            return settings ? buildInput(settings) : '@* invalid input settings *@';
        },
        // not text editable
        textPattern: /(?!)/
    });

    // finds the .dl-input div (including nested divs) inside the draggable
    var findInput = function (source, args) {
        var element = pdfDraggableSource.findElement(source, args);
        if (!element) {
            return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };
        }

        var regex = new RegExp(DIV_TAG.source, 'g'), m;
        regex.lastIndex = element.start;
        while ((m = regex.exec(source)) !== null && m.index < element.end && !INPUT_CLASS.test(m[0])) { /* next div */ }
        if (!m || m.index + m[0].length > element.end) {
            return { message: 'The draggable does not contain an input (<div class="dl-input ...">).' };
        }

        var tags = /<div\b|<\/div\s*>/gi, depth = 1, t;
        tags.lastIndex = m.index + m[0].length;
        while ((t = tags.exec(source)) !== null && t.index < element.end) {
            depth += t[0].charAt(1) === '/' ? -1 : 1;
            if (depth === 0) {
                var end = t.index + t[0].length;
                // Razor code is only expected in the data-default attribute
                var markup = m[0].replace(DEFAULT_ATTR, '') + source.substring(m.index + m[0].length, end);
                if (/@/.test(markup.replace(/@@/g, ''))) {
                    return { message: 'The input contains Razor code and can not be replaced.' };
                }
                return { match: m, end: end };
            }
        }
        return { message: 'The input content was edited manually and can not be replaced.' };
    };

    var LENGTH = function (prop) {
        return new RegExp('(?:^|[;"\\s])' + prop + '\\s*:\\s*(\\d*\\.?\\d+)\\s*(px|mm|%)', 'i');
    };

    pdfDraggableSource.registerAction('input.get', function (source, args) {
        var found = findInput(source, args);
        if (!found.match) return found;

        var data = {};
        var tag = found.match[0].replace(DEFAULT_ATTR, '');
        [['width', 'widthUnit'], ['height', 'heightUnit']].forEach(function (p) {
            var m = LENGTH(p[0]).exec(tag);
            if (m) {
                data[p[0]] = parseFloat(m[1]);
                data[p[1]] = m[2].toLowerCase();
            }
        });
        var d = DEFAULT_ATTR.exec(found.match[0]);
        data.defaultValue = d ? parseDefault(d[1]) : '';
        return { data: data };
    });

    pdfDraggableSource.registerAction('input.set', function (source, args) {
        var settings = readSettings(args && args.settings);
        if (!settings) {
            return { message: 'Invalid input settings.' };
        }

        var found = findInput(source, args);
        if (!found.match) return found;

        return { start: found.match.index, end: found.end, text: buildInput(settings) };
    });
})();
