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

// Label of the download button (icon + text).
const DOWNLOAD_BUTTON_HTML =
    '<svg class="pdf-download-icon" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
    '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>' +
    '<span>Download</span>';

// Label of the print button (icon + text).
const PRINT_BUTTON_HTML =
    '<svg class="pdf-print-icon" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
    '<polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>' +
    '<span>Print</span>';

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
    applyManualPageBreaks();
    splitAllTables();
    initializeTemplateLoader();
    addPageNumbers();
    addDateTime();
    initializePageNavigator();
});

// Floating "page X / Y" navigator with prev/next buttons and an editable page input.
function initializePageNavigator() {
    if (urlParams.get('_autoDownload') === 'true') return;

    const pages = () => [...document.querySelectorAll('.page')];
    if (pages().length === 0) return;

    const nav = document.createElement('div');
    nav.className = 'pdf-page-navigator';
    nav.innerHTML =
        '<div class="pdf-nav-controls">' +
        '<button type="button" class="pdf-nav-prev" title="Previous page">&#9650;</button>' +
        '<input type="number" class="pdf-nav-input" min="1" />' +
        '<span class="pdf-nav-total"></span>' +
        '<button type="button" class="pdf-nav-next" title="Next page">&#9660;</button>' +
        '</div>' +
        '<div class="pdf-nav-controls pdf-zoom-controls">' +
        '<button type="button" class="pdf-zoom-out" title="Zoom out">&minus;</button>' +
        '<button type="button" class="pdf-zoom-reset" title="Reset zoom">100%</button>' +
        '<button type="button" class="pdf-zoom-in" title="Zoom in">+</button>' +
        '</div>';
    if (document.getElementById('downloadBtnContainer') && !document.getElementById('downloadBtn')) {
        const button = document.createElement('button');
        button.type = 'button';
        button.id = 'downloadBtn';
        button.innerHTML = DOWNLOAD_BUTTON_HTML;
        button.classList.add('datalinq-button-pdf');
        button.addEventListener('click', async function () {
            this.textContent = 'Generating PDF ...';
            this.disabled = true;

            showLoadingOverlay();

            await new Promise(resolve => requestAnimationFrame(() =>
                requestAnimationFrame(resolve)
            ));

            await downloadPDFMethod();
        });
        nav.appendChild(button);
    }

    const printButton = document.createElement('button');
    printButton.type = 'button';
    printButton.id = 'printBtn';
    printButton.innerHTML = PRINT_BUTTON_HTML;
    printButton.classList.add('datalinq-button-pdf');
    printButton.addEventListener('click', () => window.print());
    nav.appendChild(printButton);

    document.body.appendChild(nav);

    const input = nav.querySelector('.pdf-nav-input');
    const totalSpan = nav.querySelector('.pdf-nav-total');
    let current = 0;
    let pageList = pages();
    const visibleHeights = new Map();

    totalSpan.textContent = `/ ${pageList.length}`;
    input.max = pageList.length;

    const setCurrent = (n) => {
        if (n === current) return;
        current = n;
        if (document.activeElement !== input) input.value = current;
    };

    const update = () => {
        let best = current || 1, bestVisible = 0;
        visibleHeights.forEach((h, idx) => {
            if (h > bestVisible) { bestVisible = h; best = idx + 1; }
        });
        setCurrent(best);
    };

    // IntersectionObserver avoids measuring every page on each scroll frame.
    const thresholds = Array.from({ length: 21 }, (_, i) => i / 20);
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            const idx = pageList.indexOf(entry.target);
            if (idx < 0) return;
            if (entry.isIntersecting) visibleHeights.set(idx, entry.intersectionRect.height);
            else visibleHeights.delete(idx);
        });
        update();
    }, { threshold: thresholds });
    pageList.forEach(p => observer.observe(p));

    const goTo = (n) => {
        const target = Math.min(Math.max(1, n), pageList.length);
        const distance = Math.abs(target - (current || 1));
        pageList[target - 1].scrollIntoView({ behavior: distance > 2 ? 'auto' : 'smooth', block: 'center' });
        input.value = target;
    };

    nav.querySelector('.pdf-nav-prev').addEventListener('click', () => goTo(current - 1));
    nav.querySelector('.pdf-nav-next').addEventListener('click', () => goTo(current + 1));

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            const n = parseInt(input.value);
            if (!isNaN(n)) goTo(n);
            input.blur();
        } else if (e.key === 'Escape') {
            input.value = current;
            input.blur();
        }
    });
    input.addEventListener('focus', () => input.select());
    input.addEventListener('blur', () => { input.value = current; });

    setCurrent(1);

    initializeZoom(nav, () => pageList[(current || 1) - 1], saveViewState);

    restoreViewState(pageList);

    let saveTimer = null;
    window.addEventListener('scroll', () => {
        clearTimeout(saveTimer);
        saveTimer = setTimeout(() => saveViewState(pageList), 200);
    }, { passive: true });
    window.addEventListener('pagehide', () => saveViewState(pageList));
}

