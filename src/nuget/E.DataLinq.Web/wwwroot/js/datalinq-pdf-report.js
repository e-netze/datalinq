/* WAIT FOR DATALINQ CORE CLIENT SIDE LOGIC TO FINISH */
dataLinq.events.on('onpageloaded', function () {

/* PDF GENERATION BUTTON ON SITE*/
    const downloadBtn = document.getElementById('downloadBtn');
    if (downloadBtn) {
        downloadBtn.addEventListener('click', async function () {
            this.textContent = 'Generating PDF ...';
            this.disabled = true;

            showLoadingOverlay();

            await new Promise(resolve => requestAnimationFrame(() =>
                requestAnimationFrame(resolve)
            ));

            downloadPDFMethod();
        });
    }
/* PDF GENERATION BUTTON ON SITE*/

    /* FLATTEN FINISHED INCLUDE WRAPPERS BEFORE PAGINATION */
    unwrapFinishedIncludes();
    /* CALLS TABLE SPLIT LOGIC */
    splitAllTables();
    /* CALLS TEMPLATE LOADER LOGIC */
    initializeTemplateLoader();
    /* CALLS PAGE NUMBERING LOGIC */
    addPageNumbers();
});


/* AUTODOWNLOAD URL PARAM HANDELING FOR DLH.PrintPDF BUTTON */
const urlParams = new URLSearchParams(window.location.search);
if (urlParams.get('_autoDownload') === 'true') {
    document.body.style.opacity = '0';

    setTimeout(async () => {
        await downloadPDFMethod();

        window.close();

        setTimeout(() => {
            document.body.innerHTML = '<div style="display:flex;justify-content:center;align-items:center;height:100vh;font-family:sans-serif;">PDF download started.  You can close this window.</div>';
            document.body.style.opacity = '100';
        }, 100);
    }, 1000); 
}
/* AUTODOWNLOAD URL PARAM HANDELING FOR DLH.PrintPDF BUTTON */

/* PDF RENDERING */
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
                        //CORS zu CSS wenn z.B. leaflet eingebunden wird
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
                const paperSize = sizeClass ? sizeClass.replace(/^size-/i, '').toUpperCase() : "A4";
                return { dataUrl, isHorizontal: page.classList.contains('horizontal'), paperSize: paperSize };
            })
        );

        const pdfDoc = await PDFLib.PDFDocument.create();
        pdfDoc.setCreator('DataLinq');

        for (const { dataUrl, isHorizontal, paperSize } of pageImages) {
            const paperDimensions = getPaperDimensions(paperSize);
            const [w, h] = isHorizontal ? paperDimensions.landscape : paperDimensions.portrait;

            const pngBytes = Uint8Array.from(
                atob(dataUrl.slice(dataUrl.indexOf(',') + 1)),
                c => c.charCodeAt(0)
            );

            const pngImage = await pdfDoc.embedJpg(pngBytes);
            const page = pdfDoc.addPage([w, h]);
            page.drawImage(pngImage, { x: 0, y: 0, width: w, height: h });
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
    const qualityMap = {
        Best: { pixelRatio: 2, quality: 1 },
        High: { pixelRatio: 2, quality: 0.92 },
        Medium: { pixelRatio: 1.5, quality: 0.85 },
        Low: { pixelRatio: 1.5, quality: 0.75 },
        Preview: { pixelRatio: 1, quality: 0.80 },
    };

    const qualityKey = document.querySelector('.main')?.getAttribute('quality') ?? 'High';
    return qualityMap[qualityKey] ?? qualityMap.High;
};

const getPaperDimensions = (size) => {
    const paperSizeMap = {
        A1: { portrait: [1683.78, 2383.94], landscape: [2383.94, 1683.78] },
        A2: { portrait: [1190.55, 1683.78], landscape: [1683.78, 1190.55] },
        A3: { portrait: [841.89, 1190.55], landscape: [1190.55, 841.89] },
        A4: { portrait: [595.28, 841.89], landscape: [841.89, 595.28] },
        A5: { portrait: [419.53, 595.28], landscape: [595.28, 419.53] },
        A6: { portrait: [297.64, 419.53], landscape: [419.53, 297.64] },
    };

    return paperSizeMap[size];
}
/* PDF RENDERING */

