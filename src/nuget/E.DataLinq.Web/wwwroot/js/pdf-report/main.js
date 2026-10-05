/*
 * PDF report - entry point.
 *
 * Waits until the DataLinq core client-side logic has finished rendering the
 * report, then wires up the download button and runs the page-building pipeline.
 * Must be loaded after all other pdf-report scripts (see config.js).
 */

dataLinq.events.on('onpageloaded', function () {

    // On-page "Download PDF" button (rendered next to the report).
    const downloadBtn = document.getElementById('downloadBtn');
    if (downloadBtn) {
        downloadBtn.addEventListener('click', onDownloadButtonClick);
    }

    // Pagination pipeline (order matters): flatten finished includes, split
    // overflowing content across pages, load per-page templates, then number pages.
    resolveDraggableWidths();
    unwrapFinishedIncludes();
    // Manual page breaks first: draggables are positioned relative to the page they
    // end up on after the break (that is what the editing mode shows as well).
    applyManualPageBreaks();
    if (isPdfReportEditingMode()) {
        limitTableRowsForEditing();
    } else {
        stackDraggablesOnDynamicPages();
    }
    splitAllTables();
    initializeTemplateLoader();
    addPageNumbers();
    addDateTime();
    initializePageNavigator();
});
