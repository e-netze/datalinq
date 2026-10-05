/**
 * DataLinq Code - generic dialogs
 *
 * dataLinqCode.ui.alert, confirm, confirmIf, prompt, Accept, confirmPromised.
 * Rendered with the dataLinq_code_modal plugin (ui/modal.js).
 * Depends on: core/namespace.js, ui/modal.js (at call time)
 */
(function ($) {
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

        this.prompt = function (title, message, defaultValue, onConfirm, options) {
            options = options || {};

            $('body').dataLinq_code_modal({
                title: title,
                height: options.select ? (options.noInput === true ? '260px' : '340px') : '220px',
                width: '640px',
                id: 'datalinq-code-prompt',
                onload: function ($content) {
                    $content.addClass('datalinq-code-messagebox-content');

                    if (message) {
                        $("<p>").text(message).appendTo($content);
                    }

                    var $input = $("<input>")
                        .attr('type', 'text')
                        .addClass('datalinq-code-prompt-input')
                        .val(defaultValue || '')
                        .toggle(options.noInput !== true)
                        .appendTo($content);

                    var $select = null;
                    if (options.select) {
                        if (options.select.label) {
                            $("<p>").text(options.select.label).appendTo($content);
                        }

                        $select = $("<select>")
                            .addClass('datalinq-code-prompt-select')
                            .appendTo($content);

                        $.each(options.select.items || [], function (i, item) {
                            $("<option>").attr('value', item.value).text(item.text).appendTo($select);
                        });

                        $select.val(options.select.value || '');
                    }

                    var $error = $("<div>")
                        .addClass('datalinq-code-prompt-error')
                        .appendTo($content);

                    var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);

                    var submit = function () {
                        var value = ($input.val() || '').trim();

                        if (options.validate) {
                            var message = options.validate(value);

                            if (message) {
                                $error.text(message);
                                return;
                            }
                        }

                        $('body').dataLinq_code_modal('close', { id: 'datalinq-code-prompt' });

                        if (onConfirm) {
                            onConfirm(value, $select ? $select.val() : undefined);
                        }
                    };

                    $("<button>")
                        .addClass("datalinq-code-button cancel")
                        .text("Cancel")
                        .appendTo($buttonbar)
                        .click(function () {
                            $('body').dataLinq_code_modal('close', { id: 'datalinq-code-prompt' });
                        });

                    $("<button>")
                        .addClass("datalinq-code-button")
                        .text("OK")
                        .appendTo($buttonbar)
                        .click(submit);

                    $input.on('keydown', function (e) {
                        if (e.key === 'Enter') {
                            e.preventDefault();
                            submit();
                        }
                    });

                    if (options.noInput === true && $select) {
                        $select.focus();
                    } else {
                        $input.focus();
                    }
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
}).call(dataLinqCode.ui, jQuery);
