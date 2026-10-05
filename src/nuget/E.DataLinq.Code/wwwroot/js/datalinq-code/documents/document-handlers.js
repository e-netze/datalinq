/**
 * DataLinq Code - document commands
 *
 * Handles: save-current-document, verify-current-document, save-all-documents,
 *          delete-document, run-current-document(-in-tab), run-current-document-in-code-tab.
 * Depends on: core, ui/dialogs.js, ui/blockframe.js, documents/documents-api.js
 */
/**
 * Feature "documents": save, verify, delete and run documents.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('documents', function (state) {
        dataLinqCode.events.on('save-current-document', function () {
            var id = state.$editor.dataLinqCode_editor('toSaveDoc');
            if (id) {
                dataLinqCode.events.fire('save-document', { id: id });
            }
        });

        dataLinqCode.events.on('verify-current-document', function () {
            var id = state.$editor.dataLinqCode_editor('currentDoc');
            console.log('verify-current-document',id)
            if (id) {
                dataLinqCode.events.fire('verify-document', { id: id });
            }
        });

        dataLinqCode.events.on('save-all-documents', function () {
            var ids = state.$editor.dataLinqCode_editor('dirtyDocs');
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
            var id = state.$editor.dataLinqCode_editor('currentDoc');
            var url = dataLinqCode.buildRunUrl(id);
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

        dataLinqCode.events.on('run-current-document-in-code-tab', function (channel) {
            var id = state.$editor.dataLinqCode_editor('currentDoc');
            if (!id || id.indexOf('_') === 0 || id.split('@').length !== 3) {
                return;
            }

            dataLinqCode.events.fire('open-view-preview', { id: id, url: dataLinqCode.buildRunUrl(id) });
        });
});
