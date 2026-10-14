/**
 * PDF editing tool: duplicate element
 *
 * Source actions (see E.DataLinq.Web pdf-report/tools/duplicate.js):
 *   'element.duplicate': { index, originalX, originalY, offset } -> inserts a copy of the draggable
 *                        (open call .. end call) after it, moved by offset (numeric coordinates only)
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    pdfDraggableSource.registerAction('element.duplicate', function (source, args) {
        var offset = Number(args.offset);
        if (!Number.isFinite(offset) || Math.abs(offset) > 1000) offset = 0;

        var call = pdfDraggableSource.findCall(source, args);
        var block = call ? pdfDraggableSource.findElement(source, args) : null;
        if (!call || !block) {
            return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };
        }

        var x = isNaN(call.x) ? call.xText : call.x + offset;
        var y = isNaN(call.y) ? call.yText : call.y + offset;
        var layer = isNaN(call.layer) ? call.layerText : call.layer;

        var text = source.substring(block.start, call.argsStart) +
            pdfDraggableSource.buildArgs(x === undefined ? 0 : x, y === undefined ? 0 : y, layer) +
            source.substring(call.argsEnd, block.end);

        var newLine = pdfDraggableSource.newLineOf(source);
        if (/\n$/.test(text)) {
            // whole lines -> insert the copy as own lines after the element
            return { start: block.end, end: block.end, text: text };
        }

        // inline element -> insert directly after it
        return { start: block.end, end: block.end, text: newLine + text };
    });
})();
