var dataLinq = new function () {
    var src = document.getElementById('datalinq-script') ?
        document.getElementById('datalinq-script').src.toLowerCase() :
        null;
    src = src ? src.substring(0, src.lastIndexOf('/_content/')) : '';

    this.baseUrl = src;
    console.log('dataLinq.baseUrl', this.baseUrl);

    this.clientid = null;

    this.updateElement = function (parent) {
        $(parent).find('.datalinq-include').each(function (i, e) {
            $e = $(e);
            var id = $e.attr('id');
            if (!id) {
                id = dataLinq.setId($e);
            }

            var url = $e.attr('data-source');

            dataLinq.update($e, url);
        });

        $(parent).find('.datalinq-include-click').each(function (i, e) {
            $e = $(e);
            var id = $e.attr('id');
            if (!id) {
                id = dataLinq.setId($e);
            }

            $e.addClass('button').html($e.attr('data-header'))
                .click(function () {
                    dataLinq.update($(this), $(this).attr('data-source'));
                });
        });

        $(parent).find('.datalinq-fetch').each(function (i, e) {
            $e = $(e);
            var url = $e.attr('data-source');

            dataLinq.fetch($e, url);
        });

        dataLinq.overrideUpdateElement(parent);

        if ($(parent).find('.datalinq-chart').length > 0) {
            dataLinq.createChartElements($(parent), $(parent).find('.datalinq-chart'));
        }


        $(parent).find('.datalinq-refresh-filter-body').each(function (i, filterBody) {
            var $filterBody = $(filterBody);

            var filter = $(parent).attr('data-filter');
            console.log('datafilter', filter);
            if (filter) {
                filter = filter.split('&');

                for (var f in filter) {
                    var filterClause = filter[f], pos = filterClause.indexOf('=');
                    var name = filterClause.substr(0, pos);
                    var val = filterClause.substr(pos + 1, filterClause.length - pos - 1);
                    val = decodeURIComponent(val);

                    var $filterElement = $filterBody.find(".datalinq-filter-parameter[name='" + name + "']");

                    if ($filterElement.attr("type") === "checkbox") {
                        $filterElement.prop("checked", true);
                    } else {
                        $filterElement.val(val);
                    }

                    $filterElement.addClass('hasvalue');
                    if ($filterElement.val()) {
                        $filterElement.addClass('hasvalue');
                    } else {
                        $filterElement.removeClass('hasvalue');
                    }

                    // Falls Daten für ComboBox über AJAX kommen, sind diese noch nicht da. Data-Attribut setzen und danach Wert setzen
                    if (typeof $filterElement[0] !== 'undefined' && $filterElement[0].tagName === 'SELECT') {
                        val = val.replace(/\+/g, ' ');
                        if ($filterElement.attr("multiple") === "multiple") {
                            var values = $filterElement.data("defaultvalue");
                            if (values.length > 0) {
                                values.push(val);
                                $filterElement.data("defaultvalue", values);
                            }
                            else {
                                $filterElement.data("defaultvalue", [val]);
                            }
                        } else {
                            $filterElement.data("defaultvalue", val);
                        }
                    }
                }
            }

            if ($filterBody.find('.datalinq-filter-parameter.hasvalue').length > 0)
                $filterBody.closest('.datalinq-refresh-filter-container').find('.datalinq-button.menu').removeClass('unused');
            else
                $filterBody.closest('.datalinq-refresh-filter-container').find('.datalinq-button.menu').addClass('unused');

            if ($filterBody.closest('.datalinq-refresh-filter-container').attr("data-isopen") === "True")
                $filterBody.prev("button.datalinq-button.menu").first().click();

            // if filter has cascading combo boxes: UpdateViewFilter => refill the combos
            if ($filterBody.find('select.datalinq-filter-parameter[data-depends-on]').length > 0) {
                $filterBody.find("input[type='text']").each(function (i, input) {
                    dataLinq.updateViewFilter(input);
                });
            }
        });

        $(parent).find('.datalinq-refresh-ordering-body').each(function (i, orderingBody) {
            var $orderingBody = $(orderingBody);

            var orderby = $(parent).attr('data-orderby');
            if (orderby) {
                orderby = orderby.split(',');
                // Wie kann ich Checkboxes mit initialen "checked" von anderen unterscheiden?
                // 1. Zuerst alle initialen auf "unchecked" (falls manuell gewählt, werden sie unterhalb ohnehin wieder gesetzt)
                //$orderingBody.find(".datalinq-ordering-field.initial").prop('checked', false);

                var hasChecked = false;
                for (var o in orderby) {
                    var name = orderby[o], desc = false;
                    if (name.indexOf('-') === 0) {
                        name = name.substr(1, name.length - 1);
                        desc = true;
                    }

                    hasChecked |= $orderingBody.find(".datalinq-ordering-field[name='" + name + "']").length > 0;
                    $orderingBody.find(".datalinq-ordering-field[name='" + name + "']").prop('checked', true);
                    if (desc)
                        $orderingBody.find(".datalinq-ordering-desc[name='" + name + "']").prop('checked', true);
                }

                if (hasChecked)
                    $orderingBody.closest('.datalinq-refresh-ordering-container').find('.datalinq-button.menu').removeClass('unused');
                else
                    $orderingBody.closest('.datalinq-refresh-ordering-container').find('.datalinq-button.menu').addClass('unused');

                // 2. Wenn keine nicht-initialen gecheckt sind => Order kommt nur aus Query => die initialen auf "checked" setzen
                //if ($orderingBody.find(".datalinq-ordering-field:not(.initial):checked").length == 0) {
                //    $orderingBody.find(".datalinq-ordering-field.initial").prop('checked', true);
                //}
            } else
                $(parent).find('.datalinq-refresh-ordering-container .datalinq-button.menu').addClass('unused');

            if ($orderingBody.closest('.datalinq-refresh-ordering-container').attr("data-isopen") === "True")
                $orderingBody.prev("button.datalinq-button.menu").first().click();
        });

        dataLinq.bindEvents(parent);
        dataLinq.updateSelectElements(parent, '.datalinq-include-combo');
        dataLinq.updateRadioElements(parent, '.datalinq-include-radio');
        dataLinq.updateScalarElements(parent, '.datalinq-include-scalar');
        dataLinq.updateLegend('body');
        dataLinq.updateDatePickerElements(parent, "input[data-datatype='Date'],input[data-datatype='DateTime']");
        dataLinq.updateCopyableElements(parent, '.datalinq-copyable');
        dataLinq.updateFilterableElements(parent, '.datalinq-filterable');
        dataLinq.updateRefreshTickerElements(parent, '.datalinq-refresh-ticker');
        dataLinq.multiSelect(
            $(parent).find(".datalinq-selectable").parent(),
            {
                selectableItems: ".datalinq-selectable",
                className: "datalinq-selected"
            }
        );

        // Wenn RefreshTicker dabei: Aktiv/Inaktiv über Parent-DIV steuern, da sonst bei jedem Update im gleicher Wert (merkt sich sonst nicht, falls ausgeschaltet wurde)
        $(parent).find(".datalinq-refresh-ticker").on("click", function (event) {
            $(parent).attr("data-refresh-ticker-isactive", $(this).prop("checked"));
        });

        $(parent).find(".datalinq-open-dialog").on("click", function (event) {
            event.stopPropagation();
            dataLinq.openDialog(this, $(this).data("dialog-id"), $(this).data("dialog-parameter"));
        });

        $(parent).find(".datalinq-execute-non-query").on("click", function (event) {
            event.stopPropagation();
            dataLinq.executeNonQuery(this, $(this).data("dialog-id"), $(this).data("dialog-parameter"));
        });

        $(parent).find(".datalinq-update-filter").on("click", function (event) {
            event.stopPropagation();
            dataLinq.updateFilter(this, $(this).data("filter-id"), $(this).data("filter-name"), $(this).data("filter-value"));
        });


        if ($(parent).find("form.datalinq-edit-mask").length > 0) {
            $("form.datalinq-edit-mask").on("keypress", ":input:not(textarea):not([type=submit])", function (event) {
                if (event.keyCode === 13) {
                    event.preventDefault();
                    $("form.datalinq-edit-mask").find("button.datalinq-submit-form").click();
                }
            });
        }

        $(parent).find(".datalinq-refresh-filter-container").on("keypress", function (event) {
            if (event.keyCode === 13) {
                event.preventDefault();
                $(this).find(".datalinq-button.apply:not(.datalinq-filter-clear)").click();
            }
        }).on('dragover', function (e) {
            e.preventDefault();
            try {
                var json = e.originalEvent.dataTransfer.getData('text');
                if (json) {
                    var featureCollection = $.parseJSON(json);
                    return featureCollection.features.length > 0;
                }
            } catch (ex) { }
            return false;
        }).on('drop', function (e) {
            e.preventDefault();
            var json = e.originalEvent.dataTransfer.getData('text');
            if (json) {
                var featureCollection = $.parseJSON(json);
                if (featureCollection.features) {
                    for (var f in featureCollection.features) {
                        var feature = featureCollection.features[f];
                        if (!feature.meta || !feature.meta.query)
                            continue;

                        var $filterParameters = $(this).find(".datalinq-filter-parameter[data-drop-query='" + feature.meta.query + "']");
                        if ($filterParameters.length === 0)
                            continue;

                        //$(this).find(".datalinq-filter-parameter").val("");
                        $filterParameters.each(function () {
                            if (feature.properties[$(this).attr('data-drop-property')]) {
                                $(this).val(feature.properties[$(this).attr('data-drop-property')]);
                                dataLinq.updateViewFilter($(this));
                            }
                        });
                        dataLinq.refresh($filterParameters.get(0));
                        break;
                    }
                    dataLinq.events.fire('onfilterdropped', dataLinq, { element: this, featureCollection: featureCollection });
                }
            }

        });
    };

    this.setId = function ($e) {
        var id = $e.attr('id');
        if (!id || $('#' + id).length > 1) {
            while (true) {
                var id = this.guid();
                if ($('#' + id).length > 0)
                    continue;

                $e.attr('id', id);
                return id;
            }
        }
        return id;
    };

    this.fetch = function ($e, url) {
        let id = this.setId($e);

        if (url.indexOf("http://") !== 0 && url.indexOf("https://") !== 0 && url.indexOf("//") !== 0) {
            url = this.baseUrl + "/datalinq/select/" + url;
        }
        var filter = $e.attr('data-filter');
        url += (url.indexOf('?') > 0 ? "&" : "?") + filter;

        var jsCallback = $e.attr('data-js-callback');

        $.ajax({
            url: url,
            data: dataLinq.overrideModifyRequestData({ }),
            success: function (result) {
                $e.addClass('finished');

                console.log(jsCallback);
                window[jsCallback](result);
            }
        });
    };

    this.update = function ($e, url) {
        if ($e.hasClass('datalinq-include-click-loaded'))
            return true;

        var id = this.setId($e);

        if (url.indexOf("http://") !== 0 && url.indexOf("https://") !== 0 && url.indexOf("//") !== 0) {
            url = this.baseUrl + "/datalinq/select/" + url;
        }
        var filter = $e.attr('data-filter');      
        url += (url.indexOf('?') > 0 ? "&" : "?") + filter;

        $e.html("<img src='" + dataLinq.baseUrl + "/_content/E.DataLinq.Web/css/img/hourglass/loader1.gif" + "' />");
        $.ajax({
            url: url,
            data: dataLinq.overrideModifyRequestData({ _f: 'json', _id: id, _orderby: $e.attr('data-orderby') }),
            success: function (result) {
                var $elem = $('#' + result._id).removeClass('button').html(result.html);

                dataLinq.updateElement($elem);
                if (result.success)
                    $elem.addClass('finished');
                if (result.success && $e.hasClass('datalinq-include-click'))
                    $e.addClass('datalinq-include-click-loaded');
                if (typeof $e.attr('data-scroll-position') !== "undefined" && ($e.attr('data-scroll-position') > window.innerHeight)) {
                    $('html, body').animate({ scrollTop: $e.attr("data-scroll-position") }, 0);
                    $e.removeAttr("data-scroll-position");
                }
                dataLinq.events.fire('onupdated', dataLinq, { element: $e });
                if ($('.datalinq-include').length === $('.datalinq-include.finished').length)
                    dataLinq.events.fire('onpageloaded', dataLinq);
            }
        });
    };

    this.updateViewOrdering = function (sender) {
        var $orderingBody = $(sender).closest('.datalinq-refresh-ordering-body');
        var $view = $orderingBody.closest('.datalinq-include, .datalinq-include-click');

        var orderby = '', hasChecked = false;
        $orderingBody.find('.datalinq-ordering-field').each(function (i, e) {
            var $e = $(e);
            if ($e.is(':checked')) {
                var name = $e.attr('name');
                if ($orderingBody.find(".datalinq-ordering-desc[name='" + name + "']").is(':checked'))  // Sort desc
                    name = "-" + name;
                orderby += (orderby !== '' ? ',' : '') + name;
                hasChecked = true;
            }
        });
        $view.attr('data-orderby', orderby);

        if (hasChecked)
            $orderingBody.closest('.datalinq-refresh-ordering-container').find('.datalinq-button.menu').removeClass('unused');
        else
            $orderingBody.closest('.datalinq-refresh-ordering-container').find('.datalinq-button.menu').addClass('unused');
    };

    this.updateViewFilter = function (sender) {
        var senderName = $(sender).attr('name');
        var $filterBody = $(sender).closest('.datalinq-refresh-filter-body');
        var $view = $filterBody.closest('.datalinq-include, .datalinq-include-click');

        var filter = '';
        $filterBody.find('.datalinq-filter-parameter').each(function (i, e) {
            var $e = $(e), val = $e.val() || '';
            var f;

            // Wenn "Multiple" => für Query sollten Werte mit ';' getrennt sein, nicht mit ','
            if ($e.attr("multiple") === "multiple" && val.length > 0) {
                f = $e.attr('name') + "=" + encodeURI(val.join(";"));
            } else {
                f = $e.serialize();
            }

            filter += (filter !== '' ? '&' : '') + f;

            if ($e.val()) {
                $e.addClass('hasvalue');
            } else {
                $e.removeClass('hasvalue');
            }

            if ($e.attr("type") === "checkbox") {
                $e.removeClass('hasvalue');
                if ($e.is(":checked"))
                    $e.addClass('hasvalue');
            }

            if ($e.data('depends-on') && $e.data('url')) {
                var dependsOn = $e.data('depends-on').split(',');
                if ($.inArray(senderName, dependsOn) >= 0) {
                    var dataUrl = $e.data('url');

                    $filterBody.find('.datalinq-filter-parameter').each(function (j, input) {
                        if ($(input).val()) {
                            dataUrl = dataUrl.replaceAll('[' + $(input).attr('name') + ']', $(input).val());
                        }
                    });
                    dataLinq.updateSelectElement($e, dataUrl);
                }
            }
        });
        $view.attr('data-filter', filter);

        if ($filterBody.find('.datalinq-filter-parameter.hasvalue').length > 0) {
            $filterBody.closest('.datalinq-refresh-filter-container').find('.datalinq-button.menu').removeClass('unused');
        } else {
            $filterBody.closest('.datalinq-refresh-filter-container').find('.datalinq-button.menu').addClass('unused');
        }
    };

    var _isSelect2 = function (e) { return $(e).hasClass('select2-hidden-accessible'); };
    this.clearFilter = function (sender) {
        var $filterBody = $(sender).closest('.datalinq-refresh-filter-body');
        var $view = $filterBody.closest('.datalinq-include, .datalinq-include-click');

        var hiddenFilters = [];

        $filterBody.find('input[type="hidden"].datalinq-filter-parameter').each(function (i, e) {
            var paramName = $(e).attr('name');
            var paramValue = $(e).val() || '';
            hiddenFilters.push(paramName + '=' + encodeURIComponent(paramValue));
        });

        $filterBody.find('input:not([type="hidden"]).datalinq-filter-parameter, select.datalinq-filter-parameter').each(function (i, e) {
            if (_isSelect2(e)) {
                $(e).val('').trigger('change');
            } else {
                $(e).val('');
            }

            if ($(e).attr("type") === "checkbox") {
                $(e).prop("checked", false);
            }

            if ($(e).attr("multiple") === "multiple") {
                $(e).val([]).change();
            }
        });

        $view.attr('data-filter', hiddenFilters.join('&'));

        dataLinq.updateViewFilter(sender);
    };

    this.deleteQuicksearch = function (elem) {
        var $input = $(elem).closest('.datalinq-quicksearch-search-wrapper')
            .find('.datalinq-quicksearch-search-input');

        $input.val('')
            .trigger('input')
            .focus();
    };

    this.onSearchInput = function (elem) {
        var value = $(elem).val().toLowerCase().trim();

        var columns = $(elem).closest('.datalinq-quicksearch-search-container')
            .attr('datalinq-quicksearch-columns')
            .split(',');

        var $searchContainer = $(elem).closest('.datalinq-quicksearch-search-container');
        var tableId = $searchContainer.attr('datalinq-quicksearch-table');
        var $table;

        if (tableId) {
            $table = $('#' + tableId);
        } else {
            $table = $searchContainer.nextAll('table').first();
        }

        if ($table.length === 0) {
            console.warn('No table found below search container');
            return;
        }

        var $headerRow = $table.find('thead th, thead td');
        var hasTheadStructure = $headerRow.length > 0;

        if (!hasTheadStructure) {
            $headerRow = $table.find('tr').first().find('th, td');
        }

        var columnIndices = [];
        $headerRow.each(function (index) {
            var headerText = $(this).text().trim();
            if (columns.indexOf(headerText) !== -1) {
                columnIndices.push(index);
            }
        });

        if (columnIndices.length === 0) {
            console.warn('None of the specified columns found in table:', columns);
            return;
        }

        var $rows;
        if (hasTheadStructure) {
            $rows = $table.find('tbody tr');
        } else {
            $rows = $table.find('tr:not(:first)');
        }

        $rows.each(function () {
            var $row = $(this);
            var matchFound = false;

            for (var i = 0; i < columnIndices.length; i++) {
                var cellText = $row.find('td, th').eq(columnIndices[i]).text().toLowerCase();
                if (cellText.indexOf(value) !== -1) {
                    matchFound = true;
                    break; 
                }
            }

            if (matchFound || value === '') {
                $row.show();
            } else {
                $row.hide();
            }
        });
    };

    this.refresh = function (elem) {
        var $e = $(elem).closest('.datalinq-include, .datalinq-include-click').removeClass('datalinq-include-click-loaded');
        if ($e.length > 0) {
            $e.each(function (i, e) {
                var url = $(e).attr('data-source');
                if (url) {
                    $(e).find('.datalinq-map').each(function () {
                        if ($(this).data('datalinq-map')) {
                            $(this).data('datalinq-map').destroy();
                            $(this).data('datalinq-map', null);
                        }
                    });
                    dataLinq.update($(e), url);
                }
            });
        }
    };

    this.export = function (elem, columnsToKeep) {
        var $e = $(elem).closest('.datalinq-include, .datalinq-include-click');
        var ids = $e.attr("data-source").split("@");
        if (ids.length !== 3) {
            alert("Ids für Export können nicht ausgelesen werden");
            return false;
        }
        var where = $e.attr("data-filter");
        var order = $e.attr("data-orderby");

        if (Array.isArray(columnsToKeep) && columnsToKeep.length > 0) {
            var jsonUrl = dataLinq.baseUrl + "/datalinq/select/" + ids[0] + "@" + ids[1] + "?" + where + "&_orderby=" + order + "&_pjson=true";

            fetch(dataLinq.overrideModifyRequestUrlData(jsonUrl))
                .then(response => {
                    if (!response.ok) throw new Error("Network response was not ok");
                    return response.json();
                })
                .then(jsonData => {
                    const filteredData = jsonData.map(item => {
                        const filteredItem = {};
                        columnsToKeep.forEach(col => {
                            if (col in item) filteredItem[col] = item[col];
                        });
                        return filteredItem;
                    });

                    const exportAsString = elem.getAttribute('datalinq-export-asString') || "";

                    const csv = jsonToCsv(filteredData, exportAsString);

                    downloadCsv(csv, (elem.getAttribute('datalinq-export-filename') || 'export') + '.csv');
                })
                .catch(err => alert("Fehler beim Export: " + err.message));

        } else {
            var exportUrl = dataLinq.baseUrl + "/datalinq/select/" + ids[0] + "@" + ids[1] + "?" + where + "&_orderby=" + order + "&_f=csv";
            window.open(dataLinq.overrideModifyRequestUrlData(exportUrl));
        }
    };

    function jsonToCsv(items, asString) {
        if (!items.length) return "";

        const keys = Object.keys(items[0]);

        const escapeCsv = (text, asString) => {
            if (asString) {
                return '"' + String(text).replace(/"/g, '""') + '"';
            }
            if (typeof text === "string" && (text.includes(",") || text.includes('"') || text.includes("\n"))) {
                return '"' + text.replace(/"/g, '""') + '"';
            }
            return text;
        };

        const header = keys.join(";");
        const rows = items.map(item =>
            keys.map(k => escapeCsv(item[k] ?? "", asString === "true")).join(";")
        );

        return [header, ...rows].join("\n");
    }

    function downloadCsv(csvContent, filename) {
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement("a");
        if (navigator.msSaveBlob) { 
            navigator.msSaveBlob(blob, filename);
        } else {
            const url = URL.createObjectURL(blob);
            link.href = url;
            link.setAttribute('download', filename);
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }
    }


    this.updateLegend = function (parent) {
        $('.legend').empty();
        var dict = {};
        $(parent).find('.source').each(function (i, e) {
            $e = $(e);
            dict[$e.html()] = $e.data("source");
        });
        var $table = $("<table></table>").appendTo($('.legend'));
        for (key in dict) {
            $("<tr>" +
                "<td>" + key + "</td>" +
                "<td>&nbsp;Quelle " + dict[key] + "</td>" +
                "</tr>").appendTo($table);
        }
    };

    this.createChartElements = function ($parent, $elements) {
        $elements.each(function (i, e) {
            var data = window[$(e).data('chart-data')];
            var type = $(e).data('chart-type');
            var label = $(e).data('chart-label');
            var dataset_options = window[$(e).data('chart-dataset')];
            var colorsRGB = $(e).data('chart-color').split("|");
            var localeKey = $(e).data('chart-locale');  // e.g. "DE", "US", "None"
            var localeMap = {
                'DE': 'de-DE',
                'US': 'en-US',
                'None': null
            };
            var locale = localeMap[localeKey] || null;
            //var chartOptions = $(e).data('chart-options');

            if (label.length > 0)
                var $label = $("<div class='datalinq-chart-label'>" + label + "</div>").appendTo($(e));
            var $canvas = $("<canvas class='datalinq-chart-canvas'></canvas>").appendTo($(e));

            switch (type) {
                case "Bar":
                case "Pie":
                case "Doughnut":
                    dataLinq.createBarPieChart($canvas, data, colorsRGB, type.toLowerCase(), dataset_options/*, chartOptions*/, locale);
                    break;
                case "Line":
                case "Scatter":
                    dataLinq.createLineChart($canvas, data, colorsRGB, type.toLowerCase(), dataset_options, locale);
                    break;
            }
        });

    };

    this.createBarPieChart = function (ctx, data, colorRGB, type, dataset_options/*, chartCustomOptions*/, locale) {
        var datasets = [], labels = null;
        // ### Wenn keine Kategorie (siehe StatisticsGroupBy, StatisticsGroupByDerived)
        if (typeof data.categories === 'undefined') {
            // Bei anderen Diagrammtypen als Balkendiagramm: Immer unterschiedliche Farben verwenden. Bei Balken nur bis max. 7 Kategorien
            if ((type !== 'bar') || (type === 'bar' && data.length < 8)) {
                var colors = [], colorsBorder = [], colorsBackground = [], colorsHoverBackground = [];
                for (var c in data) {
                    if (type === 'bar') {
                        // Wenn die Anzahl der Farben nicht mit der Anzahl der Werte übereinstimmt => erste Farbe nehme. Falls diese nicht gesetzt => Standardfarbe
                        var colorRGB_help = colorRGB[(data.length === colorRGB.length ? c : 0)];
                        if (colorRGB_help.length === 0)
                            colorRGB_help = undefined;
                        colors = dataLinq.randomColorRGBA(colorRGB_help);
                    }
                    else
                        colors = dataLinq.randomColorRGBA((colorRGB.length > 0 && data.length <= colorRGB.length) ? colorRGB[c] : undefined);

                    colorsBorder.push(colors['border']);
                    colorsBackground.push(colors['background']);
                    colorsHoverBackground.push(colors['hoverbackground']);
                }
                // Wenn Balkendiagramm mit mehr als 7 Kategorien: erste Farbe verwenden
            } else {
                var colors = [], colorsBorder, colorsBackground, colorsHoverBackground;
                colors = dataLinq.randomColorRGBA(colorRGB[0].length > 0 ? colorRGB[0] : "0,155,20");
                colorsBorder = colors['border'];
                colorsBackground = colors['background'];
                colorsHoverBackground = colors['hoverbackground'];
            }

            datasets.push({
                data: $.map(data, function (e) { return e.value; }),
                borderWidth: 2,
                backgroundColor: colorsBackground,
                borderColor: colorsBorder,
                hoverBackgroundColor: colorsHoverBackground
                //,hoverBorderColor: colorsBorder
            });
            labels = $.map(data, function (e) { return e.name; });
        }

        // ### Mehrere Kategorieren: nur bei Balken (siehe StatisticsGroupByTime)
        else {
            if (type === 'bar') {
                var colors = [], colorsBorder = [], colorsBackground = [], colorsHoverBackground = [];
                for (var c in data.categories) {
                    // Wenn die Anzahl der Farben nicht mit der Anzahl der Kategorien übereinstimmt => erste Farbe nehme. Falls diese nicht gesetzt => Standardfarbe
                    var colorRGB_help = colorRGB[(data.categories.length === colorRGB.length ? c : 0)];
                    if (colorRGB_help.length === 0)
                        colorRGB_help = undefined;
                    colors = dataLinq.randomColorRGBA(colorRGB_help);
                    datasets.push({
                        data: $.map(data.categories[c].data, function (e) { return e.value; }),
                        label: data.categories[c].category,
                        backgroundColor: colors['background'],
                        borderColor: colors['border']
                    });

                }
            }
            labels = data.label;
        }

        var chartData = {
            labels: labels,
            datasets: datasets
        };

        if (typeof (dataset_options) !== "undefined")
            chartData.datasets = $.extend(true, [{}], chartData.datasets, dataset_options);

        var options = {
            maintainAspectRatio: false,
            scales: {
                yAxes: [{
                    stacked: true,
                    gridLines: {
                        display: true
                    },
                    ticks: {
                        // Achse: nur ganzzahlig
                        //callback: function (value) { if (Number.isInteger(value)) { return value; } },
                        //callback: function (value) { if (Math.floor(value) === value && $.isNumeric(value)) { return value; } }

                        // Anforderung Y-Achse Tausender Trennzeichen de-DE Format mit . DataLinq Forum #32
                        callback: function (value) {
                            if (Math.floor(value) === value && $.isNumeric(value)) {
                                return locale ? value.toLocaleString(locale) : value;
                            }
                            return value;
                        }
                    }
                }],
                xAxes: [{
                    type: data.type,
                    //unit:"day",
                    //round: "day",

                    gridLines: {
                        display: false
                    },
                    ticks: {
                        autoSkip: false,
                        callback: function (value) {
                            if (Math.floor(value) === value && $.isNumeric(value)) {
                                return locale ? value.toLocaleString(locale) : value;
                            }
                            return value;
                        }
                    }
                }]
            },
            legend: {
                display: (type !== 'bar' ? true : false),
            },
            tooltips: {
                callbacks: {
                    label: function (tooltipItem, data) {
                        let label = data.datasets[tooltipItem.datasetIndex].label || '';
                        let value = tooltipItem.yLabel !== undefined ? tooltipItem.yLabel : tooltipItem.value;
                        return label ? `${label}: ${locale ? value.toLocaleString(locale) : value}` : (locale ? value.toLocaleString(locale) : value);
                    }
                }
            }

            
        };

        if (type !== 'bar') {
            delete options.scales;
            delete chartData.datasets[0].borderColor;
        }

        var chartOptions = {
            type: type,
            data: chartData,
            options: options,
            plugins: []
        };

        if (window.ChartDataLabels) {
            chartOptions.plugins.push(ChartDataLabels);
        }
        if (window.ChartColorSchemes) {
            chartOptions.plugins.push(ChartColorSchemes);
        }

        //if (chartCustomOptions) {
        //    $.extend(chartOptions.options, chartCustomOptions, chartCustomOptions);
        //    //console.log('chartOptions',chartOptions);
        //}

        var myChart = new Chart(ctx, chartOptions);

        ctx.closest(".datalinq-chart").data("datalinq-chartobject", myChart);
    };

    this.createLineChart = function (ctx, data, colorRGB, type, dataset_options, locale) {
        var chartData;

        if (typeof (data.categories) === 'undefined') {
            colors = dataLinq.randomColorRGBA((colorRGB.length > 0 && colorRGB[0].length > 0) ? colorRGB[0] : "0,155,20");
            colorsBorder = colors['border'];
            colorsBackground = colors['background'];
            colorsHoverBackground = colors['hoverbackground'];

            chartData = {
                labels: $.map(data, function (e) { return new Date(e.name); }),
                datasets: [{
                    data: $.map(data, function (e) { return e.value; }),
                    borderWidth: 2,
                    backgroundColor: colorsBackground,
                    borderColor: colorsBorder,
                    hoverBackgroundColor: colorsHoverBackground,
                    pointHoverBorderColor: colorsBorder
                    //lineTension: 0,
                    //steppedLine: true
                }]
            };

            if (typeof (dataset_options) !== "undefined")
                chartData.datasets = $.extend(true, [{}], chartData.datasets, dataset_options);
        }
        // #############################
        else {
            if (data.categories.length === 0) {
                chartData = {
                    labels: null,
                    datasets: null
                };
            } else {
                var colors = [], colorsBorder = [], colorsBackground = [], colorsHoverBackground = [];
                var datasets = [];
                for (var c in data.categories) {
                    colors = dataLinq.randomColorRGBA((typeof (colorRGB[c]) !== 'undefined' && colorRGB[c].length > 0 && data.categories.length <= colorRGB.length) ? colorRGB[c] : undefined);

                    var sparseData = [];
                    $.map(data.categories[c].data, function (e) {
                        sparseData.push({ x: new Date(e.name), y: e.value });
                    });

                    var dataset = {
                        label: data.categories[c].category,
                        data: sparseData,
                        borderWidth: 2,
                        backgroundColor: colors['background'],
                        borderColor: colors['border'],
                        hoverBackgroundColor: colors['hoverbackground'],
                        pointHoverBorderColor: colors['border']
                        //,lineTension: 0
                        //,steppedLine: true
                    };

                    if (typeof (dataset_options) !== "undefined" && typeof (dataset_options[c]) !== "undefined")
                        dataset = $.extend(true, [{}], dataset, dataset_options[c]);

                    datasets.push(dataset);
                }
                chartData = {
                    //labels: data.label,
                    labels: $.map(data.label, function (e) { return new Date(e); }),
                    datasets: datasets
                };
            }
        }
        // #############################

        var options = {
            maintainAspectRatio: false,
            scales: {
                xAxes: [{
                    ticks: {
                        autoSkip: (typeof (data.categories) === 'undefined' ? false : true),
                        callback: function (value) {
                            if (Math.floor(value) === value && $.isNumeric(value)) {
                                return locale ? value.toLocaleString(locale) : value;
                            }
                            return value;
                        }
                    },
                    type: 'time',
                    time: {
                        tooltipFormat: 'DD.MM.YYYY HH:mm',
                        displayFormats: {
                            second: 'HH:mm:ss',
                            minute: 'HH:mm',
                            hour: 'dd D.M. H[h]',
                            day: 'dd D. MMM YY'
                        }
                    }
                }],
                yAxes: [{
                    ticks: {
                        // Achse: nur ganzzahlig
                        //callback: function (value) { if (Number.isInteger(value)) { return value; } },
                        //callback: function (value) { if (Math.floor(value) === value && $.isNumeric(value)) { return value; } }

                        // Anforderung Y-Achse Tausender Trennzeichen de-DE Format mit . DataLinq Forum #32
                        callback: function (value) {
                            if (Math.floor(value) === value && $.isNumeric(value)) {
                                return locale ? value.toLocaleString(locale) : value;
                            }
                            return value;
                        }

                        //,beginAtZero: true
                    }
                }]
            },
            legend: {
                display: (typeof (data.categories) === 'undefined' || data.categories.length === 1) ? false : true
            },
            tooltips: {
                callbacks: {
                    label: function (tooltipItem, data) {
                        let label = data.datasets[tooltipItem.datasetIndex].label || '';
                        let value = tooltipItem.yLabel !== undefined ? tooltipItem.yLabel : tooltipItem.value;
                        return label ? `${label}: ${locale ? value.toLocaleString(locale) : value}` : (locale ? value.toLocaleString(locale) : value);
                    }
                }
            }
        };

        var myChart = new Chart(ctx, {
            type: type,
            data: chartData,
            options: options
        });
        ctx.closest(".datalinq-chart").data("datalinq-chartobject", myChart);
    };

    this.randomColorRGBA = function (colorRGB) {
        var r, g, b;
        if (typeof (colorRGB) !== 'undefined') {
            r = colorRGB.split(",")[0];
            g = colorRGB.split(",")[1];
            b = colorRGB.split(",")[2];
        } else {
            var r = (Math.floor(Math.random() * 256));
            var g = (Math.floor(Math.random() * 256));
            var b = (Math.floor(Math.random() * 256));
        }

        var color = 'rgba(' + r + ',' + g + ',' + b + ',';

        return {
            background: color + '0.3)',
            border: color + '1)',
            hoverbackground: color + '0.6)'
        };
    };

    this.showListItem = function (id) {
        var $listItem = $(".datalinq-geo-element[data-geo-id='" + id + "']");
        // Element angeben, in dem gescrollt werden soll
        // sonst: übergeordnetes Element mit overflow-Definition finden
        // ansonsten: ganzes Window-Objekt scrollen
        var $scrollElement = $listItem.closest(".datalinq-scroll");
        if ($scrollElement.length === 0) {
            var $e = $listItem.parent();
            var hasFound = false;
            do {
                if ($e.css("overflow") === "auto" || $e.css("overflow-y") === "auto" || $e.css("overflow") === "scroll" || $e.css("overflow-y") === "scroll") {
                    $scrollElement = $e;
                    hasFound = true;
                } else
                    $e = $e.parent();
            } while (!hasFound && !$e.is(document));
            if (!hasFound)
                $scrollElement = $(window);
        }
        $scrollElement.scrollTop(dataLinq.calcOffsetTop($listItem[0]) - dataLinq.calcOffsetTop($scrollElement[0]));
        $listItem.parent()
            .children('.datalinq-geo-element-selected')
            .removeClass('datalinq-geo-element-selected');

        $listItem
            .addClass('datalinq-geo-element-selected')
            .trigger("click");
    };

    this.guid = function () {
        function s4() {
            return Math.floor((1 + Math.random()) * 0x10000)
                .toString(16)
                .substring(1);
        }
        return s4() + s4() + '-' + s4() + '-' + s4() + '-' +
            s4() + '-' + s4() + s4() + s4();
    };

    this.bindEvents = function (parent) {
        $(parent || 'body').find('.responsive-switch')
            .click(function () {
                $(this).closest('.responsive-container').toggleClass('showall');
                if ($(this).closest('.responsive-container').hasClass('showall')) {
                    $(this).html('Weniger anzeigen');
                } else {
                    $(this).html('Alles anzeigen');
                }
            });
    };

    // UpdateCombos (Select Elements)
    this.updateSelectElements = function (parent, selector) {
        $(parent).find(selector).each(function (i, e) {
            $select = $(e);

            dataLinq.updateSelectElement($select);
        });
    };
    this.updateSelectElement = function ($select, url) {
        if (_isLoading($select))
            return;

        _setLoading($select);

        var url = url || $select.data('url');
        console.log('updateSelectElement', url);
        var nameField = $select.data('namefield');
        var valueField = $select.data('valuefield');
        var defaultValue = $select.data('defaultvalue');
        var prependEmpty = $select.data('prepend-empty') === true;

        var autoOptimize2Select2 = $select.attr('select2') === 'never' ? false : true;

        if ($select.data('depends-on')) {
            $select.empty();

            var dependsOn = $select.data('depends-on').split(',');

            var hasUnsolvedDepencies = false;
            $.each(dependsOn, function (i, key) {
                if (url.indexOf('[' + key + ']') >= 0) {
                    hasUnsolvedDepencies = true;
                }
            });

            if (hasUnsolvedDepencies === true) {
                _unsetLoading($select);
                _wrapperElement($select).addClass('has-dependencies');
                return;
            } else {
                _wrapperElement($select).removeClass('has-dependencies');
            }
        }

        if (typeof (url) === "undefined") {
            if (prependEmpty) {
                $("<option value=''></option>").prependTo($select);
            }

            if (defaultValue.length !== 0) {
                $select.val(defaultValue).change();
            }
            if (defaultValue.length === 0 && prependEmpty) {
                $select.val("").change();
            }

            if ((autoOptimize2Select2 === true && $select.find("option").length > 20) || $select.attr("multiple") === "multiple") {
                $select.select2({ dropdownAutoWidth: true });
                $select.siblings(".select2-container").css("width", "");
            }

            if ($select.attr("multiple") === "multiple" && typeof (defaultValue) === "object" && defaultValue.length > 0) {
                var defaultValueMultiple = defaultValue[0].split(";");
                $select.val(defaultValueMultiple).change();
            }

            if ($select.closest(".datalinq-refresh-filter-container").length > 0) {
                dataLinq.updateViewFilter($select);
            }

            _unsetLoading($select);
        } else {
            $select.empty();

            $.ajax({
                url: dataLinq.baseUrl + "/datalinq/select/" + url,
                data: dataLinq.overrideModifyRequestData({ _f: 'json', _id: $select.attr('id') }),
                async: true,
                success: function (result) {
                    if (prependEmpty) {
                        $("<option value=''></option>").appendTo($select);
                    }
                    for (var i in result) {
                        var r = result[i];
                        //$("<option value='" + r[valueField] + "'" + (defaultValue == r[valueField] ? " selected" : "") + ">" + r[nameField] + "</option>").appendTo($select);
                        $("<option value='" + r[valueField] + "'>" + r[nameField] + "</option>").appendTo($select);
                    }
                    if (defaultValue.length !== 0)
                        $select.val(defaultValue).change();

                    if ((autoOptimize2Select2 === true && result.length > 20) || $select.attr("multiple") === "multiple") {
                        $select.select2({ dropdownAutoWidth: true });
                        $select.siblings(".select2-container").css("width", "");
                    }

                    if ($select.attr("multiple") === "multiple" && typeof defaultValue === "object" && defaultValue.length > 0) {
                        var defaultValueMultiple = defaultValue[0].split(";");
                        $select.val(defaultValueMultiple).change();
                    }

                    if ($select.closest(".datalinq-refresh-filter-container").length > 0) {
                        dataLinq.updateViewFilter($select);
                    }

                    _unsetLoading($select);
                },
                error: function () {
                    _unsetLoading($select);
                }
            });
        }
    };

    // Update Radio Buttons
    this.updateRadioElements = function (parent, selector) {

        $(parent).find(selector).each(function (i, e) {
            dataLinq.updateRadioElement($(e));
        });
    };
    this.updateRadioElement = function ($radio, url) {
        var url = dataLinq.baseUrl + "/datalinq/select/" + (url || $radio.attr('data-url'));
        var name = $radio.attr('data-name');
        var nameField = $radio.attr('data-namefield');
        var valueField = $radio.attr('data-valuefield');
        var defaultValue = $radio.attr('data-defaultvalue');

        $.ajax({
            url: url,
            data: dataLinq.overrideModifyRequestData({ _f: 'json', _id: $radio.attr('id') }),
            async: true,
            success: function (result) {
                for (var i in result) {
                    var r = result[i];

                    $("<input type='radio' value='" + r[valueField] + "' name='" + name + "' " + (defaultValue === r[valueField] ? "checked" : "") + " />" + r[nameField] + "<br/>").appendTo($radio);
                }
            }
        });
    }

    // UpdateScalar Elements (text spans etc)
    this.updateScalarElements = function (parent, selector) {

        $(parent).find(selector).each(function (i, e) {
            dataLinq.updateScalarElement($(e));
        });
    };
    this.updateScalarElement = function ($span, url) {
        var url = dataLinq.baseUrl + "/datalinq/select/" + (url || $span.attr('data-url'));
        var nameField = $span.attr('data-namefield');
        var defaultValue = $span.attr('data-defaultvalue');

        $.ajax({
            url: url,
            data: dataLinq.overrideModifyRequestData({ _f: 'json', _id: $span.attr('id') }),
            async: true,
            success: function (result) {
                if (result.length > 0)
                    $span.html(result[0][nameField]);
                else {
                    $span.html(defaultValue).css("color", "red").css("font-style", "italic");
                }
            }
        });
    };

    // Update DatePickers
    this.updateDatePickerElements = function (parent, selector) {

        $(parent).find(selector).each(function (i, e) {
            dataLinq.updateDatePickerElement($(e));
        });
    };
    this.updateDatePickerElement = function ($picker) {
        var enableTime = false;
        var dateFormat = "d.m.Y";

        if ($picker.data('datatype') === "DateTime") {
            enableTime = true;
            dateFormat = "d.m.Y H:i";
        }

        $picker.addClass("flatpickr").attr("placeholder", "Datum auswählen...");
        $picker.flatpickr({
            locale: "de",
            weekNumbers: true, // show week numbers
            enableTime: enableTime,
            dateFormat: dateFormat,
            time_24hr: true,
            onChange: function (dateobj, datestr) {
                var realName = $(this.input).attr("name").slice(0, -7);
                $("input[name='" + realName + "']").val(datestr);
            }
        });
    };

    // Update Copyable Elements
    this.updateCopyableElements = function (parent, selector) {
        dataLinq.updateCopyableElement($(parent).find(selector));
        
    };
    this.updateCopyableElement = function ($copyable) {
        $copyable
            .on('mouseenter', function (ev) {
                $(this).find(".datalinq-copy-button").show();
            })
            .on('mouseleave', function (ev) {
                $copyable.find(".datalinq-copy-button").hide();
            });

        $copyable
            .find(".datalinq-copy-button")
            .on("click", function () {
                dataLinq.copyToClipboard($(this).data("copy-value"));
            });
    }

    // Update Filterable Elements
    this.updateFilterableElements = function (parent, selector) {
        dataLinq.updateFilterableElement($(parent).find(selector));
        
    };
    this.updateFilterableElement = function ($filterable) {
        $filterable
            .on('mouseenter', function (ev) {
                $(this).find(".datalinq-update-filter").addClass("datalinq-update-filter-hover");
            })
            .on('mouseleave', function (ev) {
                $(this).find(".datalinq-update-filter").removeClass("datalinq-update-filter-hover");
            });
    };

    // Update Refresh Tickers
    this.updateRefreshTickerElements = function (parent, selector) {
        if (parent !== "body" && typeof ($(parent).attr("data-refresh-ticker-isactive")) !== "undefined") {
            if ($(parent).attr("data-refresh-ticker-isactive") === "false")
                $(parent).find(selector).prop('checked', false);
            else {
                $(parent).find(selector).prop('checked', true);
                dataLinq._tickTimer.start();
            }
        }
        if (parent !== "body" && typeof ($(parent).attr("data-refresh-ticker-isactive")) === "undefined") {
            dataLinq._tickTimer.start();
        }
    };

    var _wrapperElement = function ($element) {
        var $fieldWrapper = $element.closest('.datalinq-filter-field-wrapper');
        if ($fieldWrapper.length === 1)
            return $fieldWrapper;

        return $element;
    };
    var _setLoading = function ($element) {
        _wrapperElement($element).addClass('loading');
    };
    var _unsetLoading = function ($element) {
        _wrapperElement($element).removeClass('loading');
    };
    var _isLoading = function ($element) {
        return _wrapperElement($element).hasClass('loading');
    }


    this.copyToClipboard = function copy(val) {
        var $temp = $("<input>");
        $temp.css('height', '0px').css('position', 'absolute').css('left', '-1000px').css('top', '-1000px');
        $("body").append($temp);

        $temp.val(val).select();

        // Falls Input formatiert übernommen werden soll:
        // https://stackoverflow.com/questions/22581345/click-button-copy-to-clipboard-using-jquery
        //$temp.attr("contenteditable", true)
        //     .html(val).select()
        //     .on("focus", function () { document.execCommand('selectAll', false, null) })
        //     .focus();

        document.execCommand("copy");
        $temp.remove();
    };

    this.updateFilter = function (sender, id, name, val) {
        var $filter = $(sender).closest('.datalinq-include, .datalinq-include-click').find(".datalinq-refresh-filter-container");
        if (id.length > 0)
            $filter = $('#' + id);
        var $field = $filter.find("input[name=" + name + "], select[name=" + name + "], textarea[name=" + name + "]");
        $field.val(val).trigger('change');
        $filter.find("button.apply").click();
    };

    this.replaceParameter4MassAttribution = function (p, sender) {
        var maxCount = 0;
        $.each(p, function (i, e) {
            // Wenn ein Parameter mit [ anfängt und mit ] aufhört
            if (/^\[.+\]$/.test(e)) {
                var list = [];
                var $selected = $(sender).closest('.datalinq-include, .datalinq-include-click').find(".datalinq-selected[data-pk]");
                $selected.each(function (index, element) {
                    if ((typeof ($(element).data("pk")) !== 'undefined') && $(element).data("pk") !== "")
                        list.push($(element).data("pk"));
                });
                p[i] = list.join(",");
                if (list.length > maxCount)
                    maxCount = list.length;
            }
        });
        return maxCount;
    };

    this.openDialog = function (sender, id, p) {
        if ($(sender).hasClass("datalinq-mass-attribution-deactivated"))
            return;
        var url = dataLinq.baseUrl + "/datalinq/select/" + id;
        var $baseView = $(sender).closest('.datalinq-include, .datalinq-include-click');
        //$baseView.attr("data-scroll-position", dataLinq.calcOffsetTop(sender) - $(sender).offset().top);
        $baseView.attr("data-scroll-position", dataLinq.calcOffsetTop(sender));
        var baseViewId = $baseView.attr("id");
        var dialogAttributes = $(sender).data("dialog-attributes");

        var $loader = $("<div class='datalinq-modal-loader'></div>").appendTo($('body'));
        var params = $.extend(true, {}, p);
        var count = dataLinq.replaceParameter4MassAttribution(params, sender);
        var countText = " (1 Datensatz)";
        if (count > 1)
            countText = " (" + count + " Datensätze)";
        var dialogTitle = (typeof (dialogAttributes.dialogTitle) !== "undefined" ? dialogAttributes.dialogTitle : "") + countText;

        $.ajax({
            url: url,
            data: dataLinq.overrideModifyRequestData(params),
            async: false,
            success: function (result) {
                $('body').dataLinq_modal({
                    title: dialogTitle,
                    width: (typeof (dialogAttributes.dialogWidth) !== "undefined" ? dialogAttributes.dialogWidth : '50%'),
                    height: (typeof (dialogAttributes.dialogHeight) !== "undefined" ? dialogAttributes.dialogHeight : '90%'),
                    onload: function ($content) {
                        $content.css('padding', '10px');
                        $(result).addClass("datalinq-edit-mask").appendTo($content);
                        $content.data("baseViewId", baseViewId);
                        dataLinq.updateElement($content);
                        // z-index runtersetzen, sonst wird select2 nicht angezeigt
                        $content.closest(".datalinq-modal").css('z-index', 1001);
                        $loader.remove();
                        // Wenn Dialog aufgerufen wird: mögliche Refresh-Timer pausieren
                        dataLinq._tickTimer.stop();
                    },
                    onclose: function () {
                        dataLinq._tickTimer.start();
                    }
                });
            }
        });
    };

    this.executeNonQuery = function (sender, id, p) {
        var params = $.extend(true, {}, p);
        var count = dataLinq.replaceParameter4MassAttribution(params, sender);
        var countText = "";
        if (count === 1)
            countText = "(1 Datensatz ausgewählt)\n\n";
        else if (count > 1)
            countText = "(" + count + " Datensätze ausgewählt)\n\n";

        var dialogAttributes = $(sender).data("dialog-attributes");
        var send = false;
        if (typeof (dialogAttributes.confirmText) === "undefined") {
            send = true;
        } else {
            if (confirm(countText + dialogAttributes.confirmText) === true)
                send = true;
        }
        if (send) {
            var url = dataLinq.baseUrl + "/datalinq/ExecuteNonQuery/" + id;
            var $baseView = $(sender).closest('.datalinq-include, .datalinq-include-click');
            $baseView.attr("data-scroll-position", $(sender).offset().top);

            $.ajax({
                url: url,
                data: dataLinq.overrideModifyRequestData(params),
                async: false,
                type: 'post',
                success: function (result) {
                    if (result.success) {
                        dataLinq.refresh($(sender));
                        dataLinq.events.fire('afterexecutenonquery', dataLinq, { id: id });
                    } else {
                        dataLinq.alert(result.exception);
                    }
                }
            });
        }
    };

    this.submitForm = function (sender) {
        var $form = $(sender).closest("form");
        $(sender).removeClass("error");
        //dataLinq.events.fire('onformsubmit', dataLinq, { form: $form});

        var url = dataLinq.overrideModifyRequestUrlData($form.attr("action"));
        var baseViewId = $form.closest("div[data-baseViewId!='']").data("baseViewId");
        var $baseView = $("#" + baseViewId);
        if ($baseView.length === 0)
            $baseView = $(sender).closest('.datalinq-include, .datalinq-include-click');

        if (dataLinq.validateForm($form) === true) {
            $.ajax({
                url: url,
                method: $form.attr("method"),
                data: $form.serialize(),
                async: false,
                success: function (result) {
                    if (result.success) {
                        $('body').dataLinq_modal('close');
                        dataLinq.refresh($baseView.children().first());
                    } else {
                        dataLinq.alert(result.exception);
                    }
                }
            });
        } else
            $(sender).addClass("error");
    };

    this.validateForm = function (form) {
        errMsg = "";
        var $form = $(form);

        // IE 11 kann "reportValidity" nicht
        // mit "checkValidity" kommt nur true/false, aber keine Rückmeldung
        if ($form.get(0).checkValidity() === true) {
            return true;
        }
        else {
            // Zuerst (alte) Fehlerhinweise löschen
            var $elements = $form.find("input, select, textarea, radio, checkbox");
            $elements.each(function (i, e) {
                var $e = $(e);
                $e.removeClass("error");
                if ($e.is("select") && $e.next().is("span.select2"))
                    $e.next("span.select2").find("span.select2-selection").removeClass("error");
            });

            $elements.each(function (i, e) {
                var $e = $(e);
                // required
                if ($e.attr("required") === "required" && $e.val() === null) {
                    errMsg = "Feld darf nicht leer sein";
                    $e.addClass("error");
                    if ($e.is("select") && $e.next().is("span.select2"))
                        $e.next("span.select2").find("span.select2-selection").addClass("error");

                    $e.prev("div.datalinq-error-msg").remove();
                    $e.before($("<div class='datalinq-error-msg'>" + errMsg + "</div>"));
                }

                if (typeof (e.reportValidity) === "function") {
                    // reportValidity zeigt auch ErrorMessage an
                    if (e.reportValidity() === false)
                        $(e).addClass("error");
                }
                // Fallback, falls IE
                else {
                    if (e.checkValidity() === false)
                        $(e).addClass("error");
                }
            });

            return false;
        }

    };

    this.timer = function (callback, duration, arg) {
        var _timer = 0;
        var _callback = callback;
        var _duration = duration;
        var _arg = arg;
        this.SetArgument = function (arg) { _arg = arg; };
        this.SetDuration = function (d) {
            _duration = d;
        };
        this.Duration = function () { return _duration; };
        this.Start = function (arg) {
            window.clearTimeout(_timer);
            if (arg)
                _arg = arg;
            if (_duration == 0) {
                if (_arg)
                    _callback(_arg);
                else
                    _callback();
            }
            else {
                if (_arg)
                    _timer = window.setTimeout(function () { _callback(_arg); }, _duration);
                else
                    _timer = window.setTimeout(_callback, _duration);
            }
        };
        this.StartWith = function (callbackFunction) {
            _callback = callbackFunction;
            this.Start();
        };
        this.Stop = function () { window.clearTimeout(_timer); };
        this.Exec = function () {
            window.clearTimeout(_timer);
            if (_arg)
                _callback(_arg);
            else
                _callback();
        };
        this.start = this.Start;
        this.stop = this.Stop;
        this.startWidth = this.StartWidth;
        this.exec = this.Exec;
    };

    this.delayed = function (callback, duration, arg) {
        var timer = new dataLinq.timer(callback, duration ? duration : 1, arg);
        timer.Start();
    };

    this._tickTimer = new this.timer(function () {
        $('.datalinq-refresh-ticker').each(function (i, e) {
            var isActive = $(e).closest('.datalinq-include, .datalinq-include-click').attr("data-refresh-ticker-isactive");

            if (typeof (isActive) === "undefined" || isActive === "true") {
                if ($(e).is(':checked')) {
                    var value = parseInt($(e).attr('data-value'));
                    if (value <= 0) {
                        dataLinq.refresh(e);
                    } else {
                        value--;
                        $(e).attr('data-value', value);
                        $("label[for='" + $(e).attr('id') + "']").html('&nbsp' + $(e).attr('data-label') + '&nbsp' + value + "&nbsp;Sekunden");
                    }
                }
            }
        });
        dataLinq._tickTimer.start();
    }, 1000);

    this.showSplashSreen = function (text, duration) {
        var width = Math.min(480, $(window).width());
        var height = 120;

        var $splash = $("<div>").addClass('datalinq-splashscreen').css({
            width: width,
            height: height,
            left: ($(window).width() - width) / 2,
            top: ($(window).height() - height) / 3
        }).appendTo('body');
        $('<div>' + text + "</div>")
            .addClass('datalinq-splashscreen-text')
            .appendTo($splash);

        //$splash.fadeIn(function () {
        var timer = new dataLinq.timer(function ($splash) {
            //$splash.fadeOut(function () {
            $splash.remove();
            //});
        }, duration, $splash);
        timer.start();
        //});

    };

    this.encodeUntrustedHtml = function (html, isMarkdown) {
        var result = html.replaceAll('<', '&lt;').replaceAll('>', '&gt;');

        return result;
    };

    this.alert = function (message, title, onClose) {
        title = title || 'DataLinq Message';

        var height = Math.max(200, message.countChar('\n') * 25);

        $('body').dataLinq_modal({
            title: title,
            onload: function ($content) {
                var imgUrl = '';
                if (title.toLowerCase() === 'hinweis' || title.toLowerCase() === 'info') {
                    imgUrl = dataLinq.baseUrl + '/_content/E.DataLinq.Web/css/img/info-80.png';
                } else if (title.toLowerCase() === 'fehler' || title.toLowerCase() === 'error') {
                    imgUrl = dataLinq.baseUrl + '/_content/E.DataLinq.Web/css/img/error-80.png';
                }
                if (imgUrl !== '') {
                    $("<img src='" + imgUrl + "' >").css({
                        padding: '5px',
                        float: 'left'
                    }).appendTo($content);
                }
                var p = $('<p>')
                    .css('font-size', '1.1em')
                    .appendTo($content)
                    .html(dataLinq.encodeUntrustedHtml(message).replaceAll('\n', '<br/>').replaceAll('  ', '&nbsp;&nbsp;'));
            },
            width: '640px', height: height + 'px',
            onclose: onClose
        });
    };

    this.calcOffsetTop = function (elem) {
        var location = 0;
        if (elem.offsetParent) {
            do {
                location += elem.offsetTop;
                elem = elem.offsetParent;
            } while (elem);
        }
        return location;
    };

    this.multiSelect = function ($e, args) {
        // geändert von https://stackoverflow.com/questions/17964108/select-multiple-html-table-rows-with-ctrlclick-and-shiftclick
        var settings = $.extend({
            selectableItems: ".datalinq-selectable",
            className: "datalinq-selected",
            onSelect: function () { }
        }, args || {});

        $e.find(settings.selectableItems).each(function (i, that) {
            var $selectable = $(this);
            $selectable.on('mouseup', function (ev) {
                if (ev.shiftKey || ev.ctrlKey) ev.preventDefault();
                var $elem = $(this);

                if (ev.shiftKey) {
                    var $nearest = $elem.prevAll('.' + settings.className);
                    if ($nearest.length > 0) {
                        $elem.prevUntil($nearest).addBack().addClass(settings.className);
                    } else {
                        $nearest = $elem.nextAll('.' + settings.className);
                        if ($nearest.length > 0) {
                            $elem.nextUntil($nearest).addBack().addClass(settings.className);
                        } else {
                            $elem.addClass(settings.className);
                        }
                    }
                } else if (ev.ctrlKey) {
                    $elem.toggleClass(settings.className);
                } else {
                    // Wenn vorher mehrere aktiv waren, nur das aktuelle aktiv setzen. Wenn nur aktuelles Element selektiert war, dieses aufheben.
                    if ($elem.parent().children('.' + settings.className).length > 1) {
                        $elem.parent().children(settings.selectableItems).removeClass(settings.className);
                        $elem.addClass(settings.className);
                    }
                    else
                        $elem.toggleClass(settings.className);
                    $elem.siblings(settings.selectableItems).removeClass(settings.className);
                }
                dataLinq.events.fire('onselected', dataLinq, { element: $elem });
            });
            $selectable.on('mousedown', function (ev) {
                if (ev.shiftKey || ev.ctrlKey) ev.preventDefault();
            });
        });
    };

    this.implementEventController = function (obj) {
        obj.events = new dataLinq.eventController(obj);
    };

    this.init = function (oninit) {
        //$('#datalinq-header').html('username...');

        $('#datalinq-body').on('drop', function (e) {
            e.preventDefault();
        }).on('dragover', function (e) {
            e.preventDefault();
        });

        $("body").on("keyup", function (event) {
            if (event.keyCode === 27) {
                $('body').dataLinq_modal('close');
                dataLinq._tickTimer.start();
            }
        });

        dataLinq.overrideInit(oninit);

        dataLinq.updateElement('body');

        dataLinq._tickTimer.start();
    };

    this._ajaxXhrFields = {
        withCredentials: true
    };

    // Overrides
    this.overrideInit = function (oninit) {};

    this.overrideUpdateElement = function (parent) {};

    this.overrideModifyRequestData = function(dataObject) {
        return dataObject;
    };
    this.overrideModifyRequestUrlData = function (url) {
        return url;
    }
}();

String.prototype.countChar = function (char) {
    try {
        return this.split(char).length;
    } catch (e) {
        return 0;
    }
};