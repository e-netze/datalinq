/**
 * DataLinq Code - version control (git) dialogs
 *
 * dataLinqCode.ui.git.diff(id, files, options)  : unified diff of item files
 * dataLinqCode.ui.git.history(id)               : commit history (optional for one item)
 * dataLinqCode.ui.git.branches(status, actions) : manage branches
 * dataLinqCode.ui.git.connect(status, onMode)   : choose how to connect to a non empty remote
 * dataLinqCode.ui.git.mergeIntoMain(status, onConfirm)
 * dataLinqCode.ui.git.diffLines(oldText, newText): line diff (Myers)
 * Depends on: core/namespace.js, ui/modal.js, git/git-api.js
 */
(function ($) {
    var ui = this;
    var git = ui.git = ui.git || {};

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

    /****** Diff rendering ******/

    var renderDiff = function ($target, file, fullFile) {
        $target.empty();

        if (file.isBinary) {
            $("<div class='git-diff-info'>").text('Binary file').appendTo($target);
            return;
        }

        var ops = git.diffLines(file.oldText, file.newText);
        var changed = ops.filter(function (o) { return o.t !== ' '; }).length;

        if (changed === 0) {
            $("<div class='git-diff-info'>").text('No differences').appendTo($target);
            return;
        }

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

        for (var i = 0; i < ops.length; i++) {
            var op = ops[i];
            if (!visible[i]) {
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

        $target.html(html.join(''));
    };

    var statusLetter = function (status) {
        return { added: 'A', modified: 'M', deleted: 'D', conflicted: 'C' }[status] || '?';
    };

    /**
     * Shows file diffs in a dialog
     * @param {string} title
     * @param {Array} files GitFileDiff[] ({ path, status, oldText, newText, oldLabel, newLabel })
     * @param {Object} options { buttons: [{ text, cls, action }] }
     */
    git.diff = function (title, files, options) {
        options = options || {};

        $('body').dataLinq_code_modal({
            title: esc(title),
            width: '90%',
            height: '85%',
            id: 'datalinq-code-git-diff',
            onload: function ($content) {
                $content.addClass('datalinq-code-git-dialog');

                var $root = $("<div class='git-diff-dialog'>").appendTo($content);
                var $files = $("<div class='git-diff-files'>").appendTo($root);
                var $header = $("<div class='git-diff-header'>").appendTo($root);
                var $body = $("<div class='git-diff-body'>").appendTo($root);
                var fullFile = false, current = null;

                var $full = $("<label class='git-diff-full'><input type='checkbox' /> show full file</label>");
                $full.find('input').on('change', function () {
                    fullFile = $(this).is(':checked');
                    if (current) renderDiff($body, current, fullFile);
                });

                var select = function (file, $li) {
                    current = file;
                    $files.children().removeClass('selected');
                    $li.addClass('selected');
                    $header.empty()
                        .append($("<span class='git-diff-label old'>").text('\u2212 ' + (file.oldLabel || 'old')))
                        .append($("<span class='git-diff-label new'>").text('+ ' + (file.newLabel || 'new')))
                        .append($full);
                    renderDiff($body, file, fullFile);
                };

                if (!files || files.length === 0) {
                    $body.append($("<div class='git-diff-info'>").text('No changes'));
                }

                $.each(files || [], function (i, file) {
                    var $li = $("<div class='git-diff-file'>")
                        .addClass('status-' + file.status)
                        .attr('title', file.path)
                        .append($("<span class='git-status-badge'>").text(statusLetter(file.status)))
                        .append($("<span class='name'>").text(file.path))
                        .appendTo($files)
                        .click(function () { select(file, $li); });

                    if (i === 0) select(file, $li);
                });

                if (options.buttons && options.buttons.length) {
                    var $bar = $("<div class='button-bar git-dialog-buttons'>").appendTo($root);
                    $.each(options.buttons, function (i, button) {
                        $("<button class='datalinq-code-button'>")
                            .addClass(button.cls || '')
                            .text(button.text)
                            .appendTo($bar)
                            .click(function () {
                                $('body').dataLinq_code_modal('close', { id: 'datalinq-code-git-diff' });
                                button.action();
                            });
                    });
                }
            }
        });
    };

    /****** History ******/

    var formatDate = function (value) {
        var d = new Date(value);
        return isNaN(d) ? value : d.toLocaleString();
    };

    git.history = function (id) {
        $('body').dataLinq_code_modal({
            title: esc('History' + (id ? ': ' + id : '')),
            width: '90%',
            height: '85%',
            id: 'datalinq-code-git-history',
            onload: function ($content) {
                $content.addClass('datalinq-code-git-dialog');

                var $root = $("<div class='git-history-dialog'>").appendTo($content);
                var $list = $("<div class='git-history-list'>").text('Loading...').appendTo($root);
                var $details = $("<div class='git-history-details'>").appendTo($root);

                dataLinqCode.api.git('history', { id: id || null, count: 200 }, function (result) {
                    $list.empty();
                    if (!result.success) {
                        $list.text(result.message);
                        return;
                    }
                    if (!result.data || result.data.length === 0) {
                        $list.text('No commits');
                        return;
                    }

                    $.each(result.data, function (i, commit) {
                        var $item = $("<div class='git-history-item'>")
                            .toggleClass('unpushed', !commit.isPushed)
                            .appendTo($list)
                            .click(function () {
                                $list.children().removeClass('selected');
                                $item.addClass('selected');
                                showCommit(commit, $details, id);
                            });

                        $("<div class='msg'>").text(commit.messageShort).appendTo($item);
                        $("<div class='meta'>")
                            .text(commit.shortSha + ' \u2022 ' + commit.author + ' \u2022 ' + formatDate(commit.date) + (commit.isPushed ? '' : ' \u2022 not pushed'))
                            .appendTo($item);

                        if (i === 0) $item.trigger('click');
                    });
                });
            }
        });
    };

    var showCommit = function (commit, $details, id) {
        $details.empty().text('Loading...');

        dataLinqCode.api.git('commit-details', { sha: commit.sha }, function (result) {
            $details.empty();
            if (!result.success) {
                $details.text(result.message);
                return;
            }

            $("<div class='git-commit-message'>").text(commit.message).appendTo($details);
            $("<div class='git-commit-meta'>").text(commit.sha + ' \u2022 ' + commit.author + ' <' + commit.email + '> \u2022 ' + formatDate(commit.date)).appendTo($details);

            var files = result.data || [];
            if (id) {
                // item history: show the files of the item first
                files.sort(function (x, y) { return (y.id === id || y.id.indexOf(id + '@') === 0) - (x.id === id || x.id.indexOf(id + '@') === 0); });
            }

            var $files = $("<div class='git-diff-files'>").appendTo($details);
            var $body = $("<div class='git-diff-body'>").appendTo($details);

            $.each(files, function (i, file) {
                var $li = $("<div class='git-diff-file'>")
                    .addClass('status-' + file.status)
                    .attr('title', file.path)
                    .append($("<span class='git-status-badge'>").text(statusLetter(file.status)))
                    .append($("<span class='name'>").text(file.path))
                    .appendTo($files)
                    .click(function () {
                        $files.children().removeClass('selected');
                        $li.addClass('selected');
                        renderDiff($body, file, false);
                    });
                if (i === 0) $li.trigger('click');
            });
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
            height: '260px',
            id: dialogId,
            onload: function ($content) {
                $content.addClass('datalinq-code-messagebox-content datalinq-code-git-dialog');

                $("<p>").text("All commits of '" + status.branch + "' are merged into '" + status.mainBranch + "' and pushed. " +
                    "Afterwards '" + status.mainBranch + "' is checked out and its content is loaded into DataLinq.").appendTo($content);

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
