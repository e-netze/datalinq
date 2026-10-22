---
name: datalinq-pdf-report
description: Create, modify and review DataLinq PDF report templates (Razor views with PDF report mode using the @PDF.* helper) on a live DataLinq instance. Use when the user asks to build/design/change a DataLinq PDF report, printable report, invoice, letter, protocol or form template from a text description, a screenshot/mockup image, or an existing template. Orchestrates the datalinq-connect, datalinq-pdf-data, datalinq-pdf-author, datalinq-pdf-from-image, datalinq-pdf-edit and datalinq-pdf-review skills.
user-invocable: true
disable-model-invocation: false
---

# DataLinq PDF Report (orchestrator)

You help DataLinq administrators (often non-programmers) build **PDF report views**: DataLinq Razor views (`endpoint@query@view`) in PDF report mode, wrapped in `@PDF.BeginReport()` ... `@PDF.EndReport()`, made of pages (`@PDF.NewPage()` ... `@PDF.EndPage()`) bound to `Model.Records`. Talk in plain, non-technical language.

This skill only routes and runs the loop. Load a sub-skill only when you reach its step.

## Routing

| Situation | Sub-skill |
|---|---|
| Tools missing, 401, first call | `datalinq-connect` |
| Which query / which columns | `datalinq-pdf-data` |
| New report from a description | `datalinq-pdf-author` |
| New report from an image/mockup | `datalinq-pdf-from-image` |
| Change an existing report | `datalinq-pdf-edit` |
| Check and show the result | `datalinq-pdf-review` |

Shared references (load on demand): `datalinq-connect/references/datalinq-model.md`, `references/pdf-helper-api.md`,

## Flow

1. `datalinq-connect` -> `whoami`.
2. `datalinq-pdf-data` -> data sheet (skip if the change does not touch data).
3. Create (`datalinq-pdf-author` / `datalinq-pdf-from-image`) or change (`datalinq-pdf-edit`).
4. **Saving**: every save, query run and preview link is approved by the user in a **server-side dialog** (see `datalinq-connect` section 3). Before the first save say which route you will write to and suggest a draft view `<view>-draft` if the report is in use, and mention the "Don't ask again for this target" option for the review loop. Use `create_view` (new), `patch_view` (changes) or `save_view` (full rewrite). If the user declines, stop and ask what to do.
5. **Review loop** - repeat until the user says it is OK:
   1. Save (step 4 rules).
   2. `datalinq-pdf-review`: open the preview in the browser tab / side panel, run checks, screenshot.
   3. Fix obvious issues found by the checks yourself first (max ~3 rounds without asking).
   4. Show the user the preview and a short summary: what changed, what was checked, remaining differences.
   5. Ask: **"Does this look right, or what should I change?"**
   6. On feedback: `datalinq-pdf-edit` with the smallest possible patch, then back to 5.1.
6. Done: give the final route and a fresh preview link. If a draft view was used, copy it to the real view after explicit confirmation.

Multiple templates: run 3-6 per template. Shared header/footer can live in a separate PDF view referenced via `PageTemplateOptions` (`pdf-helper-api.md`).

## Hard rules (apply in every sub-skill)

- Never invent `PDF.*` methods, options or enum values not listed in `references/pdf-helper-api.md`.
- **No external research.** Never search GitHub, the web or the DataLinq source code. Knowledge sources are only the MCP tools (`get_dlh_reference` etc.), these skills, their references and templates. If something is missing, ask the user.
- **Images/logos:** never embed base64/`data:image`/inline `<svg>`, never extract images from attachments, never imitate logos with CSS. Use a dashed same-sized "Logo" placeholder and ask once for a public image URL (`DLH.GetImage`) or map service (`DLH.GetAgsImage` / `SECURITY.GetAgsImage`).
- Never work around a declined or refused approval. There are no delete tools; never delete views.
- Before any overwrite keep the old code (or `oldText` of patches) in the conversation so it can be restored.
- Never call a report finished based only on a successful compile.
- Never print or store access or preview tokens in templates, files or logs.
