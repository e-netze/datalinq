---
name: datalinq-pdf-data
description: Find the DataLinq endpoint/query a PDF report is based on and learn its data (exact column names, sample rows, parameters). Sub-skill of datalinq-pdf-report; use before drafting or changing a report's data bindings.
---

# DataLinq PDF Data

Prerequisite: connected (`datalinq-connect`).

## Steps

1. **Locate.** Ask, or discover with `list_endpoints` / `list_queries` / `list_views`, which endpoint and query the report is for. One view renders one query's `Model.Records`; extra data must come from other queries (see `../datalinq-pdf-report/references/razor-model.md`, "Multiple data sources").
2. **Sample.** Call `sample_query` with the view's test parameters (`get_query`) or typical parameters from the user. Use `top` 3-5; never more than needed.
3. **Record a data sheet** and keep it in the conversation - all later steps use it instead of re-sampling:

   | Column (exact, case-sensitive) | Example | Type/format | Nullable? |
   |---|---|---|---|

   Plus: route, parameters used, record count, whether the report is one page per record or a list.
4. Repeat for every additional query the report needs.

## Rules

- Use only column names from the data sheet; never guess them.
- Do not change queries or endpoints.
- Re-sample only if the user changes the query or parameters.

Query syntax (optional parameters, `#if`) and view flags incl. form inputs: `../datalinq-connect/references/query-syntax.md` / `get_query_reference`.
