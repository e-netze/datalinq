/*
 * PDF report - loading overlay.
 *
 * Full-screen overlay with a progress bar shown while the PDF is being generated.
 */

/**
 * Shows the loading overlay (no-op if it is already visible).
 * @returns {void}
 */
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

/**
 * Updates the progress bar and text; removes the initial spinner.
 * @param {number} current Number of pages rendered so far.
 * @param {number} total Total number of pages.
 * @returns {void}
 */
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

/**
 * Removes the loading overlay if present.
 * @returns {void}
 */
function removeLoadingOverlay() {
    const overlay = document.getElementById('pdf-loading-overlay');
    if (overlay) {
        overlay.remove();
    }
}
