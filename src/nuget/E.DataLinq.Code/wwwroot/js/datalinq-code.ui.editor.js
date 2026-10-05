(function ($) {
    "use strict";
    $.fn.dataLinqCode_editor = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_editor');
        }
    };
    var defaults = {
        
    };
    var methods = {
        init: function (options) {
            var settings = $.extend({}, defaults, options);
            return this.each(function () {
                new initUI(this, settings);
            });
        },
        currentDoc: function (options) {
            var $tabs = $(this).children('.datalinq-code-tabs')

            return $tabs.children(".datalinq-code-tab.selected").attr('data-id');
        },
        toSaveDoc: function (options) {
            var $tabs = $(this).children('.datalinq-code-tabs')

            return $tabs.children(".datalinq-code-tab.selected").map(function () {
                return $(this).attr('data-id');
            }).get().filter(function (id) {
                return id && id.indexOf('_') !== 0;
            });
        },
        dirtyDocs: function (options) {
            var ids = [];
            $(this).children('.datalinq-code-tabs').children(".datalinq-code-tab.dirty").each(function (i, e) {
                ids.push($(e).attr('data-id'));
            });
            return ids;
        },
        addTab: function (options) {
            showOrAddTab($(this).children('.datalinq-code-tabs'), options.title, options.id, options.className, options.hideCloseButton)
        },
        isOpen: function (options) {
            return $(this).children('.datalinq-code-tabs').children(".datalinq-code-tab[data-id='" + options.id + "']").length > 0;
        }
    };
    var initUI = function (parent, options) {
        var $parent = $(parent);

        var $tabs = $("<div>")
            .addClass('datalinq-code-tabs')
            .appendTo($parent);
        $("<div>")
            .addClass('datalinq-code-tab-selector')
            .appendTo($tabs)
            .click(function () {
                $('body').dataLinq_code_modal({
                    title: 'Open tabs...',
                    onload: function ($content) {
                        renderOpenTabs($tabs, $content);
                    }
                });
            });

        var $editor = $("<div>")
            .addClass('datalinq-code-editor')
            .appendTo($parent);

        dataLinqCode.events.on('open-endpoint', function (channel, args) {
            var $tab = showOrAddTab($tabs, args.endpoint, args.endpoint, 'endpoint');
        });
        dataLinqCode.events.on('open-query', function (channel, args) {
            var $tab = showOrAddTab($tabs, args.query, args.endpoint + '@' + args.query, 'query');
        });
        dataLinqCode.events.on('open-view', function (channel, args) {
            var $tab = showOrAddTab($tabs, args.view, args.endpoint + '@' + args.query + '@' + args.view, 'view');
        });
        dataLinqCode.events.on('open-view-css', function (channel, args) {
            var $tab = showOrAddTab($tabs, args.view, args.endpoint + '@' + args.query + '@' + args.view + '@_css' , 'viewcss');
        });
        dataLinqCode.events.on('open-view-js', function (channel, args) {
            var $tab = showOrAddTab($tabs, args.view, args.endpoint + '@' + args.query + '@' + args.view + '@_js', 'viewjs');
        });
        dataLinqCode.events.on('open-endpoint-css', function (channel, args) {
            var $tab = showOrAddTab($tabs, 'CSS: ' + args.id, args.id + '@_css', 'css');
        });
        dataLinqCode.events.on('open-endpoint-js', function (channel, args) {
            var $tab = showOrAddTab($tabs, 'Javascript: ' + args.id, args.id + '@_js', 'js');
        });
        dataLinqCode.events.on('open-copilot', function (channel, args) {
            var $tab = showOrAddTab($tabs, 'DataLinq Copilot', 'copilot');
        });
        var closePreview = function (viewId) {
            var previewId = previewPrefix + viewId;
            delete previewUrls[previewId];
            delete previewScrolls[previewId];
            $editor.children(".datalinq-code-editor-frame[data-id='" + previewId + "']").remove();
            $tabs.children(".datalinq-code-tab[data-id='" + viewId + "']")
                .removeClass('with-preview')
                .children('.preview-indicator').remove();
        };

        dataLinqCode.events.on('open-view-preview', function (channel, args) {
            var previewId = previewPrefix + args.id;
            var $viewTab = $tabs.children(".datalinq-code-tab[data-id='" + args.id + "']");
            if ($viewTab.length === 0) {
                return;
            }

            var exists = !!previewUrls[previewId];
            var prevCtrl = ctrlPressed;

            if (exists) {
                closePreview(args.id);
                ctrlPressed = false;
                $viewTab.trigger('click');
                ctrlPressed = prevCtrl;
                return;
            }

            previewUrls[previewId] = args.url;
            $viewTab.addClass('with-preview');
            if ($viewTab.children('.preview-indicator').length === 0) {
                $("<span>")
                    .addClass('preview-indicator')
                    .attr('title', 'Live Preview')
                    .text('Live')
                    .insertBefore($viewTab.children('.close-button'));
            }

            ctrlPressed = false;
            $viewTab.trigger('click');
            ctrlPressed = prevCtrl;
        });
        dataLinqCode.events.on('document-saved', function (channel, args) {
            if (!args || typeof args.id !== 'string') {
                return;
            }

            var parts = args.id.split('@');
            if (parts.length === 4 && (parts[3] === '_css' || parts[3] === '_js')) {
                parts.pop();
            }
            if (parts.length !== 3) {
                return;
            }

            var viewId = parts.join('@');
            var previewId = previewPrefix + viewId;
            if (!previewUrls[previewId]) {
                return;
            }

            previewUrls[previewId] = dataLinqCode.buildRunUrl(viewId);
            reloadPreviewFrame(previewId);
        });
        dataLinqCode.events.on('tab-selected', function (channel, args) {
            showOrAddEditorFrame($editor, args.id);

            checkSize($tabs);
            dataLinqCode.events.fire('refresh-ui');
        });
        dataLinqCode.events.on('tab-removed', function (channel, args) {
            delete previewUrls[args.id];
            delete previewScrolls[args.id];
            dataLinqCode.events.fire('destroy-editor', { id: args.id });
            $(".datalinq-code-editor-frame[data-id='" + args.id + "']").remove();

            // closing a view also closes its live preview
            if (args.id.split('@').length === 3) {
                closePreview(args.id);
            }

            var remainingFrames = getOrderedSelectedFrames($tabs, $editor);
            if (remainingFrames.length > 0) {
                $editor.children('.datalinq-code-editor-frame').removeClass('selected');
                $(remainingFrames).addClass('selected');
                layoutFrames($editor, remainingFrames);
            } else {
                $tabs.children('.datalinq-code-tab').last().trigger('click');
            }

            checkSize($tabs);
            dataLinqCode.events.fire('refresh-ui');
        });

        dataLinqCode.events.on('document-changed', function (channel, args) {
            $tabs.children(".datalinq-code-tab[data-id='" + args.id + "']")
                .addClass('dirty');

            dataLinqCode.events.fire('refresh-ui');
        });

        dataLinqCode.events.on(['verify-document'], function (channel, args) {
            $tabs.children(".datalinq-code-tab[data-id='" + args.id + "']")
                .addClass('loading');
        });

        dataLinqCode.events.on(['save-document'], function (channel, args) {
            var ids = Array.isArray(args.id) ? args.id : [args.id];

            $tabs.children(".datalinq-code-tab").filter(function () {
                return ids.includes($(this).attr("data-id"));
            }).addClass('loading');
        });

        dataLinqCode.events.on(['document-saved', 'document-verified'], function (channel, args) {
            $tabs.children(".datalinq-code-tab[data-id='" + args.id + "']")
                .removeClass('loading')
                .removeClass('errors');

            if (channel.channel === 'document-saved') {
                $tabs.children(".datalinq-code-tab[data-id='" + args.id + "']")
                    .removeClass('dirty');
            }

            dataLinqCode.events.fire('refresh-ui');
        });
        dataLinqCode.events.on('document-errors', function (channel, args) {
            $tabs.children(".datalinq-code-tab[data-id='" + args.id + "']").removeClass('loading').addClass('errors');
        });

        dataLinqCode.events.on('ide-resize', function (channel, args) {
            checkSize($tabs);
        });

        dataLinqCode.events.on('document-deleted', function (channel, args) {
            $parent.children('.datalinq-code-tabs').children('.datalinq-code-tab').each(function (i, tab) {
                var $tab = $(tab);
                var id = $tab.attr('data-id');
                if (id === args.id || id.indexOf(args.id + '@') === 0) {
                    var selected = $tab.hasClass('selected');
                    $tab.remove();
                    dataLinqCode.events.fire('tab-removed', { id: id, selected: selected });
                };
            });
            $(".datalinq-code-editor-frame[data-id='" + args.id + "']").remove();

            dataLinqCode.events.fire('refresh-ui');
        });

        var closeSavedTabs = function ($candidates) {
            $candidates.each(function (i, tab) {
                var $tab = $(tab);

                // tabs without close button (start) and tabs with unsaved changes stay open
                if ($tab.children('.close-button').length === 0 || $tab.hasClass('dirty')) {
                    return;
                }

                var id = $tab.attr('data-id');
                var selected = $tab.hasClass('selected');
                $tab.remove();

                dataLinqCode.events.fire('tab-removed', { id: id, selected: selected });
            });

            checkSize($tabs);
        };

        $tabs.on('contextmenu', function (e) {
            if (!dataLinqCode.ui.contextMenu) {
                return;
            }

            e.preventDefault();

            var $allTabs = $tabs.children('.datalinq-code-tab');
            var $tab = $(e.target).closest('.datalinq-code-tab');
            var canClose = $tab.length > 0 && $tab.children('.close-button').length > 0;

            dataLinqCode.ui.contextMenu(e, [
                canClose ? { text: 'Close', action: function () { $tab.children('.close-button').trigger('click'); } } : null,
                $tab.length > 0 ? { text: 'Close others', action: function () { closeSavedTabs($allTabs.not($tab)); } } : null,
                $tab.length > 0 ? { text: 'Close to the right', action: function () { closeSavedTabs($tab.nextAll('.datalinq-code-tab')); } } : null,
                '-',
                { text: 'Close all', action: function () { closeSavedTabs($allTabs); } }
            ]);
        });

        var el = document.querySelector('.datalinq-code-tabs');
        var sortable = Sortable.create(el, {
            animation: 150,
            ghostClass: 'dragging',
            filter: '.start',
            onMove: function (evt) {
                return !evt.related.classList.contains('start');
            }
        });
    };

    var showOrAddTab = function ($tabs, title, id, cls, hideCloseButton) {
        var $tab = $tabs.children(".datalinq-code-tab[data-id='" + id + "']");
        if ($tab.length === 0) {
            $tab = $("<div>")
                .addClass('datalinq-code-tab')
                .attr('data-id', id)
                .attr('title', id)
                .text(title)
                .appendTo($tabs)

            if (cls) {
                $tab.addClass(cls);
            }

            if (!hideCloseButton) {
                $("<div>")
                    .addClass('close-button')
                    .appendTo($tab)
                    .click(function (e) {
                        e.stopPropagation();

                        var $tab = $(this).parent();
                        var id = $tab.attr('data-id');

                        dataLinqCode.ui.confirmIf(
                            $tab.hasClass('dirty'),
                            id,
                            "Close tab without saving? You will loose all changes!",
                            function () {
                                var selected = $tab.hasClass('selected');
                                $tab.remove();

                                dataLinqCode.events.fire('tab-removed', { id: id, selected: selected });
                            }
                        )
                    });
            }
        }

        $tab.off('click.dlc').on('click.dlc', function (e) {
            e.stopPropagation();

            const $clicked = $(this);
            const isSelected = $clicked.hasClass('selected');
            const $selectedTabs = $tabs.children(".datalinq-code-tab.selected");
            const selectedCount = $selectedTabs.length;

            if (ctrlPressed) {
                if (isSelected) {
                    $clicked.removeClass('selected');
                    $clicked.removeAttr('data-selected-at');
                } else if (selectedCount < 3) {
                    $clicked.addClass('selected');
                    $clicked.attr('data-selected-at', Date.now());
                } else if (selectedCount >= 3) {
                    dataLinqCode.ui.alert('Limit', 'You can only select up to 3 tabs.');
                    return;
                }
            } else {
                $tabs.children('.selected').removeClass('selected').removeAttr('data-selected-at');
                $clicked.addClass('selected');
                $clicked.attr('data-selected-at', Date.now());
            }

            const clickedId = $clicked.attr('data-id');

            const ide = $('.datalinq-code-ide');
            var editorTheme = ide.hasClass('colorscheme-light') ? 'vs' : 'vs-dark';
            sessionStorage.setItem('editorTheme', editorTheme);
            console.log(clickedId);
            console.log(editorTheme);

            dataLinqCode.events.fire('tab-selected', { id: $clicked.attr('data-id') });
        });

        var tabs = $tabs.children(".datalinq-code-tab");

        if (tabs.length > 1) {
            tabs.first().removeClass('selected');
        } else if (frames.length === 1) {
            tabs.first().addClass('selected');
        }

        $tab.trigger('click');

        checkSize($tabs);

        return $tab;
    };

    var renderOpenTabs = function ($tabs, $parent) {
        var $ul = $("<ul>")
            .addClass('datalinq-code-open-tabs')
            .appendTo($parent);

        $tabs.children('.datalinq-code-tab').each(function (i, tab) {
            var $tab = $(tab);

            var id = $tab.attr('data-id');

            var $li = $("<li>")
                .data("$tab", $tab)
                .attr('class', $tab.attr('class'))
                .click(function (e) {
                    e.stopPropagation();
                    $(this).data("$tab").trigger('click');
                    $(null).dataLinq_code_modal('close');
                })
                .appendTo($ul);

            $("<div>")
                .addClass('text')
                .text($tab.text())
                .appendTo($li);

            if (id.indexOf('_') != 0) {
                $("<div>")
                    .addClass('subtext')
                    .text(id)
                    .appendTo($li);
            }
        });
    };

    var checkSize = function ($tabs) {
        function check(skip) {
            var pos = 0, tabsWidth = $tabs.width();

            $tabs.children('.datalinq-code-tab').each(function (i, tab) {
                var $tab = $(tab).css('display', ''), tabWidth = $tab.outerWidth();

                if (i < skip) {
                    $tab.css('display', 'none');
                } else {
                    if (pos + tabWidth >= tabsWidth - 30) {
                        $tab.css('display', 'none');
                    } else {
                        pos += tabWidth;
                    }
                }
            });
        };

        var numTabs = $tabs.children('.datalinq-code-tab').length, skip = 0;
        var $selectedTab = $tabs.children('.datalinq-code-tab.selected');

        while (skip < numTabs) {
            check(skip);

            if ($selectedTab.length === 0 || $selectedTab.css('display') !== 'none') {
                break;
            }

            skip++;
        }
    };

    var ctrlPressed = false;

    $(document).keydown(function (e) {
        if (e.key === "Control") ctrlPressed = true;
    }).keyup(function (e) {
        if (e.key === "Control") ctrlPressed = false;
    });

    var showOrAddEditorFrame = function ($editor, id) {
        let $frame = $editor.children(`.datalinq-code-editor-frame[data-id='${id}']`);
        // Only create the iframe if it doesn't exist
        if ($frame.length === 0) {
            const src = buildFrameSrc(id);
            $frame = $("<iframe>")
                .addClass('datalinq-code-editor-frame')
                .attr('data-id', id)
                .attr('allow', 'clipboard-write')
                .attr('src', src)
                .toggleClass('preview', id.indexOf(previewPrefix) === 0)
                .appendTo($editor);

            // Attach the load event ONLY for new iframes
            $frame.on('load', function () {
                try {
                    var pdfCheckbox = this.contentDocument &&
                        this.contentDocument.querySelector("input[type='checkbox'][name='PDFReportMode']");
                    if (pdfCheckbox) {
                        pdfCheckbox.addEventListener('change', function () {
                            dataLinqCode.events.fire('refresh-ui');
                        });
                    }
                } catch (e) { }
                dataLinqCode.events.fire('refresh-ui');

                try {
                    const iframeWindow = this.contentWindow;
                    const theme = sessionStorage.getItem('editorTheme');
                    const doc = iframeWindow.document;

                    if (theme === 'vs') {
                        doc.body.classList.add('colorscheme-light');
                    } else {
                        doc.body.classList.remove('colorscheme-light');
                    }

                    iframeWindow.addEventListener('message', function (event) {
                        const data = event.data;
                        if (data && typeof data.theme === 'string') {
                            const doc = iframeWindow.document;
                            if (data.theme === 'vs') {
                                doc.body.classList.add('colorscheme-light');
                            } else {
                                doc.body.classList.remove('colorscheme-light');
                            }
                        }
                    });
                } catch (e) {
                    console.warn(`Could not access iframe content for [${id}] due to cross-origin policy. `);
                }
            });
        } else {
            // If iframe already exists, just update the theme if needed
            try {
                const iframeWindow = $frame[0].contentWindow;
                const theme = sessionStorage.getItem('editorTheme');
                const doc = iframeWindow.document;

                if (theme === 'vs') {
                    doc.body.classList.add('colorscheme-light');
                } else {
                    doc.body.classList.remove('colorscheme-light');
                }
            } catch (e) {
                console.warn(`Could not access iframe content for [${id}] due to cross-origin policy.`);
            }
        }

        const previewId = previewPrefix + id;
        if (previewUrls[previewId] && $editor.children(`.datalinq-code-editor-frame[data-id='${previewId}']`).length === 0) {
            $("<iframe>")
                .addClass('datalinq-code-editor-frame preview')
                .attr('data-id', previewId)
                .attr('allow', 'clipboard-write')
                .attr('src', previewUrls[previewId])
                .appendTo($editor);
        }

        const isSelected = $frame.hasClass('selected');
        const selectedCount = $editor.children(".datalinq-code-editor-frame.selected").length;

        if (ctrlPressed) {
            if (isSelected) {
                $frame.removeClass('selected');
            } else if (selectedCount < 3) {
                $frame.addClass('selected');
            }
        } else {
            $editor.children(".datalinq-code-editor-frame.selected").removeClass('selected');
            $frame.addClass('selected');
        }

        const $tabs = $('.datalinq-code-tabs');
        const selectedFrames = getOrderedSelectedFrames($tabs, $editor);
        selectedFrames.forEach(frame => {
            if (!$(frame).parent().is($editor)) {
                $(frame).appendTo($editor);
            }
        });

        layoutFrames($editor, selectedFrames);
    };

    var previewPrefix = '_preview:';
    var previewUrls = {};

    function reloadPreviewFrame(previewId) {
        var $frame = $(".datalinq-code-editor-frame[data-id='" + previewId + "']");
        if ($frame.length > 0 && previewUrls[previewId]) {
            $frame.attr('src', previewUrls[previewId]);
        }
    }

    var previewScrolls = {};

    var findPreviewIdBySource = function (source) {
        var result = null;
        $(".datalinq-code-editor-frame.preview").each(function () {
            if (this.contentWindow === source) {
                result = $(this).attr('data-id');
                return false;
            }
        });
        return result;
    };

    window.addEventListener('message', function (event) {
        var data = event.data;
        if (!data || (!data.datalinqPreviewScroll && !data.datalinqPreviewReady)) {
            return;
        }

        var previewId = findPreviewIdBySource(event.source);
        if (!previewId) {
            return;
        }

        if (data.datalinqPreviewScroll) {
            var s = data.datalinqPreviewScroll;
            if (typeof s.x !== 'number' || typeof s.y !== 'number' || (s.path !== undefined && typeof s.path !== 'string')) {
                return;
            }
            previewScrolls[previewId] = { path: s.path || '', x: s.x, y: s.y };
        } else if (data.datalinqPreviewReady) {
            var scroll = previewScrolls[previewId];
            if (scroll && (scroll.x || scroll.y)) {
                event.source.postMessage({ datalinqRestoreScroll: scroll }, event.origin);
            }
        }
    });

    function buildFrameSrc(id) {
        const base = dataLinqCode.targetUrl();
        const token = window._datalinqCodeAccessToken;

        if (id === '_start') return `${base}/Start`;

        if (id.indexOf(previewPrefix) === 0) {
            return previewUrls[id] || dataLinqCode.buildRunUrl(id.substring(previewPrefix.length));
        }

        const parts = id.split('@');
        const [endpoint, query, view, suffix] = parts;

        if (parts.length === 1) {
            if (parts[0] === 'copilot') {
                return `${base}/copilot?dl_token=${token}`;
            }
            return `${base}/EditEndPoint?endpoint=${endpoint}&dl_token=${token}`;
        }

        if (parts.length === 2) {
            if (query === '_css') return `${base}/EditEndPointCss?endpoint=${endpoint}&dl_token=${token}`;
            if (query === '_js') return `${base}/EditEndPointJavascript?endpoint=${endpoint}&dl_token=${token}`;
            return `${base}/EditEndPointQuery?endpoint=${endpoint}&query=${query}&dl_token=${token}`;
        }

        if (parts.length === 3) {
            return `${base}/EditEndPointQueryView?endpoint=${endpoint}&query=${query}&view=${view}&dl_token=${token}`;
        }

        if (parts.length === 4) {
            if (suffix === '_css') return `${base}/EditViewCss?endpoint=${endpoint}&query=${query}&view=${view}&dl_token=${token}`;
            if (suffix === '_js') return `${base}/EditViewJs?endpoint=${endpoint}&query=${query}&view=${view}&dl_token=${token}`;
        }

        dataLinqCode.ui.alert('Error', 'Unknown datalinq route/id: ' + id);
        return '';
    }

    function getOrderedSelectedFrames($tabs, $editor) {
        return $tabs.children(".datalinq-code-tab.selected")
            .sort((a, b) => +$(a).attr('data-selected-at') - +$(b).attr('data-selected-at'))
            .get()
            .flatMap(function (tab) {
                const id = $(tab).attr('data-id');
                const ids = $(tab).hasClass('with-preview') ? [id, previewPrefix + id] : [id];
                return ids.map(frameId => $editor.children(`.datalinq-code-editor-frame[data-id='${frameId}']`)[0]);
            }).filter(Boolean).slice(0, 3);
    }

    function layoutFrames($editor, frames) {
    $editor.find('.datalinq-frame-stack').remove();
    $editor.find('.datalinq-separator').remove();
    $editor.children(".datalinq-code-editor-frame").hide().css({ flex: '', width: '', height: '', display: 'none' });
    $editor.css({ display: 'flex', flexDirection: 'row', width: '100%', height: '100%' });

    const count = frames.length;
    $editor.toggleClass('split', count > 1);

    if (count === 1) {
        const $frame = $(frames[0]);
        // Detach without triggering reload, then reattach
        if ($frame.parent().length && ! $frame.parent().is($editor)) {
            $frame.detach().appendTo($editor);
        } else if (!$frame.parent().length) {
            $frame.appendTo($editor);
        }
        $frame.css({ flex: '1 1 100%', width:  '100%', height: '98%', display: 'block' }).show();
    } else if (count === 2) {
        const $frame0 = $(frames[0]);
        if ($frame0.parent().length && !$frame0.parent().is($editor)) {
            $frame0.detach().appendTo($editor);
        } else if (!$frame0.parent().length) {
            $frame0.appendTo($editor);
        }
        $frame0.css({ flex: '1 1 50%', width: '50%', height:  '98%', display: 'block' }).show();

        $('<div class="datalinq-separator vertical-separator"></div>').appendTo($editor);

        const $frame1 = $(frames[1]);
        if ($frame1.parent().length && !$frame1.parent().is($editor)) {
            $frame1.detach().appendTo($editor);
        } else if (!$frame1.parent().length) {
            $frame1.appendTo($editor);
        }
        $frame1.css({ flex: '1 1 50%', width:  '50%', height: '98%', display: 'block' }).show();
    } else if (count === 3) {
        const [$left, $topRight, $bottomRight] = frames;

        const $frameLeft = $($left);
        if ($frameLeft.parent().length && !$frameLeft.parent().is($editor)) {
            $frameLeft.detach().appendTo($editor);
        } else if (!$frameLeft.parent().length) {
            $frameLeft.appendTo($editor);
        }
        $frameLeft.css({ flex: '1 1 50%', width: '50%', height:  '98%', display: 'block' }).show();

        $('<div class="datalinq-separator vertical-separator"></div>').appendTo($editor);

        const $stack = $('<div class="datalinq-frame-stack">').css({
            display: 'flex',
            flexDirection:  'column',
            flex:  '1 1 50%',
            width: '50%',
            height: '98%',
            position: 'relative',
        });

        // For frames going into $stack, use detach() to preserve state
        const $frame1 = $(frames[1]);
        if ($frame1.parent().length) {
            $frame1.detach();
        }
        $frame1.css({ flex: '1 1 50%', width: '100%', height: '50%', display: 'block' }).show().appendTo($stack);

        $('<div class="datalinq-separator horizontal-separator"></div>').appendTo($stack);

        const $frame2 = $(frames[2]);
        if ($frame2.parent().length) {
            $frame2.detach();
        }
        $frame2.css({ flex: '1 1 50%', width: '100%', height: '50%', display: 'block' }).show().appendTo($stack);

        $stack.appendTo($editor);
    }
}


})(jQuery);
