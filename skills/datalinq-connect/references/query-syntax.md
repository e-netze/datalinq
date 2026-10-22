# Query statement syntax

Full, endpoint-specific text: `get_query_reference endpointType=database|cypher|textfile|jsonapi|all`.

## Parameters (database endpoints)
- `@param` / `:param`: bound parameter (preferred). Bound only if a URL parameter with that name exists.
- A `@param` without a matching URL parameter reaches the database unbound -> "Insufficient parameters" / "Must declare the scalar variable". Make it optional with `#if`.
- `{{param}}`: raw text replacement (SQL injection risk, identifiers/lists only). Not allowed in `run_sql_preview`.
- `_orderby`: `ORDER BY @_orderby` is replaced by validated columns (`-COL` = DESC). Used by `DLH.GetRecordsAsync(orderby:)` and `DLH.SortView`.

## `#if` / `#endif`
- `#if a,b` ... `#endif`, line-based, directive must start the line, no spaces around commas.
- Block kept only if ALL listed URL parameters exist and are non-empty.
- Nestable. No `#else`, no negation. Unbalanced blocks -> "ParseStatement: Syntax error".
- Once a statement contains `#if`, every line is trimmed and lines are re-joined with newlines.

Optional filter:
```sql
SELECT * FROM Users
WHERE 1=1
#if name
AND Name LIKE @name
#endif
```

Always exactly one row, filled when `id` is given (useful for PDF forms):
```sql
SELECT u.* FROM (SELECT 1 AS x) d
LEFT JOIN Users u ON 1 = 0
#if id
OR u.UserId = @id
#endif
```

## Other endpoint types
- TextFile / JsonApi: `#if` first, then `{{param}}` replacement (missing placeholders stay). JsonApi: statement = URL; values are not URL-encoded.
- PlainText: own parsing - look at an existing query with `get_query`.

# View flags

| Flag | Meaning |
|---|---|
| `pdfReportMode` | View is a paged PDF report (`@PDF.*`, export/print toolbar). `false` = HTML page/dashboard. |
| `pdfCompatibilityNotice` | PDF only: shows a browser compatibility notice to users. |
| `pdfReportEditing` | PDF only: visual editor (drag & drop, copy, shapes, form-input tool) when shown framed in DataLinq Code on Test/Development. **Not** end-user editing. |
| `cacheTokenTTL*`, `cacheTokenMaxUsage` | Lifetime / max uses of DataLinq cache tokens for this view. Leave unchanged unless asked. |

## Form inputs in PDF reports
- Use the DataLinq input markup (`<div class="dl-input dl-input-text" data-type="text" data-name="..."><label class="dl-input-label">..</label><input class="dl-input-control" ...></div>`; types: text, number, date, textarea, checkbox, radio, select, signature). The editor's "Inputs" tool creates exactly this.
- Values the user types in the browser **are** included in Download PDF / Print (pages are rendered as images from the live DOM); placeholders are hidden in the output.
- `data-required="true"` blocks Download/Print until filled. `data-default` pre-fills a value (e.g. from `Model.Records`).
- Values are not saved anywhere on the server.
