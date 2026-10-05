/**
 * DataLinq Code - application bootstrap
 *
 * dataLinqCode.start(): creates tree/editor/toolbar, initializes all registered features
 * and renders the UI. dataLinqCode.buildRunUrl(id): engine url to run a document.
 * dataLinqCode.ui.refreshTree(): reloads the tree.
 * Depends on: all core files; must be loaded before the view calls dataLinqCode.start().
 */
(function ($) {
    var state = dataLinqCode._state;

    /**
     * Builds the engine url to run a document (query => /select/, view => /report/).
     * Fires "before-run-document" so features can append url parameters.
     * @param {string} id document id (endpoint@query[@view])
     * @returns {string}
     */
    this.buildRunUrl = function (id) {
        var cmd = id.split('@').length === 3 ? '/report/' : '/select/';
        var url = state.dataLinqEngineUrl + cmd + id;

        var args = { id: id, urlParameters: '' }
        dataLinqCode.events.fire('before-run-document', args);  // collect url parameters
        if (args.urlParameters) {
            url += '?' + args.urlParameters;
        }
        return url;
    };

    /**
     * Starts the IDE.
     * @param {string} targetUrl DataLinq Code backend url
     * @param {string} dataLinqEngineUrl DataLinq engine url
     * @param {string} username logged-in user
     * @param {Object} userPrivileges privileges (see core/privileges.js)
     */
    this.start = function (targetUrl, dataLinqEngineUrl, username, userPrivileges) {
        state.targetUrl = targetUrl;
        state.dataLinqEngineUrl = dataLinqEngineUrl;
        state.username = username;
        state.userPrivileges = userPrivileges || {};

        state.$tree = $('.datalinq-code-tree-container').dataLinqCode_tree({
            $toolbar: $('.datalinq-code-tree-top > .datalinq-code-tree-toolbar')
        });
        state.$editor = $('.datalinq-code-content').dataLinqCode_editor();
        state.$toolbar = $('.datalinq-code-toolbar').dataLinqCode_toolbar();

        $('.datalinq-code-tee-title').click(function () {
            dataLinqCode.ui.refreshTree();
        });

        if (typeof window.CopilotInitializer === "function") {
            window.CopilotInitializer(state.targetUrl);
        }

        state.$editor.dataLinqCode_editor('addTab', { title: 'Start', id: '_start', className: 'start', hideCloseButton: true });

        this.bindDocumentEvents(window.document);

        dataLinqCode.events.on('refresh-ui', function (channel, args) {
            var args = {
                currentDoc: state.$editor.dataLinqCode_editor('currentDoc'),
                dirtyDocs: state.$editor.dataLinqCode_editor('dirtyDocs'),
                gitStatuses: state.gitStatuses
            };

            dataLinqCode.events.fire('refresh-ui-elements', args);
        });

        dataLinqCode.features.initAll(state);

        $('.datalinq-code-tree-collapse-button').click(function (e) {
            e.stopPropagation();
            $(this).closest('.datalinq-code-ide').toggleClass('tree-collapsed');
        });

        $(window).resize(function () {
            dataLinqCode.events.fire('ide-resize');
        });

        dataLinqCode.events.fire('refresh-ui');
    };

    /** Reloads the document tree. */
    this.ui.refreshTree = function () {
        if (state.$tree) {
            state.$tree.dataLinqCode_tree('refresh', {});
        }
    };
}).call(dataLinqCode, jQuery);