// View state (zoom + position) is kept per report in sessionStorage, so reloads
// (e.g. the live preview after saving code) return to the same zoom and page.
const PDF_VIEW_STATE_KEY = 'datalinq-pdf-view:' + window.location.pathname;

// The position is stored as page index + relative offset of the viewport center within
// that page, so it survives zoom changes and small layout differences.
function saveViewState(pageList) {
    pageList = pageList || [...document.querySelectorAll('.page')];
    if (pageList.length === 0) return;

    const centerY = window.innerHeight / 2;
    let pageIndex = 0, bestDistance = Infinity, offset = 0;
    pageList.forEach((page, i) => {
        const r = page.getBoundingClientRect();
        const distance = centerY < r.top ? r.top - centerY : centerY > r.bottom ? centerY - r.bottom : 0;
        if (distance < bestDistance) {
            bestDistance = distance;
            pageIndex = i;
            offset = r.height > 0 ? Math.min(Math.max((centerY - r.top) / r.height, 0), 1) : 0;
        }
    });

    try {
        sessionStorage.setItem(PDF_VIEW_STATE_KEY, JSON.stringify({ zoom: pdfZoomLevel, page: pageIndex, offset: offset }));
    } catch { }
}

function restoreViewState(pageList) {
    let state = null;
    try {
        state = JSON.parse(sessionStorage.getItem(PDF_VIEW_STATE_KEY));
    } catch { }
    if (!state) return;

    if (typeof state.zoom === 'number' && state.zoom !== 1) {
        setZoomLevel(state.zoom);
    }

    const page = pageList[Math.min(Math.max(0, state.page | 0), pageList.length - 1)];
    if (!page) return;

    const r = page.getBoundingClientRect();
    const target = r.top + (state.offset || 0) * r.height;
    window.scrollBy({ top: target - window.innerHeight / 2, behavior: 'auto' });

    if (pdfZoomLevel > 1) {
        const root = document.scrollingElement || document.documentElement;
        root.scrollLeft = (root.scrollWidth - root.clientWidth) / 2;
    }
}

// Zoom controls: scales the pages container via CSS zoom and keeps the current page centered.
const PDF_ZOOM_STEPS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3];
let pdfZoomLevel = 1;
let setZoomLevel = () => { };

