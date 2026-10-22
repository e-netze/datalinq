---
name: datalinq-pdf-review
description: Check a saved DataLinq PDF report visually and show it to the user - preview link, open in a browser tab/side panel, run render-check.js, screenshot each page, list issues. Sub-skill of datalinq-pdf-report, used in every iteration of the review loop.
---

# DataLinq PDF Review

`verify_view` only proves the code compiles. Layout must be checked on the rendered, **saved** view.

## Steps

1. **Preview link.** `get_preview_link(endpointId, queryId, viewId, parameters)` with the parameters the user will use. Links are short-lived and single use - request a new one for each reload.
2. **Open it where the user can see it.**
   - With a browser tool (e.g. Playwright MCP): navigate to the URL.
   - VS Code: also open it in the Simple Browser / side panel so the user sees the same page.
   - Without any browser tool: give the user the link, say you could not check the layout yourself, and ask them to look.
3. **Automatic checks.** Evaluate `../datalinq-pdf-report/assets/render-check.js` on the page. Every entry in `issues` (cut-off content, wrong page count, broken images) is a bug. Check `fonts`: `Arial 14px` usually means DataLinq's global CSS won (see `layout-and-css.md`).
4. **Look.** Screenshot every `.page` element and compare with the description/mockup: fields present and filled, nothing overlapping or cut off, order, alignment, page breaks, header/footer, page numbers.
5. **Report** to the orchestrator in short form:
   - `ok` or a numbered list of issues, each with page number and the affected section (for `datalinq-pdf-edit`),
   - the screenshots / visible preview for the user.

## Rules

- Never call a report finished based only on `verify_view`.
- Never print preview tokens anywhere except the link handed to the user.
