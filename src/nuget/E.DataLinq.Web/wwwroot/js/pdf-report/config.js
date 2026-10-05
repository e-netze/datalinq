/*
 * PDF report - configuration.
 *
 * PDF report generation (client side) runs on report pages rendered in PDF mode.
 * It paginates the rendered HTML into fixed-size pages, loads per-page templates,
 * adds page numbers, and renders the result to a downloadable PDF using
 * html-to-image (rasterize) + pdf-lib (assemble).
 *
 * The feature is split into several classic scripts under js/pdf-report/ that
 * share one global scope. They are loaded in a fixed order by Report.cshtml:
 *
 *   config.js                 constants used by all other files (this file)
 *   loading-overlay.js        progress overlay while the PDF is generated
 *   zoom.js                   zoom controls of the viewer
 *   view-state.js             persists zoom + scroll position per report
 *   page-navigator.js         floating "page X / Y" navigator, download/print buttons
 *   pdf-export.js             rasterizes pages and assembles the PDF
 *   page-decorations.js       page numbers and date/time stamps
 *   page-templates.js         per-page template loading
 *   draggable-widths.js       resolves percentage widths inside draggables
 *   page-structure.js         include unwrapping and manual page breaks
 *   editing-mode.js           editing mode detection and table previews
 *   dynamic-page-stacking.js  stacks draggables on dynamic pages
 *   pagination.js             splits overflowing content across pages
 *   pagination-builders.js    builds continuation pages/tables and measuring helpers
 *   auto-download.js          ?_autoDownload=true handling
 *   main.js                   entry point (runs the pipeline on 'onpageloaded')
 *
 * To add a feature: create a new file here, add it to the list in Report.cshtml
 * (before main.js) and call it from the pipeline in main.js.
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
