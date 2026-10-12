/**
 * PDF editing: allow-listed rich text for text elements edited in the PDF preview
 * (see E.DataLinq.Web pdf-report/text-format.js, same allow-list).
 *
 * The preview is not trusted: formatted text (html) is parsed again and rebuilt from
 * allow-listed tags/styles only, text is escaped for Razor (@ -> @@).
 * Depends on: pdf-editing/pdf-source.js
 */
var pdfRichText = new function () {
    var TAGS = { STRONG: 'strong', B: 'strong', EM: 'em', I: 'em', U: 'u', S: 's', STRIKE: 's', DEL: 's' };
    var SPAN_PROPS = ['color', 'background-color', 'font-size', 'font-family'];
    var BLOCK_PROPS = SPAN_PROPS.concat(['font-weight', 'font-style', 'text-decoration', 'text-align']);
    var COLOR = /^(#[0-9a-f]{3,8}|rgba?\(\s*[\d.]+%?\s*,\s*[\d.]+%?\s*,\s*[\d.]+%?\s*(,\s*[\d.]+%?\s*)?\)|[a-z]{3,30})$/i;
    var VALUES = {
        'color': COLOR,
        'background-color': COLOR,
        'font-size': /^\d{1,3}(\.\d{1,2})?(pt|px|em|rem|%)$/,
        'font-family': /^[\w\s,'-]{1,120}$/,
        'font-weight': /^(normal|bold|[1-9]00)$/,
        'font-style': /^(normal|italic)$/,
        'text-decoration': /^(none|underline|line-through|underline line-through|line-through underline)$/,
        'text-align': /^(left|center|right|justify)$/
    };

    // Inner content of a text element that can be replaced: text, @@, <br>, allow-listed tags (no Razor code).
    this.innerPatternSource = '(?:[^<@]|@@|<br\\s*\\/?>|<(?:strong|em|u|s|span)(?:\\s[^<>@]*)?>|<\\/(?:strong|em|u|s|span)>)*';

    this.blockProperties = BLOCK_PROPS.slice();

    var normalizeValue = function (prop, value) {
        value = String(value === undefined || value === null ? '' : value).trim();
        if (prop === 'font-family') value = value.replace(/"/g, '\'');
        return VALUES[prop] && VALUES[prop].test(value) ? value : '';
    };

    var escapeText = function (text) {
        return String(text)
            .replace(/\u00a0/g, ' ')
            .replace(/\s+/g, ' ')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/@/g, '@@');
    };

    var styleText = function (style, props) {
        var parts = [];
        props.forEach(function (p) {
            var v = style ? normalizeValue(p, style[p]) : '';
            if (v) parts.push(p + ': ' + v);
        });
        return parts.join('; ');
    };

    var elementStyle = function (el, props) {
        var result = {};
        props.forEach(function (p) { result[p] = el.style.getPropertyValue(p); });
        return result;
    };

    var build = function (parent) {
        var out = '';
        Array.prototype.forEach.call(parent.childNodes, function (n) {
            if (n.nodeType === 3) {
                out += escapeText(n.nodeValue);
                return;
            }
            if (n.nodeType !== 1 || /^(SCRIPT|STYLE|TEMPLATE)$/.test(n.nodeName)) return;
            if (n.nodeName === 'BR') {
                out += '<br />';
                return;
            }
            if (/^(DIV|P)$/.test(n.nodeName)) {
                out += (out ? '<br />' : '') + build(n);
                return;
            }

            var inner = build(n);
            if (!inner) return;

            var css = styleText(elementStyle(n, SPAN_PROPS), SPAN_PROPS);
            if (css) inner = '<span style="' + css + '">' + inner + '</span>';

            var tag = TAGS[n.nodeName];
            out += tag ? '<' + tag + '>' + inner + '</' + tag + '>' : inner;
        });
        return out;
    };

    /**
     * Rebuilds formatted html (from the preview) as safe Razor/HTML source.
     * @param {string} html
     * @returns {string}
     */
    this.toSource = function (html) {
        var doc = new DOMParser().parseFromString('<!DOCTYPE html><html><body>' + String(html) + '</body></html>', 'text/html');
        return build(doc.body)
            .replace(/^(\s|<br \/>)+|(\s|<br \/>)+$/g, '');
    };

    /**
     * Merges block styles into the attributes of a text element. Style properties that are
     * not managed by the editor (and other attributes) are kept.
     * @param {string} attributes e.g. ' class="x" style="margin: 0"'
     * @param {object} style e.g. { 'text-align': 'center' }
     * @returns {string|null} new attributes, null if they contain Razor code
     */
    this.mergeStyleAttribute = function (attributes, style) {
        attributes = attributes || '';
        if (!style || typeof style !== 'object') return attributes;
        if (attributes.indexOf('@') >= 0) return null;

        var pattern = /\sstyle\s*=\s*("([^"]*)"|'([^']*)')/i;
        var m = pattern.exec(attributes);
        var existing = m ? (m[2] !== undefined ? m[2] : m[3]) : '';

        var kept = existing.split(';')
            .map(function (d) { return d.trim(); })
            .filter(function (d) {
                var prop = d.split(':')[0].trim().toLowerCase();
                return d && BLOCK_PROPS.indexOf(prop) < 0;
            });
        var managed = styleText(style, BLOCK_PROPS);
        var css = kept.concat(managed ? [managed] : []).join('; ');

        if (m) {
            return css
                ? attributes.replace(pattern, ' style="' + css.replace(/"/g, '\'') + '"')
                : attributes.replace(pattern, '');
        }
        return css ? attributes + ' style="' + css + '"' : attributes;
    };
}();
