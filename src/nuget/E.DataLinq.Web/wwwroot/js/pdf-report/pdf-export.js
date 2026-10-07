/*
 * PDF report - PDF export.
 *
 * Rasterizes every `.page` with html-to-image and assembles the images into a
 * PDF document with pdf-lib, then triggers the browser download.
 */

/**
 * Click handler shared by all "Download" buttons. Switches the button into its
 * busy state, shows the loading overlay and starts the PDF generation.
 * Must be registered as a regular function so `this` is the clicked button.
 * @this {HTMLButtonElement}
 * @returns {Promise<void>} Resolves when the PDF has been generated.
 */
async function onDownloadButtonClick() {
    this.textContent = 'Generating PDF ...';
    this.disabled = true;

    showLoadingOverlay();

    // Wait two frames so the overlay is painted before the heavy rendering starts.
    await new Promise(resolve => requestAnimationFrame(() =>
        requestAnimationFrame(resolve)
    ));

    await downloadPDFMethod();
}

/**
 * Renders every `.page` element to a JPEG image and assembles the images into a PDF
 * that is downloaded as `.main[fileName]` (default `dataLinqReport.pdf`).
 * Errors are logged; the overlay, zoom and download button are always restored.
 * @returns {Promise<void>} Resolves when the download has been triggered (or failed).
 */
async function downloadPDFMethod() {
    const pages = [...document.querySelectorAll('.page')];
    const restoreZoom = suspendZoom();
    // Editing outlines never end up in the PDF.
    const outlinesShown = document.body.classList.contains('pdf-show-element-outlines');
    document.body.classList.remove('pdf-show-element-outlines');

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
                    // Editing guides (margin/custom lines) never end up in the PDF.
                    filter: (node) => !(node.classList && (node.classList.contains('margin-guide-line') || node.classList.contains('custom-guide-line'))),
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
        if (outlinesShown) document.body.classList.add('pdf-show-element-outlines');
        removeLoadingOverlay();

        if (window.parent !== window) {
            window.parent.postMessage({ type: 'pdfDownloadComplete' }, '*');
        }

        const btn = document.getElementById('downloadBtn');
        if (btn) { btn.innerHTML = DOWNLOAD_BUTTON_HTML; btn.disabled = false; }
    }
}

/**
 * Resolves the rendering quality preset from the `.main[quality]` attribute.
 * @returns {{pixelRatio: number, quality: number}} The preset (falls back to PDF_DEFAULT_QUALITY).
 */
const getQuality = () => {
    const qualityKey = document.querySelector('.main')?.getAttribute('quality') ?? PDF_DEFAULT_QUALITY;
    return PDF_QUALITY_PRESETS[qualityKey] ?? PDF_QUALITY_PRESETS[PDF_DEFAULT_QUALITY];
};

/**
 * Returns the PDF point dimensions for a paper size.
 * @param {string} size Paper size key, e.g. 'A4'.
 * @returns {{portrait: number[], landscape: number[]}} Dimensions (falls back to PDF_DEFAULT_PAPER_SIZE).
 */
const getPaperDimensions = (size) => {
    return PDF_PAPER_DIMENSIONS[size] ?? PDF_PAPER_DIMENSIONS[PDF_DEFAULT_PAPER_SIZE];
}
