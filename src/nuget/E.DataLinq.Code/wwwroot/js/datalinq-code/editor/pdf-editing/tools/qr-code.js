/**
 * PDF editing tool: QR code
 *
 * Source template for QR codes added in the PDF preview (see E.DataLinq.Web pdf-report/tools/qr-code.js).
 * The preview sends the settings as JSON text:
 *   { kind: 'qr', qrType: 'url', url, htmlAttributes }
 *     -> @(PDF.GenerateUrlQRCode("url", new { ... }))
 *   { kind: 'qr', qrType: 'mail', reciever, subject, body, mailEncoding }
 *     -> @(PDF.GenerateMailQRCode(new MailQrCodeDetails { reciever = "..", ..., mailEncoding = QrCodeMailEncoding.MAILTO }))
 *   { kind: 'qr', qrType: 'geo', latitude, longitude, geolocationEncoding }  -> GeoLocationQrCodeDetails
 *   { kind: 'qr', qrType: 'giro', iban, bic, name, amount, purpose }        -> GiroQrCodeDetails
 * String values are C# string literals (or DLH.GetConstant/SECURITY.GetSecret), enums are whitelisted,
 * the amount is a plain decimal number -> no code injection.
 *
 * Source actions (edit an existing QR code, double click / context menu in the preview):
 *   'qr.get': { index, originalX, originalY } -> { data: settings }
 *   'qr.set': { index, originalX, originalY, settings } -> replaces the PDF.Generate...QRCode(...) expression
 * Depends on: pdf-editing/pdf-source.js, pdf-editing/tools/image.js (window.pdfSourceCsUtils)
 */
