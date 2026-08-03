using E.DataLinq.Web.Razor.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace System.Collections.Generic;

/// <summary>
/// de: Erweiterungsmethoden, um verschachtelte JSON-API-Antworten in Razor-Views einfach auszulesen.
/// de: Die Methoden können direkt auf Model.Records (alle Datensätze) oder auf einen einzelnen Datensatz
/// de: (z.B. Model.Records.FirstOrDefault() oder die Laufvariable einer foreach-Schleife) angewendet werden.
/// de: Adressiert wird immer über einen JSONPath-ähnlichen Pfad:
/// de: "owner.email" (Unterobjekt), "items[0].name" (Index), "items[*].name" (alle Elemente),
/// de: "headers['content-type']" (Schlüssel mit Sonderzeichen).
/// de: Beispiel: @Model.Records.JsonValue("items[0].owner.email")
/// de: Der interne Marker-Datensatz der JSON-API (IsJsonApiResponse) wird bei allen Model.Records-Methoden automatisch übersprungen.
/// en: Extension methods to easily read nested JSON API responses inside Razor views.
/// en: The methods can be used directly on Model.Records (all records) or on a single record
/// en: (e.g. Model.Records.FirstOrDefault() or the loop variable of a foreach loop).
/// en: Values are always addressed with a JSONPath-like path:
/// en: "owner.email" (sub object), "items[0].name" (index), "items[*].name" (all elements),
/// en: "headers['content-type']" (keys containing special characters).
/// en: Example: @Model.Records.JsonValue("items[0].owner.email")
/// en: The internal JSON API marker record (IsJsonApiResponse) is skipped automatically by all Model.Records methods.
/// </summary>
static public class JsonApiRecordExtensions
{
    #region Single Record

    /// <summary>
    /// de: Liefert den ersten Wert, der auf den angegebenen Pfad passt, aus einem einzelnen Datensatz.
    /// de: Existiert der Pfad nicht, wird defaultValue geliefert - es wird also nie eine Exception geworfen.
    /// de: Beispiel: @record.JsonValue("owner.email", "keine E-Mail")
    /// de: Tipp: Für typsichere Werte die generische Variante JsonValue&lt;T&gt; verwenden.
    /// en: Returns the first value matching the given path from a single record.
    /// en: If the path does not exist, defaultValue is returned - so this never throws.
    /// en: Example: @record.JsonValue("owner.email", "no email")
    /// en: Hint: use the generic overload JsonValue&lt;T&gt; to get a typed value.
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz, z.B. Model.Records.FirstOrDefault() oder die Laufvariable einer foreach-Schleife.
    /// en: The JSON API record, e.g. Model.Records.FirstOrDefault() or the loop variable of a foreach loop.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items[0].owner.email.
    /// en: The JSONPath-like path, e.g. items[0].owner.email.
    /// </param>
    /// <param name="defaultValue">
    /// de: Wert, der zurückgegeben wird, wenn der Pfad nicht existiert. Ohne Angabe wird null geliefert.
    /// en: Value returned if the path does not exist. If omitted, null is returned.
    /// </param>
    static public object JsonValue(this IDictionary<string, object> record, string path, object defaultValue = null)
    {
        if (record is null)
        {
            return defaultValue;
        }

        return JsonPathResolver.SelectFirst(record, path) ?? defaultValue;
    }

    /// <summary>
    /// de: Liefert den ersten Wert, der auf den angegebenen Pfad passt, konvertiert in den angegebenen Typ.
    /// de: Schlägt die Konvertierung fehl oder existiert der Pfad nicht, wird defaultValue geliefert.
    /// de: Beispiel: @record.JsonValue&lt;int&gt;("order.count", 0)
    /// de: Unterstützt werden alle einfachen Typen (int, double, bool, DateTime, ...) sowie Enums.
    /// en: Returns the first value matching the given path, converted to the given type.
    /// en: If the conversion fails or the path does not exist, defaultValue is returned.
    /// en: Example: @record.JsonValue&lt;int&gt;("order.count", 0)
    /// en: All simple types (int, double, bool, DateTime, ...) and enums are supported.
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. order.count.
    /// en: The JSONPath-like path, e.g. order.count.
    /// </param>
    /// <param name="defaultValue">
    /// de: Wert, der zurückgegeben wird, wenn der Pfad nicht existiert oder die Konvertierung fehlschlägt.
    /// en: Value returned if the path does not exist or the conversion fails.
    /// </param>
    static public T JsonValue<T>(this IDictionary<string, object> record, string path, T defaultValue = default)
    {
        var value = record.JsonValue(path);

        return ConvertValue(value, defaultValue);
    }

