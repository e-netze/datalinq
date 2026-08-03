/*
 * PDF report generation (client side).
 *
 * Runs on report pages rendered in PDF mode. It paginates the rendered HTML into
 * fixed-size pages, loads per-page templates, adds page numbers, and renders the
 * result to a downloadable PDF using html-to-image (rasterize) + pdf-lib (assemble).
 */

// --- Configuration -------------------------------------------------------------

// Rendering quality presets, selected via the `.main[quality]` attribute.
const PDF_QUALITY_PRESETS = {
    Best: { pixelRatio: 2, quality: 1 },
    High: { pixelRatio: 2, quality: 0.92 },
    Medium: { pixelRatio: 1.5, quality: 0.85 },
    Low: { pixelRatio: 1.5, quality: 0.75 },
    Preview: { pixelRatio: 1, quality: 0.80 },
};
const PDF_DEFAULT_QUALITY = 'High';

// Paper sizes in PDF points [width, height], keyed by the `size-a1..a6` page class.
const PDF_PAPER_DIMENSIONS = {
    A1: { portrait: [1683.78, 2383.94], landscape: [2383.94, 1683.78] },
    A2: { portrait: [1190.55, 1683.78], landscape: [1683.78, 1190.55] },
    A3: { portrait: [841.89, 1190.55], landscape: [1190.55, 841.89] },
    A4: { portrait: [595.28, 841.89], landscape: [841.89, 595.28] },
    A5: { portrait: [419.53, 595.28], landscape: [595.28, 419.53] },
    A6: { portrait: [297.64, 419.53], landscape: [419.53, 297.64] },
};
const PDF_DEFAULT_PAPER_SIZE = 'A4';

// Pagination defaults and safety limits.
const PDF_DEFAULT_MARGIN = 10;              // px, used when a dynamic page defines no margins
const MAX_PAGINATION_ITERATIONS = 100;      // guards against infinite pagination loops

// Auto-download timing (milliseconds).
const AUTO_DOWNLOAD_START_DELAY = 1000;     // wait before the automatic download starts
const AUTO_DOWNLOAD_CLOSE_DELAY = 100;      // wait before showing the "you can close" message

// -------------------------------------------------------------------------------

// Entry point: wait until the DataLinq core client-side logic has finished
// rendering the report, then wire up the download button and build the pages.
dataLinq.events.on('onpageloaded', function () {

    // On-page "Download PDF" button (rendered next to the report).
    const downloadBtn = document.getElementById('downloadBtn');
    if (downloadBtn) {
        downloadBtn.addEventListener('click', async function () {
            this.textContent = 'Generating PDF ...';
            this.disabled = true;

            showLoadingOverlay();

            await new Promise(resolve => requestAnimationFrame(() =>
                requestAnimationFrame(resolve)
            ));

            await downloadPDFMethod();
        });
    }

    // Pagination pipeline (order matters): flatten finished includes, split
    // overflowing content across pages, load per-page templates, then number pages.
    unwrapFinishedIncludes();
    splitAllTables();
    initializeTemplateLoader();
    addPageNumbers();
});


// Auto-download: DLH.PrintPDF opens the report with ?_autoDownload=true so the PDF
// is generated and downloaded automatically, then the tab closes itself.
const urlParams = new URLSearchParams(window.location.search);
if (urlParams.get('_autoDownload') === 'true') {
    document.body.style.opacity = '0';

    setTimeout(async () => {
        await downloadPDFMethod();

        window.close();

        setTimeout(() => {
            document.body.innerHTML = '<div style="display:flex;justify-content:center;align-items:center;height:100vh;font-family:sans-serif;">PDF download started. You can close this window.</div>';
            document.body.style.opacity = '1';
        }, AUTO_DOWNLOAD_CLOSE_DELAY);
    }, AUTO_DOWNLOAD_START_DELAY);
}

