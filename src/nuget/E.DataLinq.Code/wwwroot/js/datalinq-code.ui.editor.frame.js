var dataLinqCode = window.parent.dataLinqCode;

dataLinqCodeEditor = new function () {
    var _id, _this;
    var _editor = null;
    var _editorDecorations = null;

    this.id = () => _id;
    this.editor = () => _editor;

    this.init = function (id, value, language) {
        _id = id;

        //console.log('init editor', id, value, language);

        $('.datalinq-code-editor-switcher').dataLinqCode_editor_switcher();
        $('.datalinq-code-editor-settings').dataLinqCode_editor_settings_form();
        $('.datalinq-code-editor-code-errors').dataLinqCode_editor_errors();

        if ($('#datalinq-code-editor-code').length === 1) {

            // register a completion item provider for DLH
            // monaco.languages.registerCompletionItemProvider('razor', getDlhCompletionProvider(monaco));

            _editor = monaco.editor.create(document.getElementById('datalinq-code-editor-code'), {
                language: language || 'text',
                automaticLayout: true,
                theme: dataLinqCode.editorTheme(),
                hover: {
                    enabled: true
                }
            });

            _editor.setValue(value || '');
            _editor.getModel().onDidChangeContent((event) => {
                dataLinqCodeEditor.events.fire('editor-value-changed', { id: _id, value: _editor.getValue() });

                this.setDirty();
                this.removeDecoration();
            });

            let cachedCompletions = JSON.parse(localStorage.getItem('dlhCompletions'));

            function loadDLHCompletions(monaco, language) {
                if (cachedCompletions) {
                    registerDLHCompletions(monaco, language || 'text', cachedCompletions);
                    return;
                }

                dataLinqCode.api.getMonacoSnippit(function (data) {
                    try {
                        const completions = JSON.parse(data);
                        cachedCompletions = completions;
                        localStorage.setItem('dlhCompletions', JSON.stringify(completions));
                        registerDLHCompletions(monaco, language || 'text', completions);
                    } catch (e) {
                        console.error("Failed to parse completions JSON", e);
                    }
                });
            }

            loadDLHCompletions(monaco, language || 'text');
            registerRazorSnippets(monaco, language || 'text');

        } else {
            $('.datalinq-code-editor-settings').css('display', 'block');
        }

        $('.datalinq-code-editor-settings .datalinq-access-tree').each(function (i, element) {
            let $element = $(element);
            $element.datalinq_access_tree({ name: 'access_tree' });
        });

        if ($('.datalinq-code-editor-settings .datalinq-access-control').length > 0) {
            dataLinqCode.api.get('authprefixes', function (result) {
                $('.datalinq-code-editor-settings .datalinq-access-control').each(function (i, element) {
                    $(element).datalinq_autocomplete_multiselect({
                        source: 'AuthAutocomplete',
                        name: 'access_string',
                        prefixes: result,
                        value: $(element).attr('data-value')
                    });
                });
            });
        }

        dataLinqCode.events.on('save-document', _event_save_document);
        dataLinqCode.events.on('verify-document', _event_verify_document);
        dataLinqCode.events.on('before-run-document', _event_before_run_document);
        dataLinqCode.events.on('destroy-editor', _event_destroy_editor);
        dataLinqCode.events.on('theme-changed', _event_theme_changed, this);

        dataLinqCode.bindDocumentEvents(window.document);

        dataLinqCode.events.fire('document-opened', { id: _id });
    };

    var _event_save_document = function (channel, args) {
        if (args.id === _id) {
            dataLinqCodeEditor.submitForm();
        }
    };

    var _event_verify_document = function (channel, args) {
        if (args.id === _id) {
            var ids = args.id.split('@')
            if (ids.length === 3) {  // view
                dataLinqCodeEditor.submitForm(true);
            }
        }
    };

    var _event_before_run_document = function (channel, args) {
        if (args.id === _id) {
            var testParameters = $("[name='TestParameters']").val();
            if (testParameters) {
                args.urlParameters += (args.urlParameters ? '&' : '') + testParameters;
            }

            if (args.id.split('@').length === 2) {
                args.urlParameters += (args.urlParameters ? '&' : '') + '_pjson=true';
            }
        }
    };

    var _event_destroy_editor = function (channel, args) {
        if (args.id === _id) {
            console.log('destroy editor ' + _id);
            dataLinqCode.events.off('save-document', _event_save_document);
            dataLinqCode.events.off('verify-document', _event_verify_document);
            dataLinqCode.events.off('before-run-document', _event_before_run_document);
            dataLinqCode.events.off('theme-changed', _event_theme_changed);
            //dataLinqCode.events.off('destroy-editor', _event_destroy_editor);

            _id = null;
        }
    }

    var _event_theme_changed = function (channel, args) {
        if (_editor) {
            _editor.updateOptions({ theme: args.theme });
        }
    };

    this.refreshToken = function (index) {
        $("input[name=datalinq_token" + index + "]").val(this.generateRandomToken(64));
        dataLinqCodeEditor.setDirty();
    };
    this.clearToken = function (index) {
        $("input[name=datalinq_token" + index + "]").val('');
        dataLinqCodeEditor.setDirty();
    };

    this.generateRandomToken = function (length) {
        var chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

        var token = '';
        for (var i = 0; i < length; i++) {
            token += chars[parseInt((Math.random()) * 0x10000) % chars.length];
        }

        return token;
    };

    this.addErrorDecoration = function (lineNumber) {
        if (lineNumber <= 0) {
            this.removeDecoration();
            return;
        }

        _editorDecorations = dataLinqCodeEditor.editor().deltaDecorations(
            _editorDecorations || [],
            [
                {
                    range: new monaco.Range(lineNumber, 1, lineNumber, 1),
                    options: {
                        isWholeLine: true,
                        linesDecorationsClassName: 'errorLineDecoration'
                    }
                }
            ]
        );

        dataLinqCodeEditor.editor().revealLine(lineNumber);
    };
    this.removeDecoration = function () {
        if (_editorDecorations) {
            dataLinqCodeEditor.editor().deltaDecorations(_editorDecorations, []);
            _editorDecorations = null;

            dataLinqCodeEditor.events.fire('editor-decoration-removed');
        }
    }

    this.delete = function () {
        dataLinqCode.events.fire('delete-document', { id: _id });
    };

    this.setDirty = function () {
        dataLinqCode.events.fire('document-changed', { id: _id });
    };

    this.submitForm = function (verifyOnly) {
        let $form =
            $(".datalinq-code-editor-settings")
                .children('form');

        let actionUrl = $form.attr('action');

        $.ajax({
            type: "POST",
            url: actionUrl + '?verifyOnly=' + (verifyOnly ? 'true' : 'false'),
            data: $form.serialize(),  // serializes the form's elements.
            headers: {
                'Authorization': 'Bearer ' + window._datalinqCodeAccessToken
            },
            success: function (data) {
                if (data.success === true) {
                    if (verifyOnly === true) {
                        dataLinqCode.events.fire('document-verified', { id: _id });
                    } else {
                        dataLinqCode.events.fire('document-saved', { id: _id });
                    }
                } else {
                    dataLinqCode.events.fire('document-errors', { id: _id, errors: data });
                }
            }
        });
    };
}();

