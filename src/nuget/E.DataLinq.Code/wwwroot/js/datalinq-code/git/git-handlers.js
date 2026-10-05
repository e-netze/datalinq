/**
 * DataLinq Code - version control commands
 *
 * Handles: git-status-changed, initialize-git-push, push-snapshot.
 * Git status per document is kept in state.gitStatuses.
 * Depends on: core, ui/dialogs.js, git/git-api.js, git/push-dialog.js
 */
/**
 * Feature "git": track git status and commit/push documents.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('git', function (state) {
        dataLinqCode.events.on('git-status-changed', function (channel, args) {
            state.gitStatuses[args.id] = args.status;
            dataLinqCode.events.fire('refresh-ui');
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
            var id = state.$editor.dataLinqCode_editor('currentDoc');
            var gitStatus = state.gitStatuses[id];

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
});
