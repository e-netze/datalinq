---
name: datalinq-pdf-edit
description: Change an EXISTING DataLinq PDF report view token-efficiently, page by page, using get_view_outline, get_view_page and patch_view instead of reading and rewriting the whole view. Sub-skill of datalinq-pdf-report.
---

# DataLinq PDF Edit

Prerequisite: connected (`datalinq-connect`). Need the data sheet (`datalinq-pdf-data`) only if the change touches data bindings.

## Steps

1. **Outline.** Call `get_view_outline(endpointId, queryId, viewId)`. It returns the sections of the view without code:
   - section `0` = code before the first page (`@PDF.BeginReport`, usings, variables, often the `<style>` block),
   - sections `1..n` = pages in order (`@PDF.NewPage` ... `@PDF.EndPage`), including `startLine`/`endLine` and a short summary (page comment, first heading, options),
   - a last section after the final page (`@PDF.EndReport`), if not empty.
   Explain the structure to the user in one or two sentences if they don't know it.
2. **Locate.** Decide which section(s) the change affects. CSS usually lives in section 0 or page 1.
3. **Read only that section** with `get_view_page(endpointId, queryId, viewId, page)`. Read another section only if really needed. Use `get_view` (full code) only if the change is truly global (e.g. restructuring all pages).
4. **Patch.** Call `patch_view` with:
   - `oldText`: an exact snippet copied from the section, just large enough to be unique (a few lines),
   - `newText`: the replacement,
   - `page`: the section number to restrict the search (recommended),
   - the server shows the user an approval dialog; if declined, do not retry.
   The server fails if `oldText` is not found or matches more than once - then enlarge the snippet. It compiles before saving; on compiler errors nothing is saved and errors are returned. Fix and retry (max ~5 rounds).
5. **Undo info.** Keep each `oldText`/`newText` pair in the conversation so any change can be reverted with a reverse `patch_view`.
6. Hand back to the orchestrator review loop.

## Rules

- Several small patches are better than one big rewrite.
- Never touch queries, endpoints, other views' CSS, secrets or constants. Never delete views.
- Same code rules as `datalinq-pdf-author` (no invented `PDF.*` API, no base64 images, no `System.` names).