/* LOGIC FOR ADDING PAGE NUMBERS AUTOMATICALLY */
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
/* LOGIC FOR ADDING PAGE NUMBERS AUTOMATICALLY */

/* LOGIC FOR LOADING TEMPLATES FOR PAGES */
function initializeTemplateLoader() {
    const pages = document.querySelectorAll('.page');

    const processedTemplates = new Set();

    const checkedPages = new Set();

    pages.forEach((page, index) => {
        if (checkedPages.has(page)) {
            return;
        }

        checkedPages.add(page);

        const templateName = page.getAttribute('datalinq-pdfreport-template');

        if (templateName) {
            if (processedTemplates.has(templateName)) {
            } else {
                processedTemplates.add(templateName);

                const pagesWithTemplate = [page];
                const duplicatePages = checkForDuplicates(pages, templateName, checkedPages, index);
                pagesWithTemplate.push(...duplicatePages);

                makeTemplateRequest(pagesWithTemplate, templateName);
            }
        }
    });

}

function checkForDuplicates(pages, templateName, checkedPages, currentIndex) {
    const duplicates = [];
    const duplicatePages = [];

    pages.forEach((page, index) => {
        if (index <= currentIndex || checkedPages.has(page)) {
            return;
        }

        const pageTemplateName = page.getAttribute('datalinq-pdfreport-template');

        if (pageTemplateName === templateName) {
            duplicates.push(index + 1);
            duplicatePages.push(page);
            checkedPages.add(page);
        }
    });

    return duplicatePages;
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
/* LOGIC FOR ADDING PAGE NUMBERS AUTOMATICALLY */

/* LOGIC SPLITTING UP TABELS LONGER THAN 1 PAGE */
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

function paginateDynamicPage(startWrapper, pagesContainer) {
    let currentWrapper = startWrapper;
    let guard = 0;

    while (currentWrapper && guard++ < 100) {
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

function getFirstOverflowElement(page, bottomMargin = 0) {
    const pageRect = page.getBoundingClientRect();
    const maxBottom = pageRect.top + pageRect.height - bottomMargin;

    return Array.from(page.children)
        .filter(child => !child.classList.contains('report-ignore'))
        .find(child => child.getBoundingClientRect().bottom > maxBottom + 0.5);
}

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

    const continuationTable = createContinuationTable(table, thead, headerRow, overflowRows);
    normalizeFirstMovedElement(continuationTable);
    nextPage.appendChild(continuationTable);
    moveFollowingSiblings(table, nextPage);

    insertPageAfter(currentWrapper, nextWrapper, pagesContainer);
    return nextWrapper;
}

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

function createContinuationTable(originalTable, thead, headerRow, rows) {
    const newTable = document.createElement('table');

    Array.from(originalTable.attributes).forEach(attr => {
        if (attr.name !== 'id') {
            newTable.setAttribute(attr.name, attr.value);
        }
    });

    if (thead) {
        newTable.appendChild(thead.cloneNode(true));
    }

    const newTbody = document.createElement('tbody');

    if (headerRow && !thead) {
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
        defaultMarginTop: Number.isFinite(defaultMarginTop) ? defaultMarginTop : 10,
        defaultMarginBottom: Number.isFinite(defaultMarginBottom) ? defaultMarginBottom : 10,
    };
}

function normalizeFirstMovedElement(element) {
    if (!element) return;

    element.style.marginTop = '0';
    element.style.paddingTop = '0';
}
/* LOGIC SPLITTING UP TABELS LONGER THAN 1 PAGE */

/* LOGIC FOR PDF-GENERATION LOADIN BAR */
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