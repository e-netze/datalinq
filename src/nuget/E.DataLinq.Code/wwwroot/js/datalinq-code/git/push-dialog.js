/**
 * DataLinq Code - git push dialog
 *
 * dataLinqCode.ui.pushMenu(title, message, onConfirm(name, message), onDecline).
 * Depends on: core/namespace.js, ui/modal.js
 */
(function ($) {
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

}).call(dataLinqCode.ui, jQuery);
