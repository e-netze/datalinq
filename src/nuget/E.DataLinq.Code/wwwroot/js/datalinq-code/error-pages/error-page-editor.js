/**
 * DataLinq Code - error page editor
 *
 * dataLinqCode.ui.errorPage(): modal to edit, preview, create and delete error pages.
 * Handles: edit-error-page.
 * Depends on: core, ui/modal.js, ui/dialogs.js
 */
(function ($) {
        this.errorPage = function () {
            var modalId = 'datalinq-code-error-page';
            var globalName = 'global';
            var currentName = globalName;

            var displayName = function (name) {
                return name === globalName ? 'Global Error Page' : 'Error Page: ' + name;
            };

            $('body').dataLinq_code_modal({
                title: displayName(globalName),
                height: 'calc(80% + 10px)',
                width: '80%',
                id: modalId,
                onload: function ($content) {
                    $content.addClass('datalinq-code-error-page-content');

                    var $toolbar = $("<div>")
                        .addClass('datalinq-code-error-page-toolbar')
                        .appendTo($content);

                    var $select = $("<select>")
                        .addClass('datalinq-code-error-page-select')
                        .appendTo($toolbar);

                    var $newButton = $("<button>")
                        .addClass('datalinq-code-button')
                        .text('New...')
                        .appendTo($toolbar);

                    var $deleteButton = $("<button>")
                        .addClass('datalinq-code-button')
                        .text('Delete')
                        .appendTo($toolbar);

                    var $previewButton = $("<button>")
                        .addClass('datalinq-code-button align-right')
                        .text('Preview')
                        .appendTo($toolbar);

                    var $saveButton = $("<button>")
                        .addClass('datalinq-code-button')
                        .text('Save')
                        .appendTo($toolbar);

                    var $frame = $("<iframe>")
                        .addClass('datalinq-code-error-page-frame')
                        .attr('frameborder', '0')
                        .css({ width: '100%', height: 'calc(100% - 50px)' })
                        .appendTo($content);

                    var isDirty = false;

                    var updateDirtyIndicator = function () {
                        var $titleElement = $('body')
                            .dataLinq_code_modal('title', { id: modalId });

                        var titleText = displayName(currentName) + (isDirty ? ' \u25CF (unsaved changes)' : '');

                        if ($titleElement && $titleElement.length) {
                            $titleElement.text(titleText);
                        }

                        $content.toggleClass('datalinq-code-editor-dirty', isDirty);
                    };

                    var setupFrameHooks = function () {
                        var frameWindow = $frame[0].contentWindow;

                        if (frameWindow) {
                            frameWindow.onErrorPageDirtyChanged = function (dirty) {
                                isDirty = dirty;
                                updateDirtyIndicator();
                            };
                        }
                    };

                    $frame.on('load', setupFrameHooks);

                    var frameUrl = function (name) {
                        return dataLinqCode.targetUrl() +
                            '/EditErrorPage?name=' + encodeURIComponent(name) +
                            '&dl_token=' + window._datalinqCodeAccessToken;
                    };

                    var loadPage = function (name) {
                        currentName = name || globalName;
                        isDirty = false;

                        $select.val(currentName);
                        // the global page is the system wide fallback => it can never be deleted
                        $deleteButton.toggle(currentName !== globalName);

                        $frame.attr('src', frameUrl(currentName));

                        updateDirtyIndicator();
                    };

                    var fillNames = function (names, selectName) {
                        $select.empty();

                        $.each(names || [globalName], function (i, name) {
                            $("<option>").attr('value', name).text(displayName(name)).appendTo($select);
                        });

                        loadPage(selectName || currentName);
                    };

                    var refreshNames = function (selectName) {
                        $.ajax({
                            url: dataLinqCode.targetUrl() + '/ErrorPageNames?dl_token=' + window._datalinqCodeAccessToken,
                            type: 'get',
                            success: function (names) { fillNames(names, selectName); },
                            error: function () { fillNames([globalName], selectName); }
                        });
                    };

                    var confirmUnsaved = function (onContinue) {
                        if (isDirty) {
                            dataLinqCode.ui.confirm(
                                "Unsaved changes",
                                "You have unsaved changes. Are you sure you want to continue without saving?",
                                onContinue);
                        } else {
                            onContinue();
                        }
                    };

                    $select.change(function () {
                        var name = $select.val();

                        if (name === currentName) {
                            return;
                        }

                        // keep the current selection visible until the user confirmed
                        $select.val(currentName);

                        confirmUnsaved(function () {
                            loadPage(name);
                        });
                    });

                    $newButton.click(function () {
                        confirmUnsaved(function () {
                            dataLinqCode.ui.prompt(
                                "New error page",
                                "Name of the new error page (letters, digits, - and _ only):",
                                "",
                                function (name) {
                                    if ($select.find("option[value='" + name + "']").length === 0) {
                                        // a new page only exists once it has been saved
                                        $("<option>").attr('value', name).text(displayName(name)).appendTo($select);
                                    }

                                    loadPage(name);
                                },
                                {
                                    validate: function (value) {
                                        if (!value) {
                                            return "Please enter a name.";
                                        }

                                        if (!/^[A-Za-z0-9_-]+$/.test(value)) {
                                            return "Only letters, digits, '-' and '_' are allowed.";
                                        }

                                        return null;
                                    }
                                });
                        });
                    });

                    $deleteButton.click(function () {
                        if (currentName === globalName) {
                            return;
                        }

                        var name = currentName;

                        dataLinqCode.ui.confirm(
                            "Delete error page",
                            "Do you really want to delete the error page '" + name + "'?",
                            function () {
                                $.ajax({
                                    url: dataLinqCode.targetUrl() + '/DeleteErrorPage?dl_token=' + window._datalinqCodeAccessToken,
                                    type: 'post',
                                    data: { name: name },
                                    success: function () {
                                        isDirty = false;
                                        refreshNames(globalName);
                                    },
                                    error: function () {
                                        dataLinqCode.ui.confirm("Error",
                                            "The error page could not be deleted.", function () { });
                                    }
                                });
                            });
                    });

                    refreshNames(globalName);

                    $previewButton.click(function () {
                        // the preview intentionally renders the last saved version in a new tab
                        var url = dataLinqCode.targetUrl() +
                            '/PreviewErrorPage?name=' + encodeURIComponent(currentName) +
                            '&dl_token=' + window._datalinqCodeAccessToken;

                        window.open(url);
                    });

                    $saveButton.click(function () {
                        var frameWindow = $frame[0].contentWindow;

                        if (frameWindow && frameWindow.dataLinqCodeEditor) {
                            frameWindow.dataLinqCodeEditor.submitForm();
                        }
                    });
                }
            });
        };
}).call(dataLinqCode.ui, jQuery);

/**
 * Feature "error-pages": opens the error page editor.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('error-pages', function (state) {
        dataLinqCode.events.on('edit-error-page', function (channel) {
            dataLinqCode.ui.errorPage();
        });
});