    /// <summary>
    /// de: Liefert alle Werte, die auf den angegebenen Pfad passen (z.B. bei Wildcards).
    /// de: Existiert der Pfad nicht, wird eine leere Liste geliefert.
    /// de: Beispiel: @foreach (var name in record.JsonValues("items[*].name")) { ... }
    /// en: Returns all values matching the given path (e.g. when using wildcards).
    /// en: If the path does not exist, an empty list is returned.
    /// en: Example: @foreach (var name in record.JsonValues("items[*].name")) { ... }
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items[*].name.
    /// en: The JSONPath-like path, e.g. items[*].name.
    /// </param>
    static public IEnumerable<object> JsonValues(this IDictionary<string, object> record, string path)
    {
        if (record is null)
        {
            return Enumerable.Empty<object>();
        }

        return JsonPathResolver.Select(record, path);
    }

    /// <summary>
    /// de: Liefert alle Werte, die auf den angegebenen Pfad passen, konvertiert in den angegebenen Typ.
    /// de: Nicht konvertierbare Werte werden als Standardwert des Typs geliefert.
    /// de: Beispiel: @foreach (var price in record.JsonValues&lt;double&gt;("items[*].price")) { ... }
    /// en: Returns all values matching the given path, converted to the given type.
    /// en: Values that cannot be converted are returned as the default value of the type.
    /// en: Example: @foreach (var price in record.JsonValues&lt;double&gt;("items[*].price")) { ... }
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items[*].price.
    /// en: The JSONPath-like path, e.g. items[*].price.
    /// </param>
    static public IEnumerable<T> JsonValues<T>(this IDictionary<string, object> record, string path)
        => record.JsonValues(path).Select(value => ConvertValue(value, default(T)));

    /// <summary>
    /// de: Liefert das erste Unterobjekt, das auf den angegebenen Pfad passt, als Dictionary.
    /// de: Auf dem Ergebnis können alle Json...-Methoden erneut aufgerufen werden (verschachtelte Objekte).
    /// de: Existiert der Pfad nicht, wird null geliefert - daher vorher am besten mit JsonHas prüfen.
    /// de: Beispiel: @{ var owner = record.JsonObject("items[0].owner"); } @owner.JsonValue("email")
    /// en: Returns the first sub object matching the given path as a dictionary.
    /// en: All Json... methods can be called again on the result (nested objects).
    /// en: If the path does not exist, null is returned - check with JsonHas beforehand.
    /// en: Example: @{ var owner = record.JsonObject("items[0].owner"); } @owner.JsonValue("email")
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. headers.
    /// en: The JSONPath-like path, e.g. headers.
    /// </param>
    static public IDictionary<string, object> JsonObject(this IDictionary<string, object> record, string path)
        => record.JsonObjects(path).FirstOrDefault();

    /// <summary>
    /// de: Liefert alle Unterobjekte, die auf den angegebenen Pfad passen, als Dictionaries. Ideal für foreach-Schleifen.
    /// de: Arrays werden dabei automatisch aufgelöst, "items" liefert also alle Elemente des Arrays.
    /// de: Beispiel: @foreach (var item in record.JsonObjects("items")) { @item.JsonValue("name") }
    /// en: Returns all sub objects matching the given path as dictionaries. Ideal for foreach loops.
    /// en: Arrays are flattened automatically, so "items" returns all elements of the array.
    /// en: Example: @foreach (var item in record.JsonObjects("items")) { @item.JsonValue("name") }
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items.
    /// en: The JSONPath-like path, e.g. items.
    /// </param>
    static public IEnumerable<IDictionary<string, object>> JsonObjects(this IDictionary<string, object> record, string path)
        => record.JsonValues(path)
                 .SelectMany(Flatten)
                 .OfType<IDictionary<string, object>>();

