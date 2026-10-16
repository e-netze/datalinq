/**
 * PDF editing tool: data (Model.Records filtering + additional query records via DLH.GetRecordsAsync)
 *
 * Source actions (see E.DataLinq.Web pdf-report/tools/data.js):
 *   'data.list':   {} -> { data: { blocks: [block, ...] } }
 *   'data.set':    { block, originalName? } -> inserts or replaces (regenerates) a data block
 *   'data.remove': { type, name } -> removes a data block
 *   'data.preview':{ block } -> { data: { code } } (generated C#, nothing is changed)
 *
 * Every data block is written as a marked Razor code block. The JSON behind "dl-data:start" is the
 * source of truth for the editor, the C# below is regenerated on every change:
 *
 *   @{
 *       // dl-data:start {"type":"model",...}
 *       Model.Records = Model.Records
 *           .Where(r => ...)
 *           .ToArray();
 *       // dl-data:end
 *   }
 *
 * block: { type: 'model'|'query', name, queryId, filter, orderby, ops: [op, ...], requireRows, errorMessage }
 * op:    { op: 'where', group } | { op: 'orderBy', column, descending } | { op: 'skip'|'take'|'skipLast'|'takeLast', count }
 *      | { op: 'distinct', columns: [] } | { op: 'select', columns: [] } | { op: 'custom', expression }
 * group: { logic: 'and'|'or', not?: bool, items: [condition | group] }
 * condition: { column, operator, value, valueType: 'string'|'number'|'bool'|'date'|'expr' }
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
    var F = 'DataLinqRecordFilter';
    var IDENTIFIER = /^[A-Za-z_][A-Za-z0-9_]{0,63}$/;
    var QUERY_ID = /^[\w\-.]+@[\w\-.]+$/;
    var RESERVED = ['Model', 'DLH', 'PDF', 'DataLinqHelper', 'IH', 'ViewBag', 'ViewData', 'Html', 'Url', 'record', 'records', 'r',
        'var', 'new', 'await', 'async', 'class', 'if', 'else', 'for', 'foreach', 'while', 'return', 'null', 'true', 'false', 'this',
        'string', 'object', 'int', 'bool', 'double', 'decimal', 'using', 'namespace', 'in', 'is', 'as', 'out', 'ref', 'base'];
    var OPERATORS = ['eq', 'ne', 'gt', 'ge', 'lt', 'le', 'contains', 'notContains', 'startsWith', 'endsWith', 'isNull', 'isNotNull', 'in', 'notIn'];
    var VALUE_TYPES = ['string', 'number', 'bool', 'date', 'expr'];
    var MAX_DEPTH = 6;

    var BLOCK_PATTERN = /^([ \t]*)@\{[ \t]*\r?\n[ \t]*\/\/ dl-data:start (.*)\r?\n[\s\S]*?\/\/ dl-data:end[ \t]*\r?\n[ \t]*\}[ \t]*(?:\r?\n|$)/gm;

    // ---------- validation / normalization ----------

    var str = function (v, max) {
        return typeof v === 'string' ? v.substring(0, max || 1000) : '';
    };

    var readCondition = function (raw) {
        raw = raw && typeof raw === 'object' ? raw : {};
        return {
            column: str(raw.column, 200).trim(),
            operator: OPERATORS.indexOf(raw.operator) >= 0 ? raw.operator : 'eq',
            value: str(raw.value, 2000),
            valueType: VALUE_TYPES.indexOf(raw.valueType) >= 0 ? raw.valueType : 'string'
        };
    };

    var readGroup = function (raw, depth) {
        raw = raw && typeof raw === 'object' ? raw : {};
        var items = Array.isArray(raw.items) ? raw.items : [];
        return {
            logic: raw.logic === 'or' ? 'or' : 'and',
            not: raw.not === true,
            items: items.map(function (item) {
                return item && Array.isArray(item.items) && depth < MAX_DEPTH ? readGroup(item, depth + 1) : readCondition(item);
            })
        };
    };

    var readColumns = function (columns) {
        return (Array.isArray(columns) ? columns : [])
            .map(function (c) { return str(c, 200).trim(); })
            .filter(function (c) { return c !== ''; });
    };

    var readOp = function (raw) {
        raw = raw && typeof raw === 'object' ? raw : {};
        switch (raw.op) {
            case 'where': return { op: 'where', group: readGroup(raw.group, 0) };
            case 'orderBy': return { op: 'orderBy', column: str(raw.column, 200).trim(), descending: raw.descending === true };
            case 'skip':
            case 'take':
            case 'skipLast':
            case 'takeLast':
                return { op: raw.op, count: Math.max(0, Math.round(Number(raw.count)) || 0) };
            case 'distinct': return { op: 'distinct', columns: readColumns(raw.columns) };
            case 'select': return { op: 'select', columns: readColumns(raw.columns) };
            case 'custom': return { op: 'custom', expression: str(raw.expression, 4000).trim() };
        }
        return null;
    };

    var readBlock = function (raw) {
        raw = raw && typeof raw === 'object' ? raw : {};
        var block = {
            type: raw.type === 'query' ? 'query' : 'model',
            name: str(raw.name, 64).trim(),
            queryId: str(raw.queryId, 300).trim(),
            filter: str(raw.filter, 2000).trim(),
            orderby: str(raw.orderby, 500).trim(),
            ops: (Array.isArray(raw.ops) ? raw.ops : []).map(readOp).filter(function (o) { return o; }),
            requireRows: raw.requireRows === true,
            errorMessage: str(raw.errorMessage, 500).trim()
        };
        if (block.type === 'model') {
            block.name = 'Model.Records';
            block.queryId = block.filter = block.orderby = '';
        }
        return block;
    };

    var validateGroup = function (group) {
        for (var i = 0; i < group.items.length; i++) {
            var item = group.items[i];
            if (item.items) {
                var message = validateGroup(item);
                if (message) return message;
                continue;
            }
            if (!item.column) return 'Where: a condition has no column.';
            if (item.operator === 'isNull' || item.operator === 'isNotNull') continue;
            if (item.valueType === 'number' && !/^-?\d+(\.\d+)?$/.test(item.value.trim()) && item.operator !== 'in' && item.operator !== 'notIn') {
                return 'Where: "' + item.value + '" is not a number (column ' + item.column + ').';
            }
            if (item.valueType === 'bool' && !/^(true|false)$/i.test(item.value.trim())) {
                return 'Where: "' + item.value + '" is not true/false (column ' + item.column + ').';
            }
            if (item.valueType === 'expr' && !item.value.trim()) {
                return 'Where: empty C# expression (column ' + item.column + ').';
            }
        }
        return null;
    };

    var validateBlock = function (block, existing, originalName) {
        if (block.type === 'query') {
            if (!IDENTIFIER.test(block.name)) return 'Invalid variable name (letters, digits, _; must not start with a digit).';
            if (RESERVED.indexOf(block.name) >= 0) return '"' + block.name + '" is a reserved name.';
            if (!QUERY_ID.test(block.queryId)) return 'Invalid query id (endpoint@query).';
            var duplicate = existing.some(function (b) {
                return b.block.type === 'query' && b.block.name === block.name && b.block.name !== originalName;
            });
            if (duplicate) return 'A data source with the variable name "' + block.name + '" already exists.';
        }
        for (var i = 0; i < block.ops.length; i++) {
            var op = block.ops[i];
            if (op.op === 'where') {
                var message = validateGroup(op.group);
                if (message) return message;
            }
            if (op.op === 'orderBy' && !op.column) return 'Sort: no column.';
            if ((op.op === 'distinct' || op.op === 'select') && op.columns.length === 0) return op.op + ': no columns.';
            if (op.op === 'custom' && !op.expression) return 'Custom LINQ: empty expression.';
        }
        return null;
    };

    // ---------- C# generation ----------

    var csString = function (value) {
        return '"' + String(value)
            .replace(/\\/g, '\\\\')
            .replace(/"/g, '\\"')
            .replace(/\r/g, '\\r')
            .replace(/\n/g, '\\n')
            .replace(/\t/g, '\\t') + '"';
    };

    var literal = function (value, valueType) {
        var v = String(value).trim();
        switch (valueType) {
            case 'number': return /^-?\d+(\.\d+)?$/.test(v) ? v : csString(v);
            case 'bool': return /^true$/i.test(v) ? 'true' : 'false';
            case 'date': return F + '.Date(' + csString(v) + ')';
            case 'expr': return '(' + v + ')';
        }
        return csString(value);
    };

    var listLiterals = function (value, valueType) {
        if (valueType === 'expr') return literal(value, 'expr');
        return String(value).split(',')
            .map(function (v) { return v.trim(); })
            .filter(function (v) { return v !== ''; })
            .map(function (v) { return literal(v, valueType); })
            .join(', ');
    };

    var textValue = function (c) {
        return c.valueType === 'expr' ? 'Convert.ToString(' + literal(c.value, 'expr') + ')' : csString(c.value);
    };

    var conditionCode = function (c) {
        var get = F + '.Get(r, ' + csString(c.column) + ')';
        var v = literal(c.value, c.valueType);
        switch (c.operator) {
            case 'eq': return F + '.Eq(' + get + ', ' + v + ')';
            case 'ne': return '!' + F + '.Eq(' + get + ', ' + v + ')';
            case 'gt': return F + '.Compare(' + get + ', ' + v + ') > 0';
            case 'ge': return F + '.Compare(' + get + ', ' + v + ') >= 0';
            case 'lt': return F + '.Compare(' + get + ', ' + v + ') < 0';
            case 'le': return F + '.Compare(' + get + ', ' + v + ') <= 0';
            case 'contains': return F + '.Contains(' + get + ', ' + textValue(c) + ')';
            case 'notContains': return '!' + F + '.Contains(' + get + ', ' + textValue(c) + ')';
            case 'startsWith': return F + '.StartsWith(' + get + ', ' + textValue(c) + ')';
            case 'endsWith': return F + '.EndsWith(' + get + ', ' + textValue(c) + ')';
            case 'isNull': return F + '.IsNull(' + get + ')';
            case 'isNotNull': return '!' + F + '.IsNull(' + get + ')';
            case 'in': return F + '.In(' + get + ', ' + listLiterals(c.value, c.valueType) + ')';
            case 'notIn': return '!' + F + '.In(' + get + ', ' + listLiterals(c.value, c.valueType) + ')';
        }
        return 'true';
    };

    var groupCode = function (group) {
        var parts = group.items
            .map(function (item) { return item.items ? groupCode(item) : '(' + conditionCode(item) + ')'; })
            .filter(function (p) { return p !== ''; });
        if (parts.length === 0) return '';
        var code = parts.length === 1 ? parts[0] : '(' + parts.join(group.logic === 'or' ? ' || ' : ' && ') + ')';
        return group.not ? '!' + code : code;
    };

    var columnArgs = function (columns) {
        return columns.map(csString).join(', ');
    };

    var opCode = function (op, previous) {
        switch (op.op) {
            case 'where': {
                var predicate = groupCode(op.group);
                return predicate ? '.Where(r => ' + predicate + ')' : null;
            }
            case 'orderBy': {
                var then = previous && previous.op === 'orderBy';
                var method = (then ? 'ThenBy' : 'OrderBy') + (op.descending ? 'Descending' : '');
                return '.' + method + '(r => ' + F + '.Get(r, ' + csString(op.column) + '), ' + F + '.Comparer)';
            }
            case 'skip': return '.Skip(' + op.count + ')';
            case 'take': return '.Take(' + op.count + ')';
            case 'skipLast': return '.SkipLast(' + op.count + ')';
            case 'takeLast': return '.TakeLast(' + op.count + ')';
            case 'distinct': return '.DistinctBy(r => ' + F + '.Key(r, ' + columnArgs(op.columns) + '))';
            case 'select': return '.Select(r => ' + F + '.Project(r, ' + columnArgs(op.columns) + '))';
            case 'custom': return '.' + op.expression.replace(/^\./, '').replace(/[\r\n]+/g, ' ');
        }
        return null;
    };

    var generateCode = function (block, newLine, indent) {
        var inner = indent + '    ';
        var chain = inner + '    ';
        var target = block.type === 'model' ? 'Model.Records' : block.name;
        var lines = [];

        if (block.type === 'model') {
            lines.push(inner + 'Model.Records = Model.Records');
        } else {
            var args = [csString(block.queryId)];
            if (block.filter || block.orderby) args.push(csString(block.filter));
            if (block.orderby) args.push(csString(block.orderby));
            lines.push(inner + 'var ' + block.name + ' = (await DLH.GetRecordsAsync(' + args.join(', ') + ')' +
                ' ?? Array.Empty<IDictionary<string, object>>())');
        }

        var previous = null;
        block.ops.forEach(function (op) {
            var code = opCode(op, previous);
            if (code) lines.push(chain + code);
            previous = op;
        });
        lines.push(chain + '.ToArray();');

        if (block.requireRows) {
            var message = block.errorMessage ||
                (block.type === 'model' ? 'No records found.' : 'No records found in ' + block.queryId + '.');
            lines.push(inner + 'if (' + target + '.Length == 0) { throw new Exception(' + csString(message) + '); }');
        }

        return lines.join(newLine);
    };

    var markerJson = function (block) {
        // '@' is escaped: '@*' inside the code block would start a Razor comment
        return JSON.stringify(block).replace(/@/g, '\\u0040');
    };

    var buildBlock = function (block, newLine, indent) {
        return indent + '@{' + newLine +
            indent + '    // dl-data:start ' + markerJson(block) + newLine +
            generateCode(block, newLine, indent) + newLine +
            indent + '    // dl-data:end' + newLine +
            indent + '}' + newLine;
    };

    // ---------- source parsing ----------

    var findBlocks = function (source) {
        var result = [];
        var m;
        BLOCK_PATTERN.lastIndex = 0;
        while ((m = BLOCK_PATTERN.exec(source)) !== null) {
            var block = null;
            try {
                block = readBlock(JSON.parse(m[2]));
            } catch (e) {
                block = null;
            }
            if (block) {
                result.push({ start: m.index, end: m.index + m[0].length, indent: m[1], block: block });
            }
            if (m[0].length === 0) BLOCK_PATTERN.lastIndex++;
        }
        return result;
    };

    var findExisting = function (blocks, type, name) {
        for (var i = 0; i < blocks.length; i++) {
            var b = blocks[i];
            if (b.block.type !== type) continue;
            if (type === 'model' || b.block.name === name) return b;
        }
        return null;
    };

    // new blocks: the Model.Records filter goes first, query blocks after the existing data blocks;
    // without data blocks right before the BeginReport line (or at the end of the source).
    var insertPosition = function (source, blocks, type) {
        if (type === 'model' && blocks.length > 0) {
            return blocks[0].start;   // filter Model.Records first
        }
        if (blocks.length > 0) {
            return blocks[blocks.length - 1].end;
        }
        var report = pdfDraggableSource.findReport(source);
        if (report) {
            var callStart = source.lastIndexOf('BeginReport', report.argsStart);
            if (callStart < 0) callStart = source.lastIndexOf('BeginPdfReport', report.argsStart);
            var lineStart = source.lastIndexOf('\n', callStart) + 1;
            return lineStart;
        }
        return source.length;
    };

    // ---------- actions ----------

    pdfDraggableSource.registerAction('data.list', function (source) {
        return {
            data: {
                blocks: findBlocks(source).map(function (b) { return b.block; }),
                hasReport: !!pdfDraggableSource.findReport(source)
            }
        };
    });

    pdfDraggableSource.registerAction('data.preview', function (source, args) {
        var block = readBlock(args.block);
        return { data: { code: generateCode(block, '\n', ''), message: validateBlock(block, findBlocks(source), str(args.originalName, 64)) } };
    });

    pdfDraggableSource.registerAction('data.set', function (source, args) {
        var block = readBlock(args.block);
        var blocks = findBlocks(source);
        var originalName = str(args.originalName, 64);

        var message = validateBlock(block, blocks, originalName);
        if (message) return { message: message };

        var newLine = pdfDraggableSource.newLineOf(source);
        var existing = findExisting(blocks, block.type, block.type === 'query' ? (originalName || block.name) : null);

        if (existing) {
            return { start: existing.start, end: existing.end, text: buildBlock(block, newLine, existing.indent) };
        }

        var at = insertPosition(source, blocks, block.type);
        var prefix = at > 0 && source.charAt(at - 1) !== '\n' ? newLine : '';
        return { start: at, end: at, text: prefix + buildBlock(block, newLine, '') };
    });

    pdfDraggableSource.registerAction('data.remove', function (source, args) {
        var type = args && args.type === 'query' ? 'query' : 'model';
        var existing = findExisting(findBlocks(source), type, str(args && args.name, 64));
        if (!existing) {
            return { message: type === 'model' ? 'Model.Records has no filter.' : 'Data source "' + (args && args.name) + '" not found.' };
        }
        return { start: existing.start, end: existing.end, text: '' };
    });
})();
