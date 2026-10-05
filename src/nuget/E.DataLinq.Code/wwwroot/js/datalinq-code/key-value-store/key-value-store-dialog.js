/**
 * DataLinq Code - key/value store dialog
 *
 * dataLinqCode.ui.keyValueStore(): manage secrets and constants per environment.
 * Handles: toggle-key-value-store.
 * Depends on: core, ui/modal.js, ui/dialogs.js, key-value-store/key-value-store-api.js
 */
(function ($) {
        this.keyValueStore = function () {
            $('body').dataLinq_code_modal({
                title: 'Secrets & Constants',
                height: '80%',
                width: '720px',
                id: 'datalinq-code-key-value-store',
                onload: function ($content) {
                    $content.addClass('datalinq-code-key-value-store-content');

                    var selectedEnvironment = null;
                    var sectionReloads = [];

                    var $environmentBar = $("<div>")
                        .addClass('datalinq-code-kvstore-environment')
                        .appendTo($content);

                    $("<span>")
                        .addClass('datalinq-code-kvstore-environment-label')
                        .text('Environment:')
                        .appendTo($environmentBar);

                    var $environmentButtons = $("<div>")
                        .addClass('datalinq-code-kvstore-environment-buttons')
                        .appendTo($environmentBar);

                    var selectEnvironment = function (environment) {
                        selectedEnvironment = environment;
                        $environmentButtons
                            .find('.datalinq-code-kvstore-environment-button')
                            .each(function () {
                                $(this).toggleClass(
                                    'selected',
                                    $(this).data('environment') === selectedEnvironment);
                            });
                        $content.find('.datalinq-code-kvstore-editor').remove();
                        $.each(sectionReloads, function (i, reload) {
                            reload();
                        });
                    };

                    dataLinqCode.api.getKeyValueStoreEnvironments(function (result) {
                        var environments = (result && result.environments) || [];

                        $.each(environments, function (i, environment) {
                            $("<button>")
                                .attr('type', 'button')
                                .addClass('datalinq-code-button datalinq-code-kvstore-environment-button')
                                .data('environment', environment)
                                .text(environment)
                                .appendTo($environmentButtons)
                                .click(function () {
                                    selectEnvironment(environment);
                                });
                        });

                        selectEnvironment((result && result.current) || environments[0] || null);
                    });

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

                            if (!selectedEnvironment) {
                                return;
                            }

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
                                                    }, selectedEnvironment);
                                                });
                                        });
                                });
                            }, selectedEnvironment);
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
                                }, selectedEnvironment);
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
                                    }, selectedEnvironment);
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

                        sectionReloads.push(reload);
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
}).call(dataLinqCode.ui, jQuery);

/**
 * Feature "key-value-store": opens the key/value store dialog.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('key-value-store', function (state) {
        dataLinqCode.events.on('toggle-key-value-store', function (channel) {
            dataLinqCode.ui.keyValueStore();
        });
});
