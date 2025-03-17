(function ($) {
    "use strict";
    $.fn.dataLinqCode_blockframe = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_blockframe');
        }
    };
    var defaults = {
        onShow: null
    };
    var methods = {
        init: function (options) {
            var settings = $.extend({}, defaults, options);
            return this.each(function () {
                new initUI(this, settings);
            });
        },
        close: function (options) {
            $(this).children('.datalinq-code-blockframe-blocker').remove();
        }
    };
    var initUI = function (parent, options) {
        var $parent = $(parent);
        
        var $blocker = $("<div>")
            .addClass("datalinq-code-blockframe-blocker")
            .appendTo($parent);

        $("<div>")
            .addClass("datalinq-code-blockframe-close")
            .appendTo($blocker)
            .click(function (e) {
                e.stopPropagation();
                $blocker.remove();
            });

        var $content = $("<div>")
            .addClass("datalinq-code-blockframe-content")
            .appendTo($blocker);

        if (options.onShow) {
            options.onShow($content);
        }
    };
})(jQuery);
