---
name: datalinq-query
description: Create or change a DataLinq query (endpoint@query) that a dashboard or PDF report needs - SQL statement, parameters, test parameters, access. Shared sub-skill of datalinq-dashboard and datalinq-pdf-report; every create/update is approved by the user in a server-side dialog.
user-invocable: false
---

# DataLinq query

Prerequisite: connected (`datalinq-connect`), data sheet (`datalinq-data`). Workflow: `../datalinq-connect/references/workflow.md`.
Syntax: read `../datalinq-connect/references/query-syntax.md` (or `get_query_reference`) before writing a statement. Optional parameters always need `#if`.

## Steps

1. **Explore** with `run_sql_preview` (read-only SELECT/WITH, no `{{param}}`, literal test values, small `top`). Each run is approved individually - combine questions into few statements.
2. **Write the statement** in the endpoint's dialect. Parameter style as in sibling queries (`get_query` of a sibling shows it, e.g. `{{id}}` or `@id`). Explicit column aliases; aggregate in SQL rather than in Razor for large data.
3. **Impact.** Existing query: list dependent views (`list_views` / `list_view_dependencies`) and tell the user before the dialog. Never remove or rename columns other views use.
4. **Save.**
   - New -> `create_query` (`queryId` lower-case, short; `name`, `description`, `testParameters`). Access: same as sibling queries unless the user says otherwise.
   - Existing -> `update_query`. Store `previousStatement` in `changes.md`.
5. `sample_query` the result and update `data-sheet.md`.

## Statement kinds

- **Read queries** (lists, KPIs, report data): SELECT/WITH only.
- **Write queries** for dashboard CRUD (`save`, `delete` in the CRUD flow of `../datalinq-dashboard/references/dashboard-patterns.md`): INSERT/UPDATE/DELETE of single rows by key, only when the user asked for create/edit/delete and confirmed the target table. Always parameterised, always with a key in the WHERE clause.
- Never DDL (CREATE/ALTER/DROP/TRUNCATE), never multi-table bulk changes, never endpoint connection strings.

## Rules

- Declined -> ask the user, do not retry or split the change.
- PDF reports usually only need read queries; changing a query a report already uses needs the same impact check.