dataLinqCode.implementEventController(dataLinqCodeEditor);

(function ($) {
    "use strict";
    $.fn.dataLinqCode_editor_switcher = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_editor_switcher');
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

    var initUI = function (parent, opitons) {
        var $parent = $(parent);

        var $code = $("<div>")
            .addClass('switch-button code')
            .appendTo($parent)
            .click(function (e) {
                e.stopPropagation();
                $('.switch-to').css('display', 'none');
                $('.switch-to.code').css('display', 'block');
            });

        $("<div>")
            .addClass('switch-button settings')
            .appendTo($parent)
            .click(function (e) {
                e.stopPropagation();
                $('.switch-to').css('display', 'none');
                $('.switch-to.settings').css('display', 'block');
            });

        $code.trigger('click');
    };
})(jQuery);

(function ($) {
    "use strict";
    $.fn.dataLinqCode_editor_settings_form = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_editor_properties_form');
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

    var initUI = function (parent, opitons) {
        var $parent = $(parent);

        $parent.find("label")
            .addClass('datalinq-label');
        
        $parent.find("input[type=text], textarea")
            .addClass('datalinq-input')
            .on("keydown", function (e) {
                if (e.ctrlKey || e.which === 116) {  // Ctrl or F5 => dont set document dirty
                    return;
                }

                dataLinqCodeEditor.setDirty();
            });
        $parent.find("input[type=checkbox]")
            .change(function (e) {
                dataLinqCodeEditor.setDirty();
            });
        $parent.find('select')
            .addClass('datalinq-input')
            .change(function (e) {
                dataLinqCodeEditor.setDirty();
            });

        $("<br/>").insertAfter($parent.find('.datalinq-input,.datalinq-label'));
    };
})(jQuery);

