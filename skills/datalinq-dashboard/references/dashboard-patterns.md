# Dashboard patterns

## Server vs. client data

| Need | Pattern |
|---|---|
| Main rows of this view | `Model.Records` |
| Rows of another query at render time | `await DLH.GetRecordsAsync("ep@q", filter: "A=1", orderby: "-DATE")` (null-check!) |
| Rows to JavaScript (charts) | `@DLH.RecordsToJs(rows, "callbackName")` + `window.callbackName = function(rows){...}` (jQuery ready) |
| Reload on user action | `DLH.JsFetchData(...)` or `fetch('/datalinq/select/ep@q?...')` |
| Embed another view | `DLH.IncludeView(...)`, `DLH.IncludeClickView(...)` |
| Auto refresh | `DLH.RefreshViewTicker(...)` / `RefreshViewClick(...)` |
| Search / sort / filter / export | `DLH.QuickSearch`, `SortView`, `FilterView`, `ExportView` |
| URL parameter | `DLH.QueryParameter("id", required: true, regex: "^[0-9]+$")` |

Exact signatures: `get_dlh_reference method=<name>`. Statistics on records: `get_dlh_reference helper=RECORDS`.

## Feature -> helper (use these first)

| Feature | Helper | What the query / view needs |
|---|---|---|
| Filter | `DLH.FilterView(label, new[]{"p1","p2"})` | One `#if p1` ... `#endif` block per parameter in the SQL (`WHERE 1=1` + `#if p1` / `AND col = @p1` / `#endif`). View must be embedded via `IncludeView`. |
| Sort | `DLH.SortView(label, new[]{"COL1","COL2"})` | `ORDER BY @_orderby` in the SQL (or ordering by the engine). View embedded via `IncludeView`. |
| Search (client side) | `DLH.QuickSearch(columns, placeholder, tableId)` | Table with that `id`. |
| Export | `DLH.ExportView(...)` | - |
| New / edit | `DLH.OpenViewInDialog("ep@q@formview", new{ id = ... }, buttonText: "...", dialogAttributes: new{ dialogTitle = "..." })` | A form view (see CRUD flow). |
| Save | `DLH.BeginForm("ep@saveQuery")`, `TextFor`, `CheckboxFor`, `ComboFor`, `HiddenFor`, `DLH.EndForm("Save","Cancel")` | Insert/update query with the field names as parameters. |
| Delete / set value | `DLH.ExecuteNonQuery("ep@deleteQuery", new{ id = ... }, buttonText: "...", dialogAttributes: new{ confirmText = "..." })` | Delete/update query with `@id`. |
| Reload part of the page | `DLH.IncludeView`, `RefreshViewClick`, `RefreshViewTicker` | - |

## CRUD flow (create / edit / delete)

Views (template: `assets/templates/05-crud.cshtml`, `05-crud-form.cshtml`):

1. **Page view** - only layout + `@DLH.IncludeView("ep@list@listview")`.
2. **List view** (`ep@list@listview`) - `FilterView`, `SortView`, "New" button (`OpenViewInDialog` without id), per row "Edit" (`OpenViewInDialog` with id) and "Delete" (`ExecuteNonQuery` with `confirmText`).
3. **Form view** (`ep@item@formview`) - `BeginForm("ep@save")` ... fields ... `EndForm`.

Queries: `list` (with `#if` filters + `@_orderby`), `item` (exactly one row, empty for "new"), `save` (insert when id empty, else update), `delete`.

Empty row for "new" (`item` query):
```
SELECT t.* FROM (SELECT 1 AS x) d LEFT JOIN MyTable t ON 1 = 0
#if id
OR t.Id = @id
#endif
```

Pitfalls:
- After save/delete only the surrounding `IncludeView` (or `IncludeClickView`) is reloaded. A list rendered directly in the page view is **not** refreshed -> always embed the list via `IncludeView`.
- `<form>` (`BeginForm`) must be the **top-level element** of the form view. A wrapping `<div>` breaks the reload after saving. Put `<style>` inside the form or into the view's CSS.
- `CheckboxFor` posts `"true"` / `"false"` (strings) - convert in the save query.
- `TextFor` with date types also posts a hidden field with the same name.
- No `'` (apostrophe) in `buttonText`, `dialogTitle`, `confirmText` - attributes are written single-quoted and the JSON breaks.
- Inline `<svg>`, base64 / `data:image` are rejected on save - use text, CSS or an image URL.
- Values the user must not edit but the save query needs (e.g. the id) go as `HiddenFor`.

## Layout

- Root class per view (`.dl-dash`) to scope CSS; views can be embedded in other views.
- CSS grid with `auto-fit/minmax` for cards; tables `width:100%`; wrap wide tables in `overflow-x:auto`.
- `@@media` in Razor.
- One `<script>` at the end; no global names except the callbacks you need (prefix `dl`).

## Performance

- Aggregate in SQL; `GetRecordsAsync` in a loop = N queries - avoid, load once and group with LINQ.
- Limit rows (`top`/paging in the query).

## Safety

- Razor `@value` encodes; never `@Html.Raw` data.
- In JS use `textContent`, not `innerHTML`, for data values.
