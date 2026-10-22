# DataLinq model: endpoints, queries, views, records

## Routing

```
endpoint            data source (database connection, API, text file...)
endpoint@query      SQL/statement + parameters + access rules  -> JSON / records
endpoint@query@view Razor view rendering the query result (HTML site or PDF)
```

URLs: `/datalinq/select/endpoint@query` (JSON), `/datalinq/report/endpoint@query@view` (rendered view).
URL parameters are passed to the query (`{{param}}` / `@param` in the statement) and are available in the view through `Model.QueryString` / `DLH.QueryParameter(...)`.

## Globals inside a view

| Name | Meaning |
|------|---------|
| `Model` | Result of **this view's own query** |
| `DLH` (= `DataLinqHelper`, `IH`) | General helper (sites, dashboards, controls, charts) |
| `PDF` | PDF helper (PDF report mode only) |
| `SECURITY` | Security helper |

`@using` is not allowed, `System.` prefixed expressions are blocked, no `CultureInfo`. Use `DateTime`, `Convert`, `Math`, LINQ.

## Model

```
Model.Success  Model.CountRecords  Model.ElapsedMillisconds
Model.Records          IDictionary<string,object>[]   keys = exact column names (Oracle: UPPER CASE)
Model.RecordColumns()  IEnumerable<string>
Model.QueryString      NameValueCollection
Model.FilterString     string
```

Extension methods on `Model.Records` (statistics, grouping...): `get_dlh_reference helper=RECORDS`.

## Model.Records vs DLH.GetRecordsAsync

- `Model.Records`: rows of the view's own query, already filtered by the request URL parameters. Use for the main content.
- `await DLH.GetRecordsAsync("endpoint@query", filter: "A=1&B=2", orderby: "COL,-COL2")`: loads **another** query server-side during rendering. `id` must be `endpoint@query` (no view). `filter` uses URL syntax and is passed as query arguments. Returns `null` on failure - always check.
- Client-side alternative for interactive dashboards: `DLH.JsFetchData(...)` / `fetch('/datalinq/select/endpoint@query?...')` and render with JS; or `DLH.RecordsToJs(records, "callback")` to hand server rows to JS (charts).
- Embedding other views: `DLH.IncludeView("endpoint@query@view")`, `DLH.IncludeClickView(...)`, refresh via `DLH.RefreshViewClick/Ticker`.

Always run `sample_query` on every query you bind (columns are case sensitive).

## Reading values safely

```razor
@{
	var r = Model.Records.FirstOrDefault();
	string V(IDictionary<string, object> rec, string key) =>
		rec != null && rec.ContainsKey(key) && rec[key] != null && !(rec[key] is DBNull) ? rec[key].ToString() : "";
}
```

## Razor pitfalls

- `@` in e-mails and CSS `@media`/`@page`/`@font-face`/`@keyframes` -> `@@`.
- `@value` HTML-encodes; never `@Html.Raw(...)` database values.
- Text lines inside `@{ }` need `<text>` or `@:`.
- In `<script>`, emit data via `DLH.RecordsToJs` instead of building JS strings from values.

Full helper lists: `get_dlh_reference`, `get_pdf_reference` (XML docs of the running instance).

Query statement syntax (#if, @param, {{param}}, _orderby) and view flags: `query-syntax.md` or `get_query_reference`.
