/**
 * PDF editing tool: new page
 *
 * Source action 'page.insert' (see E.DataLinq.Web pdf-report/tools/new-page.js):
 *   { page: number, position: 'before'|'after', options: { paperSize, landscape, usePageTemplate,
 *     templateId, dynamic, dynamicUseTemplate, defaultMarginTop, defaultMarginBottom, repeatHeader } }
 * Inserts @PDF.NewPage(...) ... @PDF.EndPage() before/after the given page. Only non default
 * options are written (named arguments).
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var PAPER_SIZES = ['A4', 'A1', 'A2', 'A3', 'A5', 'A6'];
    // endpoint@query@view, optionally with a query string incl. interpolations, e.g. id@q@v?eid={@eid}
    var TEMPLATE_ID = /^[\w\-@.]{0,200}(\?[^"\\\r\n]{0,400})?$/;

    // interpolated string ($"...") if the id contains {expression}
    var templateIdLiteral = function (id) {
        return (/[{}]/.test(id) ? '$"' : '"') + id + '"';
    };

    var isNumber = function (v) { return typeof v === 'number' && isFinite(v) && v >= 0 && v <= 10000; };
    var num = function (v) { return pdfDraggableSource.formatNumber(v); };

    var buildArguments = function (o, newLine, indent) {
        var args = [];

        if (o.usePageTemplate || o.templateId) {
            var template = [];
            if (o.usePageTemplate) template.push('UsePageTemplate = true');
            if (o.templateId) template.push('TemplateId = ' + templateIdLiteral(o.templateId));
            args.push('pageTemplateOptions: new PageTemplateOptions { ' + template.join(', ') + ' }');
        }

        if (o.dynamic) {
            var dynamic = ['Dynamic = true'];
            if (o.dynamicUseTemplate) dynamic.push('DynamicUseTemplate = true');
            if (o.defaultMarginTop !== 10) dynamic.push('DefaultMarginTop = ' + num(o.defaultMarginTop));
            if (o.defaultMarginBottom !== 10) dynamic.push('DefaultMarginBottom = ' + num(o.defaultMarginBottom));
            if (!o.repeatHeader) dynamic.push('RepeatHeader = false');
            args.push('dynamicTableOptions: new DynamicTableOptions { ' + dynamic.join(', ') + ' }');
        }

        if (o.paperSize !== 'A4') args.push('paperSize: PaperSize.' + o.paperSize);
        if (o.landscape) args.push('landscape: true');

        if (args.length <= 1) {
            return args.join('');
        }
        return newLine + args.map(function (a) { return indent + '    ' + a; }).join(',' + newLine) + newLine + indent;
    };

    var readOptions = function (raw) {
        raw = raw && typeof raw === 'object' ? raw : {};
        var o = {
            paperSize: PAPER_SIZES.indexOf(raw.paperSize) >= 0 ? raw.paperSize : 'A4',
            landscape: raw.landscape === true,
            usePageTemplate: raw.usePageTemplate === true,
            templateId: typeof raw.templateId === 'string' ? raw.templateId.trim() : '',
            dynamic: raw.dynamic === true,
            dynamicUseTemplate: raw.dynamicUseTemplate === true,
            defaultMarginTop: isNumber(raw.defaultMarginTop) ? raw.defaultMarginTop : 10,
            defaultMarginBottom: isNumber(raw.defaultMarginBottom) ? raw.defaultMarginBottom : 10,
            repeatHeader: raw.repeatHeader !== false
        };
        return TEMPLATE_ID.test(o.templateId) ? o : null;
    };

    pdfDraggableSource.registerAction('page.insert', function (source, args) {
        if (!Number.isInteger(args.page) || args.page < 0 || (args.position !== 'before' && args.position !== 'after')) {
            return null;
        }
        var options = readOptions(args.options);
        if (!options) {
            return { message: 'Invalid page options.' };
        }

        var page = pdfDraggableSource.findPage(source, args.page);
        if (!page) {
            return { message: 'Could not find the page (NewPage/EndPage) in the source.' };
        }

        var newLine = pdfDraggableSource.newLineOf(source);
        var helper = 'PDF', at = page.at, indent = page.indent;
        var text =
            indent + at + helper + '.NewPage(' + buildArguments(options, newLine, indent) + ')' + newLine +
            newLine +
            indent + '    ' + '@* new page *@' + newLine +
            newLine +
            indent + at + helper + '.EndPage()' + newLine;

        var offset = args.position === 'before' ? page.start : page.end;
        if (args.position === 'before') {
            text += newLine;
        } else {
            text = newLine + text;
        }

        return { start: offset, end: offset, text: text };
    });

    // 'page.delete': { page: number } -> removes NewPage ... EndPage (incl. one adjacent blank line)
    pdfDraggableSource.registerAction('page.delete', function (source, args) {
        if (!Number.isInteger(args.page) || args.page < 0) {
            return { message: 'Invalid page.' };
        }

        var page = pdfDraggableSource.findPage(source, args.page);
        if (!page) {
            return { message: 'Could not find the page (NewPage/EndPage) in the source.' };
        }

        var end = page.end;
        var blank = /^[ \t]*\r?\n/.exec(source.substring(end));
        if (blank) {
            end += blank[0].length;
        }

        return { start: page.start, end: end, text: '' };
    });

    var parseOptions = function (text) {
        var bool = function (name, fallback) {
            var m = new RegExp('\\b' + name + '\\s*[:=]\\s*(true|false)\\b', 'i').exec(text);
            return m ? m[1].toLowerCase() === 'true' : fallback;
        };
        var number = function (name, fallback) {
            var m = new RegExp('\\b' + name + '\\s*=\\s*([-+]?(?:\\d+(?:\\.\\d*)?|\\.\\d+))').exec(text);
            return m ? parseFloat(m[1]) : fallback;
        };
        var paperSize = /\bPaperSize\.(\w+)/.exec(text);
        var templateId = /\bTemplateId\s*=\s*(?:\$@|@\$|\$|@)?"([^"\r\n]*)"/.exec(text);

        return {
            paperSize: paperSize && PAPER_SIZES.indexOf(paperSize[1]) >= 0 ? paperSize[1] : 'A4',
            landscape: bool('landscape', false),
            usePageTemplate: bool('UsePageTemplate', false),
            templateId: templateId ? templateId[1] : '',
            dynamic: bool('Dynamic', false),
            dynamicUseTemplate: bool('DynamicUseTemplate', false),
            defaultMarginTop: number('DefaultMarginTop', 10),
            defaultMarginBottom: number('DefaultMarginBottom', 10),
            repeatHeader: bool('RepeatHeader', true)
        };
    };

    var findPageWithArgs = function (source, args) {
        if (!Number.isInteger(args.page) || args.page < 0) {
            return { message: 'Invalid page.' };
        }
        var page = pdfDraggableSource.findPage(source, args.page);
        if (!page || page.argsEnd < 0) {
            return { message: 'Could not find the page (NewPage/EndPage) in the source.' };
        }
        return page;
    };

    // 'page.options.get': { page: number } -> { data: options } (current NewPage options, read only)
    pdfDraggableSource.registerAction('page.options.get', function (source, args) {
        var page = findPageWithArgs(source, args);
        if (page.message) return page;

        return { data: parseOptions(source.substring(page.argsStart, page.argsEnd)) };
    });

    // 'page.options.set': { page: number, options } -> replaces the NewPage(...) arguments
    pdfDraggableSource.registerAction('page.options.set', function (source, args) {
        var page = findPageWithArgs(source, args);
        if (page.message) return page;

        var options = readOptions(args.options);
        if (!options) {
            return { message: 'Invalid page options.' };
        }

        return {
            start: page.argsStart,
            end: page.argsEnd,
            text: buildArguments(options, pdfDraggableSource.newLineOf(source), page.indent)
        };
    });

    // 'page.move': { page: number, direction: 'up'|'down' } -> swaps the page with its neighbour
    pdfDraggableSource.registerAction('page.move', function (source, args) {
        if (args.direction !== 'up' && args.direction !== 'down') {
            return { message: 'Invalid direction.' };
        }
        var page = findPageWithArgs(source, args);
        if (page.message) return page;

        var other = pdfDraggableSource.findPage(source, args.direction === 'up' ? args.page - 1 : args.page + 1);
        if (args.page + (args.direction === 'up' ? -1 : 1) < 0 || !other) {
            return { message: 'The page can not be moved ' + args.direction + '.' };
        }

        var first = args.direction === 'up' ? other : page;
        var second = args.direction === 'up' ? page : other;
        if (first.end > second.start) {
            return { message: 'The pages overlap in the source (nested NewPage/EndPage?).' };
        }

        return {
            start: first.start,
            end: second.end,
            text: source.substring(second.start, second.end) +
                source.substring(first.end, second.start) +
                source.substring(first.start, first.end)
        };
    });

    // 'page.append': { options } -> appends a page at the end of the report (also for reports without pages)
    pdfDraggableSource.registerAction('page.append', function (source, args) {
        var options = readOptions(args.options);
        if (!options) {
            return { message: 'Invalid page options.' };
        }
        var report = pdfDraggableSource.findReport(source);
        if (!report) {
            return { message: 'Could not find @PDF.BeginReport(...) in the source.' };
        }

        var newLine = pdfDraggableSource.newLineOf(source);
        var indent = report.indent + '    ';
        var text =
            indent + '@PDF.NewPage(' + buildArguments(options, newLine, indent) + ')' + newLine +
            newLine +
            indent + '    ' + '@* new page *@' + newLine +
            newLine +
            indent + '@PDF.EndPage()' + newLine;

        return { start: report.insertAt, end: report.insertAt, text: text };
    });

    // 'page.duplicate': { page: number } -> inserts a copy of the page after it
    pdfDraggableSource.registerAction('page.duplicate', function (source, args) {
        var page = findPageWithArgs(source, args);
        if (page.message) return page;

        var newLine = pdfDraggableSource.newLineOf(source);
        var text = source.substring(page.start, page.end);
        if (!/\n$/.test(text)) {
            text += newLine;
        }

        return { start: page.end, end: page.end, text: newLine + text };
    });
})();
