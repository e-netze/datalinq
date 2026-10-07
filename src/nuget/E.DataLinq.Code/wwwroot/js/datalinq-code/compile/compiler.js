/**
 * DataLinq Code - compiler / checks (start page)
 *
 * Verifies all views, checks changed dates, loads entities; progress plugin dataLinqCode_compile_progress.
 * Depends on: dataLinqCode (own or parent window)
 */
window.dataLinqCode = window.dataLinqCode || window.parent.dataLinqCode;

/**
 * Shared results panel: summary chips, filter, search and a grouped, scrollable list.
 * Errors are pinned to the top, the running item is always visible.
 */
datalinqResultsPanel = function ($output, title, total, onItemClick) {
    var items = {};
    var counts = { ok: 0, error: 0 };
    var filter = 'all';
    var search = '';

    $output.empty().addClass('datalinq-results-host');

    var $panel = $("<div>").addClass('datalinq-results').appendTo($output);
    var $header = $("<div>").addClass('datalinq-results-header').appendTo($panel);

    var $titleRow = $("<div>").addClass('datalinq-results-title-row').appendTo($header);
    $("<div>").addClass('datalinq-results-title').text(title).appendTo($titleRow);
    var $state = $("<div>").addClass('datalinq-results-state running').text('Running…').appendTo($titleRow);

    var $bar = $("<div>").addClass('datalinq-results-bar').appendTo($header);
    var $barOk = $("<div>").addClass('ok').appendTo($bar);
    var $barErr = $("<div>").addClass('error').appendTo($bar);

    var $toolbar = $("<div>").addClass('datalinq-results-toolbar').appendTo($header);
    var $chips = $("<div>").addClass('datalinq-results-chips').appendTo($toolbar);

    var chip = function (key, label) {
        var $c = $("<button type='button'>").addClass('datalinq-results-chip ' + key).attr('data-filter', key)
            .append($("<span>").addClass('label').text(label))
            .append($("<span>").addClass('count').text('0'))
            .appendTo($chips)
            .on('click', function () {
                filter = key;
                $chips.children().removeClass('active');
                $(this).addClass('active');
                applyFilter();
            });
        return $c;
    };
    var $chipAll = chip('all', 'All').addClass('active');
    var $chipErr = chip('error', 'Errors');
    var $chipOk = chip('ok', 'OK');
    var $chipPending = chip('pending', 'Pending');

    $("<input type='search' placeholder='Filter by name…'>").addClass('datalinq-results-search')
        .appendTo($toolbar)
        .on('input', function () {
            search = ($(this).val() || '').toLowerCase();
            applyFilter();
        });

    var $list = $("<div>").addClass('datalinq-results-list').appendTo($panel);
    var $groupError = $("<div>").addClass('datalinq-results-group error').appendTo($list);
    var $groupOk = $("<div>").addClass('datalinq-results-group ok').appendTo($list);
    var $groupRunning = $("<div>").addClass('datalinq-results-group running').appendTo($list);
    var $empty = $("<div>").addClass('datalinq-results-empty').text('No matching items').hide().appendTo($list);

    var followTail = true;
    $list.on('scroll', function () {
        followTail = this.scrollHeight - this.scrollTop - this.clientHeight < 40;
    });
    var autoScroll = function () {
        if (followTail) {
            $list.scrollTop($list[0].scrollHeight);
        }
    };

    var refresh = function () {
        var done = counts.ok + counts.error;
        $chipAll.find('.count').text(total);
        $chipOk.find('.count').text(counts.ok);
        $chipErr.find('.count').text(counts.error);
        $chipPending.find('.count').text(Math.max(0, total - done));
        $barOk.css('width', (total ? counts.ok / total * 100 : 0) + '%');
        $barErr.css('width', (total ? counts.error / total * 100 : 0) + '%');
        $chipErr.toggleClass('has-items', counts.error > 0);
    };

    var matches = function ($item) {
        var status = $item.attr('data-status');
        if (filter === 'pending' && status !== 'pending') return false;
        if (filter === 'error' && status !== 'error') return false;
        if (filter === 'ok' && status !== 'ok') return false;
        if (search && $item.attr('data-id').toLowerCase().indexOf(search) < 0) return false;
        return true;
    };

    var applyFilter = function () {
        var visible = 0;
        $.each(items, function (id, $item) {
            var m = matches($item);
            $item.toggle(m);
            if (m) visible++;
        });
        $empty.toggle(visible === 0 && Object.keys(items).length > 0);
    };

    var renderName = function (id) {
        var $name = $("<div>").addClass('datalinq-results-name');
        $.each(id.split('@'), function (i, part) {
            if (i > 0) $("<span>").addClass('sep').text('›').appendTo($name);
            $("<span>").addClass('part part-' + i).text(part).appendTo($name);
        });
        return $name;
    };

    this.start = function (id) {
        var $item = $("<div>")
            .addClass('datalinq-results-item pending')
            .attr('data-id', id)
            .attr('data-status', 'pending')
            .append($("<span>").addClass('icon'))
            .append(renderName(id))
            .append($("<span>").addClass('badge').text('Running'))
            .appendTo($groupRunning);

        if (onItemClick) {
            $item.addClass('clickable').attr('title', 'Open ' + id).on('click', function (e) {
                if ($(e.target).closest('.datalinq-results-details').length) return;
                onItemClick(id);
            });
        }

        items[id] = $item;
        $item.toggle(matches($item));
        refresh();
        autoScroll();
    };

    this.success = function (id, label) {
        var $item = items[id];
        if (!$item) return;
        counts.ok++;
        $item.removeClass('pending').addClass('ok').attr('data-status', 'ok');
        $item.find('.badge').text(label || 'OK');
        $item.appendTo($groupOk);
        $item.toggle(matches($item));
        refresh();
    };

    this.error = function (id, label, messages) {
        var $item = items[id];
        if (!$item) return;
        counts.error++;
        $item.removeClass('pending').addClass('error').attr('data-status', 'error');
        $item.find('.badge').text(label || 'Error');

        if (messages && messages.length) {
            var $details = $("<ul>").addClass('datalinq-results-details');
            $.each(messages, function (i, msg) {
                $("<li>").addClass(msg.warning ? 'warning' : 'error')
                    .append($("<span>").addClass('level').text(msg.warning ? 'WARNING' : 'ERROR'))
                    .append($("<span>").addClass('text').text(msg.text))
                    .appendTo($details);
            });
            $item.append($details);
        }

        $item.appendTo($groupError);
        $item.toggle(matches($item));
        refresh();
    };

    this.finish = function () {
        $state.removeClass('running')
            .addClass(counts.error > 0 ? 'error' : 'ok')
            .text(counts.error > 0
                ? counts.error + ' of ' + total + ' failed'
                : 'All ' + total + ' passed');
        if (counts.error > 0) {
            $chipErr.trigger('click');
        }
    };

    refresh();
};

