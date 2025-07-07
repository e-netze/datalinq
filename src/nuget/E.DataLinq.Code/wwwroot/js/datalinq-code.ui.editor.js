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
            }).get();
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
        dataLinqCode.events.on('tab-selected', function (channel, args) {
            showOrAddEditorFrame($editor, args.id);

            checkSize($tabs);
            dataLinqCode.events.fire('refresh-ui');
        });
        dataLinqCode.events.on('tab-removed', function (channel, args) {
            dataLinqCode.events.fire('destroy-editor', { id: args.id });
            $(".datalinq-code-editor-frame[data-id='" + args.id + "']").remove();

            if (args.selected) {
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

        $tab.click(function (e) {
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

        var menuRowAdded = false;

        $tabs.children('.datalinq-code-tab').each(function (i, tab) {
            var $tab = $(tab);

            var id = $tab.attr('data-id');

            if (menuRowAdded == false && id.indexOf('_') != 0) {
                menuRowAdded = true;

                var $menu = $("<li>")
                    .addClass('datalinq-code-tab')
                    .css('text-align', 'right')
                    .appendTo($ul);

                $("<button>")
                    .addClass('datalinq-code-button cancel')
                    .text('Close selected')
                    .appendTo($menu)
                    .click(function () {
                        $(this).closest('.datalinq-code-open-tabs').children('.datalinq-code-tab').each(function (i, li) {
                            var $li = $(li), $tab = $li.data("$tab");

                            if (!$tab || $tab.attr('data-id').indexOf('_') == 0) {
                                return;
                            }

                            var $checkbox = $li.children('.checkbox');

                            if ($checkbox.hasClass('checked') === true) {
                                $tab.children('.close-button').trigger('click');
                            }
                        });

                        $(null).dataLinq_code_modal('close');
                    });

                $("<div>")
                    .addClass('checkbox')
                    .appendTo($menu)
                    .click(function (e) {
                        e.stopPropagation();

                        var $this = $(this);
                        $this.toggleClass('checked');
                        var checked = $this.hasClass('checked');

                        $this.closest('.datalinq-code-open-tabs').children('.datalinq-code-tab').each(function (i, li) {
                            var $li = $(li), $tab = $li.data("$tab");

                            if (!$tab || $tab.attr('data-id').indexOf('_') == 0) {
                                return;
                            }

                            var $checkbox = $li.children('.checkbox');

                            if (($checkbox.hasClass('checked') === true && checked === false) ||
                                ($checkbox.hasClass('checked') === false && checked === true)) {
                                $checkbox.trigger('click');
                            }
                        });
                    });
            }

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

                $("<div>")
                    .addClass('checkbox')
                    .appendTo($li)
                    .click(function (e) {
                        e.stopPropagation();

                        var $this = $(this);
                        if ($this.parent().hasClass('dirty') || $this.parent().hasClass('errors')) {
                            return;
                        }

                        $this.toggleClass('checked');
                    });
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
        if ($frame.length === 0) {
            const src = buildFrameSrc(id);
            $frame = $("<iframe>")
                .addClass('datalinq-code-editor-frame')
                .attr('data-id', id)
                .attr('src', src)
                .appendTo($editor);
        }

        $frame.on('load', function () {
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
                console.warn(`Could not access iframe content for [${id}] due to cross-origin policy.`);
            }
        });

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

    function buildFrameSrc(id) {
        const base = dataLinqCode.targetUrl();
        const token = window._datalinqCodeAccessToken;

        if (id === '_start') return `${base}/Start`;

        const parts = id.split('@');
        const [endpoint, query, view, suffix] = parts;

        if (parts.length === 1) {
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
            .map(function () {
                return $editor.children(`.datalinq-code-editor-frame[data-id='${$(this).attr('data-id')}']`)[0];
            }).get().filter(Boolean).slice(0, 3);
    }

    function layoutFrames($editor, frames) {
        $editor.find('.datalinq-frame-stack').remove();
        $editor.find('.datalinq-separator').remove();
        $editor.children(".datalinq-code-editor-frame").hide().css({ flex: '', width: '', height: '', display: 'none' });
        $editor.css({ display: 'flex', flexDirection: 'row', width: '100%', height: '100%' });

        const count = frames.length;

        if (count === 1) {
            $(frames[0]).css({ flex: '1 1 100%', width: '100%', height: '98%', display: 'block' }).show().appendTo($editor);
        } else if (count === 2) {
            $(frames[0]).css({ flex: '1 1 50%', width: '50%', height: '98%', display: 'block' }).show().appendTo($editor);

            $('<div class="datalinq-separator vertical-separator"></div>').appendTo($editor);

            $(frames[1]).css({ flex: '1 1 50%', width: '50%', height: '98%', display: 'block' }).show().appendTo($editor);
        } else if (count === 3) {
            const [$left, $topRight, $bottomRight] = frames;

            $($left).css({ flex: '1 1 50%', width: '50%', height: '98%', display: 'block' }).show().appendTo($editor);

            $('<div class="datalinq-separator vertical-separator"></div>').appendTo($editor);

            const $stack = $('<div class="datalinq-frame-stack">').css({
                display: 'flex',
                flexDirection: 'column',
                flex: '1 1 50%',
                width: '50%',
                height: '98%',
                position: 'relative',
            });

            $(frames[1]).css({ flex: '1 1 50%', width: '100%', height: '50%', display: 'block' }).show().appendTo($stack);

            $('<div class="datalinq-separator horizontal-separator"></div>').appendTo($stack);

            $(frames[2]).css({ flex: '1 1 50%', width: '100%', height: '50%', display: 'block' }).show().appendTo($stack);

            $stack.appendTo($editor);
        }
    }


})(jQuery);
