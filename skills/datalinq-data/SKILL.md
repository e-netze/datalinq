---
name: datalinq-data
description: Find the DataLinq endpoint/queries a PDF report or dashboard uses and learn their data (exact column names, types, sample rows, parameters, dependent views) and write the data sheet. Shared sub-skill of datalinq-pdf-report and datalinq-dashboard; use before drafting or changing data bindings.
user-invocable: false
---

# DataLinq data

Prerequisite: connected (`datalinq-connect`). Workflow, work folder and formats: `../datalinq-connect/references/workflow.md`.
Output: `.datalinq-work/<route>/data-sheet.md` (or the same table in the conversation in single-agent mode without files).

## Steps

1. **Route.** Ask for or discover the `endpoint@query@view`: `list_endpoints` -> `list_queries` -> `list_views`. Existing view: `list_view_dependencies` lists every other query/view it already uses (`DLH.GetRecordsAsync`, `IncludeView`, JS fetch).
2. **Queries.** For the main query and every extra query: `get_query` (statement, parameters, test parameters). Note the parameter style (`{{id}}`, `@id`, `#if`).
3. **Sample.** `sample_query` with the test parameters or typical parameters from the user, `top` 3-5 (user approves; suggest "Don't ask again" for this route). One call per query - multiple queries may be sampled by parallel analysts, but approvals stay one at a time.
4. **Gaps.** Needed columns missing: inspect the schema with `run_sql_preview` (database endpoints only, single SELECT/WITH, approved every time), then hand over to `datalinq-query`. Do not change queries here.
5. **Write the data sheet.**

```markdown
# Data sheet - <route>
## main  <endpoint@query>   params: id=4711 (required)   rows: 1 per order   used as: Model.Records
| Column (exact) | Type/format | Nullable | Example (masked) |
|---|---|---|---|
| ORDER_NO | string | no | "A-1001" |
| ORDER_DATE | datetime | no | 2024-03-01 |
## extra  <endpoint@query2>   filter: ORDER_ID=<ORDER_NO>   orderby: POS   used via: DLH.GetRecordsAsync
| ... |
Dependent views: <list from list_views / list_view_dependencies>
```

## Mode notes

- **PDF**: note whether the report is *one page per record* or *a list*; one view renders one query's `Model.Records`, extra data comes from other queries (`../datalinq-pdf-report/references/razor-model.md`, "Multiple data sources").
- **Dashboard**: list every query the page, its included views and its JS fetches need; note which ones need `#if` filter parameters or `@_orderby` (helpers in `../datalinq-dashboard/references/dashboard-patterns.md`).

## Rules

- Never guess column names (case sensitive) - only names from the data sheet are used later.
- Max 3 example rows per query in the file; mask personal data; never copy secrets from statements.
- Re-sample only if a query or the parameters change.
- Query syntax (optional parameters, `#if`): `../datalinq-connect/references/query-syntax.md` / `get_query_reference`.
