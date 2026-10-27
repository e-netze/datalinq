---
name: datalinq-pdf-report
description: Create, modify and review DataLinq PDF report templates (Razor views with PDF report mode using the @PDF.* helper) on a live DataLinq instance. Use when the user asks to build/design/change a DataLinq PDF report, printable report, invoice, letter, protocol or form template from a text description, a screenshot/mockup image, or an existing template. Orchestrates datalinq-connect, datalinq-data, datalinq-query, datalinq-pdf-author, datalinq-from-image, datalinq-view-edit and datalinq-view-review.
user-invocable: true
disable-model-invocation: false
---

# DataLinq PDF Report (orchestrator)

You help DataLinq administrators (often non-programmers) build **PDF report views**: DataLinq Razor views (`endpoint@query@view`) in PDF report mode, wrapped in `@PDF.BeginReport()` ... `@PDF.EndReport()`, made of pages (`@PDF.NewPage()` ... `@PDF.EndPage()`) bound to `Model.Records`. Talk in plain, non-technical language. For dashboards/web pages use `datalinq-dashboard` instead.

This skill routes and runs the loop; it does not write view code itself. Load a sub-skill only when you reach its step.
Loop, work folder (`.datalinq-work/<route>/`), formats and definition of done: **`../datalinq-connect/references/workflow.md`** (read it first).

## Routing

| Situation | Sub-skill | Agent (multi-agent mode) |
|---|---|---|
| Tools missing, 401, refused, first call | `datalinq-connect` | orchestrator |
| Which query / which columns | `datalinq-data` (mode PDF) | `datalinq-data-analyst` |
| A needed query does not exist or must change | `datalinq-query` | `datalinq-query-engineer` |
| New report from an image/mockup | `datalinq-from-image` (mode PDF) | `datalinq-designer` |
| New report from a description | `datalinq-pdf-author` | `datalinq-view-coder` |
| Change an existing report | `datalinq-view-edit` | `datalinq-view-coder` |
| Check the result | `datalinq-view-review` (visual + code) | `datalinq-visual-reviewer` + `datalinq-code-reviewer` |

References (on demand): `../datalinq-connect/references/datalinq-model.md`, `references/pdf-helper-api.md`, `references/layout-and-css.md`, `references/razor-model.md`, `references/image-to-template.md`, `assets/templates/*.cshtml`, `assets/render-check.js`.

## Flow

1. **Connect**: `datalinq-connect` -> `whoami`.
2. **Brief**: clarify only what is unknown (paper size/orientation, one page per record vs. list, header/footer/logo, page numbers, columns, grouping, totals, signatures; otherwise defaults A4 portrait, `n/total` bottom-right). Write `brief.md` with the acceptance checklist and let the user confirm it once.
3. **Data**: `datalinq-data` -> `data-sheet.md` (skip if the change does not touch data). Missing data -> `datalinq-query`.
4. **Design/Code**: image -> `datalinq-from-image` -> `layout.md` -> `datalinq-pdf-author`; description -> `datalinq-pdf-author`; existing report -> `datalinq-view-edit`.
5. **Save**: before the first save name the route, suggest `<view>-draft` if the report is in use, mention "Don't ask again for this target". `verify_view` (pdfReportMode true) -> `create_view` / `patch_view` / `save_view`. Declined -> stop and ask.
6. **Review loop** (workflow.md section 5) until the definition of done is met:
   1. `datalinq-view-review` -> `review-<n>-*.md` (visual + code; independent of the author).
   2. Findings -> `datalinq-view-edit` with the smallest patches (max 3 automatic rounds).
   3. Show the user a fresh preview link and a short summary: what changed, what was checked, open findings.
   4. Ask: **"Does this look right, or what should I change?"** Feedback -> update the checklist in `brief.md` -> 6.2.
7. **Done**: final route, fresh preview link, where the undo log is. Draft used -> copy to the real view only after explicit confirmation.

Multiple templates: run 2-7 per template. Shared header/footer can live in a separate PDF view referenced via `PageTemplateOptions` (`references/pdf-helper-api.md`).

## Multi-agent mode

If the DataLinq custom agents are available (e.g. `datalinq-view-coder`, `datalinq-visual-reviewer`), delegate each role as in the routing table: pass the work-folder path and a one-line task, never your own reasoning. Run the visual and code reviewer in parallel; never two writers on the same view. Otherwise play all roles yourself in the order above.

## Hard rules (apply in every sub-skill and agent)

- Never invent `PDF.*` methods, options or enum values not listed in `references/pdf-helper-api.md`.
- **No external research.** Never search GitHub, the web or the DataLinq source code. Knowledge sources are only the MCP tools (`get_pdf_reference`, `get_dlh_reference` etc.), these skills, their references and templates. If something is missing, ask the user.
- **Images/logos:** never embed base64/`data:image`/inline `<svg>`, never extract images from attachments, never imitate logos with CSS. Use a dashed same-sized "Logo" placeholder and ask once for a public image URL (`DLH.GetImage`) or map service (`DLH.GetAgsImage` / `SECURITY.GetAgsImage`).
- Never work around a declined or refused approval. There are no delete tools; never delete views.
- Every overwrite/patch is logged in `changes.md` so it can be restored.
- Never call a report finished based only on a successful compile.
- Never print or store agent tokens or preview tokens in templates, files or logs.
