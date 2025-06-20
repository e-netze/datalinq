(function ($) {
    "use strict";
    $.fn.dataLinq_code_modal = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinq_code_modal');
        }
    };
    var defaults = {
        meta_left_frame: false,
        width: '50%',
        height: '80%',
        minWidth: '320px',
        //maxHeight: '80%',
        slide: true,
        closebutton: true,
        blockerclickclose: true,
        id: 'modaldialog',
        blocker_alpha: 0.5,
        dock: 'center',
        mobile_fullscreen: true,
        animate: true,
        hasBlocker: true
    };
    var methods = {
        init: function (options) {
            var settings = $.extend({}, defaults, options);
            return this.each(function () {
                new initUI(this, settings);
            });
        },
        content: function (options) {
            var settings = $.extend({}, defaults, options);
            return $(dialogSelector(settings)).find('#datalinq-code-modal-content');
        },
        title: function (options) {
            var settings = $.extend({}, defaults, options);
            return $(dialogSelector(settings)).find('.datalinq-code-modal-title');
        },
        close: function (options) {
            options = $.extend({}, defaults, options);

            if ($(this).hasClass('modal-dialog')) {
                $(this).remove();
            } else {
                if ($(dialogSelector(options)).length > 0) {
                    $(dialogSelector(options)).remove();
                    return true;
                }
            }
            return false;
        },
        hide: function (options) {
            options = $.extend({}, defaults, options);
            $(dialogSelector(options)).css('display', 'none');
        },
        show: function (options) {
            options = $.extend({}, defaults, options);
            $(dialogSelector(options)).css('display', '');
        },
        toggle_fullscreen: function (options) {
            options = $.extend({}, defaults, options);
            var elem = $(dialogSelector(options)).find('.datalinq-code-modal-body').get(0);
            if ($.fullScreenEnabled()) {
                elem.style.left = elem.style.top = elem.style.right = elem.style.bottom = '10px';
                elem.style.border = '1px solid #888888';
                $.exitFullScreen();
            }
            else {
                elem.style.left = elem.style.top = elem.style.right = elem.style.bottom = '0px';
                elem.style.border = '';
                $.fullScreen(elem);
            }
        },
        toggle_maximize: function (options) {
            options = $.extend({}, defaults, options);
            var elem = $(dialogSelector(options)).find('.datalinq-code-modal-body').get(0);
            elem.style.width = elem.style.height = '';
            if (elem.style.left !== '0px') {
                elem.style.left = elem.style.top = elem.style.right = elem.style.bottom = '0px';
            }
            else {
                elem.style.left = elem.style.top = elem.style.right = elem.style.bottom = '10px';
                elem.style.border = '1px solid #888888';
            }
        },
        fit: function (options) {
            options = $.extend({}, defaults, options);

            var useMobile = $(window).width() < 1024;
            if (useMobile)
                return;
            var $modalBody = $(dialogSelector(options)).find('.datalinq-code-modal-body');
            var $content = $modalBody.find('#datalinq-code-modal-content');
            var contentWidth = 0, contentHeight = 0;
            $content.children().each(function (i, e) {
                contentWidth = Math.max($(e).outerWidth() + parseInt(parseInt($(e).css('marginLeft')) + parseInt($(e).css('marginRight'))), contentWidth);
                contentHeight += $(e).outerHeight() + parseInt(parseInt($(e).css('marginTop')) + parseInt($(e).css('marginBottom')));

                //console.log('marginWidth :' + parseInt(parseInt($(e).css('marginLeft')) + parseInt($(e).css('marginRight'))));
                //console.log('marginHeight:' + parseInt(parseInt($(e).css('marginTop')) + parseInt($(e).css('marginBottom'))));
            });

            contentHeight += parseInt($content.css('paddingTop')) + parseInt($content.css('paddingBottom'));
            contentWidth += parseInt($content.css('paddingLeft')) + parseInt($content.css('paddingRight'));

            //console.log('contentPaddingWidth: ' + parseInt(parseInt($content.css('paddingLeft')) + parseInt($content.css('paddingRight'))));
            if (options.dock === 'left' || options.dock === 'right') {
                if ($modalBody)
                    $modalBody.css({
                        width: contentWidth + parseInt(parseInt($content.css('paddingLeft')) + parseInt($content.css('paddingRight')))
                    });
            }
            else {
                $modalBody.css({
                    width: contentWidth,
                    height: contentHeight + 68
                });
            }
        }
    };
    var initUI = function (parent, options) {
        var wWidth = $(window).width();
        var wHeight = $(window).height();
        var $parent = $(parent);
        $parent.find(dialogSelector(options)).each(function (i, e) {
            e.parentNode.removeChild(e);
        });
        var useMobile = $(window).width() < 1024 && options.mobile_fullscreen === true;
        var useMobileFullscreenDockPanels = useMobile && screen.width < 800;

        var isEnlargeable = useMobileFullscreenDockPanels === false && (options.dock === 'left' || options.dock === 'right');

        var framePos = options.framepos ?
            options.framepos :
            (((useMobile && options.dock === 'center') || (useMobileFullscreenDockPanels && options.dock !== 'center')) ?
                "left:4px;right:4px;top:4px;bottom:4px;" :
                "width:" + options.width + ";height:" + options.height + ";min-width:" + options.minWidth + ";max-height:" + options.maxHeight + ";max-width:100%;");

        // Absolute wenn sich das ganze innerhalb eines datalinq-container abspielen soll
        //var blockerPosition = $(parent).css('position') === "absolute" || $(parent).css('position') === "relative" ? "absolute" : "fixed";

        // Immer fixed => funktionerrt dann auch für Dialog, wenn API auf Drittseiten eingebunden ist, auf denen gescrollt werden muss
        var blockerPosition = 'fixed';

        const theme = sessionStorage.getItem('editorTheme');
        var schema = ''

        if (theme === 'vs') {
            schema = 'colorscheme-light';
            var $blocker = $("<div id='" + dialogId(options) + "' style='z-index:9999;position:" + blockerPosition + ";left:0px;right:0px;top:0px;bottom:0px;background:rgba(0,0,0," + options.blocker_alpha + ");' class='datalinq-code-modal " + schema + "'></div>");
            var $frame = $("<div id='" + (options.hasBlocker === true ? '' : dialogId(options)) + "' style='z-index:1000;position:absolute;" + framePos + "background:white;opacity:0;" + (useMobile === true ? "" : "display:none;") + "' class='datalinq-code-modal-body " + options.dock + "'></div>").appendTo($blocker);
        } else {
            schema = '';
            var $blocker = $("<div id='" + dialogId(options) + "' style='z-index:9999;position:" + blockerPosition + ";left:0px;right:0px;top:0px;bottom:0px;background:rgba(0,0,0," + options.blocker_alpha + ");' class='datalinq-code-modal " + schema + "'></div>");
            var $frame = $("<div id='" + (options.hasBlocker === true ? '' : dialogId(options)) + "' style='z-index:1000;position:absolute;" + framePos + "background:#555;opacity:0;" + (useMobile === true ? "" : "display:none;") + "' class='datalinq-code-modal-body " + options.dock + "'></div>").appendTo($blocker);
        }

        var pPos, mPos;
        if (useMobile === true) {
            pPos = "left:0px;top:44px;right:0px;bottom:0px";
        }
        else {
            pPos = "left:0px;top:44px;right:0px;bottom:0px";
        }
        var $content = $("<div id='datalinq-code-modal-content' class='datalinq-code-modal-content' style='z-index:1;position:absolute;overflow:auto;" + pPos + "'></div>");
        if (options.content) {
            $content.html(options.content);
        }

        $blocker.appendTo($(parent));

        $content.appendTo($frame);
        $frame.click(function (e) {
            e.stopPropagation();
        });
        if (options.blockerclickclose) {

            // Im Chrome kann es durch ziehen zum schließen kommen
            // Das passiert beispeilsweise, wenn ein Wert aus dem Dialog kopiert wird und beim ziehen dann 
            // die Mause über dem grauen Bereich (Blocker) losgelasssen wird (chrome wirft click-event!!)
            // -> Darum Koordinaten merken, wann Mouse gedrückt wird.

            $blocker.on('mousedown', function (e) {
                $(this).data('mousedown_x', e.originalEvent.offsetX);
                $(this).data('mousedown_y', e.originalEvent.offsetY);
            });
            $blocker.click(function (e) {

                var dx = e.originalEvent.offsetX - $(this).data('mousedown_x');
                var dy = e.originalEvent.offsetY - $(this).data('mousedown_y');

                //console.log(dx + " " + dy);
                if (Math.abs(dx) < 5 && Math.abs(dy) < 5) {
                    if ($(this).find('.datalinq-code-modal-close').length > 0)
                        $(this).find('.datalinq-code-modal-close').trigger('click');
                    else if ($(this).find('.datalinq-code-modal-close-element').length > 0)
                        $(this).find('.datalinq-code-modal-close-element').trigger('click');
                }
            });
        }

        if (useMobileFullscreenDockPanels === false && options.dock === 'left') {
            $frame.css({
                left: 0,
                bottom: 0,
                top: 0,
                height: '',
                maxHeight: '',
                width: options.width
            });
            if (options.hasBlocker === false) {
                $blocker.css({
                    right: '', width: options.width
                });
            }
        }
        else if (useMobileFullscreenDockPanels === false && options.dock === 'right') {
            $frame.css({
                right: 0,
                bottom: 0,
                top: 0,
                height: '',
                maxHeight: '',
                width: options.width
            });
            if (options.hasBlocker === false) {
                $blocker.css({
                    left: '', width: options.width
                });
            }
        }
        else if (options.dock === 'center') {
            var frameTop = Math.max(0, ($blocker.height() / 2 - $frame.height() / 2) / 2);
            $frame.css({
                left: $blocker.width() / 2 - $frame.width() / 2,
                top: frameTop,
                maxHeight: 'calc(100% - ' + 2 * frameTop + 'px)'
            });
        }


        //console.log("width: " + $frame.width());

        var $title = $("<div style='position:absolute;left:0px;right:0px;top:0px;height:35px'><div class='datalinq-code-modal-title'>" + (options.title ? options.title : '') + "</div></div>"),
            $close = null;
        $title.appendTo($frame);
        if (options.closebutton) {
            $title.children('.datalinq-code-modal-title').addClass('has-closebutton');
            if (!isEnlargeable) {
                $("<div class='datalinq-code-modal-resize'></div>").appendTo($title)
            }
            $close = $("<div class='datalinq-code-modal-close'></div>");
            $close.appendTo($title).get(0).options = options;
            $close.click(function (e) {
                e.stopPropagation();
                if (options.onclose)
                    options.onclose();
                $(null).dataLinq_code_modal('close', this.options);
            });
        }
        if (isEnlargeable) {
            $frame.addClass('enlargeable');
            $title.click(function () {
                var $modal = $(this).closest('.datalinq-code-modal-body');
                var $blocker = $(this).closest('.datalinq-code-modal');

                if ($modal.hasClass('modal-large')) {
                    $modal.css('width', 330);
                    if (options.hasBlocker === false) {
                        $blocker.css('width', Math.min(screen.width, 330));
                    }
                } else {
                    $modal.css('width', 640);
                    if (options.hasBlocker === false) {
                        $blocker.css('width', Math.min(screen.width, 640));
                    }
                }
                $modal.toggleClass('modal-large');
            });
        }
        else if (options.dock === 'center') {
            $title
                .css('cursor', 'pointer')
                .click(function () {
                    var $modal = $(this).closest('.datalinq-code-modal-body');
                    if ($modal.hasClass('maximized')) {
                        $modal.css({
                            width: $modal.attr('data-normal-width'),
                            height: $modal.attr('data-normal-height'),
                            left: $modal.attr('data-normal-left'),
                            top: $modal.attr('data-normal-top'),
                            right: '', bottom: ''
                        }).removeClass('maximized');
                    } else {
                        $modal
                            .attr('data-normal-width', $modal.css('width'))
                            .attr('data-normal-height', $modal.css('height'))
                            .attr('data-normal-left', $modal.css('left'))
                            .attr('data-normal-top', $modal.css('top'));

                        $modal.css({
                            left: '0px',
                            top: '0px',
                            right: '0px',
                            bottom: '0px',
                            width: 'auto',
                            height: 'auto'
                        }).addClass('maximized');
                    }

                    if (options && options.onresize) {
                        dataLinqCode.delayed(function () {
                            options.onresize();
                        }, 500);
                    }
                });
        }
        if (useMobile === false && (options.onmaximize || options.allowfullscreen === true)) {
            var $max = $("<table style='cursor:pointer;color:black;font-size:14px;font-weight:bold;position:absolute;top:2px;right:72px;margin:4px'><tr><td>Fullscreen</td><td><div class='i8-button-26 i8-maximize-26-w'></div></td></tr></table>");
            $max.appendTo($title);
            if (options.onmaximize) {
                $max.get(0).onmaximize = options.onmaximize;
                $max.click(function () { this.onmaximize(); });
            }
            else {
                $max.click(function () { $(null).dataLinq_code_modal('toggle_fullscreen'); });
            }
        }
        $frame.css('display', 'block');
        if (options.onload) {
            options.onload($content);
        }
        $frame.removeClass('animate');
        dataLinqCode.delayed(function ($frame) {
            if (options.animate) {
                var originHeight = $frame.css('height'), originWidth = $frame.css('width'), originLeft = $frame.position().left, originTop = $frame.position().top;
                $frame.css({
                    width: '0px', height: '0px',
                    left: $(document).width() / 5, top: $(document).height() / 2
                });
                dataLinqCode.delayed(function ($frame) {
                    $frame.addClass('animate')
                        .css({
                            opacity: 1,
                            width: originWidth,
                            height: originHeight,
                            left: originLeft,
                            top: originTop
                        });
                }, 10, $frame);
            }
            else {
                $frame.css('opacity', 1);
            }
        }, 10, $frame);
    };
    var dialogId = function (options) {
        if (options.id)
            return 'datalinq-code-modal-' + options.id.replace(/\./g, '-').replace(/:/g, '-');
        ;
    };
    var dialogSelector = function (options) {
        var id = dialogId(options);
        return (id === '' ? '.datalinq-code-modal' : '#' + id + '.datalinq-code-modal');
    };
})(jQuery);