window.dataLinqCode = window.dataLinqCode || window.parent.dataLinqCode;

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
        $output.empty();

        this.transmitter.events.on('start-compile', function (channel, args) {
            $("<div>")
                .addClass('datalinq-code-compile-item')
                .attr('data-id', args.id)
                .text(args.id)
                .appendTo($output)
                .click(function () {
                    var ids = $(this).attr('data-id').split('@');
                    dataLinqCode.events.fire('open-view', {
                        endpoint: ids[0],
                        query: ids[1],
                        view: ids[2]
                    });
                });

            me.transmitter.events.fire('progress-change', { pos: viewIndex, text: args.id });
        });

        this.transmitter.events.on('compile-finished', function (channel, args) {
            var $item = $output.children(".datalinq-code-compile-item[data-id='" + args.id + "']");
            if (args.result.success === true) {
                $item.addClass('success');
            } else {
                $item.addClass('has-errors');
                console.log('has-errors', args);
                if (args.result.compiler_errors && args.result.compiler_errors.length > 0) {
                    var $ul = $("<ul>").appendTo($item);
                    $.each(args.result.compiler_errors, function (i, error) {
                        $("<li>")
                            .text((error.is_warning ? "WARNING" : "ERROR") + " " + error.error_text)
                            .appendTo($ul);
                    });
                }
            }

            me.transmitter.events.fire('progress-change', { pos: viewIndex, text: '' });
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
        $output.empty();

        this.transmitter.events.on('start-load', function (channel, args) {
            $("<div>")
                .addClass('datalinq-code-compile-item')
                .attr('data-id', args.id)
                .text(args.id)
                .appendTo($output);
            //.click(function () {
            //    var ids = $(this).attr('data-id').split('@');
            //    dataLinqCode.events.fire('open-view', {
            //        endpoint: ids[0],
            //        query: ids[1],
            //        view: ids[2]
            //    });
            //});

            me.transmitter.events.fire('progress-change', { pos: progress++, text: args.id });
        });

        this.transmitter.events.on('load-finished', function (channel, args) {
            var $item = $output.children(".datalinq-code-compile-item[data-id='" + args.id + "']");
            if (args.result.success === true) {
                $item.addClass('success');
            } else {
                $item.addClass('has-errors');
                console.log('has-errors', args);
                if (args.result.error_message) {
                    var $ul = $("<ul>").appendTo($item);
                    $("<li>")
                        .text("ERROR: " + args.result.error_message)
                        .appendTo($ul);
                }
            }

            me.transmitter.events.fire('progress-change', { pos: progress, text: '' });

            if (progress === endpointDocuments.length + queryDocuments.length + viewDocuments.length) {
                me.transmitter.events.fire('finished-progress');
            }
        });

        this.transmitter.events.fire('start-progress', { max: endpointDocuments.length + queryDocuments.length + viewDocuments.length });

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