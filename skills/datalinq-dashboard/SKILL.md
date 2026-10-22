---
name: datalinq-dashboard
description: Create, modify and review DataLinq dashboards and web sites (Razor views with HTML/CSS/JavaScript using the @DLH helper, Model.Records and DLH.GetRecordsAsync) and the queries they use, on a live DataLinq instance. Use when the user asks to build/design/change a DataLinq dashboard, site, page, list, overview, chart, KPI view, form or map view (anything that is not a PDF report). Orchestrates datalinq-connect, datalinq-dashboard-data, datalinq-dashboard-query, datalinq-dashboard-author, datalinq-dashboard-from-image, datalinq-dashboard-edit and datalinq-dashboard-review.
user-invocable: true
disable-model-invocation: false
---

# DataLinq Dashboard (orchestrator)

You help DataLinq administrators build **HTML views** (`endpoint@query@view`, PDF report mode off): Razor + HTML + CSS + JS, bound to `Model.Records`, extra data via `DLH.GetRecordsAsync`, interactivity via `DLH.*` helpers and JS. Talk in plain language. For PDF reports use `datalinq-pdf-report` instead.

This skill only routes and runs the loop. Load a sub-skill only when you reach its step.

## Routing

| Situation | Sub-skill |
|---|---|
| Tools missing, 401, refused, first call | `datalinq-connect` |
| Which queries / which columns | `datalinq-dashboard-data` |
| A needed query does not exist or must change | `datalinq-dashboard-query` |
| New dashboard from a description | `datalinq-dashboard-author` |
| New dashboard from an image/mockup | `datalinq-dashboard-from-image` |
| Change an existing dashboard | `datalinq-dashboard-edit` |
| Check and show the result | `datalinq-dashboard-review` |

References (on demand): `datalinq-connect/references/datalinq-model.md`, `references/dashboard-patterns.md`, `assets/templates/*.cshtml`, `assets/dashboard-check.js`. Helper signatures: `get_dlh_reference` (never guess).

## Flow

1. `datalinq-connect` -> `whoami`. Guarded tools: the server shows the dialog or returns "Action refused" (then explain and stop).
2. `datalinq-dashboard-data` -> data sheet (all queries the dashboard needs).
3. Missing data -> `datalinq-dashboard-query` (new or changed query; user approves the SQL).
4. Create (`-author` / `-from-image`) or change (`-edit`). Before the first save, name the route; suggest `<view>-draft` if the view is in use; mention "Don't ask again for this target".
5. **Review loop** until the user is satisfied:
   1. `verify_view` (compile) - fix errors before saving.
   2. Save (`create_view` / `patch_view` / `patch_view_asset` / `save_view`) - user approves in the dialog.
   3. `datalinq-dashboard-review`: preview link, open, run `dashboard-check.js`, screenshots (desktop + narrow).
   4. Fix obvious issues yourself (max ~3 rounds without asking).
   5. Show the preview link + short summary (changed, checked, open points).
   6. Ask: **"Does this look right, or what should I change?"** -> on feedback `-edit` with the smallest patch, back to 5.1.
6. Done: final route and fresh preview link. Draft used -> copy to the real view only after the user agrees.

## Hard rules

- **DLH helpers first.** Forms, dialogs, delete/execute, filter, sort, search, export, reload: use the `DLH.*` helper (mapping in `references/dashboard-patterns.md`). Own JS only if no helper fits or the user explicitly wants it - then give a one-line reason and ask before writing it.
- **No external research.** Never search GitHub, the web or the DataLinq source code. Knowledge sources are only `get_dlh_reference`, the other MCP tools, these skills, their references and templates. If something is missing, ask the user.
- Never invent `DLH.*` / `Model.Records` methods - check `get_dlh_reference`.
- Never `@Html.Raw` database values; pass data to JS via `DLH.RecordsToJs` / `JsFetchData`, not string concatenation.
- No `@using`, no `System.` prefixes. `@` in CSS (`@media`, `@keyframes`) -> `@@`.
- External scripts/CSS only via the view's JS libraries setting (`update_view`) or libraries already used by the instance; no inline base64 images, no imitated logos (placeholder + ask for a URL).
- Never change endpoints, secrets, constants or other users' views. Change a query only through `datalinq-dashboard-query` and mention dependent views.
- Never work around a declined/refused approval. No delete tools exist.
- Keep old code / `oldText` of every change in the conversation for undo.
- Never call a dashboard finished based only on a compile.
- Never print or store access or preview tokens.
