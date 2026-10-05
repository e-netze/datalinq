/**
 * DataLinq Code - copilot integration (host side)
 *
 * Handles: toggle-copilot (Ctrl+click opens copilot in a tab) and window messages from
 * iframes (selected language, expand-copilot). The chat UI is copilot/copilot-chat.js.
 * Depends on: core
 */
/**
 * Feature "copilot": copilot side panel and iframe messages.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('copilot', function (state) {
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
});
