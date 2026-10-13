/*
 * PDF report editing mode - reusable modal dialog (wizard with one or more steps).
 *
 *   pdfEditDialog.open({
 *       title: 'New page',
 *       submitLabel: 'Insert',
 *       steps: [
 *           {
 *               title: 'Position',
 *               fields: [
 *                   { name: 'position', label: 'Insert', type: 'choice', value: 'after',
 *                     options: [{ value: 'before', label: 'Before page 2' }, { value: 'after', label: 'After page 2' }] }
 *               ]
 *           },
 *           {
 *               title: 'Options',
 *               fields: [
 *                   { name: 'landscape', label: 'Landscape', type: 'checkbox', value: false },
 *                   { name: 'size', label: 'Paper size', type: 'select', value: 'A4', options: ['A4', 'A3'] },
 *                   { name: 'margin', label: 'Margin', type: 'number', value: 10, min: 0, step: 0.5,
 *                     showIf: values => values.landscape },
 *                   { name: 'id', label: 'Id', type: 'text', value: '', placeholder: '...', pattern: /^\w*$/ }
 *               ]
 *           }
 *       ]
 *   }).then(values => { if (values) ... });   // null if cancelled
 *
 * Field types: choice (radio cards), select, checkbox, number, text, color, heading (section title),
 * keyvalue (list of { key, value } rows, optional keyPattern).
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.pdfEditDialog = (function () {
    let current = null;

    const normalizeOptions = options => (options || []).map(o => typeof o === 'object' ? o : { value: o, label: String(o) });

    const readValue = function (field, input) {
        switch (field.type) {
            case 'checkbox': return input.checked;
            case 'number': {
                const n = parseFloat(input.value);
                return Number.isFinite(n) ? n : field.value;
            }
            case 'choice': {
                const checked = input.querySelector('input:checked');
                return checked ? checked.value : field.value;
            }
            case 'keyvalue':
                return Array.from(input.querySelectorAll('.pdf-edit-dialog-kv-row'))
                    .map(r => ({ key: r.querySelector('.kv-key').value.trim(), value: r.querySelector('.kv-value').value }))
                    .filter(p => p.key !== '' || p.value !== '');
            default: return input.value;
        }
    };

    const createInput = function (field, id) {
        if (field.type === 'choice') {
            const group = document.createElement('div');
            group.className = 'pdf-edit-dialog-choices';
            group.setAttribute('role', 'radiogroup');
            normalizeOptions(field.options).forEach((o, i) => {
                const label = document.createElement('label');
                label.className = 'pdf-edit-dialog-choice';
                const radio = document.createElement('input');
                radio.type = 'radio';
                radio.name = id;
                radio.value = o.value;
                radio.checked = o.value === field.value || (field.value === undefined && i === 0);
                label.appendChild(radio);
                const text = document.createElement('span');
                text.textContent = o.label;
                label.appendChild(text);
                if (o.description) {
                    const small = document.createElement('small');
                    small.textContent = o.description;
                    label.appendChild(small);
                }
                group.appendChild(label);
            });
            return group;
        }

        if (field.type === 'select') {
            const select = document.createElement('select');
            select.id = id;
            normalizeOptions(field.options).forEach(o => {
                const option = document.createElement('option');
                option.value = o.value;
                option.textContent = o.label;
                option.selected = o.value === field.value;
                select.appendChild(option);
            });
            return select;
        }

        if (field.type === 'keyvalue') {
            const list = document.createElement('div');
            list.id = id;
            list.className = 'pdf-edit-dialog-kv';
            const changed = () => list.dispatchEvent(new Event('change', { bubbles: true }));
            const createText = function (className, placeholder, value) {
                const input = document.createElement('input');
                input.type = 'text';
                input.className = className;
                input.placeholder = placeholder;
                input.spellcheck = false;
                input.autocomplete = 'off';
                input.value = value ?? '';
                return input;
            };
            const addRow = function (pair) {
                const row = document.createElement('div');
                row.className = 'pdf-edit-dialog-kv-row';
                const key = createText('kv-key', field.keyPlaceholder || 'Key', pair ? pair.key : '');
                const value = createText('kv-value', field.valuePlaceholder || 'Value', pair ? pair.value : '');
                const remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'pdf-edit-dialog-kv-remove';
                remove.title = 'Remove';
                remove.setAttribute('aria-label', 'Remove');
                remove.textContent = '\u00d7';
                remove.addEventListener('click', () => {
                    const next = row.nextElementSibling;
                    row.remove();
                    (next && next.classList.contains('pdf-edit-dialog-kv-row') ? next.querySelector('.kv-key') : add).focus();
                    changed();
                });
                // Enter: key -> value -> next/new row (instead of submitting the dialog, see onKeyDown)
                key.pdfEditOnEnter = () => value.focus();
                value.pdfEditOnEnter = () => {
                    const next = row.nextElementSibling;
                    if (next && next.classList.contains('pdf-edit-dialog-kv-row')) {
                        next.querySelector('.kv-key').focus();
                    } else {
                        addRow(null).focus();
                        changed();
                    }
                };
                row.append(key, value, remove);
                list.insertBefore(row, add);
                return key;
            };
            const empty = document.createElement('div');
            empty.className = 'pdf-edit-dialog-kv-empty';
            empty.textContent = field.emptyText || 'None';
            list.appendChild(empty);
            const add = document.createElement('button');
            add.type = 'button';
            add.className = 'pdf-edit-dialog-kv-add';
            add.textContent = '+ ' + (field.addLabel || 'Add');
            add.addEventListener('click', () => {
                addRow(null).focus();
                changed();
            });
            list.appendChild(add);
            (Array.isArray(field.value) ? field.value : []).forEach(addRow);
            return list;
        }

        const input = document.createElement('input');
        input.id = id;
        if (field.type === 'checkbox') {
            input.type = 'checkbox';
            input.checked = !!field.value;
        } else if (field.type === 'number') {
            input.type = 'number';
            input.value = field.value ?? '';
            if (field.min !== undefined) input.min = field.min;
            if (field.max !== undefined) input.max = field.max;
            input.step = field.step ?? 'any';
        } else if (field.type === 'color') {
            input.type = 'color';
            input.value = field.value || '#000000';
        } else {
            input.type = 'text';
            input.value = field.value ?? '';
            input.placeholder = field.placeholder || '';
            input.spellcheck = false;
        }
        return input;
    };

    const close = function (result) {
        if (!current) return;
        const { overlay, resolve, previousFocus } = current;
        current = null;
        overlay.remove();
        document.removeEventListener('keydown', onKeyDown, true);
        if (previousFocus && typeof previousFocus.focus === 'function') previousFocus.focus();
        resolve(result);
    };

    const onKeyDown = function (e) {
        if (!current) return;
        e.stopPropagation();   // keep editing shortcuts (Ctrl+Z, Esc, ...) away from the report
        if (e.key === 'Escape') {
            e.preventDefault();
            close(null);
        } else if (e.key === 'Enter' && typeof e.target.pdfEditOnEnter === 'function') {
            e.preventDefault();
            e.target.pdfEditOnEnter();
        } else if (e.key === 'Enter' && !(e.target instanceof HTMLButtonElement) && !(e.target instanceof HTMLSelectElement)) {
            e.preventDefault();
            current.next();
        }
    };

    /**
     * Opens the dialog.
     * @param {{title:string, submitLabel?:string, steps:Array<{title?:string, description?:string, fields:Array<object>}>}} definition
     * @returns {Promise<object|null>} The values by field name, or null if cancelled.
     */
    const open = function (definition) {
        close(null);

        return new Promise(resolve => {
            const steps = definition.steps || [];
            const fields = steps.flatMap(s => s.fields || []).filter(f => f.name);
            const values = {};
            fields.forEach(f => { values[f.name] = f.type === 'choice' && f.value === undefined ? normalizeOptions(f.options)[0]?.value : f.value; });

            const overlay = document.createElement('div');
            overlay.className = 'pdf-edit-dialog-overlay';
            overlay.addEventListener('mousedown', e => { if (e.target === overlay) close(null); });

            const dialog = document.createElement('div');
            dialog.className = 'pdf-edit-dialog';
            dialog.setAttribute('role', 'dialog');
            dialog.setAttribute('aria-modal', 'true');
            overlay.appendChild(dialog);

            const header = document.createElement('div');
            header.className = 'pdf-edit-dialog-header';
            const title = document.createElement('h2');
            title.textContent = definition.title || '';
            header.appendChild(title);
            const progress = document.createElement('span');
            progress.className = 'pdf-edit-dialog-progress';
            header.appendChild(progress);
            dialog.appendChild(header);

            const body = document.createElement('div');
            body.className = 'pdf-edit-dialog-body';
            dialog.appendChild(body);

            const error = document.createElement('div');
            error.className = 'pdf-edit-dialog-error';
            dialog.appendChild(error);

            const footer = document.createElement('div');
            footer.className = 'pdf-edit-dialog-footer';
            const cancel = document.createElement('button');
            cancel.type = 'button';
            cancel.textContent = 'Cancel';
            const back = document.createElement('button');
            back.type = 'button';
            back.textContent = 'Back';
            const next = document.createElement('button');
            next.type = 'button';
            next.className = 'primary';
            footer.append(cancel, back, next);
            dialog.appendChild(footer);

            let stepIndex = 0;
            let rows = [];   // { field, row, input }

            const collect = function () {
                rows.forEach(r => { if (r.input) values[r.field.name] = readValue(r.field, r.input); });
            };

            const updateVisibility = function () {
                collect();
                rows.forEach(r => {
                    r.row.hidden = typeof r.field.showIf === 'function' && !r.field.showIf(values);
                });
            };

            const validate = function () {
                for (const r of rows) {
                    if (r.row.hidden || !r.input) continue;
                    const v = values[r.field.name];
                    if (r.field.required && (v === '' || v === null || v === undefined)) return r.field.label + ' is required.';
                    if (r.field.pattern && typeof v === 'string' && !r.field.pattern.test(v)) return r.field.label + ': invalid value.';
                    if (r.field.type === 'keyvalue' && Array.isArray(v)) {
                        const bad = v.find(p => r.field.keyPattern ? !r.field.keyPattern.test(p.key) : p.key === '');
                        if (bad) return r.field.label + ': invalid key "' + bad.key + '".';
                    }
                    if (r.field.type === 'number' && ((r.field.min !== undefined && v < r.field.min) || (r.field.max !== undefined && v > r.field.max))) {
                        return r.field.label + ': out of range.';
                    }
                }
                return null;
            };

            const render = function () {
                const step = steps[stepIndex];
                body.textContent = '';
                error.textContent = '';
                rows = [];

                if (step.title) {
                    const h = document.createElement('h3');
                    h.textContent = step.title;
                    body.appendChild(h);
                }
                if (step.description) {
                    const p = document.createElement('p');
                    p.className = 'pdf-edit-dialog-description';
                    p.textContent = step.description;
                    body.appendChild(p);
                }

                (step.fields || []).forEach((field, i) => {
                    const row = document.createElement('div');
                    row.className = 'pdf-edit-dialog-row pdf-edit-dialog-' + field.type;

                    if (field.type === 'heading') {
                        row.textContent = field.label;
                        body.appendChild(row);
                        rows.push({ field: field, row: row, input: null });
                        return;
                    }

                    const id = 'pdf-edit-dialog-' + stepIndex + '-' + i;
                    const input = createInput(Object.assign({}, field, { value: values[field.name] }), id);
                    const label = document.createElement('label');
                    label.htmlFor = id;
                    label.textContent = field.label;

                    if (field.type === 'checkbox') {
                        row.append(input, label);
                    } else {
                        row.append(label, input);
                    }
                    input.addEventListener('input', updateVisibility);
                    input.addEventListener('change', updateVisibility);

                    body.appendChild(row);
                    rows.push({ field: field, row: row, input: input });
                });

                progress.textContent = steps.length > 1 ? (stepIndex + 1) + ' / ' + steps.length : '';
                back.hidden = stepIndex === 0;
                next.textContent = stepIndex === steps.length - 1 ? (definition.submitLabel || 'OK') : 'Next';
                updateVisibility();

                const first = body.querySelector('input:checked, input, select');
                if (first) first.focus();
            };

            const goNext = function () {
                collect();
                const message = validate();
                if (message) {
                    error.textContent = message;
                    return;
                }
                if (stepIndex < steps.length - 1) {
                    stepIndex++;
                    render();
                } else {
                    const result = {};
                    fields.forEach(f => {
                        const visible = typeof f.showIf !== 'function' || f.showIf(values);
                        if (visible) result[f.name] = values[f.name];
                    });
                    close(result);
                }
            };

            cancel.addEventListener('click', () => close(null));
            back.addEventListener('click', () => { collect(); stepIndex--; render(); });
            next.addEventListener('click', goNext);

            current = { overlay: overlay, resolve: resolve, previousFocus: document.activeElement, next: goNext };
            document.addEventListener('keydown', onKeyDown, true);
            document.body.appendChild(overlay);
            render();
        });
    };

    /**
     * Opens a confirmation in the dialog style.
     * @returns {Promise<boolean>}
     */
    const confirm = function (options) {
        return open({
            title: options.title || 'Confirm',
            submitLabel: options.confirmLabel || 'Continue',
            steps: [{ description: options.message || '', fields: [] }]
        }).then(r => r !== null);
    };

    return {
        open: open,
        confirm: confirm,
        close: () => close(null),
        isOpen: () => !!current
    };
})();