// Renders every `.page` element to a JPEG image and assembles the images into a PDF.
async function downloadPDFMethod() {
    const pages = [...document.querySelectorAll('.page')];

    try {
        let completed = 0;

        const { pixelRatio, quality } = getQuality();

        const pageImages = await Promise.all(
            pages.map(async (page) => {
                const dataUrl = await htmlToImage.toJpeg(page, {
                    pixelRatio: pixelRatio,
                    quality: quality,
                    backgroundColor: '#ffffff',
                    skipFonts: false,
                    onclone: (clonedDoc) => {
                        // Drop cross-origin stylesheets (e.g. Leaflet): reading their
                        // cssRules would throw a CORS error inside html-to-image.
                        const links = clonedDoc.querySelectorAll('link[rel="stylesheet"]');
                        links.forEach(link => {
                            try {
                                const sheet = [...document.styleSheets]
                                    .find(s => s.href === link.href);
                                if (sheet) void sheet.cssRules;
                            } catch {
                                link.remove();
                            }
                        });
                    }
                });
                updateLoadingProgress(++completed, pages.length);

                const sizeClass = [...page.classList].find(c => /^size-a[1-6]$/i.test(c));
                const paperSize = sizeClass ? sizeClass.replace(/^size-/i, '').toUpperCase() : PDF_DEFAULT_PAPER_SIZE;
                return { dataUrl, isHorizontal: page.classList.contains('horizontal'), paperSize: paperSize };
            })
        );

        const pdfDoc = await PDFLib.PDFDocument.create();
        pdfDoc.setCreator('Energienetze Steiermark');
        pdfDoc.setProducer('DataLinq');

        for (const { dataUrl, isHorizontal, paperSize } of pageImages) {
            const paperDimensions = getPaperDimensions(paperSize);
            const [w, h] = isHorizontal ? paperDimensions.landscape : paperDimensions.portrait;

            const jpegBytes = Uint8Array.from(
                atob(dataUrl.slice(dataUrl.indexOf(',') + 1)),
                c => c.charCodeAt(0)
            );

            const jpegImage = await pdfDoc.embedJpg(jpegBytes);
            const page = pdfDoc.addPage([w, h]);
            page.drawImage(jpegImage, { x: 0, y: 0, width: w, height: h });
        }

        const pdfBytes = await pdfDoc.save();
        const blob = new Blob([pdfBytes], { type: 'application/pdf' });
        const blobUrl = URL.createObjectURL(blob);

        const fileName = document.querySelector('.main')?.getAttribute('fileName')
            ?? 'dataLinqReport.pdf';

        Object.assign(document.createElement('a'), {
            href: blobUrl,
            download: fileName
        }).click();

        URL.revokeObjectURL(blobUrl);
    } catch (error) {
        console.error('PDF generation failed:', error);
    } finally {
        removeLoadingOverlay();

        if (window.parent !== window) {
            window.parent.postMessage({ type: 'pdfDownloadComplete' }, '*');
        } else {
            const btn = document.getElementById('downloadBtn');
            if (btn) { btn.textContent = 'Download PDF'; btn.disabled = false; }
        }
    }
}

const getQuality = () => {
    const qualityKey = document.querySelector('.main')?.getAttribute('quality') ?? PDF_DEFAULT_QUALITY;
    return PDF_QUALITY_PRESETS[qualityKey] ?? PDF_QUALITY_PRESETS[PDF_DEFAULT_QUALITY];
};

const getPaperDimensions = (size) => {
    return PDF_PAPER_DIMENSIONS[size] ?? PDF_PAPER_DIMENSIONS[PDF_DEFAULT_PAPER_SIZE];
}

// Adds automatic page numbers based on the `.pdf-report-options` settings.
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

// Loads per-page templates referenced via the `datalinq-pdfreport-template` attribute.
// Pages that share a template are grouped so each template is only requested once.
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

function makeTemplateRequest(pagesArray, templateName) {
    dataLinq.getTemplate(templateName).then(function (data) {
        pagesArray.forEach(function (page) {
            $(page).append(data);
        });
    }).catch(function (error) {
        console.error(`Error loading template "${templateName}":`, error);
    });
}

// Pagination: flatten finished includes and split content that overflows a page
// onto continuation pages (tables are split row by row, other elements are moved).
function unwrapFinishedIncludes() {
    const pages = Array.from(document.querySelectorAll('.page'));

    pages.forEach(page => {
        const wrappers = Array.from(page.querySelectorAll('div.datalinq-include.finished'));

        wrappers.forEach(wrapper => {
            const parent = wrapper.parentNode;
            if (!parent) return;

            while (wrapper.firstChild) {
                parent.insertBefore(wrapper.firstChild, wrapper);
            }

            parent.removeChild(wrapper);
        });
    });
}

