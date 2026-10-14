/*
 * PDF report editing mode - reusable editing of html tables in draggables (static tables, later dynamic tables).
 *
 *   pdfEditTable.register({
 *       selector: 'table.pdf-table-static',   // tables handled by this registration
 *       actionPrefix: 'table.static',         // source actions <prefix>.get | .set | .widths | .cell (DataLinq Code
 *                                             // editor/pdf-editing/table-source.js)
 *       label: 'Table'
 *   });
 *
 *   pdfEditTable.createTable({ className, width, rowHeight, head: [[text]], body: [[text]] })  // preview DOM
 *
 * Editing: double click a cell = rich text (text-format.js toolbar), drag a column border = column widths,
 * drag the outer right border = table width (columns scale proportionally, Ctrl = snap to guides),
 * context menu "Table properties..." = modal (columns, header rows, body rows, row height, width, border).
 *
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.pdfEditTable = (function () {
    const registrations = [];
    const HANDLE = 5;          // px around a column border
    const MIN_COLUMN = 16;     // px
    const MAX_HTML_LENGTH = 20000;
    const BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    const LENGTH_UNITS = ['px', 'mm'];
    const toHex = function (color) {
        const m = /^rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(color || '');
        if (!m) return /^#[0-9a-f]{6}$/i.test(color || '') ? color : '';
        return '#' + m.slice(1, 4).map(n => (+n).toString(16).padStart(2, '0')).join('');
    };
    const roundLength = v => Math.round((Number(v) || 0) * 10) / 10;

    const bridge = () => window.dataLinqEditBridge;
    const connected = () => !!(bridge() && bridge().isConnected());

    const registrationOf = table => table ? registrations.find(r => table.matches(r.selector)) || null : null;

    const elementArgs = element => ({
        index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
        originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
        originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
    });

    const editableElement = function (table) {
        const element = table ? table.closest('.page .element') : null;
        if (!element || element.classList.contains('pdf-edit-pending')) return null;
        return Number.isInteger(elementArgs(element).index) ? element : null;
    };

    // table + registration + draggable of a cell, null if not editable
    const contextOf = function (target) {
        const cell = target instanceof Element ? target.closest('th, td') : null;
        const table = cell ? cell.closest('table') : null;
        const registration = registrationOf(table);
        const element = registration ? editableElement(table) : null;
        if (!element || cell.closest('.pdf-editing-more-rows')) return null;
        return { cell, table, registration, element };
    };

    const cellAddress = function (cell) {
        const row = cell.parentElement;
        const section = row.parentElement;
        const rows = Array.from(section.rows).filter(r => !r.classList.contains('pdf-editing-more-rows'));
        return {
            section: section.tagName === 'THEAD' ? 'head' : 'body',
            row: rows.indexOf(row),
            col: Array.from(row.cells).indexOf(cell)
        };
    };

    const createTable = function (o) {
        const table = document.createElement('table');
        table.className = o.className || '';
        if (o.width) table.style.width = o.width + 'px';
        if (o.rowHeight) table.setAttribute('data-row-height', o.rowHeight);
        if (o.border) table.style.setProperty('--dl-table-border', `${o.border.width}px ${o.border.style} ${o.border.color}`);

        const columns = Math.max(1, ...(o.head || []).concat(o.body || []).map(r => r.length));
        const colgroup = document.createElement('colgroup');
        for (let i = 0; i < columns; i++) {
            const col = document.createElement('col');
            col.style.width = (100 / columns) + '%';
            colgroup.appendChild(col);
        }
        table.appendChild(colgroup);

        const section = function (tag, cellTag, rows) {
            if (!rows || !rows.length) return;
            const s = document.createElement(tag);
            rows.forEach(r => {
                const tr = document.createElement('tr');
                if (o.rowHeight) tr.style.height = o.rowHeight + 'px';
                for (let i = 0; i < columns; i++) {
                    const c = document.createElement(cellTag);
                    c.textContent = r[i] || '';
                    tr.appendChild(c);
                }
                s.appendChild(tr);
            });
            table.appendChild(s);
        };
        section('thead', 'th', o.head);
        section('tbody', 'td', o.body);
        return table;
    };

    // ---- cell text (rich text) --------------------------------------------------------------

    const editCell = function (ctx) {
        const { cell, element, registration } = ctx;
        if (cell.isContentEditable || !window.pdfEditTextFormat || !window.pdfEditActions) return;

        const original = cell.innerHTML;
        const originalBackground = cell.style.backgroundColor;
        let finished = false;

        cell.setAttribute('contenteditable', 'true');
        cell.setAttribute('spellcheck', 'false');
        element.classList.add('pdf-edit-text-editing');
        cell.focus();

        const range = document.createRange();
        range.selectNodeContents(cell);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);

        const finish = function (commit) {
            if (finished) return;
            finished = true;
            pdfEditTextFormat.toolbar.detach();
            cell.removeEventListener('keydown', onKeyDown);
            cell.removeEventListener('blur', onBlur);
            cell.removeAttribute('contenteditable');
            cell.removeAttribute('spellcheck');
            element.classList.remove('pdf-edit-text-editing');

            const restore = function () {
                cell.innerHTML = original;
                cell.style.backgroundColor = originalBackground;
                if (!cell.getAttribute('style')) cell.removeAttribute('style');
            };
            if (!commit) {
                restore();
                return;
            }
            pdfEditTextFormat.convertTypedVariables(cell);
            pdfEditTextFormat.sanitize(cell);
            if (cell.innerHTML === original && cell.style.backgroundColor === originalBackground) return;
            if (cell.innerHTML.length > MAX_HTML_LENGTH) {
                restore();
                pdfEditActions.notify('The formatted text is too long.');
                return;
            }

            const args = Object.assign(elementArgs(element), cellAddress(cell), { html: cell.innerHTML, background: toHex(cell.style.backgroundColor) });
            pdfEditActions.request(registration.actionPrefix + '.cell', args).then(result => {
                if (!result.success) {
                    restore();
                    pdfEditActions.notify(result.message || 'The cell could not be changed.');
                }
            });
        };

        const onKeyDown = function (e) {
            if (e.key === 'Enter' && e.shiftKey) {
                e.preventDefault();
                document.execCommand('insertLineBreak');
            } else if (e.key === 'Enter') {
                e.preventDefault();
                cell.blur();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                cell.removeEventListener('blur', onBlur);
                finish(false);
            }
        };
        const onBlur = function (e) {
            if (pdfEditTextFormat.toolbar.contains(e.relatedTarget) || pdfEditTextFormat.isBusy()) return;
            finish(true);
        };

        cell.addEventListener('keydown', onKeyDown);
        cell.addEventListener('blur', onBlur);
        pdfEditTextFormat.toolbar.attach(cell, element, {
            formatting: { inline: true, block: false, cell: true },
            onLeave: () => finish(true),
            onCancel: () => finish(false)
        });
    };

    // capture phase: runs before the generic element double click (add-elements.js)
    document.addEventListener('dblclick', function (e) {
        const ctx = connected() ? contextOf(e.target) : null;
        if (!ctx) return;
        e.preventDefault();
        e.stopPropagation();
        editCell(ctx);
    }, true);

    // ---- column widths (drag column borders) ------------------------------------------------

    // column index left of the border at clientX, -1 if not on a border;
    // the last column index = the outer right border (resizes the whole table)
    const borderAt = function (ctx, clientX) {
        const row = ctx.cell.parentElement;
        const index = Array.from(row.cells).indexOf(ctx.cell);
        const rect = ctx.cell.getBoundingClientRect();
        let col = -1;
        if (clientX >= rect.right - HANDLE) col = index;
        else if (clientX <= rect.left + HANDLE) col = index - 1;
        const cols = ctx.table.querySelectorAll(':scope > colgroup > col');
        return col >= 0 && col < cols.length ? col : -1;
    };

    let resizing = null;

    const startResize = function (ctx, col, clientX) {
        const cols = Array.from(ctx.table.querySelectorAll(':scope > colgroup > col'));
        const firstRow = Array.from(ctx.table.rows).find(r => !r.classList.contains('pdf-editing-more-rows'));
        if (!firstRow || firstRow.cells.length !== cols.length) return false;

        const rect = ctx.table.getBoundingClientRect();
        const scale = ctx.table.offsetWidth > 0 ? rect.width / ctx.table.offsetWidth : 1;
        const widths = Array.from(firstRow.cells).map(c => c.offsetWidth);
        const total = widths.reduce((s, w) => s + w, 0) || 1;

        resizing = { ctx, cols, col, scale, widths, total, startX: clientX, current: widths.map(w => w / total * 100) };
        if (col === cols.length - 1) {
            // outer right border -> whole table width (columns keep their % = scale proportionally)
            const page = ctx.element.closest('.page');
            const pageRect = page ? page.getBoundingClientRect() : rect;
            resizing.table = {
                page: page,
                width0: ctx.table.offsetWidth,
                left: (rect.left - pageRect.left) / scale,
                width: ctx.table.offsetWidth,
                originalStyle: ctx.table.style.width
            };
        }
        document.body.classList.add('pdf-edit-col-resizing');
        return true;
    };

    const onTableResizeMove = function (e) {
        const r = resizing, t = r.table;
        let width = Math.max(MIN_COLUMN * r.cols.length, t.width0 + (e.clientX - r.startX) / r.scale);
        let snapped = false;
        if (e.ctrlKey && t.page && typeof snapEdgeToGuides === 'function') {
            const s = snapEdgeToGuides(t.page, 'x', t.left + width);
            if (s.snapped) {
                width = Math.max(MIN_COLUMN * r.cols.length, s.value - t.left);
                snapped = true;
            }
        }
        if (t.page) width = Math.min(width, t.page.offsetWidth - t.left);
        t.width = roundLength(width);
        r.ctx.table.style.width = t.width + 'px';
        r.ctx.element.classList.toggle('element-snapped', snapped);
    };

    const onTableResizeEnd = async function (r) {
        const t = r.table;
        r.ctx.element.classList.remove('element-snapped');
        if (Math.abs(t.width - t.width0) < 0.5) {
            r.ctx.table.style.width = t.originalStyle;
            return;
        }

        const args = elementArgs(r.ctx.element);
        const current = await pdfEditActions.request(r.ctx.registration.actionPrefix + '.get', args);
        if (!current.success || !current.data) {
            r.ctx.table.style.width = t.originalStyle;
            pdfEditActions.notify(current.message || 'The table could not be read.');
            return;
        }
        const o = current.data;
        const unit = o.widthUnit === 'mm' ? 'mm' : 'px';
        const pxPerMm = typeof mmToPx === 'function' ? mmToPx(1) : 96 / 25.4;
        const width = unit === 'mm' ? roundLength(t.width / pxPerMm) : roundLength(t.width);

        const result = await pdfEditActions.request(r.ctx.registration.actionPrefix + '.set', Object.assign(args, {
            table: {
                columns: o.columns,
                headRows: o.headRows,
                bodyRows: o.bodyRows,
                rowHeight: o.rowHeight || 0,
                rowHeightUnit: o.rowHeightUnit || 'px',
                width: width,
                widthUnit: unit,
                border: o.border || null
            }
        }));
        if (!result.success) {
            r.ctx.table.style.width = t.originalStyle;
            pdfEditActions.notify(result.message || 'The table width could not be changed.');
            return;
        }

        const page = pdfEditActions.pageOf(r.ctx.element.closest('.page'));
        pdfEditActions.saveAndReload(page ? page.sourceIndex : undefined);
    };

    const onResizeMove = function (e) {
        if (!resizing) return;
        const r = resizing;
        if (r.table) {
            onTableResizeMove(e);
            return;
        }
        const pair = r.widths[r.col] + r.widths[r.col + 1];
        const left = Math.min(pair - MIN_COLUMN, Math.max(MIN_COLUMN, r.widths[r.col] + (e.clientX - r.startX) / r.scale));
        r.current[r.col] = left / r.total * 100;
        r.current[r.col + 1] = (pair - left) / r.total * 100;
        r.cols[r.col].style.width = r.current[r.col] + '%';
        r.cols[r.col + 1].style.width = r.current[r.col + 1] + '%';
    };

    const onResizeEnd = function () {
        if (!resizing) return;
        const r = resizing;
        resizing = null;
        document.body.classList.remove('pdf-edit-col-resizing');

        if (r.table) {
            onTableResizeEnd(r);
            return;
        }

        const widths = r.current.map(w => Math.round(w * 100) / 100);
        pdfEditActions.request(r.ctx.registration.actionPrefix + '.widths', Object.assign(elementArgs(r.ctx.element), { widths: widths }))
            .then(result => {
                if (!result.success) pdfEditActions.notify(result.message || 'The column widths could not be changed.');
            });
    };

    // window capture phase: before the element dragging handlers
    const onPointerDown = function (e) {
        if (resizing || e.button !== 0 || !connected()) return;
        const ctx = contextOf(e.target);
        if (!ctx || ctx.cell.isContentEditable) return;
        const col = borderAt(ctx, e.clientX);
        if (col < 0 || !startResize(ctx, col, e.clientX)) return;
        e.preventDefault();
        e.stopImmediatePropagation();
    };
    window.addEventListener('pointerdown', onPointerDown, true);
    window.addEventListener('mousedown', function (e) {
        // pointerdown started a resize -> keep the mousedown away from the dragging handlers
        if (resizing) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);
    window.addEventListener('pointermove', function (e) {
        if (resizing) {
            onResizeMove(e);
            return;
        }
        document.querySelectorAll('.pdf-edit-col-resize').forEach(c => c.classList.remove('pdf-edit-col-resize'));
        const ctx = connected() ? contextOf(e.target) : null;
        if (ctx && !ctx.cell.isContentEditable && borderAt(ctx, e.clientX) >= 0) ctx.cell.classList.add('pdf-edit-col-resize');
    }, true);
    window.addEventListener('pointerup', onResizeEnd, true);
    window.addEventListener('blur', onResizeEnd);

    // ---- table properties (modal) -----------------------------------------------------------

    const tableOf = element => {
        const table = element.querySelector('table');
        return registrationOf(table) ? table : null;
    };

    const editProperties = async function (element) {
        const table = tableOf(element);
        const registration = registrationOf(table);
        if (!registration) return;

        const args = elementArgs(element);
        const current = await pdfEditActions.request(registration.actionPrefix + '.get', args);
        if (!current.success || !current.data) {
            pdfEditActions.notify(current.message || 'The table could not be read.');
            return;
        }
        const o = current.data;
        const border = o.border || { width: 1, style: 'solid', color: '#000000' };

        const values = await pdfEditDialog.open({
            title: (registration.label || 'Table') + ' properties',
            submitLabel: 'Apply',
            steps: [
                {
                    title: 'Structure',
                    fields: [
                        { name: 'columns', label: 'Columns', type: 'number', value: o.columns, min: 1, max: 30, step: 1 },
                        { name: 'headRows', label: 'Header rows', type: 'number', value: o.headRows, min: 0, max: 10, step: 1 },
                        { name: 'bodyRows', label: 'Rows', type: 'number', value: o.bodyRows, min: 1, max: 200, step: 1 }
                    ]
                },
                {
                    title: 'Size',
                    fields: [
                        { name: 'rowHeight', label: 'Row height (0 = auto)', type: 'number', value: o.rowHeight || 0, min: 0, max: 1000, step: 'any' },
                        { name: 'rowHeightUnit', label: 'Row height unit', type: 'select', value: o.rowHeightUnit || 'px', options: LENGTH_UNITS },
                        { name: 'width', label: 'Table width (0 = auto)', type: 'number', value: o.width || 0, min: 0, max: 3000, step: 'any' },
                        { name: 'widthUnit', label: 'Table width unit', type: 'select', value: o.widthUnit || 'px', options: LENGTH_UNITS }
                    ]
                },
                {
                    title: 'Border',
                    fields: [
                        { name: 'useBorder', label: 'Custom border', type: 'checkbox', value: !!o.border },
                        { name: 'borderWidth', label: 'Width (px)', type: 'number', value: border.width, min: 0, max: 20, step: 1, showIf: v => v.useBorder },
                        { name: 'borderStyle', label: 'Style', type: 'select', value: border.style, options: BORDER_STYLES, showIf: v => v.useBorder },
                        { name: 'borderColor', label: 'Color', type: 'color', value: border.color, showIf: v => v.useBorder }
                    ]
                }
            ]
        });
        if (!values) return;

        const removing = values.columns < o.columns || values.headRows < o.headRows || values.bodyRows < o.bodyRows;
        if (removing && !await pdfEditDialog.confirm({
            title: 'Remove rows/columns',
            message: 'Removed rows/columns and their content are deleted from the source. Continue?',
            confirmLabel: 'Remove'
        })) return;

        const result = await pdfEditActions.request(registration.actionPrefix + '.set', Object.assign(args, {
            table: {
                columns: Math.round(values.columns),
                headRows: Math.round(values.headRows),
                bodyRows: Math.round(values.bodyRows),
                rowHeight: roundLength(values.rowHeight),
                rowHeightUnit: values.rowHeightUnit,
                width: roundLength(values.width),
                widthUnit: values.widthUnit,
                border: values.useBorder
                    ? { width: Math.round(values.borderWidth), style: values.borderStyle, color: values.borderColor }
                    : null
            }
        }));
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The table could not be changed.');
            return;
        }

        const page = pdfEditActions.pageOf(element.closest('.page'));
        pdfEditActions.saveAndReload(page ? page.sourceIndex : undefined);
    };

    pdfEditContextMenu.register({
        id: 'table-properties',
        label: 'Table properties...',
        icon: '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
            '<rect x="3" y="4" width="18" height="16" rx="1"/><path d="M3 10h18"/><path d="M12 10v10"/></svg>',
        when: element => connected() && !!editableElement(tableOf(element)),
        run: editProperties
    });

    return {
        register: registration => registrations.push(registration),
        createTable: createTable,
        cellAddress: cellAddress
    };
})();
