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

        const pageImages = await Promise.all(
            pages.map(async (page) => {
                const dataUrl = await htmlToImage.toJpeg(page, {
                    pixelRatio: 1.5,
                    quality: 0.85,
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
                return { dataUrl, isHorizontal: page.classList.contains('horizontal') };
            })
        );

        const pdfDoc = await PDFLib.PDFDocument.create();
        pdfDoc.setCreator('DataLinq');

        const A4 = { portrait: [595.28, 841.89], landscape: [841.89, 595.28] };

        for (const { dataUrl, isHorizontal } of pageImages) {
            const [w, h] = isHorizontal ? A4.landscape : A4.portrait;

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
function splitAllTables() {
    const pagesContainer = document.getElementById('pagesContainer');
    const originalPageWrappers = Array.from(pagesContainer.querySelectorAll('.page-wrapper.dynamic'));

    originalPageWrappers.forEach(pageWrapper => {
        const page = pageWrapper.querySelector('.page');
        const tables = page.querySelectorAll('table');

        tables.forEach(table => {
            console.log("FOUND TABLE TO SPLIT");
            splitTable(table, page, pageWrapper);
        });
    });
}

function getWrapperSpacing(table) {
    let totalTop = 0;
    let totalBottom = 0;
    let currentElement = table.parentElement;

    while (currentElement && !currentElement.classList.contains('page')) {
        const styles = window.getComputedStyle(currentElement);
        totalTop += parseFloat(styles.paddingTop) || 0;
        totalTop += parseFloat(styles.marginTop) || 0;
        totalBottom += parseFloat(styles.paddingBottom) || 0;
        totalBottom += parseFloat(styles.marginBottom) || 0;
        currentElement = currentElement.parentElement;
    }

    return { top: totalTop, bottom: totalBottom };
}

function splitTable(table, page, originalPageWrapper) {
    const tbody = table.querySelector('tbody');
    if (!tbody) return;

    const rows = Array.from(tbody.querySelectorAll('tr'));
    if (rows.length === 0) return;

    if (table.dataset.processed === 'true') return;
    table.dataset.processed = 'true';

    const pageHeight = page.offsetHeight;
    const topPadding = 30;
    const bottomPadding = 20;

    let contentBeforeTable = 0;
    let sibling = table.previousElementSibling;
    while (sibling) {
        contentBeforeTable += sibling.offsetHeight;
        const siblingStyles = window.getComputedStyle(sibling);
        contentBeforeTable += parseFloat(siblingStyles.marginTop) || 0;
        contentBeforeTable += parseFloat(siblingStyles.marginBottom) || 0;
        sibling = sibling.previousElementSibling;
    }

    const tableStyles = window.getComputedStyle(table);
    const tableMarginTop = parseFloat(tableStyles.marginTop) || 0;
    const tableMarginBottom = parseFloat(tableStyles.marginBottom) || 0;

    let thead = table.querySelector('thead');
    let headerRow = null;
    let theadHeight = 0;
    let startRowIndex = 0;

    if (thead) {
        theadHeight = thead.offsetHeight;
    } else {
        const firstRow = rows[0];
        if (firstRow && firstRow.querySelector('th')) {
            headerRow = firstRow;
            theadHeight = headerRow.offsetHeight;
            startRowIndex = 1;
            headerRow.dataset.isHeader = 'true';
        }
    }

    const dataRows = rows.slice(startRowIndex);

    const wrapperSpacing = getWrapperSpacing(table);

    const pagePadding = 40;
    const firstPageAvailable = pageHeight
        - contentBeforeTable
        - theadHeight
        - tableMarginTop
        - tableMarginBottom
        - wrapperSpacing.top      
        - wrapperSpacing.bottom   
        - pagePadding;

    const optionsDiv = document.querySelector('.dynamic-use-template');

    const continuationAvailable = optionsDiv
        ? pageHeight
        - theadHeight
        - tableMarginTop
        - tableMarginBottom
        - wrapperSpacing.bottom   
        - pagePadding
        : pageHeight - theadHeight - topPadding - bottomPadding - pagePadding;

    const singleRowHeight = dataRows[0].offsetHeight;

    const rowsInFirstPage = Math.floor(firstPageAvailable / singleRowHeight);
    const rowsPerContinuation = Math.floor(continuationAvailable / singleRowHeight);

    if (rowsInFirstPage >= dataRows.length) {
        return;
    }

    for (let i = rowsInFirstPage; i < dataRows.length; i++) {
        dataRows[i].classList.add('hidden');
    }

    const wrapperElements = [];
    let currentElement = table.parentElement;

    console.log('Starting wrapper collection from table parent:', currentElement);

    while (currentElement && !currentElement.classList.contains('page')) {
        console.log('Found wrapper:', currentElement.tagName, currentElement.className, currentElement.getAttribute('style'));
        wrapperElements.unshift(currentElement);
        currentElement = currentElement.parentElement;
    }

    console.log('Total wrappers found:', wrapperElements.length);

    let remainingRows = dataRows.slice(rowsInFirstPage);
    const newPages = [];

    while (remainingRows.length > 0) {
        const rowsForThisPage = remainingRows.slice(0, rowsPerContinuation);
        remainingRows = remainingRows.slice(rowsPerContinuation);

        const template = optionsDiv
            ? page.getAttribute("datalinq-pdfreport-template")
            : "";

        const newPageWrapper = createContinuationPage(rowsForThisPage, table, thead, headerRow, wrapperElements, template);
        newPages.push(newPageWrapper);
    }

    const pagesContainer = document.getElementById('pagesContainer');
    let insertAfter = originalPageWrapper;

    newPages.forEach(newPageWrapper => {
        const nextElement = insertAfter.nextElementSibling;
        if (nextElement) {
            pagesContainer.insertBefore(newPageWrapper, nextElement);
        } else {
            pagesContainer.appendChild(newPageWrapper);
        }
        insertAfter = newPageWrapper;
    });
}

function removeTopSpacingFromStyle(styleString) {
    if (!styleString) return '';
    return styleString
        .replace(/padding-top\s*:\s*[^;]+;?\s*/gi, '')
        .replace(/margin-top\s*:\s*[^;]+;?\s*/gi, '')
        .trim();
}

function createContinuationPage(rows, originalTable, thead, headerRow, wrapperElements, template) {
    const newPageWrapper = document.createElement('div');
    newPageWrapper.className = 'page-wrapper';

    const newPage = document.createElement('div');
    newPage.className = 'page';

    if (template)
        newPage.setAttribute('datalinq-pdfreport-template', template);

    let currentParent = newPage;

    wrapperElements.forEach((wrapper, index) => {

        const clonedWrapper = document.createElement(wrapper.tagName);

        if (wrapper.className) {
            clonedWrapper.className = wrapper.className;
        }

        const inlineStyle = wrapper.getAttribute('style');
        if (inlineStyle) {
            const cleanedStyle = removeTopSpacingFromStyle(inlineStyle);
            if (cleanedStyle) {
                clonedWrapper.setAttribute('style', cleanedStyle);
            }
        }

        Array.from(wrapper.attributes).forEach(attr => {
            if (attr.name === 'class' || attr.name === 'style') {
                return;
            }

            if (attr.name.startsWith('data-')) {
                clonedWrapper.setAttribute(attr.name, attr.value);
            }

            if (attr.name === 'id') {

            }
        });

        currentParent.appendChild(clonedWrapper);
        currentParent = clonedWrapper;
    });

    const newTable = document.createElement('table');
    newTable.dataset.processed = 'true';

    Array.from(originalTable.classList).forEach(cls => {
        if (cls !== 'first-table') {
            newTable.classList.add(cls);
        }
    });

    const tableInlineStyle = originalTable.getAttribute('style');
    if (tableInlineStyle) {
        const optionsDiv = document.querySelector('.dynamic-use-template');

        let finalStyle = tableInlineStyle;

        

        if (finalStyle.trim()) {
            newTable.setAttribute('style', finalStyle);
        }
    }

    Array.from(originalTable.attributes).forEach(attr => {
        if (attr.name !== 'class' &&
            attr.name !== 'style' &&
            attr.name !== 'id' &&
            !attr.name.startsWith('data-')) {
            newTable.setAttribute(attr.name, attr.value);
        }
    });

    if (thead) {
        const newThead = thead.cloneNode(true);
        newTable.appendChild(newThead);
    }

    const tbody = document.createElement('tbody');

    if (headerRow && !thead) {
        const clonedHeaderRow = headerRow.cloneNode(true);
        clonedHeaderRow.classList.remove('hidden');
        clonedHeaderRow.removeAttribute('data-is-header');
        tbody.appendChild(clonedHeaderRow);
    }

    rows.forEach(row => {
        if (row.dataset.isHeader === 'true') {
            return;
        }

        const clonedRow = row.cloneNode(true);
        clonedRow.classList.remove('hidden');
        tbody.appendChild(clonedRow);
    });

    newTable.appendChild(tbody);

    currentParent.appendChild(newTable);

    newPageWrapper.appendChild(newPage);

    return newPageWrapper;
}
/* LOGIC SPLITTING UP TABELS LONGER THAN 1 PAGE */

/* LOGIC FOR PDF-GENERATION LOADIN BAR */
function showLoadingOverlay() {
    // Prevent creating multiple overlays
    if (document.getElementById('pdf-loading-overlay')) return;

    const overlay = document.createElement('div');
    overlay.id = 'pdf-loading-overlay';

    overlay.innerHTML = `
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