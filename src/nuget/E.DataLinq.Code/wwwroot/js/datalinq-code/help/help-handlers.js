/**
 * DataLinq Code - help panel
 *
 * Handles: toggle-help (help side panel), toggle-sandbox (opens the DataLinq guide).
 * Depends on: core
 */
/**
 * Feature "help": help panel and sandbox.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('help', function (state) {
        dataLinqCode.events.on('toggle-help', function (channel) {
            var $datalinqBody = $('.datalinq-code-body');
            $datalinqBody.toggleClass('showhelp');

            if ($datalinqBody.hasClass('showhelp')) {
                var $helpFrame = $datalinqBody.find('.datalinq-code-help > #help-frame');
                $helpFrame.one('load', function () {
                    if (this.contentWindow) {
                        this.contentWindow.postMessage({ theme: state.editorTheme }, '*');
                    }
                });
                $helpFrame.attr('src', state.dataLinqEngineUrl + '/help');
            }
        });

        dataLinqCode.events.on('toggle-sandbox', function (channel) {
            window.open(state.dataLinqEngineUrl + "/report/datalinq-guide@select-all-users@index", "_blank");
        });
});
