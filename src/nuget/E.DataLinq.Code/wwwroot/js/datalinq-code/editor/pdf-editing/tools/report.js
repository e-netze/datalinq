/**
 * PDF editing tool: report options
 *
 * Source actions (see E.DataLinq.Web pdf-report/tools/report-options.js):
 *   'report.options.get': {} -> { data: options } (current @PDF.BeginReport(...) options)
 *   'report.options.set': { options } -> replaces the BeginReport(...) arguments
 *   options: { usePageNumbers, pageNumberPosition, pageNumberType, pageNumberSkipPages,
 *              usePageDateTime, dateTimePosition, dateTimeFormat, dateTimeSkipPages,
 *              quality, downloadButton, fileName }
 * Only non default options are written (named arguments).
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var QUALITIES = ['Best', 'High', 'Medium', 'Low', 'Preview'];
    var FORMAT = /^[^"\\{}\r\n]{0,100}$/;
    var FILE_NAME = /^[\w\-. ]{1,200}$/;
    var DEFAULT_FORMAT = 'HH:mm dd.MM.yyyy';

    var isInt = function (v, max) { return Number.isInteger(v) && v >= 0 && v <= max; };

    // body of "new <className> { ... }" (or "new() { ... }" passed by name) in the arguments
    var initializer = function (text, className, argName) {
        var m = new RegExp('\\bnew\\s+' + className + '\\s*(?:\\(\\s*\\))?\\s*\\{([^{}]*)\\}').exec(text) ||
            new RegExp('\\b' + argName + '\\s*:\\s*new\\s*\\(\\s*\\)\\s*\\{([^{}]*)\\}').exec(text);
        return m ? m[1] : '';
    };

    var parseOptions = function (text) {
        var bool = function (body, name, fallback) {
            var m = new RegExp('\\b' + name + '\\s*[:=]\\s*(true|false)\\b').exec(body);
            return m ? m[1] === 'true' : fallback;
        };
        var int = function (body, name, fallback) {
            var m = new RegExp('\\b' + name + '\\s*=\\s*(\\d+)\\b').exec(body);
            return m ? parseInt(m[1], 10) : fallback;
        };
        var str = function (body, name, separator) {
            var m = new RegExp('\\b' + name + '\\s*' + separator + '\\s*(?:\\$@|@\\$|\\$|@)?"([^"{}]*)"').exec(body);
            return m ? m[1] : null;
        };

        var numbers = initializer(text, 'PageNumberOptions', 'pageNumberOptions');
        var dateTime = initializer(text, 'PageDateTimeOptions', 'pageDateTimeOptions');
        var quality = /\bPdfQuality\.(\w+)/.exec(text);

        return {
            usePageNumbers: bool(numbers, 'UsePageNumbers', false),
            pageNumberPosition: int(numbers, 'Position', 0),
            pageNumberType: int(numbers, 'Type', 0),
            pageNumberSkipPages: int(numbers, 'SkipPages', 0),
            usePageDateTime: bool(dateTime, 'UsePageDateTime', false),
            dateTimePosition: int(dateTime, 'Position', 0),
            dateTimeFormat: str(dateTime, 'Format', '=') || DEFAULT_FORMAT,
            dateTimeSkipPages: int(dateTime, 'SkipPages', 0),
            quality: quality && QUALITIES.indexOf(quality[1]) >= 0 ? quality[1] : 'High',
            downloadButton: bool(text, 'download_button', false),
            fileName: str(text, 'fileName', ':') || 'dataLinqPdfReport'
        };
    };

    var readOptions = function (raw) {
        raw = raw && typeof raw === 'object' ? raw : {};
        var o = {
            usePageNumbers: raw.usePageNumbers === true,
            pageNumberPosition: isInt(raw.pageNumberPosition, 2) ? raw.pageNumberPosition : 0,
            pageNumberType: isInt(raw.pageNumberType, 1) ? raw.pageNumberType : 0,
            pageNumberSkipPages: isInt(raw.pageNumberSkipPages, 10000) ? raw.pageNumberSkipPages : 0,
            usePageDateTime: raw.usePageDateTime === true,
            dateTimePosition: isInt(raw.dateTimePosition, 2) ? raw.dateTimePosition : 0,
            dateTimeFormat: typeof raw.dateTimeFormat === 'string' && raw.dateTimeFormat.trim() ? raw.dateTimeFormat.trim() : DEFAULT_FORMAT,
            dateTimeSkipPages: isInt(raw.dateTimeSkipPages, 10000) ? raw.dateTimeSkipPages : 0,
            quality: QUALITIES.indexOf(raw.quality) >= 0 ? raw.quality : 'High',
            downloadButton: raw.downloadButton === true,
            fileName: typeof raw.fileName === 'string' && raw.fileName.trim() ? raw.fileName.trim() : 'dataLinqPdfReport'
        };
        return FORMAT.test(o.dateTimeFormat) && FILE_NAME.test(o.fileName) ? o : null;
    };

    var buildArguments = function (o, newLine, indent) {
        var args = [];

        if (o.usePageNumbers) {
            var numbers = ['UsePageNumbers = true'];
            if (o.pageNumberPosition !== 0) numbers.push('Position = ' + o.pageNumberPosition);
            if (o.pageNumberType !== 0) numbers.push('Type = ' + o.pageNumberType);
            if (o.pageNumberSkipPages !== 0) numbers.push('SkipPages = ' + o.pageNumberSkipPages);
            args.push('pageNumberOptions: new PageNumberOptions { ' + numbers.join(', ') + ' }');
        }

        if (o.usePageDateTime) {
            var dateTime = ['UsePageDateTime = true'];
            if (o.dateTimePosition !== 0) dateTime.push('Position = ' + o.dateTimePosition);
            if (o.dateTimeFormat !== DEFAULT_FORMAT) dateTime.push('Format = "' + o.dateTimeFormat + '"');
            if (o.dateTimeSkipPages !== 0) dateTime.push('SkipPages = ' + o.dateTimeSkipPages);
            args.push('pageDateTimeOptions: new PageDateTimeOptions { ' + dateTime.join(', ') + ' }');
        }

        if (o.quality !== 'High') args.push('quality: PdfQuality.' + o.quality);
        if (o.downloadButton) args.push('download_button: true');
        if (o.fileName !== 'dataLinqPdfReport') args.push('fileName: "' + o.fileName + '"');

        if (args.length <= 1) {
            return args.join('');
        }
        return newLine + args.map(function (a) { return indent + '    ' + a; }).join(',' + newLine) + newLine + indent;
    };

    var findReport = function (source) {
        return pdfDraggableSource.findReport(source) ||
            { message: 'Could not find @PDF.BeginReport(...) in the source.' };
    };

    pdfDraggableSource.registerAction('report.options.get', function (source) {
        var report = pdfDraggableSource.findReport(source);
        if (!report) {
            var defaults = parseOptions('');
            defaults.exists = false;
            return { data: defaults };
        }

        var data = parseOptions(source.substring(report.argsStart, report.argsEnd));
        data.exists = true;
        return { data: data };
    });

    // 'report.create': { options } -> appends @PDF.BeginReport(...) with one empty page and @PDF.EndReport()
    pdfDraggableSource.registerAction('report.create', function (source, args) {
        if (pdfDraggableSource.findReport(source)) {
            return { message: 'The view already contains a report (BeginReport).' };
        }
        var options = readOptions(args.options);
        if (!options) {
            return { message: 'Invalid report options.' };
        }

        var newLine = pdfDraggableSource.newLineOf(source);
        var text =
            (source.length > 0 && !/\n$/.test(source) ? newLine : '') + newLine +
            '@PDF.BeginReport(' + buildArguments(options, newLine, '') + ')' + newLine +
            newLine +
            '    @PDF.NewPage()' + newLine +
            newLine +
            '        @* new page *@' + newLine +
            newLine +
            '    @PDF.EndPage()' + newLine +
            newLine +
            '@PDF.EndReport()' + newLine;

        return { start: source.length, end: source.length, text: text };
    });

    pdfDraggableSource.registerAction('report.options.set', function (source, args) {
        var report = findReport(source);
        if (report.message) return report;

        var options = readOptions(args.options);
        if (!options) {
            return { message: 'Invalid report options.' };
        }

        return {
            start: report.argsStart,
            end: report.argsEnd,
            text: buildArguments(options, pdfDraggableSource.newLineOf(source), report.indent)
        };
    });
})();
