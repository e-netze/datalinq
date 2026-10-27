/**
 * DataLinq Code - version control (git) view
 *
 * Adds a switch to the tree toolbar: Explorer (default tree) <-> Git.
 * Git mode shows the changed items (added/modified/deleted/conflicted) in the tree hierarchy
 * (folder > endpoint > query > view, css/js, error pages), with checkboxes for committing,
 * toolbar icons for fetch, pull, commit, push, history and branches, a branch bar
 * (switch/new branch from main/merge into main) and merge conflict handling.
 * Only active if the DataLinq instance has version control enabled (datalinqFeatures.VersionControl).
 * Depends on: core, git/git-api.js, git/git-dialogs.js, ui/dialogs.js, tree/tree.js, editor/editor.js
 */
dataLinqCode.features.register('git', function (state) {
    var features = window.datalinqFeatures || {};
    if (!features.VersionControl) {
        return;
    }

    var $ = jQuery;
    var storageKey = 'datalinq-code-tree-mode';
    var $holder = state.$tree;
    var $toolbar = $('.datalinq-code-tree-top > .datalinq-code-tree-toolbar');

    var mode = 'explorer';
    var status = null;
    var busy = false;
    var unchecked = {};         // change ids excluded from the next commit
    var collapsed = {};         // collapsed git tree nodes
    var folderStructure = {};
    var refreshTimer = null;
    var messageTimer = null;

    /****** helpers ******/

    var api = function (operation, request, callback) {
        dataLinqCode.api.git(operation, request, callback);
    };

    var alertMessage = function (title, message) {
        dataLinqCode.ui.alert(title, message);
    };

    var eventAt = function ($element) {
        var offset = $element.offset();
        return { clientX: offset.left, clientY: offset.top + $element.outerHeight() + 2 };
    };

    var dirtyDocs = function () {
        return state.$editor ? (state.$editor.dataLinqCode_editor('dirtyDocs') || []) : [];
    };

    var requireSavedDocs = function (operationText, callback) {
        var dirty = dirtyDocs();
        if (dirty.length > 0) {
            alertMessage('Unsaved documents', 'Save or close these documents before you ' + operationText + ': ' + dirty.join(', '));
            return;
        }
        callback();
    };

    var changeCount = function () {
        return status && status.changes ? status.changes.length : 0;
    };

    var conflicts = function () {
        return status && status.changes ? status.changes.filter(function (c) { return c.status === 'conflicted'; }) : [];
    };

    var labels = function () {
        var info = status && status.mergeInfo;
        if (info && info.kind === 'merge-into-main') {
            return { ours: "Keep '" + info.target + "' version", theirs: "Take '" + info.source + "' version" };
        }
        return { ours: 'Keep mine (local)', theirs: 'Take theirs (remote)' };
    };

    /****** toolbar ******/

    var $toggle = $("<div>")
        .addClass('tree-tool git-mode-toggle')
        .attr('title', 'Switch to Git view')
        .click(function (e) {
            e.stopPropagation();
            setMode(mode === 'git' ? 'explorer' : 'git');
        });
    // insert before the first tool (not before leading whitespace text, which would take up space)
    if ($toolbar.children().length > 0) {
        $toggle.insertBefore($toolbar.children().first());
    } else {
        $toggle.appendTo($toolbar);
    }
    var $toggleBadge = $("<span class='git-badge'>").appendTo($toggle);

    var $gitTools = $("<div class='git-tools'>").appendTo($toolbar);

    var addTool = function (cls, title, onClick) {
        var $tool = $("<div>")
            .addClass('tree-tool git-tool ' + cls)
            .attr('title', title)
            .appendTo($gitTools)
            .click(function (e) {
                e.stopPropagation();
                if (!busy && !$(this).hasClass('disabled')) {
                    onClick($(this), e);
                }
            });
        $("<span class='git-badge'>").appendTo($tool);
        return $tool;
    };

    var $fetchTool = addTool('fetch', 'Fetch & refresh', function () { refresh(true); });
    var $pullTool = addTool('pull', 'Pull (get changes from the remote repository)', function () { pull(); });
    var $commitTool = addTool('commit', 'Commit selected changes', function () { commit(false); });
    var $pushTool = addTool('push', 'Push (send commits to the remote repository)', function () { push(); });
    var $historyTool = addTool('history', 'History', function () { dataLinqCode.ui.git.history(null); });
    var $moreTool = addTool('more', 'More...', function ($tool) { showMoreMenu($tool); });

    /****** panel ******/

    var $panel = $("<div class='datalinq-code-git-view'>")
        .appendTo($holder)
        .on('contextmenu', function (e) {
            e.preventDefault();
            e.stopPropagation();
        });

    var $connect = $("<div class='git-connect'>").appendTo($panel);
    var $branchBar = $("<div class='git-branch-bar'>").appendTo($panel);
    var $mergeBanner = $("<div class='git-merge-banner'>").appendTo($panel);
    var $importBanner = $("<div class='git-import-banner'>").appendTo($panel);
    var $commitBox = $("<div class='git-commit-box'>").appendTo($panel);
    var $message = $("<textarea class='git-commit-message' rows='2' placeholder='Commit message (Ctrl+Enter to commit)'>").appendTo($commitBox);
    var $commitButtons = $("<div class='git-commit-buttons'>").appendTo($commitBox);
    $("<button class='datalinq-code-button git-commit-button'>").text('Commit').appendTo($commitButtons).click(function () { commit(false); });
    $("<button class='datalinq-code-button git-commit-push-button'>").text('Commit & Push').appendTo($commitButtons).click(function () { commit(true); });
    $message.on('keydown', function (e) {
        if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            commit(false);
        }
    });

    var $changesHeader = $("<div class='git-changes-header'>").appendTo($panel);
    var $changes = $("<ul class='git-tree'>").appendTo($panel);
    var $empty = $("<div class='git-empty'>").appendTo($panel);
    var $info = $("<div class='git-info-message'>").appendTo($panel);
    var $busy = $("<div class='git-busy'><div class='git-busy-text'></div></div>").appendTo($panel);

    /****** mode ******/

    var setMode = function (newMode) {
        mode = newMode === 'git' ? 'git' : 'explorer';
        localStorage.setItem(storageKey, mode);

        var git = mode === 'git';
        $holder.toggleClass('git-mode', git);
        $toolbar.toggleClass('git-mode', git);
        $toggle.toggleClass('active', git).attr('title', git ? 'Switch to Explorer view' : 'Switch to Git view');

        if (git) {
            render();
            refresh(true);
        }
    };

    /****** status ******/

    var setBusy = function (isBusy, text) {
        busy = isBusy;
        $panel.toggleClass('busy', isBusy);
        $toolbar.toggleClass('git-busy-state', isBusy);
        $busy.find('.git-busy-text').text(text || 'Working...');
    };

    var showInfo = function (text, isError) {
        clearTimeout(messageTimer);
        $info.text(text || '').toggleClass('error', !!isError).toggleClass('visible', !!text);
        if (text && !isError) {
            messageTimer = setTimeout(function () { $info.removeClass('visible'); }, 6000);
        }
    };

    var refresh = function (fetch, silent) {
        if (busy) {
            return;
        }
        if (!silent) {
            setBusy(true, fetch ? 'Fetching...' : 'Refreshing...');
        }

        dataLinqCode.api.getFolderStructure(function (folders) {
            folderStructure = parseFolders(folders);

            api('status', { fetch: !!fetch }, function (result) {
                if (!silent) {
                    setBusy(false);
                }
                if (result.status) {
                    setStatus(result.status);
                } else if (result.errorCode === 'not-authorized' || result.errorCode === 'not-configured') {
                    setStatus({ enabled: false, connected: false, unavailable: result.message, changes: [] });
                }
                if (!result.success) {
                    showInfo(result.message, true);
                } else if (result.message && result.message.indexOf('Fetch failed') === 0) {
                    showInfo(result.message, true);
                }
            });
        });
    };

    var scheduleRefresh = function () {
        clearTimeout(refreshTimer);
        refreshTimer = setTimeout(function () { refresh(false, true); }, 1200);
    };

    var parseFolders = function (folders) {
        if (!folders) return {};
        if (typeof folders === 'string') {
            try { folders = JSON.parse(folders); } catch (e) { return {}; }
        }
        return folders && typeof folders === 'object' ? folders : {};
    };

    var setStatus = function (newStatus) {
        status = newStatus;

        // forget selections of items that are no longer changed
        var ids = {};
        $.each(status.changes || [], function (i, c) { ids[c.id] = true; });
        $.each(Object.keys(unchecked), function (i, id) { if (!ids[id]) delete unchecked[id]; });

        render();
    };

    /****** operations ******/

    var run = function (operation, request, busyText, onSuccess) {
        if (busy) {
            return;
        }
        setBusy(true, busyText);
        showInfo('');

        api(operation, request, function (result) {
            setBusy(false);

            if (result.status) {
                setStatus(result.status);
            }

            applyImport(result);

            if (result.success) {
                showInfo(result.message);
                if (onSuccess) onSuccess(result);
            } else if (result.errorCode === 'conflicts') {
                showInfo(result.message, true);
                if (mode !== 'git') setMode('git');
            } else {
                var message = result.message || 'Unknown error';
                if (result.importErrors && result.importErrors.length) {
                    message += '\n' + result.importErrors.join('\n');
                }
                showInfo(message, true);
                alertMessage('Version control', message);
            }

            if (result.success && result.importErrors && result.importErrors.length) {
                alertMessage('Version control', 'Some items could not be loaded into DataLinq:\n' + result.importErrors.join('\n'));
            }
        });
    };

    // loads the changes of git operations into the open editors and the explorer tree
    var applyImport = function (result) {
        var imported = result.imported || [], deleted = result.deleted || [];
        if (imported.length === 0 && deleted.length === 0) {
            return;
        }

        var $tabs = $('.datalinq-code-tabs').children('.datalinq-code-tab');

        $.each(deleted, function (i, id) {
            $tabs.each(function () {
                var $tab = $(this), tabId = $tab.attr('data-id');
                if (tabId === id || (tabId && tabId.indexOf(id + '@') === 0)) {
                    var selected = $tab.hasClass('selected');
                    $tab.remove();
                    dataLinqCode.events.fire('tab-removed', { id: tabId, selected: selected });
                }
            });
        });

        $('.datalinq-code-tabs').children('.datalinq-code-tab').each(function () {
            var $tab = $(this), tabId = $tab.attr('data-id');
            if ($.inArray(tabId, imported) >= 0) {
                $(".datalinq-code-editor-frame[data-id='" + tabId + "']").remove();
                $tab.removeClass('dirty errors');
                if ($tab.hasClass('selected')) {
                    dataLinqCode.events.fire('tab-selected', { id: tabId });
                }
            }
        });

        if (state.$tree) {
            state.$tree.dataLinqCode_tree('refreshSilent');
        }
        dataLinqCode.events.fire('refresh-ui');
    };

    var selectedIds = function () {
        return (status && status.changes ? status.changes : [])
            .filter(function (c) { return c.status !== 'conflicted' && !unchecked[c.id]; })
            .map(function (c) { return c.id; });
    };

    var commit = function (andPush) {
        if (!status || !status.connected || status.merging) {
            return;
        }

        var message = ($message.val() || '').trim();
        if (!message) {
            $message.addClass('invalid').focus();
            setTimeout(function () { $message.removeClass('invalid'); }, 1500);
            showInfo('Enter a commit message', true);
            return;
        }

        var ids = selectedIds();
        if (ids.length === 0) {
            showInfo('Select the changes you want to commit', true);
            return;
        }

        var dirty = dirtyDocs();
        dataLinqCode.ui.confirmIf(dirty.length > 0, 'Unsaved documents',
            'Unsaved changes are not part of the commit (' + dirty.join(', ') + '). Commit anyway?',
            function () {
                run('commit', { message: message, ids: ids, push: !!andPush }, andPush ? 'Committing and pushing...' : 'Committing...', function () {
                    $message.val('');
                });
            });
    };

    var push = function () {
        run('push', {}, 'Pushing...');
    };

    var pull = function () {
        requireSavedDocs('pull', function () {
            run('pull', {}, 'Pulling...');
        });
    };

    var discard = function (ids) {
        if (!ids || ids.length === 0) {
            return;
        }
        requireSavedDocs('discard changes', function () {
            dataLinqCode.ui.confirm('Discard changes',
                'Discard the changes of ' + (ids.length === 1 ? ids[0] : ids.length + ' items') + '? The last committed version is restored in DataLinq. This can not be undone.',
                function () {
                    run('discard', { ids: ids }, 'Discarding...');
                });
        });
    };

    var showDiff = function (change) {
        api('diff', { id: change.id }, function (result) {
            if (!result.success) {
                alertMessage('Version control', result.message);
                return;
            }

            var buttons = [];
            if (change.status === 'conflicted') {
                var l = labels();
                buttons.push({ text: l.ours, cls: 'cancel', action: function () { resolve(change.id, 'ours'); } });
                buttons.push({ text: l.theirs, action: function () { resolve(change.id, 'theirs'); } });
            } else if (!status.merging) {
                buttons.push({ text: 'Discard changes', cls: 'cancel', action: function () { discard([change.id]); } });
            }

            dataLinqCode.ui.git.diff('Changes: ' + change.id.replace(/^_file@/, ''), result.data, { buttons: buttons });
        });
    };

    var resolve = function (id, side) {
        run('resolve', { id: id, side: side }, 'Resolving...');
    };

    var completeMerge = function () {
        requireSavedDocs('complete the merge', function () {
            run('complete-merge', {}, 'Completing merge...');
        });
    };

    var abortMerge = function () {
        dataLinqCode.ui.confirm('Abort merge', 'Abort the merge? The working copy is reset to the state before the merge.', function () {
            run('abort-merge', {}, 'Aborting merge...');
        });
    };

    var checkout = function (name) {
        requireSavedDocs('switch the branch', function () {
            run('checkout', { name: name }, "Switching to '" + name + "'...");
        });
    };

    var createBranch = function (name) {
        requireSavedDocs('create a branch', function () {
            run('create-branch', { name: name }, "Creating branch '" + name + "'...");
        });
    };

    var deleteBranch = function (name) {
        dataLinqCode.ui.confirm('Delete branch', "Delete branch '" + name + "' locally and on the remote repository?", function () {
            run('delete-branch', { name: name }, "Deleting '" + name + "'...");
        });
    };

    var mergeIntoMain = function () {
        if (!status || status.isMainBranch) {
            return;
        }
        requireSavedDocs('merge', function () {
            dataLinqCode.ui.git.mergeIntoMain(status, function (deleteBranch) {
                run('merge-into-main', { deleteBranch: deleteBranch }, "Merging '" + status.branch + "' into '" + status.mainBranch + "'...");
            });
        });
    };

    var branchActions = {
        checkout: checkout,
        create: createBranch,
        remove: deleteBranch,
        mergeIntoMain: mergeIntoMain
    };

    var connect = function (connectMode) {
        requireSavedDocs('connect the repository', function () {
            run('connect', { mode: connectMode || null }, 'Connecting repository...', null);
        });
    };

    var disconnect = function () {
        dataLinqCode.ui.confirm('Disconnect',
            'Remove the local working copy of the repository? DataLinq content and the remote repository are not changed. Uncommitted and unpushed changes are lost for version control.',
            function () {
                run('disconnect', {}, 'Disconnecting...');
            });
    };

    var showMoreMenu = function ($tool) {
        if (!status || !status.connected) {
            return;
        }
        dataLinqCode.ui.contextMenu(eventAt($tool), [
            { text: 'Fetch', action: function () { refresh(true); } },
            { text: 'Pull', action: pull },
            { text: 'Push', action: push },
            '-',
            { text: 'Branches...', action: function () { dataLinqCode.ui.git.branches(status, branchActions); } },
            !status.isMainBranch && !status.merging ? { text: "Merge '" + status.branch + "' into '" + status.mainBranch + "'...", action: mergeIntoMain } : null,
            { text: 'History...', action: function () { dataLinqCode.ui.git.history(null); } },
            '-',
            !status.merging && changeCount() > 0 ? { text: 'Discard all changes...', danger: true, action: function () { discard(status.changes.map(function (c) { return c.id; })); } } : null,
            { text: 'Disconnect local working copy...', danger: true, action: disconnect }
        ]);
    };

    var showBranchMenu = function ($button) {
        if (busy || status.merging) {
            return;
        }
        api('branches', {}, function (result) {
            if (!result.success) {
                alertMessage('Version control', result.message);
                return;
            }

            var items = [];
            $.each(result.data || [], function (i, branch) {
                items.push({
                    text: (branch.isCurrent ? '\u2713 ' : '\u2003 ') + branch.name + (branch.isLocal ? '' : '  (remote)'),
                    action: function () { if (!branch.isCurrent) checkout(branch.name); }
                });
            });
            items.push('-');
            items.push({
                text: "New branch from '" + status.mainBranch + "'...", action: function () {
                    dataLinqCode.ui.prompt('New branch', "The branch is created from '" + status.mainBranch + "' and checked out.", '', createBranch, {
                        validate: function (value) { return /^[A-Za-z0-9._\/-]+$/.test(value) && value.indexOf('..') < 0 ? null : 'Use letters, digits, ".", "_", "-" and "/" only.'; }
                    });
                }
            });
            if (!status.isMainBranch) {
                items.push({ text: "Merge into '" + status.mainBranch + "'...", action: mergeIntoMain });
            }
            items.push({ text: 'Manage branches...', action: function () { dataLinqCode.ui.git.branches(status, branchActions); } });

            dataLinqCode.ui.contextMenu(eventAt($button), items);
        });
    };

    /****** rendering ******/

    var render = function () {
        var connected = !!(status && status.connected);
        var merging = !!(status && status.merging);
        var count = changeCount();
        var conflictCount = conflicts().length;

        // toggle badge (visible in both modes)
        $toggleBadge.text(conflictCount > 0 ? '!' : count > 0 ? (count > 99 ? '99+' : count) : '')
            .toggleClass('visible', count > 0 || conflictCount > 0)
            .toggleClass('conflict', conflictCount > 0);

        if (mode !== 'git') {
            return;
        }

        $panel.toggleClass('not-connected', !connected).toggleClass('merging', merging);
        $gitTools.find('.git-tool').not('.fetch').toggleClass('disabled', !connected);
        $pushTool.toggleClass('disabled', !connected || merging);
        $pullTool.toggleClass('disabled', !connected || merging);
        $commitTool.toggleClass('disabled', !connected || merging || count === 0);
        $pullTool.find('.git-badge').text(connected && status.behind ? status.behind : '').toggleClass('visible', !!(connected && status.behind));
        $pushTool.find('.git-badge').text(connected && status.ahead ? status.ahead : '').toggleClass('visible', !!(connected && status.ahead));

        renderConnect(connected);
        if (!connected) {
            $changes.empty();
            return;
        }

        renderBranchBar();
        renderMergeBanner(merging, conflictCount);
        renderImportBanner();
        renderChanges();
    };

    var renderConnect = function (connected) {
        $connect.empty();
        if (connected || !status) {
            return;
        }

        $("<div class='title'>").text('Version control').appendTo($connect);
        if (status.unavailable) {
            $("<p>").text(status.unavailable).appendTo($connect);
            return;
        }
        $("<p>").text('This DataLinq instance is not connected to its repository yet.').appendTo($connect);
        $("<p class='git-remote-url'>").text(status.remoteUrl || '').appendTo($connect);
        $("<button class='datalinq-code-button'>").text('Connect repository').appendTo($connect).click(function () {
            if (busy) return;
            setBusy(true, 'Connecting repository...');
            api('connect', {}, function (result) {
                setBusy(false);
                if (result.errorCode === 'remote-not-empty') {
                    dataLinqCode.ui.git.connectChoice(status, function (connectMode) {
                        if (connectMode === 'import') {
                            dataLinqCode.ui.confirm('Import repository',
                                'Endpoints, queries and views of this DataLinq instance that are not in the repository will be deleted. Continue?',
                                function () { connect('import'); });
                        } else {
                            connect(connectMode);
                        }
                    });
                    return;
                }
                if (result.status) setStatus(result.status);
                applyImport(result);
                if (result.success) {
                    showInfo(result.message);
                } else {
                    showInfo(result.message, true);
                    alertMessage('Version control', result.message);
                }
            });
        });
    };

    var renderBranchBar = function () {
        $branchBar.empty();

        var $branch = $("<div class='git-branch-button'>")
            .attr('title', 'Switch branch / new branch from ' + status.mainBranch)
            .appendTo($branchBar)
            .click(function (e) {
                e.stopPropagation();
                showBranchMenu($branch);
            });
        $("<span class='git-branch-icon'>").appendTo($branch);
        $("<span class='git-branch-name'>").text(status.branch || '(no branch)').appendTo($branch);

        var sync = [];
        if (!status.hasUpstream) sync.push('not published');
        if (status.ahead) sync.push('\u2191' + status.ahead);
        if (status.behind) sync.push('\u2193' + status.behind);
        $("<span class='git-sync'>")
            .text(sync.join(' '))
            .attr('title', (status.ahead || 0) + ' commit(s) to push, ' + (status.behind || 0) + ' commit(s) to pull')
            .appendTo($branchBar);

        if (!status.isMainBranch && !status.merging) {
            $("<button class='datalinq-code-button git-merge-main-button'>")
                .text('Merge into ' + status.mainBranch)
                .appendTo($branchBar)
                .click(function (e) {
                    e.stopPropagation();
                    mergeIntoMain();
                });
        }
    };

    var renderMergeBanner = function (merging, conflictCount) {
        $mergeBanner.empty();
        if (!merging) {
            return;
        }

        var info = status.mergeInfo || {};
        $("<div class='text'>").text(
            (info.kind === 'merge-into-main' ? "Merging '" + info.source + "' into '" + info.target + "'" : 'Merging ' + (info.source || 'remote changes')) +
            (conflictCount > 0 ? ' \u2022 ' + conflictCount + ' conflict(s): choose a version for each conflicting item.' : ' \u2022 all conflicts resolved.')
        ).appendTo($mergeBanner);

        var $buttons = $("<div class='buttons'>").appendTo($mergeBanner);
        $("<button class='datalinq-code-button'>").text('Complete merge').prop('disabled', conflictCount > 0).appendTo($buttons).click(completeMerge);
        $("<button class='datalinq-code-button cancel'>").text('Abort merge').appendTo($buttons).click(abortMerge);
    };

    var renderImportBanner = function () {
        $importBanner.empty();
        var errors = status.importErrors || [];
        $importBanner.toggle(errors.length > 0 && !status.merging);
        if (errors.length === 0) {
            return;
        }

        $("<div class='text'>").text(errors.length + ' item(s) of the repository could not be loaded into DataLinq (e.g. invalid json or missing endpoint). Fix them in the repository and pull, or keep the DataLinq version:').appendTo($importBanner);
        var $list = $("<ul>").appendTo($importBanner);
        $.each(errors, function (i, id) { $("<li>").text(id.replace(/^_file@/, '')).appendTo($list); });

        var $buttons = $("<div class='buttons'>").appendTo($importBanner);
        $("<button class='datalinq-code-button'>").text('Retry import').appendTo($buttons).click(function () {
            requireSavedDocs('import', function () { run('retry-import', {}, 'Importing...'); });
        });
        $("<button class='datalinq-code-button cancel'>").text('Keep DataLinq version').appendTo($buttons).click(function () {
            dataLinqCode.ui.confirm('Keep DataLinq version',
                'The repository version of these items is replaced by the DataLinq version (shown as uncommitted changes). Continue?',
                function () { run('keep-datalinq', { ids: errors }, 'Updating...'); });
        });
    };

    /****** git tree ******/

    var buildTree = function (changes) {
        var root = { key: '', children: {}, order: [] };

        var child = function (parent, key, props) {
            if (!parent.children[key]) {
                parent.children[key] = $.extend({ key: key, children: {}, order: [], changes: [] }, props);
                parent.order.push(key);
            }
            return parent.children[key];
        };

        var endpointFolder = function (ep) {
            var found = null;
            $.each(Object.keys(folderStructure).sort(), function (i, folder) {
                if (!found && $.inArray(ep, folderStructure[folder] || []) >= 0) found = folder;
            });
            return found;
        };

        var endpointNode = function (ep) {
            var folder = endpointFolder(ep);
            var parent = folder ? child(root, 'folder:' + folder, { label: folder, kind: 'folder' }) : root;
            return child(parent, 'ep:' + ep, { label: ep, kind: 'endpoint', docId: ep });
        };

        $.each(changes, function (i, c) {
            var p = c.id.split('@');
            var node;

            switch (c.kind) {
                case 'endpoint':
                    node = endpointNode(p[0]);
                    break;
                case 'endpoint-css':
                case 'endpoint-js':
                    node = child(endpointNode(p[0]), 'item:' + c.id, { label: c.kind === 'endpoint-css' ? 'CSS' : 'JavaScript', kind: c.kind, docId: c.id });
                    break;
                case 'query':
                    node = child(endpointNode(p[0]), 'q:' + p[0] + '@' + p[1], { label: p[1], kind: 'query', docId: p[0] + '@' + p[1] });
                    break;
                case 'view':
                case 'view-css':
                case 'view-js':
                    var queryNode = child(endpointNode(p[0]), 'q:' + p[0] + '@' + p[1], { label: p[1], kind: 'query', docId: p[0] + '@' + p[1] });
                    var viewNode = child(queryNode, 'v:' + p.slice(0, 3).join('@'), { label: p[2], kind: 'view', docId: p.slice(0, 3).join('@') });
                    node = c.kind === 'view' ? viewNode : child(viewNode, 'item:' + c.id, { label: c.kind === 'view-css' ? 'CSS' : 'JavaScript', kind: c.kind, docId: c.id });
                    break;
                case 'errorpage':
                    node = child(child(root, 'group:errorpages', { label: 'Error pages', kind: 'group' }), 'item:' + c.id, { label: c.id.substring('_errorpage@'.length), kind: 'errorpage' });
                    break;
                case 'folders':
                    node = child(root, 'item:' + c.id, { label: 'Tree folders', kind: 'folders' });
                    break;
                default:
                    node = child(child(root, 'group:files', { label: 'Repository files', kind: 'group' }), 'item:' + c.id, { label: c.id.replace(/^_file@/, ''), kind: 'file' });
                    break;
            }

            node.change = c;
        });

        return root;
    };

    var descendantChanges = function (node) {
        var result = node.change ? [node.change] : [];
        $.each(node.order, function (i, key) {
            result = result.concat(descendantChanges(node.children[key]));
        });
        return result;
    };

    var sortKeys = function (node) {
        var rank = { group: 3, folders: 2 };
        return node.order.slice().sort(function (x, y) {
            var a = node.children[x], b = node.children[y];
            var ra = rank[a.kind] || (a.kind === 'folder' ? 0 : 1), rb = rank[b.kind] || (b.kind === 'folder' ? 0 : 1);
            if (ra !== rb) return ra - rb;
            var cssJs = function (n) { return /-(css|js)$/.test(n.kind) ? 0 : 1; };
            if (cssJs(a) !== cssJs(b)) return cssJs(a) - cssJs(b);
            return a.label.localeCompare(b.label);
        });
    };

    var renderChanges = function () {
        $changes.empty();
        $changesHeader.empty();

        var changes = status.changes || [];
        var merging = !!status.merging;

        $commitBox.toggle(!merging);
        $empty.text(changes.length === 0 ? 'No changes. Everything is committed.' : '').toggle(changes.length === 0);

        if (changes.length === 0) {
            return;
        }

        var committable = changes.filter(function (c) { return c.status !== 'conflicted'; });
        var selected = committable.filter(function (c) { return !unchecked[c.id]; }).length;

        if (!merging) {
            var $all = $("<input type='checkbox' class='git-check'>")
                .prop('checked', selected === committable.length && committable.length > 0)
                .prop('indeterminate', selected > 0 && selected < committable.length)
                .attr('title', 'Select all')
                .appendTo($changesHeader)
                .on('change', function () {
                    var check = $(this).is(':checked');
                    $.each(committable, function (i, c) { if (check) delete unchecked[c.id]; else unchecked[c.id] = true; });
                    renderChanges();
                });
        }
        $("<span class='title'>").text(merging ? 'Merge changes' : 'Changes').appendTo($changesHeader);
        $("<span class='count'>").text(merging ? changes.length : selected + ' / ' + changes.length).appendTo($changesHeader);

        var tree = buildTree(changes);
        $.each(sortKeys(tree), function (i, key) {
            renderNode(tree.children[key], $changes, 0, merging);
        });
    };

    var renderNode = function (node, $parent, depth, merging) {
        var all = descendantChanges(node);
        var committable = all.filter(function (c) { return c.status !== 'conflicted'; });
        var selected = committable.filter(function (c) { return !unchecked[c.id]; }).length;
        var hasChildren = node.order.length > 0;
        var change = node.change;
        var nodeKey = node.key;

        var $li = $("<li class='git-node'>")
            .addClass('kind-' + node.kind)
            .toggleClass('has-children', hasChildren)
            .toggleClass('collapsed', !!collapsed[nodeKey])
            .appendTo($parent);

        var $row = $("<div class='git-row'>")
            .css('padding-left', (depth * 14 + 4) + 'px')
            .toggleClass('has-change', !!change)
            .appendTo($li);

        if (change) {
            $row.addClass('status-' + change.status);
        } else if (all.some(function (c) { return c.status === 'conflicted'; })) {
            $row.addClass('contains-conflict');
        }

        $("<span class='git-caret'>").appendTo($row).click(function (e) {
            e.stopPropagation();
            if (!hasChildren) return;
            collapsed[nodeKey] = !collapsed[nodeKey];
            $li.toggleClass('collapsed', !!collapsed[nodeKey]);
        });

        if (!merging && committable.length > 0) {
            $("<input type='checkbox' class='git-check'>")
                .prop('checked', selected === committable.length)
                .prop('indeterminate', selected > 0 && selected < committable.length)
                .appendTo($row)
                .on('click', function (e) { e.stopPropagation(); })
                .on('change', function () {
                    var check = $(this).is(':checked');
                    $.each(committable, function (i, c) { if (check) delete unchecked[c.id]; else unchecked[c.id] = true; });
                    renderChanges();
                });
        }

        $("<span class='git-icon'>").appendTo($row);
        $("<span class='git-label'>").text(node.label).attr('title', change ? change.id + ' (' + change.status + ')' : node.label).appendTo($row);

        if (!change && all.length > 0) {
            $("<span class='git-count'>").text(all.length).appendTo($row);
        }

        if (change) {
            if (change.status === 'conflicted') {
                var l = labels();
                $("<span class='git-resolve ours'>").text('mine').attr('title', l.ours).appendTo($row)
                    .click(function (e) { e.stopPropagation(); resolve(change.id, 'ours'); });
                $("<span class='git-resolve theirs'>").text('theirs').attr('title', l.theirs).appendTo($row)
                    .click(function (e) { e.stopPropagation(); resolve(change.id, 'theirs'); });
            }
            $("<span class='git-status-badge'>")
                .text({ added: 'A', modified: 'M', deleted: 'D', conflicted: 'C' }[change.status] || '?')
                .attr('title', change.status)
                .appendTo($row);
        }

        $row.click(function (e) {
            e.stopPropagation();
            if (change) {
                showDiff(change);
            } else if (hasChildren) {
                collapsed[nodeKey] = !collapsed[nodeKey];
                $li.toggleClass('collapsed', !!collapsed[nodeKey]);
            }
        }).on('dblclick', function (e) {
            e.stopPropagation();
            openDocument(node);
        }).on('contextmenu', function (e) {
            e.preventDefault();
            e.stopPropagation();
            showNodeMenu(e, node, all, merging);
        });

        if (hasChildren) {
            var $ul = $("<ul class='git-children'>").appendTo($li);
            $.each(sortKeys(node), function (i, key) {
                renderNode(node.children[key], $ul, depth + 1, merging);
            });
        }
    };

    var canOpen = function (node) {
        return !!node.docId && !(node.change && node.change.status === 'deleted');
    };

    var openDocument = function (node) {
        if (!canOpen(node)) {
            return;
        }
        var p = node.docId.split('@');
        switch (node.kind) {
            case 'endpoint': dataLinqCode.events.fire('open-endpoint', { endpoint: p[0] }); break;
            case 'endpoint-css': dataLinqCode.events.fire('open-endpoint-css', { id: p[0] }); break;
            case 'endpoint-js': dataLinqCode.events.fire('open-endpoint-js', { id: p[0] }); break;
            case 'query': dataLinqCode.events.fire('open-query', { endpoint: p[0], query: p[1] }); break;
            case 'view': dataLinqCode.events.fire('open-view', { endpoint: p[0], query: p[1], view: p[2] }); break;
            case 'view-css': dataLinqCode.events.fire('open-view-css', { endpoint: p[0], query: p[1], view: p[2] }); break;
            case 'view-js': dataLinqCode.events.fire('open-view-js', { endpoint: p[0], query: p[1], view: p[2] }); break;
        }
    };

    var showNodeMenu = function (e, node, all, merging) {
        var change = node.change;
        var discardable = all.filter(function (c) { return c.status !== 'conflicted'; }).map(function (c) { return c.id; });
        var l = labels();

        dataLinqCode.ui.contextMenu(e, [
            change ? { text: change.status === 'conflicted' ? 'Show conflict' : 'Show changes', action: function () { showDiff(change); } } : null,
            canOpen(node) ? { text: 'Open', action: function () { openDocument(node); } } : null,
            change && change.status === 'conflicted' ? '-' : null,
            change && change.status === 'conflicted' ? { text: l.ours, action: function () { resolve(change.id, 'ours'); } } : null,
            change && change.status === 'conflicted' ? { text: l.theirs, action: function () { resolve(change.id, 'theirs'); } } : null,
            '-',
            node.docId || change ? { text: 'History', action: function () { dataLinqCode.ui.git.history(change ? change.id : node.docId); } } : null,
            { text: 'Copy ID', action: function () { navigator.clipboard && navigator.clipboard.writeText(change ? change.id : (node.docId || node.label)); } },
            '-',
            !merging && discardable.length > 0
                ? { text: discardable.length === 1 ? 'Discard changes' : 'Discard ' + discardable.length + ' changes', danger: true, action: function () { discard(discardable); } }
                : null
        ]);
    };

    /****** events ******/

    dataLinqCode.events.on(['document-saved', 'document-deleted', 'tree-refreshed'], function () {
        scheduleRefresh();
    });

    // initial state
    if (localStorage.getItem(storageKey) === 'git') {
        setMode('git');
    } else {
        setMode('explorer');
        refresh(false, true);
    }
});
