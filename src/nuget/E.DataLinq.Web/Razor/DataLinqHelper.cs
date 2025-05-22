#pragma warning restore 1591

using E.DataLinq.Core;
using E.DataLinq.Core.Models.AccessTree;
using E.DataLinq.Core.Reflection;
using E.DataLinq.Web.Extensions;
using E.DataLinq.Web.Html;
using E.DataLinq.Web.Html.Abstractions;
using E.DataLinq.Web.Html.Extensions;
using E.DataLinq.Web.Models;
using E.DataLinq.Web.Models.Razor;
using E.DataLinq.Web.Razor.Extensions;
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


/// <summary>
/// de: Die Klasse ist eine Hilfsklasse, die innerhalb der Razor-Umgebung von DataLinq genutzt werden kann. Der Zugriff erfolgt über den globalen Namen DataLinqHelper bzw. der Kurzform DLH. 
/// Die Methoden dieser Klasse ermöglichen dynamische Inhalte innerhalb einer DataLinq-Seite, wie das Nachladen von Views, Editieren, Karten, Sortierung und vieles mehr.
/// en: This class is a helper class that can be used within the Razor environment of DataLinq. It is accessed through the global name DataLinqHelper or the shorthand DLH. 
/// The methods in this class allow dynamic content within a DataLinq page, such as loading views, editing, maps, sorting, and more.
/// </summary>
public class DataLinqHelper : IDataLinqHelper
{
    private readonly HttpContext _httpContext;
    private readonly DataLinqService _currentDatalinqService;
    private readonly IDataLinqUser _ui;
    private readonly IRazorCompileEngineService _razor;

