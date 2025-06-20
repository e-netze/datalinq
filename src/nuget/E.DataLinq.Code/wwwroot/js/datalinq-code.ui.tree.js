(function ($) {
    "use strict";
    $.fn.dataLinqCode_tree = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_tree');
        }
    };
    var defaults = {
        $toolbar: null,
    };
    var methods = {
        init: function (options) {
            var settings = $.extend({}, defaults, options);
            return this.each(function () {
                new initUI(this, settings);
            });
        },
        refresh: function (options) {
            refresh($(this));
        }
    };
    var initUI = function (parent, options) {
        var $parent = $(parent).addClass('datalinq-code-tree-holder');

        if (options.$toolbar) {
            $("<div>")
                .addClass('tree-tool expand-all')
                .data('$tree', $parent)
                .appendTo(options.$toolbar)
                .click(function (e) {
                    e.stopPropagation();
                    $(this).data('$tree')
                        .find('.tree-node.collapsed')
                        .each(function (i, node) {
                            $(node).removeClass('collapsed')
                                .data('is_collapsed', false);
                        });
                });

            $("<input>")
                .addClass('datalinq-tree-search-input')
                .attr('placeholder', 'Find Endpoint, Query, View...')
                .data('$tree', $parent)
                .appendTo(options.$toolbar)
                .click(function (e) {
                    e.stopPropagation();

                    var $this=$(this), x = $(this).outerWidth() - e.originalEvent.layerX;
                    //console.log(x);
                    if (x < 8) {
                        $this.removeClass('has-value').val('');
                        setFilter($this.data('$tree'), '')
                    }
                })
                .on('keyup', function (e) {
                    var $this = $(this);
                    if ($this.val()) {
                        $this.addClass('has-value')
                    } else {
                        $this.removeClass('has-value');
                    }
                    setFilter($this.data('$tree'), $this.val())
                });
        }

        var $tree = createTreeNode("","<div>")
            .addClass('datalinq-code-tree')
            .appendTo($parent);

        dataLinqCode.events.on('document-deleted', function (channel) {
            refreshSilent($parent);
        });

        refresh($parent);
    };

    var setFilter = function ($parent, filter) {
        filter = filter.toLowerCase();

        if (!filter) {
            $parent.find('.tree-node').removeClass('hidden').removeClass('found').removeClass('collapsed');
            $parent.find('.tree-node').each(function (i, node) {
                var $node = $(node);
                if ($node.data('is_collapsed') === true) {
                    $node.addClass('collapsed');
                }
            });
            return;
        }

        $parent.find('.tree-node').each(function (i, node) {
            var $node = $(node);

            var searchText = $node.data('search-text');
            if (!searchText) {
                $node.addClass('hidden');
            } else if (searchText.indexOf(filter) < 0) {
                $node.addClass('hidden').removeClass('found');
            } else {
                $node.removeClass('hidden').addClass('found');

                //console.log(searchText, searchText.indexOf(filter));

                // Show all up nodes
                var $pNode = $node.parent().parent();
                while ($pNode.hasClass('tree-node')) {
                    $pNode.removeClass('hidden')
                          .addClass('collapsed');

                    $pNode = $pNode.parent().parent();
                }
            }
        });

        // Show all down nodes
        $parent.find('.tree-node.found').each(function (i, node) {
            var $node = $(node);

            $node
                .removeClass('collapsed')
                .find('.tree-node')
                .removeClass('hidden')
                .removeClass('collapsed');
        });
    }

    var refresh = function ($parent) {
        var $tree = $parent.children('.datalinq-code-tree');

        if (dataLinqCode.privileges.createEndpoints() && $tree.find('.tree-node.endpoint.add').length === 0) {
            addEndPointNode($tree, null);
            dataLinqCode.setAppPrefixFilters([]);
        }

        if (dataLinqCode.privileges.useAppPrefixFilters() === true) {
            dataLinqCode.api.getEndPointPrefixes(function (prefixes) {
                $('body').dataLinq_code_modal({
                    title: 'Select Application Prefixes...',
                    onload: function ($content) {
                        renderEndPointPrefixesList($parent, $content, prefixes);
                    }
                });
            });
        } else {
            refrehTree($parent, null);
        }
    };
    var refreshSilent = function ($parent) {
        refrehTree($parent, dataLinqCode.getAppPrefixFilters());
    };
    var refrehTree = function ($parent, prefixes) {
        var $tree = $parent.children('.datalinq-code-tree');

        dataLinqCode.setAppPrefixFilters(prefixes);

        var collapsedRoutes = [];
        $tree.find('.tree-node.collapsed').each(function (i, node) {
            collapsedRoutes.push($(node).data('data-route'));
        });

        $tree.empty();

        dataLinqCode.api.getEndPoints(prefixes, function (endPoints) {
            if (dataLinqCode.privileges.createEndpoints()) {
                addEndPointNode($tree, null);
            }

            $.each(endPoints, function (i, endPoint) {
                addEndPointNode($tree, endPoint, collapsedRoutes);
            });
        });
    };

    var createTreeNode = function (label, element, asInput) {
        var $node = $(element || "<li>")
            .addClass("tree-node");

        if (label) {
            $("<div>").addClass('icon').appendTo($node);
            if (asInput == true) {
                $("<input type='text'/>")
                    .attr('placeholder', label)
                    .appendTo($node);
            } else {
                var $label = $("<div>").addClass('label').text(label).appendTo($node);

                $node.on('mousemove', function (e) {
                    $(this).closest('.datalinq-code-tree-holder').find('.tree-node').removeClass('mouseover');
                    e.stopPropagation();
                    if (e.originalEvent.layerY >= 0 && e.originalEvent.layerY <= 32) {
                        $(this).addClass('mouseover');
                    } else {
                        $(this).removeClass('mouseover');
                    }
                }).on('mouseleave', function (e) {
                    $(this).removeClass('mouseover');
                });

                var $copyButton = $("<div>")
                    .addClass('copy-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        var route = $(this).closest('.tree-node').data('data-route');
                        navigator.clipboard.writeText(route);

                        if (route.length > 20)
                            route = route.substr(0, 20) + '...';

                        $(this)
                            .find('.tooltiptext')
                            .text("Copied route: " + route)
                            .addClass('show');
                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('Copy placeholder')
                    .appendTo($copyButton);
            }
        }

        return $node;
    };

    var createTreeNodeEndpoint = function (label, element, asInput, endpoint) {
        var $node = $(element || "<li>")
            .addClass("tree-node");

        if (label) {
            $("<div>").addClass('icon').appendTo($node);
            if (asInput == true) {
                $("<input type='text'/>")
                    .attr('placeholder', label)
                    .appendTo($node);
            } else {
                var $label = $("<div>").addClass('label').text(label).appendTo($node);

                $node.on('mousemove', function (e) {
                    $(this).closest('.datalinq-code-tree-holder').find('.tree-node').removeClass('mouseover');
                    e.stopPropagation();
                    if (e.originalEvent.layerY >= 0 && e.originalEvent.layerY <= 32) {
                        $(this).addClass('mouseover');
                    } else {
                        $(this).removeClass('mouseover');
                    }
                }).on('mouseleave', function (e) {
                    $(this).removeClass('mouseover');
                });

                var $copyButton = $("<div>")
                    .addClass('copy-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        var route = $(this).closest('.tree-node').data('data-route');
                        navigator.clipboard.writeText(route);

                        if (route.length > 20)
                            route = route.substr(0, 20) + '...';

                        $(this)
                            .find('.tooltiptext')
                            .text("Copied route: " + route)
                            .addClass('show');
                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('Copy placeholder')
                    .appendTo($copyButton);

                // --- Added CSS Button ---
                var $cssButton = $("<div>")
                    .addClass('copy-button css-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        dataLinqCode.events.fire('open-endpoint-css', {
                            id: endpoint
                        });

                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('CSS')
                    .appendTo($cssButton);

                // --- Added JS Button ---
                var $jsButton = $("<div>")
                    .addClass('copy-button js-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        dataLinqCode.events.fire('open-endpoint-js', {
                            id: endpoint
                        });
                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('JS')
                    .appendTo($jsButton);
            }
        }

        return $node;
    };

    var createTreeNodeView = function (label, element, asInput, endpoint, query, view) {
        var $node = $(element || "<li>")
            .addClass("tree-node");

        if (label) {
            $("<div>").addClass('icon').appendTo($node);
            if (asInput == true) {
                $("<input type='text'/>")
                    .attr('placeholder', label)
                    .appendTo($node);
            } else {
                var $label = $("<div>").addClass('label').text(label).appendTo($node);

                $node.on('mousemove', function (e) {
                    $(this).closest('.datalinq-code-tree-holder').find('.tree-node').removeClass('mouseover');
                    e.stopPropagation();
                    if (e.originalEvent.layerY >= 0 && e.originalEvent.layerY <= 32) {
                        $(this).addClass('mouseover');
                    } else {
                        $(this).removeClass('mouseover');
                    }
                }).on('mouseleave', function (e) {
                    $(this).removeClass('mouseover');
                });

                var $copyButton = $("<div>")
                    .addClass('copy-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        var route = $(this).closest('.tree-node').data('data-route');
                        navigator.clipboard.writeText(route);

                        if (route.length > 20)
                            route = route.substr(0, 20) + '...';

                        $(this)
                            .find('.tooltiptext')
                            .text("Copied route: " + route)
                            .addClass('show');
                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('Copy placeholder')
                    .appendTo($copyButton);

                // --- Added CSS Button ---
                var $cssButton = $("<div>")
                    .addClass('copy-button css-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        dataLinqCode.events.fire('open-view-css', {
                            endpoint: endpoint,
                            query: query,
                            view: view
                        });

                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('Copy CSS code')
                    .appendTo($cssButton);

                // --- Added JS Button ---
                var $jsButton = $("<div>")
                    .addClass('copy-button js-button')
                    .appendTo($node)
                    .mouseout(function () {
                        $(this).find('.tooltiptext').removeClass('show');
                    })
                    .click(function (e) {
                        e.stopPropagation();

                        dataLinqCode.events.fire('open-view-js', {
                            endpoint: endpoint,
                            query: query,
                            view: view
                        });
                    });

                $("<span>")
                    .addClass('tooltiptext')
                    .text('Copy JS code')
                    .appendTo($jsButton);
            }
        }

        return $node;
    };

    var addToNodes = function ($node, $parent) {
        var $nodes = $parent.children('.tree-nodes');
        if ($nodes.length === 0) {
            $nodes = $("<ul>")
                .addClass('tree-nodes')
                .appendTo($parent);
        }
        $node.appendTo($nodes);
    }

    var addEndPointNode = function ($parent, endPoint, collapsedRoutes) {
        var $node = createTreeNodeEndpoint(endPoint || 'New endpoint...', null, endPoint === null, endPoint)
            .addClass('endpoint')
            .data('data-endpoint', endPoint)
            .data('data-route', endPoint);

        if ($.inArray($node.data('data-route'), collapsedRoutes) >= 0) {
            $node.addClass('collapsed');
            $node.data('is_collapsed', true);
        }

        if (endPoint) {
            $node.data('search-text', endPoint.toLowerCase());
        }

        addToNodes($node, $parent);

        if (endPoint) {
            $node.addClass('loading-' + endPoint);
            dataLinqCode.api.getQueries($node.data('data-endpoint'), function (queries) {
                $node.removeClass('loading-' + endPoint);
                if (dataLinqCode.privileges.createQueries()) {
                    addQueryNode($node, $node.data('data-endpoint'), null);
                }

                $.each(queries, function (i, query) {
                    addQueryNode($node, $node.data('data-endpoint'), query, collapsedRoutes);
                });
            });
            $node.click(function (e) {
                e.stopPropagation();
                if (e.originalEvent.layerY < 24) {
                    var $this = $(this);
                    if (e.originalEvent.layerX < 30) {
                        $this.toggleClass('collapsed');
                        $this.data('is_collapsed', $this.hasClass('collapsed'));
                    } else {
                        dataLinqCode.events.fire('open-endpoint', {
                            endpoint: $this.data('data-endpoint'),
                        });
                    }
                }
            });

        } else {
            $node
                .addClass('add')
                .click(function (e) {
                    e.stopPropagation();
                })
                .find('input').on('keyup', function (e) {
                    if (e.which == 13) {
                        var $this = $(this);

                        var id = $this.val();
                        //console.log('create endpoint ' + id);
                        $this.val('');

                        dataLinqCode.api.createEndPoint(id, function (result) {
                            if (result.success == true) {
                                dataLinqCode.addAppFilterPrefixIfCurrentlyUsed(result.endPoint.split('-')[0]);
                                refreshSilent($this.closest('.datalinq-code-tree-holder'));
                            } else {
                                dataLinqCode.ui.alert("Error", (result.error_message || 'Unknown error'));
                            }
                        });
                    }
                });
        }
    };

    var addQueryNode = function ($parent, endPoint, query, collapsedRoutes) {
        var $node = createTreeNode(query || 'New query/data...', null, query === null)
            .addClass('query')
            .data('data-endpoint', endPoint)
            .data('data-query', query)
            .data('data-route', endPoint + '@' + query);

        if ($.inArray($node.data('data-route'), collapsedRoutes) >= 0) {
            $node.addClass('collapsed');
            $node.data('is_collapsed', true);
        }

        if (query) {
            $node.data('search-text', query.toLowerCase());
        }

        addToNodes($node, $parent);

        if (query) {
            var $endPointNode = $node.parent().parent();
            $endPointNode.addClass('loading-' + query).addClass('has-children');
            $node.addClass('loading-' + query);

            dataLinqCode.api.getViews($node.data('data-endpoint'), $node.data('data-query'), function (views) {
                $endPointNode.removeClass('loading-' + query);
                $node.removeClass('loading-' + query);

                if (views.length > 0) {
                    $node.addClass('has-children');
                }

                if (dataLinqCode.privileges.createViews()) {
                    addViewNode($node, $node.data('data-endpoint'), $node.data('data-query'), null);
                }

                $.each(views, function (i, view) {
                    addViewNode($node, $node.data('data-endpoint'), $node.data('data-query'), view);
                });
            });

            $node.click(function (e) {
                e.stopPropagation();
                if (e.originalEvent.layerY < 24) {
                    var $this = $(this);
                    if (e.originalEvent.layerX < 30) {
                        $this.toggleClass('collapsed');
                        $this.data('is_collapsed', $this.hasClass('collapsed'));
                    } else {
                        dataLinqCode.events.fire('open-query', {
                            endpoint: $this.data('data-endpoint'),
                            query: $this.data('data-query')
                        });
                    }
                }
            })
        } else {
            $node
                .addClass('add')
                .click(function (e) {
                    e.stopPropagation();
                })
                .find('input').on('keyup', function (e) {
                    if (e.which == 13) {
                        var $this = $(this), $node = $this.closest('.query');

                        var id = $this.val();
                        //console.log('create query ' + id);
                        $this.val('');

                        dataLinqCode.api.createQuery($node.data('data-endpoint'), id, function (result) {
                            if (result.success == true) {
                                refreshSilent($this.closest('.datalinq-code-tree-holder'));
                            } else {
                                dataLinqCode.ui.alert("Error", (result.error_message || 'Unknown error'));
                            }
                        });
                    }
                });
        }
    };

    var ctrlPressed = false;

    $(document).keydown(function (e) {
        if (e.key === "Control") ctrlPressed = true;
    }).keyup(function (e) {
        if (e.key === "Control") ctrlPressed = false;
    });

    var addViewNode = function ($parent, endPoint, query, view) {
        var $node = createTreeNodeView(view || 'New view...', null, view === null, endPoint, query, view)
            .addClass('view')
            .data('data-endpoint', endPoint)
            .data('data-query', query)
            .data('data-view', view)
            .data('data-route', endPoint + '@' + query + '@' + view);

        if (view) {
            $node.data('search-text', view.toLowerCase());
        }

        addToNodes($node, $parent);

        if (view) {
            $node.off('click').on('click', function (e) {
                e.stopPropagation();

                const $this = $(this);
                const endpoint = $this.data('data-endpoint');
                const query = $this.data('data-query');
                const view = $this.data('data-view');
                const baseId = `${endpoint}@${query}@${view}`;

                const $tabs = $(".datalinq-code-tab");
                const $existingTab = $tabs.filter(`[data-id="${baseId}"]`);
                const $existingTabCss = $tabs.filter(`[data-id="${baseId}@_css"]`);
                const $existingTabJs = $tabs.filter(`[data-id="${baseId}@_js"]`);

                const tabExists = $existingTab.length > 0;
                const tabExistsCss = $existingTabCss.length > 0;
                const tabExistsJs = $existingTabJs.length > 0;

                const clearSelection = () => {
                    $tabs.filter('.selected').removeClass('selected').removeAttr('data-selected-at');
                };

                const selectTab = ($tab) => {
                    $tab.addClass('selected').attr('data-selected-at', Date.now());
                };

                if (ctrlPressed) {
                    if (!tabExists) {
                        if (tabExistsCss || tabExistsJs) {
                            dataLinqCode.events.fire('open-view', { endpoint, query, view });
                        } else {
                            clearSelection();
                            dataLinqCode.events.fire('open-view', { endpoint, query, view });
                            dataLinqCode.events.fire('open-view-css', { endpoint, query, view });
                            dataLinqCode.events.fire('open-view-js', { endpoint, query, view });
                        }
                    } else {
                        if ($existingTab.hasClass('selected')) {
                            $existingTab.removeClass('selected').removeAttr('data-selected-at');
                        } else {
                            const selectedCount = $tabs.filter('.selected').length;
                            if (selectedCount >= 3) {
                                dataLinqCode.ui.alert('Limit', 'You can only select up to 3 tabs.');
                                return;
                            }
                            selectTab($existingTab);
                        }
                        dataLinqCode.events.fire('tab-selected', { id: baseId });
                    }
                } else {
                    if (tabExists) {
                        clearSelection();
                        selectTab($existingTab);
                        dataLinqCode.events.fire('tab-selected', { id: baseId });
                    } else {
                        dataLinqCode.events.fire('open-view', { endpoint, query, view });
                    }
                }
            });
        }
        else {
            $node
                .addClass('add')
                .click(function (e) {
                    e.stopPropagation();
                })
                .find('input').on('keyup', function (e) {
                    if (e.which == 13) {
                        var $this = $(this), $node = $this.closest('.view');

                        var id = $this.val();
                        //console.log('create view ' + id);
                        $this.val('');

                        dataLinqCode.api.createView($node.data('data-endpoint'), $node.data('data-query'), id, function (result) {
                            if (result.success == true) {
                                refreshSilent($this.closest('.datalinq-code-tree-holder'));
                            } else {
                                dataLinqCode.ui.alert("Error", (result.error_message || 'Unknown error'));
                            }
                        });
                    }
                });
        }
    };

    function restoreSavedTabs() {
        const savedTabs = JSON.parse(localStorage.getItem('datalinq-open-tabs') || '[]');

        dataLinqCode.ui.confirmIf(
            savedTabs.length > 0,
            "Restore Tabs",
            "Would you like to restore the tabs from your previous session?",
            function () {
                savedTabs.forEach(id => {
                    const parts = id.split('@');

                    if (parts.length === 1) {
                        const endpoint = parts[0];
                        dataLinqCode.events.fire('open-endpoint', { endpoint });
                    } else if (parts.length === 2) {
                        const [endpoint, query] = parts;
                        dataLinqCode.events.fire('open-query', { endpoint, query });
                    } else {
                        const endpoint = parts[0] || '';
                        const query = parts[1] || '';
                        const view = parts[2] || '';
                        const suffix = parts[3] || '';

                        if (suffix === '_js') {
                            dataLinqCode.events.fire('open-view-js', { endpoint, query, view });
                        } else if (suffix === '_css') {
                            dataLinqCode.events.fire('open-view-css', { endpoint, query, view });
                        } else {
                            dataLinqCode.events.fire('open-view', { endpoint, query, view });
                        }
                    }
                });
            }
        );
    }


    var renderEndPointPrefixesList = function ($parent, $content, prefixes) {
        var $tree = $parent.children('.datalinq-code-tree');
        var currentPrefixes = dataLinqCode.getAppPrefixFilters();

        let $ul = $("<ul>")
            .addClass('datalinq-code-app-prefixes')
            .appendTo($content);

        $.each(prefixes, function (prefix, endPointIds) {

            console.log(prefix, endPointIds);

            var $li = $("<li>")
                .addClass('datalinq-code-app-prefix')
                .data('app-prefix', prefix)
                .appendTo($ul)
                .click(function (e) {
                    e.stopPropagation();
                    $(this).toggleClass('checked');
                });

            if (currentPrefixes && $.inArray(prefix, currentPrefixes) >= 0) {
                $li.addClass('checked');
            }

            $("<div>")
                .addClass('text')
                .text(prefix)
                .appendTo($li);

            $("<div>")
                .addClass('subtext')
                .text(endPointIds)
                .appendTo($li);

            $("<div>")
                .addClass('checkbox')
                .appendTo($li); 
        });

        let $buttons = $("<div>")
            .addClass('datalinq-code-buttons-bar-right')
            .appendTo($content);

        $("<button>")
            .addClass('datalinq-code-button')
            .text('Open all')
            .appendTo($buttons)
            .click(function () {
                $(null).dataLinq_code_modal('close');
                refrehTree($parent, null);
                restoreSavedTabs();
            });

        $("<button>")
            .addClass('datalinq-code-button cancel')
            .text('Open selected')
            .appendTo($buttons)
            .click(function () {
                var prefixes = [];

                $content.find('.datalinq-code-app-prefix.checked').each(function (i, li) {
                    prefixes.push($(li).data('app-prefix'));
                });

                //if (prefixes.length === 0) {
                //    alert('Nothing selected');
                //    return;
                //}

                $(null).dataLinq_code_modal('close');
                refrehTree($parent, prefixes);
            });
    };
})(jQuery);
