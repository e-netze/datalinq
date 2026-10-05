/*
 * PDF report - page templates.
 *
 * Loads per-page templates referenced via the `datalinq-pdfreport-template` attribute.
 */

/**
 * Groups pages by template name so each template is only requested once, then
 * loads and appends every template.
 * @returns {void}
 */
function initializeTemplateLoader() {
    const pagesByTemplate = new Map();

    document.querySelectorAll('.page').forEach(page => {
        const templateName = page.getAttribute('datalinq-pdfreport-template');
        if (!templateName) return;

        if (!pagesByTemplate.has(templateName)) {
            pagesByTemplate.set(templateName, []);
        }

        pagesByTemplate.get(templateName).push(page);
    });

    pagesByTemplate.forEach((pages, templateName) => makeTemplateRequest(pages, templateName));
}

/**
 * Fetches a template and appends a copy of it to every given page (asynchronous).
 * Template draggables are moved to the start of the page and their widths resolved.
 * @param {HTMLElement[]} pagesArray Pages that use the template.
 * @param {string} templateName Name of the template to load via dataLinq.getTemplate.
 * @returns {void}
 */
function makeTemplateRequest(pagesArray, templateName) {
    dataLinq.getTemplate(templateName).then(function (data) {
        pagesArray.forEach(function (page) {
            const $content = typeof data === 'string' ? $($.parseHTML(data, document, true)) : $(data);
            $(page).append($content);

            // Template draggables are absolutely positioned relative to their static
            // position; move them to the start of the page so they are not pushed
            // below the (now flowing) page content.
            $content.filter('.element').get().reverse().forEach(el => page.prepend(el));

            resolveDraggableWidths(page);
        });
    }).catch(function (error) {
        console.error(`Error loading template "${templateName}":`, error);
    });
}