(function () {
    var cs = window.pdfSourceCsUtils;
    var CALL = /\bPDF\.(Generate(?:Url|Mail|GeoLocation|Giro)QRCode)\s*\(/g;

    var QR_METHODS = {
        GenerateUrlQRCode: 'url',
        GenerateMailQRCode: 'mail',
        GenerateGeoLocationQRCode: 'geo',
        GenerateGiroQRCode: 'giro'
    };
    var QR_TYPES = {
        url: { method: 'GenerateUrlQRCode' },
        mail: {
            method: 'GenerateMailQRCode', className: 'MailQrCodeDetails',
            strings: ['reciever', 'subject', 'body'], required: ['reciever'],
            enumMember: 'mailEncoding', enumType: 'QrCodeMailEncoding', enumValues: ['MAILTO', 'MATMSG', 'SMTP']
        },
        geo: {
            method: 'GenerateGeoLocationQRCode', className: 'GeoLocationQrCodeDetails',
            strings: ['latitude', 'longitude'], required: ['latitude', 'longitude'],
            enumMember: 'geolocationEncoding', enumType: 'QrCodeGeolocationEncoding', enumValues: ['GEO', 'GoogleMaps']
        },
        giro: {
            method: 'GenerateGiroQRCode', className: 'GiroQrCodeDetails',
            strings: ['iban', 'bic', 'name', 'purpose'], required: ['iban', 'bic', 'name'],
            amount: true
        }
    };
    var AMOUNT = /^\d{1,12}(?:\.\d{1,2})?$/;
    var MAX_TEXT = 2000;

    // helper calls allowed anywhere inside QR text values, e.g. https://x.at/?id=DLH.QueryParameter("id")
    // -> "https://x.at/?id=" + DLH.QueryParameter("id"). Only a plain string argument (no " \ or line breaks).
    var HELPER = /(?:(?:DLH|IH|DataLinqHelper|IntegratorHelper)\.(?:QueryParameter|GetConstant)|SECURITY\.GetSecret)\(\s*"[^"\\\r\n]*"\s*\)/g;

    var normalizeHelper = function (text) {
        return text.replace(/\(\s*/, '(').replace(/\s*\)$/, ')');
    };

    // text value -> C# string expression (literals escaped, helper calls emitted as code)
    var csValue = function (value) {
        var parts = [], last = 0, m;
        var regex = new RegExp(HELPER.source, 'g');
        while ((m = regex.exec(value))) {
            if (m.index > last) parts.push(cs.csString(value.substring(last, m.index)));
            parts.push(normalizeHelper(m[0]));
            last = m.index + m[0].length;
        }
        if (last < value.length || !parts.length) parts.push(cs.csString(value.substring(last)));
        return parts.join(' + ');
    };

    // C# string expression (".." + DLH.QueryParameter("..") + ..) -> text value, null if not editable
    var parseValue = function (text) {
        var pieces = splitPlus(text);
        if (!pieces) return null;
        var result = '';
        for (var i = 0; i < pieces.length; i++) {
            var piece = pieces[i].trim();
            if (new RegExp('^(?:' + HELPER.source + ')$').test(piece)) {
                result += normalizeHelper(piece);
                continue;
            }
            var literal = cs.parseString(piece);
            if (literal === null) return null;
            result += literal;
        }
        return result;
    };

    // splits at top level + (outside strings and brackets)
    var splitPlus = function (text) {
        var parts = [], depth = 0, start = 0;
        for (var i = 0; i < text.length; i++) {
            var c = text.charAt(i);
            if (c === '"' || (c === '@' && text.charAt(i + 1) === '"')) {
                var end = cs.skipString(text, i);
                if (end < 0) return null;
                i = end - 1;
            } else if (c === '(') {
                depth++;
            } else if (c === ')') {
                if (--depth < 0) return null;
            } else if (c === '+' && depth === 0) {
                parts.push(text.substring(start, i));
                start = i + 1;
            }
        }
        if (depth !== 0) return null;
        parts.push(text.substring(start));
        return parts;
    };

    // ---- build ----------------------------------------------------------------------------

    var readConfig = function (raw) {
        if (!raw || typeof raw !== 'object' || raw.kind !== 'qr') return null;
        var type = QR_TYPES[raw.qrType];
        if (!type) return null;

        var attributes = (Array.isArray(raw.htmlAttributes) ? raw.htmlAttributes : []).slice(0, cs.MAX_PAIRS + 1);
        if (attributes.length > cs.MAX_PAIRS) return null;
        var htmlAttributes = [];
        for (var a = 0; a < attributes.length; a++) {
            var p = attributes[a];
            if (!p || typeof p.key !== 'string') return null;
            var v = typeof p.value === 'number' ? String(p.value) : p.value;
            // no src/srcset/on*/asp-* and no unsafe styles (same rules as embedded images)
            if (!cs.isAllowedAttribute(p.key.replace(/_/g, '-'), v)) return null;
            htmlAttributes.push({ key: p.key, value: v });
        }

        var config = { kind: 'qr', qrType: raw.qrType, htmlAttributes: htmlAttributes };
        var text = function (value) { return typeof value === 'string' && value.length <= MAX_TEXT ? value.trim() : null; };

        if (raw.qrType === 'url') {
            config.url = text(raw.url);
            return config.url ? config : null;
        }
        for (var i = 0; i < type.strings.length; i++) {
            var name = type.strings[i], value = raw[name] == null ? '' : text(raw[name]);
            if (value === null || (!value && type.required.indexOf(name) >= 0)) return null;
            config[name] = value;
        }
        if (type.enumMember) {
            config[type.enumMember] = type.enumValues.indexOf(raw[type.enumMember]) >= 0 ? raw[type.enumMember] : type.enumValues[0];
        }
        if (type.amount) {
            var amount = String(raw.amount == null || raw.amount === '' ? '0' : raw.amount).trim().replace(',', '.');
            if (!AMOUNT.test(amount)) return null;
            config.amount = amount;
        }
        return config;
    };

    // PDF.Generate...QRCode(...) (without @( ... ))
    var buildCall = function (config) {
        var type = QR_TYPES[config.qrType];
        var htmlAttributes = cs.anonymousObject(config.htmlAttributes);
        var first;
        if (config.qrType === 'url') {
            first = csValue(config.url);
        } else {
            var members = type.strings.map(function (name) { return name + ' = ' + csValue(config[name]); });
            if (type.enumMember) members.push(type.enumMember + ' = ' + type.enumType + '.' + config[type.enumMember]);
            if (type.amount) members.push('amount = ' + config.amount + 'm');
            first = 'new ' + type.className + ' { ' + members.join(', ') + ' }';
        }
        return 'PDF.' + type.method + '(' + first + (htmlAttributes !== 'null' ? ', ' + htmlAttributes : '') + ')';
    };

    var buildSource = function (config) {
        return '@(' + buildCall(config) + ')';
    };

    // ---- parse ----------------------------------------------------------------------------

    var parseCall = function (method, text) {
        var qrType = QR_METHODS[method], type = QR_TYPES[qrType];
        var parts = cs.splitTopLevel(text);
        if (!parts || parts.length < 1 || parts.length > 2 || parts.some(function (p) { return /^\s*\w+\s*:/.test(p); })) return null;

        var htmlAttributes = cs.parseObject(parts[1] || '', true);
        if (htmlAttributes === null) return null;
        var raw = { kind: 'qr', qrType: qrType, htmlAttributes: htmlAttributes };

        if (qrType === 'url') {
            raw.url = parseValue(parts[0]);
            if (raw.url === null) return null;
        } else {
            var m = new RegExp('^\\s*new\\s+(?:E\\.DataLinq\\.Web\\.Models\\.Razor\\.)?' + type.className + '\\s*(?:\\(\\s*\\))?\\s*\\{([\\s\\S]*)\\}\\s*$').exec(parts[0]);
            var members = m ? cs.splitTopLevel(m[1]) : null;
            if (!members) return null;
            for (var i = 0; i < members.length; i++) {
                if (!members[i].trim()) continue;
                var mm = /^\s*(\w+)\s*=\s*([\s\S]*?)\s*$/.exec(members[i]);
                if (!mm) return null;
                var name = mm[1], value = mm[2];
                if (type.strings.indexOf(name) >= 0) {
                    raw[name] = parseValue(value);
                    if (raw[name] === null) return null;
                } else if (name === type.enumMember) {
                    var e = new RegExp('^(?:E\\.DataLinq\\.Web\\.Models\\.Razor\\.)?' + type.enumType + '\\.(\\w+)$').exec(value);
                    if (!e || type.enumValues.indexOf(e[1]) < 0) return null;
                    raw[name] = e[1];
                } else if (name === 'amount' && type.amount) {
                    var a = /^(\d+(?:\.\d+)?)[mM]?$/.exec(value);
                    if (!a) return null;
                    raw.amount = a[1];
                } else {
                    return null;   // unknown/unsupported member (e.g. purposeType) -> edit in source
                }
            }
        }
        return readConfig(raw);
    };

    // first PDF.Generate...QRCode(...) call inside the draggable:
    //   { method, argsStart, argsEnd, start, end } or { message }
    //   start/end: the whole @(...) / @PDF.X(...) expression if present, otherwise the call
    var findCall = function (source, args) {
        var element = pdfDraggableSource.findElement(source, args);
        if (!element) {
            return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };
        }

        var regex = new RegExp(CALL.source, 'g');
        regex.lastIndex = element.start;
        var m = regex.exec(source);
        if (!m || m.index >= element.end) {
            return { message: 'The draggable does not contain a PDF.Generate...QRCode(...) call.' };
        }

        var argsStart = m.index + m[0].length, depth = 1;
        for (var i = argsStart; i < element.end; i++) {
            var c = source.charAt(i);
            if (c === '"' || (c === '@' && source.charAt(i + 1) === '"')) {
                var end = cs.skipString(source, i);
                if (end < 0) break;
                i = end - 1;
            } else if (c === '(') {
                depth++;
            } else if (c === ')' && --depth === 0) {
                var before = /@(\(\s*)?$/.exec(source.substring(element.start, m.index));
                var after = before && before[1] ? /^\s*\)/.exec(source.substring(i + 1, element.end)) : null;
                var wrapped = !!before && (!before[1] || !!after);
                return {
                    method: m[1], argsStart: argsStart, argsEnd: i,
                    start: wrapped ? element.start + before.index : m.index,
                    end: wrapped ? i + 1 + (after ? after[0].length : 0) : i + 1,
                    wrapped: wrapped
                };
            }
        }
        return { message: 'Could not parse the ' + m[1] + '(...) call.' };
    };

    // ---- registration ---------------------------------------------------------------------

    pdfDraggableSource.registerElementTemplate('qr', {
        content: function (text) {
            var config;
            try {
                config = readConfig(JSON.parse(text));
            } catch (e) {
                config = null;
            }
            return config ? buildSource(config) : '@* invalid QR code settings *@';
        },
        // not text editable
        textPattern: /(?!)/
    });

    pdfDraggableSource.registerAction('qr.get', function (source, args) {
        var call = findCall(source, args);
        if (call.message) return call;
        var data = parseCall(call.method, source.substring(call.argsStart, call.argsEnd));
        return data
            ? { data: data }
            : { message: 'The QR code uses Razor code (variables, expressions, named arguments) and can only be edited in the source.' };
    });

    pdfDraggableSource.registerAction('qr.set', function (source, args) {
        var config = readConfig(args && args.settings);
        if (!config) {
            return { message: 'Invalid QR code settings (required values are missing).' };
        }
        var call = findCall(source, args);
        if (call.message) return call;
        return { start: call.start, end: call.end, text: call.wrapped ? buildSource(config) : buildCall(config) };
    });

    window.pdfQrCodeSource = { findCall: findCall };
})();
