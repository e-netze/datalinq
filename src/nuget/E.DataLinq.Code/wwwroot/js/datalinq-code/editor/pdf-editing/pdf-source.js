/**
 * Locates @PDF.NewDraggable(...) / DLH.NewPdfElement(...) calls in view source.
 * A call is identified by its render occurrence index (data-dl-edit-index) and verified
 * with the coordinates it was rendered with; falls back to a unique coordinate match.
 */
var pdfDraggableSource = new function () {
    var pattern = /\b(?:PDF\.NewDraggable|(?:DLH|IH|DataLinqHelper|IntegratorHelper)\.NewPdfElement)\s*\(([^()]*)\)/g;
    var TOLERANCE = 0.01;

    var parseArgs = function (argsText) {
        var result = { x: 0, y: 0, layer: 0, layerText: '' };
        if (!argsText.trim()) {
            return result;
        }

        var parts = argsText.split(','), positional = 0;
        for (var i = 0; i < parts.length; i++) {
            var m = /^\s*(?:(\w+)\s*:)?\s*(.*?)\s*$/.exec(parts[i]);
            var name = m[1] || (positional === 0 ? 'x' : positional === 1 ? 'y' : positional === 2 ? 'layer' : null);
            if (!m[1]) positional++;
            if (name === 'layer') {
                result.layerText = m[2];
                result.layer = /^[-+]?\d+$/.test(m[2]) ? parseInt(m[2], 10) : NaN;   // expressions -> not editable
                continue;
            }
            if (name !== 'x' && name !== 'y') continue;

            var value = /^[-+]?(\d+(\.\d*)?|\.\d+)$/.test(m[2]) ? parseFloat(m[2]) : NaN;   // expressions -> not editable
            result[name] = value;
            result[name + 'Text'] = m[2];
        }
        return result;
    };

    var findCalls = function (source) {
        var regex = new RegExp(pattern.source, 'g'), calls = [], m;
        while ((m = regex.exec(source)) !== null) {
            var argsStart = m.index + m[0].indexOf('(') + 1;
            var coords = parseArgs(m[1]);
            calls.push({
                start: m.index, argsStart: argsStart, argsEnd: argsStart + m[1].length, x: coords.x, y: coords.y,
                xText: coords.xText, yText: coords.yText, layer: coords.layer, layerText: coords.layerText
            });
        }
        return calls;
    };

    /**
     * Builds the arguments of a draggable call (named arguments, layer only if not 0).
     * x/y/layer: numbers or raw source expressions (strings).
     * @returns {string}
     */
    this.buildArgs = function (x, y, layer) {
        var self = this;
        var text = function (v) { return typeof v === 'number' ? self.formatNumber(v) : String(v); };
        var result = 'x: ' + text(x) + ', y: ' + text(y);
        if (layer !== undefined && layer !== null && layer !== '' && layer !== 0 && layer !== '0') {
            result += ', layer: ' + text(layer);
        }
        return result;
    };

    var matches = function (call, x, y) {
        return Math.abs(call.x - x) < TOLERANCE && Math.abs(call.y - y) < TOLERANCE;
    };

    /**
     * @param {string} source view source
     * @param {{index:number, originalX:number, originalY:number}} args
     * @returns {{argsStart:number, argsEnd:number}|null}
     */
    this.findCall = function (source, args) {
        var calls = findCalls(source);
        var byIndex = calls[args.index];
        if (byIndex && matches(byIndex, args.originalX, args.originalY)) {
            return byIndex;
        }

        var byCoords = calls.filter(function (c) { return matches(c, args.originalX, args.originalY); });
        return byCoords.length === 1 ? byCoords[0] : null;
    };

    this.formatNumber = function (value) {
        return String(Math.round(value * 100) / 100);
    };

    // ---- adding elements / editing text --------------------------------------------------

    var HELPER = '(PDF|DLH|IH|DataLinqHelper|IntegratorHelper)';
    var newPagePattern = new RegExp('\\b' + HELPER + '\\.NewPage\\s*\\(', 'g');
    var endPagePattern = new RegExp('(@?)\\b' + HELPER + '\\.EndPage\\s*\\(\\s*\\)', 'g');
    var endDraggablePattern = /\b(?:PDF\.EndDraggable|(?:DLH|IH|DataLinqHelper|IntegratorHelper)\.EndPdfElement)\s*\(\s*\)/g;

    /**
     * Escapes plain text for Razor/HTML source.
     * @param {string} text
     * @returns {string}
     */
    this.escapeText = function (text) {
        return String(text)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/@/g, '@@')
            .replace(/\r?\n/g, '<br />');
    };

    var self = this;

    // Source templates of the element types that can be added from the preview (see add-elements.js).
    // Registered by the tool modules in pdf-editing/tools/*.js.
    // content: Razor/HTML content inside the draggable; textPattern: plain text that can be edited.
    var elementTemplates = {};

    /**
     * Registers/overrides the source template of an element type.
     * @param {string} type
     * @param {{content: function(string, {indent:string, newLine:string}): string, textPattern: RegExp}} template
     *   content: indent = indent of the first line (multi line content), newLine = line break of the source
     */
    this.registerElementTemplate = function (type, template) {
        elementTemplates[type] = template;
    };

    var nthMatch = function (pattern, source, n, fromIndex) {
        var regex = new RegExp(pattern.source, 'g'), m, i = 0;
        regex.lastIndex = fromIndex || 0;
        while ((m = regex.exec(source)) !== null) {
            if (i++ === n) return m;
        }
        return null;
    };

    /**
     * Builds the source of a new draggable, inserted before the EndPage of the given page.
     * @param {string} source view source
     * @param {{type:string, page:number, x:number, y:number, text:string}} args
     * @returns {{offset:number, text:string, index:number}|null}
     */
    this.buildInsert = function (source, args) {
        var template = elementTemplates[args.type];
        var newPage = template ? nthMatch(newPagePattern, source, args.page) : null;
        var endPage = newPage ? nthMatch(endPagePattern, source, 0, newPage.index) : null;
        if (!endPage) {
            return null;
        }

        var at = endPage[1];
        var lineStart = source.lastIndexOf('\n', endPage.index) + 1;
        var beforeOnLine = source.substring(lineStart, endPage.index);
        var onOwnLine = /^\s*$/.test(beforeOnLine);
        var indent = onOwnLine ? beforeOnLine : '';
        var offset = onOwnLine ? lineStart : endPage.index;

        // new elements always use the current PDF helper syntax (also in pages written with DLH/IH)
        var open = 'PDF.NewDraggable';
        var close = 'PDF.EndDraggable';
        var newLine = source.indexOf('\r\n') >= 0 ? '\r\n' : '\n';
        var inner = indent + (onOwnLine ? '    ' : '');

        var text =
            inner + at + open + '(x: ' + this.formatNumber(args.x) + ', y: ' + this.formatNumber(args.y) + ')' + newLine +
            inner + '    ' + template.content(args.text, { indent: inner + '    ', newLine: newLine }) + newLine +
            inner + at + close + '()' + newLine +
            (onOwnLine ? '' : indent);

        var index = findCalls(source).filter(function (c) { return c.argsStart < offset; }).length;

        return { offset: offset, text: text, index: index };
    };

    // ---- pages / generic source actions -----------------------------------------------------

    this.newLineOf = function (source) {
        return source.indexOf('\r\n') >= 0 ? '\r\n' : '\n';
    };

    /**
     * Finds the n-th page (NewPage .. EndPage) in the source.
     * start: begin of the NewPage line (or the call if not on its own line),
     * end: after the EndPage line (incl. line break) or after the call.
     * @param {string} source view source
     * @param {number} n zero based page index (data-dl-edit-page)
     * @returns {{start:number, end:number, helper:string, at:string, indent:string}|null}
     */
    this.findPage = function (source, n) {
        var newPage = nthMatch(newPagePattern, source, n);
        var endPage = newPage ? nthMatch(endPagePattern, source, 0, newPage.index) : null;
        if (!endPage) {
            return null;
        }

        var callStart = newPage.index;
        var at = source.charAt(callStart - 1) === '@' ? '@' : '';
        callStart -= at.length;

        var lineStart = source.lastIndexOf('\n', callStart - 1) + 1;
        var beforeOnLine = source.substring(lineStart, callStart);
        var ownLine = /^\s*$/.test(beforeOnLine);

        var callEnd = endPage.index + endPage[0].length;
        var lineEnd = source.indexOf('\n', callEnd);
        if (lineEnd < 0) lineEnd = source.length;
        var endOwnLine = /^\s*$/.test(source.substring(callEnd, lineEnd));

        var argsStart = newPage.index + newPage[0].length;
        var argsEnd = closingParenthesis(source, argsStart);

        return {
            start: ownLine ? lineStart : callStart,
            end: endOwnLine ? Math.min(lineEnd + 1, source.length) : callEnd,
            helper: newPage[1],
            at: at,
            indent: ownLine ? beforeOnLine : '',
            argsStart: argsStart,
            argsEnd: argsEnd
        };
    };

    var beginReportPattern = /\b(?:PDF\.BeginReport|(?:DLH|IH|DataLinqHelper|IntegratorHelper)\.BeginPdfReport)\s*\(/g;

    /**
     * Finds the arguments of the (first) BeginReport/BeginPdfReport call.
     * @param {string} source view source
     * @returns {{argsStart:number, argsEnd:number, indent:string}|null}
     */
    this.findReport = function (source) {
        var m = nthMatch(beginReportPattern, source, 0);
        if (!m) return null;

        var argsStart = m.index + m[0].length;
        var argsEnd = closingParenthesis(source, argsStart);
        if (argsEnd < 0) return null;

        var lineStart = source.lastIndexOf('\n', m.index - 1) + 1;
        var indent = /^[ \t]*/.exec(source.substring(lineStart, m.index))[0];

        // insert position for new pages: start of the EndReport line, otherwise after the BeginReport line
        var end = nthMatch(endReportPattern, source, 0, argsEnd);
        var insertAt;
        if (end) {
            var endStart = end.index - (source.charAt(end.index - 1) === '@' ? 1 : 0);
            var endLineStart = source.lastIndexOf('\n', endStart - 1) + 1;
            insertAt = /^\s*$/.test(source.substring(endLineStart, endStart)) ? endLineStart : endStart;
        } else {
            var lineEnd = source.indexOf('\n', argsEnd);
            insertAt = lineEnd < 0 ? source.length : lineEnd + 1;
        }

        return { argsStart: argsStart, argsEnd: argsEnd, indent: indent, insertAt: insertAt };
    };

    var endReportPattern = /\b(?:PDF\.EndReport|(?:DLH|IH|DataLinqHelper|IntegratorHelper)\.EndPdfReport)\s*\(\s*\)/g;

    // index of the ')' closing the call whose arguments start at 'from' (skips strings), -1 if not found
    var closingParenthesis = function (source, from) {
        var depth = 0;
        for (var i = from; i < source.length; i++) {
            var c = source.charAt(i);
            if (c === '"' || c === '\'') {
                for (i++; i < source.length && source.charAt(i) !== c; i++) {
                    if (source.charAt(i) === '\\') i++;
                }
            } else if (c === '(') {
                depth++;
            } else if (c === ')') {
                if (depth-- === 0) return i;
            }
        }
        return -1;
    };

    // Generic source actions sent by the preview (bridge message source.action), registered by
    // the tool modules in pdf-editing/tools/*.js:
    //   action(source, args) -> { start, end, text } (replace source[start..end] with text)
    //                         | { message } (failed) | null
    var actions = {};

    this.registerAction = function (name, action) {
        actions[name] = action;
    };

    this.getAction = function (name) {
        return Object.prototype.hasOwnProperty.call(actions, name) ? actions[name] : null;
    };

    /**
     * Finds the complete source of a draggable (open call .. end call), including
     * the whole lines if the calls are on their own lines.
     * @param {string} source view source
     * @param {{index:number, originalX:number, originalY:number}} args
     * @returns {{start:number, end:number}|null}
     */
    this.findElement = function (source, args) {
        var call = this.findCall(source, args);
        var close = call ? nthMatch(endDraggablePattern, source, 0, call.argsEnd) : null;
        if (!close) {
            return null;
        }

        var start = call.start;
        if (source.charAt(start - 1) === '@') start--;
        var lineStart = source.lastIndexOf('\n', start - 1) + 1;
        var wholeLines = /^\s*$/.test(source.substring(lineStart, start));

        var end = close.index + close[0].length;
        var lineEnd = source.indexOf('\n', end);
        if (lineEnd < 0) lineEnd = source.length;

        if (wholeLines && /^\s*$/.test(source.substring(end, lineEnd))) {
            return { start: lineStart, end: Math.min(lineEnd + 1, source.length) };
        }
        return { start: start, end: end };
    };

    /**
     * Finds the editable plain text inside a draggable.
     * @param {string} source view source
     * @param {{index:number, originalX:number, originalY:number}} args
     * @returns {{start:number, end:number}|null}
     */
    this.findText = function (source, args) {
        var call = this.findCall(source, args);
        var end = call ? nthMatch(endDraggablePattern, source, 0, call.argsEnd) : null;
        if (!end) {
            return null;
        }

        var body = source.substring(call.argsEnd, end.index);
        for (var type in elementTemplates) {
            var m = elementTemplates[type].textPattern.exec(body);
            if (m) {
                var start = call.argsEnd + m.index + m[0].indexOf(m[1], m[0].indexOf('>') + 1);
                return { start: start, end: start + m[1].length };
            }
        }
        return null;
    };

    /**
     * Finds the formatted text element inside a draggable (template.formattedPattern:
     * group 1 = tag name, group 2 = attributes, group 3 = allow-listed inner content).
     * @param {string} source view source
     * @param {{index:number, originalX:number, originalY:number}} args
     * @returns {{start:number, innerEnd:number, tag:string, attributes:string}|null}
     */
    this.findFormattedText = function (source, args) {
        var call = this.findCall(source, args);
        var end = call ? nthMatch(endDraggablePattern, source, 0, call.argsEnd) : null;
        if (!end) {
            return null;
        }

        var body = source.substring(call.argsEnd, end.index);
        for (var type in elementTemplates) {
            var pattern = elementTemplates[type].formattedPattern;
            var m = pattern ? pattern.exec(body) : null;
            if (m) {
                var start = call.argsEnd + m.index;
                return {
                    start: start,
                    innerEnd: start + m[0].length - ('</' + m[1] + '>').length,
                    tag: m[1],
                    attributes: m[2] || ''
                };
            }
        }
        return null;
    };
}();
