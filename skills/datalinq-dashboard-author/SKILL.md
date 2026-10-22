---
name: datalinq-dashboard-author
description: Draft a NEW DataLinq dashboard/site view (Razor + HTML + CSS + JS with @DLH helpers) from a text description or a built-in template. Sub-skill of datalinq-dashboard; use after the data sheet exists.
---

# Dashboard author

1. **Clarify** briefly (only what is unknown): purpose, audience, main KPIs/lists/charts, filters, refresh, desktop vs. mobile.
2. **Start from a template** (mandatory: open the closest template before writing) in `../datalinq-dashboard/assets/templates/`:
   - `01-kpi-cards.cshtml` - KPI cards + table from `Model.Records`
   - `02-master-detail.cshtml` - list + detail via `DLH.GetRecordsAsync`
   - `03-chart.cshtml` - chart via `DLH.RecordsToJs` + JS
   - `04-filter-list.cshtml` - filter/quick search + sortable list
   - `05-crud.cshtml` + `05-crud-form.cshtml` - list with FilterView/SortView, new/edit dialog, delete (create/edit/delete records)
   Patterns: `../datalinq-dashboard/references/dashboard-patterns.md` (helper table + CRUD flow).
3. **Map features to helpers (mandatory).** For every requested feature pick the helper from the table in `dashboard-patterns.md` and note what the query needs. Show this list to the user before writing; any feature without a helper (own JS) must be named with a reason and confirmed. Then `get_dlh_reference method=<name>` for exact signatures before using any `DLH.*` call. Never look into source code or the web.
4. **Write** the view: one `<style>` block (scoped with a root class, e.g. `.dl-dash`), markup, one `<script>` block at the end. Bind exactly the data-sheet column names; handle empty results and `null`.
5. **Compile** with `verify_view` (pdfReportMode false). Fix errors (max ~5 rounds).
6. **Create** with `create_view` (user approves). Larger CSS/JS can go into the view's separate CSS/JS via `update_view`.
7. Hand back to the orchestrator review loop.

Rules: no frameworks the instance does not provide; vanilla JS preferred; responsive grid (`display:grid; grid-template-columns: repeat(auto-fit,minmax(220px,1fr))`); accessible colors/contrast; no inline base64 images.
