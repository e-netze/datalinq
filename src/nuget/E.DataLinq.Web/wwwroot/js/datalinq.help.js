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

    $("input.content-search")
        .keyup(function (e) {
            e.stopPropagation();

            var val = $(this).val().trim();

            if (!val) {
                $('.searchable-content').css('display', '');
            } else {
                $('.searchable-content').each(function (i, content) {
                    var $content = $(content);
                    var text = $content.text().toLowerCase();

                    if (text.indexOf(val) >= 0) {
                        $content.css('display', '');
                    } else {
                        $content.css('display', 'none');
                    }
                });
            }
        });
});