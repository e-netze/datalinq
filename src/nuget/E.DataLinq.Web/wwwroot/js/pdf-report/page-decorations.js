/*
 * PDF report - page decorations.
 *
 * Adds page numbers and date/time stamps to every page, configured through the
 * data attributes of the `.pdf-report-options` element.
 */

/**
 * Shifts a decoration by the configured pixel offset (x: + right, y: + down).
 * @param {HTMLElement} p
 * @param {number} position 0 = left, 1 = center, 2 = right
 * @param {number} offsetX
 * @param {number} offsetY
 * @returns {void}
 */
function applyDecorationOffset(p, position, offsetX, offsetY) {
    if (offsetX) {
        if (position === 2) p.style.right = `${-offsetX}px`;
        else if (position === 1) p.style.left = `calc(50% + ${offsetX}px)`;
        else p.style.left = `${offsetX}px`;
    }
    if (offsetY) p.style.bottom = `${-offsetY}px`;
}

/**
 * Adds automatic page numbers
 * Reads `data-type` (0 = "n/total", else "n"), `data-skipPages` and `data-position`.
 * @returns {void}
 */
function addPageNumbers() {
    const optionsDiv = document.querySelector('.pdf-report-options');

    if (!optionsDiv) return;

    const type = parseInt(optionsDiv.getAttribute('data-type')) || 0;
    const skipPages = parseInt(optionsDiv.getAttribute('data-skipPages')) || 0;
    const position = parseInt(optionsDiv.getAttribute('data-position')) || 0;
    const offsetX = parseInt(optionsDiv.getAttribute('data-offset-x')) || 0;
    const offsetY = parseInt(optionsDiv.getAttribute('data-offset-y')) || 0;

    const pages = document.querySelectorAll('.page');
    const total = pages.length;

    pages.forEach((page, index) => {
        const p = document.createElement('p');

        const currentPage = index + 1 - skipPages;

        if (index < skipPages) return;

        p.textContent = type === 0
            ? `${currentPage}/${total - skipPages}`
            : `${currentPage}`;

        p.classList.add('page-number', `position-${position}`);
        applyDecorationOffset(p, position, offsetX, offsetY);

        page.appendChild(p);
    });
}

/**
 * Adds the configured date/time text (`p.page-dateTime.position-N`) to every page.
 * Reads `data-datetime-format`, `data-datetime-skipPages` and `data-datetime-position`.
 * @returns {void}
 */
function addDateTime() {
    const optionsDiv = document.querySelector('.pdf-report-options');

    if (!optionsDiv) return;

    const skipPages = parseInt(optionsDiv.getAttribute('data-datetime-skipPages')) || 0;
    const position = parseInt(optionsDiv.getAttribute('data-datetime-position')) || 0;
    const offsetX = parseInt(optionsDiv.getAttribute('data-datetime-offset-x')) || 0;
    const offsetY = parseInt(optionsDiv.getAttribute('data-datetime-offset-y')) || 0;
    const dateTime = optionsDiv.getAttribute('data-datetime-format') || '';

    const pages = document.querySelectorAll('.page');
    const total = pages.length;

    pages.forEach((page, index) => {
        const p = document.createElement('p');

        const currentPage = index + 1 - skipPages;

        if (index < skipPages) return;

        const now = new Date();
        p.textContent = dateTime;

        p.classList.add('page-dateTime', `position-${position}`);
        applyDecorationOffset(p, position, offsetX, offsetY);

        page.appendChild(p);
    });
}
