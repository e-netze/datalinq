/**
 * DataLinq Code - preview edit bridge (IDE side)
 *
 * Secure, versioned postMessage channel between the IDE and the reports rendered in the
 * live preview iframes (DataLinq engine, different origin). Counterpart of
 * E.DataLinq.Web/wwwroot/js/pdf-report/edit-bridge.js.
 *
 * Security:
 *   - only messages from the DataLinq engine origin are accepted
 *   - only messages from a live preview iframe window are accepted
 *   - a fresh random nonce per preview page load (handshake: bridge.hello -> bridge.init),
 *     every subsequent message must carry it
 *   - envelope + payload are validated by the handlers
 *
 * Envelope: { protocol: 'datalinq-edit', version: 1, nonce, type, payload }
 *
 * Extend:
 *   dataLinqCode.previewBridge.register('my.type', function (payload, ctx) { ... })  // ctx.viewId
 *   dataLinqCode.previewBridge.send(viewId, 'my.type', payload)
 *
 { id: viewId, index, x, y, originalX, originalY }
 * Depends on: core/namespace.js, core/event-controller.js
 */
(function () {
    var PROTOCOL = 'datalinq-edit';
    var VERSION = 1;

    var handlers = {};
    var sessions = new WeakMap();   // preview window -> { nonce, viewId }
    var frameResolver = null;       // function(window) -> viewId | null

    var engineOrigin = function () {
        try {
            var url = dataLinqCode._state && dataLinqCode._state.dataLinqEngineUrl;
            return url ? new URL(url, window.location.href).origin : null;
        } catch (e) {
            return null;
        }
    };

    var createNonce = function () {
        var bytes = new Uint8Array(32);
        window.crypto.getRandomValues(bytes);
        return Array.prototype.map.call(bytes, function (b) { return ('0' + b.toString(16)).slice(-2); }).join('');
    };

    var isEnvelope = function (data) {
        return !!data && typeof data === 'object' &&
            data.protocol === PROTOCOL &&
            data.version === VERSION &&
            typeof data.type === 'string';
    };

    var post = function (targetWindow, type, payload, nonce) {
        var origin = engineOrigin();
        if (!origin || !targetWindow) {
            return false;
        }
        targetWindow.postMessage({ protocol: PROTOCOL, version: VERSION, nonce: nonce, type: type, payload: payload || {} }, origin);
        return true;
    };

    var findWindowByViewId = function (viewId) {
        var result = null;
        $('.datalinq-code-editor-frame.preview').each(function () {
            var session = this.contentWindow && sessions.get(this.contentWindow);
            if (session && session.viewId === viewId) {
                result = this.contentWindow;
                return false;
            }
        });
        return result;
    };

    window.addEventListener('message', function (event) {
        var data = event.data;
        if (!isEnvelope(data)) {
            return;
        }

        var origin = engineOrigin();
        if (!origin || event.origin !== origin || !frameResolver) {
            return;
        }

        var viewId = frameResolver(event.source);
        if (!viewId) {
            return;
        }

        if (data.type === 'bridge.hello') {
            var nonce = createNonce();
            sessions.set(event.source, { nonce: nonce, viewId: viewId });
            post(event.source, 'bridge.init', { nonce: nonce });
            return;
        }

        var session = sessions.get(event.source);
        if (!session || session.viewId !== viewId || data.nonce !== session.nonce) {
            return;
        }

        var handler = handlers[data.type];
        if (typeof handler === 'function') {
            try {
                handler(data.payload || {}, { viewId: viewId });
            } catch (e) {
                console.error('preview bridge: handler failed for ' + data.type, e);
            }
        }
    });

    var isFiniteNumber = function (v) {
        return typeof v === 'number' && isFinite(v);
    };

    dataLinqCode.previewBridge = {
        /**
         * Sets the function resolving a preview iframe window to its view id (endpoint@query@view).
         * @param {function(Window): (string|null)} resolver
         */
        setFrameResolver: function (resolver) {
            frameResolver = resolver;
        },
        /**
         * Registers a handler for a message type sent by a preview.
         * @param {string} type
         * @param {function(object, {viewId: string}): void} handler
         */
        register: function (type, handler) {
            handlers[type] = handler;
        },
        /**
         * Sends a message to the connected preview of a view.
         * @param {string} viewId
         * @param {string} type
         * @param {object} payload
         * @returns {boolean} true if sent
         */
        send: function (viewId, type, payload) {
            var target = findWindowByViewId(viewId);
            var session = target && sessions.get(target);
            return session ? post(target, type, payload, session.nonce) : false;
        }
    };

    dataLinqCode.previewBridge.register('editor.save', function (payload, ctx) {
        dataLinqCode.events.fire('save-document', { id: ctx.viewId });
    });

    dataLinqCode.previewBridge.register('drag.end', function (payload, ctx) {
        if (!Number.isInteger(payload.index) || payload.index < 0 || payload.index > 100000 ||
            !isFiniteNumber(payload.x) || !isFiniteNumber(payload.y) ||
            !isFiniteNumber(payload.originalX) || !isFiniteNumber(payload.originalY)) {
            return;
        }

        dataLinqCode.events.fire('pdf-draggable-moved', {
            id: ctx.viewId,
            index: payload.index,
            x: payload.x,
            y: payload.y,
            originalX: payload.originalX,
            originalY: payload.originalY
        });
    });

    var ELEMENT_TYPE = /^[a-z][a-z0-9-]{0,31}$/;
    var MAX_TEXT_LENGTH = 4000;

    var isValidText = function (text) {
        return typeof text === 'string' && text.length <= MAX_TEXT_LENGTH;
    };

    // Add an element (paragraph, ...) -> editor-frame inserts the source and answers with
    // 'pdf-element-add-result' (forwarded to the preview as element.added / element.add-failed).
    dataLinqCode.previewBridge.register('element.add', function (payload, ctx) {
        if (typeof payload.requestId !== 'string' || payload.requestId.length > 64 ||
            typeof payload.type !== 'string' || !ELEMENT_TYPE.test(payload.type) ||
            !Number.isInteger(payload.page) || payload.page < 0 || payload.page > 100000 ||
            !isFiniteNumber(payload.x) || !isFiniteNumber(payload.y) || !isValidText(payload.text)) {
            return;
        }

        var args = {
            id: ctx.viewId,
            requestId: payload.requestId,
            type: payload.type,
            page: payload.page,
            x: payload.x,
            y: payload.y,
            text: payload.text,
            handled: false
        };
        dataLinqCode.events.fire('pdf-element-add', args);

        if (!args.handled) {
            dataLinqCode.previewBridge.send(ctx.viewId, 'element.add-failed', {
                requestId: payload.requestId,
                message: 'The view is not open in the editor or PDF editing mode is off.'
            });
        }
    });

    dataLinqCode.events.on('pdf-element-add-result', function (channel, args) {
        if (!args || !args.id) {
            return;
        }
        if (args.success) {
            dataLinqCode.previewBridge.send(args.id, 'element.added', { requestId: args.requestId, index: args.index });
        } else {
            dataLinqCode.previewBridge.send(args.id, 'element.add-failed', { requestId: args.requestId, message: args.message });
        }
    });

    dataLinqCode.previewBridge.register('element.text', function (payload, ctx) {
        if (!Number.isInteger(payload.index) || payload.index < 0 || payload.index > 100000 ||
            !isFiniteNumber(payload.originalX) || !isFiniteNumber(payload.originalY) ||
            !isValidText(payload.text) || !payload.text.trim()) {
            return;
        }

        dataLinqCode.events.fire('pdf-element-text', {
            id: ctx.viewId,
            index: payload.index,
            originalX: payload.originalX,
            originalY: payload.originalY,
            text: payload.text
        });
    });

    // Generic source action (new page, ...) -> editor-frame runs the action registered in
    // pdf-editing/tools/*.js and answers with 'pdf-source-action-result' (-> source.action-result).
    var ACTION_NAME = /^[a-z][a-z0-9.-]{0,63}$/;
    var MAX_ARGS_LENGTH = 10000;

    dataLinqCode.previewBridge.register('source.action', function (payload, ctx) {
        var size;
        try { size = JSON.stringify(payload.args || {}).length; } catch (e) { return; }
        if (typeof payload.requestId !== 'string' || payload.requestId.length > 64 ||
            typeof payload.action !== 'string' || !ACTION_NAME.test(payload.action) ||
            !payload.args || typeof payload.args !== 'object' || size > MAX_ARGS_LENGTH) {
            return;
        }

        var args = { id: ctx.viewId, requestId: payload.requestId, action: payload.action, args: payload.args, handled: false };
        dataLinqCode.events.fire('pdf-source-action', args);

        if (!args.handled) {
            dataLinqCode.previewBridge.send(ctx.viewId, 'source.action-result', {
                requestId: payload.requestId, success: false,
                message: 'The view is not open in the editor or PDF editing mode is off.'
            });
        }
    });

    dataLinqCode.events.on('pdf-source-action-result', function (channel, args) {
        if (args && args.id) {
            dataLinqCode.previewBridge.send(args.id, 'source.action-result', {
                requestId: args.requestId, success: !!args.success, message: args.message, data: args.data
            });
        }
    });

    dataLinqCode.previewBridge.register('element.delete', function (payload, ctx) {
        if (!Number.isInteger(payload.index) || payload.index < 0 || payload.index > 100000 ||
            !isFiniteNumber(payload.originalX) || !isFiniteNumber(payload.originalY)) {
            return;
        }

        dataLinqCode.events.fire('pdf-element-delete', {
            id: ctx.viewId,
            index: payload.index,
            originalX: payload.originalX,
            originalY: payload.originalY
        });
    });
})();