    /// <summary>
    /// de: Liefert die Schlüssel/Wert-Paare des Objekts am angegebenen Pfad. Ohne Pfad werden die Paare des Datensatzes selbst geliefert.
    /// de: Damit lassen sich unbekannte Strukturen ausgeben, ohne die Feldnamen zu kennen.
    /// de: Beispiel: @foreach (var entry in record.JsonEntries("headers")) { @entry.Key : @entry.Value }
    /// en: Returns the key/value pairs of the object at the given path. Without a path the pairs of the record itself are returned.
    /// en: Use this to render unknown structures without knowing the field names.
    /// en: Example: @foreach (var entry in record.JsonEntries("headers")) { @entry.Key : @entry.Value }
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. headers. Optional - ohne Pfad wird der Datensatz selbst verwendet.
    /// en: The JSONPath-like path, e.g. headers. Optional - without a path the record itself is used.
    /// </param>
    static public IEnumerable<KeyValuePair<string, object>> JsonEntries(this IDictionary<string, object> record, string path = null)
    {
        if (record is null)
        {
            return Enumerable.Empty<KeyValuePair<string, object>>();
        }

        if (String.IsNullOrWhiteSpace(path))
        {
            return record.Select(kvp => new KeyValuePair<string, object>(kvp.Key, JsonPathResolver.Unwrap(kvp.Value)));
        }

        return record.JsonObjects(path).SelectMany(o => o);
    }

    /// <summary>
    /// de: Prüft, ob der angegebene Pfad im Datensatz existiert.
    /// de: Ideal, um optionale Bereiche einer View nur bei vorhandenen Daten zu rendern.
    /// de: Beispiel: @if (record.JsonHas("owner.email")) { @record.JsonValue("owner.email") }
    /// en: Checks whether the given path exists in the record.
    /// en: Ideal to render optional parts of a view only if the data is available.
    /// en: Example: @if (record.JsonHas("owner.email")) { @record.JsonValue("owner.email") }
    /// </summary>
    /// <param name="record">
    /// de: Der JSON-API-Datensatz.
    /// en: The JSON API record.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. owner.email.
    /// en: The JSONPath-like path, e.g. owner.email.
    /// </param>
    static public bool JsonHas(this IDictionary<string, object> record, string path)
        => record is not null && JsonPathResolver.Select(record, path).Any(value => value is not null);

    #endregion

    #region Records (Model.Records)

    /// <summary>
    /// de: Liefert den ersten Wert, der auf den angegebenen Pfad passt. Der interne Marker-Datensatz des JSON-API wird dabei übersprungen.
    /// de: Das ist der schnellste Weg, um einen einzelnen Wert aus einer JSON-API-Antwort in die View zu schreiben.
    /// de: Existiert der Pfad nicht, wird defaultValue geliefert.
    /// de: Beispiel: @Model.Records.JsonValue("items[0].owner.email", "keine E-Mail")
    /// en: Returns the first value matching the given path. The internal JSON API marker record is skipped.
    /// en: This is the fastest way to write a single value of a JSON API response into the view.
    /// en: If the path does not exist, defaultValue is returned.
    /// en: Example: @Model.Records.JsonValue("items[0].owner.email", "no email")
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. headers['content-type'].
    /// en: The JSONPath-like path, e.g. headers['content-type'].
    /// </param>
    /// <param name="defaultValue">
    /// de: Wert, der zurückgegeben wird, wenn der Pfad nicht existiert. Ohne Angabe wird null geliefert.
    /// en: Value returned if the path does not exist. If omitted, null is returned.
    /// </param>
    static public object JsonValue(this IEnumerable<IDictionary<string, object>> records, string path, object defaultValue = null)
        => records.SkipMarker().FirstOrDefault().JsonValue(path, defaultValue);

