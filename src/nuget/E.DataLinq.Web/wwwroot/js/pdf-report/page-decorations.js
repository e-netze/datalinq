/*
 * PDF report - page decorations.
 *
 * Adds page numbers and date/time stamps to every page, configured through the
 * data attributes of the `.pdf-report-options` element.
 */

/**
 * Adds automatic page numbers (`p.page-number.position-N`) to every page.
 * Reads `data-type` (0 = "n/total", else "n"), `data-skipPages` and `data-position`.
 * @returns {void}
 */
function addPageNumbers() {
    const optionsDiv = document.querySelector('.pdf-report-options');

    if (!optionsDiv) return;

    const type = parseInt(optionsDiv.getAttribute('data-type')) || 0;
    const skipPages = parseInt(optionsDiv.getAttribute('data-skipPages')) || 0;
    const position = parseInt(optionsDiv.getAttribute('data-position')) || 0;

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

        page.appendChild(p);
    });
}