function splitAllTables() {
    const pagesContainer = document.getElementById('pagesContainer');
    if (!pagesContainer) return;

    const originalPageWrappers = Array.from(pagesContainer.querySelectorAll('.page-wrapper.dynamic'));
    originalPageWrappers.forEach(pageWrapper => paginateDynamicPage(pageWrapper, pagesContainer));
}

// Repeatedly splits a dynamic page while its content overflows, chaining onto each
// newly created continuation page until nothing overflows (bounded by MAX_PAGINATION_ITERATIONS).
function paginateDynamicPage(startWrapper, pagesContainer) {
    let currentWrapper = startWrapper;
    let guard = 0;

    while (currentWrapper && guard++ < MAX_PAGINATION_ITERATIONS) {
        const page = currentWrapper.querySelector('.page');
        if (!page) return;

        const margins = getDynamicMargins(currentWrapper);
        const overflowElement = getFirstOverflowElement(page, margins.defaultMarginBottom);
        if (!overflowElement) return;

        const isTable = overflowElement.tagName.toLowerCase() === 'table';
        const nextWrapper = isTable
            ? splitOverflowingTable(overflowElement, page, currentWrapper, pagesContainer, margins)
            : moveOverflowingElementsToNextPage(overflowElement, page, currentWrapper, pagesContainer);

        if (!nextWrapper || nextWrapper === currentWrapper) {
            return;
        }

        currentWrapper = nextWrapper;
    }
}

// Returns the first laid-out child whose bottom edge crosses the page's usable area
// (ignoring `report-ignore` decorations such as spacers, headers and page numbers).
function getFirstOverflowElement(page, bottomMargin = 0) {
    const pageRect = page.getBoundingClientRect();
    const maxBottom = pageRect.top + pageRect.height - bottomMargin;

    return Array.from(page.children)
        .filter(child => !child.classList.contains('report-ignore'))
        .find(child => child.getBoundingClientRect().bottom > maxBottom + 0.5);
}

// Splits an overflowing table: keeps the rows that fit on the current page and moves
// the rest (plus any following siblings) onto a continuation page with a repeated header.
// Falls back to moving the whole table when it cannot be split meaningfully.
function splitOverflowingTable(table, page, currentWrapper, pagesContainer, margins) {
    const tbody = table.querySelector('tbody');
    if (!tbody) {
        return moveOverflowingElementsToNextPage(table, page, currentWrapper, pagesContainer);
    }

    const rows = Array.from(tbody.querySelectorAll('tr'));
    if (rows.length === 0) {
        return moveOverflowingElementsToNextPage(table, page, currentWrapper, pagesContainer);
    }

    const tableStyles = window.getComputedStyle(table);
    const bottomMargin = margins.defaultMarginBottom;

    let thead = table.querySelector('thead');
    let headerRow = null;
    let theadHeight = 0;
    let startRowIndex = 0;

    if (thead) {
        theadHeight = thead.getBoundingClientRect().height;
    } else {
        const firstRow = rows[0];
        if (firstRow && firstRow.querySelector('th')) {
            headerRow = firstRow;
            theadHeight = firstRow.getBoundingClientRect().height;
            startRowIndex = 1;
        }
    }

    const dataRows = rows.slice(startRowIndex);
    if (dataRows.length === 0) {
        return moveOverflowingElementsToNextPage(table, page, currentWrapper, pagesContainer);
    }

    const rowGap = getRowGap(tableStyles.borderSpacing);
    const pageRect = page.getBoundingClientRect();
    const tableTop = table.getBoundingClientRect().top - pageRect.top;
    const availableHeight = pageRect.height - tableTop - bottomMargin - theadHeight - rowGap;

    const rowsInCurrentPage = countRowsThatFit(dataRows, 0, availableHeight, rowGap, 0);

    if (rowsInCurrentPage <= 0) {
        return moveOverflowingElementsToNextPage(table, page, currentWrapper, pagesContainer);
    }

    if (rowsInCurrentPage >= dataRows.length) {
        return moveOverflowingElementsToNextPage(table, page, currentWrapper, pagesContainer);
    }

    const overflowRows = dataRows.slice(rowsInCurrentPage);
    overflowRows.forEach(row => row.remove());

    const nextWrapper = createContinuationPageWrapper(page, currentWrapper);
    const nextPage = nextWrapper.querySelector('.page');

    const repeatHeader = currentWrapper.getAttribute('data-dynamic-repeat-header') !== 'false';
    const continuationTable = createContinuationTable(table, thead, headerRow, overflowRows, repeatHeader);
    normalizeFirstMovedElement(continuationTable);
    nextPage.appendChild(continuationTable);
    moveFollowingSiblings(table, nextPage);

    insertPageAfter(currentWrapper, nextWrapper, pagesContainer);
    return nextWrapper;
}

