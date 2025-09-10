var dataLinqCode = new function ($) {
    var _targetUrl, _dataLinqEngineUrl, _username, _userPrivileges;
    var _editorTheme = 'vs-dark';

    var $tree, $editor, $toolbar;

    this.targetUrl = () => _targetUrl;
    this.loginUsername = () => _username;
    this.editorTheme = () => _editorTheme;

    this.start = function (targetUrl, dataLinqEngineUrl, username, userPrivileges) {
        _targetUrl = targetUrl;
        _dataLinqEngineUrl = dataLinqEngineUrl;
        _username = username;
        _userPrivileges = userPrivileges || {};

        $tree = $('.datalinq-code-tree-container').dataLinqCode_tree({
            $toolbar: $('.datalinq-code-tree-top > .datalinq-code-tree-toolbar')
        });
        $editor = $('.datalinq-code-content').dataLinqCode_editor();
        $toolbar = $('.datalinq-code-toolbar').dataLinqCode_toolbar();

        $('.datalinq-code-tee-title').click(function () {
            dataLinqCode.ui.refreshTree();
        });

        if (typeof window.CopilotInitializer === "function") {
            window.CopilotInitializer(_targetUrl);
        }

        $editor.dataLinqCode_editor('addTab', { title: 'Start', id: '_start', className: 'start', hideCloseButton: true });

        this.bindDocumentEvents(window.document);

        window.addEventListener('message', (event) => {
            if (event.data.lang) {
                sessionStorage.setItem('selectedLang', event.data.lang);
            }
            if (event.data.action === 'expand-copilot')
            {
                const copilotTab = document.querySelector('[data-id="copilot"]');
                if (copilotTab) {
                    copilotTab.querySelector('.close-button').click();
                } else {
                    dataLinqCode.events.fire('open-copilot', {});
                }

                dataLinqCode.events.fire('toggle-copilot');
            }
        });

        function clearSessionData() {
            sessionStorage.removeItem('currentChatId');
        }

        window.addEventListener('beforeunload', clearSessionData);
        window.addEventListener('unload', clearSessionData);
        window.addEventListener('pagehide', clearSessionData);

        dataLinqCode.events.on('refresh-ui', function (channel, args) {
            var args = {
                currentDoc: $editor.dataLinqCode_editor('currentDoc'),
                dirtyDocs: $editor.dataLinqCode_editor('dirtyDocs')
            };

            dataLinqCode.events.fire('refresh-ui-elements', args);
        });

        dataLinqCode.events.on('save-current-document', function () {
            var id = $editor.dataLinqCode_editor('toSaveDoc');
            if (id) {
                dataLinqCode.events.fire('save-document', { id: id });
            }
        });

        dataLinqCode.events.on('verify-current-document', function () {
            var id = $editor.dataLinqCode_editor('currentDoc');
            console.log('verify-current-document',id)
            if (id) {
                dataLinqCode.events.fire('verify-document', { id: id });
            }
        });

        dataLinqCode.events.on('save-all-documents', function () {
            var ids = $editor.dataLinqCode_editor('dirtyDocs');
            $.each(ids, function (i, id) {
                dataLinqCode.events.fire('save-document', { id: id });
            });
        });

        dataLinqCode.events.on('delete-document', function (channel, args) {
            var ids = args.id.split('@');

            var fireDeleted = function (result, args) {
                if (result.success) {
                    dataLinqCode.events.fire('document-deleted', args);
                } else {
                    dataLinqCode.ui.alert("Error", (result.error_message || 'Unknown error'));
                }
            }

            if (ids.length === 1) {
                dataLinqCode.ui.confirm('Delete endpoint', 'Delete endpoint ' + args.id + ' permanently?',
                    function () {
                        dataLinqCode.api.deleteEndPoint(ids[0], function (result) {
                            fireDeleted(result, args);
                        });
                    });
            }
            else if (ids.length === 2) {
                dataLinqCode.ui.confirm('Delete query', 'Delete query ' + args.id + ' permanently?',
                    function () {
                        dataLinqCode.api.deleteQuery(ids[0], ids[1], function (result) {
                            fireDeleted(result, args);
                        });
                    });
            }
            else if (ids.length === 3) {
                dataLinqCode.ui.confirm('Delete view', 'Delete view ' + args.id + ' permanently?',
                    function () {
                        dataLinqCode.api.deleteView(ids[0], ids[1], ids[2], function (result) {
                            fireDeleted(result, args);
                        });
                    });
            }
        });

        dataLinqCode.events.on(['run-current-document-in-tab', 'run-current-document'], function (channel) {
            var id = $editor.dataLinqCode_editor('currentDoc');

            var cmd = id.split('@').length === 3 ? '/report/' : '/select/';
            var url = _dataLinqEngineUrl + cmd + id;

            var args = { id: id, urlParameters: '' }
            dataLinqCode.events.fire('before-run-document', args);  // collect url parameters
            if (args.urlParameters) {
                url += '?' + args.urlParameters;
            }
            //console.log(channel, url);

            if (channel.channel === 'run-current-document-in-tab') {
                window.open(url);
            } else {
                $('body').dataLinqCode_blockframe({
                    onShow: function ($content) {
                        $("<iframe>")
                            .attr('src', url)
                            .css({
                                position: 'absolute',
                                left: 0, right: 0, top: 0, bottom: 0,
                                width: '100%', height: '100%',
                                border: 'none'
                            })
                            .appendTo($content);
                    }
                });
            }
        });

        dataLinqCode.events.on('toggle-color-scheme', function (channel) {
            const ide = $('.datalinq-code-ide');
            ide.toggleClass('colorscheme-light');
            _editorTheme = ide.hasClass('colorscheme-light') ? 'vs' : 'vs-dark';

            dataLinqCode.events.fire('theme-changed', {
                theme: _editorTheme
            });

            sessionStorage.setItem('editorTheme', _editorTheme);

            ide.find('iframe').each(function () {
                this.contentWindow.postMessage({ theme: _editorTheme }, '*');
            });
        }, this);


        dataLinqCode.events.on('toggle-help', function (channel) {
            var $datalinqBody = $('.datalinq-code-body');
            $datalinqBody.toggleClass('showhelp');

            if ($datalinqBody.hasClass('showhelp')) {
                $datalinqBody.find('.datalinq-code-help > #help-frame').attr('src', _dataLinqEngineUrl + '/help');
            }
        });

        var ctrlPressed = false;

        $(document).keydown(function (e) {
            if (e.key === "Control") ctrlPressed = true;
        }).keyup(function (e) {
            if (e.key === "Control") ctrlPressed = false;
        });

        dataLinqCode.events.on('toggle-copilot', function (channel, args) {
            if (ctrlPressed) {
                dataLinqCode.events.fire('open-copilot', {});
            } else {
                var $datalinqBody = $('.datalinq-code-body');
                $datalinqBody.toggleClass('showhelp');

                if ($datalinqBody.hasClass('showhelp')) {
                    $datalinqBody.find('.datalinq-code-help > #help-frame').attr('src', dataLinqCode.targetUrl() + '/copilot?dl_token=' + window._datalinqCodeAccessToken);
                }
            }
        });

        dataLinqCode.events.on('toggle-sandbox', function (channel) {
            window.open(_dataLinqEngineUrl + "/report/datalinq-guide@select-all-users@index", "_blank");
        });

        dataLinqCode.events.on('logout', function (channel) {
            document.location = document.location + '/logout';
        });

        $('.datalinq-code-tree-collapse-button').click(function (e) {
            e.stopPropagation();
            $(this).closest('.datalinq-code-ide').toggleClass('tree-collapsed');
        });

        const savedTheme = localStorage.getItem('editorColorScheme');
        if (savedTheme === 'vs') {
            dataLinqCode.events.fire('toggle-color-scheme');
        } 

        $(window).resize(function () {
            dataLinqCode.events.fire('ide-resize');
        });

        dataLinqCode.events.fire('refresh-ui');
    };

    this.implementEventController = function (obj) {
        obj.events = new dataLinqCode.eventController(obj);
    };

    this.api = new function () {
        this.get = function (route, callback, data) {
            $.ajax({
                url: dataLinqCode.targetUrl() + '/' + route,
                data: data,
                headers: {
                    'Authorization': 'Bearer ' + window._datalinqCodeAccessToken
                },
                success: function (result) {
                    callback(result)
                }
            });
        };

        this.getMonacoSnippit = function (callback, lang) {
            this.get('getMonacoSnippit', callback, { lang: lang });
        };

        this.getEndPointPrefixes = function (callback) {
            this.get('getEndPointPrefixes', callback);
        };
        this.getEndPoints = function (filters, callback) {
            let filtersArg = '';

            if (Array.isArray(filters)) {
                if (filters.length === 0) {
                    return callback([]);
                }
                filtersArg = filters.toString();
            }

            this.get('getEndPoints?filters=' + filtersArg, callback)
        };
        this.getQueries = function (endPoint, callback) {
            this.get('getQueries?endPoint=' + endPoint, callback);
        };
        this.getViews = function (endPoint, query, callback) {
            this.get('getViews?endPoint=' + endPoint + '&query=' + query, callback);
        };

        this.createEndPoint = function (endPoint, callback) {
            this.get('createEndPoint', callback, { endPoint: endPoint });
        };
        this.createQuery = function (endPoint, query, callback) {
            this.get('createEndPointQuery', callback, { endPoint: endPoint, query: query });
        };
        this.createView = function (endPoint, query, view, callback) {
            this.get('createEndPointQueryView', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.deleteEndPoint = function (endPoint, callback) {
            this.get('deleteEndPoint', callback, { endPoint: endPoint });
        };
        this.deleteQuery = function (endPoint, query, callback) {
            this.get('deleteEndPointQuery', callback, { endPoint: endPoint, query: query });
        };
        this.deleteView = function (endPoint, query, view, callback) {
            this.get('deleteEndPointQueryView', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.verifyView = function (endPoint, query, view, callback) {
            this.get('verifyEndPointQueryView', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.docInfo = function (endPoint, query, view, rewrite, callback) {
            this.get('docInfo', callback, { endPoint: endPoint, query: query || '', view: view || '', rewrite: rewrite });
        }
    };

    this.privileges = new function () {
        this.createEndpoints = function () { return _userPrivileges.createEndpoints === true; };
        this.createQueries = function () { return _userPrivileges.createQueries === true; };
        this.createViews = function () { return _userPrivileges.createViews === true; };
        this.deleteEndpoints = function () { return _userPrivileges.deleteEndpoints === true; };
        this.deleteQueries = function () { return _userPrivileges.deleteQueries === true; };
        this.deleteViews = function () { return _userPrivileges.deleteViews === true; };
        this.useAppPrefixFilters = function () { return _userPrivileges.useAppPrefixFilters === true; };
    };

    this.timer = function (callback, duration, arg) {
        var _timer = 0;
        var _callback = callback;
        var _duration = duration;
        var _arg = arg;
        this.SetArgument = function (arg) { _arg = arg; };
        this.SetDuration = function (d) {
            _duration = d;
        };
        this.Duration = function () { return _duration; };
        this.Start = function (arg) {
            window.clearTimeout(_timer);
            if (arg)
                _arg = arg;
            if (_duration == 0) {
                if (_arg)
                    _callback(_arg);
                else
                    _callback();
            }
            else {
                if (_arg)
                    _timer = window.setTimeout(function () { _callback(_arg); }, _duration);
                else
                    _timer = window.setTimeout(_callback, _duration);
            }
        };
        this.StartWith = function (callbackFunction) {
            _callback = callbackFunction;
            this.Start();
        };
        this.Stop = function () { window.clearTimeout(_timer); };
        this.Exec = function () {
            window.clearTimeout(_timer);
            if (_arg)
                _callback(_arg);
            else
                _callback();
        };
        this.start = this.Start;
        this.stop = this.Stop;
        this.startWidth = this.StartWidth;
        this.exec = this.Exec;
    };

    this.delayed = function (callback, duration, arg) {
        var timer = new dataLinqCode.timer(callback, duration ? duration : 1, arg);
        timer.Start();
    };

    this.allDocuments = function () {
        var documents = [];

        $tree.find('.tree-node').each(function (i, node) {
            var $node = $(node);
            var route = $node.data('data-route');
            if (!$node.hasClass('add') && route) {
                documents.push({
                    id: route,
                    isOpen: $editor.dataLinqCode_editor('isOpen', { id: route })
                });
            }
        });

        return documents;
    };

    // App Prefix
    var _appPrefixFilters = null;
    this.getAppPrefixFilters = function () {
        return dataLinqCode.privileges.useAppPrefixFilters() ? _appPrefixFilters : null;
    };
    this.setAppPrefixFilters = function (prefixes) {
        if (dataLinqCode.privileges.useAppPrefixFilters()) {
            _appPrefixFilters = Array.isArray(prefixes) ? prefixes : null;

            console.log('setAppPrefixFilters', _appPrefixFilters);
        }
    };
    this.addAppFilterPrefix = function (prefix) {
        if (dataLinqCode.privileges.useAppPrefixFilters()) {
            _appPrefixFilters = _appPrefixFilters || [];
            if ($.inArray(prefix, _appPrefixFilters) < 0) {
                _appPrefixFilters.push(prefix);
            }
        }
    };
    this.addAppFilterPrefixIfCurrentlyUsed = function (prefix) {
        if (_appPrefixFilters === null)
            return;

        return this.addAppFilterPrefix(prefix);
    };

    this.bindDocumentEvents = function (doc) {
        $(doc).bind("keyup keydown", function (e) {
            if (e.ctrlKey && e.shiftKey && e.which == 83) { // Ctrl + Shift + s
                if (e.type == 'keyup') {
                    e.stopPropagation();
                    dataLinqCode.events.fire('save-all-documents');
                }
                return false;
            }
            if (e.ctrlKey && e.which == 83) {  // Ctrl + s
                if (e.type == 'keyup') {
                    e.stopPropagation();
                    dataLinqCode.events.fire('save-current-document');
                }
                return false;
            }

            if (e.which === 116) { // F5 ( Ctrl + F5 )
                if (e.type == 'keyup') {
                    e.stopPropagation();
                    dataLinqCode.events.fire(e.ctrlKey ? 'run-current-document-in-tab' : 'run-current-document');
                }
                return false;
            }

            if (e.key === "Escape") {
                $('body').dataLinqCode_blockframe('close');
                return false;
            }
        });
    };

    this.ui = new function () {
        this.alert = function (title, message) {
            $('body').dataLinq_code_modal({
                title: title,
                height: '200px',
                width: '640px',
                id: 'datalinq-code-alert',
                onload: function ($content) {
                    $("<p>")
                        .text(message)
                        .appendTo($content.addClass('datalinq-code-messagebox-content'));

                    var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);

                    $("<button>")
                        .addClass("datalinq-code-button")
                        .text("OK")
                        .appendTo($buttonbar)
                        .click(function () {
                            $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });
                        });
                }
            });
        };

        this.confirm = function (title, message, onConfirm) {
            dataLinqCode.ui.confirmIf(true, title, message, onConfirm);
        }

        this.confirmIf = function (contition, title, message, onConfirm) {
            if (contition === false) {
                if (onConfirm) {
                    onConfirm();
                }
                return;
            }

            $('body').dataLinq_code_modal({
                title: title,
                height: '200px',
                width: '640px',
                id: 'datalinq-code-alert',
                onload: function ($content) {
                    $("<p>")
                        .text(message)
                        .appendTo($content.addClass('datalinq-code-messagebox-content'));

                    var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);

                    $("<button>")
                        .addClass("datalinq-code-button cancel")
                        .text("No")
                        .appendTo($buttonbar)
                        .click(function () {
                            $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });
                        });

                    $("<button>")
                        .addClass("datalinq-code-button")
                        .text("Yes")
                        .appendTo($buttonbar)
                        .click(function () {
                            if (onConfirm) {
                                onConfirm();
                            }

                            $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });
                        });
                }
            });
        }

        this.refreshTree = function () {
            if ($tree) {
                $tree.dataLinqCode_tree('refresh', {});
            }
        };

        window.addEventListener('beforeunload', function () {
            // save last opened tabs to localstorage to possible restore them on next login
            const tabs = document.querySelectorAll('.datalinq-code-tab[data-id]');
            const tabIds = Array.from(tabs)
                .map(tab => tab.getAttribute('data-id'))
                .filter(id => id !== '_start');
            localStorage.setItem('datalinq-open-tabs', JSON.stringify(tabIds));

            // save the individual width of sidebar of user
            const sidebarWidth = getComputedStyle(document.documentElement).getPropertyValue('--sidebar-width').trim();
            if (sidebarWidth !== '300px') {
                localStorage.setItem('sidebarWidth', sidebarWidth);
            } else {
                localStorage.removeItem('sidebarWidth'); // Optional: clear if default
            }

            //save current editor theme
            const ide = document.querySelector('.datalinq-code-ide');

            if (ide && ide.classList.contains('colorscheme-light')) {
                localStorage.setItem('editorColorScheme', 'vs');
            } else {
                localStorage.removeItem('editorColorScheme');
            }

        });

    };
}(jQuery);