    /// <summary>
    /// de: Liefert den ersten Wert, der auf den angegebenen Pfad passt, konvertiert in den angegebenen Typ.
    /// de: Schlägt die Konvertierung fehl oder existiert der Pfad nicht, wird defaultValue geliefert.
    /// de: Beispiel: @Model.Records.JsonValue&lt;int&gt;("paging.total", 0)
    /// en: Returns the first value matching the given path, converted to the given type.
    /// en: If the conversion fails or the path does not exist, defaultValue is returned.
    /// en: Example: @Model.Records.JsonValue&lt;int&gt;("paging.total", 0)
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. paging.total.
    /// en: The JSONPath-like path, e.g. paging.total.
    /// </param>
    /// <param name="defaultValue">
    /// de: Wert, der zurückgegeben wird, wenn der Pfad nicht existiert oder die Konvertierung fehlschlägt.
    /// en: Value returned if the path does not exist or the conversion fails.
    /// </param>
    static public T JsonValue<T>(this IEnumerable<IDictionary<string, object>> records, string path, T defaultValue = default)
        => records.SkipMarker().FirstOrDefault().JsonValue(path, defaultValue);

    /// <summary>
    /// de: Liefert alle Werte aller Datensätze, die auf den angegebenen Pfad passen.
    /// de: Mit Wildcards (items[*]) lassen sich damit ganze Spalten einer JSON-Antwort auslesen.
    /// de: Beispiel: @foreach (var name in Model.Records.JsonValues("items[*].name")) { &lt;li&gt;@name&lt;/li&gt; }
    /// en: Returns all values of all records matching the given path.
    /// en: With wildcards (items[*]) you can read whole columns of a JSON response.
    /// en: Example: @foreach (var name in Model.Records.JsonValues("items[*].name")) { &lt;li&gt;@name&lt;/li&gt; }
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items[*].name.
    /// en: The JSONPath-like path, e.g. items[*].name.
    /// </param>
    static public IEnumerable<object> JsonValues(this IEnumerable<IDictionary<string, object>> records, string path)
        => records.SkipMarker().SelectMany(record => record.JsonValues(path));

    /// <summary>
    /// de: Liefert alle Werte aller Datensätze, die auf den angegebenen Pfad passen, konvertiert in den angegebenen Typ.
    /// de: Nicht konvertierbare Werte werden als Standardwert des Typs geliefert - dadurch bleibt die Anzahl der Werte gleich.
    /// de: Beispiel: @Model.Records.JsonValues&lt;double&gt;("items[*].price").Sum()
    /// en: Returns all values of all records matching the given path, converted to the given type.
    /// en: Values that cannot be converted are returned as the default value of the type, so the number of values stays the same.
    /// en: Example: @Model.Records.JsonValues&lt;double&gt;("items[*].price").Sum()
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items[*].price.
    /// en: The JSONPath-like path, e.g. items[*].price.
    /// </param>
    static public IEnumerable<T> JsonValues<T>(this IEnumerable<IDictionary<string, object>> records, string path)
        => records.JsonValues(path).Select(value => ConvertValue(value, default(T)));

    /// <summary>
    /// de: Liefert das erste Unterobjekt, das auf den angegebenen Pfad passt, als Dictionary.
    /// de: Auf dem Ergebnis können alle Json...-Methoden erneut aufgerufen werden.
    /// de: Existiert der Pfad nicht, wird null geliefert.
    /// de: Beispiel: @{ var first = Model.Records.JsonObject("items[0]"); } @first.JsonValue("name")
    /// en: Returns the first sub object matching the given path as a dictionary.
    /// en: All Json... methods can be called again on the result.
    /// en: If the path does not exist, null is returned.
    /// en: Example: @{ var first = Model.Records.JsonObject("items[0]"); } @first.JsonValue("name")
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. headers.
    /// en: The JSONPath-like path, e.g. headers.
    /// </param>
    static public IDictionary<string, object> JsonObject(this IEnumerable<IDictionary<string, object>> records, string path)
        => records.JsonObjects(path).FirstOrDefault();

    /// <summary>
    /// de: Liefert alle Unterobjekte aller Datensätze, die auf den angegebenen Pfad passen, als Dictionaries.
    /// de: Die wichtigste Methode, um eine Liste aus einer JSON-Antwort zu rendern - Arrays werden automatisch aufgelöst.
    /// de: Beispiel: @foreach (var item in Model.Records.JsonObjects("items")) { @item.JsonValue("name") }
    /// de: Tipp: In Kombination mit DLH-Methoden kann das Ergebnis auch als Tabelle ausgegeben werden.
    /// en: Returns all sub objects of all records matching the given path as dictionaries.
    /// en: The most important method to render a list from a JSON response - arrays are flattened automatically.
    /// en: Example: @foreach (var item in Model.Records.JsonObjects("items")) { @item.JsonValue("name") }
    /// en: Hint: combined with DLH methods the result can also be rendered as a table.
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. items.
    /// en: The JSONPath-like path, e.g. items.
    /// </param>
    static public IEnumerable<IDictionary<string, object>> JsonObjects(this IEnumerable<IDictionary<string, object>> records, string path)
        => records.SkipMarker().SelectMany(record => record.JsonObjects(path));

