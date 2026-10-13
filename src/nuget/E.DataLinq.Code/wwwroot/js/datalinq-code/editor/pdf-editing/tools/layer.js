/**
 * PDF editing tool: element layer
 *
 * Source actions (see E.DataLinq.Web pdf-report/tools/layer.js):
 *   'element.layer.get': { index, originalX, originalY } -> { data: { layer } }
 *   'element.layer.set': { index, originalX, originalY, layer } -> sets/removes the layer argument
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var findEditableCall = function (source, args) {
        var call = pdfDraggableSource.findCall(source, args);
        if (!call) {
            return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };
        }
        if (isNaN(call.layer)) {
            return { message: 'The layer of this element is an expression and can not be edited here.' };
        }
        return call;
    };

    pdfDraggableSource.registerAction('element.layer.get', function (source, args) {
        var call = findEditableCall(source, args);
        if (call.message) return call;

        return { data: { layer: call.layer } };
    });

    pdfDraggableSource.registerAction('element.layer.set', function (source, args) {
        var layer = Number(args.layer);
        if (!Number.isInteger(layer) || Math.abs(layer) > 1000000) {
            return { message: 'Invalid layer (integer expected).' };
        }

        var call = findEditableCall(source, args);
        if (call.message) return call;

        return {
            start: call.argsStart,
            end: call.argsEnd,
            text: pdfDraggableSource.buildArgs(call.xText || 0, call.yText || 0, layer)
        };
    });
})();
