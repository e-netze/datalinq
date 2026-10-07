/*
 * PDF report editing mode (client side).
 *
 * Enables drag-and-drop positioning of @DLH.NewPdfElement blocks with optional
 * snapping (to page-center guides or to other elements) and a right-click action
 * that copies an element's coordinates as a ready-to-paste helper call.
 *
 * Only included by Report.cshtml when editing mode is on; its presence is what
 * isPdfReportEditingMode (editing-mode.js) checks for.
 */

// Distance in pixels within which a dragged element snaps to a guide or another element.
const SNAP_THRESHOLD = 15;

// Wait until the DataLinq core client-side logic has finished before wiring up editing.
dataLinq.events.on('onpageloaded', function () {

    // Drag-and-drop + snapping for each @DLH.NewPdfElement on the page.
    const elements = document.querySelectorAll('.element');

    elements.forEach(element => {
        let isDragging = false;
        let startX, startY;
        let initialX = 0, initialY = 0;

        const page = element.closest('.page');

        let snapLineX = document.createElement('div');
        snapLineX.className = 'snap-guide-line-x';
        page.appendChild(snapLineX);

        let snapLineY = document.createElement('div');
        snapLineY.className = 'snap-guide-line-y';
        page.appendChild(snapLineY);

        element.addEventListener('mousedown', startDrag);

        /**
         * Starts dragging: remembers the pointer and current translate offset.
         * @param {MouseEvent} e The mousedown event.
         * @returns {void}
         */
        function startDrag(e) {
            isDragging = true;
            element.classList.add('dragging');

            const style = window.getComputedStyle(element);
            const matrix = new DOMMatrix(style.transform);
            initialX = matrix.m41;
            initialY = matrix.m42;

            startX = e.clientX;
            startY = e.clientY;

            document.addEventListener('mousemove', drag);
            document.addEventListener('mouseup', stopDrag);

            e.preventDefault();
        }

        /**
         * Moves the element with the pointer, clamped to the page. With Ctrl held it
         * snaps to the visible center guides, otherwise to other elements.
         * @param {MouseEvent} e The mousemove event.
         * @returns {void}
         */
        function drag(e) {
            if (!isDragging) return;

            const pageRect = page.getBoundingClientRect();
            const elementRect = element.getBoundingClientRect();

            // Screen px -> CSS px (the viewer may be zoomed).
            const scale = page.offsetWidth > 0 ? pageRect.width / page.offsetWidth : 1;

            let newX = initialX + (e.clientX - startX) / scale;
            let newY = initialY + (e.clientY - startY) / scale;

            // The translate is relative to the element's layout position, which is not
            // necessarily the page's top-left corner, so clamp using that base offset.
            const currentMatrix = new DOMMatrix(window.getComputedStyle(element).transform);
            const baseLeft = (elementRect.left - pageRect.left) / scale - currentMatrix.m41;
            const baseTop = (elementRect.top - pageRect.top) / scale - currentMatrix.m42;
            const elementWidth = elementRect.width / scale;
            const elementHeight = elementRect.height / scale;

            const minX = -baseLeft;
            const maxX = page.offsetWidth - elementWidth - baseLeft;
            const minY = -baseTop;
            const maxY = page.offsetHeight - elementHeight - baseTop;

            newX = Math.max(minX, Math.min(newX, Math.max(minX, maxX)));
            newY = Math.max(minY, Math.min(newY, Math.max(minY, maxY)));

            let isSnapped = false;
            let snapInfo = null;

            const snapLines = getVisibleSnapLines(page, pageRect);
            const guideLinesVisible = snapLines.x.length > 0 || snapLines.y.length > 0;

            if (e.ctrlKey && guideLinesVisible) {
                const snapped = applyGuideSnapping(newX, newY, elementRect, snapLines);
                newX = snapped.x;
                newY = snapped.y;
                isSnapped = snapped.snapped;

                hideSnapGuides();
            } else if (e.ctrlKey) {
                const snapped = applyElementSnapping(newX, newY, element, page);
                newX = snapped.x;
                newY = snapped.y;
                isSnapped = snapped.snapped;
                snapInfo = snapped.snapInfo;

                if (isSnapped && snapInfo) {
                    showSnapGuides(snapInfo);
                } else {
                    hideSnapGuides();
                }
            } else {
                hideSnapGuides();
            }

            if (isSnapped) {
                element.classList.add('element-snapped');
            } else {
                element.classList.remove('element-snapped');
            }

            element.style.transform = `translate(${newX}px, ${newY}px)`;

            element.setAttribute('data-x', newX);
            element.setAttribute('data-y', newY);
        }

        /**
         * Snaps the element to the visible guide lines (page center and margin guides).
         * @param {number} x Proposed x offset.
         * @param {number} y Proposed y offset.
         * @param {DOMRect} elementRect Bounding rect of the dragged element.
         * @param {{x: Array, y: Array}} snapLines Visible lines from getVisibleSnapLines.
         * @returns {{x: number, y: number, snapped: boolean}} Resulting position.
         */
        function applyGuideSnapping(x, y, elementRect, snapLines) {
            const snappedX = snapAxisToLines(x, elementRect.width, snapLines.x, SNAP_THRESHOLD);
            const snappedY = snapAxisToLines(y, elementRect.height, snapLines.y, SNAP_THRESHOLD);

            return { x: snappedX.value, y: snappedY.value, snapped: snappedX.snapped || snappedY.snapped };
        }

        /**
         * Snaps the element's edges/center to the edges/centers of other elements on the page.
         * @param {number} x Proposed x offset.
         * @param {number} y Proposed y offset.
         * @param {HTMLElement} draggedElement The element being dragged.
         * @param {HTMLElement} page The page containing the element.
         * @returns {{x: number, y: number, snapped: boolean, snapInfo: object|null}} Resulting position and guide info.
         */
        function applyElementSnapping(x, y, draggedElement, page) {
            const pageRect = page.getBoundingClientRect();
            const draggedRect = draggedElement.getBoundingClientRect();
            const draggedWidth = draggedRect.width;
            const draggedHeight = draggedRect.height;

            const draggedLeft = x;
            const draggedRight = x + draggedWidth;
            const draggedCenterX = x + draggedWidth / 2;
            const draggedTop = y;
            const draggedBottom = y + draggedHeight;
            const draggedCenterY = y + draggedHeight / 2;

            let snappedX = x;
            let snappedY = y;
            let snapped = false;

            const otherElements = Array.from(page.querySelectorAll('.element')).filter(el => el !== draggedElement);

            let minXDistance = Infinity;
            let minYDistance = Infinity;
            let bestXSnap = x;
            let bestYSnap = y;
            let snapXPosition = null;
            let snapYPosition = null;
            let snapTargetElement = null;

            otherElements.forEach(otherElement => {
                const otherRect = otherElement.getBoundingClientRect();
                const otherX = parseFloat(otherElement.getAttribute('data-x')) || 0;
                const otherY = parseFloat(otherElement.getAttribute('data-y')) || 0;

                const otherWidth = otherRect.width;
                const otherHeight = otherRect.height;

                const otherLeft = otherX;
                const otherRight = otherX + otherWidth;
                const otherCenterX = otherX + otherWidth / 2;
                const otherTop = otherY;
                const otherBottom = otherY + otherHeight;
                const otherCenterY = otherY + otherHeight / 2;

                const xSnapPoints = [
                    { distance: Math.abs(draggedLeft - otherLeft), snap: otherLeft, position: otherLeft },
                    { distance: Math.abs(draggedLeft - otherRight), snap: otherRight, position: otherRight },
                    { distance: Math.abs(draggedRight - otherLeft), snap: otherLeft - draggedWidth, position: otherLeft },
                    { distance: Math.abs(draggedRight - otherRight), snap: otherRight - draggedWidth, position: otherRight },
                    { distance: Math.abs(draggedCenterX - otherCenterX), snap: otherCenterX - draggedWidth / 2, position: otherCenterX },
                ];

                xSnapPoints.forEach(point => {
                    if (point.distance < minXDistance && point.distance < SNAP_THRESHOLD) {
                        minXDistance = point.distance;
                        bestXSnap = point.snap;
                        snapXPosition = point.position;
                        snapTargetElement = otherElement;
                    }
                });

                const ySnapPoints = [
                    { distance: Math.abs(draggedTop - otherTop), snap: otherTop, position: otherTop },
                    { distance: Math.abs(draggedTop - otherBottom), snap: otherBottom, position: otherBottom },
                    { distance: Math.abs(draggedBottom - otherTop), snap: otherTop - draggedHeight, position: otherTop },
                    { distance: Math.abs(draggedBottom - otherBottom), snap: otherBottom - draggedHeight, position: otherBottom },
                    { distance: Math.abs(draggedCenterY - otherCenterY), snap: otherCenterY - draggedHeight / 2, position: otherCenterY },
                ];

                ySnapPoints.forEach(point => {
                    if (point.distance < minYDistance && point.distance < SNAP_THRESHOLD) {
                        minYDistance = point.distance;
                        bestYSnap = point.snap;
                        snapYPosition = point.position;
                        if (!snapTargetElement) snapTargetElement = otherElement;
                    }
                });
            });

            if (minXDistance < SNAP_THRESHOLD) {
                snappedX = bestXSnap;
                snapped = true;
            }

            if (minYDistance < SNAP_THRESHOLD) {
                snappedY = bestYSnap;
                snapped = true;
            }

            return {
                x: snappedX,
                y: snappedY,
                snapped: snapped,
                snapInfo: snapped ? {
                    xPosition: minXDistance < SNAP_THRESHOLD ? snapXPosition : null,
                    yPosition: minYDistance < SNAP_THRESHOLD ? snapYPosition : null,
                    targetElement: snapTargetElement
                } : null
            };
        }

        /**
         * Shows the snap guide lines for the current snap target.
         * @param {object} snapInfo Snap info returned by applyElementSnapping.
         * @returns {void}
         */
        function showSnapGuides(snapInfo) {
            page.querySelectorAll('.element.snap-target').forEach(el => {
                el.classList.remove('snap-target');
            });

            if (snapInfo.xPosition !== null) {
                snapLineX.style.left = snapInfo.xPosition + 'px';
                snapLineX.style.display = 'block';
            } else {
                snapLineX.style.display = 'none';
            }

            if (snapInfo.yPosition !== null) {
                snapLineY.style.top = snapInfo.yPosition + 'px';
                snapLineY.style.display = 'block';
            } else {
                snapLineY.style.display = 'none';
            }

            if (snapInfo.targetElement) {
                snapInfo.targetElement.classList.add('snap-target');
            }
        }

        /**
         * Hides both snap guide lines.
         * @returns {void}
         */
        function hideSnapGuides() {
            snapLineX.style.display = 'none';
            snapLineY.style.display = 'none';

            page.querySelectorAll('.element.snap-target').forEach(el => {
                el.classList.remove('snap-target');
            });
        }

        /**
         * Ends dragging and removes the document-level listeners.
         * @returns {void}
         */
        function stopDrag() {
            isDragging = false;
            element.classList.remove('dragging');
            element.classList.remove('element-snapped');
            hideSnapGuides();
            document.removeEventListener('mousemove', drag);
            document.removeEventListener('mouseup', stopDrag);
        }
    });

    // Right-click a @DLH.NewPdfElement to copy its coordinates as a helper call.
    document.addEventListener('contextmenu', function (e) {
        const element = e.target.closest('.element');

        if (element) {
            e.preventDefault();

            const x = element.getAttribute('data-x');
            const y = element.getAttribute('data-y');

            const textToCopy = `@PDF.NewDraggable(x: ${x}, y: ${y})`;

            const copied = navigator.clipboard && window.isSecureContext
                ? navigator.clipboard.writeText(textToCopy)
                : Promise.reject();

            copied.catch(() => {
                // Fallback for embedded frames (e.g. the live preview) without clipboard permission.
                if (!dataLinq.copyToClipboard(textToCopy)) throw new Error('copy failed');
            }).then(() => {
                showToast('Copied!', e.clientX, e.clientY);
            }).catch(() => {
                showToast('Failed to copy element location!', e.clientX, e.clientY);
            });
        }
    });

    /**
     * Shows a short-lived toast message near the mouse position.
     * @param {string} message Text to show.
     * @param {number} mouseX Client x position.
     * @param {number} mouseY Client y position.
     * @returns {void}
     */
    function showToast(message, mouseX, mouseY) {
        const toast = document.createElement('div');
        toast.textContent = message;
        toast.style.cssText = `
        position: fixed;
        left: ${mouseX}px;
        top: ${mouseY}px;
        background-color: #333;
        color: white;
        padding: 12px 24px;
        border-radius: 4px;
        font-size: 14px;
        z-index: 10000;
        opacity: 0;
        transition: opacity 0.3s ease;
        transform: translate(-50%, -100%);
        margin-top: -10px;
        pointer-events: none;
    `;

        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '1';
        }, 10);

        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => {
                document.body.removeChild(toast);
            }, 300);
        }, 1000);
    }
    initializeEditingToolbar();
});

