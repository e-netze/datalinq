/**
 * PDF editing tool: shape ("Forms")
 *
 * Source template for shapes added in the PDF preview (see E.DataLinq.Web pdf-report/tools/shape.js).
 * The preview sends the settings as JSON text:
 *   { shape: 'rect', width, widthUnit, height, heightUnit, fill, opacity, borderWidth, borderStyle, borderColor, radius }
 *     (units: px | mm | %, independent for width and height)
 *     -> <div class="dl-shape dl-shape-rect" style="box-sizing:border-box;width:120px;height:80px;..."></div>
 *
 * Source actions (resize / settings in the preview):
 *   'shape.set': { index, originalX, originalY, settings } -> replaces the opening tag of the first .dl-shape div in the draggable
 * All values are validated (numbers, #rrggbb colors, allow-listed border styles), nothing is copied verbatim.
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var SHAPES = ['rect', 'circle', 'line', 'arrow'];
    var STROKE_SHAPES = ['line', 'arrow'];
    var DIRECTIONS = ['e', 'w', 's', 'n', 'se', 'ne', 'sw', 'nw'];
    var STROKE_STYLES = ['solid', 'dashed', 'dotted'];
    var BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    var MIN_SIZE = 0.1;
    var MAX_SIZE = 5000;
    var UNITS = ['px', 'mm', '%'];
    var unit = function (v) { return UNITS.indexOf(v) >= 0 ? v : 'px'; };
    var COLOR = /^#[0-9a-fA-F]{6}$/;
    var OPEN_TAG = /<div\s[^<>@]*\bclass\s*=\s*"[^"]*\bdl-shape\b[^"]*"[^<>@]*>/g;

    var clamp = function (v, min, max, fallback) {
        var n = Number(v);
        return typeof v === 'number' && isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
    };
    var num = function (v) { return pdfDraggableSource.formatNumber(v); };

    var readSettings = function (raw) {
        if (!raw || typeof raw !== 'object') return null;
        var shape = SHAPES.indexOf(raw.shape) >= 0 ? raw.shape : 'rect';
        var stroke = STROKE_SHAPES.indexOf(shape) >= 0;
        return {
            shape: shape,
            stroke: stroke,
            direction: DIRECTIONS.indexOf(raw.direction) >= 0 ? raw.direction : 'e',
            width: clamp(raw.width, MIN_SIZE, MAX_SIZE, 120),
            widthUnit: unit(raw.widthUnit),
            height: clamp(raw.height, MIN_SIZE, MAX_SIZE, 80),
            heightUnit: unit(raw.heightUnit),
            fill: !stroke && COLOR.test(raw.fill) ? raw.fill.toLowerCase() : 'transparent',
            opacity: clamp(raw.opacity, 0, 1, 1),
            borderWidth: Math.round(clamp(raw.borderWidth, stroke ? 1 : 0, 50, 1)),
            borderStyle: (stroke ? STROKE_STYLES : BORDER_STYLES).indexOf(raw.borderStyle) >= 0 ? raw.borderStyle : 'solid',
            borderColor: COLOR.test(raw.borderColor) ? raw.borderColor.toLowerCase() : '#000000',
            marginX: clamp(raw.marginX, 0, MAX_SIZE, 0),
            marginY: clamp(raw.marginY, 0, MAX_SIZE, 0),
            radius: shape === 'rect' ? clamp(raw.radius, 0, MAX_SIZE, 0) : 0,
            rotation: Math.round((((clamp(raw.rotation, -100000, 100000, 0) % 360) + 540) % 360 - 180) * 10) / 10
        };
    };

    // inline svg of line/arrow shapes (same markup as the preview tool)
    var strokeSvg = function (s) {
        var d = s.direction, w = s.borderWidth, color = s.borderColor;
        var x1 = d.indexOf('w') >= 0 ? 100 : d.indexOf('e') >= 0 ? 0 : 50;
        var y1 = d.indexOf('n') >= 0 ? 100 : d.indexOf('s') >= 0 ? 0 : 50;
        var x2 = x1 === 50 ? 50 : 100 - x1, y2 = y1 === 50 ? 50 : 100 - y1;
        var dash = s.borderStyle === 'dashed' ? ' stroke-dasharray="' + (w * 3) + ' ' + (w * 2) + '"'
            : s.borderStyle === 'dotted' ? ' stroke-dasharray="0 ' + (w * 2) + '" stroke-linecap="round"' : '';
        var id = 'dl-arrow-' + color.substring(1);
        var marker = s.shape === 'arrow'
            ? '<defs><marker id="' + id + '" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="4" markerHeight="4" orient="auto"><path d="M0,0L10,5L0,10z" fill="' + color + '"/></marker></defs>'
            : '';
        return '<svg width="100%" height="100%" style="display:block;overflow:visible">' + marker +
            '<line x1="' + x1 + '%" y1="' + y1 + '%" x2="' + x2 + '%" y2="' + y2 + '%" stroke="' + color + '" stroke-width="' + w + '"' + dash +
            (marker ? ' marker-end="url(#' + id + ')"' : '') + '/></svg>';
    };

    var buildShape = function (s) {
        return buildOpenTag(s) + (s.stroke ? strokeSvg(s) : '') + '</div>';
    };

    var buildOpenTag = function (s) {
        var style = [
            'box-sizing:border-box',
            'width:' + num(s.width) + s.widthUnit,
            'height:' + num(s.height) + s.heightUnit
        ];
        if (!s.stroke) {
            style.push('background-color:' + s.fill);
            style.push('border:' + (s.borderStyle === 'none' || s.borderWidth === 0 ? 'none' : s.borderWidth + 'px ' + s.borderStyle + ' ' + s.borderColor));
        }
        if (s.shape === 'circle') style.push('border-radius:50%');
        else if (s.radius) style.push('border-radius:' + num(s.radius) + 'px');
        if (s.opacity < 1) style.push('opacity:' + num(s.opacity));
        if (s.rotation) style.push('transform-origin:center', 'transform:rotate(' + num(s.rotation) + 'deg)');
        // bounding box of the rotated shape, so the draggable contains it completely
        if (s.rotation && (s.marginX || s.marginY)) style.push('margin:' + num(s.marginY) + 'px ' + num(s.marginX) + 'px');
        return '<div class="dl-shape dl-shape-' + s.shape + '"' + (s.stroke ? ' data-dir="' + s.direction + '"' : '') + ' style="' + style.join(';') + '">';
    };

    pdfDraggableSource.registerElementTemplate('shape', {
        content: function (text) {
            var settings;
            try {
                settings = readSettings(JSON.parse(text));
            } catch (e) {
                settings = null;
            }
            return settings ? buildShape(settings) : '@* invalid shape settings *@';
        },
        // not text editable
        textPattern: /(?!)/
    });

    var findOpenTag = function (source, args) {
        var element = pdfDraggableSource.findElement(source, args);
        if (!element) {
            return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };
        }

        var regex = new RegExp(OPEN_TAG.source, 'g');
        regex.lastIndex = element.start;
        var m = regex.exec(source);
        if (!m || m.index + m[0].length > element.end) {
            return { message: 'The draggable does not contain a shape (<div class="dl-shape ...">) or it uses Razor code.' };
        }
        // shapes contain no nested divs (at most an inline svg)
        var close = source.indexOf('</div>', m.index + m[0].length);
        if (close < 0 || close + 6 > element.end || /<div\b|@/.test(source.substring(m.index + m[0].length, close))) {
            return { message: 'The shape content was edited manually and can not be replaced.' };
        }
        return { match: m, end: close + 6 };
    };

    var LENGTH = function (prop) {
        return new RegExp('(?:^|[;"\\s])' + prop + '\\s*:\\s*(\\d*\\.?\\d+)\\s*(px|mm|%)', 'i');
    };

    // 'shape.get': { index, originalX, originalY } -> { data: { width, widthUnit, height, heightUnit } } (size as written in the source)
    pdfDraggableSource.registerAction('shape.get', function (source, args) {
        var found = findOpenTag(source, args);
        if (!found.match) return found;

        var data = {};
        [['width', 'widthUnit'], ['height', 'heightUnit']].forEach(function (p) {
            var m = LENGTH(p[0]).exec(found.match[0]);
            if (m) {
                data[p[0]] = parseFloat(m[1]);
                data[p[1]] = m[2].toLowerCase();
            }
        });
        return { data: data };
    });

    pdfDraggableSource.registerAction('shape.set', function (source, args) {
        var settings = readSettings(args && args.settings);
        if (!settings) {
            return { message: 'Invalid shape settings.' };
        }

        var found = findOpenTag(source, args);
        if (!found.match) return found;
        var m = found.match;

        return { start: m.index, end: found.end, text: buildShape(settings) };
    });
})();