var datalinqOpenDocument = function (id) {
    var ids = id.split('@');
    if (ids.length === 2)
        dataLinqCode.events.fire('open-query', { endpoint: ids[0], query: ids[1] });
    else if (ids.length === 3)
        dataLinqCode.events.fire('open-view', { endpoint: ids[0], query: ids[1], view: ids[2] });
};

datalinqChangedDateChecker = function () {
    this.transmitter = {};
    dataLinqCode.implementEventController(this.transmitter);

    let documents = dataLinqCode.allDocuments();

    let viewDocuments = [];
    $.each(documents, function (i, doc) {
        var ids = doc.id.split('@');
        if (ids.length === 3) {
            viewDocuments.push({
                endpoint: ids[0],
                query: ids[1],
                view: ids[2],
            });
        }
        if (ids.length === 2) {
            viewDocuments.push({
                endpoint: ids[0],
                query: ids[1],
                view: "_isQuery",
            });
        }
    });

    let me = this;
    let viewIndex = 0;

    var checkNext = function () {
        if (viewIndex >= viewDocuments.length) {
            me.transmitter.events.fire('finished-progress');
            return;
        }

        let viewDocument = viewDocuments[viewIndex];
        var id = viewDocument.view == "" ? viewDocument.endpoint + '@' + viewDocument.query : viewDocument.endpoint + '@' + viewDocument.query + '@' + viewDocument.view;
        me.transmitter.events.fire('start-check', {
            id: id.replace(/@_isQuery$/, '')
        });

        dataLinqCode.api.checkGitStatus(viewDocument.endpoint, viewDocument.query, viewDocument.view, function (result) {

            me.transmitter.events.fire('check-finished', {
                id: id.replace(/@_isQuery$/, ''),
                result: result.Success
            });

            viewIndex++;
            checkNext();
        });
    };

    this.run = function ($output) {
        var panel = new datalinqResultsPanel($output, 'Snapshot Status', viewDocuments.length, datalinqOpenDocument);

        this.transmitter.events.on('start-check', function (channel, args) {
            panel.start(args.id);
            me.transmitter.events.fire('progress-change', { pos: viewIndex, text: args.id });
        });

        this.transmitter.events.on('check-finished', function (channel, args) {
            if (args.result === true) {
                panel.success(args.id, 'Up to date');
            } else {
                panel.error(args.id, 'Outdated');
            }

            me.transmitter.events.fire('progress-change', { pos: viewIndex, text: '' });
        });

        this.transmitter.events.on('finished-progress', function () {
            panel.finish();
        });

        this.transmitter.events.fire('start-progress', { max: viewDocuments.length });

        viewIndex = 0;
        checkNext();
    };
};

