/**
 * DataLinq Code - settings dialog
 *
 * dataLinqCode.ui.settings(): IDE settings modal. Handles: toggle-settings.
 * Depends on: core, ui/modal.js
 */
(function ($) {
        this.settings = function () {
            var modalId = 'datalinq-code-settings';

            var features = window.datalinqFeatures || {};

            var settingsItems = [
                {
                    key: 'colorscheme',
                    title: 'Color Scheme',
                    description: 'Toggle between the light and dark editor theme.',
                    action: function () { dataLinqCode.events.fire('toggle-color-scheme'); }
                },
                {
                    key: 'key-value-store',
                    title: 'Secrets & Constants',
                    description: 'Manage secrets and constants for your environments.',
                    action: function () { dataLinqCode.ui.keyValueStore(); }
                },
                {
                    key: 'error-page',
                    title: 'Error Page',
                    description: 'Customize the error page shown to end users.',
                    action: function () { dataLinqCode.ui.errorPage(); }
                }
            ];

            if (features.Sandbox) {
                settingsItems.push({
                    key: 'sandbox',
                    title: 'DataLinq Sandbox',
                    description: 'Open the DataLinq sandbox guide in a new tab.',
                    action: function () { dataLinqCode.events.fire('toggle-sandbox'); }
                });
            }

            $('body').dataLinq_code_modal({
                title: 'Settings',
                height: '440px',
                width: '480px',
                id: modalId,
                onload: function ($content) {
                    $content.addClass('datalinq-code-settings-content');

                    var $list = $("<div>")
                        .addClass('datalinq-code-settings-list')
                        .appendTo($content);

                    $.each(settingsItems, function (i, item) {
                        var $item = $("<div>")
                            .addClass('datalinq-code-settings-item')
                            .appendTo($list)
                            .click(function () {
                                $('body').dataLinq_code_modal('close', { id: modalId });
                                item.action();
                            });

                        $("<div>")
                            .addClass('datalinq-code-settings-icon ' + item.key)
                            .appendTo($item);

                        var $text = $("<div>")
                            .addClass('datalinq-code-settings-text')
                            .appendTo($item);

                        $("<div>").addClass('title').text(item.title).appendTo($text);
                        $("<div>").addClass('desc').text(item.description).appendTo($text);
                    });
                }
            });
        };
}).call(dataLinqCode.ui, jQuery);

/**
 * Feature "settings": opens the settings dialog.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('settings', function (state) {
        dataLinqCode.events.on('toggle-settings', function (channel) {
            dataLinqCode.ui.settings();
        });
});
