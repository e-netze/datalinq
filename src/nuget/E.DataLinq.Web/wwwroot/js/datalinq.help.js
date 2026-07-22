$(function () {

    function replaceNbsps(str) {
        var re = new RegExp(String.fromCharCode(160), "g");
        return str.replace(re, " ");
    }

    $('button.copy-button').each(function (i, button) {
        $(button).click(function (e) {
            e.stopPropagation();

            var content = '';
            $(this).closest('.copyable-content').children().each(function (e,elem) {
                switch (elem.nodeName.toUpperCase()) {
                    case 'BUTTON':
                        break;
                    case 'BR':
                        content += '\n';
                        break;
                    default:
                        content += $(elem).text().replace(" ", "");
                        break;
                }
            });

            navigator.clipboard.writeText(replaceNbsps(content));
        });
    });

    window.applyHelpSearch = function (input) {
        var $input = $(input);
        var val = $input.val().trim().toLowerCase();
        var $scope = $input.closest('.class-panel.active');
        var $contents = $scope.length > 0 ? $scope.find('.searchable-content') : $('.searchable-content');

        if (!val) {
            $contents.css('display', '');
            return;
        }

        $contents.each(function (i, content) {
            var $content = $(content);
            var text = $content.text().toLowerCase();

            if (text.indexOf(val) >= 0) {
                $content.css('display', '');
            } else {
                $content.css('display', 'none');
            }
        });
    };

    $("input.content-search")
        .keyup(function (e) {
            e.stopPropagation();
            window.applyHelpSearch(this);
        });

    function applyHelpColorScheme(theme) {
        var isLight = theme === 'vs';
        document.documentElement.classList.toggle('colorscheme-light', isLight);
        document.body.classList.toggle('colorscheme-light', isLight);
    }

    // Restore the scheme remembered from a previous message so it survives
    // in-frame navigations (e.g. switching the help language reloads the iframe).
    var storedHelpTheme = sessionStorage.getItem('helpColorScheme');
    if (storedHelpTheme) {
        applyHelpColorScheme(storedHelpTheme);
    }

    window.addEventListener('message', function (event) {
        if (event.data && event.data.theme) {
            sessionStorage.setItem('helpColorScheme', event.data.theme);
            applyHelpColorScheme(event.data.theme);
        }
    });
});