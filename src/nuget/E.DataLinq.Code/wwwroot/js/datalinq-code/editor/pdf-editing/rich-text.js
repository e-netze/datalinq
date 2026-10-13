/**
 * PDF editing: allow-listed rich text for text elements edited in the PDF preview
 * (see E.DataLinq.Web pdf-report/text-format.js, same allow-list).
 *
 * The preview is not trusted: formatted text (html) is parsed again and rebuilt from
 * allow-listed tags/styles only, text is escaped for Razor (@ -> @@).
 * Placeholders (<dl-token data-expr="...">) are validated (pdf-editing/tokens.js) and written as Razor expressions.
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/tokens.js
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
        'text-align': /^(left|center|right|justify)$/,
        'list-style-type': /^(disc|circle|square|decimal|lower-alpha|upper-alpha|lower-roman|upper-roman)$/
    };
    var LIST_TAGS = { UL: 'ul', OL: 'ol' };
    var MAX_LIST_DEPTH = 3;

    // Inner content of a list (ul/ol): items, nested lists and the allow-listed inline tags (no Razor code).
    this.listInnerPatternSource = '(?:[^<@]|@@|' + pdfTokens.patternSource + '|<br\\s*\\/?>|<(?:strong|b|em|i|u|s|strike|del|span|ul|ol|li)(?:\\s[^<>@]*)?>|<\\/(?:strong|b|em|i|u|s|strike|del|span|ul|ol|li)>)*';

    // Additional block properties of lists (see mergeStyleAttribute).
    this.listBlockProperties = ['list-style-type'];

    // Inner content of a text element that can be replaced: text, @@, <br>, allow-listed tags (no Razor code).
    this.innerPatternSource = '(?:[^<@]|@@|' + pdfTokens.patternSource + '|<br\\s*\\/?>|<(?:strong|b|em|i|u|s|strike|del|span)(?:\\s[^<>@]*)?>|<\\/(?:strong|b|em|i|u|s|strike|del|span)>)*';

    this.blockProperties = BLOCK_PROPS.slice();

    var normalizeValue = function (prop, value) {
        value = String(value === undefined || value === null ? '' : value).trim();
        if (prop === 'font-family') value = value.replace(/"/g, '\'');
        return VALUES[prop] && VALUES[prop].test(value) ? value : '';
    };

    var escapeText = function (text) {
        return String(text)
            .replace(/\u0001/g, '')
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

    // variables are written as markers first, resolved in resolveVariables (depends on the neighbours)
    var VAR_MARKER = /\u0001([A-Za-z_]\w*)\u0001/g;

    var tokenSource = function (expr) {
        var qp = pdfTokens.parseQueryParameter(expr);
        if (qp) return pdfTokens.formatQueryParameter(qp);
        var name = pdfTokens.parseVariable(expr);
        return name ? '\u0001' + name + '\u0001' : '';
    };

    var resolveVariables = function (s) {
        return s.replace(VAR_MARKER, function (m, name, offset, all) {
            var before = offset > 0 ? all.charAt(offset - 1) : '';
            var next = all.charAt(offset + m.length);
            return /[A-Za-z0-9]/.test(before) ? '@(' + name + ')' : pdfTokens.formatVariable(name, next === '\u0001' ? 'x' : next);
        });
    };

    var build = function (parent) {
        return buildNodes(parent.childNodes);
    };

    var buildNodes = function (nodes) {
        var out = '';
        Array.prototype.forEach.call(nodes, function (n) {
            if (n.nodeType === 3) {
                out += escapeText(n.nodeValue);
                return;
            }
            if (n.nodeType !== 1 || /^(SCRIPT|STYLE|TEMPLATE)$/.test(n.nodeName)) return;
            if (n.nodeName === 'BR') {
                out += '<br />';
                return;
            }
            if (n.nodeName === 'DL-TOKEN') {
                out += tokenSource(n.getAttribute('data-expr'));
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

    var trimBreaks = function (s) {
        return s.replace(/^(\s|<br \/>)+|(\s|<br \/>)+$/g, '');
    };

    var isList = function (n) {
        return n.nodeType === 1 && !!LIST_TAGS[n.nodeName];
    };

    var openList = function (n) {
        var css = styleText({ 'list-style-type': n.style.getPropertyValue('list-style-type') }, ['list-style-type']);
        return '<' + LIST_TAGS[n.nodeName] + (css ? ' style="' + css + '"' : '') + '>';
    };

    // item content: inline content + nested lists (max. MAX_LIST_DEPTH levels, deeper levels as text)
    var buildItem = function (li, depth) {
        var out = '', run = [];
        var flush = function () {
            out += buildNodes(run);
            run = [];
        };
        Array.prototype.forEach.call(li.childNodes, function (n) {
            if (isList(n) && depth < MAX_LIST_DEPTH) {
                flush();
                var inner = buildList(n, depth + 1);
                if (inner) out = trimBreaks(out) + openList(n) + inner + '</' + LIST_TAGS[n.nodeName] + '>';
            } else if (isList(n) || n.nodeName === 'LI') {
                run.push(document.createElement('br'));
                Array.prototype.forEach.call(n.querySelectorAll('li'), function (c) {
                    run.push(document.createTextNode(c.textContent + ' '));
                });
                if (n.nodeName === 'LI') run.push(document.createTextNode(n.textContent));
            } else {
                run.push(n);
            }
        });
        flush();
        return trimBreaks(out);
    };

    var buildList = function (list, depth) {
        var out = '', loose = [];
        var flushLoose = function () {
            var text = trimBreaks(buildNodes(loose));
            if (text) out += '<li>' + text + '</li>';
            loose = [];
        };
        Array.prototype.forEach.call(list.childNodes, function (n) {
            if (n.nodeName !== 'LI') {
                if (!(n.nodeType === 3 && !n.nodeValue.trim()) && !isList(n)) loose.push(n);
                return;
            }
            flushLoose();
            var inner = buildItem(n, depth);
            if (!inner) return;
            var css = styleText({ 'color': n.style.getPropertyValue('color') }, ['color']);
            out += '<li' + (css ? ' style="' + css + '"' : '') + '>' + inner + '</li>';
        });
        flushLoose();
        return out;
    };

    /**
     * Rebuilds formatted html (from the preview) as safe Razor/HTML source.
     * @param {string} html
     * @param {{list: boolean}} [options] list: html is the content of a ul/ol (items)
     * @returns {string}
     */
    this.toSource = function (html, options) {
        var doc = new DOMParser().parseFromString('<!DOCTYPE html><html><body>' + String(html) + '</body></html>', 'text/html');
        if (options && options.list) {
            return resolveVariables(buildList(doc.body, 1));
        }
        return resolveVariables(build(doc.body)
            .replace(/^(\s|<br \/>)+|(\s|<br \/>)+$/g, ''));
    };

    /**
     * Merges block styles into the attributes of a text element. Style properties that are
     * not managed by the editor (and other attributes) are kept.
     * @param {string} attributes e.g. ' class="x" style="margin: 0"'
     * @param {object} style e.g. { 'text-align': 'center' }
     * @param {string[]} [extraProps] additional managed properties (e.g. listBlockProperties)
     * @returns {string|null} new attributes, null if they contain Razor code
     */
    this.mergeStyleAttribute = function (attributes, style, extraProps) {
        var props = BLOCK_PROPS.concat(extraProps || []);
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
                return d && props.indexOf(prop) < 0;
            });
        var managed = styleText(style, props);
        var css = kept.concat(managed ? [managed] : []).join('; ');

        if (m) {
            return css
                ? attributes.replace(pattern, ' style="' + css.replace(/"/g, '\'') + '"')
                : attributes.replace(pattern, '');
        }
        return css ? attributes + ' style="' + css + '"' : attributes;
    };

    // Preview asks for the source of a text element containing placeholders before editing it:
    //   args { index, originalX, originalY } -> data { html, tokens } (html with <dl-token>)
    pdfDraggableSource.registerAction('element.text-source', function (source, args) {
        if (!args || !Number.isInteger(args.index) || typeof args.originalX !== 'number' || typeof args.originalY !== 'number') {
            return { message: 'Invalid arguments.' };
        }
        var block = pdfDraggableSource.findFormattedText(source, args);
        if (!block) {
            return { message: 'The text element contains Razor code that can not be edited here.' };
        }
        var inner = source.substring(source.indexOf('>', block.start + block.tag.length + block.attributes.length) + 1, block.innerEnd);
        var converted = pdfTokens.sourceToTokenHtml(inner);
        if (!converted) {
            return { message: 'The text element contains Razor code that can not be edited here.' };
        }
        return { data: { html: converted.html, tokens: converted.tokens, tag: block.tag.toLowerCase() } };
    });

    // Preview shows placeholders as chips in editing mode:
    //   args { elements: [{ index, originalX, originalY }] } -> data { items: [{ index, html }] } (only elements with placeholders)
    pdfDraggableSource.registerAction('elements.text-sources', function (source, args) {
        if (!args || !Array.isArray(args.elements) || args.elements.length > 200) {
            return { message: 'Invalid arguments.' };
        }
        var items = [];
        args.elements.forEach(function (e) {
            if (!e || !Number.isInteger(e.index) || typeof e.originalX !== 'number' || typeof e.originalY !== 'number') return;
            var block = pdfDraggableSource.findFormattedText(source, e);
            if (!block) return;
            var inner = source.substring(source.indexOf('>', block.start + block.tag.length + block.attributes.length) + 1, block.innerEnd);
            var converted = pdfTokens.sourceToTokenHtml(inner);
            if (converted && converted.tokens > 0) items.push({ index: e.index, html: converted.html });
        });
        return { data: { items: items } };
    });

    // Static tables (no loops) in draggables: placeholders in cells shown as chips in the preview.
    //   args { elements: [{ index, originalX, originalY }] }
    //   -> data { items: [{ index, cellCount, cells: [{ cell, html }] }] } (cell = index of the th/td in document order)
    var LOOP = /@(?:foreach|for|while|do)\b|@\{/;
    var CELL = /<(td|th)(?:\s[^<>]*)?>([\s\S]*?)<\/\1\s*>/gi;
    var PLAIN_CELL = /^(?:[^<]|<br\s*\/?>|<\/?(?:strong|em|u|s|span|dl-token)(?:\s[^<>]*)?>)*$/i;
    pdfDraggableSource.registerAction('elements.table-sources', function (source, args) {
        if (!args || !Array.isArray(args.elements) || args.elements.length > 200) {
            return { message: 'Invalid arguments.' };
        }
        var items = [];
        args.elements.forEach(function (e) {
            if (!e || !Number.isInteger(e.index) || typeof e.originalX !== 'number' || typeof e.originalY !== 'number') return;
            var range = pdfDraggableSource.findElement(source, e);
            if (!range) return;
            var body = source.substring(range.start, range.end);
            var tableStart = body.search(/<table\b/i);
            if (tableStart < 0 || LOOP.test(body)) return;
            var table = body.substring(tableStart);

            var cells = [], count = 0, m;
            CELL.lastIndex = 0;
            while ((m = CELL.exec(table)) !== null) {
                var converted = pdfTokens.sourceToTokenHtml(m[2]);
                if (converted && converted.tokens > 0 && PLAIN_CELL.test(converted.html)) {
                    cells.push({ cell: count, html: converted.html });
                }
                count++;
            }
            if (cells.length) items.push({ index: e.index, cellCount: count, cells: cells });
        });
        return { data: { items: items } };
    });
}();
