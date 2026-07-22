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

        var _gitStatuses = {};

        dataLinqCode.events.on('refresh-ui', function (channel, args) {
            var args = {
                currentDoc: $editor.dataLinqCode_editor('currentDoc'),
                dirtyDocs: $editor.dataLinqCode_editor('dirtyDocs'),
                gitStatuses: _gitStatuses
            };

            dataLinqCode.events.fire('refresh-ui-elements', args);
        });

        dataLinqCode.events.on('git-status-changed', function (channel, args) {
            _gitStatuses[args.id] = args.status;
            dataLinqCode.events.fire('refresh-ui');
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
            localStorage.setItem('editorColorScheme', _editorTheme);

            ide.find('iframe').each(function () {
                this.contentWindow.postMessage({ theme: _editorTheme }, '*');
            });

            const helpFrame = document.getElementById('help-frame');
            if (helpFrame && helpFrame.contentWindow) {
                helpFrame.contentWindow.postMessage({ theme: _editorTheme }, '*');
            }
        }, this);


        dataLinqCode.events.on('toggle-help', function (channel) {
            var $datalinqBody = $('.datalinq-code-body');
            $datalinqBody.toggleClass('showhelp');

            if ($datalinqBody.hasClass('showhelp')) {
                var $helpFrame = $datalinqBody.find('.datalinq-code-help > #help-frame');
                $helpFrame.one('load', function () {
                    if (this.contentWindow) {
                        this.contentWindow.postMessage({ theme: _editorTheme }, '*');
                    }
                });
                $helpFrame.attr('src', _dataLinqEngineUrl + '/help');
            }
        });

        dataLinqCode.events.on('toggle-key-value-store', function (channel) {
            dataLinqCode.ui.keyValueStore();
        });

        dataLinqCode.events.on('initialize-git-push', function (channel) {
            dataLinqCode.ui.confirmPromised(
                "Initializing Git",
                "This should only be done once. Are you sure?",
                function () {

                    dataLinqCode.api.initializeGitRepository(function (result) {

                        $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });

                        dataLinqCode.ui.Accept(
                            "Version Control",
                            result.Success ? result.Message : result.Error
                        );
                    });
                }
            );
        });

        dataLinqCode.events.on('push-snapshot', function (channel) {
            var id = $editor.dataLinqCode_editor('currentDoc');
            var gitStatus = _gitStatuses[id];

            if (gitStatus == "up-to-date")
                dataLinqCode.ui.Accept('Version Control', 'The file ' + id + ' already is up to date');

            if (gitStatus == "outdated")
                dataLinqCode.ui.pushMenu('Version Control', id,
                    function (name, message) {

                        var details = {
                            id: id,
                            name: name,
                            message: message
                        }

                        dataLinqCode.api.commitAndPushChanges(details, function (result) {
                            $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });

                            dataLinqCode.ui.Accept(
                                "Version Control",
                                result.Success ? result.Message : result.Error
                            );

                            if (result.Success) {
                                dataLinqCode.events.fire('git-status-changed', {
                                    id: id,
                                    status: 'up-to-date'
                                }); 
                            }
                        });
                    },
                    function () {
                        $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });
                        dataLinqCode.ui.Accept('Version Control', 'The commit and push process got canceled');
                    });
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

        this.post = function (route, data, callback) {
            $.ajax({
                url: dataLinqCode.targetUrl() + '/' + route,
                type: 'POST',
                data: JSON.stringify(data),
                contentType: 'application/json',
                headers: {
                    'Authorization': 'Bearer ' + window._datalinqCodeAccessToken
                },
                success: function (result) {
                    callback(result)
                }
            });
        };

        this.saveFolderStructure = function (folderStructure, callback) {
            this.post('saveFolderStructure', folderStructure, callback);
        };

        this.commitAndPushChanges = function (details, callback) {
            this.post('commitAndPushChanges', details, callback);
        };

        this.getFolderStructure = function (callback) {
            this.get('getFolderStructure', callback);
        };

        this.getMonacoSnippit = function (callback, lang, helper) {
            this.get('getMonacoSnippit', callback, { lang: lang, helper: helper || 'dlh' });
        };

        this.getSecretKeys = function (callback) {
            this.get('getSecretKeys', callback);
        };
        this.setSecret = function (key, value, callback) {
            this.post('setSecret', { key: key, value: value }, callback);
        };
        this.deleteSecret = function (key, callback) {
            this.post('deleteSecret', { key: key }, callback);
        };

        this.getConstantKeys = function (callback) {
            this.get('getConstantKeys', callback);
        };
        this.getConstantValue = function (key, callback) {
            this.get('getConstantValue', callback, { key: key });
        };
        this.setConstant = function (key, value, callback) {
            this.post('setConstant', { key: key, value: value }, callback);
        };
        this.deleteConstant = function (key, callback) {
            this.post('deleteConstant', { key: key }, callback);
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

        this.checkGitStatus = function (endPoint, query, view, callback) {
            this.get('checkGitStatus', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.initializeGitRepository = function (callback) {
            this.get('initializeGitRepository', callback);
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

        $tree.find('.tree-node').not('.folder').each(function (i, node) {
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

        this.keyValueStore = function () {
            $('body').dataLinq_code_modal({
                title: 'Secrets & Constants',
                height: '80%',
                width: '720px',
                id: 'datalinq-code-key-value-store',
                onload: function ($content) {
                    $content.addClass('datalinq-code-key-value-store-content');

                    var buildSection = function (options) {
                        var $section = $("<div>")
                            .addClass('datalinq-code-kvstore-section')
                            .appendTo($content);

                        $("<div>")
                            .addClass('datalinq-code-kvstore-title')
                            .text(options.title)
                            .appendTo($section);

                        if (options.description) {
                            $("<div>")
                                .addClass('datalinq-code-kvstore-description')
                                .text(options.description)
                                .appendTo($section);
                        }

                        var $list = $("<div>")
                            .addClass('datalinq-code-kvstore-list')
                            .appendTo($section);

                        var reload = function () {
                            $list.empty();
                            options.getKeys(function (result) {
                                var keys = (result && result.keys) || [];

                                if (keys.length === 0) {
                                    $("<div>")
                                        .addClass('datalinq-code-kvstore-empty')
                                        .text('No entries yet.')
                                        .appendTo($list);
                                    return;
                                }

                                $.each(keys, function (i, key) {
                                    var $row = $("<div>")
                                        .addClass('datalinq-code-kvstore-row')
                                        .appendTo($list);

                                    $("<div>")
                                        .addClass('datalinq-code-kvstore-key')
                                        .text(key)
                                        .appendTo($row);

                                    var $actions = $("<div>")
                                        .addClass('datalinq-code-kvstore-actions')
                                        .appendTo($row);

                                    $("<button>")
                                        .addClass('datalinq-code-button')
                                        .text('Edit')
                                        .appendTo($actions)
                                        .click(function () {
                                            editEntry(key);
                                        });

                                    $("<button>")
                                        .addClass('datalinq-code-button cancel')
                                        .text('Delete')
                                        .appendTo($actions)
                                        .click(function () {
                                            dataLinqCode.ui.confirm(
                                                options.title,
                                                'Delete "' + key + '"?',
                                                function () {
                                                    options.deleteValue(key, function () {
                                                        reload();
                                                    });
                                                });
                                        });
                                });
                            });
                        };

                        var editEntry = function (existingKey) {
                            var isNew = !existingKey;

                            // Only allow a single editor open at a time across the whole modal.
                            $content.find('.datalinq-code-kvstore-editor').remove();

                            var $editor = $("<div>")
                                .addClass('datalinq-code-kvstore-editor')
                                .appendTo($section);

                            var $keyInput = $("<input>")
                                .attr('type', 'text')
                                .attr('placeholder', 'Key')
                                .addClass('datalinq-code-kvstore-input')
                                .val(existingKey || '')
                                .prop('disabled', !isNew)
                                .appendTo($editor);

                            var $valueInput = $("<input>")
                                .attr('type', options.maskValue ? 'password' : 'text')
                                .attr('placeholder', options.maskValue ? 'Value (write-only)' : 'Value')
                                .addClass('datalinq-code-kvstore-input')
                                .appendTo($editor);

                            if (!isNew && options.getValue) {
                                options.getValue(existingKey, function (result) {
                                    $valueInput.val((result && result.value) || '');
                                });
                            }

                            var $editorActions = $("<div>")
                                .addClass('datalinq-code-kvstore-actions')
                                .appendTo($editor);

                            $("<button>")
                                .addClass('datalinq-code-button')
                                .text('Save')
                                .appendTo($editorActions)
                                .click(function () {
                                    var key = $.trim($keyInput.val());
                                    if (!key) {
                                        dataLinqCode.ui.alert(options.title, 'Key must not be empty.');
                                        return;
                                    }

                                    options.setValue(key, $valueInput.val(), function () {
                                        $editor.remove();
                                        reload();
                                    });
                                });

                            $("<button>")
                                .addClass('datalinq-code-button cancel')
                                .text('Cancel')
                                .appendTo($editorActions)
                                .click(function () {
                                    $editor.remove();
                                });
                        };

                        $("<button>")
                            .addClass('datalinq-code-button datalinq-code-kvstore-add')
                            .text('Add ' + options.itemName)
                            .appendTo($section)
                            .click(function () {
                                editEntry(null);
                            });

                        reload();
                    };

                    buildSection({
                        title: 'Secrets (encrypted)',
                        description: 'Values are stored encrypted and are never displayed. Use @SECURITY.GetSecret("key") to read them.',
                        itemName: 'Secret',
                        maskValue: true,
                        getKeys: dataLinqCode.api.getSecretKeys.bind(dataLinqCode.api),
                        setValue: dataLinqCode.api.setSecret.bind(dataLinqCode.api),
                        deleteValue: dataLinqCode.api.deleteSecret.bind(dataLinqCode.api)
                    });

                    buildSection({
                        title: 'Constants (plain text)',
                        description: 'Values are stored unencrypted. Use @DLH.GetConstant("key") to read them.',
                        itemName: 'Constant',
                        maskValue: false,
                        getKeys: dataLinqCode.api.getConstantKeys.bind(dataLinqCode.api),
                        getValue: dataLinqCode.api.getConstantValue.bind(dataLinqCode.api),
                        setValue: dataLinqCode.api.setConstant.bind(dataLinqCode.api),
                        deleteValue: dataLinqCode.api.deleteConstant.bind(dataLinqCode.api)
                    });
                }
            });
        }

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

        this.Accept = function (title, message) {
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
                        .text("Ok")
                        .appendTo($buttonbar)
                        .click(function () {                      
                            $('body').dataLinq_code_modal('close', { id: 'datalinq-code-alert' });
                        });
                }
            });
        }

        this.pushMenu = function (title, message, onConfirm, onDecline) {
            $('body').dataLinq_code_modal({
                title: title,
                height: '400px',
                width: '500px',
                id: 'datalinq-code-alert',
                onload: function ($content) {
                    var $formDiv = $("<div>").addClass('datalinq-code-modal-commit-form');

                    $("<p>")
                        .text('File: ' + message)
                        .appendTo($formDiv);

                    $("<label>")
                        .attr('for', 'username-input')
                        .text('Username:')
                        .appendTo($formDiv);

                    $("<input>")
                        .attr({
                            type: 'text',
                            placeholder: 'Username',
                            id: 'username-input',
                            value: dataLinqCode.loginUsername() || '???'
                        })
                        .addClass('datalinq-code-modal-input')
                        .appendTo($formDiv);

                    $("<br>")
                        .appendTo($formDiv);

                    $("<label>")
                        .attr('for', 'commit-message-input')
                        .text('Commit message:')
                        .appendTo($formDiv);

                    $("<textarea>")
                        .attr({
                            placeholder: 'Commit message',
                            id: 'commit-message-input',
                            rows: 6
                        })
                        .css('resize', 'none')
                        .addClass('datalinq-code-modal-input')
                        .appendTo($formDiv);

                    $formDiv.appendTo($content.addClass('datalinq-code-messagebox-content'));

                    var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);

                    $("<button>")
                        .addClass("datalinq-code-button cancel")
                        .text("Cancle")
                        .appendTo($buttonbar)
                        .click(function () {
                            if (onDecline) {
                                onDecline();
                            }
                        });

                    $("<button>")
                        .addClass("datalinq-code-button")
                        .text("Commit & Push")
                        .appendTo($buttonbar)
                        .click(function () {
                            if (onConfirm) {
                                var username = $('#username-input').val();
                                var commitMessage = $('#commit-message-input').val();
                                if (username && commitMessage) {
                                    onConfirm(username, commitMessage);
                                }     
                            } 
                        });
                }
            }); 
        }

        this.confirmPromised = function (title, message, onConfirm, onDecline) {
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
                            if (onDecline) {
                                onDecline();
                            }
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
                        });
                }
            });
        };


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