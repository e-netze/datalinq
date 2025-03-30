using E.DataLinq.Core;
using E.DataLinq.Core.Reflection;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Models.Razor;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace E.DataLinq.Web.Razor;

[HelpDescription(@"
Die Klasse ist eine Hilfsklasse, die innerhalb der Razor Umgebeung von DataLinq genutzt werden kann. Der Zugriff verfolgt über den globalen Namen DataLinqHelper bzw. der Kurzform DLH.
Die Methoden dieser Klasse ermöglichen dynamische Inhalte innerhalb einer DataLinq Seite, wie das nachladen von Views, Editieren, Karten, Sortierung usw. 
            ")]
public class DataLinqHelperClassic : IDataLinqHelper
{
    private readonly HttpContext _httpContext;
    private readonly DataLinqService _currentDatalinqService;
    private readonly IDataLinqUser _ui;
    private readonly IRazorCompileEngineService _razor;

    public DataLinqHelperClassic(
        HttpContext httpContext,
        DataLinqService currentDatalinqService,
        IRazorCompileEngineService razorService,
        IDataLinqUser ui)
    {
        _httpContext = httpContext;
        _currentDatalinqService = currentDatalinqService;
        _ui = ui;
        _razor = razorService;
    }

    #region Load/Fetch Data

    [HelpDescription("Die Methode holt Daten aus einer Datalinq Query ab und übergibt das Ergebnis an eine Javascript Funktion.")]
    public object JsFetchData(
            [HelpDescription("Gibt die Id der Query in folgender Form an: endpoint-id@query-id")]
            string id,
            [HelpDescription("Der Name einer Javascript Funktion, der die Daten übergeben werden: window.my_dataprocessor_funtion = function(data) {...}")]
            string jsCallbackFuncName,
            string filter = "",
            [HelpDescription("Gibt an, die Id urlkodiert werden sollte. Standardmäßig sollte hier immer true übergeben werden")]
            bool encodeUrl = true
        )
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<div class='datalinq-fetch' data-source='");
        sb.Append(ParseUrl(id, encodeUrl));
        sb.Append("' data-js-callback='");
        sb.Append(ParseUrl(jsCallbackFuncName, encodeUrl));
        if (!String.IsNullOrEmpty(filter))
        {
            sb.Append("' data-filter='");
            sb.Append(ParseUrl(filter, encodeUrl));
        }
        sb.Append("'></div>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Übergibt records an eine Javascript Funktion.")]
    public object RecordsToJs(
            [HelpDescription("Records, die an eine Javascript Funktion übergeben werden sollten.")]
            IDictionary<string, object>[] records,
            [HelpDescription("Der Name einer Javascript Funktion, der die Daten übergeben werden: window.my_dataprocessor_funtion = function(data) {...}")]
            string jsCallbackFuncName
         )
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("<script>");
        sb.Append("$(function(){");  // load, when page is rendered

        sb.Append($"window.{jsCallbackFuncName}(");
        if (records != null)
        {
            bool firstObject = true;
            sb.Append("[");
            foreach (var record in records)
            {
                if (firstObject) { firstObject = false; } else { sb.Append(","); }
                sb.Append("{");

                bool firstProperty = true;
                foreach (var kvp in record)
                {
                    if (firstProperty) { firstProperty = false; } else { sb.Append(","); }
                    sb.Append($"{kvp.Key}:{System.Text.Json.JsonSerializer.Serialize(kvp.Value)}");
                }
                sb.Append("}");
            }
            sb.Append("]");
        }
        sb.Append(");");

        sb.Append("});");
        sb.Append("</script>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Die Methode lädt die Records aus der angegeben Query (endpoint@query). Die Abfrage passiert serverseitig während des Renderns der Seite.")]
    async public Task<IDictionary<string, object>[]> GetRecordsAsync(
        [HelpDescription("Gibt die Id der Query in folgender Form an: endpoint-id@query-id")]
        string id,
        [HelpDescription("Feldnamen mit Werten, Übergabe wie bei einem URL-Parameter, bspw ((NAME=Franz&STRASSE=Eberweg))")]
        string filter = "",
        [HelpDescription("Feldnamen mit Komma getrennt in der Reihenfolge, in der sortiert werden soll. Um absteigend zu sortieren, kann ein Minus(-) vor den Feldnamen gestellt werden, bspw ((PLZ,STRASSE,-HAUSNR))")]
        string orderby = "")
    {
        var dataLinqRoute = new DataLinqRoute(id, _httpContext);

        switch ((dataLinqRoute.HasEndpoint, dataLinqRoute.HasQuery, dataLinqRoute.HasView))
        {
            case (true, true, false):
                break;
            default:
                throw new Exception($"{id} is not a valid id for an datalinq query (endpoint@query)");
        }

        var arguments = String.IsNullOrWhiteSpace(filter)
            ? new NameValueCollection()
            : HttpUtility.ParseQueryString(filter);

        if (!String.IsNullOrWhiteSpace(orderby))
        {
            arguments["_orderby"] = orderby;
        }

        var result = await _currentDatalinqService.QueryAsync(_httpContext, id, arguments, isDomainQuery: false);

        if (result.succeeded && result.result is IDictionary<string, object>[])
        {
            return (IDictionary<string, object>[])result.result;
        }
        else if (result.succeeded && result.result is object[])
        {
            return ((object[])result.result)
                        .Where(o => o is ExpandoObject || o is IDictionary<string, object>)
                        .Select(o => (IDictionary<string, object>)o)
                        .ToArray();
        }
        else if (result.succeeded == false)
        {
            // ToDo: Error Handling
        }
        return null;
    }

    #endregion

    #region Load View

    [HelpDescription("Die Methode bindet einen View ein. Der View wird sofort nach dem Aufbau des übergeordneten Views geladen und angezeigt")]
    public object IncludeView(
        [HelpDescription("Gibt die Id des Views in folgender Form an: endpoint-id@query-id@view-id")]
        string id,
        [HelpDescription("Gibt an, die Id urlkodiert werden sollte. Standardmäßig sollte hier immer true übergeben werden")]
        bool encodeQueryString = true)
    {
        return _razor.RawString("<div class='datalinq-include' data-source='" + ParseUrl(id, encodeQueryString) + "'></div>");
        //return IncludeView(id, String.Empty, String.Empty, encodeQueryString);
    }

    [HelpDescription("Hier werden anstelle einer langen URL die Filter und Sortierungsfelder getrennt eingegeben. Das ist von Bedeutung, wenn im damit eingebundenen View nach Werten gefiltert werden soll, die im übergeordneten View mitgegeben werden.")]
    public object IncludeView(
        [HelpDescription("Gibt die Id des Views in folgender Form an: endpoint-id@query-id@view-id")]
        string id,
        [HelpDescription("Feldnamen mit Werten, Übergabe wie bei einem URL-Parameter, bspw ((NAME=Franz&STRASSE=Eberweg))")]
        string filter,
        [HelpDescription("Feldnamen mit Komma getrennt in der Reihenfolge, in der sortiert werden soll. Um absteigend zu sortieren, kann ein Minus(-) vor den Feldnamen gestellt werden, bspw ((PLZ,STRASSE,-HAUSNR))")]
        string orderby = "",
        [HelpDescription("Gibt an, die Id urlkodiert werden sollte. Standardmäßig sollte hier immer true übergeben werden")]
        bool encodeUrl = true)
    {
        return _razor.RawString("<div class='datalinq-include' data-source='" + ParseUrl(id, encodeUrl) + "' data-filter='" + ParseUrl(filter, encodeUrl) + "' data-orderby='" + ParseUrl(orderby, encodeUrl) + "'></div>");
    }

    [HelpDescription(@"Die Methode bindet einen View ein. Der View wird nicht sofort nach dem Aufbau des übergeordneten Views geladen und angezeigt, sondern erst, wenn der Anwender auf einen Button klickt")]
    public object IncludeClickView(
        [HelpDescription("Gibt die Id des Views in folgender Form an: endpoint-id@query-id@view-id")]
        string id,
        [HelpDescription("Text der Schaltfläche, der die View hinzulädt")]
        string text,
        [HelpDescription("Gibt an, die Id urlkodiert werden sollte. Standardmäßig sollte hier immer true übergeben werden")]
        bool encodeUrl = true)
    {
        return _razor.RawString("<div class='datalinq-include-click' data-source='" + ParseUrl(id, encodeUrl) + "' data-header='" + text + "'></div>");
    }

    [HelpDescription("Hier werden anstelle einer langen URL die Filter und Sortierungsfelder getrennt eingegeben. Das ist von Bedeutung, wenn im damit eingebundenen View nach Werten gefiltert werden soll, die im übergeordneten View mitgegeben werden.")]
    public object IncludeClickView(
        [HelpDescription("Gibt die Id des Views in folgender Form an: endpoint-id@query-id@view-id")]
        string id,
        [HelpDescription("Text der Schaltfläche, der die View hinzulädt")]
        string text,
        [HelpDescription("Feldnamen mit Werten, Übergabe wie bei einem URL-Parameter, bspw ((NAME=Franz&STRASSE=Eberweg))")]
        string filter,
        [HelpDescription("Feldnamen mit Komma getrennt in der Reihenfolge, in der sortiert werden soll. Um absteigend zu sortieren, kann ein Minus(-) vor den Feldnamen gestellt werden, bspw ((PLZ,STRASSE,-HAUSNR))")]
        string orderby = "",
        [HelpDescription("Gibt an, die Id urlkodiert werden sollte. Standardmäßig sollte hier immer true übergeben werden")]
        bool encodeUrl = true)
    {
        return _razor.RawString("<div class='datalinq-include-click' data-source='" + ParseUrl(id, encodeUrl) + "' data-filter='" + ParseUrl(filter, encodeUrl) + "' data-orderby='" + ParseUrl(orderby, encodeUrl) + "' data-header='" + text + "'></div>");
    }

    [HelpDescription("Die Methode erzeugt einen Button, mit dem der Anwender den View durch klick aktualisieren kann. Es wird dabei immer nur der View aktualisiert, in dem der Button mit dieser Methode engefügt wurde.")]
    public object RefreshViewClick(
        [HelpDescription("Der Text für dem Button")]
        string label = "Aktualisieren",
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<button ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-button apply");
        AppendHtmlAttribute(sb, "onclick", "dataLinq.refresh(this)");
        sb.Append(">" + label + "</button>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Die Methode erzeugt einen Button, über den ein View automatisch nach einer Zeitspanne aktualisiert wird. Aktualisiert wird abei nur jener View, in dem sich der Button befinden. Der Anwender kann den Button über Checkbox aktiv bzw. inaktiv schalten.")]
    public object RefreshViewTicker(
        [HelpDescription("Der Text für dem Button")]
        string label = "Wird aktualisiert in",
        [HelpDescription("Gibt einen Wert in Sekunden an, nach der die View aktualisiert wird.")]
        int seconds = 60,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" }))")]
        object htmlAttributes = null,
        [HelpDescription("Gibt an, ob der Timer zu Beginn bereits aktiv ist, oder erst vom Anwender durch Klick auf das Checkbox Symbol aktiv wird.")]
        bool isActive = true)
    {
        StringBuilder sb = new StringBuilder();

        string id = Guid.NewGuid().ToString("N");

        sb.Append("<div ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-refresh-ticker-container");
        sb.Append(">");
        sb.Append("<input ");
        AppendHtmlAttribute(sb, "class", "datalinq-refresh-ticker");
        AppendHtmlAttribute(sb, "data-value", seconds.ToString());
        AppendHtmlAttribute(sb, "data-label", label);
        AppendHtmlAttribute(sb, "type", "checkbox");
        AppendHtmlAttribute(sb, "id", id);
        if (isActive)
        {
            AppendHtmlAttribute(sb, "checked", "checked");
        }

        sb.Append(">");
        sb.Append("</input>");

        sb.Append("<label ");
        AppendHtmlAttribute(sb, "style", "display:inline");
        AppendHtmlAttribute(sb, "for", id);
        sb.Append(">");
        sb.Append("&nbsp;" + label + "&nbsp;" + seconds + "&nbsp;Sekunden");
        sb.Append("</label>");

        sb.Append("</div>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Die Methode erzeugt ein Steuerelement zum Sortieren eines Views. Das Element klappt auf, wenn auf einen Button geklickt wird. Danach kann der Anwender über Checkboxen bestimmen, nach welchen Eigenschaften sortiert wird bzw. ob auf- oder absteigend sortiert werden soll. Nach Bestätigung der Angaben wird der View, in dem das Steuerelement eingebaut wurde aktualisiert.")]
    public object SortView(
        [HelpDescription("Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes (z.B.: \"Sortierung\")")]
        string label,
        [HelpDescription("Ein String-Array mit Feldern, nach denen sortiert werden kann ((z.B.: new string[]{ \"ORT\",\"STR\" } )). Die Felder entsprechen hier den Feldnamen, wie sie auch innerhalb des Records vorkommen.")]
        string[] orderFields,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Boolscher Wert, der angibt, ob die Sortierung zu Beginn geöffnet (aufgeklappt) ist")]
        bool isOpen = false)
    {
        if (orderFields == null)
        {
            throw new AggregateException("orderFields==null");
        }

        var dict = new Dictionary<string, object>();
        foreach (var orderField in orderFields)
        {
            if (!dict.ContainsKey(orderField))
            {
                dict.Add(orderField, null);
            }
        }

        return SortView(label, dict, htmlAttributes, isOpen);
    }

    public object SortView(
        string label,
        //[HelpDescription("Hier wird anstelle des von Strings ein Dictionary übergeben. Die Keys entspechend den Feldnamen von oben. Die Values geben ein anonymes Objekt an, mit dem Bespiesweise die Anzeigenamen der Felder bestimmt werden kann. Außerdem kann die Checkbox angehakt werden, sinnvoll etwa bei einem intialem Order (aus der Query). ((orderFields: new Dictionary&lt;string,object&gt;(){\n   {\"ORT\", new { displayname=\"Ort/Gemeinde\" }},\n   {\"STR\", new { displayname=\"Strasse\", @checked=true, checkedDesc=true} }<br/>}))")]
        [HelpDescription("Hier wird anstelle von Strings ein Dictionary übergeben. Die Keys entspechend den Feldnamen von oben. Die Values geben ein anonymes Objekt an, mit dem Bespielsweise die Anzeigenamen der Felder bestimmt werden kann. ((orderFields: new Dictionary&lt;string,object&gt;(){\n   {\"ORT\", new { displayname=\"Ort/Gemeinde\" }},\n   {\"STR\", new { displayname=\"Strasse\"} }<br/>}))")]
        Dictionary<string, object> orderFields,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes=null,
        [HelpDescription("Boolscher Wert, der angibt, ob die Sortierung zu Beginn geöffnet (aufgeklappt) ist")]
        bool isOpen = false)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("<div ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-refresh-ordering-container collapsed");  // Ist unabhängig von isOpen immer collapsed. Wenn isOpen == true wird dann beim rendern onclick des Buttons getriggert und "collapsed" wirder entfernt. 
        AppendHtmlAttribute(sb, "data-isOpen", isOpen.ToString());
        sb.Append(">");

        sb.Append("<button ");
        AppendHtmlAttribute(sb, "class", "datalinq-button menu");
        AppendHtmlAttribute(sb, "onclick", "$(this).closest('.datalinq-refresh-ordering-container').toggleClass('collapsed');$(this).next('.datalinq-refresh-ordering-body').slideToggle()");
        sb.Append(">" + label + "</button>");

        sb.Append("<div ");
        AppendHtmlAttribute(sb, "class", "datalinq-refresh-ordering-body");
        AppendHtmlAttribute(sb, "style", "display:none");
        sb.Append(">");

        sb.Append("<table>");
        sb.Append("<tr><th>Feld</th><th>Absteigend</th></tr>");
        foreach (string orderField in orderFields.Keys)
        {
            var orderProperties = ToDictionary(orderFields[orderField]);
            bool checkedAsc = Convert.ToBoolean(GetDefaultValueFromRecord(orderProperties, "checked", false));
            bool checkedDesc = Convert.ToBoolean(GetDefaultValueFromRecord(orderProperties, "checkedDesc", false));

            sb.Append("<tr>");
            sb.Append("<td>");
            sb.Append("<label>");
            sb.Append("<input");
            AppendHtmlAttributes(sb, new { type = "checkbox", name = orderField, onclick = "dataLinq.updateViewOrdering(this)" }, "datalinq-ordering-field");
            //AppendHtmlAttributes(sb, new { type = "checkbox", name = orderField, onclick = "dataLinq.updateViewOrdering(this)" }, "datalinq-ordering-field" + (checkedAsc ? " initial" : ""));
            //if (checkedAsc == true)
            //    sb.Append(" checked ");
            sb.Append("/>&nbsp;" + GetDefaultValueFromRecord(orderProperties, "displayname", orderField) + "</label>");
            sb.Append("</td>");
            sb.Append("<td style='text-align:center'>");
            sb.Append("<input");
            AppendHtmlAttributes(sb, new { type = "checkbox", name = orderField, onclick = "dataLinq.updateViewOrdering(this)" }, "datalinq-ordering-desc");
            //AppendHtmlAttributes(sb, new { type = "checkbox", name = orderField, onclick = "dataLinq.updateViewOrdering(this)" }, "datalinq-ordering-desc" + (checkedAsc ? " initial" : ""));
            //if (checkedDesc == true)
            //    sb.Append(" checked ");
            sb.Append("/>");
            sb.Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        sb.Append(RefreshViewClick("Übernehmen").ToString());
        sb.Append("</div>");

        sb.Append("</div>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Die Methode erzeugt ein Steuerelement zum Filtern eines Views. Das Element klappt auf, wenn auf einen Button geklickt wird. Danach kann der Anwender über Eingabefelder/Auswahllisten bestimmen, nach welchen Eigenschaften gefiltert wird. Nach Bestätigung der Angaben wird der View, in dem das Steuerelement eingebaut wurde aktualisiert.")]
    public object FilterView(
        [HelpDescription("Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes ((z.B.: \"Filter setzen\"))")]
        string label,
        [HelpDescription("Ein String-Array mit Parametern, nach denen gefiltert werden kann ((z.B.: new string[]{ \"ort\",\"strasse\" } )). Die Parameter entsprechen hier jenen Parameternamen, die an die enprechende Abfrage für den View übergeben werden können.")]
        string[] filterParameters,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Boolscher Wert, der angibt, ob der Filter zu Beginn geöffnet (aufgeklappt) ist")]
        bool isOpen = false)
    {
        if (filterParameters == null)
        {
            throw new AggregateException("filterParameters==null");
        }

        var dict = new Dictionary<string, object>();
        foreach (var filterParameter in filterParameters)
        {
            if (!dict.ContainsKey(filterParameter))
            {
                dict.Add(filterParameter, null);
            }
        }

        return FilterView(label, dict, htmlAttributes, isOpen);
    }

    public object FilterView(
        string label,
        [HelpDescription("Hier wird anstelle von Strings ein Dictionary übergeben. Die Keys entsprechen den Parametern von oben. Die Values geben ein anonymes Objekt an, mit dem Beispielsweise die Anzeigenamen der Parameter bestimmt werden können oder auch, dass es ein Datumsfeld ist (siehe TextFor). Außerdem besteht die Möglichkeit source, valueField, nameField und prependEmpty anzugeben. Damit wird eine Auswahllist erzeugt. Die Möglichkeiten könne unter der Methode ComboFor() nachgelesen werden. ((filterParameters: new Dictionary&lt;string,object&gt;(){\n   {\"ort\", new { displayname=\"Ort/Gemeinde\" }},\n     {\"datum\", new { displayname=\"Datum\", dataType=DataType.Date }},\n   {\"str\", new { displayname=\"Strasse\", source=\"endpoint-id@query-id\", valueField=\"STR\", nameField=\"STR_LANGTEXT\", prependEmpty=true  }}<br/>}})) Um Auswahlmenüs (ComboBox) mit Mehrfach-Auswahl zu ermöglich, kann das Attribut ((multipe='multiple')) mitgegeben werden.")]
        Dictionary<string, object> filterParameters,
        object htmlAttributes = null,
        [HelpDescription("Boolscher Wert, der angibt, ob der Filter zu Beginn geöffnet (aufgeklappt) ist")]
        bool isOpen = false)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("<div ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-refresh-filter-container collapsed");
        AppendHtmlAttribute(sb, "data-isOpen", isOpen.ToString());
        sb.Append(">");

        sb.Append("<button ");
        AppendHtmlAttribute(sb, "class", "datalinq-button menu");
        AppendHtmlAttribute(sb, "onclick", "$(this).closest('.datalinq-refresh-filter-container').toggleClass('collapsed');$(this).next('.datalinq-refresh-filter-body').slideToggle()");
        sb.Append(">" + label + "</button>");

        sb.Append("<div ");
        AppendHtmlAttribute(sb, "class", "datalinq-refresh-filter-body");
        AppendHtmlAttribute(sb, "style", "display:none");
        sb.Append(">");

        foreach (string filterParameter in filterParameters.Keys)
        {
            var filterProperties = ToDictionary(filterParameters[filterParameter]);
            sb.Append("<div class='datalinq-filter-field-wrapper'>");
            sb.Append("<div class='datalinq-label'>" + GetDefaultValueFromRecord(filterProperties, "displayname", filterParameter) + "</div>");

            if (filterProperties != null && filterProperties.ContainsKey("source"))
            {
                var source = GetDefaultValueFromRecord(filterProperties, "source").ToString();
                var dependsOn = source.KeyParameters();

                var rawString = ComboFor(null, filterParameter,
                    new
                    {
                        @class = "datalinq-filter-parameter",
                        onchange = "dataLinq.updateViewFilter(this)",
                        multiple = GetDefaultValueFromRecord(filterProperties, "multiple")
                    },
                    new
                    {
                        source = source,
                        valueField = GetDefaultValueFromRecord(filterProperties, "valueField")?.ToString(),
                        nameField = GetDefaultValueFromRecord(filterProperties, "nameField")?.ToString(),
                        prependEmpty = GetDefaultValueFromRecord(filterProperties, "prependEmpty"),
                        dependsOn = dependsOn
                    },
                    GetDefaultValueFromRecord(filterProperties, "defaultValue")
                );
                sb.Append(rawString.ToString());
            }
            else
            {
                DataType fieldType = DataType.Text;
                if (filterProperties != null && filterProperties.ContainsKey("dataType"))
                {
                    fieldType = (DataType)Enum.Parse(typeof(DataType), GetDefaultValueFromRecord(filterProperties, "dataType")?.ToString());
                    // Nur wenn Checkbox ausgewählt ist, soll der FieldType geändert werden; da input type nicht "Date", etc. sein soll.
                    if (fieldType != DataType.Checkbox)
                    {
                        fieldType = DataType.Text;
                    }
                }

                sb.Append("<input");
                AppendHtmlAttributes(sb, new
                {
                    type = fieldType.ToString().ToLower(),
                    name = filterParameter,
                    onkeyup = "dataLinq.updateViewFilter(this)",
                    onchange = "dataLinq.updateViewFilter(this)"
                }, "datalinq-filter-parameter datalinq-input");

                if (filterProperties != null)
                {
                    if (filterProperties.ContainsKey("dataType"))
                    {
                        sb.Append(" data-datatype='" + GetDefaultValueFromRecord(filterProperties, "dataType")?.ToString() + "'");
                    }
                    if (filterProperties.ContainsKey("dropProperty") && filterProperties.ContainsKey("dropQuery"))
                    {
                        sb.Append(" data-drop-property='" + GetDefaultValueFromRecord(filterProperties, "dropProperty")?.ToString() + "'");
                        sb.Append(" data-drop-query='" + GetDefaultValueFromRecord(filterProperties, "dropQuery")?.ToString() + "'");
                    }
                }

                sb.Append("/>");
            }
            sb.Append("</div>");
        }

        sb.Append("<br/>");

        sb.Append("<div ");
        AppendHtmlAttribute(sb, "class", "datalinq-filter-buttongroup");
        sb.Append(">");
        sb.Append("<button class='datalinq-button datalinq-filter-clear' onclick='dataLinq.clearFilter(this)'>Filter leeren</button>");
        sb.Append(RefreshViewClick("Übernehmen").ToString());
        sb.Append("</div>");

        sb.Append("</div>");

        sb.Append("</div>");

        return _razor.RawString(sb.ToString());
    }


    [HelpDescription("Die Methode erzeugt ein Steuerelement zum Exportieren der Daten des Views in eine CSV-Datei. Dabei wird die aktuell eingestellte Filterung bzw. Sortierung angewendet. Der Export bezieht sich auf die Daten jenes Views, in dem die Schaltfläche eingebunden ist.")]
    public object ExportView(
        [HelpDescription("Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes (z.B.: \"Export\")")]
        string label = "Export",
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<button ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-button apply");
        AppendHtmlAttribute(sb, "onclick", "dataLinq.export(this)");
        sb.Append(">" + label + "</button>");

        return _razor.RawString(sb.ToString());
    }


    [HelpDescription("Macht aus einem Text einen Link. Dieser aktualisiert einen Filter einer View, das kann auch ein Filter eines in der Seite eingebauten Include(Click)Views sein")]
    public object UpdateFilterButton(
        [HelpDescription("Name des Filters, der gesetzt werden soll")]
        string filterName,
        [HelpDescription("Neuer Wert des Filters")]
        object filterValue,
        [HelpDescription("Text, der auf der Schaltfläche stehen soll. Ansonsten kann diese über CSS mit einem Symbol versehen werden")]
        string buttonText = "",
        [HelpDescription("HTML (DOM) Id des zu verändernden Filters. Dieser kann in der Methode 'FilterView' im 'htmlAttributes'-Parameter gesetzt werden. Falls dieser Wert leergelassen wird, wird der erste Filter im aktuellen View verwendet.")]
        string filterId = "",
        [HelpDescription("Ein anonymes Objekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        //sb.Append("<button onclick=\"dataLinq.updateFilter(this, '" + filterId + "', '" + filterName + "', '" + filterValue + "')\" ");
        sb.Append("<button data-filter-id='" + filterId + "' data-filter-name='" + filterName + "' data-filter-value='" + filterValue + "' ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-update-filter");
        sb.Append(" >");
        sb.Append(String.IsNullOrEmpty(buttonText) ? "" : buttonText);
        sb.Append("</button>");
        return _razor.RawString(sb.ToString());
    }

    #endregion

    [HelpDescription("Erstellt aus den übergebenen Records eine HTML Tablelle")]
    public object Table(
        [HelpDescription("Die Records, für die die Tabelle erstellt werden soll.")]
        IEnumerable<IDictionary<string, object>> records,
        [HelpDescription("Die Spalten, die in der Tabelle übernommen werden sollten. Wird hier nichts angegeben, werden alle Spalten angegeben")]
        IEnumerable<string> columns = null,
        [HelpDescription("Anonymes Objekt für die HTML-Attribute für die Tabelle (table-Element). zB. zum styling.")]
        object htmlAttributes = null,
        [HelpDescription("Anonymes Objekt für die HTML-Attribute der Titlezeile der Tabelle")]
        object row0HtmlAttributes = null,
        [HelpDescription("Anonymes Objekt für die HTML-Attribute für ungerade Tablellenzeilen (1, 3, 5, ...)")]
        object row1HtmlAttributes = null,
        [HelpDescription("Anonymes Objekt für die HTML-Attribute für gerade Tablellenzeilen (2, 4, 6, ...)")]
        object row2HtmlAttributes = null,
        [HelpDescription("Anonymes Objekt für die HTML-Attribute die Tabellenzellen der Titlezeile")]
        object cell0HtmlAttributes = null,
        [HelpDescription("Anonymes Objekt für die HTML-Attribute die Tabellenzellen")]
        object cellHtmlAttributes = null,
        [HelpDescription("Die maximale Anzahl der Zeilen, die dargestellt werden. Wir ein Wert <= 0 angegeben, werden alle Zeilen dargstellt.")]
        int max = 0)
    {
        if (records == null || records.Count() == 0)
        {
            return _razor.RawString(String.Empty);
        }

        StringBuilder sb = new StringBuilder();

        if (columns == null)
        {
            #region All Columns

            var columnList = new List<string>();
            foreach (var record in records)
            {
                foreach (var key in record.Keys)
                {
                    if (!columnList.Contains(key))
                    {
                        columnList.Add(key);
                    }
                }
            }

            columns = columnList;

            #endregion
        }

        sb.Append("<table ");
        AppendHtmlAttributes(sb, htmlAttributes ?? new { style = "width:100%;text-align:left;background:#efefef" });
        sb.Append(" >");

        #region Haeder

        sb.Append("<tr ");
        AppendHtmlAttributes(sb, row0HtmlAttributes ?? new { style = "background-color:#eee" });
        sb.Append(" >");
        foreach (var colum in columns)
        {
            sb.Append("<th ");
            AppendHtmlAttributes(sb, cell0HtmlAttributes ?? new { style = "padding:4px" });
            sb.Append(" >");
            sb.Append(ToHtml(colum));
            sb.Append("</th>");
        }
        sb.Append("</tr>");

        #endregion

        #region Body

        int counter = 1;
        foreach (var record in records)
        {
            sb.Append("<tr ");
            if (counter % 2 == 1)
            {
                AppendHtmlAttributes(sb, row1HtmlAttributes ?? new { style = "background-color:#fff" });
            }
            else
            {
                AppendHtmlAttributes(sb, row2HtmlAttributes ?? new { style = "background-color:#ffd" });
            }
            sb.Append(" >");
            foreach (var colum in columns)
            {
                sb.Append("<td ");
                AppendHtmlAttributes(sb, cellHtmlAttributes ?? new { style = "padding:4px" });
                sb.Append(" >");
                sb.Append(record.ContainsKey(colum) ? ToHtml(record[colum]?.ToString()) : String.Empty);
                sb.Append("</td>");
            }
            sb.Append("</tr>");

            if (max > 0 && counter >= max)
            {
                break;
            }
            counter++;
        }

        #endregion

        sb.Append("</table>");

        return _razor.RawString(sb.ToString());
    }

    #region Formular

    [HelpDescription("Erstellt den Beginn eines HTML-Formulars zum Abschicken von Daten")]
    public object BeginForm(
        [HelpDescription("Gibt die Id der Query, an den die Daten des Formulars geschickt werden sollen, in folgender Form an: ((endpoint-id@query-id))")]
        string id,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für den Formular-Tag, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<form ");
        AppendHtmlAttributes(sb, htmlAttributes);
        return _razor.RawString(sb.ToString() + " action='" + "../ExecuteNonQuery/" + id + "' method='POST'>");
    }

    [HelpDescription("Erstellt das Ende eines HTML-Formulars zum Abschicken von Daten, bei Bedarf mit Schaltflächen")]
    public object EndForm(
        [HelpDescription("Falls eingeben, wird eine Schaltflächen zum Abschicken des Formulars mit dem angegebenen Text erstellt")]
        string submitText = "",
        [HelpDescription("Falls eingeben, wird eine Schaltflächen zum Zurücksetzen des Formulars mit dem angegebenen Text erstellt")]
        string cancelText = "")
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("<br/>");

        if (!String.IsNullOrEmpty(submitText))
        {
            sb.Append("<button type='button', class='datalinq-submit-form' onclick='dataLinq.submitForm(this)' >" + submitText + "</button>");
        }

        if (!String.IsNullOrEmpty(cancelText))
        {
            sb.Append("<button type='reset' class='datalinq-reset-form'>" + cancelText + "</button>");
        }

        return _razor.RawString(sb.ToString() + "</form>");
    }

    [HelpDescription("Erstellt eine Schaltfläche, die eine View in einem Dialogfeld auf der aktuellen Seite öffnet. Darin könnte sich etwa ein Formular zum Bearbeiten oder Erstellen von Datensatzes befinden")]
    public object OpenViewInDialog(
        [HelpDescription("Gibt die Id des Views in folgender Form an: endpoint-id@query-id@view-id")]
        string id,
        [HelpDescription("Ein anonymes Objekt mit den Parametern, welche die Query erwartet. Falls ein Datensatz berarbeitet werden soll, dessen Primärschlüsseln. Falls ein neuer Datensatz eingefügt werden soll (d.h. PK noch nicht vorhanden): Wert als leerer String übergeben, bspw. ((new { PK=\"''\"} ))")]
        object parameter,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für die Schaltfläche, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Text, der auf der Schaltfläche stehen soll. Ansonsten kann diese über CSS mit einem Symbol versehen werden")]
        string buttonText = "",
        [HelpDescription("Ein anonymes Ojekt mit Attributen, die für das Dialogfenster relevant sind oder einen Bestätigungstext enthalten, bspw. ((new { dialogWidth = \"'500px'\", dialogHeight = \"'500px'\", dialogTitle = \"'Gewählte Datensätze bearbeiten'\", confirmText = \"'sicher?'\" } ))")]
        object dialogAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        //sb.Append("<button onclick=\"dataLinq.openDialog(this, '" + id + "', " + (parameter != null ? JsonConvert.SerializeObject(parameter).Replace("\"", "") : "{}") + ");\" ");
        sb.Append("<button data-dialog-id='" + id + "' data-dialog-parameter='" + (parameter != null ? JsonConvert.SerializeObject(parameter) : "{}") + "' ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-open-dialog");
        sb.Append(" data-dialog-attributes='" + (dialogAttributes != null ? JsonConvert.SerializeObject(dialogAttributes) : "{}") + "' >");
        sb.Append(String.IsNullOrEmpty(buttonText) ? "" : buttonText);
        sb.Append("</button>");
        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt eine Schaltfläche, die eine Query mit den übergebenen Parametern ausführt. Damit kann bspw. ein Datensatz gelöscht oder eine Wert gesetzt werden.")]
    public object ExecuteNonQuery(
        [HelpDescription("Gibt die Id des Query in folgender Form an: endpoint-id@query-id")]
        string id,
        [HelpDescription("Ein anonymes Objekt mit den Parametern, welche die Query erwartet, bspw. ((new { PK=record[\"PK\"} ))")]
        object parameter,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für die Schaltfläche, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Text, der auf der Schaltfläche stehen soll. Ansonsten kann diese über CSS mit einem Symbol versehen werden")]
        string buttonText = "",
        [HelpDescription("Ein anonymes Ojekt mit Attributen, die für das Dialogfenster relevant sind oder einen Bestätigungstext enthalten, bspw. ((new { dialogWidth = \"'500px'\", dialogHeight = \"'500px'\", confirmText = \"'sicher?'\" } ))")]
        object dialogAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        //sb.Append("<button onclick=\"dataLinq.executeNonQuery(this, '" + id + "', " + (parameter != null ? JsonConvert.SerializeObject(parameter).Replace("\"", "") : "{}") + ");\" ");
        sb.Append("<button data-dialog-id='" + id + "' data-dialog-parameter='" + (parameter != null ? JsonConvert.SerializeObject(parameter) : "{}") + "' ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-execute-non-query");
        sb.Append(" data-dialog-attributes=\'" + (dialogAttributes != null ? JsonConvert.SerializeObject(dialogAttributes) : "{}") + "\' >");
        sb.Append(String.IsNullOrEmpty(buttonText) ? "" : buttonText);
        sb.Append("</button>");
        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt ein HTML-Auswahlliste (Select), deren Optionen aus einer Abfrage geladen werden")]
    public object IncludeCombo(
        [HelpDescription("Gibt die Id des Querys, der die Optionen bereithält, in folgender Form an: endpoint-id@query-id")]
        string id, string url,
        [HelpDescription("Name der Spalte des Abfrage(Query)-Ergebnisses, die die Values für die Select-Option enthält")]
        string valueField,
        [HelpDescription("Name der Spalte des Abfrage(Query)-Ergebnisses, die den Anzeigenamen für die Select-Option enthält")]
        string nameField,
        [HelpDescription("Wert (Value), der vorausgewählt sein soll")]
        object defaultValue = null,
        [HelpDescription("Gibt an, ob den Auswahloptionen eine leere Option vorangestellt werden soll (damit ist die Auswahl wieder aufhebbar)")]
        bool prependEmpty = false)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"<select id='{id}' name='{id}' class='datalinq-include-combo' ");
        sb.Append($" data-url='{url}'");
        sb.Append($" data-valuefield='{valueField}'");
        sb.Append($" data-namefield='{nameField}'");
        sb.Append($" data-defaultvalue='{(defaultValue == null ? "" : defaultValue.ToString())}'");
        sb.Append($" data-prepend-empty='{prependEmpty.ToString().ToLower()}'");
        sb.Append("></select>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt eine HTML-Auswahlliste (Select) für ein Formular, deren Optionen aus einer Abfrage ODER einer Liste geladen werden")]
    public object ComboFor(
        [HelpDescription("DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen")]
        object record,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" select2=\"never\" } ))... wird select2=never nicht angegeben, wird das Element automatisch in ein Select2 Element (Combo + Eingabeled für die Suche nach Elementen) umgewandelt, wenn mehr als 20 Einträge vorhanden sind.")]
        object htmlAttributes = null,
        [HelpDescription(@"Datenquelle der Auswahloptionen, 
                kann eine Abfrage-URL sein (siehe IncludeCombo, bspw. (( new { source=""endpoint-id@query-id@view-id"", valueField=""VALUE"", nameField=""NAME"", prependEmpty=true }) )), 
                oder ein Dictionary mit Werten für Value und Anzeigenname, bspw. (( new { source=new Dictionary&lt;object,string&gt;() { {0,""Nein"" },{ 1, ""Ja""} }} )),
                oder eine String-Array, bei dem Value und Anzeigename den selben Wert haben, bspw. (( new { source=new string[]{ ""Ja"",""Nein"",""Vielleicht""}} ))")]
        object source = null,
        [HelpDescription("Falls noch kein Wert im record-Datensatz vorliegt (null), kann so ein Wert für die Vorauswahl getroffen werden.")]
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        StringBuilder sb = new StringBuilder();
        sb.Append("<select name='" + name + "' ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-include-combo");

        if (source != null && source.GetType().GetProperty("source") != null)
        {
            var sourceProperty = source.GetType().GetProperty("source");
            var sourceValue = sourceProperty.GetValue(source);
            bool prependEmpty = Convert.ToBoolean(GetDefaultValueFromRecord(ToDictionary(source), "prependEmpty", false));
            sb.Append(" data-prepend-empty='" + prependEmpty.ToString().ToLower() + "' data-defaultvalue='" + (val == null ? "" : val.ToString()) + "'");

            var dependsOn = GetDefaultValueFromRecord(ToDictionary(source), "dependsOn", null) as string[];
            if (dependsOn != null && dependsOn.Length > 0)
            {
                sb.Append($" data-depends-on='{String.Join(",", dependsOn)}'");
            }

            if (sourceValue is string[])
            {
                sb.Append(">");
                foreach (var optionValue in (string[])sourceValue)
                {
                    sb.Append("<option value='" + optionValue + "' " + (optionValue == val?.ToString() ? "selected" : "") + ">" + optionValue + "</option>");
                }
            }
            else if (sourceValue is Dictionary<object, string>)
            {
                sb.Append(">");
                foreach (var kvp in (Dictionary<object, string>)sourceValue)
                {
                    sb.Append("<option value='" + kvp.Key + "' " + (kvp.Key.ToString() == val?.ToString() ? "selected" : "") + ">" + kvp.Value + "</option>");
                }
            }
            else if (sourceValue is string)
            {
                var valueFieldProperty = source.GetType().GetProperty("valueField");
                var nameFieldProperty = source.GetType().GetProperty("nameField");

                if (valueFieldProperty == null || nameFieldProperty == null)
                {
                    valueFieldProperty.SetValue(source, "VALUE");
                    nameFieldProperty.SetValue(source, "NAME");
                }

                if (valueFieldProperty.PropertyType == typeof(string) && nameFieldProperty.PropertyType == typeof(string))
                {
                    sb.Append(" data-url='" + sourceProperty.GetValue(source) + "' data-valuefield='" + valueFieldProperty.GetValue(source) + "' data-namefield='" + nameFieldProperty.GetValue(source) + "' >");
                }
                else
                {
                    throw new ArgumentException("valueField and nameField have to be typeof(string)");
                }
            }
        }

        sb.Append("</select>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt HTML-Radiobuttons für ein Formular, deren Optionen aus einer Abfrage ODER einer Liste geladen werden")]
    public object RadioFor(
        [HelpDescription("DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen")]
        object record,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das  Formular-Element, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription(@"Datenquelle der Auswahloptionen, 
                kann eine Abfrage-URL sein (siehe IncludeCombo, bspw. (( new { source=""endpoint-id@query-id@view-id"", valueField=""VALUE"", nameField=""NAME"", prependEmpty=true }) )), 
                oder ein Dictionary mit Werten für Value und Anzeigenname, bspw. (( new { source=new Dictionary&lt;object,string&gt;() { {0,""Nein"" },{ 1, ""Ja""} }} )),
                oder eine String-Array, bei dem Value und Anzeigename den selben Wert haben, bspw. (( new { source=new string[]{ ""Ja"",""Nein"",""Vielleicht""}} ))")]
        object source = null,
        [HelpDescription("Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.")]
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        StringBuilder sb = new StringBuilder();
        sb.Append("<div ");
        AppendHtmlAttributes(sb, htmlAttributes);
        sb.Append(">");

        if (source != null && source.GetType().GetProperty("source") != null)
        {
            var sourceProperty = source.GetType().GetProperty("source");

            if (sourceProperty.PropertyType == typeof(string[]))
            {
                foreach (var optionValue in (string[])sourceProperty.GetValue(source))
                {
                    sb.Append("<input type='radio' name='" + name + "' value='" + optionValue + "' " + (optionValue == val.ToString() ? "checked" : "") + " />" + optionValue + "<br/>");
                }
            }
            else if (sourceProperty.PropertyType == typeof(Dictionary<object, string>))
            {
                foreach (var kvp in (Dictionary<object, string>)sourceProperty.GetValue(source))
                {
                    sb.Append("<input type='radio' name='" + name + "' value='" + kvp.Key + "' " + (kvp.Key.ToString() == val.ToString() ? "checked" : "") + " />" + kvp.Value + "<br/>");
                }
            }
            else if (sourceProperty.PropertyType == typeof(string))
            {
                var valueFieldProperty = source.GetType().GetProperty("valueField");
                var nameFieldProperty = source.GetType().GetProperty("nameField");
                if (valueFieldProperty == null || nameFieldProperty == null)
                {
                    valueFieldProperty.SetValue(source, "VALUE");
                    nameFieldProperty.SetValue(source, "NAME");
                }

                if (valueFieldProperty.PropertyType == typeof(string) && nameFieldProperty.PropertyType == typeof(string))
                {
                    sb.Clear();
                    sb.Append("<div ");
                    AppendHtmlAttributes(sb, htmlAttributes, "datalinq-include-radio");
                    sb.Append(" data-name='" + name + "' data-url='" + sourceProperty.GetValue(source) + "' data-valuefield='" + valueFieldProperty.GetValue(source) + "' data-namefield='" + nameFieldProperty.GetValue(source) + "' data-defaultvalue='" + (val == null ? "" : val.ToString()) + "' >");
                }
                else
                {
                    throw new ArgumentException("valueField and nameField have to be typeof(string)");
                }
            }
        }

        sb.Append("</select>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt ein HTML-Textfeld für ein Formular")]
    public object TextFor(
        [HelpDescription("DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen")]
        object record,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das Formular-Element; auch reguläre Ausdrücke zum Überprüfen sind möglich., bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\", required=\"required\", pattern=\"[A-Za-z]{3}\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.")]
        object defaultValue = null,
        [HelpDescription("Art des Textfeldes als Enumeration, hier sind Text-(Text), Datum-(Date) oder Datum+Uhrzeit(DateTime) als Typ möglich, bspw. ((DataType.DateTime)).\n Bei Datumsfeldern muss das voreingestellte Datum folgendes Format haben: DD.MM.YYYY HH:mm, bspw. ((\"08.11.2017 09:32\")) ")]
        DataType dataType = DataType.Text)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        StringBuilder sb = new StringBuilder();
        if (dataType == DataType.DateTime || dataType == DataType.Date)
        {
            sb.Append("<input type='hidden' name='" + name + "' value='" + (val == null ? "NULL" : val.ToString()) + "' />");
            name += "_helper";
        }
        sb.Append("<input type='text' name='" + name + "' data-datatype='" + dataType.ToString() + "'");
        AppendHtmlAttributes(sb, htmlAttributes);
        sb.Append("value='" + (val == null ? "" : val.ToString()) + "'/>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt ein HTML-Checkbox für ein Formular")]
    public object CheckboxFor(
        [HelpDescription("DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen")]
        object record,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.")]
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        StringBuilder sb = new StringBuilder();

        // Checkbox-Value wird nur übergeben, wenn gecheckt. => Direkt davor ein hidden feld mit gleichem Namen und Guid
        string guid = Guid.NewGuid().ToString();
        sb.Append("<input type='hidden' name='" + name + "' value='" + (Convert.ToBoolean(val) == true ? "true" : "false") + "' id='" + guid + "' />");
        sb.Append("<input type='checkbox' data-guid='" + guid + "' ");
        AppendHtmlAttributes(sb, htmlAttributes);
        sb.Append("value='True' " + (Convert.ToBoolean(val) == true ? "checked" : ""));
        sb.Append(" onclick='$(\"#\" + $(this).data(\"guid\")).val(this.checked)' />");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt eine HTML-Textarea (Textfeld für mehrzeilige Eingaben) für ein Formular")]
    public object TextboxFor(
        [HelpDescription("DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen")]
        object record,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Falls noch kein Wert im record-Datensatz vorliegt (null), kann so ein Wert für die Vorauswahl getroffen werden.")]
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        StringBuilder sb = new StringBuilder();
        sb.Append("<textarea name='" + name + "' ");
        AppendHtmlAttributes(sb, htmlAttributes);
        sb.Append(">" + (val == null ? "" : val.ToString()) + "</textarea>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Erstellt ein verstecktes HTML-Feld für ein Formular")]
    public object HiddenFor(
        [HelpDescription("DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen")]
        object record,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name,
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.")]
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        StringBuilder sb = new StringBuilder();
        sb.Append("<input type='hidden' name='" + name + "' ");
        AppendHtmlAttributes(sb, htmlAttributes);
        sb.Append(" value='" + (val == null ? "" : val.ToString()) + "' />");
        return _razor.RawString(sb.ToString());
    }


    [HelpDescription("Erstellt ein Label for eine Formular Eingabe-Element")]
    public object LabelFor(
        [HelpDescription("Label/Text der Angezeigt werden sollte.")]
        string label,
        [HelpDescription("Name (Attribut) des Formular-Elements")]
        string name = "",
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Soll nach dem Label in eine neue Zeile gewechselt werden?")]
        bool newLine=true)
    {
        var sb = new StringBuilder();

        if (newLine)
        {
            sb.Append("<br/>");
        }

        sb.Append($"<label for='{name}'");
        AppendHtmlAttributes(sb, htmlAttributes);
        sb.Append($">{label}</label>");

        if (newLine)
        {
            sb.Append("<br/>");
        }

        return _razor.RawString(sb.ToString());
    }

    #endregion

    #region Statistics & Charts

    [HelpDescription("Erstellt ein HTML DIV-Element, dass die Anzahl der Datensätze anzeigt")]
    public object StatisticsCount(
        [HelpDescription("DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.")]
        IDictionary<string, object>[] records,
        [HelpDescription("Der Text, der über der Zahl anzeigt werden soll, bspw. \"Anzahl\"")]
        string label = "",
        [HelpDescription("Ein anonymes Ojekt mit HTML-Attributen für das DIV-Elment, bspw. ((new { style=\"height:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<div ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-statistics");
        sb.Append(">");
        if (!String.IsNullOrWhiteSpace(label))
        {
            sb.Append("<strong>" + ToHtml(label) + "</strong>");
        }

        sb.Append(records != null ? records.Length.ToString() : "0");
        sb.Append("</div>");

        return _razor.RawString(sb.ToString());
    }

    [HelpDescription(@"Gruppiert Datensätze auf Basis eines Feldes und gibt dazugehörige Gruppengröße (d.h. Anzahl der Datensätze in der Gruppe) in einer JavaScript Variable im JSON-Format aus.")]
    public object StatisticsGroupBy(
        [HelpDescription("DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.")]
        IDictionary<string, object>[] records,
        [HelpDescription(@"Name des auszugebenden Javascript-Objektes, mit folgendem Aufbau:
                (([
                    {name: ""Name / Kategorie / etc"", value: ""Wert""}, 
                    {name: ""Laubbäume"", value: ""2""},
                    {name: ""Nadelbäume"", value: ""5""},
                ]))")]
        string jsVariableName,
        [HelpDescription("Feldname im DataLinq-Objekt, nach dem gruppiert werden soll.")]
        string field,
        [HelpDescription(@"Art, nach der das Objekt sortiert werden soll. Es kann auf- und absteigend nach dem Gruppennamen (""OrderField.NameAsc"", ""OrderField.NameDesc"") oder nach Gruppengröße (""OrderField.ValueAsc"", ""OrderField.ValueDesc"") sortiert werden.")]
        OrderField orderField = OrderField.Non)
    {
        return ToParseJson(Stat_GroupBy(records, field, orderField), jsVariableName);
    }

    [HelpDescription(@"Gruppiert Datensätze auf Basis eines Feldes und erzeugt eine JavaScript Variable im JSON-Format für Diagramme.")]
    public object StatisticsGroupByDerived(
        [HelpDescription("DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.")]
        IDictionary<string, object>[] records,
        [HelpDescription(@"Name des auszugebenden Javascript-Objektes, mit folgendem Aufbau:
                (([
                    {name: ""Name / Kategorie / etc"", value: ""Wert""}, 
                    {name: ""Laubbäume"", value: ""2""},
                    {name: ""Nadelbäume"", value: ""5""},
                ]))")]
        string jsVariableName,
        [HelpDescription("Feldname im DataLinq-Objekt, das die Kategorie enthält.")]
        string categoryField,
        [HelpDescription("Feldname im DataLinq-Objekt, das die Werte enthält.")]
        string valueField,
        [HelpDescription(@"Art, nach der das Wert-Feld abgeleitet werden soll. (""StatType.Sum"", ""StatType.Min"", ""StatType.Max"", ""StatType.Mean"",  ""StatType.Count"")")]
        StatType statType = StatType.Sum,
        [HelpDescription(@"Art, nach der das Objekt sortiert werden soll. Es kann auf- und absteigend nach dem Gruppennamen (""OrderField.NameAsc"", ""OrderField.NameDesc"") oder nach Gruppengröße (""OrderField.ValueAsc"", ""OrderField.ValueDesc"") sortiert werden.")]
        OrderField orderField = OrderField.Non)
    {
        return ToParseJson(Stat_GroupByDerived(records, categoryField, valueField, statType, orderField), jsVariableName);
    }

    [HelpDescription(@"Gruppiert Datensätze nach Zeitintervallen gibt dazugehörige Gruppengröße (d.h. Anzahl der Datensätze in der Gruppe) in einer JavaScript Variable im JSON-Format aus. Zusätzlich kann nach einem weiteren Feld gruppiert werden.")]
    public object StatisticsGroupByTime(
        [HelpDescription("Datalinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.")]
        IDictionary<string, object>[] records,
        [HelpDescription(@"Name des auszugebenden Javascript-Objektes")]
        string jsVariableName,
        [HelpDescription("Feldname im DataLinq-Objekt, dass den Zeitstempel enthält (DateTime).")]
        string datetimeField,
        [HelpDescription("Feldname im DataLinq-Objekt, nach dem gruppiert werden soll.")]
        string categoryField = "",
        [HelpDescription(@"Zeitinterval in Sekunden, nach dem gruppiert werden soll. Wenn nichts angegeben wird, wird die Zeitspanne der Datensätze 
                (frühestes bis spätestes vorkommendes Datum) berechnet und daraus eine passende Gruppierung erstellt.)")]
        int secondsInterval = 0,
        [HelpDescription("Falls Lücken in den Datensätzen mit einem Wert gefüllt werden soll, kann dieser (Integer) hier angeben werden.")]
        object fillMissingDataValue = null,
        [HelpDescription(@"Art, nach der das Objekt sortiert werden soll. Es kann auf- und absteigend nach dem Gruppennamen (""OrderField.NameAsc"", ""OrderField.NameDesc"") sortiert werden.")]
        OrderField orderField = OrderField.Non)
    {
        object help = new { label = "", categories = "" };

        if (records.Count() > 0)
        {
            // Ältesten Datensatz holen und Offset auf diesen Tag 00:00 Uhr setzen
            //wenn Zeiteinheit = Sekunde: offset auf vorhergehende Minute setzen, Minute: offset auf vorhergehende Stunde setzen,, wenn Stunde => auf 00:00, wenn Woche => auf Monat, wenn Monat => auf Jahr
            DateTime offset;
            DateTime firstDate = records.Min(r => Convert.ToDateTime(r[datetimeField] is DBNull ? DateTime.Now : r[datetimeField]));
            DateTime lastDate = records.Max(r => Convert.ToDateTime(r[datetimeField] is DBNull ? null : r[datetimeField]));

            if (secondsInterval == 0)
            {
                int timespanSeconds = (int)(lastDate - firstDate).TotalSeconds;
                if (timespanSeconds < 3600 * 2)                   // unter 2 Stunden => auf Minuten (120)
                {
                    secondsInterval = 60;
                }
                else if (timespanSeconds < 3600 * 24 * 3)           // unter 3 Tage => auf Stunden (72)
                {
                    secondsInterval = 3600;
                }
                else if (timespanSeconds < 3600 * 24 * 31)          // unter 2 Monate => auf Tage (60)
                {
                    secondsInterval = 3600 * 24;
                }
                else if (timespanSeconds < 3600 * 24 * 365)         // unter 1 Jahr => auf Wochen (52)
                {
                    secondsInterval = 3600 * 24 * 7;
                }
                else if (timespanSeconds < 3600 * 24 * 365 * 3)       // unter 3 Jahre => auf Monate (36)
                {
                    secondsInterval = 3600 * 24 * 7;
                }
                else                                            // über  3 Jahre => auf Jahre
                {
                    secondsInterval = 3600 * 24 * 7 * 365;
                }
            }
            if (secondsInterval <= 60)
            {
                offset = new DateTime(firstDate.Year, firstDate.Month, firstDate.Day, firstDate.Hour, firstDate.Minute, 0);
            }
            else if (secondsInterval <= 3600)
            {
                offset = new DateTime(firstDate.Year, firstDate.Month, firstDate.Day, firstDate.Hour, 0, 0);
            }
            else if (secondsInterval <= 86400)
            {
                offset = new DateTime(firstDate.Year, firstDate.Month, firstDate.Day, 0, 0, 0); // = firstDate.Date
            }
            else if (secondsInterval <= 604800)
            {
                int diff = firstDate.Date.DayOfWeek - DayOfWeek.Monday;
                offset = new DateTime(firstDate.Year, firstDate.Month, firstDate.AddDays(-diff).Day, 0, 0, 0);
            }
            else if (secondsInterval <= 2592000)
            {
                offset = new DateTime(firstDate.Year, firstDate.Month, 1, 0, 0, 0);
            }
            else
            {
                offset = new DateTime(firstDate.Year, 1, 1, 0, 0, 0);
            }

            foreach (var r in records)
            {
                DateTime date = Convert.ToDateTime(r[datetimeField] is DBNull ? DateTime.Now : r[datetimeField]);
                int intervals = (int)(date - offset).TotalSeconds / secondsInterval;
                r["helpTime"] = offset.AddSeconds(intervals * secondsInterval);
            }

            var data = records
                .GroupBy(r => new
                {
                    Category = String.IsNullOrWhiteSpace(categoryField) ? "" : r[categoryField],
                    Time = r["helpTime"]
                })
                .Select(s => new
                {
                    Category = s.Key.Category,
                    Time = s.Key.Time,
                    Count = s.Count()
                })
                .OrderBy(o => o.Time);

            var categories = new List<StatTimeCategory>();
            foreach (var d in data)
            {
                var statCategory = categories.Where(s => s.Category == d.Category?.ToString()).FirstOrDefault();
                if (statCategory == null)
                {
                    statCategory = new StatTimeCategory()
                    {
                        Category = d.Category?.ToString()
                    };
                    categories.Add(statCategory);
                }

                statCategory.Data.Add((DateTime)d.Time, d.Count);
            }

            // Alle möglichen Zeitschritte für Label erstellen
            // TODO: Monatweise richtig (ungleiche Tage/Monat,...)
            List<DateTime> labelTime = new List<DateTime>() { offset };
            DateTime helpDate = offset;
            while (helpDate <= lastDate)
            {
                helpDate = helpDate.AddSeconds(secondsInterval);
                labelTime.Add(helpDate);
            }

            if (fillMissingDataValue != null)
            {
                var val = Convert.ToDouble(fillMissingDataValue);
                foreach (var c in categories)
                {
                    foreach (var l in labelTime)
                    {
                        if (!c.Data.ContainsKey(l))
                        {
                            c.Data[l] = val;
                        }
                    }
                    c.Data = c.Data.OrderBy(d => d.Key).ToDictionary(h => h.Key, h => h.Value);
                }
            }

            switch (orderField)
            {
                case OrderField.NameAsc:
                    categories = categories.OrderBy(c => c.Category).ToList();
                    break;
                case OrderField.NameDesc:
                    categories = categories.OrderByDescending(c => c.Category).ToList();
                    break;
            }

            help = new { label = labelTime, categories = categories, type = "time" };
            //help = new
            //{
            //    label = labelTime,
            //    categories = categories,
            //    scales = new
            //    {
            //        x = new
            //        {
            //            type = "time",
            //            time = new
            //            {
            //                minUnit = "month"
            //            }
            //        }
            //    }
            //};
        }
        return ToParseJson(help, jsVariableName);
    }

    [HelpDescription(@"Gibt eine Zeitreihe in einer JavaScript Variable im JSON-Format aus. Zusätzlich kann nach einem weiteren Feld gruppiert werden.")]
    public object StatisticsTime(
        [HelpDescription("DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.")]
        IDictionary<string, object>[] records,
        [HelpDescription(@"Name des auszugebenden Javascript-Objektes")]
        string jsVariableName,
        [HelpDescription("Feldname im DataLinq-Objekt, dass den Zeitstempel enthält (DateTime).")]
        string datetimeField,
        [HelpDescription("Feldname im DataLinq-Objekt, dass den Wert enthält (Zahl).")]
        string valueField,
        [HelpDescription("Feldname im DataLinq-Objekt, nach dem gruppiert werden soll.")]
        string categoryField = "")
    {
        object help = new { label = "", categories = "" };

        if (records.Count() > 0)
        {
            foreach (var r in records)
            {
                r["helpTime"] = Convert.ToDateTime(r[datetimeField] is DBNull ? DateTime.Now : r[datetimeField]);
            }

            var data = records
                .GroupBy(r => new
                {
                    Category = String.IsNullOrWhiteSpace(categoryField) ? "" : r[categoryField],
                    Time = r["helpTime"],
                    Value = r[valueField]
                })
                .OrderBy(o => o.Key.Time);

            var categories = new List<StatTimeCategory>();
            foreach (var d in data)
            {
                var statCategory = categories.Where(s => s.Category == d.Key.Category?.ToString()).FirstOrDefault();
                if (statCategory == null)
                {
                    statCategory = new StatTimeCategory()
                    {
                        Category = d.Key.Category?.ToString()
                    };
                    categories.Add(statCategory);
                }

                statCategory.Data.Add((DateTime)d.Key.Time, d.Key.Value);
            }

            help = new { label = records.Select(r => (DateTime)r["helpTime"]).ToList(), categories = categories, type = "time" };

        }
        return ToParseJson(help, jsVariableName);
    }

    #region Statistic Helper Functions

    private object ToParseJson(object obj, string name)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("<script>");
        sb.Append("var " + name + "=");
        sb.Append("jQuery.parseJSON('");
        sb.Append(JsonConvert.SerializeObject(obj).Replace("\\", "\\\\"));
        sb.Append("');");
        //sb.Append("console.log(" + name + ");");
        sb.Append("</script>");

        return _razor.RawString(sb.ToString());
    }

    private object Stat_GroupBy(IDictionary<string, object>[] records, string field, OrderField orderField = OrderField.Non)
    {
        Dictionary<string, int> groupBy = new Dictionary<string, int>();

        foreach (object val in records.Select(r => r[field]).Distinct())
        {
            if (val == null)
            {
                groupBy.Add(String.Empty, records.Where(r => null == r[field]).Count());
            }
            else
            {
                groupBy.Add(val.ToString(), records.Where(r => val.Equals(r[field])).Count());
            }
        }

        var group = groupBy.Keys.Select(k => new
        {
            name = k,
            value = groupBy[k]
        });

        switch (orderField)
        {
            case OrderField.NameAsc:
                return group.OrderBy(g => g.name).ToArray();
            case OrderField.NameDesc:
                return group.OrderByDescending(g => g.name).ToArray();
            case OrderField.ValueAsc:
                return group.OrderBy(g => g.value).ToArray();
            case OrderField.ValueDesc:
                return group.OrderByDescending(g => g.value).ToArray();
        }
        return group.ToArray();
    }

    private object Stat_GroupByDerived(IDictionary<string, object>[] records, string categoryField, string valueField, StatType statType = StatType.Sum, OrderField orderField = OrderField.Non)
    {
        IEnumerable<NameValue<double>> group;
        switch (statType)
        {
            case StatType.Sum:
                group = records.GroupBy(r => r[categoryField]).Select(s => new NameValue<double>() { name = s.Key.ToString(), value = s.Sum(t => Convert.ToDouble(t[valueField])) });
                break;
            case StatType.Min:
                group = records.GroupBy(r => r[categoryField]).Select(s => new NameValue<double>() { name = s.Key.ToString(), value = s.Min(t => Convert.ToDouble(t[valueField])) });
                break;
            case StatType.Max:
                group = records.GroupBy(r => r[categoryField]).Select(s => new NameValue<double>() { name = s.Key.ToString(), value = s.Max(t => Convert.ToDouble(t[valueField])) });
                break;
            case StatType.Mean:
                group = records.GroupBy(r => r[categoryField]).Select(s => new NameValue<double>() { name = s.Key.ToString(), value = s.Average(t => Convert.ToDouble(t[valueField])) });
                break;
            case StatType.Count:
                group = records.GroupBy(r => r[categoryField]).Select(s => new NameValue<double>() { name = s.Key.ToString(), value = s.Count() });
                break;
            default:
                throw new ArgumentException("Unknown StatType");
        }

        switch (orderField)
        {
            case OrderField.NameAsc:
                return group.OrderBy(g => g.name).ToArray();
            case OrderField.NameDesc:
                return group.OrderByDescending(g => g.name).ToArray();
            case OrderField.ValueAsc:
                return group.OrderBy(g => g.value).ToArray();
            case OrderField.ValueDesc:
                return group.OrderByDescending(g => g.value).ToArray();
        }
        return group.ToArray();
    }


    #region Stat Classes

    public class StatTimeCategory
    {
        public StatTimeCategory()
        {
            this.Data = new Dictionary<DateTime, object>();
        }
        [JsonProperty(PropertyName = "category")]
        public string Category { get; set; }

        [JsonIgnore]
        public Dictionary<DateTime, object> Data { get; set; }

        [JsonProperty(PropertyName = "data")]
        public object[] DataArray
        {
            get
            {
                return Data.Select(d => new
                {
                    name = d.Key,
                    value = d.Value
                }).ToArray();
            }
        }
    }

    #endregion

    #endregion

    [HelpDescription(@"Erstellt ein Diagramm mit unterschiedlichen Typen, das auf einem JSON-Objekt im Javascript-Code basiert")]
    public object Chart(
        [HelpDescription(@"Diagrammtyp als Enumeration, hier sind Balken-(Bar), Torten-(Pie), Doughnut oder Linien(Line) als Typ möglich, bspw. ((ChartType.Bar))")]
        ChartType chartType,
        [HelpDescription(@"Name der Javascript-Variable, die das JSON-Datenobjekt enthält. Das JSON-Objekt könnte bspw. von der Methode ""StatisticsGroupBy"" oder ""StatisticsGroupByTime"" stammen und muss folgende Struktur haben:
                (([
                    {name: ""Name/Kategorie/etc"", value: ""Wert""}, 
                    {name: ""Laubbäume"", value: ""2""},
                    {name: ""Nadelbäume"", value: ""5""},
                ]))")]
        string jsValueVariable,
        [HelpDescription(@"Beschriftung des Diagramms")]
        string label = "",
        [HelpDescription(@"Ein anonymes Objekt mit HTML-Attributen für die Schaltfläche, bspw. ((new { style=""width:300px"" @class=""meine-klasse"" } )).
                Das Chartobjekt liegt als data-Attribute ('datalinq-chartobject') auf dem erzeugten DIV, über das mit einem Selektor (id, etc.) zugegriffen werden kann. 
                Am Chartobjekt können Änderung, bspw. Achsen-MinMax-Werten geändert und mit der 'update()'-Methode des Chartobjekts geändert werden.
                Siehe dazu: https://www.chartjs.org/docs/latest/developers/updates.html oder bspw:
                    ((var timeChart = $(""#timechart"").data(""datalinq-chartobject"");
                    timeChart.options.scales.yAxes[0].ticks.min = 0;
                    timeChart.update();))")]
        object htmlAttributes = null,
        [HelpDescription(@"Farben, die im Diagramm verwendet werden sollen als R,G,B-String, bspw. ((new string[] {""0,155,20"", ""160,0,25""} )).
                Bis maximal 7 Kategorien kann die Farbe für jede einzelne Kategorie angegeben werden. Bei 8 oder mehr Kategorien wird die erste Farbe verwendet.
                Wird nur ein Wert angegeben, haben alle Balken, etc. Diagramm diese Farbe.
                Wird nichts angegeben, so wird beim Tortendiagramm immer Zufallsfarbe verwendet, beim Balkendiagramm werden für die ersten 7 Kategorien Zufallsfarben ausgewählt, bei mehr Kategorien eine Standardfarbe herangezogen")]
        string[] chartColorRGB = null,
        [HelpDescription(@"Name der Javascript-Variable, die als JSON-Objekt Einstellungen zur Darstellung der Datensätze enthält. 
                Je nach Diagrammtyp sind unterschiedliche Einstellungen möglich, siehe dazu http://www.chartjs.org/docs/latest/charts/ => Charttypen => Dataset Properties, bspw.:
                (([
                    {
                        border: 2, 
                        lineTension: 0,
                        steppedLine: true,
                        ....
                    }
                ]))")]
        string jsDatasetVariable = ""
        //,object chartOptions = null
        )
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<div ");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-chart");
        sb.Append(" data-chart-label='" + label + "'");
        sb.Append(" data-chart-data='" + jsValueVariable + "'");
        sb.Append(" data-chart-type='" + chartType.ToString() + "'");
        sb.Append(" data-chart-dataset='" + jsDatasetVariable + "'");
        chartColorRGB = (chartColorRGB == null ? new string[] { } : chartColorRGB);        // "0,155,20"
        sb.Append(" data-chart-color='" + String.Join("|", chartColorRGB).Replace(" ", "") + "'");
        //if (chartOptions != null)
        //{
        //    sb.Append(" data-chart-options='" + JsonConvert.SerializeObject(chartOptions) + "'");
        //}
        sb.Append("></div>");

        return _razor.RawString(sb.ToString());
    }
    #endregion

    [HelpDescription("Bei Hover über einen Datensatz wird ein Symbol zum Kopieren angezeigt")]
    public object CopyButton(
        [HelpDescription("Wert, der kopiert werden soll")]
        object copyValue,
        [HelpDescription("Text, zu sehen sein soll, kann auch Leerstring sein und damit nichts beinhalten. Wenn kein Wert übergeben wird, wird der Wert des CopyValues herangezogen")]
        object copyBaseText = null,
        [HelpDescription("Ein anonymes Objekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null)
    {
        if (String.IsNullOrEmpty(copyValue?.ToString()))
        {
            return _razor.RawString(String.Empty);
        }

        string baseText = String.Empty;
        if (copyBaseText == null)
        {
            baseText = copyValue.ToString();
        }
        else
        {
            baseText = copyBaseText.ToString();
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("<div class='datalinq-copy-helper'>" + baseText);
        sb.Append("<div data-copy-value='" + copyValue + "'");
        AppendHtmlAttributes(sb, htmlAttributes, "datalinq-copy-button");
        sb.Append(" ></div>");
        sb.Append("</div>");
        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Liefert einen Skalarwert einer Abfrage zurück und gibt das Ergebnis als Inhalt eines HTML-Elementes aus.")]
    public object ExecuteScalar(
        [HelpDescription("Ein anonymes Objekt mit HTML-Attributen für diesen Button ((z.B.: new { style=\"width:300px\" @class=\"meine-klasse\" } ))")]
        object htmlAttributes = null,
        [HelpDescription("Datenquelle der Abfrage, eine Abfrage-URL die einen Wert liefert bspw. (( new { source=\"endpoint-id@query-id?id=2\", nameField=\"NAME\"}) ))")]
        object source = null,
         [HelpDescription(@"Art des HTML-Elements, dass erzeugt wird.")]
        string htmlTag = "span",
        [HelpDescription("Text, der angezeigt werden soll, falls die Abfrage kein Ergebnis liefert.")]
        string defaultValue = null)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<" + htmlTag);

        if (source != null && source.GetType().GetProperty("source") != null)
        {
            var sourceProperty = source.GetType().GetProperty("source");
            var sourceValue = sourceProperty.GetValue(source);

            if (sourceValue is string)
            {
                var nameFieldProperty = source.GetType().GetProperty("nameField");
                if (nameFieldProperty == null)
                {
                    nameFieldProperty.SetValue(source, "NAME");
                }

                if (nameFieldProperty.PropertyType == typeof(string))
                {
                    if (htmlAttributes != null)
                    {
                        foreach (var htmlAttribute in htmlAttributes.GetType().GetProperties())
                        {
                            if (htmlAttribute.Name.ToLower() == "class")
                            {
                                sb.Append(htmlAttribute.Name.ToLower() + "='" + (htmlAttribute.GetValue(htmlAttributes) != null ? htmlAttribute.GetValue(htmlAttributes).ToString() : "") + " datalinq-include-scalar' ");
                            }
                            else
                            {
                                if (htmlAttribute.GetValue(htmlAttributes) != null)
                                {
                                    sb.Append(htmlAttribute.Name + "='" + htmlAttribute.GetValue(htmlAttributes).ToString() + "' ");
                                }
                            }
                        }
                    }

                    if (!sb.ToString().Contains("class="))
                    {
                        sb.Append(" class='datalinq-include-scalar'");
                    }

                    bool prependEmpty = Convert.ToBoolean(GetDefaultValueFromRecord(ToDictionary(source), "prependEmpty", false));
                    sb.Append(" data-url='" + sourceProperty.GetValue(source) + "' data-namefield='" + nameFieldProperty.GetValue(source) + "' data-defaultvalue='" + defaultValue.ToString() + "'>");
                }
                else
                {
                    throw new ArgumentException("valueField and nameField have to be typeof(string)");
                }
            }
            sb.Append("</" + htmlTag + ">");
        }
        return _razor.RawString(sb.ToString());
    }

    [HelpDescription("Kodiert die Werte eines Objektes, sodass sie URL-tauglich sind")]
    public object UrlEncode(object parameter)
    {
        if (parameter == null)
        {
            return _razor.RawString(String.Empty);
        }

        return _razor.RawString(HttpUtility.UrlEncode(parameter.ToString()));
    }

    [HelpDescription("Erstellt eine Schaltfläche, mit der alle DOM-Elemente mit der Klasse \"responsive\" sichtbar geschaltet werden.")]
    public object ResponsiveSwitcher()
    {
        return _razor.RawString("<button class='responsive-switch'>Alles anzeigen</button>");
    }

    [HelpDescription("Der Username des atuell angemeldeten Benutzers")]
    public string GetCurrentUsername()
        => _ui?.Username ?? "";

    [HelpDescription("Prüft, ob der aktuelle User Mitglied in der angegeben Rolle ist")]
    public bool HasRole(
            [HelpDescription("Rollenname, der geprüft werdens soll")]
            string roleName
        ) => _ui?
             .Userroles?
             .Any(r => r.Equals(roleName, StringComparison.InvariantCultureIgnoreCase)) == true;

    //[HelpDescription("Gibt alle HTTP Request Header Namen zurück")]
    //public IEnumerable<string> GetRequestHeaders()
    //    => _httpContext?.Request?.Headers?.Keys ?? Enumerable.Empty<string>();

    [HelpDescription("Gibt den Wert eines HTTP Request Header zurück")]
    public string GetRequestHeaderValue(string header)
        => _httpContext?.Request?.Headers[header] ?? "";

    private string ParseUrl(string url, bool encodeQueryString)
    {
        if (encodeQueryString && url.Contains("?"))
        {
            var queryString = HttpUtility.ParseQueryString(url.Substring(url.IndexOf("?"))); ;
            url = url.Split('?')[0];

            foreach (string key in queryString.Keys)
            {
                url += url.Contains("?") ? "&" : "?";
                url += key + "=" + HttpUtility.UrlEncode(queryString[key]);
            }
        }

        return url;
    }

    #region IDataLinqHelper

    public void AppendHtmlAttributes(StringBuilder sb, object htmlAttributes, string addClass = "")
    {
        bool classAdded = false;
        if (htmlAttributes != null)
        {
            foreach (var htmlAttribute in htmlAttributes.GetType().GetProperties())
            {
                string val = htmlAttribute.GetValue(htmlAttributes)?.ToString();
                if (htmlAttribute.Name == "class" && !String.IsNullOrWhiteSpace(addClass))
                {
                    val = addClass + " " + val;
                    classAdded = true;
                }
                AppendHtmlAttribute(sb, htmlAttribute.Name, val);
            }
        }
        if (!classAdded && !String.IsNullOrWhiteSpace(addClass))
        {
            AppendHtmlAttribute(sb, "class", addClass);
        }
    }

    public void AppendHtmlAttribute(StringBuilder sb, string attributeName, string attributeValue)
    {
        if (attributeValue != null)
        {
            sb.Append(" " + attributeName + "='" + attributeValue.Replace("\n", "<br/>").Replace("\r", "").Replace("'", "\"") + "'");
        }
    }

    public object ToRawString(string str)
    {
        return _razor.RawString(str);
    }

    public object ToHtmlEncoded(string str)
    {
        return _razor switch
        {
            RazorEngineService => str,
            _ => HttpUtility.HtmlEncode(str)
        };
    }

    #endregion

    #region Helper

    private string ToHtml(string str)
    {
        if (str == null)
        {
            return String.Empty;
        }

        str = str.Replace("\n", "<br/>").Replace("\r", "");
        return str;
    }

    private object GetDefaultValueFromRecord(object record, string name, object defaultValue = null)
    {
        object val = defaultValue;
        if (record != null)
        {
            if (!(record is IDictionary<string, object>))
            {
                throw new ArgumentException("record is not an ExpandoObject");
            }

            var recordDictionary = (IDictionary<string, object>)record;
            // Wenn bei Queries Domains erstellt wurden (bspw. 0 durch "Nein" ersetzt; bspw. bei Select) => defaultValue sollte wieder Originalwert sein
            // bei DOMAINS wird ein Dictionary-Eintrag mit Namen + "_ORIGINAL" mit dem Originalwert gesetzt
            if (recordDictionary.ContainsKey(name + "_ORIGINAL") && !DBNull.Value.Equals(recordDictionary[name + "_ORIGINAL"]) && recordDictionary[name + "_ORIGINAL"] != null && !String.IsNullOrEmpty(recordDictionary[name + "_ORIGINAL"].ToString()))
            {
                val = recordDictionary[name + "_ORIGINAL"];
            }
            else if (recordDictionary.ContainsKey(name) && !DBNull.Value.Equals(recordDictionary[name]) && recordDictionary[name] != null && !String.IsNullOrEmpty(recordDictionary[name].ToString()))
            {
                val = recordDictionary[name];
            }
        }
        return val;
    }

    private IDictionary<string, object> ToDictionary(object anonymousObject)
    {
        if (anonymousObject == null)
        {
            return null;
        }

        if (anonymousObject is IDictionary<string, object>)
        {
            return (IDictionary<string, object>)anonymousObject;
        }

        Dictionary<string, object> dict = new Dictionary<string, object>();
        foreach (PropertyInfo pi in anonymousObject.GetType().GetProperties())
        {
            if (dict.ContainsKey(pi.Name))
            {
                continue;
            }

            dict.Add(pi.Name, pi.GetValue(anonymousObject));
        }
        return dict;
    }

    #endregion
}