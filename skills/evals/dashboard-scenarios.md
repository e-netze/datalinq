# Dashboard scenarios

Fixtures (endpoint `eval`):
- `eval@orders` (see `pdf-scenarios.md`) plus `eval@orders-by-month` (`MONTH`, `ORDER_COUNT`, `REVENUE`).
- `eval@customers` - `CUSTOMER_ID`, `NAME`, `CITY`, `ACTIVE`.
- `eval@tasks` - table `EVAL_TASKS` (`ID`, `TITLE`, `DUE`, `DONE`) on a database endpoint the test user may write to.

Process checks W1-W14 and red flags: `process-checks.md`.

## D1 - KPI dashboard (author)

**Prompt:** "Build a KPI dashboard for `eval@orders`: total revenue, number of orders, average order value, a chart per month and a table of the last 20 orders."
**Answers:** desktop first, must work on phones; no auto refresh.
**Checks:**
1. Three KPI cards with values matching the query (spot check one).
2. Chart from `eval@orders-by-month` (or a new aggregated query created with approval) - not computed from all rows in Razor.
3. Table with 20 rows, dates formatted, numbers right-aligned.
4. No horizontal overflow at 420 px; cards wrap.
5. `dashboard-check.js` reports no issues.
6. Data to JS via `DLH.RecordsToJs` / `JsFetchData`.

## D2 - Filter, sort, search, export (helpers first)

**Prompt:** "List of customers from `eval@customers` with a filter by city and active, sortable columns, quick search and Excel export."
**Checks:**
1. `DLH.FilterView`, `DLH.SortView`, `DLH.QuickSearch`, `DLH.ExportView` used (no hand-written equivalents).
2. Query has `#if` blocks for the filter parameters and `@_orderby` (changed only with approval, dependent views named).
3. List view embedded via `DLH.IncludeView`.
4. Helper mapping was shown to the user before code was written.

## D3 - CRUD

**Prompt:** "Page to manage `EVAL_TASKS`: list, new, edit, delete with confirmation."
**Checks:**
1. Page/list/form views as in the CRUD flow (`dashboard-patterns.md`).
2. `save` and `delete` queries are single-row, parameterised, keyed; no DDL; user confirmed the table.
3. Delete via `DLH.ExecuteNonQuery` with `confirmText`; edit/new via `DLH.OpenViewInDialog`.
4. A created task appears in the list after save (manual test).

## D4 - Rebuild from mockup

**Prompt:** "Rebuild this overview as a DataLinq dashboard for `eval@orders`." + mockup image with header, 4 KPI cards, chart left, table right.
**Checks:**
1. `layout.md` with regions, mapping, helper per feature; unmapped KPI asked about.
2. Screenshot at mockup width matches the region arrangement.
3. Narrow screenshot: chart and table stacked, no overflow.
4. Logo placeholder; no imitated logo.

## D5 - Edit an existing dashboard

Setup: D1 result copied to `eval@orders@kpi-eval-d5`.
**Prompt:** "In `eval@orders@kpi-eval-d5` add a search box to the table and change the card color to the company blue #0055a5."
**Checks:**
1. Outline + section reads only; `patch_view` / `patch_view_asset`, no full rewrite.
2. `DLH.QuickSearch` used.
3. Contrast of text on #0055a5 checked (white text) by the code reviewer.
4. `changes.md` updated; nothing else changed.
