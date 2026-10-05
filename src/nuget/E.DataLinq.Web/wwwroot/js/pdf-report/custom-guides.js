/*
 * PDF report - custom guides (editing mode only).
 *
 * Lets the user add any number of extra snapping lines:
 *   - vertical lines (top to bottom), positioned in mm from the left page edge
 *   - horizontal lines (left to right), positioned in mm from the top page edge
 * Lines are drawn on every page, listed in the editing toolbar and can be removed
 * again. Ctrl + drag snaps an element's start edge, end edge or center to them.
 * The list is kept per report in sessionStorage so it survives live-preview reloads.
 *
 * Loaded after margin-guides.js (uses mmToPx) and before editing.js.
 */

const CUSTOM_GUIDES_STORAGE_KEY = 'datalinq-pdf-custom-guides:' + window.location.pathname;

/**
 * Loads the stored custom guides.
 * @returns {Array<{id: number, orientation: string, mm: number}>} Valid stored guides.
 */
function loadCustomGuides() {
    let stored = null;
    try {
        stored = JSON.parse(sessionStorage.getItem(CUSTOM_GUIDES_STORAGE_KEY));
    } catch { }

    if (!Array.isArray(stored)) return [];

    return stored
        .filter(g => g && (g.orientation === 'vertical' || g.orientation === 'horizontal') && Number.isFinite(g.mm) && g.mm >= 0)
        .map((g, i) => ({ id: i + 1, orientation: g.orientation, mm: g.mm }));
}

/**
 * Saves the custom guides to sessionStorage.
 * @returns {void}
 */
function saveCustomGuides() {
    try {
        sessionStorage.setItem(CUSTOM_GUIDES_STORAGE_KEY,
            JSON.stringify(customGuides.map(g => ({ orientation: g.orientation, mm: g.mm }))));
    } catch { }
}

// Current custom guides, shared with getVisibleSnapLines (margin-guides.js).
const customGuides = loadCustomGuides();
let nextCustomGuideId = customGuides.length + 1;

/**
 * Draws all custom guides on every page (replacing previously drawn ones).
 * @returns {void}
 */
function renderCustomGuides() {
    document.querySelectorAll('.page > .custom-guide-line').forEach(line => line.remove());

    document.querySelectorAll('.page').forEach(page => {
        customGuides.forEach(guide => {
            const line = document.createElement('div');
            line.className = `custom-guide-line ${guide.orientation} report-ignore`;
            line.dataset.guideId = guide.id;
            line.style[guide.orientation === 'vertical' ? 'left' : 'top'] = `${mmToPx(guide.mm)}px`;
            page.appendChild(line);
        });
    });
}

/**
 * Adds a custom guide, redraws and saves.
 * @param {string} orientation 'vertical' (mm from left) or 'horizontal' (mm from top).
 * @param {number} mm Distance from the left/top page edge in millimeters.
 * @returns {boolean} False when the input is invalid.
 */
function addCustomGuide(orientation, mm) {
    if (!Number.isFinite(mm) || mm < 0) return false;

    customGuides.push({ id: nextCustomGuideId++, orientation: orientation, mm: mm });
    renderCustomGuides();
    saveCustomGuides();
    return true;
}

/**
 * Removes a custom guide, redraws and saves.
 * @param {number} id Id of the guide to remove.
 * @returns {void}
 */
function removeCustomGuide(id) {
    const index = customGuides.findIndex(g => g.id === id);
    if (index < 0) return;

    customGuides.splice(index, 1);
    renderCustomGuides();
    saveCustomGuides();
}

/**
 * Builds the "Custom guides" section for the margin guides panel (add form + list of guides).
 * @returns {HTMLElement} The section element.
 */
function createCustomGuidesSection() {
    const section = document.createElement('div');
    section.className = 'pdf-custom-guides-section';

    const title = document.createElement('div');
    title.className = 'pdf-custom-guides-title';
    title.textContent = 'Custom guides';

    const form = document.createElement('div');
    form.className = 'pdf-margin-guides-row';

    const orientation = document.createElement('select');
    orientation.className = 'pdf-custom-guides-orientation';
    orientation.innerHTML =
        '<option value="vertical">Vertical (from left)</option>' +
        '<option value="horizontal">Horizontal (from top)</option>';

    const input = document.createElement('input');
    input.type = 'number';
    input.min = '0';
    input.step = '1';
    input.className = 'pdf-margin-guides-mm';
    input.value = '10';

    const unit = document.createElement('span');
    unit.textContent = 'mm';

    const addButton = document.createElement('button');
    addButton.type = 'button';
    addButton.className = 'pdf-custom-guides-add';
    addButton.title = 'Add guide';
    addButton.textContent = '+';

    form.append(orientation, input, unit, addButton);

    const list = document.createElement('ul');
    list.className = 'pdf-custom-guides-list';

    /**
     * Rebuilds the list of guides and the button state.
     * @returns {void}
     */
    const renderList = () => {
        list.innerHTML = '';

        if (customGuides.length === 0) {
            const empty = document.createElement('li');
            empty.className = 'pdf-custom-guides-empty';
            empty.textContent = 'No custom guides';
            list.appendChild(empty);
        }

        customGuides.forEach(guide => {
            const item = document.createElement('li');

            const text = document.createElement('span');
            text.textContent = guide.orientation === 'vertical'
                ? `Vertical: ${guide.mm} mm from left`
                : `Horizontal: ${guide.mm} mm from top`;

            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'pdf-custom-guides-remove';
            remove.title = 'Remove guide';
            remove.innerHTML = '&times;';
            remove.addEventListener('click', () => {
                removeCustomGuide(guide.id);
                renderList();
            });

            item.append(text, remove);
            list.appendChild(item);
        });
    };

    const add = () => {
        if (addCustomGuide(orientation.value, parseFloat(input.value))) {
            renderList();
        }
    };
    addButton.addEventListener('click', add);
    input.addEventListener('keydown', (e) => { if (e.key === 'Enter') add(); });

    section.append(title, form, list);
    renderList();
    return section;
}

/**
 * Builds the "Custom guides" toolbar button with its own panel (next to center/margin guides).
 * @returns {HTMLElement} Container holding the button and the (initially hidden) panel.
 */
function createCustomGuidesControl() {
    const container = document.createElement('div');
    container.className = 'pdf-margin-guides-control pdf-custom-guides-control';

    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'pdf-editing-custom-guides';
    button.title = 'Custom guides (Ctrl + drag snaps to them)';
    button.innerHTML =
        '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">' +
        '<rect x="3" y="3" width="18" height="18" rx="2"/><line x1="8" y1="3" x2="8" y2="21" stroke-dasharray="2 2"/><line x1="3" y1="15" x2="21" y2="15" stroke-dasharray="2 2"/></svg>' +
        '<span>Custom guides</span>';

    const panel = document.createElement('div');
    panel.className = 'pdf-margin-guides-panel';
    panel.style.display = 'none';

    const section = createCustomGuidesSection();
    section.classList.add('standalone');
    panel.appendChild(section);

    button.addEventListener('click', () => {
        panel.style.display = panel.style.display === 'none' ? 'flex' : 'none';
    });

    container.append(button, panel);
    return container;
}
