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
        'text-align': /^(left|center|right|justify)$/
    };

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
        Array.from(source.childNodes).forEach(n => {
            if (n.nodeType === Node.TEXT_NODE) {
                if (n.nodeValue) target.appendChild(document.createTextNode(n.nodeValue.replace(/\u00a0/g, ' ')));
                return;
            }
            if (n.nodeType !== Node.ELEMENT_NODE) return;
            if (n.nodeName === 'BR') {
                target.appendChild(document.createElement('br'));
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
        });
    };

    const mergeSiblings = function (parent) {
        let n = parent.firstChild;
        while (n) {
            const next = n.nextSibling;
            if (n.nodeType === Node.ELEMENT_NODE && next && next.nodeType === Node.ELEMENT_NODE &&
                n.nodeName !== 'BR' && n.nodeName === next.nodeName &&
                (n.getAttribute('style') || '') === (next.getAttribute('style') || '')) {
                while (next.firstChild) n.appendChild(next.firstChild);
                next.remove();
                continue;
            }
            if (n.nodeType === Node.ELEMENT_NODE) mergeSiblings(n);
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
        clean(node, container);
        tidy(container);
        node.textContent = '';
        while (container.firstChild) node.appendChild(container.firstChild);
    };

    const serialize = function (node) {
        const copy = node.cloneNode(true);
        sanitize(copy);
        return copy.innerHTML;
    };

    const getBlockStyle = node => readStyle(node, BLOCK_PROPS);

    /**
     * @returns {boolean} true if the node only contains text, <br> and allow-listed formatting tags.
     */
    const isFormattedTextNode = function (node) {
        return Array.from(node.querySelectorAll('*')).every(c => c.nodeName === 'BR' || TAGS[c.nodeName] || c.nodeName === 'SPAN');
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
     * @param {{formatting: object, onLeave: function, onCancel: function}} options
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
            return { node: node, element: element, range: range, whole: blockOnlyMode || !range || coversAll(range, node), api: api };
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
            let top = rect.top - bar.offsetHeight - 6;
            if (top < 4) top = rect.bottom + 6;
            const left = Math.max(4, Math.min(rect.left, window.innerWidth - bar.offsetWidth - 4));
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
                if (current && current.bar === bar && document.activeElement !== node && !bar.contains(document.activeElement)) {
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

        document.addEventListener('selectionchange', onSelectionChange);
        node.addEventListener('keydown', onKeyDown);
        bar.addEventListener('focusout', onBarFocusOut);
        bar.addEventListener('keydown', onBarKeyDown);
        window.addEventListener('scroll', position, true);
        window.addEventListener('resize', position);

        current = {
            bar: bar,
            dispose: function () {
                document.removeEventListener('selectionchange', onSelectionChange);
                node.removeEventListener('keydown', onKeyDown);
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

    return {
        registerCommand: registerCommand,
        commands: () => commands.slice(),
        sanitize: sanitize,
        serialize: serialize,
        getBlockStyle: getBlockStyle,
        isFormattedTextNode: isFormattedTextNode,
        api: api,
        toolbar: {
            attach: attach,
            detach: detach,
            contains: target => !!(current && target instanceof Node && current.bar.contains(target))
        }
    };
})();
