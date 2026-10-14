/*
 * PDF report editing tool: shapes ("Forms" toolbar button)
 *
 * Clicking the toolbar button opens a modal to choose the shape (rectangle, circle, line, arrow),
 * then a click on a page places it with the default settings. Shapes are plain divs with an
 * inline style in the source (line/arrow contain an inline svg):
 *
 *   <div class="dl-shape dl-shape-rect" style="width:120px;height:80px;background-color:transparent;border:1px solid #000000;..."></div>
 *
 * Existing shapes:
 *   - resize by dragging the handles on the edges/corners (Shift = keep the aspect ratio)
 *   - double click / context menu "Shape settings..." = size, fill, opacity, border, corner radius
 * Changes are written with the source action shape.set; the source template and the action live
 * in DataLinq Code editor/pdf-editing/tools/shape.js (same type id).
 */
window.pdfEditShapes = (function () {
    const shapes = [];
    return {
        register: shape => shapes.push(shape),
        all: () => shapes.slice(),
        get: id => shapes.find(s => s.id === id) || null
    };
})();

(function () {
    const svgIcon = body => '<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + body + '</svg>';
    const rectIcon = svgIcon('<rect x="3" y="6" width="18" height="12" rx="1"/>');
    const circleIcon = svgIcon('<circle cx="12" cy="12" r="8"/>');
    const lineIcon = svgIcon('<line x1="4" y1="20" x2="20" y2="4"/>');
    const arrowIcon = svgIcon('<line x1="4" y1="12" x2="20" y2="12"/><polyline points="14 6 20 12 14 18"/>');
    const formsIcon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<rect x="3" y="3" width="11" height="11" rx="1"/><circle cx="16" cy="16" r="5"/></svg>';

    const BORDER_STYLES = ['solid', 'dashed', 'dotted', 'double', 'none'];
    const LENGTH_UNITS = ['px', 'mm', '%'];   // % = of the page width/height
    const PX_PER_MM = 96 / 25.4;
    const DIRECTIONS = [
        { value: 'e', label: '→ right' }, { value: 'w', label: '← left' },
        { value: 's', label: '↓ down' }, { value: 'n', label: '↑ up' },
        { value: 'se', label: '↘ down right' }, { value: 'ne', label: '↗ up right' },
        { value: 'sw', label: '↙ down left' }, { value: 'nw', label: '↖ up left' }
    ];
    const STROKE_STYLES = ['solid', 'dashed', 'dotted'];
    const MIN_SIZE = 4;
    const MAX_SIZE = 5000;
    const HANDLES = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];

    const area = { fill: 'transparent', opacity: 1, borderWidth: 1, borderStyle: 'solid', borderColor: '#000000', radius: 0 };
    pdfEditShapes.register({ id: 'rect', label: 'Rectangle', icon: rectIcon, defaults: Object.assign({ width: 120, height: 80 }, area) });
    pdfEditShapes.register({ id: 'circle', label: 'Circle / ellipse', icon: circleIcon, defaults: Object.assign({ width: 80, height: 80 }, area) });
    pdfEditShapes.register({ id: 'line', label: 'Line', icon: lineIcon, stroke: true, defaults: { width: 120, height: 10, opacity: 1, borderWidth: 1, borderStyle: 'solid', borderColor: '#000000', direction: 'e' } });
    pdfEditShapes.register({ id: 'arrow', label: 'Arrow', icon: arrowIcon, stroke: true, defaults: { width: 120, height: 20, opacity: 1, borderWidth: 2, borderStyle: 'solid', borderColor: '#000000', direction: 'e' } });

    const isStroke = id => !!(pdfEditShapes.get(id) || {}).stroke;

    // ---- settings <-> style ---------------------------------------------------------------------

    const clamp = (v, min, max, fallback) => {
        const n = Number(v);
        return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
    };
    const round = v => Math.round(v * 10) / 10;

    const toHex = function (color) {
        const m = /^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?/.exec(color || '');
        if (!m) return /^#[0-9a-f]{6}$/i.test(color || '') ? color.toLowerCase() : '';
        if (m[4] !== undefined && parseFloat(m[4]) === 0) return 'transparent';
        return '#' + m.slice(1, 4).map(n => (+n).toString(16).padStart(2, '0')).join('');
    };

    const normalize = function (s) {
        s = s || {};
        const shape = pdfEditShapes.get(s.shape) ? s.shape : 'rect';
        const stroke = isStroke(shape);
        const fill = stroke || s.fill === 'transparent' ? 'transparent' : (toHex(s.fill) || 'transparent');
        return {
            shape: shape,
            width: round(clamp(s.width, 0.1, MAX_SIZE, 120)),
            widthUnit: LENGTH_UNITS.indexOf(s.widthUnit) >= 0 ? s.widthUnit : 'px',
            height: round(clamp(s.height, 0.1, MAX_SIZE, 80)),
            heightUnit: LENGTH_UNITS.indexOf(s.heightUnit) >= 0 ? s.heightUnit : 'px',
            fill: fill,
            opacity: Math.round(clamp(s.opacity, 0, 1, 1) * 100) / 100,
            borderWidth: Math.round(clamp(s.borderWidth, stroke ? 1 : 0, 50, 1)),
            borderStyle: (stroke ? STROKE_STYLES : BORDER_STYLES).indexOf(s.borderStyle) >= 0 ? s.borderStyle : 'solid',
            borderColor: toHex(s.borderColor) && toHex(s.borderColor) !== 'transparent' ? toHex(s.borderColor) : '#000000',
            radius: shape === 'rect' ? round(clamp(s.radius, 0, MAX_SIZE, 0)) : 0,
            rotation: round(((clamp(s.rotation, -100000, 100000, 0) % 360) + 540) % 360 - 180),
            direction: stroke ? (DIRECTIONS.some(d => d.value === s.direction) ? s.direction : 'e') : undefined
        };
    };

    // svg content of line/arrow shapes (same markup as the source template in DataLinq Code)
    const strokeSvg = function (s) {
        const d = s.direction, w = s.borderWidth, color = s.borderColor;
        const x1 = d.indexOf('w') >= 0 ? 100 : d.indexOf('e') >= 0 ? 0 : 50;
        const y1 = d.indexOf('n') >= 0 ? 100 : d.indexOf('s') >= 0 ? 0 : 50;
        const x2 = x1 === 50 ? 50 : 100 - x1, y2 = y1 === 50 ? 50 : 100 - y1;
        const dash = s.borderStyle === 'dashed' ? ` stroke-dasharray="${w * 3} ${w * 2}"`
            : s.borderStyle === 'dotted' ? ` stroke-dasharray="0 ${w * 2}" stroke-linecap="round"` : '';
        const id = 'dl-arrow-' + color.substring(1);
        const marker = s.shape === 'arrow'
            ? `<defs><marker id="${id}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="4" markerHeight="4" orient="auto"><path d="M0,0L10,5L0,10z" fill="${color}"/></marker></defs>`
            : '';
        return `<svg width="100%" height="100%" style="display:block;overflow:visible">${marker}` +
            `<line x1="${x1}%" y1="${y1}%" x2="${x2}%" y2="${y2}%" stroke="${color}" stroke-width="${w}"${dash}${marker ? ` marker-end="url(#${id})"` : ''}/></svg>`;
    };

    // length in the given unit -> px (% of the page size)
    const toPx = (value, unit, pageSize) => unit === 'mm' ? value * PX_PER_MM : unit === '%' ? value * pageSize / 100 : value;
    const fromPx = (px, unit, pageSize) => round(unit === 'mm' ? px / PX_PER_MM : unit === '%' ? (pageSize ? px * 100 / pageSize : 100) : px);

    // % is resolved against the page (draggables are shrink-to-fit, see draggable-widths.js)
    const cssLength = (value, unit, pageSize) => unit === '%'
        ? (pageSize ? toPx(value, unit, pageSize) + 'px' : value + '%')
        : value + unit;

    const applyStyle = function (node, s, page) {
        node.className = 'dl-shape dl-shape-' + s.shape;
        node.style.boxSizing = 'border-box';
        node.style.width = cssLength(s.width, s.widthUnit, page ? page.clientWidth : 0);
        node.style.height = cssLength(s.height, s.heightUnit, page ? page.clientHeight : 0);
        node.style.opacity = s.opacity < 1 ? String(s.opacity) : '';
        setRotation(node, s.rotation);
        if (isStroke(s.shape)) {
            node.style.backgroundColor = '';
            node.style.border = '';
            node.style.borderRadius = '';
            node.setAttribute('data-dir', s.direction);
            node.innerHTML = strokeSvg(s);
            return;
        }
        node.removeAttribute('data-dir');
        node.innerHTML = '';
        node.style.backgroundColor = s.fill;
        node.style.border = s.borderStyle === 'none' || s.borderWidth === 0
            ? 'none'
            : `${s.borderWidth}px ${s.borderStyle} ${s.borderColor}`;
        node.style.borderRadius = s.shape === 'circle' ? '50%' : s.radius ? s.radius + 'px' : '';
    };

    const readSettings = function (node) {
        const st = node.style;
        const shape = (/\bdl-shape-([a-z0-9-]+)/.exec(node.className) || [])[1];
        const line = node.querySelector('svg line');
        if (line) {
            const dash = line.getAttribute('stroke-dasharray') || '';
            return normalize({
                shape: shape,
                width: node.offsetWidth || parseFloat(st.width),
                height: node.offsetHeight || parseFloat(st.height),
                opacity: st.opacity === '' ? 1 : parseFloat(st.opacity),
                borderWidth: parseFloat(line.getAttribute('stroke-width')) || 1,
                borderStyle: !dash ? 'solid' : /^0\s/.test(dash) ? 'dotted' : 'dashed',
                borderColor: line.getAttribute('stroke'),
                direction: node.getAttribute('data-dir'),
                rotation: rotationOf(node)
            });
        }
        const borderStyle = st.borderTopStyle || 'none';
        return normalize({
            shape: shape,
            width: node.offsetWidth || parseFloat(st.width),
            height: node.offsetHeight || parseFloat(st.height),
            fill: toHex(st.backgroundColor) || 'transparent',
            opacity: st.opacity === '' ? 1 : parseFloat(st.opacity),
            borderWidth: borderStyle === 'none' ? 0 : parseFloat(st.borderTopWidth) || 0,
            borderStyle: borderStyle === 'none' ? 'solid' : borderStyle,
            borderColor: st.borderTopColor,
            radius: parseFloat(st.borderTopLeftRadius) || 0,
            rotation: rotationOf(node)
        });
    };

    const rotationOf = node => parseFloat((/rotate\(\s*(-?[\d.]+)deg/.exec(node.style.transform) || [])[1]) || 0;

    // margins that make the layout box (and so the draggable outline) match the bounding box of the rotated shape
    const rotationMargins = function (w, h, rotation) {
        const a = rotation * Math.PI / 180;
        const bw = Math.abs(w * Math.cos(a)) + Math.abs(h * Math.sin(a));
        const bh = Math.abs(w * Math.sin(a)) + Math.abs(h * Math.cos(a));
        return { x: round((bw - w) / 2), y: round((bh - h) / 2) };
    };

    const marginsOf = node => ({ x: parseFloat(node.style.marginLeft) || 0, y: parseFloat(node.style.marginTop) || 0 });

    // rotates around the center; the draggable is shifted so the center stays where it is
    const setRotation = function (node, rotation) {
        node.style.transformOrigin = rotation ? 'center' : '';
        node.style.transform = rotation ? `rotate(${rotation}deg)` : '';
        const before = marginsOf(node);
        const m = rotation && node.isConnected ? rotationMargins(node.offsetWidth, node.offsetHeight, rotation) : { x: 0, y: 0 };
        node.style.margin = m.x || m.y ? `${m.y}px ${m.x}px` : '';
        const element = node.closest('.page .element');
        if (element && (m.x !== before.x || m.y !== before.y)) {
            const t = translateOf(element);
            setTranslate(element, round(t.x + before.x - m.x), round(t.y + before.y - m.y));
        }
    };

    const withMargins = function (settings, node) {
        const m = marginsOf(node);
        return Object.assign(settings, { marginX: m.x, marginY: m.y });
    };

    // ---- editable shapes -------------------------------------------------------------------------

    const bridge = () => window.dataLinqEditBridge;
    const connected = () => !!(bridge() && bridge().isConnected());

    const shapeOf = element => element ? element.querySelector(':scope > .dl-shape, :scope > * > .dl-shape') || element.querySelector('.dl-shape') : null;

    const isEditableShape = element =>
        !!(element && connected() &&
            !element.classList.contains('pdf-edit-pending') &&
            Number.isInteger(parseInt(element.getAttribute('data-dl-edit-index'), 10)) &&
            shapeOf(element));

    const locate = element => ({
        index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
        originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
        originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
    });

    const translateOf = element => {
        const m = new DOMMatrix(window.getComputedStyle(element).transform);
        return { x: m.m41, y: m.m42 };
    };

    const setTranslate = function (element, x, y) {
        element.style.transform = `translate(${x}px, ${y}px)`;
        element.setAttribute('data-x', x);
        element.setAttribute('data-y', y);
    };

    // settings as written in the source (with the units), falls back to the rendered px values
    const sourceSettings = async function (element, node) {
        const current = await pdfEditActions.request('shape.get', locate(element));
        const rendered = readSettings(node);
        return current.success && current.data ? normalize(Object.assign(rendered, current.data)) : rendered;
    };

    // writes the shape settings to the source; moved = the position changed too (left/top handles)
    const commit = async function (element, node, settings, previous, moved) {
        const result = await pdfEditActions.request('shape.set', Object.assign(locate(element), { settings: withMargins(Object.assign({}, settings), node) }));
        if (!result.success) {
            applyStyle(node, previous.settings);
            if (moved) setTranslate(element, previous.x, previous.y);
            pdfEditActions.notify(result.message || 'The shape could not be changed.');
            return;
        }
        if (moved) {
            element.dispatchEvent(new CustomEvent('datalinq-pdf:drag-end', { bubbles: true }));
        }
    };

    // ---- resize handles ----------------------------------------------------------------------------

    const ensureHandles = function (element) {
        if (element.querySelector(':scope > .pdf-edit-shape-handle')) return;
        HANDLES.forEach(dir => {
            const handle = document.createElement('div');
            handle.className = 'pdf-edit-shape-handle pdf-edit-shape-handle-' + dir;
            handle.setAttribute('data-dir', dir);
            handle.setAttribute('data-html2canvas-ignore', 'true');
            handle.addEventListener('mousedown', e => startResize(e, element, dir));
            element.appendChild(handle);
        });
        const rotate = document.createElement('div');
        rotate.className = 'pdf-edit-shape-handle pdf-edit-shape-rotate';
        rotate.title = 'Rotate (Shift = 5° steps)';
        rotate.setAttribute('data-html2canvas-ignore', 'true');
        rotate.addEventListener('mousedown', e => startRotate(e, element));
        element.appendChild(rotate);
    };

    const startRotate = function (e, element) {
        if (e.button !== 0) return;
        e.preventDefault();
        e.stopPropagation();

        const node = shapeOf(element);
        if (!node || !isEditableShape(element)) return;

        const startSettings = readSettings(node);
        const start = translateOf(element);
        const r = node.getBoundingClientRect();
        const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
        const angleAt = ev => Math.atan2(ev.clientY - cy, ev.clientX - cx) * 180 / Math.PI;
        const offset = startSettings.rotation - angleAt(e);
        let rotation = startSettings.rotation;

        element.classList.add('pdf-edit-shape-resizing');
        document.body.classList.add('pdf-edit-shape-rotating');

        const move = function (ev) {
            let a = angleAt(ev) + offset;
            a = ev.shiftKey ? Math.round(a / 5) * 5 : round(a);
            a = round(((a % 360) + 540) % 360 - 180);
            if (a === rotation) return;
            rotation = a;
            setRotation(node, rotation);
        };

        const end = async function () {
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', end);
            window.removeEventListener('blur', end);
            element.classList.remove('pdf-edit-shape-resizing');
            document.body.classList.remove('pdf-edit-shape-rotating');
            if (rotation === startSettings.rotation) return;

            const now = translateOf(element);
            const settings = await sourceSettings(element, node);
            settings.rotation = rotation;
            commit(element, node, settings, { settings: startSettings, x: start.x, y: start.y }, now.x !== start.x || now.y !== start.y);
        };

        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', end);
        window.addEventListener('blur', end);
    };

    document.addEventListener('mouseover', function (e) {
        const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
        if (element && isEditableShape(element)) ensureHandles(element);
    });

    const startResize = function (e, element, dir) {
        if (e.button !== 0) return;
        // keep the draggable from moving
        e.preventDefault();
        e.stopPropagation();

        const node = shapeOf(element);
        const page = element.closest('.page');
        if (!node || !page || !isEditableShape(element)) return;

        const pageRect = page.getBoundingClientRect();
        const scale = page.offsetWidth > 0 ? pageRect.width / page.offsetWidth : 1;
        const start = translateOf(element);
        const startSettings = readSettings(node);
        const startMargins = marginsOf(node);
        const w0 = node.offsetWidth, h0 = node.offsetHeight;
        const ratio = w0 / Math.max(1, h0);
        const startX = e.clientX, startY = e.clientY;

        // page bounds relative to the translate (layout position of the draggable)
        const elementRect = element.getBoundingClientRect();
        const baseLeft = (elementRect.left - pageRect.left) / scale - start.x;
        const baseTop = (elementRect.top - pageRect.top) / scale - start.y;
        const minX = -baseLeft, minY = -baseTop;
        const maxRight = page.offsetWidth - baseLeft;
        const maxBottom = page.offsetHeight - baseTop;

        const hasE = dir.indexOf('e') >= 0, hasW = dir.indexOf('w') >= 0;
        const hasN = dir.indexOf('n') >= 0, hasS = dir.indexOf('s') >= 0;
        const horizontal = hasE || hasW, vertical = hasN || hasS;

        let changed = false;
        element.classList.add('pdf-edit-shape-resizing');
        document.body.classList.add('pdf-edit-shape-resizing-' + dir);

        const move = function (ev) {
            const dx = (ev.clientX - startX) / scale;
            const dy = (ev.clientY - startY) / scale;

            let w = w0 + (hasE ? dx : hasW ? -dx : 0);
            let h = h0 + (hasS ? dy : hasN ? -dy : 0);

            // limit to the page (anchored edge stays where it is)
            const right = start.x + w0, bottom = start.y + h0;
            if (hasE) w = Math.min(w, maxRight - start.x);
            if (hasW) w = Math.min(w, right - minX);
            if (hasS) h = Math.min(h, maxBottom - start.y);
            if (hasN) h = Math.min(h, bottom - minY);
            w = Math.max(MIN_SIZE, w);
            h = Math.max(MIN_SIZE, h);

            // Ctrl = snap the moving edges to the visible guide lines (as while dragging)
            let snapped = false;
            if (ev.ctrlKey && typeof snapEdgeToGuides === 'function') {
                if (hasE) {
                    const s = snapEdgeToGuides(page, 'x', baseLeft + start.x + w);
                    if (s.snapped) { w = Math.max(MIN_SIZE, Math.min(s.value - baseLeft - start.x, maxRight - start.x)); snapped = true; }
                } else if (hasW) {
                    const s = snapEdgeToGuides(page, 'x', baseLeft + right - w);
                    if (s.snapped) { w = Math.max(MIN_SIZE, Math.min(baseLeft + right - s.value, right - minX)); snapped = true; }
                }
                if (hasS) {
                    const s = snapEdgeToGuides(page, 'y', baseTop + start.y + h);
                    if (s.snapped) { h = Math.max(MIN_SIZE, Math.min(s.value - baseTop - start.y, maxBottom - start.y)); snapped = true; }
                } else if (hasN) {
                    const s = snapEdgeToGuides(page, 'y', baseTop + bottom - h);
                    if (s.snapped) { h = Math.max(MIN_SIZE, Math.min(baseTop + bottom - s.value, bottom - minY)); snapped = true; }
                }
            }
            element.classList.toggle('element-snapped', snapped);

            if (ev.shiftKey) {
                if (horizontal && vertical) {
                    if (Math.abs(w - w0) / w0 >= Math.abs(h - h0) / Math.max(1, h0)) h = w / ratio;
                    else w = h * ratio;
                } else if (horizontal) {
                    h = w / ratio;
                } else {
                    w = h * ratio;
                }
                // keep inside the page after applying the ratio
                const maxW = hasW ? right - minX : maxRight - start.x;
                const maxH = hasN ? bottom - minY : maxBottom - start.y;
                const factor = Math.min(1, maxW / w, maxH / h);
                w = Math.max(MIN_SIZE, w * factor);
                h = Math.max(MIN_SIZE, h * factor);
            }

            w = round(w);
            h = round(h);
            const x = hasW ? round(right - w) : start.x;
            const y = hasN ? round(bottom - h) : start.y;

            node.style.width = w + 'px';
            node.style.height = h + 'px';
            if (startSettings.rotation) {
                // keep the layout box = bounding box of the rotated shape
                const m = rotationMargins(w, h, startSettings.rotation);
                node.style.margin = `${m.y}px ${m.x}px`;
                setTranslate(element, round(x + startMargins.x - m.x), round(y + startMargins.y - m.y));
            } else {
                setTranslate(element, x, y);
            }
            changed = w !== w0 || h !== h0;
        };

        const end = async function () {
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', end);
            window.removeEventListener('blur', end);
            element.classList.remove('pdf-edit-shape-resizing');
            element.classList.remove('element-snapped');
            document.body.classList.remove('pdf-edit-shape-resizing-' + dir);
            if (typeof limitDraggableToPage === 'function') limitDraggableToPage(element);
            if (!changed) return;

            const now = translateOf(element);
            const settings = await sourceSettings(element, node);
            settings.width = fromPx(node.offsetWidth, settings.widthUnit, page.clientWidth);
            settings.height = fromPx(node.offsetHeight, settings.heightUnit, page.clientHeight);
            commit(element, node, settings, { settings: startSettings, x: start.x, y: start.y }, now.x !== start.x || now.y !== start.y);
        };

        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', end);
        window.addEventListener('blur', end);
    };

    // ---- settings dialog ---------------------------------------------------------------------------

    const openSettings = function (initial, title, submitLabel) {
        const stroke = isStroke(initial.shape);
        const sizeFields = [
            { name: 'width', label: 'Width', type: 'number', value: initial.width, min: 0.1, max: MAX_SIZE, step: 'any' },
            { name: 'widthUnit', label: 'Width unit', type: 'select', value: initial.widthUnit, options: LENGTH_UNITS },
            { name: 'height', label: 'Height', type: 'number', value: initial.height, min: 0.1, max: MAX_SIZE, step: 'any' },
            { name: 'heightUnit', label: 'Height unit', type: 'select', value: initial.heightUnit, options: LENGTH_UNITS }
        ];
        if (initial.shape === 'rect') {
            sizeFields.push({ name: 'radius', label: 'Corner radius (px)', type: 'number', value: initial.radius, min: 0, max: MAX_SIZE, step: 'any' });
        }
        sizeFields.push({ name: 'rotation', label: 'Rotation (°, around the center)', type: 'number', value: initial.rotation, min: -360, max: 360, step: 'any' });
        const steps = stroke
            ? [
                { title: 'Size', fields: sizeFields },
                {
                    title: 'Line',
                    fields: [
                        { name: 'direction', label: 'Direction', type: 'select', value: initial.direction, options: DIRECTIONS },
                        { name: 'borderWidth', label: 'Width (px)', type: 'number', value: initial.borderWidth, min: 1, max: 50, step: 1 },
                        { name: 'borderStyle', label: 'Style', type: 'select', value: initial.borderStyle, options: STROKE_STYLES },
                        { name: 'borderColor', label: 'Color', type: 'color', value: initial.borderColor },
                        { name: 'opacity', label: 'Opacity (%)', type: 'number', value: Math.round(initial.opacity * 100), min: 0, max: 100, step: 1 }
                    ]
                }
            ]
            : null;
        return pdfEditDialog.open({
            title: title,
            submitLabel: submitLabel,
            steps: steps || [
                {
                    title: 'Size',
                    fields: sizeFields
                },
                {
                    title: 'Fill',
                    fields: [
                        { name: 'useFill', label: 'Fill color', type: 'checkbox', value: initial.fill !== 'transparent' },
                        { name: 'fill', label: 'Color', type: 'color', value: initial.fill !== 'transparent' ? initial.fill : '#ffffff', showIf: v => v.useFill },
                        { name: 'opacity', label: 'Opacity (%)', type: 'number', value: Math.round(initial.opacity * 100), min: 0, max: 100, step: 1 }
                    ]
                },
                {
                    title: 'Border',
                    fields: [
                        { name: 'useBorder', label: 'Border', type: 'checkbox', value: initial.borderWidth > 0 },
                        { name: 'borderWidth', label: 'Width (px)', type: 'number', value: Math.max(1, initial.borderWidth), min: 1, max: 50, step: 1, showIf: v => v.useBorder },
                        { name: 'borderStyle', label: 'Style', type: 'select', value: initial.borderStyle, options: BORDER_STYLES, showIf: v => v.useBorder },
                        { name: 'borderColor', label: 'Color', type: 'color', value: initial.borderColor, showIf: v => v.useBorder }
                    ]
                }
            ]
        }).then(values => {
            if (!values) return null;
            return normalize({
                shape: initial.shape,
                width: values.width,
                widthUnit: values.widthUnit,
                height: values.height,
                heightUnit: values.heightUnit,
                radius: values.radius,
                fill: values.useFill ? values.fill : 'transparent',
                opacity: (Number(values.opacity) || 0) / 100,
                borderWidth: stroke || values.useBorder ? values.borderWidth : 0,
                borderStyle: values.borderStyle,
                borderColor: values.borderColor,
                direction: values.direction,
                rotation: values.rotation
            });
        });
    };

    let busy = false;

    const editShape = async function (element) {
        if (busy || !isEditableShape(element)) return;
        busy = true;
        try {
            const node = shapeOf(element);
            const previous = readSettings(node);
            const initial = await sourceSettings(element, node);
            const settings = await openSettings(initial, 'Shape settings', 'Apply');
            if (!settings || !element.isConnected) return;

            const t = translateOf(element);
            applyStyle(node, settings, element.closest('.page'));
            const now = translateOf(element);
            await commit(element, node, settings, { settings: previous, x: t.x, y: t.y }, now.x !== t.x || now.y !== t.y);
        } finally {
            busy = false;
        }
    };

    document.addEventListener('dblclick', function (e) {
        const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
        if (element && isEditableShape(element)) {
            e.preventDefault();
            editShape(element);
        }
    });

    pdfEditContextMenu.register({
        id: 'shape-settings',
        label: 'Shape settings...',
        icon: formsIcon,
        when: isEditableShape,
        run: editShape
    });

    // ---- add a new shape -------------------------------------------------------------------------

    const chooseShape = function () {
        const shapes = pdfEditShapes.all();
        return pdfEditDialog.open({
            title: 'Forms',
            submitLabel: 'Place',
            steps: [{
                title: 'Choose a form',
                description: 'Click on a page afterwards to place it (Esc to cancel).',
                fields: [
                    { name: 'shape', label: 'Form', type: 'choice', value: shapes[0].id, options: shapes.map(s => ({ value: s.id, label: s.label, icon: s.icon })) }
                ]
            }]
        }).then(values => {
            const shape = values ? pdfEditShapes.get(values.shape) : null;
            return shape ? JSON.stringify(normalize(Object.assign({ shape: shape.id }, shape.defaults))) : null;
        });
    };

    pdfEditElementTypes.register({
        type: 'shape',
        label: 'Forms',
        icon: formsIcon,
        choose: chooseShape,
        prompt: chooseShape,
        create: function (text) {
            let settings = {};
            try { settings = JSON.parse(text); } catch (e) { /* defaults */ }
            const node = document.createElement('div');
            applyStyle(node, normalize(settings));
            return node;
        },
        textTarget: function () {
            return null;
        }
    });
})();
