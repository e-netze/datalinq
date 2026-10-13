/**
 * PDF editing: placeholders (tokens) in text elements edited in the PDF preview
 * (see E.DataLinq.Web pdf-report/text-format.js, same grammar):
 *
 *   @DLH.QueryParameter(key, required = false, regex = "", defaultValue = "")
 *       only plain string literals ("..." / @"...") and true/false, positional or named arguments
 *   @varName / @(varName)
 *       simple variable (no member access, calls, indexers; no Razor keywords)
 *
 * In formatted html (preview <-> editor) a token is <dl-token data-expr="EXPR"></dl-token>.
 * Depends on: nothing (loaded before rich-text.js)
 */
var pdfTokens = new function () {
    var STR = '"(?:[^"\\\\\\r\\n]|\\\\.)*"|@"(?:[^"]|"")*"';
    var ARG_NAMES = ['key', 'required', 'regex', 'defaultValue'];
    var ARG = '(?:(?:' + ARG_NAMES.join('|') + ')\\s*:\\s*)?(?:' + STR + '|true|false)';
    var KEYWORDS = 'if|else|for|foreach|while|do|switch|case|try|catch|finally|lock|using|model|section|functions|' +
        'inherits|inject|await|helper|namespace|page|layout|code|implements|attribute|preservewhitespace|typeparam|rendermode|' +
        'true|false|null|new|this|default|typeof|nameof|var|return';
    var NAME = '(?!(?:' + KEYWORDS + ')\\b)[A-Za-z_]\\w*';

    /** source of @DLH.QueryParameter(...) */
    this.qpPatternSource = '@DLH\\.QueryParameter\\(\\s*' + ARG + '(?:\\s*,\\s*' + ARG + '){0,3}\\s*\\)';
    /** source of @name / @(name) (not followed by member access, call or indexer) */
    this.varPatternSource = '@\\(\\s*' + NAME + '\\s*\\)|@' + NAME + '(?![\\w(\\[]|\\.[A-Za-z_])';
    /** both, for inner content patterns */
    this.patternSource = '(?:' + this.qpPatternSource + '|' + this.varPatternSource + ')';

    var qpFull = new RegExp('^' + this.qpPatternSource + '$');
    var varFull = new RegExp('^(?:@\\(\\s*(' + NAME + ')\\s*\\)|@(' + NAME + '))$');
    var argRegex = new RegExp('\\s*(?:(' + ARG_NAMES.join('|') + ')\\s*:\\s*)?(' + STR + '|true|false)\\s*(?:,|$)', 'y');

    var unquote = function (literal) {
        if (literal.charAt(0) === '@') return literal.slice(2, -1).replace(/""/g, '"');
        return literal.slice(1, -1).replace(/\\(.)/g, function (m, c) {
            return c === 'n' ? '\n' : c === 't' ? '\t' : c === 'r' ? '\r' : c === '0' ? '' : c;
        });
    };

    /**
     * @param {string} expr
     * @returns {{key:string, required:boolean, regex:string, defaultValue:string}|null}
     */
    this.parseQueryParameter = function (expr) {
        expr = String(expr || '').trim();
        if (!qpFull.test(expr)) return null;
        var args = expr.substring(expr.indexOf('(') + 1, expr.length - 1);
        var result = { key: null, required: false, regex: '', defaultValue: '' };
        argRegex.lastIndex = 0;
        var position = 0, m;
        while (argRegex.lastIndex < args.length && (m = argRegex.exec(args)) !== null) {
            var name = m[1] || ARG_NAMES[position];
            position++;
            if (!name) return null;
            var raw = m[2];
            var isBool = raw === 'true' || raw === 'false';
            if ((name === 'required') !== isBool) return null;
            result[name] = isBool ? raw === 'true' : unquote(raw);
            if (m[0] === '' ) break;
        }
        return typeof result.key === 'string' && result.key ? result : null;
    };

    var literal = function (s) {
        return '"' + String(s || '').replace(/[\r\n]+/g, ' ').replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
    };

    /**
     * @param {{key:string, required:boolean, regex:string, defaultValue:string}} p
     * @returns {string} @DLH.QueryParameter(...) (trailing empty optional arguments omitted)
     */
    this.formatQueryParameter = function (p) {
        var args = [literal(p.key)];
        if (p.required || p.regex || p.defaultValue) args.push(p.required ? 'true' : 'false');
        if (p.regex || p.defaultValue) args.push(literal(p.regex));
        if (p.defaultValue) args.push(literal(p.defaultValue));
        return '@DLH.QueryParameter(' + args.join(', ') + ')';
    };

    /** @returns {string|null} the variable name of @name / @(name) */
    this.parseVariable = function (expr) {
        var m = varFull.exec(String(expr || '').trim());
        return m ? (m[1] || m[2]) : null;
    };

    this.isValid = function (expr) {
        return !!(this.parseQueryParameter(expr) || this.parseVariable(expr));
    };

    /**
     * @param {string} name
     * @param {string} nextChar the character following in the source ('' at the end)
     * @returns {string} @name, or @(name) if the next character would continue the expression
     */
    this.formatVariable = function (name, nextChar) {
        return /^[\w.(\[]/.test(nextChar || '') ? '@(' + name + ')' : '@' + name;
    };

    var escapeAttribute = function (s) {
        return s.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    };

    var scan = new RegExp('@@|(' + this.qpPatternSource + ')|(' + this.varPatternSource + ')|@', 'g');

    /**
     * Converts the inner source of a text element for the preview: tokens -> <dl-token>, @@ -> @.
     * @param {string} inner
     * @returns {{html:string, tokens:number}|null} null if it contains other Razor code
     */
    this.sourceToTokenHtml = function (inner) {
        var failed = false, tokens = 0;
        var html = String(inner).replace(scan, function (m, qp, variable, offset, all) {
            if (m === '@@') return '@';
            var before = offset > 0 ? all.charAt(offset - 1) : '';
            if (variable && /[A-Za-z0-9]/.test(before) && variable.charAt(1) !== '(') return '@';   // e-mail address
            if (qp || variable) {
                tokens++;
                return '<dl-token data-expr="' + escapeAttribute(m) + '"></dl-token>';
            }
            if (/[A-Za-z0-9]/.test(before)) return '@';
            failed = true;
            return m;
        });
        return failed ? null : { html: html, tokens: tokens };
    };
}();
