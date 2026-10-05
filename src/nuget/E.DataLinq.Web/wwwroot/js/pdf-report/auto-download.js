/*
 * PDF report - auto download.
 *
 * DLH.PrintPDF opens the report with ?_autoDownload=true so the PDF is generated
 * and downloaded automatically, then the tab closes itself.
 * Runs at script load (not on 'onpageloaded').
 */

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