    public DataLinqHelper(
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

    /// <summary>
    /// de: Die Methode holt Daten aus einer Datalinq-Query ab, verarbeitet die Daten und übergibt das Ergebnis an eine angegebene JavaScript-Funktion.
    /// en: The method retrieves data from a Datalinq query, processes the data, and passes the result to a specified JavaScript function.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die ID der Query an, die in der Form endpoint-id@query-id übergeben wird, um die richtigen Daten abzurufen.
    /// en: Specifies the query ID in the format endpoint-id@query-id to fetch the correct data.
    /// </param>
    /// <param name="jsCallbackFuncName">
    /// de: Der Name einer JavaScript-Funktion, die die abgerufenen Daten entgegennehmen wird, z.B. window.my_dataprocessor_function = function(data) {...}.
    /// en: The name of the JavaScript function that will receive the retrieved data, e.g., window.my_dataprocessor_function = function(data) {...}.
    /// </param>
    /// <param name="filter">
    /// de: Ein optionaler Filter, der auf die abgerufenen Daten angewendet wird, um nur relevante Ergebnisse zurückzugeben.
    /// en: An optional filter applied to the retrieved data to return only relevant results.
    /// </param>
    /// <param name="encodeUrl">
    /// de: Gibt an, ob die URL kodiert werden soll. Standardmäßig ist dieser Wert auf true gesetzt, um sicherzustellen, dass die URL korrekt übertragen wird.
    /// en: Indicates whether the URL should be encoded. By default, this is set to true to ensure the URL is properly transmitted.
    /// </param>
    /// <returns>
    /// de: Gibt das erzeugte HTML-Element als Raw-String zurück, das in der Webanwendung verwendet werden kann.
    /// en: Returns the generated HTML element as a raw string, which can be used in the web application.
    /// </returns>
    public object JsFetchData(
            string id,
            string jsCallbackFuncName,
            string filter = "",
            bool encodeUrl = true
        )
    {
        var htmlBuilder = HtmlBuilder.Create()
                .AppendDiv(div =>
                {
                    div.AddAttribute("class", "datalinq-fetch")
                       .AddAttribute("data-source", ParseUrl(id, encodeUrl))
                       .AddAttribute("data-js-callback", ParseUrl(jsCallbackFuncName, encodeUrl));

                    if (!String.IsNullOrEmpty(filter))
                    {
                        div.AddAttribute("data-filter", ParseUrl(filter, encodeUrl));
                    }
                });

        return _razor.RawString(htmlBuilder.BuildHtmlString());
    }

    /// <summary>
    /// de: Die Methode übergibt eine Sammlung von Datensätzen (Records) an eine JavaScript-Funktion, die diese dann verarbeitet.
    /// en: The method passes a collection of records to a JavaScript function for processing.
    /// </summary>
    /// <param name="records">
    /// de: Eine Sammlung von Records (Dictionary), die an die JavaScript-Funktion übergeben werden. Jeder Record ist ein Key-Value-Paar.
    /// en: A collection of records (Dictionaries) to be passed to the JavaScript function. Each record is a key-value pair.
    /// </param>
    /// <param name="jsCallbackFuncName">
    /// de: Der Name der JavaScript-Funktion, die die Records entgegennimmt und verarbeitet: window.my_dataprocessor_function = function(data) {...}.
    /// en: The name of the JavaScript function that will receive and process the records: window.my_dataprocessor_function = function(data) {...}.
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block mit eingebettetem JavaScript zurück, der die Records an die JavaScript-Funktion übergibt.
    /// en: Returns the generated HTML block with embedded JavaScript that passes the records to the JavaScript function.
    /// </returns>
    public object RecordsToJs(
            IDictionary<string, object>[] records,
            string jsCallbackFuncName
         )
    {
        #region Build Javascript Block

        StringBuilder js = new StringBuilder();

        js.Append("$(function(){");  // load, when page is rendered
        js.Append($"window.{jsCallbackFuncName}(");

        if (records != null)
        {
            bool firstObject = true;
            js.Append("[");
            foreach (var record in records)
            {
                if (firstObject) { firstObject = false; } else { js.Append(","); }
                js.Append("{");

                bool firstProperty = true;
                foreach (var kvp in record)
                {
                    if (firstProperty) { firstProperty = false; } else { js.Append(","); }
                    js.Append($"{kvp.Key}:{System.Text.Json.JsonSerializer.Serialize(kvp.Value)}");
                }
                js.Append("}");
            }
            js.Append("]");
        }

        js.Append(");");  // window.{jsCallbackFuncName}
        js.Append("});");  // (function() {

        #endregion

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendJavaScriptBlock(js.ToString())
                .BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Die Methode lädt Records aus einer angegebenen Query (endpoint@query) und gibt diese als Sammlung von Dictionaries zurück. Die Abfrage erfolgt serverseitig während des Renderns der Seite.
    /// en: The method loads records from a specified query (endpoint@query) and returns them as a collection of dictionaries. The query is executed server-side during page rendering.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die ID der Query an, die im Format endpoint-id@query-id vorliegt. Diese wird zur Abfrage der entsprechenden Daten verwendet.
    /// en: Specifies the query ID in the format endpoint-id@query-id. This is used to query the corresponding data.
    /// </param>
    /// <param name="filter">
    /// de: Ein optionaler Filter im Format von URL-Parametern, der auf die abgerufenen Daten angewendet wird: ((NAME=Franz&amp;STRASSE=Eberweg))
    /// en: An optional filter in the format of URL parameters applied to the retrieved data: ((NAME=Franz&amp;STRASSE=Eberweg))
    /// </param>
    /// <param name="orderby">
    /// de: Ein optionaler Parameter, der angibt, nach welchen Feldern die Daten sortiert werden sollen. Vor einem Feldnamen kann ein Minuszeichen (-) gesetzt werden, um absteigend zu sortieren.
    /// en: An optional parameter specifying the fields to sort the data by. A minus sign (-) before a field name indicates descending order.
    /// </param>
    /// <returns>
    /// de: Gibt eine Sammlung von Records als Dictionary zurück, die aus der serverseitigen Datalinq-Abfrage geladen wurden.
    /// en: Returns a collection of records as dictionaries, loaded from the server-side Datalinq query.
    /// </returns>
    public async Task<IDictionary<string, object>[]> GetRecordsAsync(
        string id,
        string filter = "",
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

    /// <summary>
    /// de: Die Methode bindet einen View ein und zeigt diesen sofort nach dem Aufbau des übergeordneten Views an.
    /// en: The method embeds a view and displays it immediately after the parent view is built.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die ID des Views an, die im Format endpoint-id@query-id@view-id vorliegt, um den richtigen View zu laden.
    /// en: Specifies the ID of the view in the format endpoint-id@query-id@view-id to load the correct view.
    /// </param>
    /// <param name="encodeQueryString">
    /// de: Gibt an, ob die Query-String-Parameter URL-kodiert werden sollen. Standardmäßig ist dieser Wert auf true gesetzt.
    /// en: Indicates whether the query string parameters should be URL encoded. This is true by default.
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block mit eingebettetem View zurück, der im übergeordneten View angezeigt wird.
    /// en: Returns the generated HTML block with the embedded view, which will be displayed in the parent view.
    /// </returns>
    public object IncludeView(
        string id,
        bool encodeQueryString = true)
    {
        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                        div.AddClass("datalinq-include")
                           .AddAttribute("data-source", ParseUrl(id, encodeQueryString))
                    )
                    .BuildHtmlString()
             );
    }

    /// <summary>
    /// de: Die Methode bindet einen View ein und übergibt Filter- und Sortierungsparameter an den eingebundenen View. Dies ist nützlich, wenn der eingebundene View nach Werten filtern soll, die im übergeordneten View mitgegeben werden.
    /// en: The method embeds a view and passes filter and sorting parameters to the embedded view. This is useful when the embedded view needs to filter based on values provided in the parent view.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die ID des Views an, die im Format endpoint-id@query-id@view-id vorliegt, um den richtigen View zu laden.
    /// en: Specifies the ID of the view in the format endpoint-id@query-id@view-id to load the correct view.
    /// </param>
    /// <param name="filter">
    /// de: Ein Filter im Format von URL-Parametern (z.B. NAME=Franz&amp;STRASSE=Eberweg), der auf die abgerufenen Daten im eingebundenen View angewendet wird.
    /// en: A filter in the format of URL parameters (e.g., NAME=Franz&amp;STRASSE=Eberweg) that is applied to the data retrieved in the embedded view.
    /// </param>
    /// <param name="orderby">
    /// de: Ein optionaler Parameter, der angibt, nach welchen Feldern die Daten im eingebundenen View sortiert werden sollen. Vor einem Feldnamen kann ein Minuszeichen (-) gesetzt werden, um absteigend zu sortieren.
    /// en: An optional parameter specifying the fields by which the data in the embedded view should be sorted. A minus sign (-) before a field name indicates descending order.
    /// </param>
    /// <param name="encodeUrl">
    /// de: Gibt an, ob die URL-Parameter URL-kodiert werden sollen. Standardmäßig ist dieser Wert auf true gesetzt.
    /// en: Indicates whether the URL parameters should be URL encoded. This is true by default.
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block mit eingebettetem View und den übergebenen Parametern zurück.
    /// en: Returns the generated HTML block with the embedded view and the passed parameters.
    /// </returns>
    public object IncludeView(
        string id,
        string filter,
        string orderby = "",
        bool encodeUrl = true)
    {
        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                        div.AddClass("datalinq-include")
                           .AddAttribute("data-source", ParseUrl(id, encodeUrl))
                           .AddAttribute("data-filter", ParseUrl(filter, encodeUrl))
                           .AddAttribute("data-orderby", ParseUrl(orderby, encodeUrl))
                    )
                    .BuildHtmlString()
             );
    }

    /// <summary>
    /// de: Die Methode bindet einen View ein, der nicht sofort nach dem Aufbau des übergeordneten Views geladen wird, sondern erst, wenn der Benutzer auf einen Button klickt, der den View anzeigt.
    /// en: The method embeds a view that is not loaded immediately after the parent view is built, but only when the user clicks a button to load the view.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die ID des Views an, die im Format endpoint-id@query-id@view-id vorliegt, um den richtigen View zu laden.
    /// en: Specifies the ID of the view in the format endpoint-id@query-id@view-id to load the correct view.
    /// </param>
    /// <param name="text">
    /// de: Der Text, der auf der Schaltfläche angezeigt wird, die den View lädt, wenn der Benutzer darauf klickt.
    /// en: The text displayed on the button that loads the view when the user clicks it.
    /// </param>
    /// <param name="encodeUrl">
    /// de: Gibt an, ob die URL-Parameter URL-kodiert werden sollen. Standardmäßig ist dieser Wert auf true gesetzt.
    /// en: Indicates whether the URL parameters should be URL encoded. This is true by default.
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block mit eingebettetem View und einer Schaltfläche zurück, die den View aufruft.
    /// en: Returns the generated HTML block with the embedded view and a button that triggers the view to be loaded.
    /// </returns>
    public object IncludeClickView(
        string id,
        string text,
        bool encodeUrl = true)
    {
        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                        div.AddClass("datalinq-include-click")
                           .AddAttribute("data-source", ParseUrl(id, encodeUrl))
                           .AddAttribute("data-header", text)
                    )
                    .BuildHtmlString()
             );
    }

    /// <summary>
    /// de: Die Methode bindet einen View ein, der nicht sofort nach dem Aufbau des übergeordneten Views geladen wird, sondern erst, wenn der Benutzer auf eine Schaltfläche klickt. Dabei werden Filter- und Sortierungsparameter an den eingebundenen View übergeben.
    /// en: The method embeds a view that is not loaded immediately after the parent view is built, but only when the user clicks a button. Filter and sorting parameters are passed to the embedded view.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die ID des Views an, die im Format endpoint-id@query-id@view-id vorliegt, um den richtigen View zu laden.
    /// en: Specifies the ID of the view in the format endpoint-id@query-id@view-id to load the correct view.
    /// </param>
    /// <param name="text">
    /// de: Der Text, der auf der Schaltfläche angezeigt wird, die den View lädt, wenn der Benutzer darauf klickt.
    /// en: The text displayed on the button that loads the view when the user clicks it.
    /// </param>
    /// <param name="filter">
    /// de: Ein Filter im Format von URL-Parametern (z.B. NAME=Franz&amp;STRASSE=Eberweg), der auf die abgerufenen Daten im eingebundenen View angewendet wird.
    /// en: A filter in the format of URL parameters (e.g., NAME=Franz&amp;STRASSE=Eberweg) that is applied to the data retrieved in the embedded view.
    /// </param>
    /// <param name="orderby">
    /// de: Ein optionaler Parameter, der angibt, nach welchen Feldern die Daten im eingebundenen View sortiert werden sollen. Vor einem Feldnamen kann ein Minuszeichen (-) gesetzt werden, um absteigend zu sortieren.
    /// en: An optional parameter specifying the fields by which the data in the embedded view should be sorted. A minus sign (-) before a field name indicates descending order.
    /// </param>
    /// <param name="encodeUrl">
    /// de: Gibt an, ob die URL-Parameter URL-kodiert werden sollen. Standardmäßig ist dieser Wert auf true gesetzt.
    /// en: Indicates whether the URL parameters should be URL encoded. This is true by default.
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block mit eingebettetem View und einer Schaltfläche zurück, die den View aufruft, nachdem der Benutzer darauf geklickt hat.
    /// en: Returns the generated HTML block with the embedded view and a button that triggers the view to be loaded when the user clicks it.
    /// </returns>
    public object IncludeClickView(
        string id,
        string text,
        string filter,
        string orderby = "",
        bool encodeUrl = true)
    {
        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                        div.AddClass("datalinq-include-click")
                           .AddAttribute("data-source", ParseUrl(id, encodeUrl))
                           .AddAttribute("data-filter", ParseUrl(filter, encodeUrl))
                           .AddAttribute("data-orderby", ParseUrl(orderby, encodeUrl))
                           .AddAttribute("data-header", text)
                    )
                    .BuildHtmlString()
             );
    }

    /// <summary>
    /// de: Die Methode erzeugt einen Button, mit dem der Anwender den View durch Klick aktualisieren kann. Es wird dabei immer nur der View aktualisiert, in dem der Button mit dieser Methode eingefügt wurde.
    /// en: The method generates a button that allows the user to refresh the view by clicking. Only the view in which the button was added will be refreshed.
    /// </summary>
    /// <param name="label">
    /// de: Der Text, der auf dem Button angezeigt wird. Standardmäßig ist der Text "Aktualisieren".
    /// en: The text displayed on the button. By default, the text is "Aktualisieren".
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für den Button. ((z.B.: new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object containing HTML attributes for the button. ((e.g.: new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block für den Button zurück.
    /// en: Returns the generated HTML block for the button.
    /// </returns>
    public object RefreshViewClick(
        string label = "Aktualisieren",
        object htmlAttributes = null)
    {
        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendRefreshViewClickButtton(label, htmlAttributes)
                    .BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Die Methode erzeugt einen Button, über den ein View automatisch nach einer festgelegten Zeitspanne aktualisiert wird. Der Button ermöglicht es dem Benutzer, das automatische Aktualisieren durch ein Checkbox-Symbol zu aktivieren oder zu deaktivieren. Es wird nur der View aktualisiert, in dem sich der Button befindet.
    /// en: The method creates a button that automatically refreshes a view after a set period of time. The button allows the user to activate or deactivate the auto-refresh feature through a checkbox symbol. Only the view containing the button will be refreshed.
    /// </summary>
    /// <param name="label">
    /// de: Der Text, der auf dem Button angezeigt wird, um die automatische Aktualisierung zu steuern. Standardmäßig ist der Text "Wird aktualisiert in".
    /// en: The text displayed on the button to control the auto-refresh. By default, the text is "Wird aktualisiert in".
    /// </param>
    /// <param name="seconds">
    /// de: Die Anzahl der Sekunden, nach denen der View automatisch aktualisiert wird.
    /// en: The number of seconds after which the view will be automatically refreshed.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für den Button. ((z.B.: new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object containing HTML attributes for the button. ((e.g.: new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="isActive">
    /// de: Gibt an, ob der Timer zu Beginn aktiv ist oder ob er erst durch den Benutzer über das Checkbox-Symbol aktiviert wird.
    /// en: Indicates whether the timer is active initially or if it is activated by the user through the checkbox symbol.
    /// </param>
    /// <returns>
    /// de: Gibt den generierten HTML-Block für den Button und das zugehörige Kontrollkästchen zurück, mit der Möglichkeit zur Steuerung des automatischen Updates.
    /// en: Returns the generated HTML block for the button and the associated checkbox, allowing the user to control the automatic update.
    /// </returns>
    public object RefreshViewTicker(
        string label = "Wird aktualisiert in",
        int seconds = 60,
        object htmlAttributes = null,
        bool isActive = true)
    {
        string id = Guid.NewGuid().ToString("N");
        string label_ = label;

        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                    {
                        div.AddClass("datalinq-refresh-ticker-container")
                           .AddAttributes(htmlAttributes)

                           .AppendCheckbox(isActive, checkbox =>
                               checkbox
                                    .WithId(id)
                                    .AddClass("datalinq-refresh-ticker")
                                    .AddAttribute("data-value", seconds.ToString())
                                    .AddAttribute("data-label", label)
                           )
                           .AppendLabelFor(id, label =>
                               label.AddStyle("display", "inline")
                                    .Content($"&nbsp;{label_}&nbsp;{seconds}&nbsp;Sekunden")
                           );
                    })
                    .BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Die Methode erzeugt ein Steuerelement zum Sortieren eines Views. Das Element klappt auf, wenn auf einen Button geklickt wird. Danach kann der Anwender über Checkboxen bestimmen, nach welchen Eigenschaften sortiert wird bzw. ob auf- oder absteigend sortiert werden soll. Nach Bestätigung der Angaben wird der View, in dem das Steuerelement eingebaut wurde, aktualisiert.
    /// en: The method generates a control for sorting a view. The element expands when a button is clicked. The user can then select which properties to sort by and whether the sorting should be ascending or descending. Once the options are confirmed, the view containing the control will be updated.
    /// </summary>
    /// <param name="label">
    /// de: Der Text für den Button, der das Sortier-Steuerelement aufklappen lässt (z.B.: "Sortierung")
    /// en: The text for the button that expands the sorting control (e.g., "Sorting")
    /// </param>
    /// <param name="orderFields">
    /// de: Ein String-Array mit den Feldern, nach denen sortiert werden kann (z.B.: new string[]{ "ORT","STR" }). Die Felder entsprechen den Feldnamen, die auch innerhalb des Records vorkommen
    /// en: A string array containing the fields that can be used for sorting (e.g., new string[]{ "CITY", "STREET" }). The fields match the field names as they appear within the record
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für den Button. ((z.B.: new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object containing HTML attributes for the button. ((e.g.: new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="isOpen">
    /// de: Boolscher Wert, der angibt, ob die Sortieransicht zu Beginn geöffnet (aufgeklappt) sein soll.
    /// en: A boolean value indicating whether the sorting view should be open (expanded) by default.
    /// </param>
    /// <returns>
    /// de: Gibt das generierte HTML für das Sortier-Steuerelement zurück.
    /// en: Returns the generated HTML for the sorting control.
    /// </returns>
    public object SortView(
        string label,
        string[] orderFields,
        object htmlAttributes = null,
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


    /// <summary>
    /// de: Die Methode erzeugt ein Steuerelement zum Sortieren eines Views. Das Element klappt auf, wenn auf einen Button geklickt wird. Danach kann der Anwender über Checkboxen bestimmen, nach welchen Eigenschaften sortiert wird bzw. ob auf- oder absteigend sortiert werden soll. Nach Bestätigung der Angaben wird der View, in dem das Steuerelement eingebaut wurde, aktualisiert.
    /// en: The method generates a control for sorting a view. The element expands when a button is clicked. The user can then select which properties to sort by and whether the sorting should be ascending or descending. Once the options are confirmed, the view containing the control will be updated.
    /// </summary>
    /// <param name="label">
    /// de: Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes (z.B.: "Sortierung").
    /// en: The text for the button that expands the sorting control (e.g., "Sorting").
    /// </param>
    /// <param name="orderFields">
    /// de: Hier wird anstelle von Strings ein Dictionary übergeben. Die Keys entsprechen den Feldnamen von oben. Die Values geben ein anonymes Objekt an, mit dem beispielsweise die Anzeigenamen der Felder bestimmt werden können. ((orderFields: new Dictionary&lt;string, object&gt;() { { "ORT", new { 
    /// displayname = "Ort/Gemeinde" } }, { "STR", new { displayname = "Strasse" } } }))
    /// en: Instead of strings, a dictionary is passed. The keys correspond to the field names from above. The values are anonymous objects that can define properties like display names for the fields. ((orderFields: new Dictionary&lt;string, object&gt;() { { "CITY", new { 
    /// displayname = "City/Village" } }, { "STREET", new { displayname = "Street" } } }))
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für den Button ((z.B.: new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object containing HTML attributes for the button ((e.g.: new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="isOpen">
    /// de: Boolscher Wert, der angibt, ob die Sortierung zu Beginn geöffnet (aufgeklappt) ist.
    /// en: A boolean value indicating whether the sorting view should be open (expanded) by default.
    /// </param>
    /// <returns>
    /// de: Gibt das generierte HTML für das Sortier-Steuerelement zurück.
    /// en: Returns the generated HTML for the sorting control.
    /// </returns>
    public object SortView(
        string label,
        Dictionary<string, object> orderFields,
        object htmlAttributes = null,
        bool isOpen = false)
    {
        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                    {
                        div.AddClass("datalinq-refresh-ordering-container")
                           .AddClass("collapsed")
                           .AddAttributes(htmlAttributes)
                           .AddAttribute("data-isOpen", isOpen.ToString())
                           .AppendButton(button =>
                               button.AddClass("datalinq-button menu")
                                     .AddAttribute("onclick", "$(this).closest('.datalinq-refresh-ordering-container').toggleClass('collapsed');$(this).next('.datalinq-refresh-ordering-body').slideToggle()")
                                     .Content(label)
                           )
                           .AppendDiv(div =>
                               div.AddClass("datalinq-refresh-ordering-body")
                                  .AddStyle("display", "none")
                                  .AppendTable(table =>
                                  {
                                      table.AppendTableRow(tr =>
                                      {
                                          tr.AppendTableHeaderCell(th => th.Content("Feld"))
                                            .AppendTableHeaderCell(th => th.Content("Absteigend"));
                                      });
                                      foreach (var orderField in orderFields)
                                      {
                                          var orderProperties = ToDictionary(orderField.Value);
                                          bool checkedAsc = Convert.ToBoolean(GetDefaultValueFromRecord(orderProperties, "checked", false));
                                          bool checkedDesc = Convert.ToBoolean(GetDefaultValueFromRecord(orderProperties, "checkedDesc", false));

                                          table.AppendTableRow(tr =>
                                          {
                                              tr.AppendTableCell(td =>
                                                  td.AppendLabel(label =>
                                                      label
                                                        .AppendCheckbox(checkedAsc, checkbox =>
                                                            checkbox
                                                                .AddClass("datalinq-ordering-field")
                                                                .WithName(orderField.Key)
                                                                .AddAttribute("onclick", "dataLinq.updateViewOrdering(this)")
                                                        )
                                                        .AppendSpan(span =>
                                                            span.Content(GetDefaultValueFromRecord(orderProperties, "displayname", orderField.Key).ToString())
                                                        )
                                                     )
                                                 )
                                                 .AppendTableCell(td =>
                                                    td.AppendCheckbox(checkedDesc, checkbox =>
                                                        checkbox
                                                           .AddClass("datalinq-ordering-desc")
                                                           .WithName(orderField.Key)
                                                           .AddAttribute("onclick", "dataLinq.updateViewOrdering(this)")
                                                    ).AddStyle("text-align", "center")
                                              );
                                          });
                                      }
                                  })
                                  .AppendRefreshViewClickButtton("Übernehmen")
                           );
                    })
                    .BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Die Methode erzeugt ein Steuerelement zum Filtern eines Views. Das Element klappt auf, wenn auf einen Button geklickt wird. Danach kann der Anwender über Eingabefelder/Auswahllisten bestimmen, nach welchen Eigenschaften gefiltert wird. Nach Bestätigung der Angaben wird der View, in dem das Steuerelement eingebaut wurde, aktualisiert.
    /// en: The method generates a control for filtering a view. The element expands when a button is clicked. The user can then select which properties to filter by using input fields or dropdown lists. Once the options are confirmed, the view containing the control will be updated.
    /// </summary>
    /// <param name="label">
    /// de: Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes (z.B.: "Filter setzen").
    /// en: The text for the button that expands the filtering control (e.g., "Apply Filter").
    /// </param>
    /// <param name="filterParameters">
    /// de: Ein String-Array mit Parametern, nach denen gefiltert werden kann ((z.B.: new string[]{ "ort","strasse" } )) 
    /// Die Parameter entsprechen hier jenen Parameternamen, die an die entsprechende Abfrage für den View übergeben 
    /// werden können.
    /// en: A string array with parameters that can be used for filtering ((e.g., new string[]{ "city","street" })) 
    /// The parameters correspond to the parameter names that can be passed to the 
    /// corresponding query for the view.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für den Button ((z.B.: new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object containing HTML attributes for the button ((e.g., new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="isOpen">
    /// de: Boolscher Wert, der angibt, ob der Filter zu Beginn geöffnet (aufgeklappt) ist.
    /// en: A boolean value indicating whether the filter view should be open (expanded) by default.
    /// </param>
    /// <returns>
    /// de: Gibt das generierte HTML für das Filter-Steuerelement zurück.
    /// en: Returns the generated HTML for the filtering control.
    /// </returns>
    public object FilterView(
        string label,
        string[] filterParameters,
        object htmlAttributes = null,
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

    /// <summary>
    /// de: Die Methode erzeugt ein Steuerelement zum Filtern eines Views. Das Element klappt auf, wenn auf einen Button geklickt wird. Danach kann der Anwender über Eingabefelder/Auswahllisten bestimmen, nach welchen Eigenschaften gefiltert wird. Nach Bestätigung der Angaben wird der View, in dem das Steuerelement eingebaut wurde, aktualisiert.
    /// en: The method generates a control for filtering a view. The element expands when a button is clicked. The user can then select which properties to filter by using input fields or dropdown lists. Once the options are confirmed, the view containing the control will be updated.
    /// </summary>
    /// <param name="label">
    /// de: Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes (z.B.: "Filter setzen").
    /// en: The text for the button that expands the filtering control (e.g., "Apply Filter").
    /// </param>
    /// <param name="filterParameters">
    /// de: Hier wird anstelle von Strings ein Dictionary übergeben. Die Keys entsprechen den Parametern von oben. 
    /// Die Values geben ein anonymes Objekt an, mit dem beispielsweise die Anzeigenamen der Parameter bestimmt
    /// werden können oder auch, dass es ein Datumsfeld ist (siehe TextFor). Außerdem besteht die Möglichkeit, 
    /// `diaplyName`, `source`, `valueField`, `nameField` und `prependEmpty` anzugeben. Damit wird eine Auswahlliste erzeugt. 
    /// (( 
    /// {
    ///   { "kg", new { 
    ///     displayname="KG Number", 
    ///     valueField="kg", 
    ///     nameField="kg", 
    ///     source="offline-dkm@lut-kg", 
    ///     prependEmpty=true 
    ///     } 
    ///   },
    ///   { "gnr", new { 
    ///     displayname="GR Number", 
    ///     valueField="gnr", 
    ///     nameField="gnr", 
    ///     source="offline-dkm@lut-gnr?kg=[kg]", 
    ///     prependEmpty=true 
    ///     } 
    ///   }
    /// } ))
    /// Gibt man bei `source` einen Filter an, der auf auf mindesten ein anderes Feld verweist, wird dieses Feld
    /// erst geladen, wenn der Filter für das andere Feld gesetzt wurde. Das bedeutet, dass die Abfrage erst
    /// durchgeführt wird, wenn alle abhängigen Werte eingeben werden. So können Cascading Comboboxen erzeugt werden.
    /// Um Auswahlmenüs (ComboBox) mit Mehrfach-Auswahl zu ermöglichen, kann das Attribut `multiple='multiple'` 
    /// mitgegeben werden.
    /// en: Instead of strings, a dictionary is passed. The keys correspond to the filter parameters from above.
    /// The values represent an anonymous object that can define display names, data types (e.g., Date), 
    /// and additional options like 
    /// `diaplayName`, `source`, `valueField`, `nameField`, and `prependEmpty`. This allows 
    /// generating a dropdown list. 
    /// (( 
    /// {
    ///   { "kg", new { 
    ///     displayname="KG Number", 
    ///     valueField="kg", 
    ///     nameField="kg", 
    ///     source="offline-dkm@lut-kg", 
    ///     prependEmpty=true 
    ///     } 
    ///   },
    ///   { "gnr", new { 
    ///     displayname="GR Number", 
    ///     valueField="gnr", 
    ///     nameField="gnr", 
    ///     source="offline-dkm@lut-gnr?kg=[kg]", 
    ///     prependEmpty=true 
    ///     } 
    ///   }
    /// } ))
    /// If a filter is specified in `source` that references at least one other field, 
    /// this field will only be loaded once the filter for the other field is set. This means 
    /// the query will only be executed after all dependent values are provided. 
    /// This allows for the creation of cascading comboboxes.
    /// For multi-select dropdowns, the `multiple='multiple'` attribute 
    /// can be added.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für den Button (z.B.: new { style="width:300px" @class="meine-klasse" }).
    /// en: An anonymous object containing HTML attributes for the button (e.g., new { style="width:300px" @class="my-class" }).
    /// </param>
    /// <param name="isOpen">
    /// de: Boolscher Wert, der angibt, ob der Filter zu Beginn geöffnet (aufgeklappt) ist.
    /// en: A boolean value indicating whether the filter should be open (expanded) by default.
    /// </param>
    /// <returns>
    /// de: Gibt das generierte HTML für das Filter-Steuerelement zurück.
    /// en: Returns the generated HTML for the filtering control.
    /// </returns>
    public object FilterView(
        string label,
        Dictionary<string, object> filterParameters,
        object htmlAttributes = null,
        bool isOpen = false)
    {

        return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(div =>
                    {
                        div.AddClass("datalinq-refresh-filter-container collapsed");
                        div.AddAttributes(htmlAttributes);
                        div.AddAttribute("data-isOpen",isOpen.ToString());
                        div.AppendButton(button =>
                        {
                            button.AddClass("datalinq-button menu");
                            button.AddAttribute("onclick", "$(this).closest('.datalinq-refresh-filter-container').toggleClass('collapsed');$(this).next('.datalinq-refresh-filter-body').slideToggle()");
                            button.Content(label);
                        });
                        div.AppendDiv(div2 =>
                        {
                            div2.AddClass("datalinq-refresh-filter-body");
                            div2.AddStyle("display", "none");

                            foreach (string filterParameter in filterParameters.Keys)
                            {
                                var filterProperties = ToDictionary(filterParameters[filterParameter]);
                                div2.AppendDiv(div3 =>
                                {
                                    div3.AddClass("datalinq-filter-field-wrapper");

                                    if (filterProperties != null && filterProperties.ContainsKey("source"))
                                    {
                                        var source = GetDefaultValueFromRecord(filterProperties, "source").ToString();
                                        var dependsOn = source.KeyParameters();

                                        div3.AppendDiv(div4 =>
                                        {
                                            div4.AddClass("datalinq-label");
                                            div4.Content(GetDefaultValueFromRecord(filterProperties, "displayname", filterParameter).ToString());
                                        });
                                        div3.ComboFor(
                                            GetDefaultValueFromRecord(filterProperties, "defaultValue"), 
                                            filterParameter,
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
                                            }
                                        );
                                    }
                                    else if (filterProperties != null && filterProperties.ContainsKey("hidden") && GetDefaultValueFromRecord(filterProperties, "hidden").Equals("true"))
                                    {
                                        div3.AppendInput(input =>
                                        {
                                            input.AddAttribute("type", "hidden");
                                            input.AddAttribute("name", filterParameter);
                                            input.AddClass("datalinq-filter-parameter");
                                            input.AddAttribute("value", GetDefaultValueFromRecord(filterProperties, "defaultValue")?.ToString() ?? "");
                                        });
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

                                        div3.AppendDiv(div6 =>
                                        {
                                            div6.AddClass("datalinq-label");
                                            div6.Content(GetDefaultValueFromRecord(filterProperties, "displayname", filterParameter).ToString());
                                        }).AppendInput(input =>
                                        {
                                            input.AddClass("datalinq-filter-parameter datalinq-input");
                                            input.AddAttribute("type", fieldType.ToString().ToLower());
                                            input.AddAttribute("name", filterParameter);
                                            input.AddAttribute("onkeyup", "dataLinq.updateViewFilter(this)");
                                            input.AddAttribute("onchange", "dataLinq.updateViewFilter(this)");

                                            if (filterProperties != null)
                                            {
                                                if (filterProperties.ContainsKey("dataType"))
                                                {
                                                    input.AddAttribute("data-datatype", GetDefaultValueFromRecord(filterProperties, "dataType")?.ToString());
                                                }
                                                if (filterProperties.ContainsKey("dropProperty") && filterProperties.ContainsKey("dropQuery"))
                                                {
                                                    input.AddAttribute("data-drop-property", GetDefaultValueFromRecord(filterProperties, "dropProperty")?.ToString());
                                                    input.AddAttribute("data-drop-query", GetDefaultValueFromRecord(filterProperties, "dropQuery")?.ToString());
                                                }
                                            }
                                        });                                    
                                    }
                                });
                            }
                            div2.AppendBreak();
                            div2.AppendDiv(div5 =>
                            {
                                div5.AddClass("datalinq-filter-buttongroup");
                                div5.AppendButton(button2 =>
                                {
                                    button2.AddClass("datalinq-button datalinq-filter-clear");
                                    button2.AddAttribute("onclick", "dataLinq.clearFilter(this)");
                                    button2.Content("Filter leeren");
                                });
                                div5.AppendRefreshViewClickButtton("Übernehmen");
                            });
                        });
                    }).BuildHtmlString()
                );
    }


    /// <summary>
    /// de: Die Methode erzeugt ein Steuerelement zum Exportieren der Daten des Views in eine CSV-Datei. Dabei wird die aktuell eingestellte Filterung bzw. Sortierung angewendet. Der Export bezieht sich auf die Daten jenes Views, in dem die Schaltfläche eingebunden ist.
    /// en: The method generates a control to export the data of the view into a CSV file, applying the current filtering and sorting settings. The export relates to the data of the view in which the button is embedded.
    /// </summary>
    /// <param name="label">
    /// de: Der Text für den Button zum Aufklappen des eigentlichen Steuerelementes (z.B.: "Export").
    /// en: The text for the button that expands the actual control (e.g., "Export").
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für diesen Button (z.B.: new { style="width:300px" @class="meine-klasse" }).
    /// en: An anonymous object with HTML attributes for the button (e.g., new { style="width:300px" @class="my-class" }).
    /// </param>
    /// <param name="columns">
    /// de: Die Spalten, die exportiert werden sollen. Wird nichts angegeben, werden alle Spalten exportiert.  
    /// en: The columns to be exported. If nothing is specified, all columns will be exported.  
    /// </param>
    /// <returns>
    /// de: Gibt das generierte HTML für das Export-Steuerelement zurück.
    /// en: Returns the generated HTML for the export control.
    /// </returns>
    public object ExportView(
        string label = "Export",
        object htmlAttributes = null,
        IEnumerable<string> columns = null)
    {
        string columnsJson = (columns != null && columns.Any())
            ? System.Text.Json.JsonSerializer.Serialize(columns)
            : null;

        string onclickJs = columnsJson != null
            ? $"dataLinq.export(this, {columnsJson})"
            : "dataLinq.export(this)";

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendButton(b =>
                {
                    b.AddClass("datalinq-button apply");
                    b.AddAttributes(htmlAttributes);
                    b.AddAttribute("onclick", onclickJs);
                    b.Content(label);
                }).BuildHtmlString()
            );
    }


    /// <summary>
    /// de: Macht aus einem Text einen Link. Dieser aktualisiert einen Filter einer View, das kann auch ein Filter eines in der Seite eingebauten Include(Click)Views sein.
    /// en: Turns text into a link that updates a filter of a view, which can also be a filter of an included (Click)View embedded on the page.
    /// </summary>
    /// <param name="filterName">
    /// de: Name des Filters, der gesetzt werden soll.
    /// en: The name of the filter to be set.
    /// </param>
    /// <param name="filterValue">
    /// de: Neuer Wert des Filters.
    /// en: The new value for the filter.
    /// </param>
    /// <param name="buttonText">
    /// de: Text, der auf der Schaltfläche stehen soll. Ansonsten kann diese über CSS mit einem Symbol versehen werden.
    /// en: Text to display on the button. Alternatively, this can be styled with a symbol via CSS.
    /// </param>
    /// <param name="filterId">
    /// de: HTML (DOM) Id des zu verändernden Filters. Dieser kann in der Methode 'FilterView' im 'htmlAttributes'-Parameter gesetzt werden. Falls dieser Wert leergelassen wird, wird der erste Filter im aktuellen View verwendet.
    /// en: HTML (DOM) Id of the filter to be changed. This can be set in the 'htmlAttributes' parameter of the 'FilterView' method. If left empty, the first filter in the current view will be used.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für diesen Button ((z.B.: new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the button ((e.g., new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <returns>
    /// de: Gibt das generierte HTML für den Filter-Update-Button zurück.
    /// en: Returns the generated HTML for the filter update button.
    /// </returns>
    public object UpdateFilterButton(
        string filterName,
        object filterValue,
        string buttonText = "",
        string filterId = "",
        object htmlAttributes = null)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendButton(b =>
                {
                    b.AddClass("datalinq-update-filter");
                    b.AddAttributes(htmlAttributes);
                    b.AddAttribute("data-filter-id", filterId);
                    b.AddAttribute("data-filter-name", filterName);
                    b.AddAttribute("data-filter-value", filterValue.ToString());
                    b.Content(String.IsNullOrEmpty(buttonText) ? "" : buttonText);
                }).BuildHtmlString()
            );
    }

    #endregion

    /// <summary>
    /// de: Erstellt aus den übergebenen Records eine HTML-Tabelle.  
    /// en: Creates an HTML table from the given records.  
    /// </summary>
    /// <param name="records">  
    /// de: Die Records, für die die Tabelle erstellt werden soll.  
    /// en: The records for which the table should be created.  
    /// </param>
    /// <param name="columns">  
    /// de: Die Spalten, die in der Tabelle angezeigt werden sollen. Wird nichts angegeben, werden alle Spalten verwendet.  
    /// en: The columns to be displayed in the table. If nothing is specified, all columns will be used.  
    /// </param>
    /// <param name="htmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für das <c>table</c>-Element, z. B. für das Styling.  
    /// en: An anonymous object containing HTML attributes for the <c>table</c> element, e.g., for styling.  
    /// </param>
    /// <param name="row0HtmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für die Titelzeile der Tabelle.  
    /// en: An anonymous object containing HTML attributes for the table's title row.  
    /// </param>
    /// <param name="row1HtmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für ungerade Tabellenzeilen (1, 3, 5, ...).  
    /// en: An anonymous object containing HTML attributes for odd table rows (1, 3, 5, ...).  
    /// </param>
    /// <param name="row2HtmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für gerade Tabellenzeilen (2, 4, 6, ...).  
    /// en: An anonymous object containing HTML attributes for even table rows (2, 4, 6, ...).  
    /// </param>
    /// <param name="cell0HtmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für die Zellen der Titelzeile.  
    /// en: An anonymous object containing HTML attributes for the title row cells.  
    /// </param>
    /// <param name="cellHtmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für die Zellen der Tabelle.  
    /// en: An anonymous object containing HTML attributes for the table cells.  
    /// </param>
    /// <param name="max">  
    /// de: Die maximale Anzahl an Zeilen, die angezeigt werden. Wenn ≤ 0 angegeben wird, werden alle Zeilen dargestellt.  
    /// en: The maximum number of rows to display. If ≤ 0 is specified, all rows will be displayed.  
    /// </param>
    /// <returns>  
    /// de: Eine HTML-Tabelle als Objekt.  
    /// en: An HTML table as an object.  
    /// </returns>
    public object Table(
        IEnumerable<IDictionary<string, object>> records,
        IEnumerable<string> columns = null,
        object htmlAttributes = null,
        object row0HtmlAttributes = null,
        object row1HtmlAttributes = null,
        object row2HtmlAttributes = null,
        object cell0HtmlAttributes = null,
        object cellHtmlAttributes = null,
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

        var html = HtmlBuilder.Create()
            .AppendTable(table =>
            {
                table
                    .AddAttributes(htmlAttributes ?? new { style = "width:100%;text-align:left;background:#efefef" })
                    // header row
                    .AppendTableRow(headerRow =>
                    {
                        headerRow.AddAttributes(row0HtmlAttributes ?? new { style = "background-color:#eee" });

                        // header cells
                        foreach (var column in columns)
                        {
                            headerRow.AppendTableHeaderCell(headerCell =>
                                  headerCell
                                      .Content(ToHtml(column))
                                      .AddAttributes(cell0HtmlAttributes ?? new { style = "padding:4px" })
                            );
                        }
                    });

                // data rows
                int counter = 0;
                foreach (var record in records.Take(max.Equals(0)?records.Count():max))
                {
                    table.AppendTableRow(tableRow =>
                    {
                        tableRow.AddAttributes(counter++ % 2 == 1
                                ? row1HtmlAttributes ?? new { style = "background-color:#ffd" }
                                : row2HtmlAttributes ?? new { style = "background-color:#fff" });

                        foreach (var column in columns)
                        {
                            tableRow.AppendTableCell(tableCell =>
                                        tableCell.Content(record.ContainsKey(column)
                                                        ? ToHtml(record[column]?.ToString())
                                                        : String.Empty)
                                        .AddAttributes(cellHtmlAttributes ?? new { style = "padding:4px" })
                                    );
                        }
                    });
                }

            })
        .BuildHtmlString();

        return _razor.RawString(html);
    }

    #region Formular

    /// <summary>
    /// de: Erstellt den Beginn eines HTML-Formulars zum Abschicken von Daten.  
    /// en: Creates the beginning of an HTML form for submitting data.  
    /// </summary>
    /// <param name="id">  
    /// de: Die ID der Query, an die die Formulardaten gesendet werden sollen,  
    /// im Format (( endpoint-id@query-id ))
    /// en: The ID of the query to which the form data should be sent,  
    /// in the format (( endpoint-id@query-id ))
    /// </param>
    /// <param name="htmlAttributes">  
    /// de: Ein anonymes Objekt mit HTML-Attributen für das form-Tag,  
    /// z. B.: (( new { style="width:300px", @class="meine-klasse" } ))
    /// en: An anonymous object containing HTML attributes for the form tag,  
    /// e.g.: (( new { style="width:300px", @class="my-class" } ))
    /// </param>
    /// <returns>  
    /// de: Der HTML-String für den Anfang eines Formulars.  
    /// en: The HTML string for the beginning of a form.  
    /// </returns>
    public object BeginForm(
        string id,
        object htmlAttributes = null)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendForm(f =>
                {
                    f.AddAttributes(htmlAttributes);
                    f.AddAttribute("action", $"../ExecuteNonQuery/{id}");
                    f.AddAttribute("method", "POST");
                }, WriteTags.OpenOnly).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt das Ende eines HTML-Formulars zum Abschicken von Daten,  
    /// optional mit Schaltflächen zum Senden und Zurücksetzen.  
    /// en: Creates the end of an HTML form for submitting data,  
    /// optionally with buttons for submission and reset.  
    /// </summary>
    /// <param name="submitText">  
    /// de: Falls angegeben, wird eine Schaltfläche zum Abschicken des Formulars mit dem angegebenen Text erstellt.  
    /// en: If provided, a button for submitting the form is created with the given text.  
    /// </param>
    /// <param name="cancelText">  
    /// de: Falls angegeben, wird eine Schaltfläche zum Zurücksetzen des Formulars mit dem angegebenen Text erstellt.  
    /// en: If provided, a button for resetting the form is created with the given text.  
    /// </param>
    /// <returns>  
    /// de: Der HTML-String für das Ende des Formulars.  
    /// en: The HTML string for the end of the form.  
    /// </returns>
    public object EndForm(
        string submitText = "",
        string cancelText = "")
    {
        var htmlBuilder = HtmlBuilder.Create();

        return _razor.RawString(
               (submitText.IsNotEmpty(), cancelText.IsNotEmpty()) switch
        {
            (true, true) => htmlBuilder
                                .AppendForm(form =>
                                {
                                    form
                                    .AppendBreak()
                                    .AppendButton(submitButton =>
                                    {
                                        submitButton.AddClass("datalinq-submit-form");
                                        submitButton.AddAttribute("type", "button");
                                        submitButton.AddAttribute("onclick", "dataLinq.submitForm(this)");
                                        submitButton.Content(submitText);
                                    })
                                    .AppendButton(cancelButton =>
                                    {
                                        cancelButton.AddClass("datalinq-reset-form");
                                        cancelButton.AddAttribute("type", "reset");
                                        cancelButton.Content(cancelText);
                                    });
                                }, WriteTags.CloseOnly).BuildHtmlString(),
            (true, false) => htmlBuilder
                                .AppendForm(form =>
                                {
                                    form
                                    .AppendBreak()
                                    .AppendButton(submitButton =>
                                    {
                                        submitButton.AddClass("datalinq-submit-form");
                                        submitButton.AddAttribute("type", "button");
                                        submitButton.AddAttribute("onclick", "dataLinq.submitForm(this)");
                                        submitButton.Content(submitText);
                                    });
                                }, WriteTags.CloseOnly).BuildHtmlString(),
            (false, true) => htmlBuilder
                                .AppendForm(form =>
                                {
                                    form
                                    .AppendBreak()
                                    .AppendButton(resetButton =>
                                    {
                                        resetButton.AddClass("datalinq-reset-form");
                                        resetButton.AddAttribute("type", "reset");
                                        resetButton.Content(cancelText);
                                    });
                                }, WriteTags.CloseOnly).BuildHtmlString(),
            _ => htmlBuilder
                    .AppendForm(form =>
                    {
                        form.AppendBreak();
                    }, WriteTags.CloseOnly).BuildHtmlString()
        });
    }

    /// <summary>
    /// de: Erstellt eine Schaltfläche, die eine View in einem Dialogfeld auf der aktuellen Seite öffnet. Darin könnte sich etwa ein Formular zum Bearbeiten oder Erstellen von Datensatzes befinden.
    /// en: "Creates a button that opens a view in a dialog on the current page. This could be a form for editing or creating a record.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die Id des Views in folgender Form an: endpoint-id@query-id@view-id
    /// en: Specifies the ID of the view in the following format: endpoint-id@query-id@view-id
    /// </param>
    /// <param name="parameter">
    /// de: Ein anonymes Objekt mit den Parametern, welche die Query erwartet. Falls ein Datensatz bearbeitet werden soll, dessen Primärschlüssel. Falls ein neuer Datensatz eingefügt werden soll (d.h. PK noch nicht vorhanden): Wert als leerer String übergeben, bspw. ((new { PK="''"} ))
    /// en: An anonymous object with the parameters expected by the query. If a record is to be edited, its primary keys should be provided. If a new record is to be inserted (i.e., PK not yet present): pass an empty string as the value, e.g., ((new { PK="''"} ))
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit Attributen, die für die Schaltfläche relevant sind, bspw. ((new { style="width:300px" @class="meine-klasse" } ))
    /// en: An anonymous object with attributes relevant to the button, e.g., ((new { style="width:300px" @class="my-class" } ))
    /// </param>
    /// <param name="buttonText">
    /// de: Text, der auf der Schaltfläche stehen soll. Ansonsten kann diese über CSS mit einem Symbol versehen werden.
    /// en: Text to display on the button. Otherwise, this can be styled with a symbol via CSS.
    /// </param>
    /// <param name="dialogAttributes">
    /// de: Ein anonymes Objekt mit Attributen, die für das Dialogfenster relevant sind oder einen Bestätigungstext enthalten, bspw. ((new { dialogWidth = "'500px'", dialogHeight = "'500px'",
    /// dialogTitle = "'Gewählte Datensätze bearbeiten'", confirmText = "'sicher?'" } ))
    /// en: An anonymous object with attributes relevant to the dialog window or containing a confirmation text, e.g., ((new { dialogWidth = "'500px'", dialogHeight = "'500px'",
    /// dialogTitle = "'Edit Selected Records'", confirmText = "'Are you sure?'" } ))
    /// </param>
    /// <returns>
    /// de: Ein HTML-Button, der beim Klicken das Dialogfeld öffnet und die angegebenen Parameter übergibt.
    /// en: An HTML button that opens the dialog when clicked and passes the specified parameters.
    /// </returns>
    public object OpenViewInDialog(
        string id,
        object parameter,
        object htmlAttributes = null,
        string buttonText = "",
        object dialogAttributes = null)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendButton(b => {
                    b.AddClass("datalinq-open-dialog");
                    b.AddAttributes(htmlAttributes);
                    b.AddAttribute("data-dialog-id",id);
                    b.AddAttribute("data-dialog-parameter", parameter != null ? JsonConvert.SerializeObject(parameter) : "{}");
                    b.AddAttribute("data-dialog-attributes", dialogAttributes != null ? JsonConvert.SerializeObject(dialogAttributes) : "{}");
                    b.Content(String.IsNullOrEmpty(buttonText) ? "" : buttonText);
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt eine Schaltfläche, die eine Query mit den übergebenen Parametern ausführt. Damit kann bspw. ein Datensatz gelöscht oder ein Wert gesetzt werden.
    /// en: Creates a button that executes a query with the passed parameters. This can be used to delete a record or set a value.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die Id des Query in folgender Form an: endpoint-id@query-id
    /// en: Specifies the ID of the query in the following format: endpoint-id@query-id
    /// </param>
    /// <param name="parameter">
    /// de: Ein anonymes Objekt mit den Parametern, welche die Query erwartet, bspw. ((new { PK=record["PK"} ))
    /// en: An anonymous object with the parameters expected by the query, e.g., ((new { PK=record["PK"} ))
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für die Schaltfläche, bspw. ((new { style="width:300px" @class="meine-klasse" } ))
    /// en: An anonymous object with HTML attributes for the button, e.g., ((new { style="width:300px" @class="my-class" } ))
    /// </param>
    /// <param name="buttonText">
    /// de: Text, der auf der Schaltfläche stehen soll. Ansonsten kann diese über CSS mit einem Symbol versehen werden.
    /// en: Text to display on the button. Otherwise, this can be styled with a symbol via CSS.
    /// </param>
    /// <param name="dialogAttributes">
    /// de: Ein anonymes Objekt mit Attributen, die für das Dialogfenster relevant sind oder einen Bestätigungstext enthalten, bspw. ((new { dialogWidth = "'500px'", dialogHeight = "'500px'",
    /// confirmText = "'sicher?'" } ))
    /// en: An anonymous object with attributes relevant to the dialog window or containing a confirmation text, e.g., ((new { dialogWidth = "'500px'", dialogHeight = "'500px'",
    /// confirmText = "'Are you sure?'" } ))
    /// </param>
    /// <returns>
    /// de: Ein HTML-Button, der beim Klicken die Query ausführt und die angegebenen Parameter übergibt.
    /// en: An HTML button that executes the query when clicked and passes the specified parameters.
    /// </returns>
    public object ExecuteNonQuery(
        string id,
        object parameter,
        object htmlAttributes = null,
        string buttonText = "",
        object dialogAttributes = null)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendButton(b =>
                {
                    b.AddClass("datalinq-execute-non-query");
                    b.AddAttributes(htmlAttributes);
                    b.AddAttribute("data-dialog-id", id);
                    b.AddAttribute("data-dialog-parameter", parameter != null ? JsonConvert.SerializeObject(parameter) : "{}");
                    b.AddAttribute("data-dialog-attributes", dialogAttributes != null ? JsonConvert.SerializeObject(dialogAttributes) : "{}");
                    b.Content(String.IsNullOrEmpty(buttonText) ? "" : buttonText);
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt ein HTML-Auswahlliste (Select), deren Optionen aus einer Abfrage geladen werden
    /// en: Creates an HTML select dropdown whose options are loaded from a query.
    /// </summary>
    /// <param name="id">
    /// de: Gibt die Id des Querys, der die Optionen bereithält, in folgender Form an: endpoint-id@query-id
    /// en: Specifies the ID of the query that provides the options in the following format: endpoint-id@query-id
    /// </param>
    /// <param name="url">
    /// de: Die URL des Endpunkts, von dem die Daten geladen werden sollen.
    /// en: The URL of the endpoint from which the data should be loaded.
    /// </param>
    /// <param name="valueField">
    /// de: Name der Spalte des Abfrage(Query)-Ergebnisses, die die Values für die Select-Option enthält.
    /// en: The name of the column in the query result that contains the values for the select options.
    /// </param>
    /// <param name="nameField">
    /// de: Name der Spalte des Abfrage(Query)-Ergebnisses, die den Anzeigenamen für die Select-Option enthält.
    /// en: The name of the column in the query result that contains the display names for the select options.
    /// </param>
    /// <param name="defaultValue">
    /// de: Wert (Value), der vorausgewählt sein soll.
    /// en: The value to be pre-selected.
    /// </param>
    /// <param name="prependEmpty">
    /// de: Gibt an, ob den Auswahloptionen eine leere Option vorangestellt werden soll (damit ist die Auswahl wieder aufhebbar).
    /// en: Indicates whether an empty option should be prepended to the options (making the selection resettable).
    /// </param>
    /// <returns>
    /// de: Ein HTML-Select-Element, das die Optionen aus der angegebenen Abfrage lädt.
    /// en: An HTML select element that loads options from the specified query.
    /// </returns>
    public object IncludeCombo(
        string id, string url,
        string valueField,
        string nameField,
        object defaultValue = null,
        bool prependEmpty = false)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendSelect(s =>
                {
                    s.AddClass("datalinq-include-combo");
                    s.AddAttribute("id", id);
                    s.AddAttribute("name",id);
                    s.AddAttribute("data-url",url);
                    s.AddAttribute("data-valuefield", valueField);
                    s.AddAttribute("data-namefield", nameField);
                    s.AddAttribute("data-defaultvalue", defaultValue == null ? "" : defaultValue.ToString());
                    s.AddAttribute("data-prepend-empty", prependEmpty.ToString().ToLower());
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt eine HTML-Auswahlliste (Select) für ein Formular, deren Optionen aus einer Abfrage ODER einer Liste geladen werden.
    /// en: Creates an HTML select dropdown for a form, with options loaded from a query OR a list.
    /// </summary>
    /// <param name="record">
    /// de: DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen.
    /// en: DataLinq record from which the default selected value will be retrieved. If the record is null, no default selection will be made.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements.
    /// en: The name (attribute) of the form element.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style="width:300px" @class="meine-klasse" select2="never" }))... wird select2=never nicht angegeben, wird das Element automatisch in ein Select2 Element (Combo + Eingabeled für die Suche nach Elementen) umgewandelt, wenn mehr als 20 Einträge vorhanden sind.
    /// en: An anonymous object with HTML attributes for the form element, e.g., ((new { style="width:300px" @class="meine-klasse" select2="never" }))... if select2=never is not provided, the element will automatically be converted into a Select2 element (combo with input field for searching items) if more than 20 entries are present.
    /// </param>
    /// <param name="source">
    /// de: Datenquelle der Auswahloptionen, kann eine Abfrage-URL sein (siehe IncludeCombo, bspw. (( new { source="endpoint-id@query-id@view-id",
    /// valueField="VALUE", nameField="NAME", prependEmpty=true }) )), oder ein Dictionary mit Werten für Value und Anzeigenname, bspw. (( new { source=new Dictionary&lt;object,string&gt;() { {0,"Nein" },{ 1, "Ja"} }} )), oder eine String-Array, bei dem Value und Anzeigename den selben Wert haben, bspw. (( new { source=new string[]{ "Ja","Nein","Vielleicht"}} ))
    /// en: Data source for the select options, which can be a query URL (see IncludeCombo, e.g., (( new { source="endpoint-id@query-id@view-id",
    /// valueField="VALUE", nameField="NAME", prependEmpty=true }) )), or a Dictionary with values for Value and Display Name, e.g., (( new { source=new Dictionary&lt;object,string&gt;() { {0,"No" },{ 1, "Yes"} }} )), or a String Array where Value and Display Name are the same, e.g., (( new { source=new string[]{ "Yes","No","Maybe"}} ))
    /// </param>
    /// <param name="defaultValue">
    /// de: Falls noch kein Wert im record-Datensatz vorliegt (null), kann so ein Wert für die Vorauswahl getroffen werden.
    /// en: If no value exists in the record, a default value can be set for the pre-selection.
    /// </param>
    /// <returns>
    /// de: Ein HTML-Select-Element für das Formular, dessen Optionen aus einer Abfrage oder Liste geladen werden.
    /// en: An HTML select element for the form, with options loaded from a query or list.
    /// </returns>
    public object ComboFor(
        object record,
        string name,
        object htmlAttributes = null,
        object source = null,
        object defaultValue = null)
    {
        object val = record.GetDefaultValueFromRecord(name, defaultValue);

        return _razor.RawString(
            HtmlBuilder.Create()
                .ComboFor(val, name, htmlAttributes, source)
                .BuildHtmlString()
            ); 
    }

    /// <summary>
    /// de: Erstellt HTML-Radiobuttons für ein Formular, deren Optionen aus einer Abfrage ODER einer Liste geladen werden.
    /// en: Creates HTML radio buttons for a form, with options loaded from a query OR a list.
    /// </summary>
    /// <param name="record">
    /// de: DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen.
    /// en: DataLinq record from which the default selected value will be retrieved. If the record is null, no default selection will be made.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements.
    /// en: The name (attribute) of the form element.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the form element, e.g., ((new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="source">
    /// de: Datenquelle der Auswahloptionen, kann eine Abfrage-URL sein (siehe IncludeCombo, bspw. (( new { source="endpoint-id@query-id@view-id", valueField="VALUE",
    /// nameField="NAME", prependEmpty=true }) )), oder ein Dictionary mit Werten für Value und Anzeigenname, bspw. (( new { source=new Dictionary&lt;object,string&gt;() { {0,"Nein" },{ 1, "Ja"} }} )), oder eine String-Array, bei dem Value und Anzeigename den selben Wert haben, bspw. (( new { source=new string[]{ "Ja","Nein","Vielleicht"}} ))
    /// en: Data source for the radio button options, which can be a query URL (see IncludeCombo, e.g., (( new { source="endpoint-id@query-id@view-id", valueField="VALUE",
    /// nameField="NAME", prependEmpty=true }) )), or a Dictionary with values for Value and Display Name, e.g., (( new { source=new Dictionary&lt;object,string&gt;() { {0,"No" },{ 1, "Yes"} }} )), or a String Array where Value and Display Name are the same, e.g., (( new { source=new string[]{ "Yes","No","Maybe"}} ))
    /// </param>
    /// <param name="defaultValue">
    /// de: Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.
    /// en: If no value exists in the record (null or empty string), a default value can be set for the pre-selection.
    /// </param>
    /// <returns>
    /// de: HTML-Radiobuttons für das Formular, deren Optionen aus einer Abfrage oder Liste geladen werden.
    /// en: HTML radio buttons for the form, with options loaded from a query or list.
    /// </returns>
    public object RadioFor(
        object record,
        string name,
        object htmlAttributes = null,
        object source = null,
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        if (source != null && source.GetType().GetProperty("source") != null)
        {
            var sourceProperty = source.GetType().GetProperty("source");

            if (sourceProperty.PropertyType == typeof(string[]))
            {
                return _razor.RawString(
                    HtmlBuilder.Create()
                        .AppendDiv(d =>
                        {
                            d.AddAttributes(htmlAttributes);
                            foreach (var optionValue in (string[])sourceProperty.GetValue(source))
                            {
                                d.AppendInput(i =>
                                {
                                    i.AddAttribute("type", "radio");
                                    i.AddAttribute("name", name);
                                    i.AddAttribute("value", optionValue);
                                    if (optionValue == val.ToString())
                                        i.AddAttribute("checked", "checked");
                                    i.Content(optionValue);
                                });
                            }
                        }).BuildHtmlString()
                    );
            }
            else if (sourceProperty.PropertyType == typeof(Dictionary<object, string>))
            {
                return _razor.RawString(
                   HtmlBuilder.Create()
                       .AppendDiv(d =>
                       {
                           d.AddAttributes(htmlAttributes);
                           foreach (var kvp in (Dictionary<object, string>)sourceProperty.GetValue(source))
                           {
                               d.AppendInput(i =>
                               {
                                   i.AddAttribute("type", "radio");
                                   i.AddAttribute("name", name);
                                   i.AddAttribute("value", kvp.Key.ToString());
                                   if (kvp.Key.ToString() == val.ToString())
                                       i.AddAttribute("checked", "checked");
                                   i.Content(kvp.Value);
                               });
                           }
                       }).BuildHtmlString()
                   );
            }
            else if (sourceProperty.PropertyType == typeof(string))
            {
                var valueFieldProperty = source.GetType().GetProperty("valueField");
                var nameFieldProperty = source.GetType().GetProperty("nameField");

                if (valueFieldProperty.PropertyType == typeof(string) && nameFieldProperty.PropertyType == typeof(string))
                {
                    if (valueFieldProperty == null || nameFieldProperty == null)
                    {
                        valueFieldProperty.SetValue(source, "VALUE");
                        nameFieldProperty.SetValue(source, "NAME");
                    }

                    return _razor.RawString(
                        HtmlBuilder.Create()
                            .AppendDiv(d =>
                            {
                                d.AddClass("datalinq-include-radio");
                                d.AddAttributes(htmlAttributes);
                                d.AddAttribute("data-name", name);
                                d.AddAttribute("data-url", sourceProperty.GetValue(source).ToString());
                                d.AddAttribute("data-valuefield", valueFieldProperty.GetValue(source).ToString());
                                d.AddAttribute("data-namefield", nameFieldProperty.GetValue(source).ToString());
                                d.AddAttribute("data-defaultvalu", val == null ? "" : val.ToString());
                            }).BuildHtmlString()
                        );
                }
                else
                {
                    throw new ArgumentException("valueField and nameField have to be typeof(string)");
                }
            }
        }
        else
        {
            return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendDiv(d =>
                    {
                        d.AddAttributes(htmlAttributes);
                    }).BuildHtmlString()
                );
        }

        throw new ArgumentException("Invalid or unsupported source object passed to RadioFor.");
    }

    /// <summary>
    /// de: Erstellt ein HTML-Textfeld für ein Formular.
    /// en: Creates an HTML text field for a form.
    /// </summary>
    /// <param name="record">
    /// de: DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen.
    /// en: DataLinq record from which the default selected value will be retrieved. If the record is null, no default selection will be made.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements.
    /// en: The name (attribute) of the form element.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element; auch reguläre Ausdrücke zum Überprüfen sind möglich., bspw. ((new { style="width:300px" @class="meine-klasse",
    /// required="required", pattern="[A-Za-z]{3}" }))
    /// en: An anonymous object with HTML attributes for the form element; regular expressions for validation are also possible, e.g., ((new { style="width:300px" @class="my-class",
    /// required="required", pattern="[A-Za-z]{3}" }))
    /// </param>
    /// <param name="defaultValue">
    /// de: Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.
    /// en: If no value exists in the record (null or empty string), a default value can be set for the pre-selection.
    /// </param>
    /// <param name="dataType">
    /// de: Art des Textfeldes als Enumeration, hier sind Text-(Text), Datum-(Date) oder Datum+Uhrzeit(DateTime) als Typ möglich, bspw. ((DataType.DateTime)). Bei Datumsfeldern muss das voreingestellte Datum folgendes Format haben: DD.MM.YYYY HH:mm, bspw. (("08.11.2017 09:32"))
    /// en: Type of the text field as an enumeration. Options include Text (Text), Date (Date), or Date+Time (DateTime), e.g., ((DataType.DateTime)). For date fields, the default date must be in the format DD.MM.YYYY HH:mm, e.g., (("08.11.2017 09:32")).
    /// </param>
    /// <returns>
    /// de: Ein HTML-Textfeld für ein Formular.
    /// en: An HTML text field for a form.
    /// </returns>
    public object TextFor(
        object record,
        string name,
        object htmlAttributes = null,
        object defaultValue = null,
        DataType dataType = DataType.Text)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        if (dataType == DataType.DateTime || dataType == DataType.Date)
        {
            return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendInput(i =>
                    {
                        i.AddAttribute("type", "hidden");
                        i.AddAttribute("name", name);
                        i.AddAttribute("value", val == null ? "NULL" : val.ToString());
                    }).AppendInput(i =>
                    {
                        i.AddAttributes(htmlAttributes);
                        i.AddAttribute("type", "text");
                        i.AddAttribute("name", name+ "_helper");
                        i.AddAttribute("data-datatype", dataType.ToString());
                        i.AddAttribute("value", val == null ? "" : val.ToString());
                    }).BuildHtmlString()
                );
        } else
        {
            return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendInput(i =>
                    {
                        i.AddAttributes(htmlAttributes);
                        i.AddAttribute("type", "text");
                        i.AddAttribute("name", name);
                        i.AddAttribute("data-datatype", dataType.ToString());
                        i.AddAttribute("value", val == null ? "" : val.ToString());
                    }).BuildHtmlString()
                );
        }
    }

    /// <summary>
    /// de: Erstellt ein HTML-Checkbox für ein Formular.
    /// en: Creates an HTML checkbox for a form.
    /// </summary>
    /// <param name="record">
    /// de: DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen.
    /// en: DataLinq record from which the default selected value will be retrieved. If the record is null, no default selection will be made.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements."
    /// en: The name (attribute) of the form element."
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the form element, e.g., ((new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="defaultValue">
    /// de: Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.
    /// en: If no value exists in the record (null or empty string), a default value can be set for the pre-selection.
    /// </param>
    /// <returns>
    /// de: Ein HTML-Checkbox für ein Formular.
    /// en: An HTML checkbox for a form.
    /// </returns>
    public object CheckboxFor(
        object record,
        string name,
        object htmlAttributes = null,
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);
        string guid = Guid.NewGuid().ToString();

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendInput(i =>
                {
                    i.AddAttribute("type", "hidden");
                    i.AddAttribute("name", name);
                    i.AddAttribute("value", Convert.ToBoolean(val) == true ? "true" : "false");
                    i.AddAttribute("id", guid);
                }).AppendInput(i =>
                {
                    i.AddAttributes(htmlAttributes);
                    i.AddAttribute("type", "checkbox");
                    i.AddAttribute("data-guid", guid);
                    i.AddAttribute("value", "True");
                    if ((Convert.ToBoolean(val) == true))
                        i.AddAttribute("checked", "checked");
                    i.AddAttribute("onclick", "$(\"#\" + $(this).data(\"guid\")).val(this.checked)");
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt eine HTML-Textarea (Textfeld für mehrzeilige Eingaben) für ein Formular.
    /// en: Creates an HTML textarea (text field for multiline input) for a form.
    /// </summary>
    /// <param name="record">
    /// de: DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen.
    /// en: DataLinq record from which the default selected value will be retrieved. If the record is null, no default selection will be made.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements.
    /// en: The name (attribute) of the form element.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the form element, e.g., ((new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="defaultValue">
    /// de: Falls noch kein Wert im record-Datensatz vorliegt (null), kann so ein Wert für die Vorauswahl getroffen werden.
    /// en: If no value exists in the record (null), a default value can be set for the pre-selection.
    /// </param>
    /// <returns>
    /// de: Ein HTML-Textarea (Textfeld für mehrzeilige Eingaben) für ein Formular.
    /// en: An HTML textarea (text field for multiline input) for a form.
    /// </returns>
    public object TextboxFor(
        object record,
        string name,
        object htmlAttributes = null,
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendTextarea(t =>
                {
                    t.AddAttributes(htmlAttributes);
                    t.AddAttribute("name", name);
                    t.Content(val == null ? "" : val.ToString());
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt ein verstecktes HTML-Feld für ein Formular.
    /// en: Creates a hidden HTML field for a form.
    /// </summary>
    /// <param name="record">
    /// de: DataLinq-Datensatz, aus dem der vorausgewählte Wert geholt wird, falls der Datensatz leer (null) ist, wird keine Vorauswahl getroffen.
    /// en: DataLinq record from which the default selected value will be retrieved. If the record is null, no default selection will be made.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements.
    /// en: The name (attribute) of the form element.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the form element, e.g., ((new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="defaultValue">
    /// de: Falls noch kein Wert im record-Datensatz vorliegt (null oder leeren String), kann so ein Wert für die Vorauswahl getroffen werden.
    /// en: If no value exists in the record (null or empty string), a default value can be set for the pre-selection.
    /// </param>
    /// <returns>
    /// de: Ein verstecktes HTML-Feld für ein Formular.
    /// en: A hidden HTML field for a form.
    /// </returns>
    public object HiddenFor(
        object record,
        string name,
        object htmlAttributes = null,
        object defaultValue = null)
    {
        object val = GetDefaultValueFromRecord(record, name, defaultValue);

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendInput(i =>
                {
                    i.AddAttributes(htmlAttributes);
                    i.AddAttribute("type", "hidden");
                    i.AddAttribute("name", name);
                    i.AddAttribute("value", val == null ? "" : val.ToString());
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Erstellt ein Label für eine Formular-Eingabe-Element.
    /// en: Creates a label for a form input element.
    /// </summary>
    /// <param name="label">
    /// de: Label/Text, der angezeigt werden soll.
    /// en: The label/text to be displayed.
    /// </param>
    /// <param name="name">
    /// de: Name (Attribut) des Formular-Elements.
    /// en: The name (attribute) of the form element.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das Formular-Element, bspw. ((new { style="width:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the form element, e.g., ((new { style="width:300px" @class="my-class" }))
    /// </param>
    /// <param name="newLine">
    /// de: Soll nach dem Label in eine neue Zeile gewechselt werden? (Standardwert: true)
    /// en: Should a new line be added after the label? (Default: true)
    /// </param>
    /// <returns>
    /// de: Ein Label für das Formular-Eingabe-Element.
    /// en: A label for the form input element.
    /// </returns>
    public object LabelFor(
        string label,
        string name = "",
        object htmlAttributes = null,
        bool newLine = true)
    {
        if (newLine)
        {
            return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendBreak()
                    .AppendLabel(l =>
                    {
                        l.AddAttributes(htmlAttributes);
                        l.AddAttribute("for", name);
                        l.Content(label);
                    }).AppendBreak().BuildHtmlString()
                );
        } else
        {
            return _razor.RawString(
                HtmlBuilder.Create()
                    .AppendLabel(l =>
                    {
                        l.AddAttributes(htmlAttributes);
                        l.AddAttribute("for", name);
                        l.Content(label);
                    }).BuildHtmlString()
                );
        }
    }

    #endregion

    #region Statistics & Charts

    /// <summary>
    /// de: Erstellt ein HTML DIV-Element, das die Anzahl der Datensätze anzeigt.
    /// en: Creates an HTML DIV element that displays the number of records.
    /// </summary>
    /// <param name="records">
    /// de: DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.
    /// en: DataLinq records to be counted. These may also be restricted by a C# LINQ condition."
    /// </param>
    /// <param name="label">
    /// de: Der Text, der über der Zahl anzeigt werden soll, bspw. "Anzahl"
    /// en: The text to be displayed above the number, e.g., "Count".
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für das DIV-Element, bspw. ((new { style="height:300px" @class="meine-klasse" }))
    /// en: An anonymous object with HTML attributes for the DIV element, e.g., ((new { style="height:300px" @class="my-class" }))
    /// </param>
    /// <returns>
    /// de: Ein HTML DIV-Element, das die Anzahl der Datensätze anzeigt.
    /// en: An HTML DIV element that displays the number of records.
    /// </returns>
    public object StatisticsCount(
        IDictionary<string, object>[] records,
        string label = "",
        object htmlAttributes = null)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                    d.AddClass("datalinq-statistics");
                    d.AddAttributes(htmlAttributes);
                    if (!String.IsNullOrWhiteSpace(label))
                    {
                        string labelStrong = $"<strong>{ToHtml(label)}</strong>";
                        string content = (records != null) ? records.Length.ToString() : "0";
                        d.Content(labelStrong + content);
                    }
                    else
                    {
                        d.Content(records != null ? records.Length.ToString() : "0");
                    }
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Gruppiert Datensätze auf Basis eines Feldes und gibt dazugehörige Gruppengröße (d.h. Anzahl der Datensätze in der Gruppe) in einer JavaScript-Variablen im JSON-Format aus.
    /// en: Groups records based on a field and outputs the corresponding group size (i.e., number of records in the group) in a JavaScript variable in JSON format.
    /// </summary>
    /// <param name="records">
    /// de: DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.
    /// en: DataLinq records to be counted. These may also be restricted by a C# LINQ condition.
    /// </param>
    /// <param name="jsVariableName">
    /// de: Name des auszugebenden JavaScript-Objektes, mit folgendem Aufbau: (([ {name: Name / Kategorie / etc, value: Wert},
    /// {name: Laubbäume, value: 2},
    /// {name: Nadelbäume, value: 5}, ]))
    /// en: The name of the JavaScript object to output, with the following structure: (([ {name: Name / Category / etc, value: Value},
    /// {name: Deciduous Trees, value: 2},
    /// {name: Coniferous Trees, value: 5}, ]))
    /// </param>
    /// <param name="field">
    /// de: Feldname im DataLinq-Objekt, nach dem gruppiert werden soll.
    /// en: Field name in the DataLinq object to group by.
    /// </param>
    /// <param name="orderField">
    /// de: Art, nach der das Objekt sortiert werden soll. Es kann auf- und absteigend nach dem Gruppennamen (OrderField.NameAsc, OrderField.NameDesc) oder nach Gruppengröße (OrderField.ValueAsc, OrderField.ValueDesc) sortiert werden.
    /// en: The way in which the object should be sorted. It can be sorted ascending or descending by group name (OrderField.NameAsc, OrderField.NameDesc) or by group size (OrderField.ValueAsc, OrderField.ValueDesc).
    /// </param>
    /// <returns>
    /// de: Gibt eine JavaScript-Variable im JSON-Format zurück, die die gruppierten Datensätze und deren Größen enthält.
    /// en: Returns a JavaScript variable in JSON format that contains the grouped records and their sizes.
    /// </returns>
    public object StatisticsGroupBy(
        IDictionary<string, object>[] records,
                string jsVariableName,
        string field,
        OrderField orderField = OrderField.Non)
    {
        return ToParseJson(Stat_GroupBy(records, field, orderField), jsVariableName);
    }

    /// <summary>
    /// de: Gruppiert Datensätze auf Basis eines Feldes und erzeugt eine JavaScript-Variable im JSON-Format für Diagramme.
    /// en: Groups records based on a field and generates a JavaScript variable in JSON format for charts.
    /// </summary>
    /// <param name="records">
    /// de: DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.
    /// en: DataLinq records to be counted. These may also be restricted by a C# LINQ condition.
    /// </param>
    /// <param name="jsVariableName">
    /// de: Name des auszugebenden JavaScript-Objektes, mit folgendem Aufbau: (([ {name: Name / Kategorie / etc, value: Wert},
    /// {name: Laubbäume, value: 2},
    /// {name: Nadelbäume, value: 5}, ]))
    /// en: The name of the JavaScript object to output, with the following structure: (([ {name: Name / Category / etc, value: Value},
    /// {name: Deciduous Trees, value: 2},
    /// {name: Coniferous Trees, value: 5}, ]))
    /// </param>
    /// <param name="categoryField">
    /// de: Feldname im DataLinq-Objekt, das die Kategorie enthält.
    /// en: Field name in the DataLinq object that contains the category.
    /// </param>
    /// <param name="valueField">
    /// de: Feldname im DataLinq-Objekt, das die Werte enthält.
    /// en: Field name in the DataLinq object that contains the values.
    /// </param>
    /// <param name="statType">
    /// de: Art, nach der das Wert-Feld abgeleitet werden soll. ((""StatType.Sum", "StatType.Min", "StatType.Max", "StatType.Mean",  "StatType.Count"))
    /// en: The type by which the value field should be derived. (("StatType.Sum", "StatType.Min", "StatType.Max", "StatType.Mean",  "StatType.Count"))
    /// </param>
    /// <param name="orderField">
    /// de: Art, nach der das Objekt sortiert werden soll. Es kann auf- und absteigend nach dem Gruppennamen (("OrderField.NameAsc", "OrderField.NameDesc")) oder nach Gruppengröße (("OrderField.ValueAsc", "OrderField.ValueDesc")) sortiert werden.
    /// en: The way in which the object should be sorted. It can be sorted ascending or descending by group name (("OrderField.NameAsc", "OrderField.NameDesc")) or by group size (("OrderField.ValueAsc", "OrderField.ValueDesc"))
    /// </param>
    /// <returns>
    /// de: Gibt eine JavaScript-Variable im JSON-Format zurück, die die gruppierten und abgeleiteten Werte enthält, geeignet für Diagramme.
    /// en: Returns a JavaScript variable in JSON format that contains the grouped and derived values, suitable for charts.
    /// </returns>
    public object StatisticsGroupByDerived(
        IDictionary<string, object>[] records,
        string jsVariableName,
        string categoryField,
        string valueField,
        StatType statType = StatType.Sum,
        OrderField orderField = OrderField.Non)
    {
        return ToParseJson(Stat_GroupByDerived(records, categoryField, valueField, statType, orderField), jsVariableName);
    }

    /// <summary>
    /// de: Gruppiert Datensätze nach Zeitintervallen und gibt dazugehörige Gruppengröße (d.h. Anzahl der Datensätze in der Gruppe) in einer JavaScript Variable im JSON-Format aus. Zusätzlich kann nach einem weiteren Feld gruppiert werden.
    /// en: Groups records by time intervals and outputs the group size (i.e., the number of records in the group) in a JavaScript variable in JSON format. Additionally, grouping can be done by another field.
    /// </summary>
    /// <param name="records">
    /// de: Datalinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.
    /// en: Datalinq records to be counted. These can also be restricted by a C# LINQ condition.
    /// </param>
    /// <param name="jsVariableName">
    /// de: Name des auszugebenden Javascript-Objektes.
    /// en: Name of the JavaScript object to be output.
    /// </param>
    /// <param name="datetimeField">
    /// de: Feldname im DataLinq-Objekt, das den Zeitstempel enthält (DateTime).
    /// en: Field name in the DataLinq object that contains the timestamp (DateTime).
    /// </param>
    /// <param name="categoryField">
    /// de: Feldname im DataLinq-Objekt, nach dem gruppiert werden soll.
    /// en: Field name in the DataLinq object to group by.
    /// </param>
    /// <param name="secondsInterval">
    /// de: Zeitintervall in Sekunden, nach dem gruppiert werden soll. Wenn nichts angegeben wird, wird die Zeitspanne der Datensätze (frühestes bis spätestes vorkommendes Datum) berechnet und daraus eine passende Gruppierung erstellt.
    /// en: Time interval in seconds to group by. If not provided, the time span of the records (earliest to latest date) is calculated and a suitable grouping is created.
    /// </param>
    /// <param name="fillMissingDataValue">
    /// de: Falls Lücken in den Datensätzen mit einem Wert gefüllt werden sollen, kann dieser (Integer) hier angegeben werden.
    /// en: If gaps in the records should be filled with a value, this (integer) can be specified here.
    /// </param>
    /// <param name="orderField">
    /// de: Art, nach der das Objekt sortiert werden soll. Es kann auf- und absteigend nach dem Gruppennamen (("OrderField.NameAsc", "OrderField.NameDesc")) oder nach Gruppengröße (("OrderField.ValueAsc", "OrderField.ValueDesc")) sortiert werden.
    /// en: The type of sorting to be applied to the object. It can be sorted ascending or descending by group name (("OrderField.NameAsc", "OrderField.NameDesc")) or by group size (("OrderField.ValueAsc", "OrderField.ValueDesc"))
    /// </param>
    /// <returns>
    /// de: Gibt eine JavaScript-Variable im JSON-Format zurück, die die gruppierten Datensätze und ihre Anzahl enthält.
    /// en: Returns a JavaScript variable in JSON format containing the grouped records and their count.
    /// </returns>
    public object StatisticsGroupByTime(
        IDictionary<string, object>[] records,
        string jsVariableName,
        string datetimeField,
        string categoryField = "",
        int secondsInterval = 0,
        object fillMissingDataValue = null,
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

    /// <summary>
    /// de: Gibt eine Zeitreihe in einer JavaScript Variable im JSON-Format aus. Zusätzlich kann nach einem weiteren Feld gruppiert werden.
    /// en: Outputs a time series in a JavaScript variable in JSON format. Additionally, it can be grouped by another field.
    /// </summary>
    /// <param name="records">
    /// de: DataLinq-Datensätze, die gezählt werden sollen. Diese könnten auch durch eine C#-LINQ-Bedingung eingeschränkt sein.
    /// en: DataLinq records to be counted, which could also be filtered by a C# LINQ condition.
    /// </param>
    /// <param name="jsVariableName">
    /// de: Name des auszugebenden Javascript-Objektes.
    /// en: Name of the JavaScript object to be output.
    /// </param>
    /// <param name="datetimeField">
    /// de: Feldname im DataLinq-Objekt, dass den Zeitstempel enthält (DateTime).
    /// en: Field name in the DataLinq object that contains the timestamp (DateTime).
    /// </param>
    /// <param name="valueField">
    /// de: Feldname im DataLinq-Objekt, das den Wert enthält (Zahl).
    /// en: Field name in the DataLinq object that contains the value (number).
    /// </param>
    /// <param name="categoryField">
    /// de: Feldname im DataLinq-Objekt, nach dem gruppiert werden soll.
    /// en: Field name in the DataLinq object by which grouping should be done.
    /// </param>
    /// <returns>
    /// de: Gibt eine JavaScript-Variable im JSON-Format zurück, die die Zeitreihe und die gruppierten Daten enthält.
    /// en: Returns a JavaScript variable in JSON format containing the time series and the grouped data.
    /// </returns>
    public object StatisticsTime(
        IDictionary<string, object>[] records,
        string jsVariableName,
        string datetimeField,
        string valueField,
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

        sb.Append($"var {name}=");
        sb.Append("jQuery.parseJSON('");
        sb.Append(JsonConvert.SerializeObject(obj).Replace("\\", "\\\\"));
        sb.Append("');");

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendJavaScriptBlock(sb.ToString())
                .BuildHtmlString());
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

    /// <summary>
    /// de: Erstellt ein Diagramm mit unterschiedlichen Typen, das auf einem JSON-Objekt im Javascript-Code basiert.
    /// en: Creates a chart with different types based on a JSON object in the JavaScript code.
    /// </summary>
    /// <param name="chartType">
    /// de: Diagrammtyp als Enumeration, hier sind Balken-(Bar), Torten-(Pie), Doughnut oder Linien(Line) als Typ möglich, bspw. ChartType.Bar
    /// en: Chart type as an enumeration, here bar (Bar), pie (Pie), doughnut, or line (Line) are possible types, e.g. ChartType.Bar
    /// </param>
    /// <param name="jsValueVariable">
    /// de: Name der Javascript-Variable, die das JSON-Datenobjekt enthält. Das JSON-Objekt könnte bspw. von der Methode "StatisticsGroupBy" oder "StatisticsGroupByTime" stammen und muss folgende Struktur haben:
    /// (([ {name: "Name/Kategorie/etc", value: "Wert"}, {name: "Laubbäume", value: "2"}, {name: "Nadelbäume", value: "5"} ]))
    /// en: Name of the JavaScript variable containing the JSON data object. The JSON object could come from methods like "StatisticsGroupBy" or "StatisticsGroupByTime" and must have the following structure:
    /// (([ {name: "Name/category/etc", value: "value"}, {name: "Deciduous trees", value: "2"}, {name: "Coniferous trees", value: "5"} ]))
    /// </param>
    /// <param name="label">
    /// de: Beschriftung des Diagramms.
    /// en: Label for the chart.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für die Schaltfläche, bspw. ((new { style="width:300px" @class="meine-klasse" } ))
    /// en: An anonymous object with HTML attributes for the button, e.g. ((new { style="width:300px" @class="my-class" } ))
    /// </param>
    /// <param name="chartColorRGB">
    /// de: Farben, die im Diagramm verwendet werden sollen als R,G,B-String, bspw. ((new string[] {""0,155,20"", ""160,0,25""} ))
    /// en: Colors to be used in the chart as R,G,B string, e.g. ((new string[] {""0,155,20"", ""160,0,25""} ))
    /// </param>
    /// <param name="jsDatasetVariable">
    /// de: Name der Javascript-Variable, die als JSON-Objekt Einstellungen zur Darstellung der Datensätze enthält. Je nach Diagrammtyp sind unterschiedliche Einstellungen möglich, siehe dazu http://www.chartjs.org/docs/latest/charts/ => Charttypen => Dataset Properties.
    /// en: Name of the JavaScript variable containing JSON settings for dataset representation. Depending on the chart type, different settings are possible, see http://www.chartjs.org/docs/latest/charts/ => Chart types => Dataset Properties.
    /// </param>
    /// <returns>
    /// de: Gibt HTML-Code für das Diagramm zurück.
    /// en: Returns HTML code for the chart.
    /// </returns>
    public object Chart(
        ChartType chartType,
        string jsValueVariable,
        string label = "",
        object htmlAttributes = null,
        string[] chartColorRGB = null,
        string jsDatasetVariable = ""
        //,object chartOptions = null
        )
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                    d.AddClass("datalinq-chart");
                    d.AddAttributes(htmlAttributes);
                    d.AddAttribute("data-chart-label", label);
                    d.AddAttribute("data-chart-data", jsValueVariable);
                    d.AddAttribute("data-chart-type", chartType.ToString());
                    d.AddAttribute("data-chart-dataset", jsDatasetVariable);
                    chartColorRGB = (chartColorRGB == null ? new string[] { } : chartColorRGB);
                    d.AddAttribute("data-chart-color", String.Join("|", chartColorRGB).Replace(" ", ""));
                }).BuildHtmlString()
            );
    }
    #endregion

    /// <summary>
    /// de: Bei Hover über einen Datensatz wird ein Symbol zum Kopieren angezeigt.
    /// en: A copy icon is displayed when hovering over a data entry.
    /// </summary>
    /// <param name="copyValue">
    /// de: Wert, der kopiert werden soll.
    /// en: Value to be copied.
    /// </param>
    /// <param name="copyBaseText">
    /// de: Text, zu sehen sein soll, kann auch Leerstring sein und damit nichts beinhalten. Wenn kein Wert übergeben wird, wird der Wert des CopyValues herangezogen.
    /// en: Text to be displayed, can also be an empty string and thus display nothing. If no value is passed, the value of copyValue is used.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für diesen Button ((z.B.: new { style="width:300px" @class="meine-klasse" } ))
    /// en: An anonymous object with HTML attributes for this button (e.g. new { style="width:300px" @class="my-class" } )
    /// </param>
    /// <returns>
    /// de: Gibt HTML-Code für einen Button zum Kopieren zurück.
    /// en: Returns HTML code for a copy button.
    /// </returns>
    public object CopyButton(
        object copyValue,
        object copyBaseText = null,
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

        string innerHTML = baseText + _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d1 =>
                {
                    d1.AddClass("datalinq-copy-button");
                    d1.AddAttributes(htmlAttributes);
                    d1.AddAttribute("data-copy-value", copyValue.ToString());
                }).BuildHtmlString()
            );

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                    d.AddClass("datalinq-copy-helper");
                    d.Content(innerHTML);
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Liefert einen Skalarwert einer Abfrage zurück und gibt das Ergebnis als Inhalt eines HTML-Elementes aus.
    /// en: Returns a scalar value from a query and outputs the result as the content of an HTML element.
    /// </summary>
    /// <param name="htmlAttributes">
    /// de: Ein anonymes Objekt mit HTML-Attributen für dieses Element (z.B.: new { style="width:300px" @class="meine-klasse" }).
    /// en: An anonymous object with HTML attributes for this element (e.g., new { style="width:300px" @class="my-class" }).
    /// </param>
    /// <param name="source">
    /// de: Datenquelle der Abfrage, eine Abfrage-URL die einen Wert liefert bspw. (( new { source="endpoint-id@query-id?id=2", nameField="NAME" })
    /// en: Data source for the query, a query URL that returns a value, e.g., (( new { source="endpoint-id@query-id?id=2", nameField="NAME" }) 
    /// </param>
    /// <param name="htmlTag">
    /// de: Art des HTML-Elements, dass erzeugt wird.
    /// en: Type of the HTML element to be created.
    /// </param>
    /// <param name="defaultValue">
    /// de: Text, der angezeigt werden soll, falls die Abfrage kein Ergebnis liefert.
    /// en: Text to display if the query does not return any result.
    /// </param>
    /// <returns>
    /// de: Gibt den HTML-Code für das Element zurück, das den Skalarwert enthält.
    /// en: Returns the HTML code for the element containing the scalar value.
    /// </returns>
    public object ExecuteScalar(
        object htmlAttributes = null,
        object source = null,
        string htmlTag = "span",
        string defaultValue = null)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendHtmlElement(e =>
                {
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
                                            e.AddAttribute(htmlAttribute.Name.ToLower(), (htmlAttribute.GetValue(htmlAttributes) != null ? htmlAttribute.GetValue(htmlAttributes).ToString() : "") + " datalinq-include-scalar");
                                        }
                                        else
                                        {
                                            if (htmlAttribute.GetValue(htmlAttributes) != null)
                                            {
                                                e.AddAttribute(htmlAttribute.Name, htmlAttribute.GetValue(htmlAttributes).ToString());
                                            }
                                        }
                                    }
                                }

                                if (!e.ToString().Contains("class="))
                                {
                                    e.AddClass("datalinq-include-scalar");
                                }

                                bool prependEmpty = Convert.ToBoolean(GetDefaultValueFromRecord(ToDictionary(source), "prependEmpty", false));
                                e.AddAttribute("data-url", sourceProperty.GetValue(source).ToString());
                                e.AddAttribute("data-namefield", nameFieldProperty.GetValue(source).ToString());
                                e.AddAttribute("data-defaultvalue", defaultValue.ToString().ToString());
                            }
                            else
                            {
                                throw new ArgumentException("valueField and nameField have to be typeof(string)");
                            }
                        }
                    }
                },htmlTag).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Kodiert die Werte eines Objektes, sodass sie URL-tauglich sind.
    /// en: Encodes the values of an object so that they are URL-safe.
    /// </summary>
    /// <param name="parameter">
    /// de: Das Objekt, dessen Werte URL-kodiert werden sollen.
    /// en: The object whose values should be URL encoded.
    /// </param>
    /// <returns>
    /// de: Gibt den URL-kodierten Wert des Objekts zurück.
    /// en: Returns the URL encoded value of the object.
    /// </returns>
    public object UrlEncode(object parameter)
    {
        if (parameter == null)
        {
            return _razor.RawString(String.Empty);
        }

        return _razor.RawString(HttpUtility.UrlEncode(parameter.ToString()));
    }

    /// <summary>
    /// de: Erstellt eine Schaltfläche, mit der alle DOM-Elemente mit der Klasse "responsive" sichtbar geschaltet werden.
    /// en: Creates a button that makes all DOM elements with the "responsive" class visible.
    /// </summary>
    /// <returns>
    /// de: Gibt eine Schaltfläche zurück, die beim Klicken alle DOM-Elemente mit der Klasse "responsive" sichtbar schaltet.
    /// en: Returns a button that, when clicked, makes all DOM elements with the "responsive" class visible.
    /// </returns>
    public object ResponsiveSwitcher()
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendButton(b =>
                {
                    b.AddClass("responsive-switch");
                    b.Content("Alles anzeigen");
                }).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Der Username des aktuell angemeldeten Benutzers.
    /// en: The username of the currently logged-in user.
    /// </summary>
    /// <returns>
    /// de: Gibt den Username des aktuell angemeldeten Benutzers zurück, falls verfügbar, ansonsten einen leeren String.
    /// en: Returns the username of the currently logged-in user, if available, otherwise an empty string.
    /// </returns>
    public string GetCurrentUsername()
        => _ui?.Username ?? "";

    /// <summary>
    /// de: Prüft, ob der aktuelle User Mitglied in der angegeben Rolle ist.
    /// en: Checks if the current user is a member of the specified role.
    /// </summary>
    /// <param name="roleName">
    /// de: Der Name der Rolle, die geprüft werden soll.
    /// en: The name of the role to be checked.
    /// </param>
    /// <returns>
    /// de: Gibt true zurück, wenn der Benutzer Mitglied der angegebenen Rolle ist, andernfalls false.
    /// en: Returns true if the user is a member of the specified role, otherwise false.
    /// </returns>
    public bool HasRole(
            [HelpDescription("Rollenname, der geprüft werdens soll")]
            string roleName
        ) => _ui?
             .Userroles?
             .Any(r => r.Equals(roleName, StringComparison.InvariantCultureIgnoreCase)) == true;

    //[HelpDescription("Gibt alle HTTP Request Header Namen zurück")]
    //public IEnumerable<string> GetRequestHeaders()
    //    => _httpContext?.Request?.Headers?.Keys ?? Enumerable.Empty<string>();

    /// <summary>
    /// de: Gibt den Wert eines HTTP Request Headers zurück.
    /// en: Returns the value of an HTTP request header.
    /// </summary>
    /// <param name="header">
    /// de: Der Name des Headers, dessen Wert zurückgegeben werden soll.
    /// en: The name of the header whose value is to be returned.
    /// </param>
    /// <returns>
    /// de: Gibt den Wert des angegebenen Headers zurück. Falls der Header nicht vorhanden ist, wird ein leerer String zurückgegeben.
    /// en: Returns the value of the specified header. If the header is not present, an empty string is returned.
    /// </returns>
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
            sb.Append($" {attributeName}='{attributeValue.Replace("\n", "<br/>").Replace("\r", "").Replace("'", "\"")}'");
        }
    }

    public object ToRawString(string str)
    {
        return _razor.RawString(str);
    }

    //public object ToHtmlEncoded(string str)
    //{
    //    return _razor switch
    //    {
    //        RazorEngineService => str,
    //        _ => HttpUtility.HtmlEncode(str)
    //    };
    //}

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
