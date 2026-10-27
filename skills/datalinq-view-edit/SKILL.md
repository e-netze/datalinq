---
name: datalinq-view-edit
description: Change an EXISTING DataLinq view (PDF report or dashboard/site) token-efficiently using get_view_outline, get_view_page, patch_view, get_view_assets and patch_view_asset instead of reading and rewriting the whole view. Shared sub-skill of datalinq-pdf-report and datalinq-dashboard.
user-invocable: false
---

# DataLinq view edit

Prerequisite: connected (`datalinq-connect`). Data sheet (`datalinq-data`) only if the change touches data bindings. Workflow: `../datalinq-connect/references/workflow.md`.
Input: the change request or the findings of the last `review-<n>-*.md`. Output: saved patches + entries in `changes.md`.

## Steps

1. **Outline.** `get_view_outline(endpointId, queryId, viewId)` returns the sections without code:
   - **PDF** (`pdf-pages`): section `0` = before the first page (`@PDF.BeginReport`, variables, often `<style>`), `1..n` = pages (`@PDF.NewPage` ... `@PDF.EndPage`) with `startLine`/`endLine` and a summary, last = after the final page.
   - **Dashboard** (`html-sections`): kinds `style`, `script`, `razor-code`, `loop`, `condition`, `helper`, `html:<tag>`, ... with line ranges and summaries (ids/classes, DLH calls, routes). Separate view CSS/JS: `get_view_assets`. Other queries/views: `list_view_dependencies`.
2. **Locate** the affected section(s). Review findings name the page/section/selector - start there. CSS: PDF usually section 0 or page 1; dashboards the `style` section or the CSS asset.
3. **Read only that section** with `get_view_page`. `get_view` only for truly global restructuring.
4. **Patch.**
   - Markup/Razor: `patch_view` (`oldText` exact and unique - a few lines, `newText`, `page` = section number).
   - Dashboard separate CSS/JS: `patch_view_asset` (`asset` = `css` | `js`).
   - `patch_view` compiles before saving; on errors nothing is saved - fix and retry (max ~5 rounds). Run `verify_view` first for bigger changes.
   - Not found / not unique -> enlarge `oldText`.
   - The user approves each save (or "Don't ask again" for this route). Declined -> stop and ask.
5. **Undo log.** Append each `oldText`/`newText` pair to `changes.md`.
6. Hand back to the orchestrator (next: review). Do not judge your own result as done.

## Rules

- Several small patches beat one rewrite; never touch unrelated sections.
- Never change queries here (use `datalinq-query`), endpoints, other views, secrets or constants. Never delete views.
- Same code rules as the author skill of the mode (`datalinq-pdf-author` / `datalinq-dashboard-author`): no invented `PDF.*`/`DLH.*` API, no base64 images, no `System.` names, `@@` in CSS at-rules for dashboards.