    /// <summary>
    /// de: Liefert die Schlüssel/Wert-Paare des Objekts am angegebenen Pfad. Ohne Pfad werden die Paare des ersten Datensatzes geliefert.
    /// de: Damit lassen sich unbekannte Strukturen ausgeben, ohne die Feldnamen zu kennen.
    /// de: Beispiel: @foreach (var entry in Model.Records.JsonEntries("headers")) { @entry.Key : @entry.Value }
    /// en: Returns the key/value pairs of the object at the given path. Without a path the pairs of the first record are returned.
    /// en: Use this to render unknown structures without knowing the field names.
    /// en: Example: @foreach (var entry in Model.Records.JsonEntries("headers")) { @entry.Key : @entry.Value }
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. headers. Optional - ohne Pfad wird der erste Datensatz verwendet.
    /// en: The JSONPath-like path, e.g. headers. Optional - without a path the first record is used.
    /// </param>
    static public IEnumerable<KeyValuePair<string, object>> JsonEntries(this IEnumerable<IDictionary<string, object>> records, string path = null)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            return records.SkipMarker().FirstOrDefault().JsonEntries();
        }

        return records.JsonObjects(path).SelectMany(o => o);
    }

    /// <summary>
    /// de: Prüft, ob der angegebene Pfad in einem der Datensätze existiert.
    /// de: Ideal, um optionale Bereiche einer View nur bei vorhandenen Daten zu rendern.
    /// de: Beispiel: @if (Model.Records.JsonHas("error")) { @Model.Records.JsonValue("error.message") }
    /// en: Checks whether the given path exists in any of the records.
    /// en: Ideal to render optional parts of a view only if the data is available.
    /// en: Example: @if (Model.Records.JsonHas("error")) { @Model.Records.JsonValue("error.message") }
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. error.message.
    /// en: The JSONPath-like path, e.g. error.message.
    /// </param>
    static public bool JsonHas(this IEnumerable<IDictionary<string, object>> records, string path)
        => records.SkipMarker().Any(record => record.JsonHas(path));

    /// <summary>
    /// de: Liefert die Datensätze ohne den internen Marker-Datensatz des JSON-API.
    /// de: Immer dann verwenden, wenn direkt über Model.Records iteriert wird - sonst wird der Marker-Datensatz mit ausgegeben.
    /// de: Beispiel: @foreach (var record in Model.Records.JsonRecords()) { @record.JsonValue("name") }
    /// en: Returns the records without the internal JSON API marker record.
    /// en: Always use this when iterating over Model.Records directly, otherwise the marker record is rendered as well.
    /// en: Example: @foreach (var record in Model.Records.JsonRecords()) { @record.JsonValue("name") }
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    static public IEnumerable<IDictionary<string, object>> JsonRecords(this IEnumerable<IDictionary<string, object>> records)
        => records.SkipMarker();

    #endregion

    #region Helper

    private const string JsonApiMarkerKey = "IsJsonApiResponse";

    static private IEnumerable<IDictionary<string, object>> SkipMarker(this IEnumerable<IDictionary<string, object>> records)
    {
        if (records is null)
        {
            return Enumerable.Empty<IDictionary<string, object>>();
        }

        return records.Where(record => record is not null && !IsMarker(record));
    }

    static private bool IsMarker(IDictionary<string, object> record)
        => record.TryGetValue(JsonApiMarkerKey, out object value)
        && value is bool flag
        && flag;

    static private IEnumerable<object> Flatten(object value)
    {
        if (value is IDictionary<string, object> || value is string || value is null)
        {
            yield return value;
            yield break;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        yield return value;
    }

    static private T ConvertValue<T>(object value, T defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (value is T typed)
        {
            return typed;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (targetType.IsEnum)
            {
                return (T)Enum.Parse(targetType, value.ToString(), true);
            }

            return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    #endregion
}