// Moves the overflowing element and every element after it onto a new continuation page.
function moveOverflowingElementsToNextPage(startElement, page, currentWrapper, pagesContainer) {
    const nextWrapper = createContinuationPageWrapper(page, currentWrapper);
    const nextPage = nextWrapper.querySelector('.page');

    let current = startElement;
    let firstMovedElement = null;

    while (current) {
        const next = current.nextElementSibling;
        nextPage.appendChild(current);

        if (!firstMovedElement) {
            firstMovedElement = current;
        }

        current = next;
    }

    normalizeFirstMovedElement(firstMovedElement);

    insertPageAfter(currentWrapper, nextWrapper, pagesContainer);
    return nextWrapper;
}

// Builds an empty continuation page that mirrors the source page's format (orientation,
// paper size, template, ignored decorations) and re-applies the configured top margin.
function createContinuationPageWrapper(sourcePage, sourceWrapper) {
    const newPageWrapper = document.createElement('div');
    newPageWrapper.className = 'page-wrapper';

    if (sourceWrapper.classList.contains('dynamic')) {
        newPageWrapper.classList.add('dynamic');
    }

    if (sourceWrapper.classList.contains('dynamic-use-template')) {
        newPageWrapper.classList.add('dynamic-use-template');
    }

    const margins = getDynamicMargins(sourceWrapper);
    newPageWrapper.setAttribute('data-dynamic-margin-top', margins.defaultMarginTop.toString());
    newPageWrapper.setAttribute('data-dynamic-margin-bottom', margins.defaultMarginBottom.toString());

    if (sourceWrapper.getAttribute('data-dynamic-repeat-header') === 'false') {
        newPageWrapper.setAttribute('data-dynamic-repeat-header', 'false');
    }

    const newPage = document.createElement('div');
    newPage.className = 'page';

    if (sourcePage.classList.contains('horizontal')) {
        newPage.classList.add('horizontal');
    }

    const paperSizeClass = [...sourcePage.classList].find(c => /^size-a[1-6]$/i.test(c));
    if (paperSizeClass) {
        newPage.classList.add(paperSizeClass);
    }

    const template = sourcePage.getAttribute('datalinq-pdfreport-template');
    if (template) {
        newPage.setAttribute('datalinq-pdfreport-template', template);
    }

    Array.from(sourcePage.children)
        .filter(child => child.classList.contains('report-ignore') && !child.classList.contains('dynamic-top-margin-spacer'))
        .forEach(child => newPage.appendChild(child.cloneNode(true)));

    if (margins.defaultMarginTop > 0) {
        const topSpacer = document.createElement('div');
        topSpacer.className = 'report-ignore dynamic-top-margin-spacer';
        topSpacer.style.height = `${margins.defaultMarginTop}px`;
        newPage.appendChild(topSpacer);
    }

    newPageWrapper.appendChild(newPage);
    return newPageWrapper;
}

// Clones the original table (minus its id) so the moved rows keep the same styling,
// repeating the header (thead or a th header row) on the continuation page unless
// repeatHeader is false.
function createContinuationTable(originalTable, thead, headerRow, rows, repeatHeader = true) {
    const newTable = document.createElement('table');

    Array.from(originalTable.attributes).forEach(attr => {
        if (attr.name !== 'id') {
            newTable.setAttribute(attr.name, attr.value);
        }
    });

    if (thead && repeatHeader) {
        newTable.appendChild(thead.cloneNode(true));
    }

    const newTbody = document.createElement('tbody');

    if (headerRow && !thead && repeatHeader) {
        newTbody.appendChild(headerRow.cloneNode(true));
    }

    rows.forEach(row => {
        row.classList.remove('hidden');
        newTbody.appendChild(row);
    });

    newTable.appendChild(newTbody);
    return newTable;
}

