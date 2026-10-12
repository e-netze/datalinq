/*
 * PDF report editing mode - toolbar actions (new page, ...) and generic source actions.
 *
 *   pdfEditActions.register({
 *       id: 'new-page',
 *       label: 'New page',
 *       icon: '<svg ...>',
 *       title: 'Tooltip',                 // optional
 *       run: function (button) { ... }
 *   });
 *
 *   pdfEditActions.request('page.insert', { ... })   // -> Promise<{success, message}>
 *       sends a 'source.action' to DataLinq Code, which runs the action registered there with
 *       pdfDraggableSource.registerAction (editor/pdf-editing/tools/*.js).
 *
 *   pdfEditActions.currentPage()   // -> { element, sourceIndex, number } of the page in view
 *   pdfEditActions.pageOf(element) // -> same for a given .page element
 *   pdfEditActions.saveAndReload(sourcePage)   // save view (re-runs preview), then show sourcePage
 *
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.pdfEditActions = (function () {
    const actions = [];
    const pending = {};
    let requestCounter = 0;

    const bridge = () => window.dataLinqEditBridge;

    const notify = function (message) {
        console.info('PDF editing mode: ' + message);
        if (window.dataLinq && typeof dataLinq.toast === 'function') {
            dataLinq.toast(message);
        }
    };

    const request = function (action, args) {
        const b = bridge();
        if (!b || !b.isConnected()) {
            return Promise.resolve({ success: false, message: 'Only available in the DataLinq Code live preview.' });
        }

        return new Promise(resolve => {
            const requestId = 'action-' + (++requestCounter);
            const timeout = setTimeout(() => {
                delete pending[requestId];
                resolve({ success: false, message: 'DataLinq Code did not answer.' });
            }, 10000);

            pending[requestId] = result => {
                clearTimeout(timeout);
                resolve(result);
            };
            b.send('source.action', { requestId: requestId, action: action, args: args || {} });
        });
    };

    if (bridge()) {
        bridge().register('source.action-result', function (payload) {
            const done = pending[payload.requestId];
            delete pending[payload.requestId];
            if (done) {
                done({
                    success: payload.success === true,
                    message: typeof payload.message === 'string' ? payload.message : '',
                    data: payload.data && typeof payload.data === 'object' ? payload.data : null
                });
            }
        });
    }

    /**
     * The page most visible in the viewport.
     * @returns {{element: HTMLElement, sourceIndex: number, number: number}|null}
     *   sourceIndex: zero based NewPage index in the source (pages created by page breaks belong
     *   to the preceding source page), number: 1 based number of that source page.
     */
    const currentPage = function () {
        const pages = Array.from(document.querySelectorAll('.page'));
        let best = null, bestVisible = 0;
        pages.forEach(page => {
            const r = page.getBoundingClientRect();
            const visible = Math.max(0, Math.min(r.bottom, window.innerHeight) - Math.max(r.top, 0));
            if (visible > bestVisible) {
                best = page;
                bestVisible = visible;
            }
        });
        if (!best) return null;
        return pageOf(best);
    };

    /**
     * @param {HTMLElement} page a rendered .page
     * @returns {{element: HTMLElement, sourceIndex: number, number: number}|null}
     */
    const pageOf = function (best) {
        const pages = Array.from(document.querySelectorAll('.page'));
        for (let i = pages.indexOf(best); i >= 0; i--) {
            const value = parseInt(pages[i].getAttribute('data-dl-edit-page'), 10);
            if (Number.isInteger(value)) {
                return { element: best, sourceIndex: value, number: value + 1 };
            }
        }
        return null;
    };

    /**
     * Creates the toolbar buttons of the registered actions. Called by initializeEditingToolbar (editing.js).
     * @returns {HTMLElement}
     */
    const createControl = function () {
        const container = document.createElement('div');
        container.className = 'pdf-edit-add-elements pdf-edit-actions';

        actions.forEach(action => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'pdf-editing-guides pdf-edit-action';
            button.title = action.title || action.label;
            button.innerHTML = (action.icon || '') + '<span></span>';
            button.querySelector('span').textContent = action.label;
            button.addEventListener('click', e => {
                e.stopPropagation();
                const b = bridge();
                if (!b || !b.isConnected()) {
                    notify('Only available in the DataLinq Code live preview.');
                    return;
                }
                action.run(button);
            });
            container.appendChild(button);
        });

        return container;
    };

    /**
     * Saves the view in DataLinq Code (same as Ctrl+S), which re-runs the live preview. After the
     * reload the preview scrolls to the given source page (see view-state.js).
     * @param {number} [focusSourcePage] zero based NewPage index to show after the reload
     * @returns {void}
     */
    const saveAndReload = function (focusSourcePage) {
        const b = bridge();
        if (!b || !b.isConnected()) return;
        if (Number.isInteger(focusSourcePage) && typeof requestViewFocus === 'function') {
            requestViewFocus(focusSourcePage);
        }
        b.send('editor.save', {});
    };

    return {
        register: action => actions.push(action),
        all: () => actions.slice(),
        request: request,
        saveAndReload: saveAndReload,
        currentPage: currentPage,
        pageOf: pageOf,
        notify: notify,
        createControl: createControl
    };
})();
