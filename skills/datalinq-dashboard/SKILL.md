---
name: datalinq-dashboard
description: Create, modify and review DataLinq dashboards and web sites (Razor views with HTML/CSS/JavaScript using the @DLH helper, Model.Records and DLH.GetRecordsAsync) and the queries they use, on a live DataLinq instance. Use when the user asks to build/design/change a DataLinq dashboard, site, page, list, overview, chart, KPI view, form or map view (anything that is not a PDF report). Orchestrates datalinq-connect, datalinq-data, datalinq-query, datalinq-dashboard-author, datalinq-from-image, datalinq-view-edit and datalinq-view-review.
user-invocable: true
disable-model-invocation: false
---

# DataLinq Dashboard (orchestrator)

You help DataLinq administrators build **HTML views** (`endpoint@query@view`, PDF report mode off): Razor + HTML + CSS + JS, bound to `Model.Records`, extra data via `DLH.GetRecordsAsync`, interactivity via `DLH.*` helpers and JS. Talk in plain language. For PDF reports use `datalinq-pdf-report` instead.

This skill routes and runs the loop; it does not write view code itself. Load a sub-skill only when you reach its step.
Loop, work folder (`.datalinq-work/<route>/`), formats and definition of done: **`../datalinq-connect/references/workflow.md`** (read it first).

## Routing

| Situation | Sub-skill | Agent (multi-agent mode) |
|---|---|---|
| Tools missing, 401, refused, first call | `datalinq-connect` | orchestrator |
| Which queries / which columns | `datalinq-data` (mode dashboard) | `datalinq-data-analyst` (one per query) |
| A needed query does not exist or must change | `datalinq-query` | `datalinq-query-engineer` |
| New dashboard from an image/mockup | `datalinq-from-image` (mode dashboard) | `datalinq-designer` |
| New dashboard from a description | `datalinq-dashboard-author` | `datalinq-view-coder` |
| Change an existing dashboard | `datalinq-view-edit` | `datalinq-view-coder` |
| Check the result | `datalinq-view-review` (visual + code) | `datalinq-visual-reviewer` + `datalinq-code-reviewer` |

References (on demand): `../datalinq-connect/references/datalinq-model.md`, `references/dashboard-patterns.md`, `assets/templates/*.cshtml`, `assets/dashboard-check.js`. Helper signatures: `get_dlh_reference` (never guess).

## Flow

1. **Connect**: `datalinq-connect` -> `whoami`. Guarded tools: the server shows the dialog or returns "Action refused" (then explain and stop).
2. **Brief**: clarify only what is unknown (purpose, audience, KPIs/lists/charts, filters, refresh, create/edit/delete, desktop vs. mobile). Write `brief.md` with the acceptance checklist and let the user confirm it once.
3. **Data**: `datalinq-data` -> `data-sheet.md` (all queries the dashboard needs). Missing data -> `datalinq-query` (user approves the SQL).
4. **Design/Code**: image -> `datalinq-from-image` -> `layout.md` -> `datalinq-dashboard-author`; description -> `datalinq-dashboard-author`; existing view -> `datalinq-view-edit`.
5. **Save**: before the first save name the route, suggest `<view>-draft` if the view is in use, mention "Don't ask again for this target". `verify_view` -> `create_view` / `patch_view` / `patch_view_asset` / `save_view`. Declined -> stop and ask.
6. **Review loop** (workflow.md section 5) until the definition of done is met:
   1. `datalinq-view-review` -> `review-<n>-*.md` (visual: desktop + narrow screenshots, `dashboard-check.js`; code: static rules).
   2. Findings -> `datalinq-view-edit` with the smallest patches (max 3 automatic rounds).
   3. Show the preview link + short summary (changed, checked, open findings).
   4. Ask: **"Does this look right, or what should I change?"** Feedback -> update the checklist in `brief.md` -> 6.2.
7. **Done**: final route, fresh preview link, where the undo log is. Draft used -> copy to the real view only after the user agrees.

## Multi-agent mode

If the DataLinq custom agents are available (e.g. `datalinq-view-coder`, `datalinq-visual-reviewer`), delegate each role as in the routing table: pass the work-folder path and a one-line task, never your own reasoning. Data analysts (one per query) and the two reviewers may run in parallel; approvals are still answered one at a time; never two writers on the same view. Otherwise play all roles yourself in the order above.

## Hard rules (apply in every sub-skill and agent)

- **DLH helpers first.** Forms, dialogs, delete/execute, filter, sort, search, export, reload: use the `DLH.*` helper (mapping in `references/dashboard-patterns.md`). Own JS only if no helper fits or the user explicitly wants it - then give a one-line reason and ask before writing it.
- **No external research.** Never search GitHub, the web or the DataLinq source code. Knowledge sources are only `get_dlh_reference`, the other MCP tools, these skills, their references and templates. If something is missing, ask the user.
- Never invent `DLH.*` / `Model.Records` methods - check `get_dlh_reference`.
- Never `@Html.Raw` database values; pass data to JS via `DLH.RecordsToJs` / `JsFetchData`, not string concatenation.
- No `@using`, no `System.` prefixes. `@` in CSS (`@media`, `@keyframes`) -> `@@`.
- External scripts/CSS only via the view's JS libraries setting (`update_view`) or libraries already used by the instance; no inline base64 images, no imitated logos (placeholder + ask for a URL).
- Never change endpoints, secrets, constants or other users' views. Change a query only through `datalinq-query` and mention dependent views.
- Never work around a declined/refused approval. No delete tools exist.
- Every overwrite/patch is logged in `changes.md` so it can be restored.
- Never call a dashboard finished based only on a compile.
- Never print or store agent tokens or preview tokens.
