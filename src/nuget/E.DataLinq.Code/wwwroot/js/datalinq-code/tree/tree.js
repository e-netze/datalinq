/**
 * DataLinq Code - document tree
 *
 * jQuery plugin $.fn.dataLinqCode_tree: endpoints/queries/views tree, folders, context menu.
 * Depends on: core, api, ui/dialogs.js
 */
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
        // share the context menu with other ui components (e.g. editor tabs)
        dataLinqCode.ui.contextMenu = showContextMenu;

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

            var $searchInput = options.$toolbar.find('.datalinq-tree-search-input');

            $("<div>")
                .addClass('tree-tool new-folder')
                .attr('title', 'New folder')
                .insertBefore($searchInput)
                .click(function (e) {
                    e.stopPropagation();
                    actions.newFolder($parent);
                });

            if (dataLinqCode.privileges.createEndpoints()) {
                $("<div>")
                    .addClass('tree-tool new-endpoint')
                    .attr('title', 'New endpoint')
                    .insertBefore($searchInput)
                    .click(function (e) {
                        e.stopPropagation();
                        actions.newEndpoint($parent, null);
                    });
            }
        }

        var $tree = createTreeNode("", "<div>")
            .addClass('datalinq-code-tree')
            .appendTo($parent);

        $parent.on('contextmenu', function (e) {
            e.preventDefault();

            showContextMenu(e, [
                _foldersEnabled ? { text: 'New folder', action: function () { actions.newFolder($parent); } } : null,
                dataLinqCode.privileges.createEndpoints() ? { text: 'New endpoint', action: function () { actions.newEndpoint($parent, null); } } : null
            ]);
        });

        dataLinqCode.events.on('document-deleted', function (channel, args) {
            if (args && args.id && args.id.split('@').length === 1 && removeEndpointFromFolders(args.id)) {
                saveFolders(function () { refreshSilent($parent); });
            } else {
                refreshSilent($parent);
            }
        });

        refresh($parent);
    };

    /****** Folder structure ******/

    var _folderStructure = {};
    var _foldersEnabled = true;

    var parseFolderStructure = function (folderStructure) {
        if (!folderStructure || folderStructure === 'null') {
            return {};
        }

        if (typeof folderStructure === 'string') {
            folderStructure = JSON.parse(folderStructure);
        }

        var result = {};
        for (var folderName in folderStructure) {
            if (folderStructure.hasOwnProperty(folderName) && folderName !== 'no folder') {
                result[folderName] = (folderStructure[folderName] || []).slice();
            }
        }
        return result;
    };

    var saveFolders = function (callback) {
        dataLinqCode.api.saveFolderStructure(_folderStructure, function (result) {
            if (callback) {
                callback(result);
            }
        });
    };

    var removeEndpointFromFolders = function (endPoint) {
        var changed = false;
        for (var folderName in _folderStructure) {
            var index = $.inArray(endPoint, _folderStructure[folderName]);
            if (index >= 0) {
                _folderStructure[folderName].splice(index, 1);
                changed = true;
            }
        }
        return changed;
    };

    /****** Context menu ******/

    var closeContextMenu = function () {
        $('.datalinq-code-contextmenu').remove();
    };

    $(document)
        .on('mousedown', function (e) {
            if ($(e.target).closest('.datalinq-code-contextmenu').length === 0) {
                closeContextMenu();
            }
        })
        .on('keydown', function (e) {
            if (e.key === 'Escape') {
                closeContextMenu();
            }
        });
    $(window).on('blur resize', closeContextMenu);
    document.addEventListener('scroll', closeContextMenu, true);

    var showContextMenu = function (e, items) {
        closeContextMenu();

        // remove empty entries and leading/trailing/duplicate separators
        var cleaned = [];
        $.each(items, function (i, item) {
            if (!item) return;
            if (item === '-' && (cleaned.length === 0 || cleaned[cleaned.length - 1] === '-')) return;
            cleaned.push(item);
        });
        while (cleaned.length > 0 && cleaned[cleaned.length - 1] === '-') {
            cleaned.pop();
        }
        if (cleaned.length === 0) {
            return;
        }

        var $container = $('.datalinq-code-ide').first();
        var $menu = $("<ul>")
            .addClass('datalinq-code-contextmenu')
            .appendTo($container.length ? $container : $('body'))
            .on('contextmenu', function (e) { e.preventDefault(); });

        $.each(cleaned, function (i, item) {
            if (item === '-') {
                $("<li>").addClass('separator').appendTo($menu);
                return;
            }

            $("<li>")
                .addClass('item')
                .toggleClass('danger', item.danger === true)
                .text(item.text)
                .appendTo($menu)
                .click(function (ev) {
                    ev.stopPropagation();
                    closeContextMenu();
                    item.action();
                });
        });

        var x = e.clientX, y = e.clientY;
        var maxX = window.innerWidth - $menu.outerWidth() - 4;
        var maxY = window.innerHeight - $menu.outerHeight() - 4;
        $menu.css({ left: Math.max(0, Math.min(x, maxX)), top: Math.max(0, Math.min(y, maxY)) });
    };

    /****** Actions ******/

    var validateName = function (value) {
        return value ? null : 'Please enter a name.';
    };

    var handleResult = function (result, onSuccess) {
        if (result.success == true) {
            onSuccess(result);
        } else {
            dataLinqCode.ui.alert("Error", (result.error_message || 'Unknown error'));
        }
    };

    var actions = {
        newFolder: function ($parent) {
            if (!_foldersEnabled) return;

            dataLinqCode.ui.prompt('New folder', 'Name of the new folder:', '', function (name) {
                _folderStructure[name] = [];
                saveFolders(function () { refreshSilent($parent); });
            }, {
                validate: function (value) {
                    if (!value) return 'Please enter a name.';
                    if (value === 'no folder' || _folderStructure.hasOwnProperty(value)) return 'A folder with this name already exists.';
                    return null;
                }
            });
        },

        renameFolder: function ($parent, folderName) {
            dataLinqCode.ui.prompt('Rename folder', 'New name of the folder:', folderName, function (name) {
                if (name === folderName) return;

                var renamed = {};
                for (var key in _folderStructure) {
                    renamed[key === folderName ? name : key] = _folderStructure[key];
                }
                _folderStructure = renamed;
                saveFolders(function () { refreshSilent($parent); });
            }, {
                validate: function (value) {
                    if (!value) return 'Please enter a name.';
                    if (value !== folderName && (value === 'no folder' || _folderStructure.hasOwnProperty(value))) return 'A folder with this name already exists.';
                    return null;
                }
            });
        },

        deleteFolder: function ($parent, folderName) {
            dataLinqCode.ui.confirm('Delete folder', 'Delete folder "' + folderName + '"? Its endpoints will be moved to the root.', function () {
                delete _folderStructure[folderName];
                saveFolders(function () { refreshSilent($parent); });
            });
        },

        newEndpoint: function ($parent, folderName) {
            if (!dataLinqCode.privileges.createEndpoints()) return;

            var options = { validate: validateName };
            var folders = Object.keys(_folderStructure).sort();
            if (_foldersEnabled && folders.length > 0) {
                options.select = {
                    label: 'Folder:',
                    items: [{ value: '', text: '(no folder)' }].concat($.map(folders, function (f) { return { value: f, text: f }; })),
                    value: folderName || ''
                };
            }

            dataLinqCode.ui.prompt('New endpoint', 'Name of the new endpoint:', '', function (name, selectedFolder) {
                dataLinqCode.api.createEndPoint(name, function (result) {
                    handleResult(result, function () {
                        dataLinqCode.addEndpointToSelectionIfActive(result.endPoint);

                        if (selectedFolder && _folderStructure[selectedFolder]) {
                            _folderStructure[selectedFolder].push(result.endPoint);
                            saveFolders(function () { refreshSilent($parent); });
                        } else {
                            refreshSilent($parent);
                        }
                    });
                });
            }, options);
        },

        moveEndpoint: function ($parent, endPoint) {
            if (!_foldersEnabled) return;

            var currentFolder = '';
            for (var key in _folderStructure) {
                if ($.inArray(endPoint, _folderStructure[key]) >= 0) {
                    currentFolder = key;
                    break;
                }
            }

            var folders = Object.keys(_folderStructure).sort();

            dataLinqCode.ui.prompt('Move endpoint', 'Move endpoint "' + endPoint + '" to:', '', function (value, selectedFolder) {
                if (selectedFolder === currentFolder) return;

                removeEndpointFromFolders(endPoint);
                if (selectedFolder && _folderStructure[selectedFolder]) {
                    _folderStructure[selectedFolder].push(endPoint);
                }
                saveFolders(function () { refreshSilent($parent); });
            }, {
                noInput: true,
                select: {
                    items: [{ value: '', text: '(no folder)' }].concat($.map(folders, function (f) { return { value: f, text: f }; })),
                    value: currentFolder
                }
            });
        },

        newQuery: function ($parent, endPoint) {
            if (!dataLinqCode.privileges.createQueries()) return;

            dataLinqCode.ui.prompt('New query', 'Name of the new query in endpoint "' + endPoint + '":', '', function (name) {
                dataLinqCode.api.createQuery(endPoint, name, function (result) {
                    handleResult(result, function () { refreshSilent($parent); });
                });
            }, { validate: validateName });
        },

        newView: function ($parent, endPoint, query) {
            if (!dataLinqCode.privileges.createViews()) return;

            dataLinqCode.ui.prompt('New view', 'Name of the new view in query "' + endPoint + '@' + query + '":', '', function (name) {
                dataLinqCode.api.createView(endPoint, query, name, function (result) {
                    handleResult(result, function () { refreshSilent($parent); });
                });
            }, { validate: validateName });
        },

        deleteItem: function (id) {
            dataLinqCode.events.fire('delete-document', { id: id });
        },

        run: function (id) {
            window.open(dataLinqCode.buildRunUrl(id));
        },

        copyId: function (id) {
            navigator.clipboard.writeText(id);
        }
    };

    var appendAddButton = function ($node, title, onClick) {
        $("<div>")
            .addClass('copy-button add-button')
            .attr('title', title)
            .appendTo($node)
            .click(function (e) {
                e.stopPropagation();
                onClick();
            });
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
    if (dataLinqCode.privileges.useEndpointSelection() === true) {
        dataLinqCode.api.getEndPoints(function (endPoints) {
            dataLinqCode.api.getFolderStructure(function (folderStructure) {
                $('body').dataLinq_code_modal({
                    title: 'Select folders / endpoints...',
                    onload: function ($content) {
                        renderEndpointSelectionTree($parent, $content, endPoints || [], parseFolderStructure(folderStructure));
                    }
                });
            });
        });
    } else {
        refrehTree($parent, null);
    }
};
var refreshSilent = function ($parent) {
    refrehTree($parent, dataLinqCode.getEndpointSelection());
};
var refrehTree = function ($parent, selection) {
    var $tree = $parent.children('.datalinq-code-tree');

    dataLinqCode.setEndpointSelection(selection);
    var isSelected = function (endPoint) {
        return selection == null || $.inArray(endPoint, selection) >= 0;
    };

    var collapsedRoutes = [];
    $tree.find('.tree-node.collapsed').each(function (i, node) {
        collapsedRoutes.push($(node).data('data-route'));
    });

    $tree.empty();

    dataLinqCode.api.getEndPoints(function (endPoints) {
        endPoints = $.grep(endPoints || [], isSelected);

        _foldersEnabled = true;
        $('.tree-tool.new-folder').toggle(_foldersEnabled);

        dataLinqCode.api.getFolderStructure(function (folderStructure) {
            _folderStructure = parseFolderStructure(folderStructure);

            var renderedEndpoints = {};

            $.each(Object.keys(_folderStructure).sort(), function (i, folderName) {
                var folderEndpoints = $.grep(_folderStructure[folderName], function (endpointName) {
                    return $.inArray(endpointName, endPoints) >= 0;
                });
                if (selection != null && folderEndpoints.length === 0) {
                    return;
                }

                var $folder = createTreeNodeFolder(folderName)
                    .data('data-folder', folderName)
                    .data('data-route', folderName)
                    .data('search-text', folderName.toLowerCase());

                if ($.inArray(folderName, collapsedRoutes) >= 0) {
                    $folder.addClass('collapsed');
                    $folder.data('is_collapsed', true);
                }

                addToNodes($folder, $tree);
                attachFolderEventHandlers($folder);

                $.each(_folderStructure[folderName], function (j, endpointName) {
                    if ($.inArray(endpointName, endPoints) >= 0 && !renderedEndpoints[endpointName]) {
                        addEndPointNode($folder, endpointName, collapsedRoutes);
                        renderedEndpoints[endpointName] = true;
                    }
                });
            });

            $.each(endPoints, function (i, endPoint) {
                if (!renderedEndpoints[endPoint]) {
                    addEndPointNode($tree, endPoint, collapsedRoutes);
                }
            });
        });
    });
};

