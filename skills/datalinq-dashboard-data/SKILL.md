---
name: datalinq-dashboard-data
description: Find the DataLinq endpoint/queries a dashboard or site uses and learn their data (exact column names, sample rows, parameters, dependent views). Sub-skill of datalinq-dashboard; use before drafting or changing data bindings.
---

# Dashboard data

1. **Route.** Ask for or find the `endpoint@query@view`: `list_endpoints` -> `list_queries` -> `list_views`. Existing view: `list_view_dependencies` lists every other query/view it already uses.
2. **Queries.** For the main query and every extra query (`DLH.GetRecordsAsync`, JS fetch, included views): `get_query` (statement, parameters, test parameters).
3. **Sample.** `sample_query` with the test parameters (user approves; suggest "don't ask again" for this route). Note exact column names and types.
4. **Gaps.** If needed columns do not exist: look at the schema with `run_sql_preview` (database endpoints only, single SELECT, approved each time), then hand over to `datalinq-dashboard-query`.
5. **Data sheet** (keep in the conversation, short):

```
main  endpoint@query   params: id (required)   rows: ~1..n
  COL_A string  "..."   COL_B decimal 12.5   COL_C datetime
extra endpoint@query2  filter: ORDER_ID=<COL_A>   orderby: POS
  ...
```

Rules: never guess column names (case sensitive); keep rows out of the final answer unless asked; never show secrets from statements.
