---
name: datalinq-dashboard-query
description: Create or change a DataLinq query (endpoint@query) that a dashboard needs - SQL statement, parameters, test parameters, access. Sub-skill of datalinq-dashboard; every create/update is approved by the user in a server-side dialog.
---

# Dashboard query

1. **Explore** with `run_sql_preview` (read-only SELECT/WITH, no `{{param}}`, literal test values). Keep result sizes small (`top`).
2. **Write the statement** in the endpoint's dialect. Parameters as used by the existing queries of the endpoint (`get_query` of a sibling query shows the style, e.g. `{{id}}` or `@id`). Prefer explicit column aliases; aggregate in SQL rather than in Razor for large data.
3. **New query** -> `create_query` (`queryId` lower-case, short; `name`, `description`, `testParameters`). Access: same as sibling queries unless the user says otherwise.
   **Existing query** -> `update_query`. The dialog shows the SQL diff and dependent views; tell the user beforehand which views depend on it (`list_views` / `list_view_dependencies`). Keep `previousStatement` for undo. Never remove or rename columns used by other views.
4. `sample_query` the result and update the data sheet.

Rules: SELECT-only intent - never write DML/DDL into a dashboard query; never touch endpoint connection strings; declined -> ask, do not retry.

Syntax: read `../datalinq-connect/references/query-syntax.md` (or `get_query_reference`) before writing a statement. Optional parameters always need `#if`.
