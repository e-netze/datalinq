/*
 * PDF report editing tool: form inputs ("Inputs" toolbar button)
 *
 * Clicking the toolbar button opens a modal to choose the input type (text, number, date, text area,
 * checkbox, radio buttons, dropdown, signature), then a click on a page places it with the default settings.
 * Inputs are plain html form controls wrapped in a div (styled by css/datalinq/pdf-report/form-inputs.css):
 *
 *   <div class="dl-input dl-input-text" data-type="text" data-name="field_x" data-label-pos="top" style="width:200px;height:48px;--dl-bc:#c8ccd2;...">
 *       <label class="dl-input-label">Name</label><input class="dl-input-control" type="text" name="field_x" placeholder="..." />
 *   </div>
 *
 * Existing inputs:
 *   - resize by dragging the handles on the right/bottom edges
 *   - double click / context menu "Input settings..." = type options, label, placeholder, required, size, colors, border
 * Changes are written with the source action input.set; the source template and the action live
 * in DataLinq Code editor/pdf-editing/tools/input.js (same type id, same markup).
 */
(function () {
    const svgIcon = body => '<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + body + '</svg>';
    const inputsIcon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<rect x="2" y="6" width="20" height="12" rx="2"/><line x1="6" y1="10" x2="6" y2="14"/></svg>';

    const TYPES = [
        { id: 'text', label: 'Text', icon: svgIcon('<rect x="2" y="7" width="20" height="10" rx="2"/><line x1="6" y1="10" x2="6" y2="14"/>'), defaults: { width: 200, height: 48, placeholder: '' } },
        { id: 'number', label: 'Number', icon: svgIcon('<rect x="2" y="7" width="20" height="10" rx="2"/><text x="5" y="15" font-size="7" stroke="none" fill="currentColor">123</text>'), defaults: { width: 120, height: 48 } },
        { id: 'date', label: 'Date', icon: svgIcon('<rect x="3" y="5" width="18" height="16" rx="2"/><line x1="3" y1="10" x2="21" y2="10"/><line x1="8" y1="3" x2="8" y2="7"/><line x1="16" y1="3" x2="16" y2="7"/>'), defaults: { width: 150, height: 48 } },
        { id: 'textarea', label: 'Text area', icon: svgIcon('<rect x="3" y="4" width="18" height="16" rx="2"/><line x1="7" y1="9" x2="17" y2="9"/><line x1="7" y1="13" x2="17" y2="13"/><line x1="7" y1="17" x2="12" y2="17"/>'), defaults: { width: 260, height: 100 } },
        { id: 'checkbox', label: 'Checkbox', icon: svgIcon('<rect x="4" y="4" width="16" height="16" rx="2"/><polyline points="8 12 11 15 16 9"/>'), defaults: { width: 200, height: 24, borderWidth: 0, background: 'transparent' } },
        { id: 'radio', label: 'Radio buttons', icon: svgIcon('<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="3" fill="currentColor"/>'), defaults: { width: 200, height: 80, borderWidth: 0, background: 'transparent', options: ['Option 1', 'Option 2'] } },
        { id: 'select', label: 'Dropdown', icon: svgIcon('<rect x="2" y="7" width="20" height="10" rx="2"/><polyline points="15 11 17 13 19 11"/>'), defaults: { width: 200, height: 48, options: ['Option 1', 'Option 2'] } },
        { id: 'signature', label: 'Signature', icon: svgIcon('<path d="M3 17c3-6 5-9 6-6s-1 6 2 4 3-6 5-4 2 3 5 2"/><line x1="3" y1="21" x2="21" y2="21"/>'), defaults: { width: 260, height: 110, label: 'Signature' } }
    ];
    const typeOf = id => TYPES.find(t => t.id === id) || null;

    const BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    const LABEL_POSITIONS = [{ value: 'top', label: 'Above' }, { value: 'left', label: 'Left' }];
    const LENGTH_UNITS = ['px', 'mm', '%'];
    const PX_PER_MM = 96 / 25.4;
    const MIN_SIZE = 10;
    const MAX_SIZE = 5000;
    const HANDLES = ['e', 's', 'se'];
    const NAME = /^[A-Za-z][\w-]{0,63}$/;

    const BASE = {
        label: '', labelPos: 'top', placeholder: '', required: false, options: [], defaultValue: '',
        widthUnit: 'px', heightUnit: 'px',
        borderWidth: 1, borderStyle: 'solid', borderColor: '#c8ccd2', background: '#ffffff', color: '#1f2328', fontSize: 12, radius: 4
    };

    // ---- settings <-> markup ------------------------------------------------------------------------

    const clamp = (v, min, max, fallback) => {
        const n = Number(v);
        return v !== '' && v !== null && Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
    };
    const round = v => Math.round(v * 10) / 10;
    const text = (v, max) => typeof v === 'string' ? v.replace(/[\r\n]+/g, ' ').substring(0, max) : '';
    const color = (v, fallback) => /^#[0-9a-f]{6}$/i.test(v || '') ? v.toLowerCase() : fallback;
    const newName = () => 'field_' + Math.random().toString(36).substring(2, 10);

    const esc = v => String(v)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');

    const normalize = function (s) {
        s = Object.assign({}, BASE, s || {});
        const type = typeOf(s.type) ? s.type : 'text';
        return {
            type: type,
            name: NAME.test(s.name || '') ? s.name : newName(),
            label: text(s.label, 500),
            labelPos: LABEL_POSITIONS.some(p => p.value === s.labelPos) ? s.labelPos : 'top',
            placeholder: text(s.placeholder, 500),
            required: s.required === true,
            options: (Array.isArray(s.options) ? s.options : []).map(o => text(o, 200).trim()).filter(o => o !== '').slice(0, 50),
            defaultValue: type === 'signature' ? '' : text(s.defaultValue, 500),
            width: round(clamp(s.width, 0.1, MAX_SIZE, 200)),
            widthUnit: LENGTH_UNITS.indexOf(s.widthUnit) >= 0 ? s.widthUnit : 'px',
            height: round(clamp(s.height, 0.1, MAX_SIZE, 30)),
            heightUnit: LENGTH_UNITS.indexOf(s.heightUnit) >= 0 ? s.heightUnit : 'px',
            borderWidth: Math.round(clamp(s.borderWidth, 0, 20, 1)),
            borderStyle: BORDER_STYLES.indexOf(s.borderStyle) >= 0 ? s.borderStyle : 'solid',
            borderColor: color(s.borderColor, '#c8ccd2'),
            background: s.background === 'transparent' ? 'transparent' : color(s.background, '#ffffff'),
            color: color(s.color, '#1f2328'),
            fontSize: round(clamp(s.fontSize, 6, 72, 12)),
            radius: round(clamp(s.radius, 0, 100, 4))
        };
    };

    // inner markup (same as the source template in DataLinq Code, without the Razor @ escaping)
    const innerHtml = function (s) {
        const req = s.required ? ' required' : '';
        const name = ` name="${s.name}"`;
        const placeholder = s.placeholder ? ` placeholder="${esc(s.placeholder)}"` : '';
        let html = s.label && s.type !== 'checkbox' ? `<label class="dl-input-label">${esc(s.label)}</label>` : '';
        switch (s.type) {
            case 'textarea':
                return html + `<textarea class="dl-input-control"${name}${placeholder}${req}></textarea>`;
            case 'checkbox':
                return html + `<label class="dl-input-control dl-input-check"><input type="checkbox"${name}${req} /><span>${esc(s.label)}</span></label>`;
            case 'radio':
                return html + '<div class="dl-input-control dl-input-options">' +
                    s.options.map(o => `<label class="dl-input-check"><input type="radio"${name} value="${esc(o)}"${req} /><span>${esc(o)}</span></label>`).join('') +
                    '</div>';
            case 'select':
                return html + `<select class="dl-input-control"${name}${req}><option value="">${esc(s.placeholder)}</option>` +
                    s.options.map(o => `<option value="${esc(o)}">${esc(o)}</option>`).join('') + '</select>';
            case 'signature':
                return html + '<div class="dl-input-control dl-signature"><canvas class="dl-signature-canvas"></canvas>' +
                    '<button type="button" class="dl-signature-clear" data-noprint="true" title="Clear">&times;</button></div>';
            default:
                return html + `<input class="dl-input-control" type="${s.type}"${name}${placeholder}${req} />`;
        }
    };

    const toPx = (value, unit, pageSize) => unit === 'mm' ? value * PX_PER_MM : unit === '%' ? value * pageSize / 100 : value;
    const fromPx = (px, unit, pageSize) => round(unit === 'mm' ? px / PX_PER_MM : unit === '%' ? (pageSize ? px * 100 / pageSize : 100) : px);
    const cssLength = (value, unit, pageSize) => unit === '%'
        ? (pageSize ? toPx(value, unit, pageSize) + 'px' : value + '%')
        : value + unit;

    const applySettings = function (node, s, page) {
        Array.from(node.attributes).forEach(a => node.removeAttribute(a.name));
        node.className = 'dl-input dl-input-' + s.type;
        node.setAttribute('data-type', s.type);
        node.setAttribute('data-name', s.name);
        if (s.required) node.setAttribute('data-required', 'true');
        node.setAttribute('data-label-pos', s.labelPos);
        const st = node.style;
        st.boxSizing = 'border-box';
        st.width = cssLength(s.width, s.widthUnit, page ? page.clientWidth : 0);
        st.height = cssLength(s.height, s.heightUnit, page ? page.clientHeight : 0);
        st.setProperty('--dl-bw', s.borderWidth + 'px');
        st.setProperty('--dl-bs', s.borderStyle);
        st.setProperty('--dl-bc', s.borderColor);
        st.setProperty('--dl-bg', s.background);
        st.setProperty('--dl-fg', s.color);
        st.setProperty('--dl-fs', s.fontSize + 'px');
        st.setProperty('--dl-r', s.radius + 'px');
        node.innerHTML = innerHtml(s);
        // Razor expressions (@...) are resolved on the server after the next run, literals are shown immediately
        if (s.defaultValue && s.defaultValue.charAt(0) !== '@') {
            node.setAttribute('data-default', s.defaultValue);
            if (window.pdfFormInputs && pdfFormInputs.applyDefault) pdfFormInputs.applyDefault(node);
        }
    };

    const readSettings = function (node) {
        const st = node.style;
        const prop = name => st.getPropertyValue(name).trim();
        const type = node.getAttribute('data-type');
        const control = node.querySelector('.dl-input-control');
        const label = type === 'checkbox'
            ? (node.querySelector('.dl-input-check span') || {}).textContent
            : (node.querySelector('.dl-input-label') || {}).textContent;
        let options = [], placeholder = '';
        if (type === 'radio') options = Array.from(node.querySelectorAll('input[type=radio]')).map(r => r.value);
        if (type === 'select' && control) {
            options = Array.from(control.options).slice(1).map(o => o.value);
            placeholder = control.options.length ? control.options[0].textContent : '';
        } else if (control && control.hasAttribute('placeholder')) {
            placeholder = control.getAttribute('placeholder');
        }
        return normalize({
            type: type,
            name: node.getAttribute('data-name'),
            label: label || '',
            labelPos: node.getAttribute('data-label-pos'),
            placeholder: placeholder,
            required: node.getAttribute('data-required') === 'true',
            options: options,
            width: node.offsetWidth || parseFloat(st.width),
            height: node.offsetHeight || parseFloat(st.height),
            borderWidth: parseFloat(prop('--dl-bw')),
            borderStyle: prop('--dl-bs'),
            borderColor: prop('--dl-bc'),
            background: prop('--dl-bg'),
            color: prop('--dl-fg'),
            fontSize: parseFloat(prop('--dl-fs')),
            radius: parseFloat(prop('--dl-r'))
        });
    };

    // ---- editable inputs ---------------------------------------------------------------------------

    const bridge = () => window.dataLinqEditBridge;
    const connected = () => !!(bridge() && bridge().isConnected());

    const inputOf = element => element ? element.querySelector('.dl-input') : null;

    const isEditableInput = element =>
        !!(element && connected() &&
            !element.classList.contains('pdf-edit-pending') &&
            Number.isInteger(parseInt(element.getAttribute('data-dl-edit-index'), 10)) &&
            inputOf(element));

    const locate = element => ({
        index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
        originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
        originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
    });

    // settings as written in the source (with the units), falls back to the rendered px values
    const sourceSettings = async function (element, node) {
        const current = await pdfEditActions.request('input.get', locate(element));
        const rendered = readSettings(node);
        return current.success && current.data ? normalize(Object.assign(rendered, current.data)) : rendered;
    };

    const commit = async function (element, node, settings, previous) {
        const result = await pdfEditActions.request('input.set', Object.assign(locate(element), { settings: settings }));
        if (!result.success) {
            applySettings(node, previous, element.closest('.page'));
            pdfEditActions.notify(result.message || 'The input could not be changed.');
        }
    };

    // ---- resize handles (right / bottom, the position never changes) -------------------------------

    const ensureHandles = function (element) {
        if (element.querySelector(':scope > .pdf-edit-input-handle')) return;
        HANDLES.forEach(dir => {
            const handle = document.createElement('div');
            handle.className = 'pdf-edit-shape-handle pdf-edit-input-handle pdf-edit-shape-handle-' + dir;
            handle.setAttribute('data-dir', dir);
            handle.setAttribute('data-html2canvas-ignore', 'true');
            handle.addEventListener('mousedown', e => startResize(e, element, dir));
            element.appendChild(handle);
        });
    };

    document.addEventListener('mouseover', function (e) {
        const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
        if (element && isEditableInput(element)) ensureHandles(element);
    });

    const startResize = function (e, element, dir) {
        if (e.button !== 0) return;
        e.preventDefault();
        e.stopPropagation();

        const node = inputOf(element);
        const page = element.closest('.page');
        if (!node || !page || !isEditableInput(element)) return;

        const scale = page.offsetWidth > 0 ? page.getBoundingClientRect().width / page.offsetWidth : 1;
        const previous = readSettings(node);
        const w0 = node.offsetWidth, h0 = node.offsetHeight;
        const startX = e.clientX, startY = e.clientY;
        const horizontal = dir.indexOf('e') >= 0, vertical = dir.indexOf('s') >= 0;
        let changed = false;

        element.classList.add('pdf-edit-shape-resizing');
        document.body.classList.add('pdf-edit-shape-resizing-' + dir);

        const move = function (ev) {
            const w = horizontal ? round(Math.max(MIN_SIZE, w0 + (ev.clientX - startX) / scale)) : w0;
            const h = vertical ? round(Math.max(MIN_SIZE, h0 + (ev.clientY - startY) / scale)) : h0;
            node.style.width = w + 'px';
            node.style.height = h + 'px';
            changed = w !== w0 || h !== h0;
        };

        const end = async function () {
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', end);
            window.removeEventListener('blur', end);
            element.classList.remove('pdf-edit-shape-resizing');
            document.body.classList.remove('pdf-edit-shape-resizing-' + dir);
            if (typeof limitDraggableToPage === 'function') limitDraggableToPage(element);
            if (!changed) return;

            const settings = await sourceSettings(element, node);
            settings.width = fromPx(node.offsetWidth, settings.widthUnit, page.clientWidth);
            settings.height = fromPx(node.offsetHeight, settings.heightUnit, page.clientHeight);
            commit(element, node, settings, previous);
        };

        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', end);
        window.addEventListener('blur', end);
    };

    // ---- settings dialog ---------------------------------------------------------------------------

    const openSettings = function (initial, title, submitLabel) {
        const hasOptions = initial.type === 'radio' || initial.type === 'select';
        const hasPlaceholder = ['text', 'number', 'textarea', 'select'].indexOf(initial.type) >= 0;
        const fieldFields = [
            { name: 'label', label: initial.type === 'checkbox' ? 'Text' : 'Label', type: 'text', value: initial.label, placeholder: 'optional' }
        ];
        if (initial.type !== 'checkbox') {
            fieldFields.push({ name: 'labelPos', label: 'Label position', type: 'select', value: initial.labelPos, options: LABEL_POSITIONS });
        }
        if (hasPlaceholder) {
            fieldFields.push({ name: 'placeholder', label: initial.type === 'select' ? 'Empty option text' : 'Placeholder', type: 'text', value: initial.placeholder });
        }
        if (hasOptions) {
            fieldFields.push({ name: 'options', label: 'Options (separated by ;)', type: 'text', value: initial.options.join('; '), placeholder: 'Option 1; Option 2' });
        }
        if (initial.type !== 'signature') {
            fieldFields.push({
                name: 'defaultValue', label: 'Default value', type: 'text', value: initial.defaultValue,
                placeholder: initial.type === 'checkbox' ? 'true | false | @DLH.QueryParameter("key")' : 'text, @var, @DLH.QueryParameter("key"), @DLH.GetConstant("key"), @SECURITY.GetSecret("key")'
            });
        }
        fieldFields.push({ name: 'required', label: 'Required (download / print only when filled in)', type: 'checkbox', value: initial.required });

        return pdfEditDialog.open({
            title: title,
            submitLabel: submitLabel,
            steps: [
                { title: 'Field', fields: fieldFields },
                {
                    title: 'Size',
                    fields: [
                        { name: 'width', label: 'Width', type: 'number', value: initial.width, min: 0.1, max: MAX_SIZE, step: 'any' },
                        { name: 'widthUnit', label: 'Width unit', type: 'select', value: initial.widthUnit, options: LENGTH_UNITS },
                        { name: 'height', label: 'Height', type: 'number', value: initial.height, min: 0.1, max: MAX_SIZE, step: 'any' },
                        { name: 'heightUnit', label: 'Height unit', type: 'select', value: initial.heightUnit, options: LENGTH_UNITS },
                        { name: 'fontSize', label: 'Font size (px)', type: 'number', value: initial.fontSize, min: 6, max: 72, step: 'any' }
                    ]
                },
                {
                    title: 'Style',
                    fields: [
                        { name: 'useBackground', label: 'Background color', type: 'checkbox', value: initial.background !== 'transparent' },
                        { name: 'background', label: 'Background', type: 'color', value: initial.background !== 'transparent' ? initial.background : '#ffffff', showIf: v => v.useBackground },
                        { name: 'color', label: 'Text color', type: 'color', value: initial.color },
                        { name: 'useBorder', label: 'Border', type: 'checkbox', value: initial.borderWidth > 0 && initial.borderStyle !== 'none' },
                        { name: 'borderWidth', label: 'Border width (px)', type: 'number', value: Math.max(1, initial.borderWidth), min: 1, max: 20, step: 1, showIf: v => v.useBorder },
                        { name: 'borderStyle', label: 'Border style', type: 'select', value: initial.borderStyle === 'none' ? 'solid' : initial.borderStyle, options: BORDER_STYLES.filter(b => b !== 'none'), showIf: v => v.useBorder },
                        { name: 'borderColor', label: 'Border color', type: 'color', value: initial.borderColor, showIf: v => v.useBorder },
                        { name: 'radius', label: 'Corner radius (px)', type: 'number', value: initial.radius, min: 0, max: 100, step: 'any' }
                    ]
                }
            ]
        }).then(values => {
            if (!values) return null;
            return normalize({
                type: initial.type,
                name: initial.name,
                label: values.label,
                labelPos: values.labelPos !== undefined ? values.labelPos : initial.labelPos,
                placeholder: values.placeholder !== undefined ? values.placeholder : initial.placeholder,
                options: values.options !== undefined ? String(values.options).split(';') : initial.options,
                defaultValue: values.defaultValue !== undefined ? values.defaultValue : initial.defaultValue,
                required: !!values.required,
                width: values.width,
                widthUnit: values.widthUnit,
                height: values.height,
                heightUnit: values.heightUnit,
                fontSize: values.fontSize,
                background: values.useBackground ? values.background : 'transparent',
                color: values.color,
                borderWidth: values.useBorder ? values.borderWidth : 0,
                borderStyle: values.useBorder ? values.borderStyle : 'solid',
                borderColor: values.borderColor,
                radius: values.radius
            });
        });
    };

    let busy = false;

    const editInput = async function (element) {
        if (busy || !isEditableInput(element)) return;
        busy = true;
        try {
            const node = inputOf(element);
            const previous = readSettings(node);
            const initial = await sourceSettings(element, node);
            const settings = await openSettings(initial, 'Input settings', 'Apply');
            if (!settings || !element.isConnected) return;

            applySettings(node, settings, element.closest('.page'));
            await commit(element, node, settings, previous);
        } finally {
            busy = false;
        }
    };

    document.addEventListener('dblclick', function (e) {
        const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
        if (element && isEditableInput(element)) {
            e.preventDefault();
            editInput(element);
        }
    });

    pdfEditContextMenu.register({
        id: 'input-settings',
        label: 'Input settings...',
        icon: inputsIcon,
        when: isEditableInput,
        run: editInput
    });

    // inputs are not usable while editing (clicks select / drag the draggable)
    if (typeof isPdfReportEditingMode === 'function' && isPdfReportEditingMode()) {
        document.body.classList.add('pdf-edit-inputs-inert');
    }

    // ---- add a new input ---------------------------------------------------------------------------

    const chooseInput = function () {
        return pdfEditDialog.open({
            title: 'Inputs',
            submitLabel: 'Place',
            steps: [{
                title: 'Choose an input',
                description: 'Click on a page afterwards to place it (Esc to cancel).',
                fields: [
                    { name: 'type', label: 'Input', type: 'choice', value: TYPES[0].id, options: TYPES.map(t => ({ value: t.id, label: t.label, icon: t.icon })) }
                ]
            }]
        }).then(values => {
            const type = values ? typeOf(values.type) : null;
            return type ? JSON.stringify(normalize(Object.assign({ type: type.id, name: newName() }, type.defaults))) : null;
        });
    };

    pdfEditElementTypes.register({
        type: 'input',
        label: 'Inputs',
        icon: inputsIcon,
        choose: chooseInput,
        prompt: chooseInput,
        create: function (json) {
            let settings = {};
            try { settings = JSON.parse(json); } catch (e) { /* defaults */ }
            const node = document.createElement('div');
            applySettings(node, normalize(settings));
            return node;
        },
        textTarget: function () {
            return null;
        }
    });
})();
