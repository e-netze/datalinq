(function ($) {
    "use strict";
    $.fn.dataLinqCode_toolbar = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_toolbar');
        }
    };
    var defaults = {
        
    };
    var methods = {
        init: function (options) {
            var settings = $.extend({}, defaults, options);
            return this.each(function () {
                new initUI(this, settings);
            });
        }
    };
    var initUI = function (parent, options) {
        var $parent = $(parent);

        $("<div><div class='text'>Check syntax</div></div>")
            .data('event', 'verify-current-document')
            .addClass('datalinq-code-toolbutton verify-current')
            .data('refresh-ui', function (args) {
                return args.currentDoc && args.currentDoc.split('@').length === 3;
            })
            .appendTo($parent);

        $("<div><div class='text'>Save Document</div></div>")
            .data('event', 'save-current-document')
            .addClass('datalinq-code-toolbutton save-current')
            .data('refresh-ui', function (args) {
                return args.currentDoc && args.dirtyDocs &&
                    $.inArray(args.currentDoc, args.dirtyDocs) >= 0;
            })
            .appendTo($parent);

        $("<div><div class='text'>Save all Docs</div></div>")
            .data('event', 'save-all-documents')
            .addClass('datalinq-code-toolbutton save-all')
            .data('refresh-ui', function (args) {
                if (!args.dirtyDocs || args.dirtyDocs.length == 0)
                    return false;

                if (args.dirtyDocs.length == 1 && $.inArray(args.currentDoc, args.dirtyDocs) < 0)
                    return true;

                if (args.dirtyDocs.length > 1)
                    return true;

                return false;
            })
            .appendTo($parent);

        $("<div><div class='text'>Simple Preview</div></div>")
            .data('event', 'run-current-document')
            .addClass('datalinq-code-toolbutton run')
            .data('refresh-ui', function (args) {
                return args.currentDoc &&
                    args.currentDoc.indexOf('_') !== 0 &&
                    args.currentDoc.indexOf('@_css') < 0;
            })
            .appendTo($parent);

        $("<div><div class='text'>Preview in tab</div></div>")
            .data('event', 'run-current-document-in-tab')
            .addClass('datalinq-code-toolbutton run-in-tab')
            .data('refresh-ui', function (args) {
                return args.currentDoc &&
                    args.currentDoc.indexOf('_') !== 0 &&
                    args.currentDoc.indexOf('@_css') < 0;
            })
            .appendTo($parent);

        $("<div><div class='text'>Color scheme</div></div>")
            .data('event', 'toggle-color-scheme')
            .addClass('datalinq-code-toolbutton colorscheme')
            .appendTo($parent);

        $("<div><div class='text'>Datalinq Helper</div></div>")
            .data('event', 'toggle-help')
            .addClass('datalinq-code-toolbutton help')
            .appendTo($parent);

        $("<div><div class='text'>Datalinq Sandbox</div></div>")
            .data('event', 'toggle-sandbox')
            .addClass('datalinq-code-toolbutton sandbox')
            .appendTo($parent);

        $("<div><div class='text'>Datalinq Copilot</div></div>")
            .data('event', 'toggle-copilot')
            .addClass('datalinq-code-toolbutton copilot')
            .appendTo($parent);

        var $logout = $("<div>")
            .data('event', 'logout')
            .addClass('datalinq-code-toolbutton logout')
            .appendTo($parent);

        $("<div>")
            .text(dataLinqCode.loginUsername() || '???')
            .appendTo($logout);
        $("<div>")
            .text('Logout...')
            .appendTo($logout);

        $parent
            .children('.datalinq-code-toolbutton')
            .click(function (e) {
                e.stopPropagation();
                if (!$(this).hasClass('disabled')) {
                    dataLinqCode.events.fire($(this).data('event'));
                }
            });

        dataLinqCode.events.on('refresh-ui-elements', function (channel, args) {
            //console.log('refresh-ui-elements', args);
            $parent.children('.datalinq-code-toolbutton').each(function (i, button) {
                var $button = $(button);
                var func = $button.data('refresh-ui');

                if (func && func(args) === false) {
                    $button.addClass('disabled');
                } else {
                    $button.removeClass('disabled');
                }
            });
        });
    };
})(jQuery);