function moveFollowingSiblings(referenceElement, targetPage) {
    let current = referenceElement.nextElementSibling;
    let firstMovedElement = null;

    while (current) {
        const next = current.nextElementSibling;
        targetPage.appendChild(current);

        if (!firstMovedElement) {
            firstMovedElement = current;
        }

        current = next;
    }

    normalizeFirstMovedElement(firstMovedElement);
}

function insertPageAfter(currentWrapper, newPageWrapper, pagesContainer) {
    const nextElement = currentWrapper.nextElementSibling;
    if (nextElement) {
        pagesContainer.insertBefore(newPageWrapper, nextElement);
    } else {
        pagesContainer.appendChild(newPageWrapper);
    }
}

// Counts how many consecutive rows fit within availableHeight, never returning fewer
// than minRows so pagination always makes forward progress.
function countRowsThatFit(rows, startIndex, availableHeight, rowGap, minRows = 1) {
    let usedHeight = 0;
    let count = 0;

    for (let i = startIndex; i < rows.length; i++) {
        const rowHeight = getRowOuterHeight(rows[i]) + rowGap;

        if (usedHeight + rowHeight > availableHeight) {
            break;
        }

        usedHeight += rowHeight;
        count++;
    }

    return Math.max(count, minRows);
}

function getRowOuterHeight(row) {
    const styles = window.getComputedStyle(row);
    return row.getBoundingClientRect().height
        + (parseFloat(styles.marginTop) || 0)
        + (parseFloat(styles.marginBottom) || 0);
}

// Extracts the vertical component of a CSS `border-spacing` value (used as the row gap).
function getRowGap(borderSpacing) {
    if (!borderSpacing) return 0;

    const spacing = borderSpacing.toString().trim().split(/\s+/);
    if (spacing.length === 0) return 0;

    const verticalSpacing = spacing.length > 1 ? spacing[1] : spacing[0];
    return parseFloat(verticalSpacing) || 0;
}

function getDynamicMargins(pageWrapper) {
    const defaultMarginTop = parseFloat(pageWrapper.getAttribute('data-dynamic-margin-top'));
    const defaultMarginBottom = parseFloat(pageWrapper.getAttribute('data-dynamic-margin-bottom'));

    return {
        defaultMarginTop: Number.isFinite(defaultMarginTop) ? defaultMarginTop : PDF_DEFAULT_MARGIN,
        defaultMarginBottom: Number.isFinite(defaultMarginBottom) ? defaultMarginBottom : PDF_DEFAULT_MARGIN,
    };
}

function normalizeFirstMovedElement(element) {
    if (!element) return;

    element.style.marginTop = '0';
    element.style.paddingTop = '0';
}

// Loading overlay + progress bar shown while the PDF is being generated.
function showLoadingOverlay() {
    // Prevent creating multiple overlays
    if (document.getElementById('pdf-loading-overlay')) return;

    const overlay = document.createElement('div');
    overlay.id = 'pdf-loading-overlay';

    overlay.innerHTML = `
        <span id="pdf-loader"></span>
        <div id="pdf-loading-text">Preparing PDF...</div>
        <div class="pdf-progress-container">
            <div id="pdf-progress-bar"></div>
        </div>
    `;

    document.body.appendChild(overlay);
}

function updateLoadingProgress(current, total) {
    const bar = document.getElementById('pdf-progress-bar');
    const text = document.getElementById('pdf-loading-text');
    const loader = document.getElementById('pdf-loader');

    if (loader) loader.remove();

    if (bar && text) {
        const percentage = Math.round((current / total) * 100);
        bar.style.width = percentage + '%';
        text.innerText = `Processing page ${current} of ${total} (${percentage}%)`;
    }
}

function removeLoadingOverlay() {
    const overlay = document.getElementById('pdf-loading-overlay');
    if (overlay) {
        overlay.remove();
    }
}