(function ($) {
    "use strict"
    $.fn.datalinq_autocomplete_multiselect = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.datalinq_autocomplete_multiselect');
        }
    };

    var defaults = {
        source: '',
        name: '',
        prefixes: [],
        prefix_separator: '::',
        alwaysIncludeOwner: ''
    };

    var methods = {
        init: function (options) {
            var $this = $(this);
            options = $.extend({}, defaults, options);

            return this.each(function () {
                new initUI(this, options);
            });
        },
        add: function (options) {
            options = $.extend({}, defaults, options);

            var prefix = $(this).find('.datalinq-autocomplete-multiselect-prefix').val() || '';

            if (options.value !== "*") {
                $(this).find('.datalinq-autocomplete-multiselect-prefix').children("option").each(function (i, o) {
                    if ($(o).val() && options.value.indexOf($(o).val()) === 0)
                        prefix = $(o).val();
                });
            }

            //console.log(options.value, options.prefix_separator,  options.value.indexOf(options.prefix_separator));
            if (options.value !== "*" &&
                //options.value.indexOf(prefix) !== 0 &&
                options.value.indexOf(options.prefix_separator) < 0) {  // check if options has already a prefix
                options.value = prefix + options.value;
            }

            $(this).datalinq_autocomplete_multiselect('remove', options);

            var displayValue = options.value;
            //if (prefix && displayValue.indexOf(prefix) === 0) {
            //    displayValue = "<strong>" + prefix + "</strong>" + displayValue.substr(prefix.length, displayValue.length - prefix.length);
            //}

            var $valContainer = $(this).find('.datalinq-autocomplete-multiselect-value-conatiner');

            var $div = $("<div>")
                .addClass('datalinq-autocomplete-multiselect-value-item')
                .attr('data-value', options.value)
                .text(displayValue)
                .appendTo($valContainer);



            $("<div class='close-button'>")
                .addClass('close-botton')
                .appendTo($div)
                .click(function () {
                    $(this)
                        .closest('.datalinq-autocomplete-multiselect')
                        .datalinq_autocomplete_multiselect('remove', {
                            value: $(this).closest('.datalinq-autocomplete-multiselect-value-item').attr('data-value')
                    });
                });

            if (options.alwaysIncludeOwner && options.alwaysIncludeOwner !== options.value) {
                $(this).datalinq_autocomplete_multiselect('add', { value: options.alwaysIncludeOwner, suppressEvent: true });
            } else {
                $(this).datalinq_autocomplete_multiselect('_calc');
            }

            if (!options.suppressEvent) {
                dataLinqCodeEditor.setDirty();
            }
        },
        remove: function (options) {
            var value = options.value.replace(/\\/g, '\\\\');

            $(this)
                .find(".datalinq-autocomplete-multiselect-value-item[data-value='" + value + "']")
                .remove();

            $(this).datalinq_autocomplete_multiselect('_calc');

            if (!options.suppressEvent) {
                dataLinqCodeEditor.setDirty();
            }
        },
        _calc: function (options) {
            var val = '';
            $(this).find('.datalinq-autocomplete-multiselect-value-item').each(function (i, e) {
                if (val !== '') val += ',';
                val += $(e).attr('data-value');
            });
            $(this).find('.datalinq-autocomplete-multiselect-value').val(val);
        }
    };

    var initUI = function (elem, options) {

        var $elem = $(elem);
        $elem.addClass('datalinq-autocomplete-multiselect');

        var $inputRow = $("<tr>").appendTo($("<table style='padding:0px;margin:0px'>").appendTo($elem));

        if (options.prefixes && options.prefixes.length > 0) {
            var $select = $("<select class='datalinq-autocomplete-multiselect-prefix datalinq-input' />");
            $select.appendTo($("<td style='padding:0px'>").appendTo($inputRow));

            for (var i in options.prefixes) {
                var prefix = options.prefixes[i];
                $("<option value='" + prefix + "'>" + prefix + "</option>").appendTo($select);
            }

            $("<option value=''>custom</option>").appendTo($select);
        }

        var $input = $("<input class='datalinq-autocomplete-multiselect-input datalinq-input' type='text' />");
        $input.appendTo($("<td style='padding:0px'>").appendTo($inputRow));
        $input.keydown(function (event) {
            if (event.keyCode === 13) {
                event.preventDefault();
                $(this).closest('.datalinq-autocomplete-multiselect').find('.add-button').trigger('click');
                return false;
            }
        });

        var $button = $("<button>+</button>")
            .addClass('add-button')
            .appendTo($("<td style='padding:0px'>").appendTo($inputRow))
            .click(function (event) {
                event.stopPropagation();
                var $elem = $(this).closest('.datalinq-autocomplete-multiselect');
                $elem.datalinq_autocomplete_multiselect('add', { value: $elem.find('.datalinq-autocomplete-multiselect-input').val(), alwaysIncludeOwner: options.alwaysIncludeOwner });
                return false;
            });

        var $valContainer = $("<div>")
            .addClass('datalinq-autocomplete-multiselect-value-conatiner')
            .appendTo($elem);
        var $value = $("<input type='hidden' name='" + options.name + "' id='" + options.name + "' />")
            .addClass('datalinq-autocomplete-multiselect-value')
            .appendTo($elem);

        if (options.value) {
            console.log('options.value', options.value);
            $.each(options.value.split(','), function (i, item) {
                $elem.datalinq_autocomplete_multiselect('add', { value: item, suppressEvent: true });
            });
        }

        if ($.fn.typeahead) {
            $input.on({
                'typeahead:select': function (e, item) {
                    $(this).closest('.datalinq-autocomplete-multiselect').datalinq_autocomplete_multiselect('add', { value: item, alwaysIncludeOwner: options.alwaysIncludeOwner });
                },
                'keyup': function (e) {
                    if (e.keyCode === 13) {
                        $(this).typeahead('close');
                    }
                }
            })
                .typeahead({
                    hint: false,
                    highlight: false,
                    minLength: 3
                },
                    {
                        limit: Number.MAX_VALUE,
                        async: true,
                        source: function (query, processSync, processAsync) {
                            var $element = $(this.$el[0].parentElement.parentElement).children(".datalinq-autocomplete-multiselect-input").first(); // Ugly!!!
                            var $prefix = $(this.$el[0].parentElement.parentElement).closest('.datalinq-autocomplete-multiselect').find('.datalinq-autocomplete-multiselect-prefix');

                            var source = $element.data('datalinq-multiselect-source');
                            if ($prefix.length > 0) {
                                source += (source.indexOf('?') > 0 ? '&' : '?') + 'prefix=' + $prefix.val();
                            }

                            return $.ajax({
                                url: source,
                                type: 'get',
                                data: { term: query },
                                headers: {
                                    'Authorization': 'Bearer ' + window._datalinqCodeAccessToken 
                                },
                                success: function (data) {
                                    //console.log(data);
                                    data = data.slice(0, 12);
                                    //console.log(data);
                                    processAsync(data);
                                },
                                error: function () {
                                }
                            });
                        }
                    }).data('datalinq-multiselect-source', options.source);

        } else if ($.fn.autocomplete) {
            $input.autocomplete({
                search: function (event, ui) {
                    var source = $(this).data('datalinq-multiselect-source');
                    var $prefix = $(this).closest('.datalinq-autocomplete-multiselect').find('.datalinq-autocomplete-multiselect-prefix');
                    if ($prefix.length > 0) {
                        source += (source.indexOf('?') > 0 ? '&' : '?') + 'prefix=' + $prefix.val();
                    }
                    $(this).autocomplete('option', 'source', source);
                },
                minLength: 3,
                select: function (event, ui) {
                    $(this).closest('.datalinq-autocomplete-multiselect').datalinq_autocomplete_multiselect('add', { value: ui.item.value, alwaysIncludeOwner: options.alwaysIncludeOwner });
                }
            }).data('datalinq-multiselect-source', options.source);
        }
    };

})(jQuery);

