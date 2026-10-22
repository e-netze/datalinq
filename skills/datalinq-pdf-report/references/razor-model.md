# Razor model and data binding

A DataLinq view is compiled with these globals (injected by `DatalinqCompilerService`):

| Name | Meaning |
|------|---------|
| `Model` | Result of the view's query |
| `DLH` (= `DataLinqHelper`, `IH`) | General DataLinq helper |
| `PDF` | PDF helper (see `pdf-helper-api.md`) |
| `SECURITY` | Security helper |

Namespaces `E.DataLinq.Web.Razor` and `E.DataLinq.Web.Models.Razor` are imported. `@using` is normally **not allowed**, and some API calls are blacklisted by the instance – keep code to plain C#/LINQ/string formatting.

## Model

```
Model.Success              bool
Model.CountRecords         int
Model.ElapsedMillisconds   int
Model.Records              IDictionary<string,object>[]   // one entry per row; keys = column names
Model.RecordColumns()      IEnumerable<string>
Model.QueryString          NameValueCollection           // URL parameters of the request
Model.FilterString         string
```

Column keys are exactly as returned by the query (case matters; Oracle usually returns UPPER CASE). Always confirm with the `sample_query` tool.

## Reading values safely

```razor
@{
	var r = Model.Records.FirstOrDefault();
	string V(IDictionary<string, object> rec, string key) =>
		rec != null && rec.ContainsKey(key) && rec[key] != null ? rec[key].ToString() : "";
}
@if (r == null)
{
	<p>No data found.</p>
}
else
{
	<h1>@V(r, "TITLE")</h1>
}
```

Formatting:

```razor
@(Convert.ToDateTime(r["CREATED"]).ToString("dd.MM.yyyy"))
@(Convert.ToDecimal(r["AMOUNT"]).ToString("#,##0.00"))
```

**`System.` is blocked.** Any fully qualified `System.xxx` expression fails with "System. is not allowed with DataLinq Pages", and `CultureInfo` is not available (no `@using` allowed). Use types without prefix (`DateTime`, `Convert`, `Math`, `DBNull`, LINQ) and explicit format patterns. The number format uses the server culture; for a guaranteed Austrian/German format, format manually, e.g. `.ToString("#,##0.00").Replace(",", "§").Replace(".", ",").Replace("§", ".")` after checking the rendered output.

Guard conversions against `null`/`DBNull` (`r["X"] == null || r["X"] is DBNull`).

## LINQ patterns

```razor
@foreach (var row in Model.Records.OrderBy(x => x["POS"]))
{ <tr><td>@row["POS"]</td><td>@row["TEXT"]</td></tr> }

@{ var total = Model.Records.Sum(x => Convert.ToDecimal(x["AMOUNT"])); }

@foreach (var g in Model.Records.GroupBy(x => x["DEPARTMENT"]?.ToString()))
{ <h2>@g.Key</h2> ... }
```

## URL parameters

```razor
@{ var id = DLH.QueryParameter("id", required: true, regex: "^[0-9]+$"); }
```
The query itself normally already filters by URL parameters; use `QueryParameter` for display or for passing filters to other queries.

## Multiple data sources

One view gets one query's records in `Model.Records`. Load additional queries server-side:

```razor
@{
	var positions = await DLH.GetRecordsAsync("shop@order-positions", filter: "ORDER_ID=" + Model.Records[0]["ORDER_ID"], orderby: "POS");
}
```
- `id` must be `endpoint@query` (no view).
- `filter` uses URL parameter syntax (`A=1&B=2`), passed to the query as arguments.
- `orderby`: comma separated fields, prefix `-` for descending.
- May return `null` on failure – check before use.

Run `sample_query` on every additional query to learn its columns.

## Razor pitfalls

- `@` inside e-mail addresses or CSS `@media`/`@page`/`@font-face` must be written as `@@`.
- Inside `<style>`, avoid Razor expressions unless needed.
- `@value` HTML-encodes; never use `@Html.Raw(...)` on database values.
- Comments: `@* ... *@`.
- Code blocks: `@{ ... }`; inside HTML use `@if`, `@foreach`. Text lines inside code blocks need `<text>` or `@:`.
- Local functions inside `@{ }` are allowed (as in the example above).

## Other DLH helpers useful in reports

- `DLH.Table(Model.Records, max: 100)` – quick HTML table (useful for prototyping only; custom tables give better PDF control).
- `DLH.RecordsToJs(records, "jsCallbackName")` – pass records to JavaScript (e.g. for charts).
Check `<DATALINQ_URL>/datalinq/help` for the full DLH reference of the specific instance version.
