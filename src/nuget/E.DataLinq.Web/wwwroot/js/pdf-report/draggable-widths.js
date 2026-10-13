/*
 * PDF report - draggable widths.
 */

/**
 * Limits the width of a draggable to the space between its position and the right page edge,
 * so text wraps at the page edge instead of overflowing it (and scrolling the page while editing).
 * An explicit max-width of the element (inline style from the view) is kept.
 * @param {HTMLElement} element A draggable (.element).
 * @returns {void}
 */
function limitDraggableToPage(element) {
    const page = element ? element.closest('.page') : null;
    if (!page || !page.clientWidth) return;
    if (element.style.maxWidth && !element.hasAttribute('data-dl-auto-max-width')) return;

    let x = parseFloat(element.getAttribute('data-x'));
    if (!Number.isFinite(x)) x = new DOMMatrix(getComputedStyle(element).transform).m41 || 0;

    const left = element.offsetLeft + Math.max(0, x);
    element.style.maxWidth = `${Math.max(20, page.clientWidth - left)}px`;
    element.setAttribute('data-dl-auto-max-width', '');
}

/**
 * Draggable elements (.element) are absolutely positioned and shrink-to-fit, so
 * percentage widths of their direct children would resolve against the (empty)
 * element itself. Converts them to pixel widths based on the page width instead.
 * @param {Document|Element} [root=document] Element whose `.page .element` descendants are processed.
 * @returns {void}
 */
function resolveDraggableWidths(root = document) {
    root.querySelectorAll('.page .element').forEach(element => {
        const page = element.closest('.page');
        if (!page) return;

        const pageWidth = page.clientWidth;
        if (!pageWidth) return;

        Array.from(element.children).forEach(child => {
            // For non-rendered elements getComputedStyle returns the specified
            // value (e.g. "90%") instead of the used pixel value, which lets us
            // detect percentages coming from inline styles and CSS classes alike.
            const originalDisplay = child.style.display;
            const originalPriority = child.style.getPropertyPriority('display');
            child.style.setProperty('display', 'none', 'important');

            const computed = window.getComputedStyle(child);
            const specified = {
                width: computed.width,
                minWidth: computed.minWidth,
                maxWidth: computed.maxWidth
            };

            child.style.removeProperty('display');
            if (originalDisplay) {
                child.style.setProperty('display', originalDisplay, originalPriority);
            }

            Object.entries(specified).forEach(([prop, value]) => {
                const match = /^\s*(-?\d*\.?\d+)%\s*$/.exec(value || '');
                if (match) {
                    child.style[prop] = `${(pageWidth * parseFloat(match[1]) / 100)}px`;
                }
            });
        });

        limitDraggableToPage(element);
    });
}