function initializeZoom(nav, getCurrentPage, onChange) {
    const container = document.getElementById('pagesContainer');
    if (!container) return;

    const resetBtn = nav.querySelector('.pdf-zoom-reset');

    setZoomLevel = (level) => {
        pdfZoomLevel = level;
        container.style.zoom = level;
        resetBtn.textContent = `${Math.round(level * 100)}%`;
    };

    const apply = (level) => {
        const page = getCurrentPage();
        setZoomLevel(level);
        if (page) page.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'center' });
        if (onChange) onChange();
    };

    const step = (dir) => {
        const idx = PDF_ZOOM_STEPS.findIndex(z => z >= pdfZoomLevel - 0.001);
        const next = PDF_ZOOM_STEPS[Math.min(Math.max(0, (idx < 0 ? PDF_ZOOM_STEPS.length - 1 : idx) + dir), PDF_ZOOM_STEPS.length - 1)];
        apply(next);
    };

    nav.querySelector('.pdf-zoom-in').addEventListener('click', () => step(1));
    nav.querySelector('.pdf-zoom-out').addEventListener('click', () => step(-1));
    resetBtn.addEventListener('click', () => apply(1));

    window.addEventListener('keydown', (e) => {
        if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
        if (e.key === '+' || e.key === '=') { e.preventDefault(); step(1); }
        else if (e.key === '-') { e.preventDefault(); step(-1); }
        else if (e.key === '0') { e.preventDefault(); apply(1); }
    });
}

// Temporarily resets zoom (e.g. while rasterizing pages) and returns a restore function.
function suspendZoom() {
    const container = document.getElementById('pagesContainer');
    if (!container || pdfZoomLevel === 1) return () => { };
    container.style.zoom = 1;
    return () => { container.style.zoom = pdfZoomLevel; };
}


// Auto-download: DLH.PrintPDF opens the report with ?_autoDownload=true so the PDF
// is generated and downloaded automatically, then the tab closes itself.
const urlParams = new URLSearchParams(window.location.search);

// PDF reports restore their own view (zoom + page) instead of the live preview's
// pixel-based scroll restore, which breaks once the zoom level differs.
window.__datalinqPdfRestoresView = urlParams.get('_autoDownload') !== 'true';
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
    const restoreZoom = suspendZoom();

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
        restoreZoom();
        removeLoadingOverlay();

        if (window.parent !== window) {
            window.parent.postMessage({ type: 'pdfDownloadComplete' }, '*');
        }

        const btn = document.getElementById('downloadBtn');
        if (btn) { btn.innerHTML = DOWNLOAD_BUTTON_HTML; btn.disabled = false; }
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

// Adds automatic date and time based on the `.pdf-report-options` settings.
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

// Manual page breaks: every `.page-break` marker (emitted by @PDF.PageBreak between
// NewPage/EndPage) starts a fresh continuation page. Everything after the marker on the
// same page is moved onto a new page that mirrors the parent page's metadata (paper size,
// orientation, template, dynamic table options and margins) via createContinuationPageWrapper.
// Runs before splitAllTables so the resulting pages still participate in dynamic pagination.
function applyManualPageBreaks() {
    const pagesContainer = document.getElementById('pagesContainer');
    if (!pagesContainer) return;

    let guard = 0;
    let pageBreak = pagesContainer.querySelector('.page-break');

    while (pageBreak && guard++ < MAX_PAGINATION_ITERATIONS) {
        const page = pageBreak.closest('.page');
        const wrapper = pageBreak.closest('.page-wrapper');

        if (!page || !wrapper) {
            pageBreak.remove();
            pageBreak = pagesContainer.querySelector('.page-break');
            continue;
        }

        const firstToMove = pageBreak.nextElementSibling;
        pageBreak.remove();

        const nextWrapper = createContinuationPageWrapper(page, wrapper);
        const nextPage = nextWrapper.querySelector('.page');

        let current = firstToMove;
        let firstMovedElement = null;

        while (current) {
            const next = current.nextElementSibling;

            if (!current.classList.contains('report-ignore')) {
                nextPage.appendChild(current);

                if (!firstMovedElement) {
                    firstMovedElement = current;
                }
            }

            current = next;
        }

        normalizeFirstMovedElement(firstMovedElement);
        insertPageAfter(wrapper, nextWrapper, pagesContainer);

        pageBreak = pagesContainer.querySelector('.page-break');
    }
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
    if (template && sourceWrapper.classList.contains('dynamic-use-template')) {
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