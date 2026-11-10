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
/// de: JSON aus Datenbank-Spalten: JSON-Texte (z.B. nvarchar-Spalte "json") werden nicht automatisch ausgewertet.
/// de: Mit record.JsonParse("json") wird der Text in ein Objekt umgewandelt, auf dem alle Json...-Methoden funktionieren.
/// de: Mit Model.Records.JsonParseColumn("json") erhält man Kopien aller Datensätze mit umgewandelter Spalte und allen anderen Spalten (z.B. id).
/// de: Schlüssel, die mit $ beginnen (z.B. $type), können direkt angesprochen werden: data.JsonValue("$type").
/// de: Für die Ausgabe in PDF-Reports wird JsonText empfohlen (formatiert Zahlen, Datum und Ja/Nein-Werte), für unbekannte Strukturen JsonFlatten.
/// de: Beispiel: @foreach (var record in Model.Records) { var data = record.JsonParse("json"); @data.JsonText("vertragsleistungKw", "N0") }
/// en: Extension methods to easily read nested JSON API responses inside Razor views.
/// en: The methods can be used directly on Model.Records (all records) or on a single record
/// en: (e.g. Model.Records.FirstOrDefault() or the loop variable of a foreach loop).
/// en: Values are always addressed with a JSONPath-like path:
/// en: "owner.email" (sub object), "items[0].name" (index), "items[*].name" (all elements),
/// en: "headers['content-type']" (keys containing special characters).
/// en: Example: @Model.Records.JsonValue("items[0].owner.email")
/// en: The internal JSON API marker record (IsJsonApiResponse) is skipped automatically by all Model.Records methods.
/// en: JSON from database columns: JSON texts (e.g. nvarchar column "json") are not parsed automatically.
/// en: Use record.JsonParse("json") to turn the text into an object on which all Json... methods work.
/// en: Model.Records.JsonParseColumn("json") returns copies of all records with the parsed column and all other columns (e.g. id).
/// en: Keys starting with $ (e.g. $type) can be addressed directly: data.JsonValue("$type").
/// en: JsonText is the recommended way to print values in PDF reports (formats numbers, dates and booleans), JsonFlatten renders unknown structures.
/// en: Example: @foreach (var record in Model.Records) { var data = record.JsonParse("json"); @data.JsonText("vertragsleistungKw", "N0") }
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

    /// <summary>
    /// de: Wandelt einen JSON-Text aus einer Datenbank-Spalte (z.B. nvarchar-Spalte "json") in ein Objekt um,
    /// de: auf dem alle Json...-Methoden (JsonValue, JsonHas, JsonObject, JsonEntries, ...) verwendet werden können.
    /// de: Liefert null, wenn der Wert fehlt, leer oder kein gültiges JSON-Objekt ist (z.B. Array oder Einzelwert) - es wird nie eine Exception geworfen.
    /// de: Der Datensatz selbst wird nicht verändert. Ist der Wert bereits ein Objekt (JSON-API), wird er unverändert geliefert.
    /// de: Schlüssel mit Sonderzeichen (z.B. $type) über die Klammer-Schreibweise ansprechen: @data.JsonValue("['$type']")
    /// de: Beispiel: @{ var data = record.JsonParse("json"); } @data.JsonValue("regelung.vorgabeDynamischeNetzstuetzung")
    /// de: Beispiel: @foreach (var entry in record.JsonParse("json").JsonEntries()) { @entry.Key: @entry.Value }
    /// en: Parses a JSON text stored in a database column (e.g. nvarchar column "json") into an object
    /// en: on which all Json... methods (JsonValue, JsonHas, JsonObject, JsonEntries, ...) can be used.
    /// en: Returns null if the value is missing, empty or not a valid JSON object (e.g. an array or a scalar) - this never throws.
    /// en: The record itself is not modified. If the value is already an object (JSON API), it is returned unchanged.
    /// en: Use bracket syntax for keys containing special characters (e.g. $type): @data.JsonValue("['$type']")
    /// en: Example: @{ var data = record.JsonParse("json"); } @data.JsonValue("regelung.vorgabeDynamischeNetzstuetzung")
    /// en: Example: @foreach (var entry in record.JsonParse("json").JsonEntries()) { @entry.Key: @entry.Value }
    /// </summary>
    /// <param name="record">
    /// de: Der Datensatz, z.B. die Laufvariable einer foreach-Schleife über Model.Records.
    /// en: The record, e.g. the loop variable of a foreach loop over Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der Spaltenname (z.B. json) oder ein JSONPath-ähnlicher Pfad zum Wert, der den JSON-Text enthält.
    /// en: The column name (e.g. json) or a JSONPath-like path to the value containing the JSON text.
    /// </param>
    static public IDictionary<string, object> JsonParse(this IDictionary<string, object> record, string path)
        => ParseJsonValue(record, path) as IDictionary<string, object>;

    /// <summary>
    /// de: Wandelt einen JSON-Text aus einer Datenbank-Spalte, der ein JSON-Array enthält, in eine Liste um.
    /// de: Elemente, die Objekte sind, können mit allen Json...-Methoden weiterverarbeitet werden.
    /// de: Liefert eine leere Liste, wenn der Wert fehlt, leer oder kein gültiges JSON-Array ist - es wird nie eine Exception geworfen.
    /// de: Beispiel: @foreach (var item in record.JsonParseArray("positionen").OfType&lt;IDictionary&lt;string, object&gt;&gt;()) { @item.JsonValue("name") }
    /// en: Parses a JSON text stored in a database column that contains a JSON array into a list.
    /// en: Elements that are objects can be processed further with all Json... methods.
    /// en: Returns an empty list if the value is missing, empty or not a valid JSON array - this never throws.
    /// en: Example: @foreach (var item in record.JsonParseArray("positionen").OfType&lt;IDictionary&lt;string, object&gt;&gt;()) { @item.JsonValue("name") }
    /// </summary>
    /// <param name="record">
    /// de: Der Datensatz, z.B. die Laufvariable einer foreach-Schleife über Model.Records.
    /// en: The record, e.g. the loop variable of a foreach loop over Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der Spaltenname (z.B. positionen) oder ein JSONPath-ähnlicher Pfad zum Wert, der den JSON-Text enthält.
    /// en: The column name (e.g. positionen) or a JSONPath-like path to the value containing the JSON text.
    /// </param>
    static public IEnumerable<object> JsonParseArray(this IDictionary<string, object> record, string path)
    {
        var value = ParseJsonValue(record, path);

        if (value is null || value is string || value is IDictionary<string, object>)
        {
            return Enumerable.Empty<object>();
        }

        if (value is IEnumerable<object> items)
        {
            return items;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>().ToList();
        }

        return Enumerable.Empty<object>();
    }

    /// <summary>
    /// de: Liefert den Wert am angegebenen Pfad als fertig formatierten Text - die empfohlene Art, Werte in PDF-Reports und Dashboards auszugeben.
    /// de: Zahlen ohne Format werden ohne überflüssige Nachkommastellen mit Tausender-Trennzeichen der Kultur ausgegeben (170.000 aus JSON wird zu 170).
    /// de: Mit Format: Zahlen z.B. "N2", ISO-Datums-Texte z.B. "dd.MM.yyyy HH:mm", Ja/Nein-Werte z.B. "✓|–" (Text für true|false).
    /// de: Ja/Nein-Werte ohne Format: "ja"/"nein" bei deutscher Kultur, sonst "true"/"false". Texte werden ohne Format unverändert geliefert.
    /// de: Arrays aus Einzelwerten werden mit ", " verbunden. Fehlt der Wert, ist er null oder ein Objekt, wird defaultValue geliefert. Es wird nie eine Exception geworfen.
    /// de: Beispiel: @data.JsonText("eigenverbrauchKw", "N0")
    /// de: Beispiel: @data.JsonText("technik_sqlps_upd_dat", "dd.MM.yyyy HH:mm")
    /// de: Beispiel: @data.JsonText("inselbetriebsfaehig") @data.JsonText("bemerkung_anl", defaultValue: "–")
    /// en: Returns the value at the given path as a formatted display text - the recommended way to print values in PDF reports and dashboards.
    /// en: Numbers without format are printed without superfluous decimals using the group separators of the culture (170.000 from JSON becomes 170).
    /// en: With format: numbers e.g. "N2", ISO date strings e.g. "dd.MM.yyyy HH:mm", booleans e.g. "✓|–" (text for true|false).
    /// en: Booleans without format: "ja"/"nein" for German cultures, otherwise "true"/"false". Strings without format are returned unchanged.
    /// en: Arrays of scalar values are joined with ", ". If the value is missing, null or an object, defaultValue is returned. This never throws.
    /// en: Example: @data.JsonText("eigenverbrauchKw", "N0")
    /// en: Example: @data.JsonText("technik_sqlps_upd_dat", "dd.MM.yyyy HH:mm")
    /// en: Example: @data.JsonText("inselbetriebsfaehig") @data.JsonText("bemerkung_anl", defaultValue: "–")
    /// </summary>
    /// <param name="record">
    /// de: Der Datensatz, z.B. das Ergebnis von JsonParse oder die Laufvariable einer foreach-Schleife.
    /// en: The record, e.g. the result of JsonParse or the loop variable of a foreach loop.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. regelung.vorgabeDynamischeNetzstuetzung.
    /// en: The JSONPath-like path, e.g. regelung.vorgabeDynamischeNetzstuetzung.
    /// </param>
    /// <param name="format">
    /// de: Optionales Format: .NET-Zahlenformat (N0, N2, #,##0.0), Datumsformat (dd.MM.yyyy) oder "TextTrue|TextFalse" für Ja/Nein-Werte.
    /// en: Optional format: .NET number format (N0, N2, #,##0.0), date format (dd.MM.yyyy) or "TextTrue|TextFalse" for booleans.
    /// </param>
    /// <param name="defaultValue">
    /// de: Text, der geliefert wird, wenn der Wert fehlt, null oder nicht als Text darstellbar ist (Objekt). Standard: leerer Text.
    /// en: Text returned if the value is missing, null or cannot be displayed as text (object). Default: empty text.
    /// </param>
    /// <param name="culture">
    /// de: Kultur für Zahlen- und Datumsformate, z.B. de-AT oder en-US. Ungültige Werte werden durch de-AT ersetzt.
    /// en: Culture for number and date formats, e.g. de-AT or en-US. Invalid values fall back to de-AT.
    /// </param>
    static public string JsonText(this IDictionary<string, object> record, string path,
                                  string format = null, string defaultValue = "", string culture = DefaultCulture)
    {
        if (record is null)
        {
            return defaultValue;
        }

        try
        {
            return FormatText(JsonPathResolver.SelectFirst(record, path), format, defaultValue, culture);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// de: Liefert alle Einzelwerte eines (verschachtelten) Objekts als flache Liste von Schlüssel/Wert-Paaren - in der Reihenfolge der Daten.
    /// de: Ideal, um unbekannte oder je Datensatz unterschiedliche JSON-Strukturen mit einer einzigen foreach-Schleife als Tabelle auszugeben.
    /// de: Schlüssel sind die verbundenen Namen, z.B. regelung.regelungPuQu oder items[0].price. Leere Objekte/Arrays werden mit Wert null geliefert.
    /// de: Achtung: Die Schlüssel sind Beschriftungen und nicht immer gültige Pfade (Namen können . oder $ enthalten) - zum Formatieren daher field.JsonText(...) verwenden.
    /// de: Beispiel: @foreach (var field in data.JsonFlatten()) { &lt;tr&gt;&lt;th&gt;@field.Key&lt;/th&gt;&lt;td&gt;@field.JsonText("N2")&lt;/td&gt;&lt;/tr&gt; }
    /// en: Returns all leaf values of a (nested) object as a flat list of key/value pairs - in source order.
    /// en: Ideal to render unknown JSON structures, or structures that differ per record, as a table with a single foreach loop.
    /// en: Keys are the joined names, e.g. regelung.regelungPuQu or items[0].price. Empty objects/arrays are returned with value null.
    /// en: Note: keys are labels and not always valid paths (names may contain . or $) - use field.JsonText(...) to format the values.
    /// en: Example: @foreach (var field in data.JsonFlatten()) { &lt;tr&gt;&lt;th&gt;@field.Key&lt;/th&gt;&lt;td&gt;@field.JsonText("N2")&lt;/td&gt;&lt;/tr&gt; }
    /// </summary>
    /// <param name="record">
    /// de: Der Datensatz, z.B. das Ergebnis von JsonParse.
    /// en: The record, e.g. the result of JsonParse.
    /// </param>
    /// <param name="path">
    /// de: Optionaler JSONPath-ähnlicher Pfad zum Objekt/Array, das aufgelöst werden soll, z.B. json. Ohne Pfad wird der Datensatz selbst verwendet.
    /// en: Optional JSONPath-like path to the object/array to flatten, e.g. json. Without a path the record itself is used.
    /// </param>
    /// <param name="separator">
    /// de: Trennzeichen zwischen den Namen im Schlüssel. Standard: ".".
    /// en: Separator between the names in the key. Default: ".".
    /// </param>
    static public IEnumerable<KeyValuePair<string, object>> JsonFlatten(this IDictionary<string, object> record,
                                                                        string path = null, string separator = ".")
    {
        var result = new List<KeyValuePair<string, object>>();

        if (record is null)
        {
            return result;
        }

        try
        {
            object start;

            if (String.IsNullOrWhiteSpace(path))
            {
                start = record;
            }
            else
            {
                var matches = JsonPathResolver.Select(record, path).Take(1).ToArray();
                if (matches.Length == 0)
                {
                    return result;
                }

                start = matches[0];
            }

            if (IsContainer(start))
            {
                FlattenContainer(start, null, separator ?? ".", result, new HashSet<object>(ReferenceComparer.Instance));
            }
            else
            {
                result.Add(new KeyValuePair<string, object>(path ?? String.Empty, start));
            }
        }
        catch
        {
            // never throw - return what we have
        }

        return result;
    }

    /// <summary>
    /// de: Liefert den Typ des Werts am angegebenen Pfad als Text: "missing", "null", "object", "array", "string", "number" oder "bool".
    /// de: "missing" bedeutet, der Pfad existiert nicht; "null" bedeutet, der Wert ist vorhanden, aber leer (JSON null).
    /// de: Damit lassen sich gemischte Daten in Views unterscheiden, ohne C#-Typprüfungen zu schreiben.
    /// de: Beispiel: @if (data.JsonType("regelung") == "object") { foreach (var e in data.JsonEntries("regelung")) { ... } }
    /// en: Returns the type of the value at the given path as text: "missing", "null", "object", "array", "string", "number" or "bool".
    /// en: "missing" means the path does not exist; "null" means the value is present but empty (JSON null).
    /// en: Use this to handle mixed data in views without writing C# type checks.
    /// en: Example: @if (data.JsonType("regelung") == "object") { foreach (var e in data.JsonEntries("regelung")) { ... } }
    /// </summary>
    /// <param name="record">
    /// de: Der Datensatz, z.B. das Ergebnis von JsonParse.
    /// en: The record, e.g. the result of JsonParse.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. regelung.regelungPuQu.
    /// en: The JSONPath-like path, e.g. regelung.regelungPuQu.
    /// </param>
    static public string JsonType(this IDictionary<string, object> record, string path)
    {
        if (record is null)
        {
            return "missing";
        }

        try
        {
            var matches = JsonPathResolver.Select(record, path).Take(1).ToArray();

            return matches.Length == 0
                ? "missing"
                : TypeName(matches[0]);
        }
        catch
        {
            return "missing";
        }
    }

    #endregion

    #region Entry (KeyValuePair, e.g. from JsonFlatten or JsonEntries)

    /// <summary>
    /// de: Formatiert den Wert eines Schlüssel/Wert-Paars (z.B. aus JsonFlatten oder JsonEntries) mit denselben Regeln wie record.JsonText(path, ...).
    /// de: Beispiel: @foreach (var field in data.JsonFlatten()) { &lt;tr&gt;&lt;th&gt;@field.Key&lt;/th&gt;&lt;td&gt;@field.JsonText("N2", "–")&lt;/td&gt;&lt;/tr&gt; }
    /// en: Formats the value of a key/value pair (e.g. from JsonFlatten or JsonEntries) with the same rules as record.JsonText(path, ...).
    /// en: Example: @foreach (var field in data.JsonFlatten()) { &lt;tr&gt;&lt;th&gt;@field.Key&lt;/th&gt;&lt;td&gt;@field.JsonText("N2", "–")&lt;/td&gt;&lt;/tr&gt; }
    /// </summary>
    /// <param name="entry">
    /// de: Das Schlüssel/Wert-Paar, z.B. die Laufvariable einer foreach-Schleife über JsonFlatten().
    /// en: The key/value pair, e.g. the loop variable of a foreach loop over JsonFlatten().
    /// </param>
    /// <param name="format">
    /// de: Optionales Format: .NET-Zahlenformat (N0, N2), Datumsformat (dd.MM.yyyy) oder "TextTrue|TextFalse" für Ja/Nein-Werte.
    /// en: Optional format: .NET number format (N0, N2), date format (dd.MM.yyyy) or "TextTrue|TextFalse" for booleans.
    /// </param>
    /// <param name="defaultValue">
    /// de: Text, der geliefert wird, wenn der Wert null oder nicht als Text darstellbar ist. Standard: leerer Text.
    /// en: Text returned if the value is null or cannot be displayed as text. Default: empty text.
    /// </param>
    /// <param name="culture">
    /// de: Kultur für Zahlen- und Datumsformate, z.B. de-AT oder en-US. Ungültige Werte werden durch de-AT ersetzt.
    /// en: Culture for number and date formats, e.g. de-AT or en-US. Invalid values fall back to de-AT.
    /// </param>
    static public string JsonText(this KeyValuePair<string, object> entry,
                                  string format = null, string defaultValue = "", string culture = DefaultCulture)
    {
        try
        {
            return FormatText(entry.Value, format, defaultValue, culture);
        }
        catch
        {
            return defaultValue;
        }
    }

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
    /// de: Liefert den ersten Wert, der auf den angegebenen Pfad passt, als fertig formatierten Text (Regeln wie record.JsonText).
    /// de: Der interne Marker-Datensatz des JSON-API wird dabei übersprungen. Es wird nie eine Exception geworfen.
    /// de: Beispiel: @Model.Records.JsonText("paging.total", "N0", "–")
    /// en: Returns the first value matching the given path as a formatted display text (same rules as record.JsonText).
    /// en: The internal JSON API marker record is skipped. This never throws.
    /// en: Example: @Model.Records.JsonText("paging.total", "N0", "–")
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="path">
    /// de: Der JSONPath-ähnliche Pfad, z.B. paging.total.
    /// en: The JSONPath-like path, e.g. paging.total.
    /// </param>
    /// <param name="format">
    /// de: Optionales Format: .NET-Zahlenformat (N0, N2), Datumsformat (dd.MM.yyyy) oder "TextTrue|TextFalse" für Ja/Nein-Werte.
    /// en: Optional format: .NET number format (N0, N2), date format (dd.MM.yyyy) or "TextTrue|TextFalse" for booleans.
    /// </param>
    /// <param name="defaultValue">
    /// de: Text, der geliefert wird, wenn der Wert fehlt, null oder nicht als Text darstellbar ist. Standard: leerer Text.
    /// en: Text returned if the value is missing, null or cannot be displayed as text. Default: empty text.
    /// </param>
    /// <param name="culture">
    /// de: Kultur für Zahlen- und Datumsformate, z.B. de-AT oder en-US. Ungültige Werte werden durch de-AT ersetzt.
    /// en: Culture for number and date formats, e.g. de-AT or en-US. Invalid values fall back to de-AT.
    /// </param>
    static public string JsonText(this IEnumerable<IDictionary<string, object>> records, string path,
                                  string format = null, string defaultValue = "", string culture = DefaultCulture)
    {
        var record = records.SkipMarker().FirstOrDefault();

        return record is null
            ? defaultValue
            : record.JsonText(path, format, defaultValue, culture);
    }

    /// <summary>
    /// de: Wandelt die JSON-Texte einer Spalte für alle Datensätze um und liefert Kopien der Datensätze, in denen die Spalte das umgewandelte Objekt enthält.
    /// de: Alle anderen Spalten (z.B. id, bezeichnung) bleiben erhalten - ideal für Listen und PDF-Schleifen, die beides brauchen.
    /// de: Model.Records selbst wird nicht verändert. Ungültiges oder leeres JSON ergibt null in der Spalte, der Datensatz bleibt erhalten.
    /// de: Beispiel: @foreach (var r in Model.Records.JsonParseColumn("json")) { @r.JsonText("id") @r.JsonText("json.vertragsleistungKw", "N0") }
    /// en: Parses the JSON texts of a column for all records and returns copies of the records in which the column contains the parsed object.
    /// en: All other columns (e.g. id, bezeichnung) are kept - ideal for lists and PDF loops that need both.
    /// en: Model.Records itself is not modified. Invalid or empty JSON results in null in the column, the record is kept.
    /// en: Example: @foreach (var r in Model.Records.JsonParseColumn("json")) { @r.JsonText("id") @r.JsonText("json.vertragsleistungKw", "N0") }
    /// </summary>
    /// <param name="records">
    /// de: Die Datensätze, üblicherweise Model.Records.
    /// en: The records, usually Model.Records.
    /// </param>
    /// <param name="column">
    /// de: Der Name der Spalte mit dem JSON-Text, z.B. json (Groß-/Kleinschreibung egal).
    /// en: The name of the column containing the JSON text, e.g. json (case insensitive).
    /// </param>
    static public IEnumerable<IDictionary<string, object>> JsonParseColumn(this IEnumerable<IDictionary<string, object>> records, string column)
    {
        foreach (var record in records.SkipMarker())
        {
            var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in record)
            {
                copy[kvp.Key] = kvp.Value;
            }

            if (!String.IsNullOrEmpty(column) && copy.TryGetValue(column, out object value))
            {
                var parsed = ParseJson(value);

                copy[column] = parsed is IDictionary<string, object> || IsContainer(parsed)
                    ? parsed
                    : null;
            }

            yield return copy;
        }
    }

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

    private const string DefaultCulture = "de-AT";

    static private bool IsContainer(object value)
        => value is IDictionary<string, object>
        || (value is System.Collections.IEnumerable && value is not string);

    static private void FlattenContainer(object node, string prefix, string separator,
                                         List<KeyValuePair<string, object>> result, HashSet<object> visited)
    {
        if (!visited.Add(node))
        {
            return;
        }

        bool empty = true;

        if (node is IDictionary<string, object> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                empty = false;
                FlattenNode(JsonPathResolver.Unwrap(kvp.Value),
                            prefix is null ? kvp.Key : prefix + separator + kvp.Key,
                            separator, result, visited);
            }
        }
        else if (node is System.Collections.IEnumerable enumerable)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                empty = false;
                FlattenNode(JsonPathResolver.Unwrap(item),
                            (prefix ?? String.Empty) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                            separator, result, visited);
                index++;
            }
        }

        if (empty && prefix is not null)
        {
            result.Add(new KeyValuePair<string, object>(prefix, null));
        }

        visited.Remove(node);
    }

    static private void FlattenNode(object value, string key, string separator,
                                    List<KeyValuePair<string, object>> result, HashSet<object> visited)
    {
        if (IsContainer(value))
        {
            FlattenContainer(value, key, separator, result, visited);
        }
        else
        {
            result.Add(new KeyValuePair<string, object>(key, value is DBNull ? null : value));
        }
    }

    static private string TypeName(object value)
    {
        value = JsonPathResolver.Unwrap(value);

        return value switch
        {
            null => "null",
            DBNull => "null",
            bool => "bool",
            string => "string",
            IDictionary<string, object> => "object",
            _ when IsNumber(value) => "number",
            System.Collections.IEnumerable => "array",
            _ => "string"
        };
    }

    static private bool IsNumber(object value)
        => value is byte || value is sbyte
        || value is short || value is ushort
        || value is int || value is uint
        || value is long || value is ulong
        || value is float || value is double
        || value is decimal;

    static private CultureInfo ResolveCulture(string culture)
    {
        if (!String.IsNullOrWhiteSpace(culture))
        {
            try
            {
                return CultureInfo.GetCultureInfo(culture.Trim());
            }
            catch { }
        }

        return CultureInfo.GetCultureInfo(DefaultCulture);
    }

    static private string FormatText(object value, string format, string defaultValue, string culture)
    {
        value = JsonPathResolver.Unwrap(value);

        if (value is null || value is DBNull || value is IDictionary<string, object>)
        {
            return defaultValue;
        }

        var cultureInfo = ResolveCulture(culture);

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var items = new List<string>();

            foreach (var item in enumerable)
            {
                var itemValue = JsonPathResolver.Unwrap(item);

                if (IsContainer(itemValue))
                {
                    return defaultValue;
                }

                if (itemValue is null || itemValue is DBNull)
                {
                    continue;
                }

                items.Add(FormatScalar(itemValue, format, cultureInfo));
            }

            return items.Count == 0
                ? defaultValue
                : String.Join(", ", items);
        }

        return FormatScalar(value, format, cultureInfo);
    }

    static private string FormatScalar(object value, string format, CultureInfo culture)
    {
        bool hasFormat = !String.IsNullOrEmpty(format);

        switch (value)
        {
            case bool flag:
                if (hasFormat && format.Contains('|'))
                {
                    var texts = format.Split('|');
                    return flag ? texts[0] : texts[1];
                }

                return culture.TwoLetterISOLanguageName == "de"
                    ? (flag ? "ja" : "nein")
                    : (flag ? "true" : "false");

            case string text:
                if (hasFormat)
                {
                    if (IsNumericFormat(format)
                        && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)
                        && TryFormat(number, format, culture, out string formattedNumber))
                    {
                        return formattedNumber;
                    }

                    if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
                        && TryFormat(date, format, culture, out string formattedDate))
                    {
                        return formattedDate;
                    }
                }

                return text;

            case DateTime dateTime:
                return hasFormat && TryFormat(dateTime, format, culture, out string formattedDateTime)
                    ? formattedDateTime
                    : dateTime.ToString(culture);

            case DateTimeOffset dateTimeOffset:
                return hasFormat && TryFormat(dateTimeOffset, format, culture, out string formattedDateTimeOffset)
                    ? formattedDateTimeOffset
                    : dateTimeOffset.ToString(culture);
        }

        if (IsNumber(value))
        {
            if (hasFormat && TryFormat((IFormattable)value, format, culture, out string formatted))
            {
                return formatted;
            }

            // no (valid) format: remove trailing zeros (170.000m => 170), keep group separators of the culture
            return value switch
            {
                double d => d.ToString("#,##0.###############", culture),
                float f => f.ToString("#,##0.#######", culture),
                _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("#,##0.############################", culture)
            };
        }

        return Convert.ToString(value, culture);
    }

    static private bool IsNumericFormat(string format)
    {
        // standard numeric formats: N2, F0, C, P1, E3, G, D5, X, R
        if (System.Text.RegularExpressions.Regex.IsMatch(format, @"^[CcDdEeFfGgNnPpRrXx]\d{0,2}$"))
        {
            return true;
        }

        // custom numeric formats: #,##0.00, 0.0 %, ... (no date/time letters)
        return format.IndexOfAny(new[] { '0', '#' }) >= 0
            && !format.Any(c => Char.IsLetter(c) && c != 'E' && c != 'e');
    }

    static private bool TryFormat(IFormattable value, string format, CultureInfo culture, out string result)
    {
        try
        {
            result = value.ToString(format, culture);
            return true;
        }
        catch (FormatException)
        {
            result = null;
            return false;
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        static public readonly ReferenceComparer Instance = new ReferenceComparer();

        private ReferenceComparer() { }

        new public bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

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

    static private readonly System.Text.Json.JsonDocumentOptions ParseOptions = new System.Text.Json.JsonDocumentOptions
    {
        AllowTrailingCommas = true,
        CommentHandling = System.Text.Json.JsonCommentHandling.Skip
    };

    /// <summary>
    /// Returns the value at path as plain clr structure (dictionary / list / scalar).
    /// JSON strings are parsed, already structured values (json api) are only unwrapped.
    /// Never throws; returns null for missing, empty or invalid values.
    /// </summary>
    static private object ParseJsonValue(IDictionary<string, object> record, string path)
    {
        if (record is null || String.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return ParseJson(JsonPathResolver.SelectFirst(record, path));
        }
        catch
        {
            return null;
        }
    }

    static private object ParseJson(object value)
    {
        try
        {
            value = JsonPathResolver.Unwrap(value);

            if (value is null || value is DBNull)
            {
                return null;
            }

            if (value is string text)
            {
                text = text.Trim();

                if (text.Length == 0)
                {
                    return null;
                }

                using (var document = System.Text.Json.JsonDocument.Parse(text, ParseOptions))
                {
                    return JsonPathResolver.Unwrap(document.RootElement);
                }
            }

            return value;
        }
        catch
        {
            return null;
        }
    }

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