datalinqCodeCompiler = function () {

    this.transmitter = {};
    dataLinqCode.implementEventController(this.transmitter);

    let documents = dataLinqCode.allDocuments();

    let viewDocuments = [];
    $.each(documents, function (i, doc) {
        var ids = doc.id.split('@');
        if (ids.length === 3) {
            viewDocuments.push({
                endpoint: ids[0],
                query: ids[1],
                view: ids[2]
            });
        }
    });

    let me = this;

    let viewIndex = 0;
    var verifyNext = function () {
        if (viewIndex >= viewDocuments.length) {
            me.transmitter.events.fire('finished-progress');
            return;
        }

        let viewDocument = viewDocuments[viewIndex];
        me.transmitter.events.fire('start-compile', { id: viewDocument.endpoint + '@' + viewDocument.query + '@' + viewDocument.view });

        //viewIndex++;
        //verifyNext();

        dataLinqCode.api.verifyView(viewDocument.endpoint, viewDocument.query, viewDocument.view, function (result) {
            //console.log(viewDocument, result);

            me.transmitter.events.fire('compile-finished', { id: viewDocument.endpoint + '@' + viewDocument.query + '@' + viewDocument.view, result: result });

            viewIndex++;
            verifyNext();
        });
    }

    this.run = function ($output) {
        var panel = new datalinqResultsPanel($output, 'Verify All Views', viewDocuments.length, datalinqOpenDocument);

        this.transmitter.events.on('start-compile', function (channel, args) {
            panel.start(args.id);
            me.transmitter.events.fire('progress-change', { pos: viewIndex, text: args.id });
        });

        this.transmitter.events.on('compile-finished', function (channel, args) {
            if (args.result.success === true) {
                panel.success(args.id, 'Compiled');
            } else {
                var messages = $.map(args.result.compiler_errors || [], function (error) {
                    return { warning: error.is_warning, text: error.error_text };
                });
                panel.error(args.id, 'Failed', messages);
            }

            me.transmitter.events.fire('progress-change', { pos: viewIndex, text: '' });
        });

        this.transmitter.events.on('finished-progress', function () {
            panel.finish();
        });

        this.transmitter.events.fire('start-progress', { max: viewDocuments.length });

        viewIndex = 0;
        verifyNext();
    };
};

