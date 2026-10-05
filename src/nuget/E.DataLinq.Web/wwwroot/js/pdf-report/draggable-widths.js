/*
 * PDF report - draggable widths.
 */

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
    });
}
