/*
 * PDF report - dynamic page stacking.
 */

/**
 * Outside editing mode tables have their full height, so absolutely positioned
 * draggables on dynamic pages would overlap. Turns them into flow elements and lays
 * out all page content (draggables and regular elements like headings, text or
 * dividers) top to bottom, keeping the vertical gaps they had in editing mode
 * (where tables are shortened). Growing tables push all following content down.
 * @returns {void}
 */
function stackDraggablesOnDynamicPages() {
    const uniquePages = Array.from(document.querySelectorAll('.page-wrapper.dynamic .page'));

    uniquePages.forEach(page => {
        const flowElements = Array.from(page.children)
            .filter(child => !child.classList.contains('report-ignore'));
        if (!flowElements.some(child => child.classList.contains('element'))) return;

        const relTop = el => el.getBoundingClientRect().top - page.getBoundingClientRect().top;
        const relBottom = el => el.getBoundingClientRect().bottom - page.getBoundingClientRect().top;

        // Measure the layout as it looks in editing mode.
        const restore = limitTableRowsForEditing(page.parentElement || page, false);
        const editLayout = new Map(flowElements.map(el => {
            const rect = el.getBoundingClientRect();
            return [el, { top: relTop(el), bottom: relBottom(el), width: rect.width }];
        }));
        restore();

        // Bring the DOM into visual (top to bottom) order so the flow matches the design.
        const ordered = flowElements
            .map((el, index) => ({ el, index }))
            .sort((a, b) => (editLayout.get(a.el).top - editLayout.get(b.el).top) || (a.index - b.index))
            .map(entry => entry.el);

        const anchor = document.createComment('flow-anchor');
        page.insertBefore(anchor, flowElements[0]);
        ordered.forEach(el => page.insertBefore(el, anchor));
        anchor.remove();

        let editMaxBottom = null;
        let actualMaxBottom = null;

        ordered.forEach(el => {
            const edit = editLayout.get(el);

            if (el.classList.contains('element')) {
                const x = parseFloat(el.getAttribute('data-x')) || 0;
                el.style.position = 'relative';
                // Keep the box of the absolutely positioned draggable: same width (shrink-to-fit)
                // and its own block formatting context so child margins don't collapse out.
                el.style.display = 'flow-root';
                el.style.width = `${edit.width}px`;
                el.style.transform = `translate(${x}px, 0px)`;
                el.style.marginTop = '0px';
            }

            const desiredTop = editMaxBottom === null
                ? edit.top
                : actualMaxBottom + (edit.top - editMaxBottom);

            // Two passes to compensate for margin collapsing.
            for (let pass = 0; pass < 2; pass++) {
                const delta = desiredTop - relTop(el);
                if (Math.abs(delta) < 0.5) break;
                const currentMargin = parseFloat(window.getComputedStyle(el).marginTop) || 0;
                el.style.marginTop = `${currentMargin + delta}px`;
            }

            editMaxBottom = editMaxBottom === null ? edit.bottom : Math.max(editMaxBottom, edit.bottom);
            actualMaxBottom = actualMaxBottom === null ? relBottom(el) : Math.max(actualMaxBottom, relBottom(el));
        });
    });
}
