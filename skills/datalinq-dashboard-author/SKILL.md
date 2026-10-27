---
name: datalinq-dashboard-author
description: Draft a NEW DataLinq dashboard/site view (Razor + HTML + CSS + JS with @DLH helpers) from a text description, a layout.md from an image or a built-in template. Sub-skill of datalinq-dashboard; use after brief.md and the data sheet exist.
user-invocable: false
---

# Dashboard author

Prerequisites: connected (`datalinq-connect`), `brief.md` with acceptance checklist, `data-sheet.md` (`datalinq-data`), optionally `layout.md` (`datalinq-from-image`). Workflow: `../datalinq-connect/references/workflow.md`.
Output: the saved view + entries in `changes.md` (+ `layout.md` if it did not exist yet).

1. **Read the inputs** (`brief.md`, `data-sheet.md`, `layout.md`). Anything still unclear (purpose, KPIs, filters, refresh, desktop vs. mobile): ask the orchestrator/user, do not guess.
2. **Start from a template** (mandatory: open the closest template before writing) in `../datalinq-dashboard/assets/templates/`:
   - `01-kpi-cards.cshtml` - KPI cards + table from `Model.Records`
   - `02-master-detail.cshtml` - list + detail via `DLH.GetRecordsAsync`
   - `03-chart.cshtml` - chart via `DLH.RecordsToJs` + JS
   - `04-filter-list.cshtml` - filter/quick search + sortable list
   - `05-crud.cshtml` + `05-crud-form.cshtml` - list with FilterView/SortView, new/edit dialog, delete (create/edit/delete records)
   Patterns: `../datalinq-dashboard/references/dashboard-patterns.md` (helper table + CRUD flow).
3. **Map features to helpers (mandatory).** For every requested feature pick the helper from the table in `dashboard-patterns.md` and note what the query needs. Write the mapping into `layout.md` and show it to the user before writing code; any feature without a helper (own JS) must be named with a reason and confirmed. Then `get_dlh_reference method=<name>` for exact signatures before using any `DLH.*` call. Never look into source code or the web.
4. **Write** the view: one `<style>` block (scoped with a root class, e.g. `.dl-dash`), markup, one `<script>` block at the end. Bind exactly the data-sheet column names; handle empty results and `null`. Each region gets a short `@* Region: name *@` comment so outline/patch tools and reviewers can find it.
5. **Compile** with `verify_view` (pdfReportMode false). Fix errors (max ~5 rounds), then show the error to the user.
6. **Create** with `create_view` (user approves). Larger CSS/JS can go into the view's separate CSS/JS via `update_view`. Note "created" in `changes.md`.
7. Hand back to the orchestrator review loop. Do not review or approve your own result.

Rules: no frameworks the instance does not provide; vanilla JS preferred; responsive grid (`display:grid; grid-template-columns: repeat(auto-fit,minmax(220px,1fr))`); accessible colors/contrast; no inline base64 images.
