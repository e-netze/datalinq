/**
 * DataLinq Code - version control (git) dialogs
 *
 * dataLinqCode.ui.git.diff(title, files, options)      : changes of an item (monaco diff editor, json summary)
 * dataLinqCode.ui.git.history(id)                       : commit history (optional for one item)
 * dataLinqCode.ui.git.conflicts(options)                : resolve merge conflicts (local vs incoming)
 * dataLinqCode.ui.git.changeViewer($target, files, opt) : file list + diff view (used by the dialogs above)
 * dataLinqCode.ui.git.branches(status, actions)         : manage branches
 * dataLinqCode.ui.git.connectChoice(status, onMode)     : choose how to connect to a non empty remote
 * dataLinqCode.ui.git.mergeIntoMain(status, onConfirm)
 * dataLinqCode.ui.git.loadMonaco(callback)              : lazy loads monaco into the main window
 * dataLinqCode.ui.git.diffLines(oldText, newText)       : line diff (Myers)
 * Depends on: core/namespace.js, ui/modal.js, git/git-api.js
 */
(function ($) {
    var ui = this;
    var git = ui.git = ui.git || {};

    // captured while the script is executed (used to find the monaco library)
    var scriptSrc = (document.currentScript && document.currentScript.src) || '';

    var esc = function (text) { return $('<div>').text(text == null ? '' : text).html(); };

    // the modal plugin renders the title as html => always escape (names come from the repository)
    git.esc = esc;

    var splitLines = function (text) {
        if (text == null || text === '') {
            return [];
        }
        var lines = text.split('\n');
        if (lines[lines.length - 1] === '') {
            lines.pop();
        }
        return lines.map(function (l) { return l.replace(/\r$/, ''); });
    };

    var prefs = {
        get: function (key, defaultValue) {
            var value = localStorage.getItem('datalinq-code-git-' + key);
            return value === null ? defaultValue : value;
        },
        set: function (key, value) {
            localStorage.setItem('datalinq-code-git-' + key, value);
        }
    };

    /****** Diff (Myers) ******/

    git.diffLines = function (oldText, newText) {
        var a = splitLines(oldText), b = splitLines(newText);
        var ops = [];

        var start = 0;
        while (start < a.length && start < b.length && a[start] === b[start]) {
            ops.push({ t: ' ', a: start, b: start, text: a[start] });
            start++;
        }
        var endA = a.length, endB = b.length, suffix = [];
        while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) {
            endA--; endB--;
            suffix.unshift({ t: ' ', a: endA, b: endB, text: a[endA] });
        }

        var middle = myers(a.slice(start, endA), b.slice(start, endB), start);
        return ops.concat(middle, suffix);
    };

    var myers = function (a, b, offsetIndex) {
        var n = a.length, m = b.length, max = n + m;
        var simple = function () {
            var result = [];
            for (var i = 0; i < n; i++) result.push({ t: '-', a: offsetIndex + i, text: a[i] });
            for (var j = 0; j < m; j++) result.push({ t: '+', b: offsetIndex + j, text: b[j] });
            return result;
        };

        if (n === 0 || m === 0) {
            return simple();
        }

        var offset = max + 1, v = new Int32Array(2 * max + 3), trace = [], maxD = 2000;
        v[offset + 1] = 0;

        for (var d = 0; d <= max; d++) {
            if (d > maxD) {
                return simple();
            }
            trace.push(v.slice(offset - d - 1, offset + d + 2));

            for (var k = -d; k <= d; k += 2) {
                var x = (k === -d || (k !== d && v[offset + k - 1] < v[offset + k + 1])) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] === b[y]) { x++; y++; }
                v[offset + k] = x;

                if (x >= n && y >= m) {
                    return backtrack(trace, a, b, offsetIndex);
                }
            }
        }
        return simple();
    };

    var backtrack = function (trace, a, b, offsetIndex) {
        var x = a.length, y = b.length, ops = [];
        var get = function (vs, d, k) { return vs[k + d + 1]; };

        for (var d = trace.length - 1; d > 0; d--) {
            var vs = trace[d], k = x - y;
            var prevK = (k === -d || (k !== d && get(vs, d, k - 1) < get(vs, d, k + 1))) ? k + 1 : k - 1;
            var prevX = get(vs, d, prevK), prevY = prevX - prevK;

            while (x > prevX && y > prevY) {
                x--; y--;
                ops.push({ t: ' ', a: offsetIndex + x, b: offsetIndex + y, text: a[x] });
            }
            if (x === prevX) {
                y--;
                ops.push({ t: '+', b: offsetIndex + y, text: b[y] });
            } else {
                x--;
                ops.push({ t: '-', a: offsetIndex + x, text: a[x] });
            }
        }
        while (x > 0 && y > 0) {
            x--; y--;
            ops.push({ t: ' ', a: offsetIndex + x, b: offsetIndex + y, text: a[x] });
        }
        return ops.reverse();
    };

    // +/- line counts of a file diff (cached on the file object)
    var fileStats = function (file) {
        if (!file._stats) {
            var stats = { add: 0, del: 0 };
            if (!file.isBinary && (file.oldText || '').length + (file.newText || '').length < 400000) {
                $.each(git.diffLines(file.oldText, file.newText), function (i, op) {
                    if (op.t === '+') stats.add++;
                    else if (op.t === '-') stats.del++;
                });
            } else {
                stats = null;
            }
            file._stats = stats || { add: null, del: null };
        }
        return file._stats;
    };

    /****** Diff rendering (fallback if monaco can not be loaded) ******/

    var renderDiff = function ($target, file, fullFile) {
        $target.empty();

        var ops = git.diffLines(file.oldText, file.newText);
        var context = 3, visible = new Array(ops.length);
        for (var i = 0; i < ops.length; i++) {
            if (fullFile || ops[i].t !== ' ') {
                for (var j = Math.max(0, i - context); j <= Math.min(ops.length - 1, i + context); j++) {
                    visible[j] = true;
                }
            }
        }

        var html = ["<table class='git-diff-table'><tbody>"], skipped = 0;
        var flushSkipped = function () {
            if (skipped > 0) {
                html.push("<tr class='git-diff-skip'><td></td><td></td><td>\u22EF " + skipped + " unchanged line" + (skipped > 1 ? 's' : '') + "</td></tr>");
                skipped = 0;
            }
        };

        for (var k = 0; k < ops.length; k++) {
            var op = ops[k];
            if (!visible[k]) {
                skipped++;
                continue;
            }
            flushSkipped();
            var cls = op.t === '+' ? 'add' : op.t === '-' ? 'del' : 'ctx';
            html.push("<tr class='git-diff-" + cls + "'><td class='ln'>" + (op.a != null ? op.a + 1 : '') + "</td><td class='ln'>" + (op.b != null ? op.b + 1 : '') +
                "</td><td class='code'><span class='sign'>" + (op.t === ' ' ? '&nbsp;' : op.t) + "</span>" + esc(op.text) + "</td></tr>");
        }
        flushSkipped();
        html.push("</tbody></table>");

        $("<div class='git-diff-fallback'>").html(html.join('')).appendTo($target);
    };

    /****** Monaco (lazy loaded into the main window, the document editors live in iframes) ******/

    var monacoState = null, monacoWaiting = [], diffEditors = [];

    var monacoBase = function () {
        var src = scriptSrc;
        if (!/\/js\/datalinq-code\//.test(src)) {
            var script = $("script[src*='/js/datalinq-code/']").get(0);
            src = script ? script.src : '';
        }
        return src.replace(/\/js\/datalinq-code\/.*$/, '/lib/monaco-editor/min/vs');
    };

    var monacoTheme = function () {
        return $('.datalinq-code-ide').hasClass('colorscheme-light') ? 'vs' : 'vs-dark';
    };

    git.loadMonaco = function (callback) {
        if (window.monaco && window.monaco.editor && monacoState !== 'loading') {
            callback(window.monaco);
            return;
        }
        if (monacoState === 'failed') {
            callback(null);
            return;
        }

        monacoWaiting.push(callback);
        if (monacoState === 'loading') {
            return;
        }
        monacoState = 'loading';

        var base = monacoBase();
        var finish = function (monaco) {
            if (monacoState !== 'loading') {
                return;
            }
            monacoState = monaco ? 'loaded' : 'failed';
            var waiting = monacoWaiting;
            monacoWaiting = [];
            $.each(waiting, function (i, cb) {
                try { cb(monaco); } catch (e) { console.error(e); }
            });
        };
        var start = function () {
            try {
                window.require.config({ paths: { vs: base } });
                window.require(['vs/editor/editor.main'],
                    function () { finish(window.monaco || null); },
                    function (err) { console.warn('monaco could not be loaded', err); finish(null); });
            } catch (e) {
                console.warn('monaco could not be loaded', e);
                finish(null);
            }
        };

        setTimeout(function () { finish(null); }, 30000);

        if (typeof window.require === 'function' && typeof window.require.config === 'function') {
            start();
            return;
        }

        $("<link rel='stylesheet'>").attr('href', base + '/editor/editor.main.css').appendTo('head');
        var script = document.createElement('script');
        script.src = base + '/loader.js';
        script.onload = start;
        script.onerror = function () { finish(null); };
        document.head.appendChild(script);
    };

    // disposes monaco editors of closed dialogs (or all)
    var releaseEditors = function (all) {
        diffEditors = diffEditors.filter(function (entry) {
            if (all === true || !document.body.contains(entry.element)) {
                entry.dispose();
                return false;
            }
            return true;
        });
    };
    git.releaseEditors = releaseEditors;

    /****** Files & items ******/

    var fileName = function (path) { return (path || '').split('/').pop(); };
    var fileDir = function (path) { var p = (path || '').split('/'); p.pop(); return p.join('/'); };
    var extension = function (path) {
        var name = fileName(path), i = name.lastIndexOf('.');
        return i > 0 ? name.substring(i + 1).toLowerCase() : '';
    };

    var fileTypes = {
        json: { language: 'json', badge: '{ }', cls: 'json', rank: 0 },
        sql: { language: 'sql', badge: 'SQL', cls: 'sql', rank: 1 },
        razor: { language: 'razor', badge: '@', cls: 'razor', rank: 1 },
        cshtml: { language: 'razor', badge: '@', cls: 'razor', rank: 1 },
        html: { language: 'html', badge: '<>', cls: 'html', rank: 1 },
        css: { language: 'css', badge: '#', cls: 'css', rank: 2 },
        js: { language: 'javascript', badge: 'JS', cls: 'js', rank: 3 },
        md: { language: 'markdown', badge: 'MD', cls: 'md', rank: 4 },
        xml: { language: 'xml', badge: '<>', cls: 'html', rank: 4 }
    };

    git.fileType = function (path) {
        return fileTypes[extension(path)] || { language: 'plaintext', badge: 'TXT', cls: 'text', rank: 5 };
    };

    var fileTitle = function (file) {
        var name = fileName(file.path);
        switch (file.kind === 'file' ? '' : name) {
            case 'endpoint.json': return 'Endpoint settings';
            case 'endpoint.css': return 'Endpoint stylesheet';
            case 'endpoint.js': return 'Endpoint JavaScript';
            case 'query.json': return 'Query settings';
            case 'query.sql': return 'SQL statement';
            case 'view.json': return 'View settings';
            case 'view.razor': return 'View template';
            case 'view.css': return 'View stylesheet';
            case 'view.js': return 'View JavaScript';
            case '_folders.json': return 'Tree folders';
        }
        if (file.kind === 'errorpage') {
            return 'Error page';
        }
        return name;
    };

    // readable item id: endpoint › query › view
    git.prettyId = function (id) {
        id = id || '';
        if (id === '_folders') return 'Tree folders';
        if (id.indexOf('_errorpage@') === 0) return 'Error page \u203A ' + id.substring('_errorpage@'.length);
        if (id.indexOf('_file@') === 0) return id.substring('_file@'.length);
        return id.replace(/@_css$/, '@CSS').replace(/@_js$/, '@JavaScript').split('@').join(' \u203A ');
    };

    // files are grouped by their DataLinq item (css/js belong to their endpoint/view)
    var groupOf = function (file) {
        var kind = file.kind || 'file', id = file.id || ('_file@' + file.path);
        switch (kind) {
            case 'endpoint-css':
            case 'endpoint-js':
                return { key: id.replace(/@_(css|js)$/, ''), kind: 'endpoint' };
            case 'view-css':
            case 'view-js':
                return { key: id.replace(/@_(css|js)$/, ''), kind: 'view' };
            case 'endpoint':
            case 'query':
            case 'view':
            case 'errorpage':
            case 'folders':
                return { key: id, kind: kind };
            default:
                return { key: '_files', kind: 'files' };
        }
    };

    var groupFiles = function (files, priorityId) {
        var groups = {}, order = [];
        $.each(files, function (i, file) {
            var g = groupOf(file);
            if (!groups[g.key]) {
                groups[g.key] = {
                    key: g.key,
                    kind: g.kind,
                    label: g.kind === 'files' ? 'Repository files' : git.prettyId(g.key),
                    files: []
                };
                order.push(g.key);
            }
            groups[g.key].files.push(file);
        });

        var isPriority = function (key) {
            return !!priorityId && (key === priorityId || priorityId.indexOf(key + '@') === 0 || key.indexOf(priorityId + '@') === 0);
        };

        return order.map(function (key) { return groups[key]; }).sort(function (a, b) {
            var pa = isPriority(a.key) ? 0 : 1, pb = isPriority(b.key) ? 0 : 1;
            if (pa !== pb) return pa - pb;
            var fa = a.kind === 'files' ? 1 : 0, fb = b.kind === 'files' ? 1 : 0;
            if (fa !== fb) return fa - fb;
            return a.label.localeCompare(b.label);
        }).map(function (group) {
            group.files.sort(function (x, y) {
                var r = git.fileType(x.path).rank - git.fileType(y.path).rank;
                return r !== 0 ? r : x.path.localeCompare(y.path);
            });
            return group;
        });
    };

    var statusText = { added: 'Added', modified: 'Modified', deleted: 'Deleted', conflicted: 'Conflict' };
    var statusLetter = function (status) {
        return { added: 'A', modified: 'M', deleted: 'D', conflicted: '!' }[status] || '?';
    };

    var badge = function (path) {
        var type = git.fileType(path);
        return $("<span class='git-file-badge'>").addClass(type.cls).text(type.badge);
    };

    var statsElement = function (file) {
        var stats = fileStats(file), $stats = $("<span class='git-stats'>");
        if (stats.add === null) {
            return $stats;
        }
        if (stats.add) $("<span class='add'>").text('+' + stats.add).appendTo($stats);
        if (stats.del) $("<span class='del'>").text('\u2212' + stats.del).appendTo($stats);
        return $stats;
    };

    var kindIcon = function (kind) {
        return $("<span class='git-kind-icon'>").addClass('kind-' + kind);
    };

    /****** JSON summary ******/

    var parseJson = function (text) {
        if (text == null || text.trim() === '') {
            return { ok: true, value: undefined };
        }
        try {
            return { ok: true, value: JSON.parse(text) };
        } catch (e) {
            return { ok: false };
        }
    };

    var canSummarize = function (file) {
        return extension(file.path) === 'json' && !file.isBinary && parseJson(file.oldText).ok && parseJson(file.newText).ok;
    };

    var isScalar = function (v) { return v === null || typeof v !== 'object'; };

    var jsonEntries = function (value) {
        var entries = {}, order = [];
        var add = function (path, entry) {
            var key = JSON.stringify(path);
            if (!(key in entries)) order.push(key);
            entry.path = path;
            entries[key] = entry;
        };
        var walk = function (v, path) {
            if (v === undefined) {
                return;
            }
            if (Array.isArray(v)) {
                if (v.length === 0 || v.every(isScalar)) {
                    add(path, { list: v });
                    return;
                }
                var used = {};
                $.each(v, function (i, item) {
                    var key = '[' + i + ']';
                    if (item && typeof item === 'object' && !Array.isArray(item)) {
                        $.each(['name', 'id', 'key'], function (j, n) {
                            if ((typeof item[n] === 'string' || typeof item[n] === 'number') && !used['[' + item[n] + ']']) {
                                key = '[' + item[n] + ']';
                                return false;
                            }
                        });
                    }
                    used[key] = true;
                    walk(item, path.concat([key]));
                });
                return;
            }
            if (v !== null && typeof v === 'object') {
                var keys = Object.keys(v);
                if (keys.length === 0) {
                    add(path, { value: {} });
                    return;
                }
                $.each(keys, function (i, k) { walk(v[k], path.concat([k])); });
                return;
            }
            add(path, { value: v });
        };
        walk(value, []);
        return { entries: entries, order: order };
    };

    // keeps the property order of the new version, removed properties stay at their old position
    var mergeOrder = function (oldOrder, newOrder) {
        var result = newOrder.slice(), inNew = {}, last = -1;
        $.each(newOrder, function (i, key) { inNew[key] = true; });
        $.each(oldOrder, function (i, key) {
            if (inNew[key]) {
                last = result.indexOf(key);
            } else {
                result.splice(++last, 0, key);
            }
        });
        return result;
    };

    var compareJson = function (oldValue, newValue) {
        var a = jsonEntries(oldValue), b = jsonEntries(newValue);
        return mergeOrder(a.order, b.order).map(function (key) {
            var x = a.entries[key], y = b.entries[key];
            return {
                path: (x || y).path,
                old: x,
                'new': y,
                status: !x ? 'added' : !y ? 'removed' : JSON.stringify(x) === JSON.stringify(y) ? 'same' : 'changed'
            };
        });
    };

    var acronyms = { id: 'ID', url: 'URL', sql: 'SQL', css: 'CSS', js: 'JS', json: 'JSON', html: 'HTML', api: 'API', pdf: 'PDF', ui: 'UI', ip: 'IP' };
    var humanize = function (key) {
        if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(key)) {
            return key;
        }
        var words = key.replace(/_/g, ' ')
            .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
            .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
            .split(' ').filter(Boolean);
        return words.map(function (w, i) {
            var l = w.toLowerCase();
            if (acronyms[l]) return acronyms[l];
            return i === 0 ? l.charAt(0).toUpperCase() + l.substring(1) : l;
        }).join(' ');
    };

    var pathHtml = function (path, raw) {
        if (path.length === 0) {
            return "<b>(value)</b>";
        }
        return path.map(function (seg, i) {
            var text = seg.charAt(0) === '[' ? "<span class='idx'>" + esc(seg.substring(1, seg.length - 1)) + "</span>" : esc(raw ? seg : humanize(seg));
            return i === path.length - 1 ? "<b>" + text + "</b>" : "<span class='parent'>" + text + "</span>";
        }).join("<span class='sep'>\u203A</span>");
    };

    var valueHtml = function (entry, other, side) {
        if (!entry) {
            return "<span class='git-jv none'>\u2014</span>";
        }
        if (entry.list) {
            if (entry.list.length === 0) {
                return "<span class='git-jv empty'>empty list</span>";
            }
            var otherList = other && other.list ? other.list.map(String) : null;
            return "<span class='git-jv-list'>" + entry.list.map(function (item) {
                var cls = otherList && otherList.indexOf(String(item)) < 0 ? (side === 'old' ? ' removed' : ' added') : '';
                return "<span class='git-jv-chip" + cls + "'>" + esc(item === null ? 'null' : String(item)) + "</span>";
            }).join('') + "</span>";
        }

        var v = entry.value;
        if (v === null) return "<span class='git-jv null'>null</span>";
        if (typeof v === 'boolean') return "<span class='git-jv bool " + v + "'>" + (v ? 'true' : 'false') + "</span>";
        if (typeof v === 'number') return "<span class='git-jv num'>" + v + "</span>";
        if (typeof v === 'object') return "<span class='git-jv empty'>{ }</span>";
        if (v === '') return "<span class='git-jv empty'>empty</span>";
        return "<span class='git-jv str'>" + esc(v) + "</span>";
    };

    var sideHeadHtml = function (side) {
        return "<span class='git-side-dot'></span><span class='t'>" + esc(side.title) + "</span>" + (side.sub ? "<span class='s'>" + esc(side.sub) + "</span>" : '');
    };

    var renderJsonSummary = function ($target, file, sides, showUnchanged, onToggleUnchanged) {
        var rows = compareJson(parseJson(file.oldText).value, parseJson(file.newText).value);
        var raw = file.kind === 'folders';
        var counts = { changed: 0, added: 0, removed: 0, same: 0 };
        $.each(rows, function (i, r) { counts[r.status]++; });

        var $wrap = $("<div class='git-json'>").appendTo($target);
        var $head = $("<div class='git-json-head'>").appendTo($wrap);
        $.each([['changed', 'changed'], ['added', 'added'], ['removed', 'removed']], function (i, c) {
            if (counts[c[0]]) {
                $("<span class='git-json-count'>").addClass(c[0]).text(counts[c[0]] + ' ' + c[1]).appendTo($head);
            }
        });
        if (counts.changed + counts.added + counts.removed === 0) {
            $("<span class='git-json-count same'>").text('No property changes (formatting only)').appendTo($head);
        }
        if (counts.same) {
            $("<button type='button' class='git-link-button'>")
                .text(showUnchanged ? 'Hide ' + counts.same + ' unchanged' : 'Show ' + counts.same + ' unchanged')
                .appendTo($head)
                .click(function () { onToggleUnchanged(!showUnchanged); });
        }

        var html = ["<table class='git-json-table'><thead><tr><th class='prop'>Property</th>",
            "<th class='old'>" + sideHeadHtml(sides.old) + "</th>",
            "<th class='new'>" + sideHeadHtml(sides['new']) + "</th></tr></thead><tbody>"];

        $.each(rows, function (i, r) {
            if (r.status === 'same' && !showUnchanged) {
                return;
            }
            html.push("<tr class='row-" + r.status + "'><td class='prop' title='" + esc(r.path.join('.')) + "'>" +
                "<span class='git-json-marker'></span>" + pathHtml(r.path, raw) + "</td>" +
                "<td class='old'>" + valueHtml(r.old, r['new'], 'old') + "</td>" +
                "<td class='new'>" + valueHtml(r['new'], r.old, 'new') + "</td></tr>");
        });
        html.push("</tbody></table>");

        $(html.join('')).appendTo($wrap);
    };

    /****** Change viewer ******/

    /**
     * Renders changed files: file list (or tabs) + diff (monaco diff editor or json summary)
     * @param {jQuery} $target
     * @param {Array} files GitFileDiff[] ({ path, id, kind, status, oldText, newText, oldLabel, newLabel, isBinary })
     * @param {Object} options {
     *      layout: 'list' | 'tabs' (default: tabs for one item with a few files),
     *      sides: { old: { title, sub }, new: { title, sub } } or function(file) => sides,
     *      conflict: true => local/incoming colors instead of removed/added,
     *      priorityId: item shown first,
     *      emptyText
     * }
     */
    git.changeViewer = function ($target, files, options) {
        options = options || {};
        files = files || [];

        var groups = groupFiles(files, options.priorityId);
        var layout = options.layout || (groups.length <= 1 && files.length <= 4 ? 'tabs' : 'list');

        var $root = $("<div class='git-cv'>")
            .addClass('layout-' + layout)
            .toggleClass('conflict', !!options.conflict)
            .appendTo($target);

        if (files.length === 0) {
            $("<div class='git-empty-state'>")
                .append("<div class='icon'>\u2713</div>")
                .append($("<div class='text'>").text(options.emptyText || 'No changes'))
                .appendTo($root.addClass('empty'));
            return { dispose: function () { } };
        }

        var current = null, token = 0, editorEntry = null;
        var $rows = $();

        /* file list */

        var $files, $tabs;
        if (layout === 'list') {
            $files = $("<div class='git-cv-files'>").appendTo($root);
            var $head = $("<div class='git-cv-files-head'>").appendTo($files);
            $("<span class='title'>").text('Changed files').appendTo($head);
            $("<span class='git-count-pill'>").text(files.length).appendTo($head);

            var total = { add: 0, del: 0 };
            $.each(files, function (i, f) { var s = fileStats(f); total.add += s.add || 0; total.del += s.del || 0; });
            statsElement({ _stats: total }).appendTo($head);

            var $list = $("<div class='git-cv-files-list'>").appendTo($files);
            $.each(groups, function (i, group) {
                var $group = $("<div class='git-cv-group'>").appendTo($list);
                $("<div class='git-cv-group-head'>")
                    .attr('title', group.label)
                    .append(kindIcon(group.kind))
                    .append($("<span class='label'>").text(group.label))
                    .appendTo($group);

                $.each(group.files, function (j, file) {
                    var $row = $("<div class='git-cv-file'>")
                        .addClass('status-' + file.status)
                        .attr('title', file.path + ' (' + (statusText[file.status] || file.status) + ')')
                        .append(badge(file.path))
                        .append($("<span class='text'>")
                            .append($("<span class='name'>").text(fileTitle(file)))
                            .append($("<span class='sub'>").text(group.kind === 'files' ? fileDir(file.path) || '/' : fileName(file.path))))
                        .append(statsElement(file))
                        .append($("<span class='git-status-pill'>").addClass(file.status).text(statusLetter(file.status)))
                        .appendTo($group)
                        .click(function () { select(file, $row); });
                    $row.data('file', file);
                    $rows = $rows.add($row);
                });
            });
        }

        /* main */

        var $main = $("<div class='git-cv-main'>").appendTo($root);
        var $bar = $("<div class='git-cv-toolbar'>").appendTo($main);
        var $title = $("<div class='git-cv-title'>").appendTo($bar);
        var $tools = $("<div class='git-cv-tools'>").appendTo($bar);

        if (layout === 'tabs') {
            $tabs = $("<div class='git-cv-tabs'>").appendTo($title);
            $.each(groups, function (i, group) {
                $.each(group.files, function (j, file) {
                    var $tab = $("<div class='git-cv-tab'>")
                        .addClass('status-' + file.status)
                        .attr('title', file.path + ' (' + (statusText[file.status] || file.status) + ')')
                        .append(badge(file.path))
                        .append($("<span class='name'>").text(fileTitle(file)))
                        .append(statsElement(file))
                        .appendTo($tabs)
                        .click(function () { select(file, $tab); });
                    $tab.data('file', file);
                    $rows = $rows.add($tab);
                });
            });
        } else {
            var $fileTitle = $("<div class='git-cv-file-title'>").appendTo($title);
        }

        var $view = $("<div class='git-segmented'>").appendTo($tools);
        var addViewButton = function (view, text, title) {
            return $("<button type='button'>").attr('data-view', view).attr('title', title).text(text).appendTo($view).click(function () {
                if (!current) return;
                if (view === 'summary') {
                    prefs.set('json-view', 'summary');
                } else {
                    prefs.set('diff-mode', view);
                    if (canSummarize(current)) prefs.set('json-view', 'diff');
                }
                show(current);
            });
        };
        var $summaryButton = addViewButton('summary', 'Summary', 'Property overview of the json settings');
        addViewButton('split', 'Side by side', 'Show both versions side by side');
        addViewButton('inline', 'Inline', 'Show the changes inline');

        var $collapse = $("<button type='button' class='git-tool-button toggle'>")
            .attr('title', 'Collapse unchanged regions')
            .text('Collapse unchanged')
            .appendTo($tools)
            .click(function () {
                prefs.set('hide-unchanged', prefs.get('hide-unchanged', '1') === '1' ? '0' : '1');
                if (current) show(current);
            });

        var $nav = $("<span class='git-nav'>").appendTo($tools);
        var navigate = function (target) {
            var editor = editorEntry && editorEntry.editor;
            if (editor && typeof editor.goToDiff === 'function') {
                editor.goToDiff(target);
            }
        };
        $("<button type='button' class='git-tool-button icon'>").attr('title', 'Previous change').html('&#x2191;').appendTo($nav).click(function () { navigate('previous'); });
        $("<button type='button' class='git-tool-button icon'>").attr('title', 'Next change').html('&#x2193;').appendTo($nav).click(function () { navigate('next'); });

        var $sides = $("<div class='git-cv-sides'>").appendTo($main);
        var $body = $("<div class='git-cv-body'>").appendTo($main);

        var sidesFor = function (file) {
            var s = typeof options.sides === 'function' ? options.sides(file) : options.sides;
            s = s || { old: { title: file.oldLabel || 'Before' }, 'new': { title: file.newLabel || 'After' } };
            return {
                old: $.extend({}, s.old, { missing: file.oldText == null && !file.isBinary }),
                'new': $.extend({}, s['new'], { missing: file.newText == null && !file.isBinary })
            };
        };

        var viewFor = function (file) {
            if (canSummarize(file) && prefs.get('json-view', 'summary') === 'summary') {
                return 'summary';
            }
            return prefs.get('diff-mode', 'split') === 'inline' ? 'inline' : 'split';
        };

        var releaseEditor = function () {
            if (editorEntry) {
                editorEntry.dispose();
                diffEditors = diffEditors.filter(function (e) { return e !== editorEntry; });
                editorEntry = null;
            }
        };

        var renderSides = function (sides, view) {
            $sides.empty().attr('data-view', view);
            $.each(['old', 'new'], function (i, key) {
                var side = sides[key];
                $("<div class='git-cv-side'>")
                    .addClass(key)
                    .html(sideHeadHtml(side) + (side.missing ? "<span class='missing'>" + (options.conflict ? 'deleted' : 'does not exist') + "</span>" : ''))
                    .appendTo($sides);
            });
        };

        var show = function (file) {
            current = file;
            token++;
            releaseEditor();
            $body.empty().removeClass('loading');

            if ($fileTitle) {
                $fileTitle.empty()
                    .append(badge(file.path))
                    .append($("<span class='name'>").text(fileTitle(file)))
                    .append($("<span class='path'>").text(file.path));
            }

            var view = viewFor(file);
            var hideUnchanged = prefs.get('hide-unchanged', '1') === '1';
            var sides = sidesFor(file);

            $summaryButton.toggle(canSummarize(file));
            $view.children().each(function () { $(this).toggleClass('active', $(this).attr('data-view') === view); });
            $collapse.toggleClass('active', hideUnchanged).toggle(view !== 'summary');
            $nav.toggle(view !== 'summary');
            renderSides(sides, view);
            $sides.toggle(view !== 'summary');

            if (file.isBinary) {
                $("<div class='git-diff-info'>").text('Binary file - no preview').appendTo($body);
                return;
            }
            if ((file.oldText || '') === (file.newText || '') && file.oldText != null && file.newText != null) {
                $("<div class='git-diff-info'>").text('No differences').appendTo($body);
                return;
            }

            if (view === 'summary') {
                renderJsonSummary($body, file, sides, prefs.get('json-unchanged', '0') === '1', function (showUnchanged) {
                    prefs.set('json-unchanged', showUnchanged ? '1' : '0');
                    show(file);
                });
                return;
            }

            var myToken = token;
            $body.addClass('loading').append("<div class='git-loading'><span class='git-spinner'></span>Loading editor...</div>");

            git.loadMonaco(function (monaco) {
                if (myToken !== token || !document.body.contains($body[0])) {
                    return;
                }
                $body.empty().removeClass('loading');

                if (!monaco) {
                    renderDiff($body, file, !hideUnchanged);
                    return;
                }

                var $host = $("<div class='git-monaco-host'>").appendTo($body);
                var language = git.fileType(file.path).language;
                monaco.editor.setTheme(monacoTheme());

                var original = monaco.editor.createModel(file.oldText || '', language);
                var modified = monaco.editor.createModel(file.newText || '', language);
                var editor = monaco.editor.createDiffEditor($host[0], {
                    readOnly: true,
                    originalEditable: false,
                    automaticLayout: true,
                    renderSideBySide: view === 'split',
                    useInlineViewWhenSpaceIsLimited: false,
                    enableSplitViewResizing: true,
                    hideUnchangedRegions: { enabled: hideUnchanged, contextLineCount: 3, minimumLineCount: 3, revealLineCount: 20 },
                    ignoreTrimWhitespace: false,
                    renderIndicators: !options.conflict,
                    renderMarginRevertIcon: false,
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    stickyScroll: { enabled: false },
                    fontSize: 13,
                    lineHeight: 20,
                    padding: { top: 6 },
                    scrollbar: { alwaysConsumeMouseWheel: false }
                });
                editor.setModel({ original: original, modified: modified });

                editorEntry = {
                    element: $host[0],
                    editor: editor,
                    dispose: function () {
                        editor.dispose();
                        original.dispose();
                        modified.dispose();
                    }
                };
                diffEditors.push(editorEntry);
            });
        };

        var select = function (file, $row) {
            $rows.removeClass('selected');
            $row.addClass('selected');
            show(file);
        };

        $rows.first().trigger('click');

        return {
            dispose: releaseEditor
        };
    };

    /****** Dialog helpers ******/

    var openDialog = function (settings) {
        // the modal plugin replaces an open dialog with the same id => remove it first to release its editors
        $('body').dataLinq_code_modal('close', { id: settings.id });
        releaseEditors();
        $('body').dataLinq_code_modal($.extend({}, settings, {
            onload: function ($content) {
                $content.addClass('datalinq-code-git-dialog git-dialog-surface');
                settings.onload($content);
            },
            onclose: function () {
                if (settings.onclose) settings.onclose();
                setTimeout(releaseEditors, 0);
            }
        }));
        return function () {
            $('body').dataLinq_code_modal('close', { id: settings.id });
            releaseEditors();
        };
    };

    var buttonBar = function ($parent, buttons, close) {
        if (!buttons || !buttons.length) {
            return null;
        }
        var $bar = $("<div class='git-dialog-footer'>").appendTo($parent);
        $.each(buttons, function (i, button) {
            $("<button type='button' class='datalinq-code-button'>")
                .addClass(button.cls || '')
                .text(button.text)
                .attr('title', button.title || '')
                .appendTo($bar)
                .click(function () {
                    close();
                    button.action();
                });
        });
        return $bar;
    };

    /****** Changes of an item ******/

    /**
     * Shows file diffs in a dialog
     * @param {string} title
     * @param {Array} files GitFileDiff[]
     * @param {Object} options { sides, conflict, subtitle, buttons: [{ text, cls, title, action }] }
     */
    git.diff = function (title, files, options) {
        options = options || {};
        var dialogId = 'datalinq-code-git-diff';

        var close = openDialog({
            title: esc(title),
            width: '90%',
            height: '85%',
            id: dialogId,
            onload: function ($content) {
                var $root = $("<div class='git-diff-dialog'>").appendTo($content);
                git.changeViewer($root, files, {
                    sides: options.sides,
                    conflict: options.conflict,
                    emptyText: 'No changes'
                });
                buttonBar($root, options.buttons, function () { close(); });
            }
        });
    };

    /****** History ******/

    var formatDate = function (value) {
        var d = new Date(value);
        return isNaN(d) ? value : d.toLocaleString();
    };

    var relativeTime = function (value) {
        var d = new Date(value);
        if (isNaN(d)) {
            return '';
        }
        var seconds = (d.getTime() - Date.now()) / 1000;
        if (Math.abs(seconds) < 60) {
            return 'just now';
        }
        var units = [['year', 31536000], ['month', 2592000], ['week', 604800], ['day', 86400], ['hour', 3600], ['minute', 60]];
        for (var i = 0; i < units.length; i++) {
            if (Math.abs(seconds) >= units[i][1]) {
                var amount = Math.round(seconds / units[i][1]);
                if (window.Intl && Intl.RelativeTimeFormat) {
                    return new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(amount, units[i][0]);
                }
                return Math.abs(amount) + ' ' + units[i][0] + (Math.abs(amount) === 1 ? '' : 's') + ' ago';
            }
        }
        return '';
    };

    var dayLabel = function (value) {
        var d = new Date(value);
        if (isNaN(d)) {
            return '';
        }
        var day = new Date(d.getFullYear(), d.getMonth(), d.getDate());
        var today = new Date(); today = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        var diff = Math.round((today - day) / 86400000);
        if (diff === 0) return 'Today';
        if (diff === 1) return 'Yesterday';
        return d.toLocaleDateString(undefined, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' });
    };

    var avatar = function (name) {
        name = (name || '?').trim();
        var parts = name.split(/[\s._-]+/).filter(Boolean);
        var initials = ((parts[0] || '?').charAt(0) + (parts.length > 1 ? parts[parts.length - 1].charAt(0) : '')).toUpperCase();
        var hash = 0;
        for (var i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0;
        return $("<span class='git-avatar'>").text(initials).attr('title', name).css('background-color', 'hsl(' + (Math.abs(hash) % 360) + ', 45%, 42%)');
    };

    var copyButton = function (text, label) {
        return $("<button type='button' class='git-sha'>").text(label || text).attr('title', 'Copy ' + text).click(function (e) {
            e.stopPropagation();
            var $button = $(this);
            if (navigator.clipboard) {
                navigator.clipboard.writeText(text).then(function () {
                    $button.addClass('copied');
                    setTimeout(function () { $button.removeClass('copied'); }, 1200);
                });
            }
        });
    };

    git.history = function (id) {
        var dialogId = 'datalinq-code-git-history';

        openDialog({
            title: esc('History' + (id ? ' \u00B7 ' + git.prettyId(id) : '')),
            width: '92%',
            height: '88%',
            id: dialogId,
            onload: function ($content) {
                var $root = $("<div class='git-history'>").appendTo($content);
                var $side = $("<div class='git-history-side'>").appendTo($root);
                var $searchBox = $("<div class='git-history-search'>").appendTo($side);
                var $search = $("<input type='search' class='git-input' placeholder='Filter by message, author or SHA' />").appendTo($searchBox);
                var $list = $("<div class='git-history-list'>").appendTo($side);
                var $main = $("<div class='git-history-main'>").appendTo($root);

                $list.append("<div class='git-loading'><span class='git-spinner'></span>Loading history...</div>");

                dataLinqCode.api.git('history', { id: id || null, count: 200 }, function (result) {
                    $list.empty();
                    if (!result.success) {
                        $("<div class='git-diff-info error'>").text(result.message).appendTo($list);
                        return;
                    }
                    var commits = result.data || [];
                    if (commits.length === 0) {
                        $("<div class='git-empty-state'>").append("<div class='icon'>\u2205</div>").append($("<div class='text'>").text('No commits yet')).appendTo($main);
                        $searchBox.hide();
                        return;
                    }

                    var lastDay = null;
                    $.each(commits, function (i, commit) {
                        var day = dayLabel(commit.date);
                        if (day !== lastDay) {
                            $("<div class='git-history-day'>").text(day).appendTo($list);
                            lastDay = day;
                        }

                        var $item = $("<div class='git-commit-item'>")
                            .toggleClass('unpushed', !commit.isPushed)
                            .attr('data-search', (commit.message + ' ' + commit.author + ' ' + commit.email + ' ' + commit.sha).toLowerCase())
                            .appendTo($list)
                            .click(function () {
                                $list.find('.git-commit-item').removeClass('selected');
                                $item.addClass('selected');
                                showCommit(commit, $main, id);
                            });

                        avatar(commit.author).appendTo($item);
                        var $text = $("<div class='text'>").appendTo($item);
                        $("<div class='msg'>").text(commit.messageShort).attr('title', commit.message).appendTo($text);
                        var $meta = $("<div class='meta'>").appendTo($text);
                        $("<span>").text(commit.author).appendTo($meta);
                        $("<span class='dot'>").text('\u00B7').appendTo($meta);
                        $("<span>").text(relativeTime(commit.date)).attr('title', formatDate(commit.date)).appendTo($meta);

                        var $right = $("<div class='right'>").appendTo($item);
                        $("<span class='git-sha static'>").text(commit.shortSha).appendTo($right);
                        if (!commit.isPushed) {
                            $("<span class='git-pill accent'>").text('not pushed').appendTo($right);
                        }
                        if (commit.parents > 1) {
                            $("<span class='git-pill'>").text('merge').appendTo($right);
                        }

                        if (i === 0) $item.trigger('click');
                    });

                    $search.on('input', function () {
                        var term = ($search.val() || '').toLowerCase().trim();
                        $list.children().each(function () {
                            var $c = $(this);
                            if ($c.hasClass('git-commit-item')) {
                                $c.toggle(!term || $c.attr('data-search').indexOf(term) >= 0);
                            }
                        });
                        $list.children('.git-history-day').each(function () {
                            var $day = $(this);
                            $day.toggle($day.nextUntil('.git-history-day').filter(':visible').length > 0);
                        });
                    });
                });
            }
        });
    };

    var showCommit = function (commit, $main, id) {
        releaseEditors();
        $main.empty();

        var lines = (commit.message || '').replace(/\s+$/, '').split('\n');
        var subject = lines.shift();
        var body = lines.join('\n').trim();

        var $card = $("<div class='git-commit-card'>").appendTo($main);
        avatar(commit.author).addClass('large').appendTo($card);
        var $info = $("<div class='info'>").appendTo($card);
        $("<div class='subject'>").text(subject).appendTo($info);
        if (body) {
            $("<div class='body'>").text(body).appendTo($info);
        }
        var $meta = $("<div class='meta'>").appendTo($info);
        $("<span class='author'>").text(commit.author).attr('title', commit.email).appendTo($meta);
        $("<span class='dot'>").text('\u00B7').appendTo($meta);
        $("<span>").text(formatDate(commit.date) + ' (' + relativeTime(commit.date) + ')').appendTo($meta);
        $("<span class='dot'>").text('\u00B7').appendTo($meta);
        copyButton(commit.sha, commit.shortSha).appendTo($meta);
        $("<span class='git-pill'>").toggleClass('accent', !commit.isPushed).text(commit.isPushed ? 'pushed' : 'not pushed').appendTo($meta);
        if (commit.parents > 1) {
            $("<span class='git-pill'>").text('merge commit').appendTo($meta);
        }
        var $summary = $("<div class='summary'>").appendTo($card);

        var $changes = $("<div class='git-history-changes'>").appendTo($main);
        $changes.append("<div class='git-loading'><span class='git-spinner'></span>Loading changes...</div>");

        dataLinqCode.api.git('commit-details', { sha: commit.sha }, function (result) {
            if (!document.body.contains($changes[0])) {
                return;
            }
            $changes.empty();
            if (!result.success) {
                $("<div class='git-diff-info error'>").text(result.message).appendTo($changes);
                return;
            }

            var files = result.data || [];
            var total = { add: 0, del: 0 };
            $.each(files, function (i, f) { var s = fileStats(f); total.add += s.add || 0; total.del += s.del || 0; });
            $("<span class='files'>").text(files.length + ' file' + (files.length === 1 ? '' : 's')).appendTo($summary);
            statsElement({ _stats: total }).appendTo($summary);

            git.changeViewer($changes, files, {
                layout: 'list',
                priorityId: id,
                emptyText: 'This commit has no file changes',
                sides: function (file) {
                    return {
                        old: { title: 'Before', sub: file.oldLabel && file.oldLabel !== '(none)' ? file.oldLabel : 'no parent' },
                        'new': { title: 'After', sub: file.newLabel }
                    };
                }
            });
        });
    };

    /****** Merge conflicts ******/

    var mergeTexts = function (options) {
        var local = options.local || 'local', incoming = options.incoming || 'incoming';
        if (options.kind === 'merge-into-main') {
            return {
                local: "The version in '" + local + "' (the branch you merge into)",
                incoming: "The version from '" + incoming + "' (the branch you merge)",
                keepLocal: "Use the version from '" + local + "'",
                takeIncoming: "Use the version from '" + incoming + "'"
            };
        }
        return {
            local: "Your version in '" + local + "' (this DataLinq instance)",
            incoming: "The version from the remote repository ('" + incoming + "')",
            keepLocal: 'Keep your version, discard the remote change',
            takeIncoming: 'Use the remote version, discard your change'
        };
    };

    // the flow "incoming -> local" with colored branch chips
    git.mergeFlow = function (options) {
        var $flow = $("<div class='git-merge-flow'>");
        $("<span class='git-ref incoming'>").text(options.incoming || 'incoming').attr('title', 'Incoming').appendTo($flow);
        $("<span class='arrow'>").text('\u2192').appendTo($flow);
        $("<span class='git-ref local'>").text(options.local || 'local').attr('title', 'Local').appendTo($flow);
        return $flow;
    };

    git.mergeLegend = function (options) {
        var texts = mergeTexts(options);
        var $legend = $("<div class='git-merge-legend'>");
        $("<div class='git-side-key local'>").append("<span class='git-side-dot'></span><b>Local</b>").append($("<span>").text(texts.local)).appendTo($legend);
        $("<div class='git-side-key incoming'>").append("<span class='git-side-dot'></span><b>Incoming</b>").append($("<span>").text(texts.incoming)).appendTo($legend);
        return $legend;
    };

    /**
     * Resolve merge conflicts item by item
     * @param {Object} options {
     *      items: [{ id, kind }], startId,
     *      kind: 'pull' | 'merge-into-main', local: branch name, incoming: branch name,
     *      loadDiff(id, callback(files, message)),
     *      resolve(id, side ('ours' = local | 'theirs' = incoming), callback()),
     *      complete(), abort()
     * }
     */
    git.conflicts = function (options) {
        var dialogId = 'datalinq-code-git-conflicts';
        var texts = mergeTexts(options);
        var items = (options.items || []).map(function (item) { return $.extend({ resolved: null }, item); });

        var close = openDialog({
            title: 'Resolve merge conflicts',
            width: '92%',
            height: '88%',
            id: dialogId,
            onload: function ($content) {
                var $root = $("<div class='git-conflicts'>").appendTo($content);

                var $banner = $("<div class='git-conflicts-banner'>").appendTo($root);
                var $bannerLeft = $("<div class='left'>").appendTo($banner);
                $("<div class='title'>").text(options.kind === 'merge-into-main' ? 'Merging branches' : 'Merging remote changes').appendTo($bannerLeft);
                git.mergeFlow(options).appendTo($bannerLeft);
                git.mergeLegend(options).appendTo($banner);
                var $progress = $("<div class='git-progress'>").appendTo($banner);
                var $progressBar = $("<div class='bar'><span></span></div>").appendTo($progress);
                var $progressText = $("<div class='text'>").appendTo($progress);

                var $body = $("<div class='git-conflicts-body'>").appendTo($root);
                var $list = $("<div class='git-conflicts-list'>").appendTo($body);
                $("<div class='git-cv-files-head'>").append("<span class='title'>Conflicting items</span>").appendTo($list);
                var $items = $("<div class='git-conflicts-items'>").appendTo($list);
                var $main = $("<div class='git-conflicts-main'>").appendTo($body);

                var viewer = null, currentItem = null;

                var updateProgress = function () {
                    var done = items.filter(function (i) { return i.resolved; }).length;
                    var count = items.length;
                    $progressBar.children('span').css('width', (count ? Math.round(100 * done / count) : 100) + '%');
                    $progressText.text(count ? done + ' of ' + count + ' resolved' : 'No conflicts');
                };

                var renderItems = function () {
                    $items.empty();
                    $.each(items, function (i, item) {
                        var kind = groupOf({ id: item.id, kind: item.kind, path: item.id }).kind;
                        var $row = $("<div class='git-conflict-item'>")
                            .toggleClass('selected', currentItem === item)
                            .toggleClass('resolved', !!item.resolved)
                            .attr('title', item.id)
                            .append(kindIcon(kind))
                            .append($("<span class='label'>").text(git.prettyId(item.id)))
                            .appendTo($items);

                        if (item.resolved) {
                            $("<span class='git-state'>").addClass(item.resolved).text('\u2713 ' + (item.resolved === 'local' ? 'Local' : 'Incoming')).appendTo($row);
                        } else {
                            $("<span class='git-state conflict'>").text('Conflict').appendTo($row);
                            $row.click(function () { selectItem(item); });
                        }
                    });
                    updateProgress();
                };

                var showDone = function () {
                    currentItem = null;
                    if (viewer) viewer.dispose();
                    renderItems();
                    $main.empty();
                    var $done = $("<div class='git-empty-state done'>").appendTo($main);
                    $("<div class='icon'>").text('\u2713').appendTo($done);
                    $("<div class='text'>").text('All conflicts are resolved').appendTo($done);
                    $("<div class='hint'>").text('Complete the merge to commit the result' + (options.kind === 'merge-into-main' ? " and push '" + (options.local || 'main') + "'." : '.')).appendTo($done);
                    var $buttons = $("<div class='buttons'>").appendTo($done);
                    $("<button type='button' class='datalinq-code-button'>").text('Complete merge').appendTo($buttons).click(function () {
                        close();
                        if (options.complete) options.complete();
                    });
                    $("<button type='button' class='datalinq-code-button cancel'>").text('Close').appendTo($buttons).click(function () { close(); });
                };

                var nextItem = function () {
                    var open = items.filter(function (i) { return !i.resolved; });
                    if (open.length === 0) {
                        showDone();
                    } else {
                        selectItem(open[0]);
                    }
                };

                var selectItem = function (item) {
                    currentItem = item;
                    if (viewer) viewer.dispose();
                    renderItems();

                    $main.empty();
                    var $head = $("<div class='git-conflict-head'>").appendTo($main);
                    $("<div class='title'>").text(git.prettyId(item.id)).appendTo($head);
                    $("<div class='hint'>").text('Both sides changed this item. Compare the versions and choose the one to keep for the whole item.').appendTo($head);

                    var $viewer = $("<div class='git-conflict-viewer'>").appendTo($main);
                    $viewer.append("<div class='git-loading'><span class='git-spinner'></span>Loading versions...</div>");

                    var $actions = $("<div class='git-conflict-actions'>").appendTo($main);
                    var choice = function (side, cls, title, text) {
                        return $("<button type='button' class='git-choice'>").addClass(cls)
                            .append("<span class='git-side-dot'></span>")
                            .append($("<span class='t'>").text(title))
                            .append($("<span class='s'>").text(text))
                            .appendTo($actions)
                            .click(function () {
                                $actions.find('button').prop('disabled', true);
                                options.resolve(item.id, side, function () {
                                    item.resolved = cls;
                                    nextItem();
                                });
                                // re-enable if the resolve failed (no callback)
                                setTimeout(function () { $actions.find('button').prop('disabled', false); }, 4000);
                            });
                    };
                    choice('ours', 'local', 'Keep local', texts.keepLocal);
                    choice('theirs', 'incoming', 'Take incoming', texts.takeIncoming);

                    options.loadDiff(item.id, function (files, message) {
                        if (currentItem !== item || !document.body.contains($viewer[0])) {
                            return;
                        }
                        $viewer.empty();
                        if (!files) {
                            $("<div class='git-diff-info error'>").text(message || 'Could not load the conflict').appendTo($viewer);
                            return;
                        }
                        viewer = git.changeViewer($viewer, files, {
                            layout: 'tabs',
                            conflict: true,
                            sides: {
                                old: { title: 'Local', sub: options.local },
                                'new': { title: 'Incoming', sub: options.incoming }
                            }
                        });
                    });
                };

                var start = items.filter(function (i) { return i.id === options.startId; })[0];
                if (items.length === 0) {
                    showDone();
                } else if (start) {
                    selectItem(start);
                } else {
                    nextItem();
                }

                var $footer = $("<div class='git-dialog-footer'>").appendTo($root);
                $("<button type='button' class='datalinq-code-button cancel'>").text('Abort merge').attr('title', 'Reset to the state before the merge').appendTo($footer).click(function () {
                    close();
                    if (options.abort) options.abort();
                });
                $("<span class='spacer'>").appendTo($footer);
                $("<button type='button' class='datalinq-code-button cancel'>").text('Close').appendTo($footer).click(function () { close(); });
            }
        });
    };

    /****** Branches ******/

    /**
     * @param {Object} status current git status
     * @param {Object} actions { checkout(name), create(name), remove(name), mergeIntoMain() }
     */
    git.branches = function (status, actions) {
        var dialogId = 'datalinq-code-git-branches';
        var close = function () { $('body').dataLinq_code_modal('close', { id: dialogId }); };

        $('body').dataLinq_code_modal({
            title: 'Branches',
            width: '640px',
            height: '70%',
            id: dialogId,
            onload: function ($content) {
                $content.addClass('datalinq-code-git-dialog');

                var $root = $("<div class='git-branches-dialog'>").appendTo($content);

                var $new = $("<div class='git-branch-new'>").appendTo($root);
                $("<div class='hint'>").text("New branches are always created from '" + status.mainBranch + "'.").appendTo($new);
                var $input = $("<input type='text' class='datalinq-code-prompt-input' placeholder='new branch name' />").appendTo($new);
                $("<button class='datalinq-code-button'>").text("Create from " + status.mainBranch).appendTo($new).click(function () {
                    var name = ($input.val() || '').trim();
                    if (!name) {
                        $input.focus();
                        return;
                    }
                    close();
                    actions.create(name);
                });
                $input.on('keydown', function (e) {
                    if (e.key === 'Enter') $new.find('button').trigger('click');
                });

                var $list = $("<div class='git-branch-list'>").text('Loading...').appendTo($root);

                dataLinqCode.api.git('branches', {}, function (result) {
                    $list.empty();
                    if (!result.success) {
                        $list.text(result.message);
                        return;
                    }

                    $.each(result.data || [], function (i, branch) {
                        var $row = $("<div class='git-branch-row'>")
                            .toggleClass('current', branch.isCurrent)
                            .toggleClass('main', branch.isMain)
                            .appendTo($list);

                        $("<span class='name'>").text(branch.name).appendTo($row);

                        var info = [];
                        if (branch.isCurrent) info.push('current');
                        if (!branch.isLocal) info.push('remote only');
                        if (branch.isLocal && !branch.isRemote) info.push('not published');
                        if (branch.ahead) info.push('\u2191' + branch.ahead);
                        if (branch.behind) info.push('\u2193' + branch.behind);
                        $("<span class='info'>").text(info.join(' \u2022 ')).appendTo($row);

                        var $buttons = $("<span class='buttons'>").appendTo($row);
                        if (!branch.isCurrent) {
                            $("<button class='datalinq-code-button'>").text('Switch').appendTo($buttons).click(function () {
                                close();
                                actions.checkout(branch.name);
                            });
                        }
                        if (branch.isCurrent && !branch.isMain) {
                            $("<button class='datalinq-code-button'>").text('Merge into ' + status.mainBranch).appendTo($buttons).click(function () {
                                close();
                                actions.mergeIntoMain();
                            });
                        }
                        if (!branch.isCurrent && !branch.isMain) {
                            $("<button class='datalinq-code-button cancel'>").text('Delete').appendTo($buttons).click(function () {
                                close();
                                actions.remove(branch.name);
                            });
                        }
                    });
                });

                $input.focus();
            }
        });
    };

    /****** Connect ******/

    git.connectChoice = function (status, onMode) {
        var dialogId = 'datalinq-code-git-connect';
        $('body').dataLinq_code_modal({
            title: 'Connect repository',
            width: '680px',
            height: '380px',
            id: dialogId,
            onload: function ($content) {
                $content.addClass('datalinq-code-messagebox-content datalinq-code-git-dialog');

                $("<p>").text('The repository ' + status.remoteUrl + ' already contains content. How do you want to connect?').appendTo($content);

                var addOption = function (mode, title, text, danger) {
                    var $option = $("<div class='git-connect-option'>").toggleClass('danger', !!danger).appendTo($content);
                    $("<div class='title'>").text(title).appendTo($option);
                    $("<div class='text'>").text(text).appendTo($option);
                    $option.click(function () {
                        $('body').dataLinq_code_modal('close', { id: dialogId });
                        onMode(mode);
                    });
                };

                addOption('merge-local', 'Merge local content on top',
                    'Keeps the content of this DataLinq instance (it wins for items that exist on both sides), adds items that only exist in the repository and pushes the result.');
                addOption('import', 'Import repository into DataLinq',
                    'Replaces the DataLinq content with the repository content. Endpoints, queries and views that do not exist in the repository are deleted.', true);

                var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);
                $("<button>").addClass("datalinq-code-button cancel").text("Cancel").appendTo($buttonbar).click(function () {
                    $('body').dataLinq_code_modal('close', { id: dialogId });
                });
            }
        });
    };

    /****** Merge into main ******/

    git.mergeIntoMain = function (status, onConfirm) {
        var dialogId = 'datalinq-code-git-merge';
        $('body').dataLinq_code_modal({
            title: esc("Merge '" + status.branch + "' into '" + status.mainBranch + "'"),
            width: '640px',
            height: '330px',
            id: dialogId,
            onload: function ($content) {
                $content.addClass('datalinq-code-messagebox-content datalinq-code-git-dialog');

                git.mergeFlow({ incoming: status.branch, local: status.mainBranch }).addClass('large').appendTo($content);

                $("<p>").text("All commits of '" + status.branch + "' are merged into '" + status.mainBranch + "' and pushed. " +
                    "Afterwards '" + status.mainBranch + "' is checked out and its content is loaded into DataLinq.").appendTo($content);
                $("<p class='git-hint'>").text("If both branches changed the same item, you choose for each conflict: Local = '" + status.mainBranch +
                    "' version, Incoming = '" + status.branch + "' version.").appendTo($content);

                var $label = $("<label class='git-checkbox-label'>").appendTo($content);
                var $delete = $("<input type='checkbox' checked />").appendTo($label);
                $("<span>").text(" delete branch '" + status.branch + "' after the merge").appendTo($label);

                var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);
                $("<button>").addClass("datalinq-code-button cancel").text("Cancel").appendTo($buttonbar).click(function () {
                    $('body').dataLinq_code_modal('close', { id: dialogId });
                });
                $("<button>").addClass("datalinq-code-button").text("Merge").appendTo($buttonbar).click(function () {
                    $('body').dataLinq_code_modal('close', { id: dialogId });
                    onConfirm($delete.is(':checked'));
                });
            }
        });
    };
}).call(dataLinqCode.ui, jQuery);
