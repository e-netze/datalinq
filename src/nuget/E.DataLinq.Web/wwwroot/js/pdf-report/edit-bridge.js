/*
 * PDF report editing mode - edit bridge (report side).
 *
 * Secure, versioned postMessage channel between the report rendered in the DataLinq Code
 * live preview iframe and the DataLinq Code IDE (parent window, different origin).
 *
 * Handshake:
 *   1. report  -> parent  bridge.hello { capabilities }          (no secrets, targetOrigin '*')
 *   2. parent  -> report  bridge.init  { nonce } 
                (parent validates origin/source first)
 *   3. report locks onto the parent origin of bridge.init; every later message in both
 *      directions carries the nonce and is sent/accepted only for that origin/window.
 *
 * Envelope: { protocol: 'datalinq-edit', version: 1, nonce, type, payload }
 *
 * Extend: dataLinqEditBridge.register('my.type', payload => { ... }) for messages from the IDE,
 *         dataLinqEditBridge.send('my.type', payload) for messages to the IDE.
 *
 * Only included by Report.cshtml when editing mode is on (Development/Test only).
 */
window.dataLinqEditBridge = (function () {
    const PROTOCOL = 'datalinq-edit';
    const VERSION = 1;
    const MAX_NONCE_LENGTH = 128;

    const handlers = {};
    const capabilities = ['drag.end', 'element.add', 'element.text', 'element.delete', 'source.action'];

    let parentOrigin = null;
    let nonce = null;

    const isEmbedded = window.parent && window.parent !== window;

    const isEnvelope = function (data) {
        return !!data && typeof data === 'object' &&
            data.protocol === PROTOCOL &&
            data.version === VERSION &&
            typeof data.type === 'string';
    };

    const post = function (type, payload, targetOrigin, withNonce) {
        if (!isEmbedded) return false;
        const message = { protocol: PROTOCOL, version: VERSION, type: type, payload: payload || {} };
        if (withNonce) message.nonce = nonce;
        window.parent.postMessage(message, targetOrigin);
        return true;
    };

    /**
     * Sends a message to the IDE. Dropped until the handshake is completed.
     * @param {string} type Message type.
     * @param {object} payload Message payload.
     * @returns {boolean} True if sent.
     */
    const send = function (type, payload) {
        if (!parentOrigin || !nonce) return false;
        return post(type, payload, parentOrigin, true);
    };

    /**
     * Registers a handler for a message type sent by the IDE.
     * @param {string} type Message type.
     * @param {function(object): void} handler Handler receiving the payload.
     * @returns {void}
     */
    const register = function (type, handler) {
        handlers[type] = handler;
        if (capabilities.indexOf(type) < 0) capabilities.push(type);
    };

    const isConnected = function () {
        return !!(parentOrigin && nonce);
    };

    window.addEventListener('message', function (event) {
        if (!isEmbedded || event.source !== window.parent) return;

        const data = event.data;
        if (!isEnvelope(data)) return;

        if (data.type === 'bridge.init') {
            if (isConnected()) return; // one handshake per page load
            const n = data.payload && data.payload.nonce;
            if (typeof n !== 'string' || n.length < 16 || n.length > MAX_NONCE_LENGTH) return;
            parentOrigin = event.origin;
            nonce = n;
            document.dispatchEvent(new CustomEvent('datalinq-edit:connected'));
            return;
        }

        if (!isConnected() || event.origin !== parentOrigin || data.nonce !== nonce) return;

        const handler = handlers[data.type];
        if (typeof handler === 'function') {
            try {
                handler(data.payload || {});
            } catch (e) {
                console.error('datalinq edit bridge: handler failed for ' + data.type, e);
            }
        }
    });

    const sendHello = function () {
        post('bridge.hello', { capabilities: capabilities.slice() }, '*', false);
    };

    if (document.readyState === 'complete') {
        sendHello();
    } else {
        window.addEventListener('load', sendHello);
    }

    const round = function (v) {
        return Math.round(v * 100) / 100;
    };

    const readCoord = function (element, attribute) {
        const value = parseFloat(element.getAttribute(attribute));
        return Number.isFinite(value) ? value : 0;
    };

    /**
     * Reports the element's current position (data-x/data-y) to the IDE and makes it the
     * reference position (data-dl-edit-ox/oy) for the next move.
     * @param {HTMLElement} element The draggable.
     * @returns {{from: {x: number, y: number}, to: {x: number, y: number}}|null} The move.
     */
    const reportPosition = function (element) {
        const index = parseInt(element.getAttribute('data-dl-edit-index'), 10);
        const x = parseFloat(element.getAttribute('data-x'));
        const y = parseFloat(element.getAttribute('data-y'));
        if (!Number.isInteger(index) || !Number.isFinite(x) || !Number.isFinite(y)) return null;

        const from = { x: readCoord(element, 'data-dl-edit-ox'), y: readCoord(element, 'data-dl-edit-oy') };
        const to = { x: round(x), y: round(y) };

        send('drag.end', { index: index, x: to.x, y: to.y, originalX: from.x, originalY: from.y });

        element.setAttribute('data-dl-edit-ox', to.x);
        element.setAttribute('data-dl-edit-oy', to.y);

        return { from: from, to: to };
    };

    const moveElement = function (element, position) {
        element.style.transform = `translate(${position.x}px, ${position.y}px)`;
        element.setAttribute('data-x', position.x);
        element.setAttribute('data-y', position.y);
        if (typeof limitDraggableToPage === 'function') limitDraggableToPage(element);
    };

    // Undo/redo of moves (Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z) while the preview has the focus.
    const undoStack = [];
    const redoStack = [];

    const replay = function (source, target, useFrom) {
        const entry = source.pop();
        if (!entry || !entry.element.isConnected) return;

        moveElement(entry.element, useFrom ? entry.from : entry.to);
        reportPosition(entry.element);
        target.push(entry);
    };

    document.addEventListener('keydown', function (e) {
        if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
        if (e.target instanceof HTMLElement && e.target.closest('input, textarea, select, [contenteditable="true"]')) return;

        const key = e.key.toLowerCase();
        if (key === 's' && !e.shiftKey) {
            // save the view in the IDE instead of the browser's "save page as"
            if (isConnected()) {
                e.preventDefault();
                send('editor.save', {});
            }
        } else if (key === 'z' && !e.shiftKey) {
            e.preventDefault();
            replay(undoStack, redoStack, true);
        } else if (key === 'y' || (key === 'z' && e.shiftKey)) {
            e.preventDefault();
            replay(redoStack, undoStack, false);
        }
    });

    // Draggable moved (fired by editing.js) -> tell the IDE.
    document.addEventListener('datalinq-pdf:drag-end', function (e) {
        const element = e.target;
        if (!(element instanceof HTMLElement)) return;

        const move = reportPosition(element);
        if (move) {
            undoStack.push({ element: element, from: move.from, to: move.to });
            redoStack.length = 0;
        }
    });

    // Remember the rendered (source) coordinates so the IDE can verify the matching helper call.
    const snapshotOriginals = function () {
        document.querySelectorAll('.element[data-dl-edit-index]').forEach(function (el) {
            if (el.hasAttribute('data-dl-edit-ox')) return;
            el.setAttribute('data-dl-edit-ox', el.getAttribute('data-x') || '0');
            el.setAttribute('data-dl-edit-oy', el.getAttribute('data-y') || '0');
        });
    };

    if (window.dataLinq && dataLinq.events) {
        dataLinq.events.on('onpageloaded', snapshotOriginals);
    }

    return {
        register: register,
        send: send,
        isConnected: isConnected,
        snapshotOriginals: snapshotOriginals
    };
})();