/**
 * Creates the floating editing toolbar (styled like the page navigator) with a
 * button that toggles the page center guide lines and the margin guides control
 * (see margin-guides.js).
 * @returns {void}
 */
function initializeEditingToolbar() {
    if (document.querySelector('.pdf-editing-toolbar')) return;

    const toolbar = document.createElement('div');
    toolbar.className = 'pdf-page-navigator pdf-editing-toolbar';

    const guidesButton = document.createElement('button');
    guidesButton.type = 'button';
    guidesButton.className = 'pdf-editing-guides';
    guidesButton.innerHTML =
        '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">' +
        '<rect x="3" y="3" width="18" height="18" rx="2"/><line x1="12" y1="3" x2="12" y2="21" stroke-dasharray="2 2"/><line x1="3" y1="12" x2="21" y2="12" stroke-dasharray="2 2"/></svg>' +
        '<span>Center guides</span>';

    const guideLines = () => document.querySelectorAll('.vertical-middle-line, .horizontal-middle-line');
    const guidesVisible = () => Array.from(guideLines())
        .some(line => window.getComputedStyle(line).display !== 'none');

    const updateButton = () => {
        const visible = guidesVisible();
        guidesButton.classList.toggle('active', visible);
        guidesButton.title = visible
            ? 'Hide page center guides (Ctrl + drag snaps to them)'
            : 'Show page center guides (Ctrl + drag snaps to them)';
    };

    guidesButton.addEventListener('click', () => {
        const show = !guidesVisible();
        guideLines().forEach(line => line.style.display = show ? 'block' : 'none');
        updateButton();
    });

    toolbar.appendChild(guidesButton);

    const outlinesButton = document.createElement('button');
    outlinesButton.type = 'button';
    outlinesButton.className = 'pdf-editing-guides pdf-editing-outlines';
    outlinesButton.innerHTML =
        '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">' +
        '<rect x="4" y="4" width="16" height="16" rx="1" stroke-dasharray="3 2"/></svg>' +
        '<span>Element outlines</span>';

    const updateOutlinesButton = () => {
        const active = document.body.classList.contains('pdf-show-element-outlines');
        outlinesButton.classList.toggle('active', active);
        outlinesButton.title = active ? 'Hide draggable element outlines' : 'Show draggable element outlines';
    };

    outlinesButton.addEventListener('click', () => {
        document.body.classList.toggle('pdf-show-element-outlines');
        updateOutlinesButton();
    });

    toolbar.appendChild(outlinesButton);
    updateOutlinesButton();

    applyMarginGuideSettings();
    toolbar.appendChild(createMarginGuidesControl());

    renderCustomGuides();
    toolbar.appendChild(createCustomGuidesControl());

    document.body.appendChild(toolbar);
    updateButton();
}
