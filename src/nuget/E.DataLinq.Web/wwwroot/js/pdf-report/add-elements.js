/*
 * PDF report editing mode - add elements, edit text, element context menu.
 *
 * Element types (paragraph, later lists, ...) and context menu actions are table driven:
 *
 *   pdfEditElementTypes.register({
 *       type: 'paragraph',                         // also the source template id in DataLinq Code
 *       label: 'Paragraph',
 *       icon: '<svg ...>',
 *       defaultText: 'New paragraph',
 *       create: function (text) { return htmlElement; },   // content of the new .element
 *       textTarget: function (element) { return node|null; } // editable text node of an existing element
 *       formatting: { inline: true, block: true }            // optional, rich text toolbar (see text-format.js)
 *   });
 *
 *   pdfEditContextMenu.register({
 *       id: 'my-action',
 *       label: 'My action',
 *       target: 'element',                         // optional, 'element' (default) | 'page' (empty page area) | 'report' (.main outside of pages)
 *       when: function (element) { return true; },  // optional, element = .element or .page
 *       run: function (element) { ... }
 *   });
 *
 * New elements are inserted into the view source by the DataLinq Code editor
 * (element.add -> element.added / element.add-failed); text edits are sent as element.text
 * (with formatting: additionally html (allow-listed markup), style (block styles) and format: 1).
 * The preview only sends the type id and plain text; the IDE builds the Razor source.
 *
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.pdfEditElementTypes = (function () {
    const types = [];

    return {
        register: function (definition) {
            types.push(definition);
        },
        all: function () {
            return types.slice();
        },
        get: function (type) {
            return types.find(t => t.type === type) || null;
        },
        /**
         * @param {HTMLElement} element A draggable (.element).
         * @returns {{type: object, node: HTMLElement}|null} The editable text of the element.
         */
        findText: function (element) {
            for (const t of types) {
                const node = typeof t.textTarget === 'function' ? t.textTarget(element) : null;
                if (node) return { type: t, node: node };
            }
            return null;
        }
    };
})();

window.pdfEditContextMenu = (function () {
    const actions = [];
    let menu = null;

    const close = function () {
        if (menu) {
            menu.remove();
            menu = null;
        }
    };

    const open = function (element, clientX, clientY, target) {
        close();

        const available = actions.filter(a => (a.target || 'element') === target &&
            (typeof a.when !== 'function' || a.when(element)));
        if (available.length === 0) return false;

        menu = document.createElement('div');
        menu.className = 'pdf-edit-context-menu';

        available.forEach(action => {
            const item = document.createElement('button');
            item.type = 'button';
            item.innerHTML = (action.icon || '') + '<span></span>';
            item.querySelector('span').textContent = action.label;
            item.addEventListener('click', () => {
                close();
                action.run(element);
            });
            menu.appendChild(item);
        });

        document.body.appendChild(menu);

        const rect = menu.getBoundingClientRect();
        menu.style.left = Math.min(clientX, window.innerWidth - rect.width - 4) + 'px';
        menu.style.top = Math.min(clientY, window.innerHeight - rect.height - 4) + 'px';
        return true;
    };

    document.addEventListener('contextmenu', function (e) {
        const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
        const page = !element && e.target instanceof Element ? e.target.closest('.page') : null;
        const report = !element && !page && e.target instanceof Element
            ? (e.target.closest('.main') || (!document.querySelector('.main') && !e.target.closest('.pdf-editing-toolbar, .pdf-edit-dialog') ? document.body : null))
            : null;
        if (report) {
            if (open(report, e.clientX, e.clientY, 'report')) {
                e.preventDefault();
            }
            return;
        }
        if ((!element && !page) || (element && element.querySelector('[contenteditable="true"]'))) {
            close();
            return;
        }
        if (open(element || page, e.clientX, e.clientY, element ? 'element' : 'page')) {
            e.preventDefault();
        }
    });
    document.addEventListener('mousedown', function (e) {
        if (menu && !menu.contains(e.target)) close();
    }, true);
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') close();
    });
    window.addEventListener('blur', close);
    window.addEventListener('scroll', close, true);

    return {
        register: function (action) {
            actions.push(action);
        },
        close: close
    };
})();