(function ($) {
    "use strict"
    $.fn.datalinq_access_tree = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_editor_errors');
        }
    }
    var defaults = {
        name: 'access_tree'
    };
    var methods = {
        init: function (options) {
            var $this = $(this);
            options = $.extend({}, defaults, options);

            return this.each(function () {
                new initUI(this, options);
            });
        }
    };

    let initUI = function (elem, options) {
        let $elem = $(elem), tree = $elem.data('tree');

        if (!tree && !tree.Children) return;

        $elem.addClass('datalinq-code-editor-access-tree');

        for (let node of tree.Children) {
            renderNode($elem, node)
        }

        $("<input type='hidden' name='" + options.name + "' id='" + options.name + "' />")
            .addClass('datalinq-access-tree-value')
            .appendTo($elem);

        refresh($elem);
    };

    let renderNode = function ($parent, node, parentPath) {
        if (!node) return;

        var path = parentPath ? parentPath + '/' + node.Id : node.Id;

        var $node = $("<div>")
            .addClass('datalinq-code-editor-access-tree-node')
            .data('node', node)
            .data('path', path)
            .appendTo($parent)
            .click(function (e) {
                if (e.offsetX < 16) {
                    $(this).toggleClass('selected');
                    refresh($(this));
                    dataLinqCodeEditor.setDirty();
                } else {
                    $(this).next().toggleClass('collapsed');
                }
            });

        $("<div>")
            .addClass('title')
            .text(node.Name)
            .appendTo($node);
        $("<div>")
            .addClass('description')
            .text(node.Description)
            .appendTo($node);

        if (node.Selected === true) {
            $node.addClass('selected');
        }

        if (node.Children) {
            let $nodes = $("<div>")
                .addClass('child-nodes')
                .appendTo($parent);

            for(let childNode of node.Children) {
                renderNode($nodes, childNode, path);
            }
        }
    };

    let refresh = function ($sender) {
        let $tree = $sender.hasClass('datalinq-code-editor-access-tree') ? $sender : $sender.closest('.datalinq-code-editor-access-tree');
        let selected = [];

        $tree.find('.datalinq-code-editor-access-tree-node')
            .each(function (i, node) {
                let $node = $(node);
                if ($node.hasClass('selected')) {
                    selected.push($node.data('path'));
                }
                if ($node.next().find('.datalinq-code-editor-access-tree-node.selected').length > 0) {
                    $node.addClass('has-selected');
                } else {
                    $node.removeClass('has-selected');
                }
            });

        $tree.children('.datalinq-access-tree-value').val(selected.toString());
    }
})(jQuery);

