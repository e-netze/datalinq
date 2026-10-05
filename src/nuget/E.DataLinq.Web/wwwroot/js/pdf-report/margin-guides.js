/*
 * PDF report - margin guides (editing mode only).
 *
 * Adds optional snapping guide lines at a configurable distance (in mm) from the
 * left, right, top and bottom page edges - at most one line per side. Together
 * with the page center lines they are the targets for Ctrl + drag snapping.
 * Settings are kept per report in sessionStorage so they survive live-preview reloads.
 *
 * Loaded before editing.js, which uses getVisibleSnapLines and the toolbar helpers.
 */

const MARGIN_GUIDE_SIDES = ['left', 'right', 'top', 'bottom'];
const MARGIN_GUIDE_DEFAULT_MM = 10;
const MARGIN_GUIDE_STORAGE_KEY = 'datalinq-pdf-margin-guides:' + window.location.pathname;

// CSS pixels per millimeter (CSS defines 96px per inch).
const PX_PER_MM = 96 / 25.4;

/**
 * Converts millimeters to CSS pixels.
 * @param {number} mm Distance in millimeters.
 * @returns {number} Distance in CSS pixels.
 */
function mmToPx(mm) {
    return mm * PX_PER_MM;
}

/**
 * Loads the margin guide settings (falls back to all sides disabled, 10 mm).
 * @returns {Object<string, {enabled: boolean, mm: number}>} Settings per side.
 */
function loadMarginGuideSettings() {
    let stored = null;
    try {
        stored = JSON.parse(sessionStorage.getItem(MARGIN_GUIDE_STORAGE_KEY));
    } catch { }

    const settings = {};
    MARGIN_GUIDE_SIDES.forEach(side => {
        const entry = stored && stored[side];
        const mm = entry && Number.isFinite(entry.mm) && entry.mm >= 0 ? entry.mm : MARGIN_GUIDE_DEFAULT_MM;
        settings[side] = { enabled: !!(entry && entry.enabled), mm: mm };
    });
    return settings;
}

/**
 * Saves the margin guide settings to sessionStorage.
 * @param {Object<string, {enabled: boolean, mm: number}>} settings Settings per side.
 * @returns {void}
 */
function saveMarginGuideSettings(settings) {
    try {
        sessionStorage.setItem(MARGIN_GUIDE_STORAGE_KEY, JSON.stringify(settings));
    } catch { }
}

// Current settings, shared by the toolbar and the snapping logic.
const marginGuideSettings = loadMarginGuideSettings();

/**
 * Adds one (hidden) margin guide line per side to every page that does not have them yet.
 * @returns {void}
 */
function createMarginGuides() {
    document.querySelectorAll('.page').forEach(page => {
        MARGIN_GUIDE_SIDES.forEach(side => {
            if (page.querySelector(`:scope > .margin-guide-line.margin-${side}`)) return;

            const line = document.createElement('div');
            line.className = `margin-guide-line margin-${side} report-ignore`;
            page.appendChild(line);
        });
    });
}

/**
 * Shows/positions or hides the guide line of one side on every page.
 * @param {string} side One of 'left', 'right', 'top', 'bottom'.
 * @returns {void}
 */
function renderMarginGuide(side) {
    const { enabled, mm } = marginGuideSettings[side];
    const offset = `${mmToPx(mm)}px`;

    document.querySelectorAll(`.page > .margin-guide-line.margin-${side}`).forEach(line => {
        line.style.display = enabled ? 'block' : 'none';
        line.style.left = line.style.right = line.style.top = line.style.bottom = '';
        line.style[side] = offset;
    });
}

/**
 * Updates the setting of one side, re-renders its lines and saves the settings.
 * @param {string} side One of 'left', 'right', 'top', 'bottom'.
 * @param {boolean} enabled Whether the line is shown.
 * @param {number} mm Distance from the page edge in millimeters (negative values are clamped to 0).
 * @returns {void}
 */
function setMarginGuide(side, enabled, mm) {
    if (!marginGuideSettings[side]) return;

    marginGuideSettings[side] = {
        enabled: !!enabled,
        mm: Number.isFinite(mm) ? Math.max(0, mm) : MARGIN_GUIDE_DEFAULT_MM
    };
    renderMarginGuide(side);
    saveMarginGuideSettings(marginGuideSettings);
}

/**
 * Creates the guide lines and applies the stored settings to all pages.
 * @returns {void}
 */
function applyMarginGuideSettings() {
    createMarginGuides();
    MARGIN_GUIDE_SIDES.forEach(renderMarginGuide);
}

/**
 * Checks whether an element is currently displayed.
 * @param {Element|null} element The element.
 * @returns {boolean} True when the element exists and is not `display: none`.
 */
function isGuideVisible(element) {
    return !!element && window.getComputedStyle(element).display !== 'none';
}

