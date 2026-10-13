/*
 * PDF report editing mode - reusable rich text formatting for text elements (paragraph, later headings, lists, ...).
 *
 * Formatting is stored as plain, allow-listed markup (no CSS classes):
 *   - inline:  <strong>, <em>, <u>, <s>, <br>, <span style="color|background-color|font-size|font-family">
 *   - block:   style attribute of the text element itself (the properties above + font-weight,
 *              font-style, text-decoration, text-align)
 * The same allow-list is enforced again by the DataLinq Code editor (pdf-editing/rich-text.js).
 *
 * Element types opt in with   formatting: { inline: true, block: true, commands: ['bold', ...] (optional) }
 * Further commands can be added with pdfEditTextFormat.registerCommand({
 *     id, label, icon, group, type: 'toggle'|'select'|'color'|'action', scope: 'inline'|'block'|'both',
 *     options: [{ value, label }],                      // select only
 *     shortcut: 'b',                                    // optional, Ctrl/Cmd + key
 *     apply: function (ctx, value) { },                 // ctx = { node, element, range, whole, api }
 *     state: function (ctx) { return true|false|value; }
 * });
 *
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.pdfEditTextFormat = (function () {
    const TAGS = { STRONG: 'strong', B: 'strong', EM: 'em', I: 'em', U: 'u', S: 's', STRIKE: 's', DEL: 's' };
    const FORMAT_SELECTOR = 'strong,em,u,s,span';
    const SPAN_PROPS = ['color', 'background-color', 'font-size', 'font-family'];
    const BLOCK_PROPS = SPAN_PROPS.concat(['font-weight', 'font-style', 'text-decoration', 'text-align']);
    const COLOR = /^(#[0-9a-f]{3,8}|rgba?\(\s*[\d.]+%?\s*,\s*[\d.]+%?\s*,\s*[\d.]+%?\s*(,\s*[\d.]+%?\s*)?\)|[a-z]{3,30})$/i;
    const VALUES = {
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

    // lists (list element types): <ul>/<ol> root, <li> items (style: color = marker color), nested up to MAX_LIST_DEPTH
    const LIST_TAGS = { UL: 'ul', OL: 'ol' };
    const LIST_BLOCK_PROPS = BLOCK_PROPS.concat(['list-style-type']);
    const MAX_LIST_DEPTH = 3;
    const isList = n => !!(n && n.nodeType === Node.ELEMENT_NODE && LIST_TAGS[n.nodeName]);

    // ---- placeholders (tokens) -----------------------------------------------------------
    // <dl-token data-expr="@DLH.QueryParameter(...)|@name" contenteditable="false"> chips, same grammar as
    // the DataLinq Code editor (pdf-editing/tokens.js), written to the source as Razor expressions.
    const TOKEN_STR = '"(?:[^"\\\\\\r\\n]|\\\\.)*"|@"(?:[^"]|"")*"';
    const TOKEN_ARG_NAMES = ['key', 'required', 'regex', 'defaultValue'];
    const TOKEN_ARG = '(?:(?:' + TOKEN_ARG_NAMES.join('|') + ')\\s*:\\s*)?(?:' + TOKEN_STR + '|true|false)';
    const TOKEN_KEYWORDS = 'if|else|for|foreach|while|do|switch|case|try|catch|finally|lock|using|model|section|functions|' +
        'inherits|inject|await|helper|namespace|page|layout|code|implements|attribute|preservewhitespace|typeparam|rendermode|' +
        'true|false|null|new|this|default|typeof|nameof|var|return';
    const TOKEN_NAME = '(?!(?:' + TOKEN_KEYWORDS + ')\\b)[A-Za-z_]\\w*';
    const QP_FULL = new RegExp('^@DLH\\.QueryParameter\\(\\s*' + TOKEN_ARG + '(?:\\s*,\\s*' + TOKEN_ARG + '){0,3}\\s*\\)$');
    const VAR_FULL = new RegExp('^(?:@\\(\\s*(' + TOKEN_NAME + ')\\s*\\)|@(' + TOKEN_NAME + '))$');
    const VAR_TYPED = new RegExp('(^|[^A-Za-z0-9_@])@(' + TOKEN_NAME + ')(?![\\w(\\[]|\\.[A-Za-z_])', 'g');

    const unquote = literal => literal.charAt(0) === '@'
        ? literal.slice(2, -1).replace(/""/g, '"')
        : literal.slice(1, -1).replace(/\\(.)/g, (m, c) => c === 'n' ? '\n' : c === 't' ? '\t' : c);

    const parseQueryParameter = function (expr) {
        expr = String(expr || '').trim();
        if (!QP_FULL.test(expr)) return null;
        const args = expr.substring(expr.indexOf('(') + 1, expr.length - 1);
        const argRegex = new RegExp('\\s*(?:(' + TOKEN_ARG_NAMES.join('|') + ')\\s*:\\s*)?(' + TOKEN_STR + '|true|false)\\s*(?:,|$)', 'y');
        const result = { key: null, required: false, regex: '', defaultValue: '' };
        let position = 0, m;
        while (argRegex.lastIndex < args.length && (m = argRegex.exec(args)) !== null && m[0] !== '') {
            const name = m[1] || TOKEN_ARG_NAMES[position];
            position++;
            if (!name) return null;
            const isBool = m[2] === 'true' || m[2] === 'false';
            if ((name === 'required') !== isBool) return null;
            result[name] = isBool ? m[2] === 'true' : unquote(m[2]);
        }
        return typeof result.key === 'string' && result.key ? result : null;
    };

    const csLiteral = s => '"' + String(s || '').replace(/[\r\n]+/g, ' ').replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';

    const formatQueryParameter = function (p) {
        const args = [csLiteral(p.key)];
        if (p.required || p.regex || p.defaultValue) args.push(p.required ? 'true' : 'false');
        if (p.regex || p.defaultValue) args.push(csLiteral(p.regex));
        if (p.defaultValue) args.push(csLiteral(p.defaultValue));
        return '@DLH.QueryParameter(' + args.join(', ') + ')';
    };

    /**
     * @param {string} expr @DLH.QueryParameter(...) or @name
     * @returns {HTMLElement|null} a placeholder chip, null if the expression is not allowed
     */
    const createToken = function (expr) {
        const qp = parseQueryParameter(expr);
        const v = qp ? null : VAR_FULL.exec(String(expr || '').trim());
        if (!qp && !v) return null;

        const token = document.createElement('dl-token');
        token.setAttribute('contenteditable', 'false');
        if (qp) {
            token.setAttribute('data-expr', formatQueryParameter(qp));
            token.className = 'pdf-edit-token pdf-edit-token-query';
            token.textContent = '{' + qp.key + '}';
            token.title = 'Query parameter \u201c' + qp.key + '\u201d' + (qp.required ? ' (required)' : '') +
                (qp.defaultValue ? ', default: ' + qp.defaultValue : '') + (qp.regex ? ', regex: ' + qp.regex : '');
        } else {
            const name = v[1] || v[2];
            token.setAttribute('data-expr', '@' + name);
            token.className = 'pdf-edit-token pdf-edit-token-variable';
            token.textContent = '@' + name;
            token.title = 'Variable ' + name;
        }
        return token;
    };

    /**
     * Converts typed variables (@name in the text) to placeholder chips.
     * @param {HTMLElement} node
     */
    const convertTypedVariables = function (node) {
        const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT, {
            acceptNode: t => t.parentElement && t.parentElement.closest('dl-token') ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT
        });
        const texts = [];
        while (walker.nextNode()) texts.push(walker.currentNode);
        texts.forEach(t => {
            const value = t.nodeValue;
            VAR_TYPED.lastIndex = 0;
            if (!VAR_TYPED.test(value)) return;
            VAR_TYPED.lastIndex = 0;
            const fragment = document.createDocumentFragment();
            let last = 0, m;
            while ((m = VAR_TYPED.exec(value)) !== null) {
                const at = m.index + m[1].length;
                if (at > last) fragment.appendChild(document.createTextNode(value.substring(last, at)));
                fragment.appendChild(createToken('@' + m[2]));
                last = at + 1 + m[2].length;
            }
            if (last < value.length) fragment.appendChild(document.createTextNode(value.substring(last)));
            t.replaceWith(fragment);
        });
    };

    // dialogs opened by commands (focus leaves the text node, editing must continue)
    let busy = 0;

    // ---- allow-list / sanitizing ---------------------------------------------------------

    const normalizeValue = function (prop, value) {
        value = String(value || '').trim();
        if (prop === 'font-family') value = value.replace(/"/g, '\'');
        return VALUES[prop] && VALUES[prop].test(value) ? value : '';
    };

    const readStyle = function (el, props) {
        const result = {};
        props.forEach(p => {
            const v = normalizeValue(p, el.style.getPropertyValue(p));
            if (v) result[p] = v;
        });
        return result;
    };

    const writeStyle = function (el, style) {
        Object.keys(style).forEach(p => el.style.setProperty(p, style[p]));
    };

    const isEmpty = el => !el.textContent && !el.querySelector('br');

    const unwrap = function (el) {
        const parent = el.parentNode;
        while (el.firstChild) parent.insertBefore(el.firstChild, el);
        el.remove();
    };

    const clean = function (source, target) {
        Array.from(source.childNodes).forEach(n => cleanNode(n, target));
    };

    const cleanNode = function (n, target) {
        {
            if (n.nodeType === Node.TEXT_NODE) {
                if (n.nodeValue) target.appendChild(document.createTextNode(n.nodeValue.replace(/\u00a0/g, ' ')));
                return;
            }
            if (n.nodeType !== Node.ELEMENT_NODE) return;
            if (n.nodeName === 'BR') {
                target.appendChild(document.createElement('br'));
                return;
            }
            if (n.nodeName === 'DL-TOKEN') {
                const token = createToken(n.getAttribute('data-expr'));
                if (token) target.appendChild(token);
                return;
            }
            if (/^(DIV|P)$/.test(n.nodeName)) {
                if (target.childNodes.length) target.appendChild(document.createElement('br'));
                clean(n, target);
                return;
            }

            let outer = null, inner = target;
            const tag = TAGS[n.nodeName];
            if (tag) {
                outer = inner = document.createElement(tag);
            }
            const style = readStyle(n, SPAN_PROPS);
            if (Object.keys(style).length) {
                const span = document.createElement('span');
                writeStyle(span, style);
                if (outer) outer.appendChild(span); else outer = span;
                inner = span;
            }
            if (outer) {
                target.appendChild(outer);
                clean(n, inner);
            } else {
                clean(n, target);
            }
        }
    };

    const newList = function (source) {
        const list = document.createElement(LIST_TAGS[source.nodeName]);
        const type = normalizeValue('list-style-type', source.style.getPropertyValue('list-style-type'));
        if (type) list.style.setProperty('list-style-type', type);
        return list;
    };

    // content of a list item: inline formatting, <br>, nested lists (deeper levels are flattened)
    const cleanItemNode = function (n, li, depth) {
        if (isList(n)) {
            if (depth < MAX_LIST_DEPTH) {
                const sub = newList(n);
                li.appendChild(sub);
                cleanList(n, sub, depth + 1);
            } else {
                cleanList(n, li.parentNode, depth);
            }
            return;
        }
        if (n.nodeName === 'LI') {
            Array.from(n.childNodes).forEach(c => cleanItemNode(c, li, depth));
            return;
        }
        cleanNode(n, li);
    };

    const cleanList = function (source, target, depth) {
        let item = null;
        Array.from(source.childNodes).forEach(n => {
            if (n.nodeName === 'LI') {
                item = document.createElement('li');
                const color = normalizeValue('color', n.style.getPropertyValue('color'));
                if (color) item.style.setProperty('color', color);
                target.appendChild(item);
                Array.from(n.childNodes).forEach(c => cleanItemNode(c, item, depth));
                while (item.lastChild && item.lastChild.nodeName === 'BR') item.lastChild.remove();
                return;
            }
            if (n.nodeType === Node.TEXT_NODE && !n.nodeValue.trim()) return;
            if (!item) {
                item = document.createElement('li');
                target.appendChild(item);
            }
            cleanItemNode(n, item, depth);
        });
    };

    // removes empty items (no text, no nested list) and empty nested lists
    const pruneList = function (root) {
        for (let pass = 0; pass < MAX_LIST_DEPTH; pass++) {
            Array.from(root.querySelectorAll('li')).reverse().forEach(li => {
                if (!li.textContent.trim() && !Array.from(li.children).some(isList)) li.remove();
            });
            Array.from(root.querySelectorAll('ul,ol')).reverse().forEach(l => {
                if (!l.children.length) l.remove();
            });
        }
    };

    const mergeSiblings = function (parent) {
        let n = parent.firstChild;
        while (n) {
            const next = n.nextSibling;
            if (n.nodeType === Node.ELEMENT_NODE && next && next.nodeType === Node.ELEMENT_NODE &&
                !/^(BR|LI|UL|OL|DL-TOKEN)$/.test(n.nodeName) && n.nodeName === next.nodeName &&
                (n.getAttribute('style') || '') === (next.getAttribute('style') || '')) {
                while (next.firstChild) n.appendChild(next.firstChild);
                next.remove();
                continue;
            }
            if (n.nodeType === Node.ELEMENT_NODE && n.nodeName !== 'DL-TOKEN') mergeSiblings(n);
            n = n.nextSibling;
        }
    };

    const tidy = function (root) {
        root.querySelectorAll(FORMAT_SELECTOR).forEach(el => {
            if (!root.contains(el)) return;
            if (isEmpty(el)) {
                el.remove();
                return;
            }
            const outer = el.parentElement ? el.parentElement.closest(el.nodeName) : null;
            if (el.nodeName !== 'SPAN' && outer && outer !== root && root.contains(outer)) unwrap(el);
        });
        mergeSiblings(root);
        root.normalize();
    };

    /**
     * Cleans the content of a text node in place (allow-listed tags/styles only).
     * @param {HTMLElement} node
     */
    const sanitize = function (node) {
        const container = document.createElement('div');
        if (isList(node)) {
            cleanList(node, container, 1);
            pruneList(container);
        } else {
            clean(node, container);
        }
        tidy(container);
        node.textContent = '';
        while (container.firstChild) node.appendChild(container.firstChild);
    };

    const serialize = function (node) {
        const copy = node.cloneNode(true);
        sanitize(copy);
        return copy.innerHTML;
    };

    const getBlockStyle = node => readStyle(node, isList(node) ? LIST_BLOCK_PROPS : BLOCK_PROPS);

    /**
     * @returns {boolean} true if the node only contains text, <br> and allow-listed formatting tags.
     */
    const isFormattedTextNode = function (node) {
        const list = isList(node);
        return Array.from(node.querySelectorAll('*')).every(c => c.nodeName === 'BR' || TAGS[c.nodeName] || c.nodeName === 'SPAN' || c.nodeName === 'DL-TOKEN' ||
            (list && (c.nodeName === 'LI' || isList(c))));
    };

    // ---- selection helpers ---------------------------------------------------------------

    const rangeIn = function (node) {
        const selection = window.getSelection();
        if (!selection.rangeCount) return null;
        const range = selection.getRangeAt(0);
        return node.contains(range.commonAncestorContainer) ? range : null;
    };

    const coversAll = (range, node) => range.collapsed ||
        range.toString().replace(/\s+/g, '') === node.textContent.replace(/\s+/g, '');

    const select = function (first, last) {
        const range = document.createRange();
        range.setStartBefore(first);
        range.setEndAfter(last);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        return range;
    };

    const ancestors = function (range, node, test) {
        const result = [];
        let n = range.commonAncestorContainer;
        if (n.nodeType !== Node.ELEMENT_NODE) n = n.parentNode;
        while (n && n !== node) {
            if (test(n)) result.push(n);
            n = n.parentNode;
        }
        return result;
    };

    // Splits an ancestor at the range: <a>before</a> range-content <a>after</a>; returns the middle nodes.
    const splitOut = function (anc, range) {
        const parent = anc.parentNode;

        const after = document.createRange();
        after.setStart(range.endContainer, range.endOffset);
        after.setEnd(anc, anc.childNodes.length);
        const afterContent = after.extractContents();

        const before = document.createRange();
        before.setStart(anc, 0);
        before.setEnd(range.startContainer, range.startOffset);
        const beforeContent = before.extractContents();

        const b = anc.cloneNode(false);
        b.appendChild(beforeContent);
        const a = anc.cloneNode(false);
        a.appendChild(afterContent);
        const middle = Array.from(anc.childNodes);

        parent.insertBefore(b, anc);
        middle.forEach(m => parent.insertBefore(m, anc));
        parent.insertBefore(a, anc);
        anc.remove();
        [b, a].forEach(x => { if (isEmpty(x)) x.remove(); });
        return middle;
    };

    // Removes elements matching the selector from the given (top level) nodes; returns the new top level nodes.
    const strip = function (nodes, selector) {
        const result = [];
        nodes.forEach(n => {
            if (n.nodeType !== Node.ELEMENT_NODE) {
                result.push(n);
                return;
            }
            n.querySelectorAll(selector).forEach(unwrap);
            if (n.matches(selector)) {
                Array.from(n.childNodes).forEach(c => result.push(c));
                unwrap(n);
            } else {
                result.push(n);
            }
        });
        return result;
    };

    const selectNodes = function (nodes) {
        nodes = nodes.filter(n => n.parentNode);
        return nodes.length ? select(nodes[0], nodes[nodes.length - 1]) : null;
    };

    const wrap = function (range, el) {
        el.appendChild(range.extractContents());
        range.insertNode(el);
        return select(el, el);
    };

    const removeStyleProp = function (root, prop) {
        root.querySelectorAll('span').forEach(s => {
            s.style.removeProperty(prop);
            if (!s.getAttribute('style')) s.removeAttribute('style');
        });
    };

    // ---- formatting operations (ctx.api) -------------------------------------------------

    const blockTokens = (node, prop) => (node.style.getPropertyValue(prop) || '').split(/\s+/).filter(t => t && t !== 'none' && t !== 'normal');

    // font-weight/font-style may come from the stylesheet (e.g. headings are bold by default)
    const INHERITED = { 'font-weight': true, 'font-style': true };

    const matchesValue = (t, value) => t === value || (value === 'bold' && (t === 'bolder' || parseInt(t, 10) >= 600));

    const blockActive = function (node, prop, value) {
        if (INHERITED[prop] && !node.style.getPropertyValue(prop)) {
            return matchesValue(getComputedStyle(node).getPropertyValue(prop), value);
        }
        return blockTokens(node, prop).some(t => matchesValue(t, value));
    };

    const toggleBlock = function (node, prop, value) {
        const on = !blockActive(node, prop, value);
        const tokens = blockTokens(node, prop).filter(t => !matchesValue(t, value));
        if (on) tokens.push(value);
        if (tokens.length) node.style.setProperty(prop, tokens.join(' '));
        else node.style.removeProperty(prop);
        if (!on && INHERITED[prop] && blockActive(node, prop, value)) node.style.setProperty(prop, 'normal');
        return on;
    };

    const allTextIn = function (node, nodeName) {
        const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT);
        let any = false;
        while (walker.nextNode()) {
            const text = walker.currentNode;
            if (!text.nodeValue.trim()) continue;
            any = true;
            const owner = text.parentElement.closest(nodeName);
            if (!owner || !node.contains(owner) || owner === node) return false;
        }
        return any;
    };

    const api = {
        /** Toggles a semantic tag on the selection or (whole paragraph) the matching block style. */
        toggleTag: function (ctx, tag, blockProp, blockValue) {
            const T = tag.toUpperCase();
            if (ctx.whole) {
                const on = !blockActive(ctx.node, blockProp, blockValue) && !allTextIn(ctx.node, T);
                if (on !== blockActive(ctx.node, blockProp, blockValue)) toggleBlock(ctx.node, blockProp, blockValue);
                ctx.node.querySelectorAll(tag).forEach(unwrap);
                ctx.node.normalize();
                return;
            }
            const anc = ancestors(ctx.range, ctx.node, n => n.nodeName === T).pop();
            if (!anc && blockActive(ctx.node, blockProp, blockValue)) {
                // set on the whole paragraph -> move it to a tag around the content and split the selection out
                const r = ctx.range;
                const map = c => c === ctx.node ? el : c;
                const bounds = [r.startContainer, r.startOffset, r.endContainer, r.endOffset];
                const el = document.createElement(tag);
                while (ctx.node.firstChild) el.appendChild(ctx.node.firstChild);
                ctx.node.appendChild(el);
                toggleBlock(ctx.node, blockProp, blockValue);
                if (!ctx.node.getAttribute('style')) ctx.node.removeAttribute('style');
                const range = document.createRange();
                range.setStart(map(bounds[0]), bounds[1]);
                range.setEnd(map(bounds[2]), bounds[3]);
                selectNodes(strip(splitOut(el, range), tag));
                return;
            }
            if (anc) {
                selectNodes(strip(splitOut(anc, ctx.range), tag));
            } else {
                const el = document.createElement(tag);
                const range = wrap(ctx.range, el);
                el.querySelectorAll(tag).forEach(unwrap);
                return range;
            }
        },
        tagActive: function (ctx, tag, blockProp, blockValue) {
            if (blockActive(ctx.node, blockProp, blockValue)) return true;
            return !!ctx.range && ancestors(ctx.range, ctx.node, n => n.nodeName === tag.toUpperCase()).length > 0;
        },
        /** Sets (value) or removes ('') a style property on the selection or the whole paragraph. */
        setStyle: function (ctx, prop, value, blockOnly) {
            value = value ? normalizeValue(prop, value) : '';
            if (ctx.whole || blockOnly || SPAN_PROPS.indexOf(prop) < 0) {
                if (value) ctx.node.style.setProperty(prop, value);
                else ctx.node.style.removeProperty(prop);
                if (!ctx.node.getAttribute('style')) ctx.node.removeAttribute('style');
                removeStyleProp(ctx.node, prop);
                return;
            }
            if (value) {
                const span = document.createElement('span');
                span.style.setProperty(prop, value);
                wrap(ctx.range, span);
                removeStyleProp(span, prop);
                return;
            }
            let nodes = null;
            const owners = ancestors(ctx.range, ctx.node, n => n.nodeName === 'SPAN' && n.style.getPropertyValue(prop));
            let range = ctx.range;
            owners.forEach(anc => {
                if (!range) return;
                const rest = anc.cloneNode(false);
                rest.style.removeProperty(prop);
                nodes = splitOut(anc, range);
                if (rest.getAttribute('style')) {
                    range = selectNodes(nodes);
                    if (range) {
                        wrap(range, rest);
                        nodes = [rest];
                    }
                }
                range = selectNodes(nodes);
            });
            const current = owners.length ? range : ctx.range;
            if (!current) return;
            const fragment = current.extractContents();
            removeStyleProp(fragment, prop);
            const children = Array.from(fragment.childNodes);
            current.insertNode(fragment);
            selectNodes(children);
        },
        styleValue: function (ctx, prop) {
            if (ctx.range && !ctx.whole) {
                const owner = ancestors(ctx.range, ctx.node, n => n.nodeName === 'SPAN' && n.style.getPropertyValue(prop))[0];
                if (owner) return normalizeValue(prop, owner.style.getPropertyValue(prop));
            }
            return normalizeValue(prop, ctx.node.style.getPropertyValue(prop));
        },
        /** Removes all formatting from the selection or the whole paragraph. */
        clear: function (ctx) {
            if (ctx.whole) {
                BLOCK_PROPS.forEach(p => ctx.node.style.removeProperty(p));
                if (!ctx.node.getAttribute('style')) ctx.node.removeAttribute('style');
                ctx.node.querySelectorAll(FORMAT_SELECTOR).forEach(unwrap);
                ctx.node.normalize();
                return;
            }
            let range = ctx.range, nodes = null;
            ancestors(range, ctx.node, n => n.matches(FORMAT_SELECTOR)).forEach(anc => {
                if (!range) return;
                nodes = splitOut(anc, range);
                range = selectNodes(nodes);
            });
            if (!range) return;
            const fragment = range.extractContents();
            fragment.querySelectorAll(FORMAT_SELECTOR).forEach(unwrap);
            const children = Array.from(fragment.childNodes);
            range.insertNode(fragment);
            selectNodes(children);
        }
    };

    // ---- lists (root = contenteditable <ul>/<ol>) -----------------------------------------

    const ownList = li => Array.from(li.children).find(isList) || null;
    const ownText = li => Array.from(li.childNodes).filter(n => !isList(n)).map(n => n.textContent).join('');

    const subDepth = function (li) {
        const sub = ownList(li);
        return sub ? 1 + Math.max(0, ...Array.from(sub.children).map(subDepth)) : 0;
    };

    const deepestLast = function (li) {
        let sub = ownList(li);
        while (sub && sub.lastElementChild) {
            li = sub.lastElementChild;
            sub = ownList(li);
        }
        return li;
    };

    const setCaret = function (container, offset) {
        const range = document.createRange();
        range.setStart(container, offset);
        range.collapse(true);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
    };

    // moving nodes collapses ranges -> remember and restore the selection boundaries
    const keepCaret = function (root, fn) {
        const range = rangeIn(root);
        const saved = range ? [range.startContainer, range.startOffset, range.endContainer, range.endOffset] : null;
        const result = fn();
        if (saved) {
            const r = document.createRange();
            r.setStart(saved[0], saved[1]);
            r.setEnd(saved[2], saved[3]);
            const selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(r);
        }
        return result;
    };

    const placeholder = function (li) {
        if (!ownText(li) && !li.querySelector('br')) li.insertBefore(document.createElement('br'), ownList(li));
    };

    const list = {
        maxDepth: MAX_LIST_DEPTH,
        isList: isList,
        /** @returns {HTMLLIElement|null} the (innermost) item containing the caret */
        itemAt: function (root) {
            const range = rangeIn(root);
            if (!range) return null;
            let n = range.startContainer;
            if (n.nodeType !== Node.ELEMENT_NODE) n = n.parentNode;
            const li = n.closest('li');
            return li && root.contains(li) ? li : null;
        },
        /** @returns {number} 1 = top level */
        depth: function (root, li) {
            let d = 0;
            for (let n = li.parentNode; n; n = n.parentNode) {
                if (isList(n)) d++;
                if (n === root) break;
            }
            return d;
        },
        isEmptyItem: li => !ownText(li).trim() && !ownList(li),
        isLastItem: (root, li) => li.parentNode === root && !li.nextElementSibling,
        placeCaret: function (li, atEnd) {
            const sub = ownList(li);
            let index = 0;
            if (atEnd) {
                index = sub ? Array.prototype.indexOf.call(li.childNodes, sub) : li.childNodes.length;
                if (index > 0 && li.childNodes[index - 1].nodeName === 'BR') index--;
            }
            setCaret(li, index);
        },
        /** Splits the item at the caret (Enter); the rest goes into a new item. */
        splitItem: function (root, li) {
            const range = rangeIn(root);
            const item = document.createElement('li');
            if (li.style.color) item.style.color = li.style.color;
            const sub = ownList(li);
            if (range) {
                if (!range.collapsed) range.deleteContents();
                const tail = document.createRange();
                tail.setStart(range.startContainer, range.startOffset);
                if (sub) tail.setEndBefore(sub); else tail.setEnd(li, li.childNodes.length);
                item.appendChild(tail.extractContents());
            }
            if (sub) sub.insertBefore(item, sub.firstElementChild); else li.after(item);
            placeholder(li);
            placeholder(item);
            setCaret(item, 0);
            return item;
        },
        /** Adds an empty item after the given item (or at the end of the list). */
        addItem: function (root, li) {
            const item = document.createElement('li');
            item.appendChild(document.createElement('br'));
            const sub = li ? ownList(li) : null;
            if (li && li.style.color) item.style.color = li.style.color;
            if (sub) sub.insertBefore(item, sub.firstElementChild);
            else if (li) li.after(item);
            else root.appendChild(item);
            setCaret(item, 0);
            return item;
        },
        /** Removes the item (nested items move up); the caret goes to the end of the previous item. */
        removeItem: function (root, li) {
            if (!li || root.querySelectorAll('li').length <= 1) return false;
            const parentItem = li.parentNode !== root ? li.parentNode.closest('li') : null;
            const previous = li.previousElementSibling ? deepestLast(li.previousElementSibling) : parentItem;
            const next = li.nextElementSibling;
            const sub = ownList(li);
            const items = sub ? Array.from(sub.children) : [];
            const parent = li.parentNode;
            items.forEach(c => parent.insertBefore(c, li));
            li.remove();
            if (parent !== root && !parent.children.length) parent.remove();
            const target = previous || items[0] || next;
            if (target) list.placeCaret(target, !!previous);
            return true;
        },
        indent: function (root, li) {
            const previous = li ? li.previousElementSibling : null;
            if (!previous || list.depth(root, li) + subDepth(li) >= MAX_LIST_DEPTH) return false;
            return keepCaret(root, () => {
                let sub = ownList(previous);
                if (!sub) {
                    sub = document.createElement(li.parentNode.nodeName.toLowerCase());
                    while (previous.lastChild && previous.lastChild.nodeName === 'BR' && ownText(previous)) previous.lastChild.remove();
                    previous.appendChild(sub);
                }
                sub.appendChild(li);
                return true;
            });
        },
        outdent: function (root, li) {
            if (!li || li.parentNode === root) return false;
            return keepCaret(root, () => {
                const parent = li.parentNode;
                const parentItem = parent.parentNode;
                const following = [];
                for (let n = li.nextElementSibling; n; n = n.nextElementSibling) following.push(n);
                if (following.length) {
                    let sub = ownList(li);
                    if (!sub) {
                        sub = document.createElement(parent.nodeName.toLowerCase());
                        li.appendChild(sub);
                    }
                    following.forEach(f => sub.appendChild(f));
                }
                parentItem.after(li);
                if (!parent.children.length) parent.remove();
                return true;
            });
        },
        /** @returns {HTMLElement} the list containing the item (root if none) */
        listOf: (root, li) => li ? li.parentNode : root,
        /**
         * Switches the list containing the item to ul/ol. The root itself is replaced
         * with replaceRoot(newRoot) (the editor re-binds the contenteditable node).
         */
        switchType: function (root, li, tag, replaceRoot) {
            const current = list.listOf(root, li);
            if (current.nodeName.toLowerCase() === tag) return;
            if (current === root && typeof replaceRoot !== 'function') return;
            const range = rangeIn(root);
            const saved = range ? [range.startContainer, range.startOffset] : null;
            const el = document.createElement(tag);
            Array.from(current.attributes).forEach(a => el.setAttribute(a.name, a.value));
            el.style.removeProperty('list-style-type');
            if (!el.getAttribute('style')) el.removeAttribute('style');
            while (current.firstChild) el.appendChild(current.firstChild);
            if (current === root) replaceRoot(el); else current.replaceWith(el);
            if (saved && el.contains(saved[0])) setCaret(saved[0], saved[1]);
        },
        markerStyle: (root, li) => normalizeValue('list-style-type', list.listOf(root, li).style.getPropertyValue('list-style-type')),
        setMarkerStyle: function (root, li, value) {
            const target = list.listOf(root, li);
            value = normalizeValue('list-style-type', value);
            if (value) target.style.setProperty('list-style-type', value);
            else target.style.removeProperty('list-style-type');
            if (!target.getAttribute('style')) target.removeAttribute('style');
        },
        markerColor: function (root) {
            const li = root.querySelector('li');
            return li ? normalizeValue('color', li.style.getPropertyValue('color')) : '';
        },
        /**
         * Sets the marker color (color of all items). The item text keeps its current color
         * with a <span style="color"> around the item content.
         */
        setMarkerColor: function (root, value) {
            value = value ? normalizeValue('color', value) : '';
            const items = Array.from(root.querySelectorAll('li'));
            const textColors = items.map(li => toHex(getComputedStyle(li).color));
            items.forEach((li, i) => {
                if (!value) {
                    li.style.removeProperty('color');
                    if (!li.getAttribute('style')) li.removeAttribute('style');
                    return;
                }
                if (!li.style.getPropertyValue('color') && textColors[i]) {
                    const own = Array.from(li.childNodes).filter(n => !isList(n));
                    if (own.some(n => n.textContent.trim())) {
                        const span = document.createElement('span');
                        span.style.setProperty('color', textColors[i]);
                        li.insertBefore(span, own[0]);
                        own.forEach(n => span.appendChild(n));
                    }
                }
                li.style.setProperty('color', value);
            });
        }
    };

    // ---- commands ------------------------------------------------------------------------

    const commands = [];

    const registerCommand = function (command) {
        const i = commands.findIndex(c => c.id === command.id);
        if (i >= 0) commands[i] = command; else commands.push(command);
    };

    // ---- floating toolbar ----------------------------------------------------------------

    let current = null;

    const allowed = function (command, formatting) {
        if (Array.isArray(formatting.commands) && formatting.commands.indexOf(command.id) < 0) return false;
        const scope = command.scope || 'both';
        if (scope === 'list') return !!formatting.list;
        if (scope === 'inline') return formatting.inline !== false;
        if (scope === 'block') return formatting.block !== false;
        return formatting.inline !== false || formatting.block !== false;
    };

    const detach = function () {
        if (!current) return;
        current.dispose();
        current = null;
    };

    /**
     * Shows the formatting toolbar for a text node being edited.
     * @param {HTMLElement} node contenteditable text node
     * @param {HTMLElement} element the .element (used for positioning)
     * @param {{formatting: object, onLeave: function, onCancel: function, replaceNode: function}} options
     *        replaceNode(newNode): replaces the edited node (lists: switching the root ul/ol)
     */
    const attach = function (node, element, options) {
        detach();
        const formatting = options.formatting || {};
        const available = commands.filter(c => allowed(c, formatting));
        if (available.length === 0) return;

        const bar = document.createElement('div');
        bar.className = 'pdf-edit-format-toolbar';
        bar.setAttribute('role', 'toolbar');
        let savedRange = rangeIn(node) ? rangeIn(node).cloneRange() : null;
        const refreshers = [];

        const context = function () {
            let range = rangeIn(node) || savedRange;
            if (range && !node.contains(range.commonAncestorContainer)) range = null;
            const blockOnlyMode = formatting.inline === false;
            return { node: node, element: element, range: range, whole: blockOnlyMode || !range || coversAll(range, node), api: api, replaceNode: options.replaceNode };
        };

        const restoreSelection = function () {
            node.focus({ preventScroll: true });
            if (savedRange && node.contains(savedRange.commonAncestorContainer)) {
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(savedRange);
            }
        };

        const refresh = function () {
            const ctx = context();
            refreshers.forEach(r => r(ctx));
        };

        const run = function (command, value) {
            restoreSelection();
            let ctx = context();
            if (formatting.block === false && ctx.whole && ctx.range && !ctx.range.collapsed) {
                ctx = Object.assign({}, ctx, { whole: false });
            }
            try {
                command.apply(ctx, value);
            } catch (e) {
                console.error('PDF editing mode: format command failed', command.id, e);
            }
            const range = rangeIn(node);
            savedRange = range ? range.cloneRange() : null;
            refresh();
        };

        let lastGroup = null;
        available.forEach(command => {
            if (lastGroup !== null && command.group !== lastGroup) {
                const separator = document.createElement('span');
                separator.className = 'pdf-edit-format-separator';
                bar.appendChild(separator);
            }
            lastGroup = command.group;

            if (command.type === 'select') {
                const select = document.createElement('select');
                select.title = command.label;
                (command.options || []).forEach(o => {
                    const option = document.createElement('option');
                    option.value = o.value;
                    option.textContent = o.label;
                    select.appendChild(option);
                });
                select.addEventListener('change', () => run(command, select.value));
                refreshers.push(ctx => {
                    const value = typeof command.state === 'function' ? command.state(ctx) || '' : '';
                    select.value = Array.from(select.options).some(o => o.value === value) ? value : '';
                });
                bar.appendChild(select);
            } else if (command.type === 'color') {
                const wrapper = document.createElement('span');
                wrapper.className = 'pdf-edit-format-color';
                wrapper.title = command.label;
                wrapper.innerHTML = command.icon || '';
                const input = document.createElement('input');
                input.type = 'color';
                input.setAttribute('aria-label', command.label);
                input.addEventListener('change', () => run(command, input.value));
                const reset = document.createElement('button');
                reset.type = 'button';
                reset.textContent = '\u00d7';
                reset.title = command.label + ' (reset)';
                reset.addEventListener('mousedown', e => e.preventDefault());
                reset.addEventListener('click', () => run(command, ''));
                wrapper.appendChild(input);
                wrapper.appendChild(reset);
                refreshers.push(ctx => {
                    const value = typeof command.state === 'function' ? command.state(ctx) : '';
                    input.value = toHex(value) || (command.id === 'background' ? '#ffff00' : '#000000');
                });
                bar.appendChild(wrapper);
            } else {
                const button = document.createElement('button');
                button.type = 'button';
                button.title = command.label + (command.shortcut ? ' (Ctrl+' + command.shortcut.toUpperCase() + ')' : '');
                button.innerHTML = command.icon || '';
                if (!command.icon) button.textContent = command.label;
                button.addEventListener('mousedown', e => e.preventDefault());
                button.addEventListener('click', () => run(command));
                if (command.type === 'toggle') {
                    refreshers.push(ctx => button.classList.toggle('active', !!(typeof command.state === 'function' && command.state(ctx))));
                }
                bar.appendChild(button);
            }
        });

        document.body.appendChild(bar);

        const position = function () {
            const rect = element.getBoundingClientRect();
            const margin = 12;
            let top = rect.top - bar.offsetHeight - 6;
            if (top < margin) top = rect.bottom + 6;
            const left = Math.max(margin, Math.min(rect.left, window.innerWidth - bar.offsetWidth - margin));
            bar.style.top = top + 'px';
            bar.style.left = left + 'px';
        };

        const onSelectionChange = function () {
            const range = rangeIn(node);
            if (range) {
                savedRange = range.cloneRange();
                refresh();
            }
        };
        const onKeyDown = function (e) {
            if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
            const key = e.key.toLowerCase();
            const command = available.find(c => c.shortcut === key);
            if (command) {
                e.preventDefault();
                run(command);
            }
        };
        const onBarFocusOut = function (e) {
            if (e.relatedTarget && (bar.contains(e.relatedTarget) || e.relatedTarget === node)) return;
            setTimeout(() => {
                if (!busy && current && current.bar === bar && document.activeElement !== node && !bar.contains(document.activeElement)) {
                    if (typeof options.onLeave === 'function') options.onLeave();
                }
            }, 0);
        };
        const onBarKeyDown = function (e) {
            if (e.key === 'Escape' && typeof options.onCancel === 'function') {
                e.preventDefault();
                options.onCancel();
            }
        };

        const onTokenDblClick = function (e) {
            const token = e.target instanceof Element ? e.target.closest('dl-token') : null;
            if (!token || !node.contains(token)) return;
            e.preventDefault();
            e.stopPropagation();
            editToken(node, token);
        };

        document.addEventListener('selectionchange', onSelectionChange);
        node.addEventListener('keydown', onKeyDown);
        node.addEventListener('dblclick', onTokenDblClick);
        bar.addEventListener('focusout', onBarFocusOut);
        bar.addEventListener('keydown', onBarKeyDown);
        window.addEventListener('scroll', position, true);
        window.addEventListener('resize', position);

        current = {
            bar: bar,
            dispose: function () {
                document.removeEventListener('selectionchange', onSelectionChange);
                node.removeEventListener('keydown', onKeyDown);
                node.removeEventListener('dblclick', onTokenDblClick);
                window.removeEventListener('scroll', position, true);
                window.removeEventListener('resize', position);
                bar.remove();
            }
        };

        refresh();
        position();
    };

    const toHex = function (value) {
        if (!value) return '';
        if (/^#[0-9a-f]{6}$/i.test(value)) return value.toLowerCase();
        if (/^#[0-9a-f]{3}$/i.test(value)) return '#' + value.slice(1).split('').map(c => c + c).join('').toLowerCase();
        const probe = document.createElement('span');
        probe.style.color = value;
        document.body.appendChild(probe);
        const m = /rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(getComputedStyle(probe).color);
        probe.remove();
        return m ? '#' + [m[1], m[2], m[3]].map(v => ('0' + parseInt(v, 10).toString(16)).slice(-2)).join('') : '';
    };

    // ---- built-in commands -----------------------------------------------------------------

    const svg = body => '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + body + '</svg>';

    [
        { id: 'bold', label: 'Bold', icon: '<b>B</b>', shortcut: 'b', tag: 'strong', prop: 'font-weight', value: 'bold' },
        { id: 'italic', label: 'Italic', icon: '<i>I</i>', shortcut: 'i', tag: 'em', prop: 'font-style', value: 'italic' },
        { id: 'underline', label: 'Underline', icon: '<u>U</u>', shortcut: 'u', tag: 'u', prop: 'text-decoration', value: 'underline' },
        { id: 'strikethrough', label: 'Strikethrough', icon: '<s>S</s>', tag: 's', prop: 'text-decoration', value: 'line-through' }
    ].forEach(d => registerCommand({
        id: d.id, label: d.label, icon: d.icon, shortcut: d.shortcut, group: 'style', type: 'toggle', scope: 'both',
        apply: ctx => ctx.api.toggleTag(ctx, d.tag, d.prop, d.value),
        state: ctx => ctx.api.tagActive(ctx, d.tag, d.prop, d.value)
    }));

    registerCommand({
        id: 'font-size', label: 'Font size', group: 'font', type: 'select', scope: 'both',
        options: [{ value: '', label: 'Size' }].concat([8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36]
            .map(s => ({ value: s + 'pt', label: s + ' pt' }))),
        apply: (ctx, value) => ctx.api.setStyle(ctx, 'font-size', value),
        state: ctx => ctx.api.styleValue(ctx, 'font-size')
    });

    registerCommand({
        id: 'font-family', label: 'Font', group: 'font', type: 'select', scope: 'both',
        options: [
            { value: '', label: 'Font' },
            { value: 'Arial, Helvetica, sans-serif', label: 'Arial' },
            { value: 'Verdana, sans-serif', label: 'Verdana' },
            { value: '\'Times New Roman\', Times, serif', label: 'Times New Roman' },
            { value: 'Georgia, serif', label: 'Georgia' },
            { value: '\'Courier New\', Courier, monospace', label: 'Courier New' }
        ],
        apply: (ctx, value) => ctx.api.setStyle(ctx, 'font-family', value),
        state: ctx => (ctx.api.styleValue(ctx, 'font-family') || '').replace(/,\s*/g, ', ')
    });

    registerCommand({
        id: 'color', label: 'Text color', group: 'color', type: 'color', scope: 'both',
        icon: svg('<path d="M5 20h14"/><path d="M8 16l4-12 4 12"/><path d="M9.5 12h5"/>'),
        apply: (ctx, value) => ctx.api.setStyle(ctx, 'color', value),
        state: ctx => ctx.api.styleValue(ctx, 'color')
    });

    registerCommand({
        id: 'background', label: 'Background color', group: 'color', type: 'color', scope: 'both',
        icon: svg('<path d="m9 11-6 6v3h9l3-3"/><path d="m22 12-4.6 4.6a2 2 0 0 1-2.8 0l-5.2-5.2a2 2 0 0 1 0-2.8L14 4"/>'),
        apply: (ctx, value) => ctx.api.setStyle(ctx, 'background-color', value),
        state: ctx => ctx.api.styleValue(ctx, 'background-color')
    });

    [
        { id: 'align-left', label: 'Align left', value: 'left', lines: '<path d="M3 6h18"/><path d="M3 12h12"/><path d="M3 18h16"/>' },
        { id: 'align-center', label: 'Center', value: 'center', lines: '<path d="M3 6h18"/><path d="M6 12h12"/><path d="M4 18h16"/>' },
        { id: 'align-right', label: 'Align right', value: 'right', lines: '<path d="M3 6h18"/><path d="M9 12h12"/><path d="M5 18h16"/>' },
        { id: 'align-justify', label: 'Justify', value: 'justify', lines: '<path d="M3 6h18"/><path d="M3 12h18"/><path d="M3 18h18"/>' }
    ].forEach(d => registerCommand({
        id: d.id, label: d.label, icon: svg(d.lines), group: 'align', type: 'toggle', scope: 'block',
        apply: ctx => ctx.api.setStyle(ctx, 'text-align', ctx.node.style.textAlign === d.value ? '' : d.value, true),
        state: ctx => ctx.node.style.textAlign === d.value
    }));

    registerCommand({
        id: 'clear', label: 'Clear formatting', group: 'clear', type: 'action', scope: 'both',
        icon: svg('<path d="M4 7V4h16v3"/><path d="M5 20h6"/><path d="M13 4 8 20"/><path d="m15 15 5 5"/><path d="m20 15-5 5"/>'),
        apply: ctx => ctx.api.clear(ctx)
    });

    // list commands (element types with formatting.list, see tools/list.js)
    const item = ctx => list.itemAt(ctx.node);
    const lastItem = ctx => ctx.node.lastElementChild;

    [
        { tag: 'ul', label: 'Bulleted list', icon: '<path d="M9 6h11"/><path d="M9 12h11"/><path d="M9 18h11"/><circle cx="4" cy="6" r="1"/><circle cx="4" cy="12" r="1"/><circle cx="4" cy="18" r="1"/>' },
        { tag: 'ol', label: 'Numbered list', icon: '<path d="M10 6h11"/><path d="M10 12h11"/><path d="M10 18h11"/><path d="M4 4v4"/><path d="M3 14h3l-3 4h3"/>' }
    ].forEach(d => registerCommand({
        id: 'list-' + d.tag, label: d.label, icon: svg(d.icon), group: 'list', type: 'toggle', scope: 'list',
        apply: ctx => list.switchType(ctx.node, item(ctx), d.tag, ctx.replaceNode),
        state: ctx => list.listOf(ctx.node, item(ctx)).nodeName.toLowerCase() === d.tag
    }));

    registerCommand({
        id: 'list-add-item', label: 'Add item', group: 'list-items', type: 'action', scope: 'list',
        icon: svg('<path d="M12 5v14"/><path d="M5 12h14"/>'),
        apply: ctx => list.addItem(ctx.node, item(ctx) || lastItem(ctx))
    });

    registerCommand({
        id: 'list-remove-item', label: 'Remove item', group: 'list-items', type: 'action', scope: 'list',
        icon: svg('<path d="M5 12h14"/>'),
        apply: ctx => list.removeItem(ctx.node, item(ctx))
    });

    registerCommand({
        id: 'list-outdent', label: 'Outdent (Shift+Tab)', group: 'list-items', type: 'action', scope: 'list',
        icon: svg('<path d="M21 6H11"/><path d="M21 12h-8"/><path d="M21 18H11"/><path d="m7 8-4 4 4 4"/>'),
        apply: ctx => list.outdent(ctx.node, item(ctx))
    });

    registerCommand({
        id: 'list-indent', label: 'Indent (Tab, max. ' + MAX_LIST_DEPTH + ' levels)', group: 'list-items', type: 'action', scope: 'list',
        icon: svg('<path d="M21 6H11"/><path d="M21 12h-8"/><path d="M21 18H11"/><path d="m3 8 4 4-4 4"/>'),
        apply: ctx => list.indent(ctx.node, item(ctx))
    });

    registerCommand({
        id: 'list-marker-style', label: 'Marker style', group: 'list-marker', type: 'select', scope: 'list',
        options: [
            { value: '', label: 'Marker' },
            { value: 'disc', label: '\u25cf Disc' },
            { value: 'circle', label: '\u25cb Circle' },
            { value: 'square', label: '\u25a0 Square' },
            { value: 'decimal', label: '1. Decimal' },
            { value: 'lower-alpha', label: 'a. Lower alpha' },
            { value: 'upper-alpha', label: 'A. Upper alpha' },
            { value: 'lower-roman', label: 'i. Lower roman' },
            { value: 'upper-roman', label: 'I. Upper roman' }
        ],
        apply: (ctx, value) => list.setMarkerStyle(ctx.node, item(ctx), value),
        state: ctx => list.markerStyle(ctx.node, item(ctx))
    });

    registerCommand({
        id: 'list-marker-color', label: 'Marker color', group: 'list-marker', type: 'color', scope: 'list',
        icon: svg('<circle cx="6" cy="12" r="3"/><path d="M12 12h9"/>'),
        apply: (ctx, value) => list.setMarkerColor(ctx.node, value),
        state: ctx => list.markerColor(ctx.node)
    });

    // dialog for a query parameter placeholder; resolves the new chip or null
    const queryParameterDialog = function (initial) {
        const p = initial || { key: '', required: false, regex: '', defaultValue: '' };
        busy++;
        return pdfEditDialog.open({
            title: initial ? 'Edit query parameter' : 'Insert query parameter',
            submitLabel: initial ? 'Apply' : 'Insert',
            steps: [{
                description: 'Inserts @DLH.QueryParameter(...). The value of the query parameter is shown in the report.',
                fields: [
                    { name: 'key', label: 'Name', type: 'text', value: p.key, required: true, pattern: /^[A-Za-z0-9_.\-]{1,100}$/ },
                    { name: 'required', label: 'Required (the report fails without this parameter)', type: 'checkbox', value: !!p.required },
                    { name: 'defaultValue', label: 'Default value', type: 'text', value: p.defaultValue, showIf: v => !v.required },
                    { name: 'regex', label: 'Regex (validation, optional)', type: 'text', value: p.regex }
                ]
            }]
        }).then(values => {
            busy--;
            if (!values || !values.key) return null;
            return createToken(formatQueryParameter({
                key: values.key, required: !!values.required, regex: values.regex || '', defaultValue: values.required ? '' : values.defaultValue || ''
            }));
        }, () => { busy--; return null; });
    };

    // double click on a chip while editing: query parameter -> dialog, variable -> rename
    const editToken = function (node, token) {
        const expr = token.getAttribute('data-expr');
        const qp = parseQueryParameter(expr);
        if (qp) {
            if (!window.pdfEditDialog) return;
            queryParameterDialog(qp).then(newToken => {
                node.focus({ preventScroll: true });
                if (newToken && token.isConnected) token.replaceWith(newToken);
            });
            return;
        }
        if (!window.pdfEditDialog) return;
        busy++;
        pdfEditDialog.open({
            title: 'Edit variable',
            submitLabel: 'Apply',
            steps: [{
                description: 'Razor variable (@name), e.g. declared in a @{ } block of the view. Leave empty to remove it.',
                fields: [
                    { name: 'name', label: 'Name', type: 'text', value: expr.replace(/^@\(?\s*|\s*\)$/g, ''), pattern: /^([A-Za-z_]\w{0,99})?$/ }
                ]
            }]
        }).then(values => {
            busy--;
            node.focus({ preventScroll: true });
            if (!values || !token.isConnected) return;
            const name = String(values.name || '').trim();
            if (!name) { token.remove(); return; }
            const newToken = createToken('@' + name);
            if (newToken) token.replaceWith(newToken);
        }, () => { busy--; });
    };

    registerCommand({
        id: 'insert-query-parameter', label: 'Insert query parameter (variables: type @name; double click a placeholder to edit it)', group: 'placeholder', type: 'action', scope: 'inline',
        icon: svg('<path d="M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5a2 2 0 0 0 2 2h1"/><path d="M16 21h1a2 2 0 0 0 2-2v-5a2 2 0 0 1 2-2 2 2 0 0 1-2-2V5a2 2 0 0 0-2-2h-1"/>'),
        apply: function (ctx) {
            if (!window.pdfEditDialog) return;
            const node = ctx.node;
            const range = ctx.range ? ctx.range.cloneRange() : null;
            queryParameterDialog(null).then(token => {
                node.focus({ preventScroll: true });
                if (!token) return;

                let target = range && node.contains(range.commonAncestorContainer) ? range : null;
                if (!target) {
                    target = document.createRange();
                    const last = isList(node) ? (node.querySelector('li:last-child') || node) : node;
                    target.selectNodeContents(last);
                    target.collapse(false);
                }
                target.deleteContents();
                target.insertNode(token);
                const space = document.createTextNode('\u00a0');
                token.after(space);
                const selection = window.getSelection();
                const caret = document.createRange();
                caret.setStartAfter(space);
                caret.collapse(true);
                selection.removeAllRanges();
                selection.addRange(caret);
            });
        }
    });

    return {
        registerCommand: registerCommand,
        commands: () => commands.slice(),
        createToken: createToken,
        convertTypedVariables: convertTypedVariables,
        isBusy: () => busy > 0,
        sanitize: sanitize,
        serialize: serialize,
        getBlockStyle: getBlockStyle,
        isFormattedTextNode: isFormattedTextNode,
        list: list,
        api: api,
        toolbar: {
            attach: attach,
            detach: detach,
            contains: target => !!(current && target instanceof Node && current.bar.contains(target))
        }
    };
})();
