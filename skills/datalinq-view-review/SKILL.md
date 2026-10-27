---
name: datalinq-view-review
description: Review a saved DataLinq view (PDF report or dashboard/site) independently of its author - visual review (preview link, browser, check script, screenshots) and/or code review (static rules) against the acceptance checklist, and write review-<n>-*.md. Shared sub-skill of datalinq-pdf-report and datalinq-dashboard, used in every iteration of the review loop.
user-invocable: false
---

# DataLinq view review

Inputs: `brief.md` (acceptance checklist), `data-sheet.md`, `layout.md`, the route, the original image if any. **Not** the author's reasoning. Output: part A -> `review-<n>-visual.md`, part B -> `review-<n>-code.md`, in the format of `../datalinq-connect/references/workflow.md` section 4 (single-agent mode: do both parts, write both files).

`verify_view` only proves the code compiles. Layout and data must be checked on the rendered, **saved** view.

## A. Visual review

1. **Preview link.** `get_preview_link(endpointId, queryId, viewId, parameters)` with the parameters from `brief.md` / test parameters. Single use, open within ~2 min (then ~10 min browser session) - open it right away; request a new one for every reload and a fresh one for the user at the end.
2. **Open** it in the browser tool (e.g. Playwright MCP `browser_navigate`). VS Code: also open it in the Simple Browser / side panel so the user sees the same page. No browser tool: give the link to the user, say you could not check the layout yourself, ask for a screenshot.
3. **Check script** (browser `evaluate`):
   - **PDF**: `../datalinq-pdf-report/assets/render-check.js` - every entry in `issues` (cut-off content, wrong page count, broken images) is a finding. `fonts` showing `Arial 14px` usually means DataLinq's global CSS won (`../datalinq-pdf-report/references/layout-and-css.md`).
   - **Dashboard**: `../datalinq-dashboard/assets/dashboard-check.js` - JS errors, failed requests, empty tables/charts, `undefined`/`NaN`, horizontal overflow, broken images, Razor error text.
4. **Screenshots.**
   - **PDF**: every `.page` element.
   - **Dashboard**: ~1400 px and ~420 px width; with a mockup also at the mockup's width.
5. **Judge** item by item against the acceptance checklist, then look for: missing/empty fields, wrong formats vs. the data sheet, overlap/clipping, alignment, contrast, page breaks, header/footer/page numbers (PDF), responsive layout (dashboard). With an original image: compare region by region.

## B. Code review (no approvals needed)

Read with `get_view_outline` / `get_view_page` (and `get_view_assets` for dashboards). Check:
- API: every `PDF.*` call exists in `../datalinq-pdf-report/references/pdf-helper-api.md` / `get_pdf_reference`; every `DLH.*` / records method in `get_dlh_reference`.
- Data: only column names from `data-sheet.md`; null/empty handling; no per-row queries in loops where one query would do.
- Security: no `@Html.Raw` of database values; data to JS only via `DLH.RecordsToJs` / `JsFetchData`; no tokens, passwords or preview links in code; no base64/`data:image`/inline `<svg>` images, no imitated logos.
- Structure: PDF - exactly one `BeginReport`/`EndReport`, every `NewPage`/`NewDraggable` closed, page comments present. Dashboard - scoped CSS root class, `@@` in CSS at-rules, one script block, DLH helper used where `dashboard-patterns.md` maps one (own JS only with a confirmed reason).
- Accessibility (dashboard): labels on inputs, alt texts, contrast of chosen colors.

## Rules

- Do not change views or queries; do not fix things yourself - report them with a fix hint.
- Every finding cites a checklist item or concrete evidence. Verdict `pass` only without `high` findings.
- Never print preview tokens anywhere except the link handed to the user; never store them in files.