/**
 * Collects all visible snapping lines of a page.
 * Center lines snap the element's start edge, end edge or center (in that priority),
 * margin lines snap the matching edge (left line -> left edge, ...) or the element's center.
 * @param {HTMLElement} page The page.
 * @param {DOMRect} pageRect Bounding rect of the page (used for the center lines, as before).
 * @returns {{x: Array<{pos: number, edges: string[]}>, y: Array<{pos: number, edges: string[]}>}}
 *          Vertical lines (x positions) and horizontal lines (y positions).
 */
function getVisibleSnapLines(page, pageRect) {
    const lines = { x: [], y: [] };

    if (isGuideVisible(page.querySelector('.vertical-middle-line'))) {
        lines.x.push({ pos: pageRect.width / 2, edges: ['start', 'end', 'center'] });
    }
    if (isGuideVisible(page.querySelector('.horizontal-middle-line'))) {
        lines.y.push({ pos: pageRect.height / 2, edges: ['start', 'end', 'center'] });
    }

    const margin = side => mmToPx(marginGuideSettings[side].mm);
    const visible = side => isGuideVisible(page.querySelector(`:scope > .margin-guide-line.margin-${side}`));

    if (visible('left')) lines.x.push({ pos: margin('left'), edges: ['start', 'center'] });
    if (visible('right')) lines.x.push({ pos: page.offsetWidth - margin('right'), edges: ['end', 'center'] });
    if (visible('top')) lines.y.push({ pos: margin('top'), edges: ['start', 'center'] });
    if (visible('bottom')) lines.y.push({ pos: page.offsetHeight - margin('bottom'), edges: ['end', 'center'] });

    // User-defined guides (custom-guides.js); only present in editing mode.
    if (typeof customGuides !== 'undefined') {
        customGuides.forEach(guide => {
            const target = guide.orientation === 'vertical' ? lines.x : lines.y;
            target.push({ pos: mmToPx(guide.mm), edges: ['start', 'end', 'center'] });
        });
    }

    return lines;
}

/**
 * Snaps one axis to the given lines. Per line the edges are tried in order; across
 * lines the closest match within SNAP_THRESHOLD wins.
 * @param {number} start Element start (left or top) offset.
 * @param {number} size Element size (width or height) on this axis.
 * @param {Array<{pos: number, edges: string[]}>} lines Lines of this axis.
 * @param {number} threshold Snap distance in px.
 * @returns {{value: number, snapped: boolean}} Snapped start offset.
 */
function snapAxisToLines(start, size, lines, threshold) {
    const anchors = { start: 0, end: size, center: size / 2 };
    let best = null;

    lines.forEach(line => {
        const edge = line.edges.find(e => Math.abs(start + anchors[e] - line.pos) < threshold);
        if (!edge) return;

        const distance = Math.abs(start + anchors[edge] - line.pos);
        if (!best || distance < best.distance) {
            best = { distance, value: line.pos - anchors[edge] };
        }
    });

    return best ? { value: best.value, snapped: true } : { value: start, snapped: false };
}

/**
 * Builds the "Margin guides" toolbar button plus its settings panel.
 * @returns {HTMLElement} Container holding the button and the (initially hidden) panel.
 */
function createMarginGuidesControl() {
    const container = document.createElement('div');
    container.className = 'pdf-margin-guides-control';

    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'pdf-editing-margin-guides';
    button.title = 'Margin guides (Ctrl + drag snaps to them)';
    button.innerHTML =
        '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">' +
        '<rect x="3" y="3" width="18" height="18" rx="2"/><rect x="7" y="7" width="10" height="10" stroke-dasharray="2 2"/></svg>' +
        '<span>Margin guides</span>';

    const panel = document.createElement('div');
    panel.className = 'pdf-margin-guides-panel';
    panel.style.display = 'none';

    const labels = { left: 'Left', right: 'Right', top: 'Top', bottom: 'Bottom' };

    MARGIN_GUIDE_SIDES.forEach(side => {
        const row = document.createElement('label');
        row.className = 'pdf-margin-guides-row';

        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.checked = marginGuideSettings[side].enabled;

        const name = document.createElement('span');
        name.textContent = labels[side];

        const input = document.createElement('input');
        input.type = 'number';
        input.min = '0';
        input.step = '1';
        input.className = 'pdf-margin-guides-mm';
        input.value = marginGuideSettings[side].mm;

        const unit = document.createElement('span');
        unit.textContent = 'mm';

        const update = () => {
            setMarginGuide(side, checkbox.checked, parseFloat(input.value));
            updateButton();
        };
        checkbox.addEventListener('change', update);
        input.addEventListener('input', () => {
            if (input.value !== '' && !checkbox.checked) checkbox.checked = true;
            update();
        });

        row.append(checkbox, name, input, unit);
        panel.appendChild(row);
    });

    const updateButton = () => {
        button.classList.toggle('active', MARGIN_GUIDE_SIDES.some(s => marginGuideSettings[s].enabled));
    };

    button.addEventListener('click', () => {
        panel.style.display = panel.style.display === 'none' ? 'flex' : 'none';
    });

    if (typeof createCustomGuidesSection === 'function') {
        panel.appendChild(createCustomGuidesSection());
    }

    container.append(button, panel);
    updateButton();
    return container;
}