(function () {
    const MAX_TEXT_LENGTH = 4000;
    const MAX_HTML_LENGTH = 20000;

    // ---- helpers --------------------------------------------------------------------------

    const bridge = window.dataLinqEditBridge;
    const pending = {};
    let requestCounter = 0;

    const round = v => Math.round(v * 100) / 100;

    const pageIndexOf = function (page) {
        // pages created by PageBreak/auto splitting have no own id -> use the preceding source page
        const pages = Array.from(document.querySelectorAll('.page'));
        for (let i = pages.indexOf(page); i >= 0; i--) {
            const value = parseInt(pages[i].getAttribute('data-dl-edit-page'), 10);
            if (Number.isInteger(value)) return value;
        }
        return -1;
    };

    const shiftIndices = function (fromIndex, except) {
        document.querySelectorAll('.element[data-dl-edit-index]').forEach(el => {
            if (el === except) return;
            const index = parseInt(el.getAttribute('data-dl-edit-index'), 10);
            if (index >= fromIndex) el.setAttribute('data-dl-edit-index', index + 1);
        });
    };

    const notify = function (message) {
        console.warn('PDF editing mode: ' + message);
        if (window.dataLinq && typeof dataLinq.toast === 'function') {
            dataLinq.toast(message);
        }
    };

    // ---- add element ------------------------------------------------------------------------

    let placing = null;   // element type waiting for a click on a page

    const endPlacing = function () {
        placing = null;
        document.body.classList.remove('pdf-edit-placing');
        document.querySelectorAll('.pdf-edit-add-type.active').forEach(b => b.classList.remove('active'));
    };

    const startPlacing = function (type, button) {
        if (!bridge || !bridge.isConnected()) {
            notify('Adding elements is only available in the DataLinq Code live preview.');
            return;
        }
        endPlacing();
        placing = type;
        document.body.classList.add('pdf-edit-placing');
        button.classList.add('active');
    };

    const addElement = function (type, page, clientX, clientY) {
        const sourcePage = pageIndexOf(page);
        if (sourcePage < 0) {
            notify('This page cannot be edited (no source page found).');
            return;
        }

        const pageRect = page.getBoundingClientRect();
        const scale = page.offsetWidth > 0 ? pageRect.width / page.offsetWidth : 1;
        const x = round(Math.max(0, (clientX - pageRect.left) / scale));
        const y = round(Math.max(0, (clientY - pageRect.top) / scale));
        const text = type.defaultText || '';

        const element = document.createElement('div');
        element.className = 'element pdf-edit-pending';
        element.style.transform = `translate(${x}px, ${y}px)`;
        element.setAttribute('data-x', x);
        element.setAttribute('data-y', y);
        element.setAttribute('data-dl-edit-ox', x);
        element.setAttribute('data-dl-edit-oy', y);
        element.appendChild(type.create(text));
        page.appendChild(element);

        if (typeof enablePdfElementDragging === 'function') {
            enablePdfElementDragging(element);
        }

        const requestId = 'add-' + (++requestCounter);
        pending[requestId] = element;
        bridge.send('element.add', { requestId: requestId, type: type.type, page: sourcePage, x: x, y: y, text: text });
    };

    document.addEventListener('mousedown', function (e) {
        if (!placing || e.button !== 0) return;
        if (e.target instanceof Element && e.target.closest('.pdf-editing-toolbar')) return;
        const page = e.target instanceof Element ? e.target.closest('.page') : null;
        const group = placing;
        endPlacing();
        if (!page) return;

        e.preventDefault();
        e.stopPropagation();
        // the picker is opened during this mousedown -> keep the "click outside closes" handler from closing it
        e.stopImmediatePropagation();
        if (group.types.length > 1) {
            openVariantPicker(group, page, e.clientX, e.clientY);
        } else {
            addElement(group.types[0], page, e.clientX, e.clientY);
        }
    }, true);

    // ---- variant picker (e.g. H1..H6), shown at the click position after placing ---------

    let picker = null;

    const closeVariantPicker = function () {
        if (picker) {
            picker.remove();
            picker = null;
        }
    };

    const openVariantPicker = function (group, page, clientX, clientY) {
        closeVariantPicker();

        picker = document.createElement('div');
        picker.className = 'pdf-edit-context-menu pdf-edit-variant-picker';

        const title = document.createElement('div');
        title.className = 'pdf-edit-variant-picker-title';
        title.textContent = group.label;
        picker.appendChild(title);

        group.types.forEach(type => {
            const item = document.createElement('button');
            item.type = 'button';
            if (typeof type.preview === 'function') {
                item.appendChild(type.preview());
            } else {
                item.textContent = type.variantLabel || type.label;
            }
            item.addEventListener('click', () => {
                closeVariantPicker();
                addElement(type, page, clientX, clientY);
            });
            picker.appendChild(item);
        });

        document.body.appendChild(picker);

        const rect = picker.getBoundingClientRect();
        picker.style.left = Math.min(clientX, window.innerWidth - rect.width - 4) + 'px';
        picker.style.top = Math.min(clientY, window.innerHeight - rect.height - 4) + 'px';
        picker.querySelector('button').focus();
    };

    document.addEventListener('mousedown', function (e) {
        if (picker && !picker.contains(e.target)) closeVariantPicker();
    }, true);
    document.addEventListener('keydown', function (e) {
        if (!picker) return;
        if (e.key === 'Escape') {
            closeVariantPicker();
        } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            const items = Array.from(picker.querySelectorAll('button'));
            const i = items.indexOf(document.activeElement);
            const next = e.key === 'ArrowDown' ? i + 1 : i - 1;
            items[(next + items.length) % items.length].focus();
        }
    });
    window.addEventListener('scroll', closeVariantPicker, true);

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && placing) endPlacing();
    });

    if (bridge) {
        bridge.register('element.added', function (payload) {
            const element = pending[payload.requestId];
            delete pending[payload.requestId];
            if (!element || !Number.isInteger(payload.index) || payload.index < 0) return;

            shiftIndices(payload.index, element);
            element.setAttribute('data-dl-edit-index', payload.index);
            element.classList.remove('pdf-edit-pending');
        });

        bridge.register('element.add-failed', function (payload) {
            const element = pending[payload.requestId];
            delete pending[payload.requestId];
            if (element) element.remove();
            notify(typeof payload.message === 'string' ? payload.message : 'The element could not be added to the source.');
        });
    }

    // ---- edit text ------------------------------------------------------------------------

    // Text with line breaks: <br> <-> '\n'.
    const readText = function (node) {
        let text = '';
        node.childNodes.forEach(n => {
            if (n.nodeName === 'BR') text += '\n';
            else if (n.nodeType === Node.ELEMENT_NODE && /^(DIV|P|LI)$/.test(n.nodeName)) text += '\n' + readText(n);
            else if (n.nodeType === Node.ELEMENT_NODE) text += readText(n);
            else text += n.textContent;
        });
        return text;
    };

    const normalizeText = text => text
        .split('\n')
        .map(line => line.replace(/\s+/g, ' ').trim())
        .join('\n')
        .replace(/^\n+|\n+$/g, '')
        .slice(0, MAX_TEXT_LENGTH);

    const writeText = function (node, text) {
        node.textContent = '';
        text.split('\n').forEach((line, i) => {
            if (i > 0) node.appendChild(document.createElement('br'));
            node.appendChild(document.createTextNode(line));
        });
    };

    const editText = function (element) {
        const found = pdfEditElementTypes.findText(element);
        if (!found || element.classList.contains('pdf-edit-pending') || found.node.isContentEditable) return;
        if (!(found.type.formatting && window.pdfEditTextFormat && window.pdfEditActions)) {
            startEditText(element, found, null);
            return;
        }
        if (element.classList.contains('pdf-edit-loading')) return;

        // placeholders (@DLH.QueryParameter, @var) are evaluated in the preview -> edit the source content
        element.classList.add('pdf-edit-loading');
        pdfEditActions.request('element.text-source', {
            index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
            originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
            originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
        }).then(result => {
            element.classList.remove('pdf-edit-loading');
            if (!result.success && /Razor code/.test(result.message)) {
                notify(result.message);
                return;
            }
            const data = result.success && result.data && typeof result.data.html === 'string' && result.data.tokens > 0 ? result.data : null;
            startEditText(element, found, data ? data.html : null);
        });
    };

    const startEditText = function (element, found, sourceHtml) {
        let node = found.node;
        const originalNode = node;
        if (node.isContentEditable || !node.isConnected) return;

        const formatting = found.type.formatting && window.pdfEditTextFormat ? found.type.formatting : null;
        const list = formatting && formatting.list ? pdfEditTextFormat.list : null;
        const formatKey = () => JSON.stringify({
            tag: node.nodeName,
            html: pdfEditTextFormat.serialize(node),
            style: pdfEditTextFormat.getBlockStyle(node)
        });

        const originalText = normalizeText(readText(node));
        const originalHtml = node.innerHTML;
        const originalStyle = node.getAttribute('style');
        if (sourceHtml !== null && formatting) {
            node.innerHTML = sourceHtml;
            pdfEditTextFormat.sanitize(node);
        }
        const originalFormat = formatting ? formatKey() : null;
        let finished = false;

        const restore = function () {
            if (node !== originalNode) {
                node.replaceWith(originalNode);
                node = originalNode;
            }
            node.innerHTML = originalHtml;
            if (originalStyle === null) node.removeAttribute('style');
            else node.setAttribute('style', originalStyle);
        };

        node.setAttribute('contenteditable', 'true');
        node.setAttribute('spellcheck', 'false');
        element.classList.add('pdf-edit-text-editing');
        node.focus();

        const range = document.createRange();
        range.selectNodeContents(node);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);

        const finish = function (commit) {
            if (finished) return;
            finished = true;
            if (formatting) pdfEditTextFormat.toolbar.detach();
            node.removeEventListener('keydown', onKeyDown);
            node.removeEventListener('blur', onBlur);
            node.removeEventListener('paste', onPaste);
            node.removeAttribute('contenteditable');
            node.removeAttribute('spellcheck');
            element.classList.remove('pdf-edit-text-editing');

            const text = normalizeText(readText(node));
            const payload = {
                index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
                originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
                originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0,
                text: text
            };

            if (formatting) {
                if (!commit || !text) {
                    restore();
                    return;
                }
                pdfEditTextFormat.convertTypedVariables(node);
                pdfEditTextFormat.sanitize(node);
                if (formatKey() === originalFormat) {
                    restore();
                    return;
                }
                payload.html = node.innerHTML;
                payload.style = pdfEditTextFormat.getBlockStyle(node);
                payload.format = 1;
                if (list) payload.listTag = node.nodeName.toLowerCase();
                if (payload.html.length > MAX_HTML_LENGTH) {
                    restore();
                    notify('The formatted text is too long.');
                    return;
                }
            } else {
                if (!commit || !text || text === originalText) {
                    writeText(node, originalText);
                    return;
                }
                writeText(node, text);
            }

            if (!Number.isInteger(payload.index)) return;
            bridge.send('element.text', payload);
        };

        const onListKeyDown = function (e) {
            const li = list.itemAt(node);
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (!li) {
                    node.blur();
                } else if (list.isEmptyItem(li) && list.depth(node, li) > 1) {
                    list.outdent(node, li);
                } else if (list.isEmptyItem(li) && list.isLastItem(node, li)) {
                    list.removeItem(node, li);
                    node.blur();
                } else {
                    list.splitItem(node, li);
                }
                return true;
            }
            if (e.key === 'Tab') {
                e.preventDefault();
                if (li) {
                    if (e.shiftKey) list.outdent(node, li); else list.indent(node, li);
                }
                return true;
            }
            if (e.key === 'Backspace' && li && list.isEmptyItem(li) && window.getSelection().isCollapsed) {
                e.preventDefault();
                list.removeItem(node, li);
                return true;
            }
            return false;
        };

        const onKeyDown = function (e) {
            if (list && onListKeyDown(e)) return;
            if (e.key === 'Enter' && e.shiftKey) {
                // keep the paragraph flat: <br> instead of nested <div>s
                e.preventDefault();
                document.execCommand('insertLineBreak');
            } else if (e.key === 'Enter') {
                e.preventDefault();
                node.blur();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                node.removeEventListener('blur', onBlur);
                finish(false);
            }
        };
        const onBlur = function (e) {
            // focus moved into the formatting toolbar (select, color picker) -> keep editing
            if (formatting && pdfEditTextFormat.toolbar.contains(e.relatedTarget)) return;
            // a toolbar dialog (insert query parameter) is open -> keep editing
            if (formatting && pdfEditTextFormat.isBusy()) return;
            finish(true);
        };

        // lists: paste as plain text (keeps the <li> structure intact)
        const onPaste = function (e) {
            if (!list) return;
            e.preventDefault();
            const text = (e.clipboardData ? e.clipboardData.getData('text/plain') : '').replace(/\s+/g, ' ');
            if (text) document.execCommand('insertText', false, text);
        };

        node.addEventListener('keydown', onKeyDown);
        node.addEventListener('blur', onBlur);
        node.addEventListener('paste', onPaste);

        const toolbarOptions = {
            formatting: formatting,
            onLeave: () => finish(true),
            onCancel: () => finish(false),
            // lists: switching the root ul <-> ol replaces the edited node
            replaceNode: function (newNode) {
                node.removeEventListener('keydown', onKeyDown);
                node.removeEventListener('blur', onBlur);
                node.removeEventListener('paste', onPaste);
                node.replaceWith(newNode);
                node = newNode;
                node.setAttribute('contenteditable', 'true');
                node.setAttribute('spellcheck', 'false');
                node.addEventListener('keydown', onKeyDown);
                node.addEventListener('blur', onBlur);
                node.addEventListener('paste', onPaste);
                node.focus({ preventScroll: true });
                pdfEditTextFormat.toolbar.attach(node, element, toolbarOptions);
            }
        };

        if (formatting) {
            pdfEditTextFormat.toolbar.attach(node, element, toolbarOptions);
        }
    };

    // editing mode: show placeholders (@DLH.QueryParameter, @var) as chips instead of their values
    const showPlaceholders = function () {
        if (!window.pdfEditTextFormat || !window.pdfEditActions || !bridge || !bridge.isConnected()) return;
        // the source call is located by the original coordinates -> make sure they are recorded
        if (typeof bridge.snapshotOriginals === 'function') bridge.snapshotOriginals();
        const targets = [];
        document.querySelectorAll('.page .element[data-dl-edit-index]').forEach(element => {
            if (element.hasAttribute('data-dl-edit-placeholders')) return;
            const found = pdfEditElementTypes.findText(element);
            if (found && found.type.formatting) targets.push({ element: element, node: found.node });
        });
        for (let i = 0; i < targets.length; i += 100) {
            const batch = targets.slice(i, i + 100);
            pdfEditActions.request('elements.text-sources', {
                elements: batch.map(t => ({
                    index: parseInt(t.element.getAttribute('data-dl-edit-index'), 10),
                    originalX: parseFloat(t.element.getAttribute('data-dl-edit-ox')) || 0,
                    originalY: parseFloat(t.element.getAttribute('data-dl-edit-oy')) || 0
                }))
            }).then(result => {
                if (!result.success || !result.data || !Array.isArray(result.data.items)) return;
                result.data.items.forEach(item => {
                    const t = batch.find(b => parseInt(b.element.getAttribute('data-dl-edit-index'), 10) === item.index);
                    if (!t || typeof item.html !== 'string' || t.node.isContentEditable || !t.node.isConnected) return;
                    t.node.innerHTML = item.html;
                    pdfEditTextFormat.sanitize(t.node);
                    t.element.setAttribute('data-dl-edit-placeholders', '1');
                });
            });
        }

        // static tables: placeholders in cells (cells matched by order, only if the cell count matches the source)
        const tables = Array.from(document.querySelectorAll('.page .element[data-dl-edit-index]'))
            .filter(el => !el.hasAttribute('data-dl-edit-table-placeholders') && el.querySelector('table'));
        for (let i = 0; i < tables.length; i += 100) {
            const batch = tables.slice(i, i + 100);
            pdfEditActions.request('elements.table-sources', {
                elements: batch.map(el => ({
                    index: parseInt(el.getAttribute('data-dl-edit-index'), 10),
                    originalX: parseFloat(el.getAttribute('data-dl-edit-ox')) || 0,
                    originalY: parseFloat(el.getAttribute('data-dl-edit-oy')) || 0
                }))
            }).then(result => {
                if (!result.success || !result.data || !Array.isArray(result.data.items)) return;
                result.data.items.forEach(item => {
                    const el = batch.find(b => parseInt(b.getAttribute('data-dl-edit-index'), 10) === item.index);
                    if (!el || !Array.isArray(item.cells)) return;
                    const cells = Array.from(el.querySelectorAll('table th, table td'))
                        .filter(c => !c.closest('.pdf-editing-more-rows'));
                    if (cells.length !== item.cellCount) return;
                    item.cells.forEach(c => {
                        const cell = cells[c.cell];
                        if (!cell || typeof c.html !== 'string') return;
                        cell.innerHTML = c.html;
                        pdfEditTextFormat.sanitize(cell);
                    });
                    el.setAttribute('data-dl-edit-table-placeholders', '1');
                });
            });
        }
    };
    // run when the bridge is connected and whenever the report content (pages) has been loaded
    if (bridge && bridge.isConnected()) showPlaceholders();
    document.addEventListener('datalinq-edit:connected', () => setTimeout(showPlaceholders, 0));
    if (window.dataLinq && dataLinq.events) dataLinq.events.on('onpageloaded', () => setTimeout(showPlaceholders, 0));

    document.addEventListener('dblclick', function (e) {
        const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
        if (element && bridge && bridge.isConnected()) {
            e.preventDefault();
            editText(element);
        }
    });

    pdfEditContextMenu.register({
        id: 'edit-text',
        label: 'Edit text',
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>',
        when: element => !!(bridge && bridge.isConnected() && !element.classList.contains('pdf-edit-pending') &&
            pdfEditElementTypes.findText(element)),
        run: editText
    });

    // ---- delete element -------------------------------------------------------------------

    const deleteElement = function (element) {
        const index = parseInt(element.getAttribute('data-dl-edit-index'), 10);
        if (!Number.isInteger(index)) return;

        bridge.send('element.delete', {
            index: index,
            originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
            originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
        });

        element.remove();
        document.querySelectorAll('.element[data-dl-edit-index]').forEach(el => {
            const i = parseInt(el.getAttribute('data-dl-edit-index'), 10);
            if (i > index) el.setAttribute('data-dl-edit-index', i - 1);
        });
    };

    pdfEditContextMenu.register({
        id: 'delete-element',
        label: 'Delete element',
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/></svg>',
        when: element => !!(bridge && bridge.isConnected() && !element.classList.contains('pdf-edit-pending') &&
            Number.isInteger(parseInt(element.getAttribute('data-dl-edit-index'), 10))),
        run: deleteElement
    });

    // ---- toolbar control --------------------------------------------------------------------

    /**
     * Creates the toolbar buttons for adding elements (one per registered element type).
     * Called by initializeEditingToolbar (editing.js).
     * @returns {HTMLElement} The control.
     */
    window.createAddElementsControl = function () {
        const container = document.createElement('div');
        container.className = 'pdf-edit-add-elements';

        // types with the same "group" (e.g. heading h1..h6) share one button; the variant is
        // chosen from a small picker after clicking on the page
        const groups = [];
        pdfEditElementTypes.all().forEach(type => {
            const key = type.group || type.type;
            let group = groups.find(g => g.key === key);
            if (!group) {
                group = { key: key, label: type.groupLabel || type.label, types: [] };
                groups.push(group);
            }
            group.types.push(type);
        });

        groups.forEach(group => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'pdf-editing-guides pdf-edit-add-type';
            button.title = 'Add ' + group.label.toLowerCase() + ' (click on a page to place it, Esc to cancel)';
            button.innerHTML = group.types[0].icon + '<span></span>';
            button.querySelector('span').textContent = group.label;
            button.addEventListener('click', e => {
                e.stopPropagation();
                if (placing === group) {
                    endPlacing();
                } else {
                    startPlacing(group, button);
                }
            });
            container.appendChild(button);
        });

        return container;
    };
})();