(function ($) {
    "use strict"
    $.fn.dataLinqCode_editor_errors = function (method) {
        if (methods[method]) {
            return methods[method].apply(this, Array.prototype.slice.call(arguments, 1));
        }
        else if (typeof method === 'object' || !method) {
            return methods.init.apply(this, arguments);
        }
        else {
            $.error('Method ' + method + ' does not exist on jQuery.dataLinqCode_editor_errors');
        }
    };

    var defaults = {
    };

    var methods = {
        init: function (options) {
            var $this = $(this);
            options = $.extend({}, defaults, options);

            return this.each(function () {
                new initUI(this, options);
            });
        },
        add: function (options) {
            var $content = $(this).children('.content'), lineNumber = 0;

            var model = dataLinqCodeEditor.editor().getModel();
            //console.log(model);

            var codeLine = (options.code_line || '').trim();
            if (codeLine.indexOf("Write(") == 0) {
                codeLine = codeLine.substr(6);
            }
            if (codeLine.indexOf(");") == codeLine.length - 2) {
                codeLine = codeLine.substr(0, codeLine.length - 2);
            }
            //console.log(codeLine);

            if (codeLine) {
                for (var i = 1, to = model.getLineCount(); i <= to; i++) {
                    var line = model.getLineContent(i);

                    if (!line.trim())
                        continue;

                    //console.log('******' + line);

                    if (line.indexOf(codeLine) >= 0) {
                        lineNumber = i;
                        break;
                    }
                }
            }

            console.log(lineNumber);
            
            $("<div>")
                .addClass(options.is_warning === true ? 'warning' : 'error')
                .text('Line ' + lineNumber + ': ' + options.error_text)
                .data('line', lineNumber)
                .appendTo($content)
                .click(function (e) {
                    e.stopPropagation();

                    var lineNumber = $(this).data('line');
                    //console.log('select line ' + lineNumber);

                    $(this).parent().children('.selected').removeClass('selected');
                    $(this).addClass('selected');

                    dataLinqCodeEditor.addErrorDecoration(lineNumber);
                })
        }
    };

    var initUI = function (elem, options) {
        var $elem = $(elem);

        var $title = $("<div><span class='title'></span></<div>")
            .addClass('titlebar')
            .appendTo($elem);

        $("<div>")
            .addClass('closebutton')
            .appendTo($title)
            .click(function (e) {
                e.stopPropagation();
                $(this).closest('.datalinq-code-editor-code-panel').removeClass('show-errors');
            });

        var $content = $("<div>")
            .addClass('content')
            .appendTo($elem);

        dataLinqCode.events.on(['document-saved', 'document-verified'], _event_document_saved, $elem);
        dataLinqCode.events.on('document-errors', _event_document_errors, $elem);
        dataLinqCode.events.on('destroy-editor', _event_destroy_editor);

        dataLinqCodeEditor.events.on('editor-decoration-removed', function (channel) {
            $content.children('.selected').removeClass('selected');
        });
    };

    var _event_document_saved = function (channel, args) {
        if (args.id === dataLinqCodeEditor.id()) {
            var $content = this.children('.content');

            $content.empty();
            $content.closest('.datalinq-code-editor-code-panel').removeClass('show-errors');
        }
    };

    var _event_document_errors = function (channel, args) {
        if (args.id === dataLinqCodeEditor.id()) {
            var $elem = this;
            console.log($elem);

            var $content = $elem.children('.content');
            var $title = $elem.children('.titlebar');

            $content.empty();
            $content.closest('.datalinq-code-editor-code-panel').addClass('show-errors');

            console.log(args);

            var errors = args.errors;
            console.log(errors);
            if (errors) {
                $title.children('.title').text(errors.error_message);

                $.each(errors.compiler_errors, function (i, e) {
                    $elem.dataLinqCode_editor_errors('add', e);
                });
            }
        }
    };

    var _event_destroy_editor = function (channel, args) {
        if (args.id === dataLinqCodeEditor.id()) {
            console.log('destroy dataLinqCode_editor_errors');
            dataLinqCode.events.off('document-saved', _event_document_saved);
            dataLinqCode.events.off('document-errors', _event_document_errors);
            //dataLinqCode.events.off('destroy-editor', _event_destroy_editor);
        }
    };

})(jQuery);

