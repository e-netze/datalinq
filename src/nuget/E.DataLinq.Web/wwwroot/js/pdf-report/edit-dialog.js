/*
 * PDF report editing mode - reusable modal dialog. Several steps are shown at once as accordion sections
 * (several may be open, the first one is open by default).
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
 * Field types: choice (radio cards), select, checkbox, number, text, textarea (rows), color, heading (section title),
 * keyvalue (list of { key, value } rows, optional keyPattern),
 * custom ({ create: value => HTMLElement, read: element => value }; dispatch 'change' on the element when it changes).
 * Any field may define validate: (value, values) => errorMessage|null.
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.pdfEditDialog = (function () {
    let current = null;

    // dark/light follows the DataLinq Code IDE (see css/datalinq/pdf-report/editor-theme.css)
    if (window.parent !== window) {
        const markEmbedded = () => document.body.classList.add('pdf-edit-embedded');
        if (document.body) markEmbedded(); else document.addEventListener('DOMContentLoaded', markEmbedded);
        // the IDE posts { theme: 'vs' | 'vs-dark' } to all iframes when the color scheme is toggled
        window.addEventListener('message', e => {
            if (e.source !== window.parent || !e.data || (e.data.theme !== 'vs' && e.data.theme !== 'vs-dark')) return;
            document.body.classList.toggle('colorscheme-light', e.data.theme === 'vs');
        });
    }

    const normalizeOptions = options => (options || []).map(o => typeof o === 'object' ? o : { value: o, label: String(o) });

    const readValue = function (field, input) {
        switch (field.type) {
            case 'custom': return typeof field.read === 'function' ? field.read(input) : field.value;
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
        if (field.type === 'custom') {
            const element = field.create(field.value);
            element.id = id;
            return element;
        }

        if (field.type === 'choice') {
            const group = document.createElement('div');
            group.className = 'pdf-edit-dialog-choices';
            group.setAttribute('role', 'radiogroup');
            const options = normalizeOptions(field.options);
            if (options.length && options.every(o => o.icon)) group.classList.add('pdf-edit-dialog-choices-icons');
            options.forEach((o, i) => {
                const label = document.createElement('label');
                label.className = 'pdf-edit-dialog-choice';
                const radio = document.createElement('input');
                radio.type = 'radio';
                radio.name = id;
                radio.value = o.value;
                radio.checked = o.value === field.value || (field.value === undefined && i === 0);
                label.appendChild(radio);
                if (o.icon) {
                    // icon only (trusted svg markup from the registering tool), the label becomes the tooltip
                    const icon = document.createElement('span');
                    icon.className = 'pdf-edit-dialog-choice-icon';
                    icon.innerHTML = o.icon;
                    label.appendChild(icon);
                    label.title = o.label;
                    radio.setAttribute('aria-label', o.label);
                    group.appendChild(label);
                    return;
                }
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

        if (field.type === 'textarea') {
            const area = document.createElement('textarea');
            area.id = id;
            area.rows = field.rows || 6;
            area.value = field.value ?? '';
            area.placeholder = field.placeholder || '';
            area.spellcheck = false;
            return area;
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
        } else if (e.key === 'Enter' && !(e.target instanceof HTMLButtonElement) && !(e.target instanceof HTMLSelectElement) && !(e.target instanceof HTMLTextAreaElement)) {
            e.preventDefault();
            current.next();
        } else if (e.key === 'Tab') {
            const focusable = Array.from(current.overlay.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])'))
                .filter(el => !el.disabled && el.offsetParent !== null);
            if (focusable.length === 0) return;
            const first = focusable[0], last = focusable[focusable.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
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
            title.id = 'pdf-edit-dialog-title';
            title.textContent = definition.title || '';
            header.appendChild(title);
            dialog.setAttribute('aria-labelledby', title.id);
            const closeButton = document.createElement('button');
            closeButton.type = 'button';
            closeButton.className = 'pdf-edit-dialog-close';
            closeButton.title = 'Close';
            closeButton.setAttribute('aria-label', 'Close');
            closeButton.innerHTML = '<svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true"><path d="M3.5 3.5l9 9m0-9l-9 9" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" fill="none"/></svg>';
            closeButton.addEventListener('click', () => close(null));
            header.appendChild(closeButton);
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
            const next = document.createElement('button');
            next.type = 'button';
            next.className = 'primary';
            next.textContent = definition.submitLabel || 'OK';
            footer.append(cancel, next);
            dialog.appendChild(footer);

            let rows = [];   // { field, row, input, section }
            let sections = [];   // { element, toggle, rows }

            const collect = function () {
                rows.forEach(r => { if (r.input) values[r.field.name] = readValue(r.field, r.input); });
            };

            const updateVisibility = function () {
                collect();
                rows.forEach(r => {
                    r.row.hidden = typeof r.field.showIf === 'function' && !r.field.showIf(values);
                });
                sections.forEach(s => {
                    const inputs = s.rows.filter(r => r.input);
                    s.element.hidden = inputs.length > 0 && inputs.every(r => r.row.hidden);
                });
            };

            const setExpanded = function (section, expanded) {
                section.element.classList.toggle('is-open', expanded);
                section.toggle.setAttribute('aria-expanded', String(expanded));
            };

            const validate = function () {
                for (const r of rows) {
                    const message = validateRow(r);
                    if (message) return { message: message, row: r };
                }
                return null;
            };

            const validateRow = function (r) {
                if (r.row.hidden || !r.input) return null;
                const v = values[r.field.name];
                if (r.field.required && (v === '' || v === null || v === undefined)) return r.field.label + ' is required.';
                if (r.field.pattern && typeof v === 'string' && !r.field.pattern.test(v)) return r.field.label + ': invalid value.';
                if (typeof r.field.validate === 'function') {
                    const message = r.field.validate(v, values);
                    if (message) return message;
                }
                if (r.field.type === 'keyvalue' && Array.isArray(v)) {
                    const bad = v.find(p => r.field.keyPattern ? !r.field.keyPattern.test(p.key) : p.key === '');
                    if (bad) return r.field.label + ': invalid key "' + bad.key + '".';
                }
                if (r.field.type === 'number' && ((r.field.min !== undefined && v < r.field.min) || (r.field.max !== undefined && v > r.field.max))) {
                    return r.field.label + ': out of range.';
                }
                return null;
            };

            const renderFields = function (container, step, stepIndex, section) {
                (step.fields || []).forEach((field, i) => {
                    const row = document.createElement('div');
                    row.className = 'pdf-edit-dialog-row pdf-edit-dialog-' + field.type;
                    if (field.wide || field.type === 'textarea' || field.type === 'keyvalue' || field.type === 'choice' || field.type === 'custom') {
                        row.classList.add('pdf-edit-dialog-row-wide');
                    }

                    if (field.type === 'heading') {
                        row.textContent = field.label;
                        container.appendChild(row);
                        const entry = { field: field, row: row, input: null, section: section };
                        rows.push(entry);
                        if (section) section.rows.push(entry);
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
                    if (field.help) {
                        const help = document.createElement('small');
                        help.className = 'pdf-edit-dialog-help';
                        help.textContent = field.help;
                        row.appendChild(help);
                    }
                    input.addEventListener('input', updateVisibility);
                    input.addEventListener('change', updateVisibility);
                    input.addEventListener('input', () => { row.classList.remove('has-error'); });

                    container.appendChild(row);
                    const entry = { field: field, row: row, input: input, section: section };
                    rows.push(entry);
                    if (section) section.rows.push(entry);
                });
            };

            const appendDescription = function (container, text) {
                const p = document.createElement('p');
                p.className = 'pdf-edit-dialog-description';
                p.textContent = text;
                container.appendChild(p);
            };

            const render = function () {
                body.textContent = '';
                error.textContent = '';
                rows = [];
                sections = [];

                if (steps.length <= 1) {
                    const step = steps[0] || { fields: [] };
                    if (step.title) {
                        const h = document.createElement('h3');
                        h.textContent = step.title;
                        body.appendChild(h);
                    }
                    if (step.description) appendDescription(body, step.description);
                    const grid = document.createElement('div');
                    grid.className = 'pdf-edit-dialog-grid';
                    body.appendChild(grid);
                    renderFields(grid, step, 0, null);
                } else {
                    // all steps at once, as accordion sections (several may be open, the first one is open)
                    steps.forEach((step, stepIndex) => {
                        const element = document.createElement('section');
                        element.className = 'pdf-edit-dialog-section';
                        const toggle = document.createElement('button');
                        toggle.type = 'button';
                        toggle.className = 'pdf-edit-dialog-section-toggle';
                        toggle.id = 'pdf-edit-dialog-section-' + stepIndex;
                        toggle.setAttribute('aria-controls', toggle.id + '-content');
                        const badge = document.createElement('span');
                        badge.className = 'pdf-edit-dialog-section-index';
                        badge.textContent = String(stepIndex + 1);
                        const label = document.createElement('span');
                        label.className = 'pdf-edit-dialog-section-title';
                        label.textContent = step.title || ('Section ' + (stepIndex + 1));
                        const chevron = document.createElement('span');
                        chevron.className = 'pdf-edit-dialog-section-chevron';
                        chevron.setAttribute('aria-hidden', 'true');
                        chevron.innerHTML = '<svg viewBox="0 0 16 16" width="14" height="14"><path d="M4 6l4 4 4-4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" fill="none"/></svg>';
                        toggle.append(badge, label, chevron);

                        const content = document.createElement('div');
                        content.className = 'pdf-edit-dialog-section-content';
                        content.id = toggle.id + '-content';
                        content.setAttribute('role', 'region');
                        content.setAttribute('aria-labelledby', toggle.id);
                        if (step.description) appendDescription(content, step.description);
                        const grid = document.createElement('div');
                        grid.className = 'pdf-edit-dialog-grid';
                        content.appendChild(grid);

                        element.append(toggle, content);
                        body.appendChild(element);

                        const section = { element: element, toggle: toggle, rows: [] };
                        sections.push(section);
                        toggle.addEventListener('click', () => setExpanded(section, !element.classList.contains('is-open')));
                        setExpanded(section, stepIndex === 0);
                        renderFields(grid, step, stepIndex, section);
                    });
                }

                updateVisibility();

                const first = body.querySelector('.pdf-edit-dialog-section.is-open input:checked, .pdf-edit-dialog-section.is-open input, .pdf-edit-dialog-section.is-open select, .pdf-edit-dialog-section.is-open textarea')
                    || body.querySelector('input:checked, input, select, textarea');
                if (first) first.focus();
                else next.focus();
            };

            const goNext = function () {
                collect();
                rows.forEach(r => r.row.classList.remove('has-error'));
                const invalid = validate();
                if (invalid) {
                    error.textContent = invalid.message;
                    invalid.row.row.classList.add('has-error');
                    if (invalid.row.section) setExpanded(invalid.row.section, true);
                    invalid.row.row.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
                    const target = invalid.row.input.matches('input, select, textarea') ? invalid.row.input : invalid.row.input.querySelector('input, select, textarea');
                    if (target) target.focus();
                    return;
                }
                const result = {};
                fields.forEach(f => {
                    const visible = typeof f.showIf !== 'function' || f.showIf(values);
                    if (visible) result[f.name] = values[f.name];
                });
                close(result);
            };

            cancel.addEventListener('click', () => close(null));
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