datalinqEntityLoader = function (rewrite) {
    this.transmitter = {};
    dataLinqCode.implementEventController(this.transmitter);

    let documents = dataLinqCode.allDocuments();
    let endpointDocuments = [], queryDocuments = [], viewDocuments = [];
    let progress = 0;

    $.each(documents, function (i, doc) {
        var ids = doc.id.split('@');
        if (ids.length === 1) {
            endpointDocuments.push({ endpoint: ids[0] });
        }
        else if (ids.length === 2) {
            queryDocuments.push({ endpoint: ids[0], query: ids[1] });
        }
        else if (ids.length === 3) {
            viewDocuments.push({ endpoint: ids[0], query: ids[1], view: ids[2] });
        }
    });

    let me = this;

    verifyNextDocument = function (documents, index) {
        index = index || 0;
        if (index >= documents.length) {
            return;
        }

        let doc = documents[index];

        let docId = doc.endpoint;
        if (doc.query) docId += '@' + doc.query;
        if (doc.view) docId += '@' + doc.view;
        
        me.transmitter.events.fire('start-load', { id: docId });

        dataLinqCode.api.docInfo(doc.endpoint, doc.query, doc.view, rewrite, function (result) {
            //console.log(viewDocument, result);

            me.transmitter.events.fire('load-finished', { id: docId, result: result });

            index++;
            verifyNextDocument(documents, index++);
        });
    }

    this.run = function ($output) {
        var total = endpointDocuments.length + queryDocuments.length + viewDocuments.length;
        var progress = 0, finished = 0;

        var panel = new datalinqResultsPanel($output, 'Try Load All Documents', total, datalinqOpenDocument);

        this.transmitter.events.on('start-load', function (channel, args) {
            panel.start(args.id);
            me.transmitter.events.fire('progress-change', { pos: progress++, text: args.id });
        });

        this.transmitter.events.on('load-finished', function (channel, args) {
            if (args.result.success === true) {
                panel.success(args.id, 'Loaded');
            } else {
                panel.error(args.id, 'Failed', args.result.error_message
                    ? [{ warning: false, text: args.result.error_message }]
                    : []);
            }

            finished++;
            me.transmitter.events.fire('progress-change', { pos: progress, text: '' });

            if (finished === total) {
                me.transmitter.events.fire('finished-progress');
                panel.finish();
            }
        });

        this.transmitter.events.fire('start-progress', { max: total });

        if (total === 0) {
            me.transmitter.events.fire('finished-progress');
            panel.finish();
            return;
        }

        verifyNextDocument(endpointDocuments);
        verifyNextDocument(queryDocuments);
        verifyNextDocument(viewDocuments);
    };
};

(function ($) {
    "use strict";
    $.fn.dataLinqCode_compile_progress = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_compile_progress');
        }
    };
    var defaults = {
        transmitter: null
    };
    var methods = {
        init: function (options) {
            var settings = $.extend({}, defaults, options);
            return this.each(function () {
                new initUI(this, settings);
            });
        }
    };
    var initUI = function (parent, options) {
        var $parent = $(parent).addClass('datalinq-code-progress').empty();

        var $progressbar = $("<div>")
            .addClass('progressbar')
            .data('max', 100)
            .data('pos', 0)
            .appendTo($("<div>").addClass('progressbar-holder').appendTo($parent));

        var $progressText = $("<div>")
            .addClass('progresstext')
            .appendTo($parent);

        options.transmitter.events.on('start-progress', function (channel, args) {
            $progressbar
                .data('pos', 0)
                .data('max', args.max)
                .css('width', '0%');
        });

        options.transmitter.events.on('finished-progress', function () {
            $parent.empty();
        });

        options.transmitter.events.on('progress-change', function (channel, args) {
            $progressbar.data('pos', args.pos);
            var percentage = parseFloat($progressbar.data('pos')) / parseFloat($progressbar.data('max')) * 100.0;
            $progressbar.css('width', +percentage + '%');

            $progressText.text(args.text);
        });
    }; 
})(jQuery);