var attachFolderEventHandlers = function ($folder) {
    var folderName = $folder.data('data-folder');

    if (dataLinqCode.privileges.createEndpoints()) {
        appendAddButton($folder, 'New endpoint', function () {
            actions.newEndpoint($folder.closest('.datalinq-code-tree-holder'), folderName);
        });
    }

    $folder.click(function (e) {
        e.stopPropagation();

        if (e.originalEvent.layerY < 24) {
            var $this = $(this);
            $this.toggleClass('collapsed');
            $this.data('is_collapsed', $this.hasClass('collapsed'));
        }
    });

    $folder.on('contextmenu', function (e) {
        e.preventDefault();
        e.stopPropagation();

        var $parent = $folder.closest('.datalinq-code-tree-holder');

        showContextMenu(e, [
            dataLinqCode.privileges.createEndpoints() ? { text: 'New endpoint', action: function () { actions.newEndpoint($parent, folderName); } } : null,
            '-',
            { text: 'Rename', action: function () { actions.renameFolder($parent, folderName); } },
            { text: 'Delete', danger: true, action: function () { actions.deleteFolder($parent, folderName); } }
        ]);
    });
};

    var createTreeNodeFolder = function (folderName, element) {
        var $node = $(element || "<li>")
            .addClass("tree-node folder has-children");

        var $icon = $("<div>")
            .addClass('icon')
            .appendTo($node);

        var $label = $("<div>")
            .addClass('label')
            .text(folderName)
            .appendTo($node);

        var $nestedList = $("<ul>")
            .addClass('tree-nodes')
            .appendTo($node);

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

        return $node;
    };

    var createTreeNode = function (label, element) {
        var $node = $(element || "<li>")
            .addClass("tree-node");

        if (label) {
            $("<div>").addClass('icon').appendTo($node);
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

        }

        return $node;
    };

    var createTreeNodeEndpoint = function (label, element, endpoint) {
        var $node = $(element || "<li>")
            .addClass("tree-node");

        if (label) {
            $("<div>").addClass('icon').appendTo($node);
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

        return $node;
    };

    var createTreeNodeView = function (label, element, endpoint, query, view) {
        var $node = $(element || "<li>")
            .addClass("tree-node");

        if (label) {
            $("<div>").addClass('icon').appendTo($node);
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
        var $node = createTreeNodeEndpoint(endPoint, null, endPoint)
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

            if (dataLinqCode.privileges.createQueries()) {
                appendAddButton($node, 'New query', function () {
                    actions.newQuery($node.closest('.datalinq-code-tree-holder'), endPoint);
                });
            }

            $node.on('contextmenu', function (e) {
                e.preventDefault();
                e.stopPropagation();

                var $holder = $node.closest('.datalinq-code-tree-holder');

                showContextMenu(e, [
                    dataLinqCode.privileges.createQueries() ? { text: 'New query', action: function () { actions.newQuery($holder, endPoint); } } : null,
                    '-',
                    _foldersEnabled && Object.keys(_folderStructure).length > 0 ? { text: 'Move to folder...', action: function () { actions.moveEndpoint($holder, endPoint); } } : null,
                    { text: 'Copy ID', action: function () { actions.copyId(endPoint); } },
                    '-',
                    dataLinqCode.privileges.deleteEndpoints() ? { text: 'Delete', danger: true, action: function () { actions.deleteItem(endPoint); } } : null
                ]);
            });
        }
    };

    var addQueryNode = function ($parent, endPoint, query, collapsedRoutes) {
        var $node = createTreeNode(query, null)
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

            var queryId = endPoint + '@' + query;

            if (dataLinqCode.privileges.createViews()) {
                appendAddButton($node, 'New view', function () {
                    actions.newView($node.closest('.datalinq-code-tree-holder'), endPoint, query);
                });
            }

            $node.on('contextmenu', function (e) {
                e.preventDefault();
                e.stopPropagation();

                var $holder = $node.closest('.datalinq-code-tree-holder');

                showContextMenu(e, [
                    dataLinqCode.privileges.createViews() ? { text: 'New view', action: function () { actions.newView($holder, endPoint, query); } } : null,
                    '-',
                    { text: 'Run (new tab)', action: function () { actions.run(queryId); } },
                    { text: 'Copy ID', action: function () { actions.copyId(queryId); } },
                    '-',
                    dataLinqCode.privileges.deleteQueries() ? { text: 'Delete', danger: true, action: function () { actions.deleteItem(queryId); } } : null
                ]);
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
        var $node = createTreeNodeView(view, null, endPoint, query, view)
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
            var viewId = endPoint + '@' + query + '@' + view;

            $node.on('contextmenu', function (e) {
                e.preventDefault();
                e.stopPropagation();

                showContextMenu(e, [
                    { text: 'Run (new tab)', action: function () { actions.run(viewId); } },
                    { text: 'Copy ID', action: function () { actions.copyId(viewId); } },
                    '-',
                    dataLinqCode.privileges.deleteViews() ? { text: 'Delete', danger: true, action: function () { actions.deleteItem(viewId); } } : null
                ]);
            });

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
    };

    function restoreSavedTabs() {
        const savedTabs = JSON.parse(localStorage.getItem('datalinq-open-tabs') || '[]')
            .filter(id => id && id.indexOf('_') !== 0);

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


    var renderEndpointSelectionTree = function ($parent, $content, endPoints, folderStructure) {
        var currentSelection = dataLinqCode.getEndpointSelection();

        var $filter = $("<input>")
            .addClass('datalinq-code-modal-input datalinq-code-endpoint-select-filter')
            .attr('placeholder', 'Search for folders, endpoints...')
            .appendTo($content);

        let $ul = $("<ul>")
            .addClass('datalinq-code-app-prefixes datalinq-code-endpoint-select')
            .appendTo($content);

        var addRow = function ($target, text, subtext, cssClass) {
            var $li = $("<li>")
                .addClass('datalinq-code-app-prefix ' + cssClass)
                .appendTo($target);

            $("<div>").addClass('icon').appendTo($li);
            $("<div>").addClass('text').text(text).appendTo($li);
            if (subtext) {
                $("<div>").addClass('subtext').text(subtext).appendTo($li);
            }
            $("<div>").addClass('checkbox').appendTo($li);

            return $li;
        };

        var updateFolderState = function ($folderLi) {
            var $children = $folderLi.data('$children').children('.endpoint');
            var checked = $children.filter('.checked').length;
            $folderLi
                .toggleClass('checked', checked > 0 && checked === $children.length)
                .toggleClass('partial', checked > 0 && checked < $children.length);
        };

        var addEndpointRow = function ($target, endPoint, $folderLi) {
            var $li = addRow($target, endPoint, null, 'endpoint')
                .data('endpoint', endPoint)
                .click(function (e) {
                    e.stopPropagation();
                    $(this).toggleClass('checked');
                    if ($folderLi) {
                        updateFolderState($folderLi);
                    }
                });

            if (currentSelection && $.inArray(endPoint, currentSelection) >= 0) {
                $li.addClass('checked');
            }
            return $li;
        };

        var renderedEndpoints = {};

        $.each(Object.keys(folderStructure).sort(), function (i, folderName) {
            var folderEndpoints = $.grep(folderStructure[folderName], function (endPoint) {
                return $.inArray(endPoint, endPoints) >= 0 && !renderedEndpoints[endPoint];
            });
            if (folderEndpoints.length === 0) {
                return;
            }

            var $folderLi = addRow($ul, folderName, folderEndpoints.length + ' endpoint(s)', 'folder')
                .click(function (e) {
                    e.stopPropagation();
                    var check = !$(this).hasClass('checked');
                    $(this).data('$children').children('.endpoint').toggleClass('checked', check);
                    updateFolderState($(this));
                });

            var $children = $("<ul>")
                .addClass('datalinq-code-endpoint-select-children')
                .appendTo($ul)
                .hide();
            $folderLi
                .addClass('collapsed')
                .data('$children', $children);

            $("<div>")
                .addClass('expander')
                .prependTo($folderLi)
                .click(function (e) {
                    e.stopPropagation();
                    $folderLi.toggleClass('collapsed');
                    $children.toggle(!$folderLi.hasClass('collapsed'));
                });

            $.each(folderEndpoints, function (j, endPoint) {
                addEndpointRow($children, endPoint, $folderLi);
                renderedEndpoints[endPoint] = true;
            });

            updateFolderState($folderLi);
        });

        $.each(endPoints, function (i, endPoint) {
            if (!renderedEndpoints[endPoint]) {
                addEndpointRow($ul, endPoint, null);
            }
        });

        $filter.on('keyup', function () {
            var term = ($(this).val() || '').toLowerCase();

            $ul.children('.endpoint').each(function () {
                var $li = $(this);
                $li.toggle(!term || $li.data('endpoint').toLowerCase().indexOf(term) >= 0);
            });
            $ul.children('.folder').each(function () {
                var $folderLi = $(this), $children = $folderLi.data('$children');
                var folderMatch = !term || $folderLi.children('.text').text().toLowerCase().indexOf(term) >= 0;
                var anyChild = false;
                $children.children('.endpoint').each(function () {
                    var match = folderMatch || $(this).data('endpoint').toLowerCase().indexOf(term) >= 0;
                    $(this).toggle(match);
                    anyChild = anyChild || match;
                });
                $folderLi.toggle(anyChild);
                $children.toggle(anyChild && (!!term || !$folderLi.hasClass('collapsed')));
            });
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
                var selection = [];

                $ul.find('.endpoint.checked').each(function (i, li) {
                    var endPoint = $(li).data('endpoint');
                    if ($.inArray(endPoint, selection) < 0) {
                        selection.push(endPoint);
                    }
                });

                $(null).dataLinq_code_modal('close');
                refrehTree($parent, selection);
            });
    };
})(jQuery);
