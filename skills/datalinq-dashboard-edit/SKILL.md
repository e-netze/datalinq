---
name: datalinq-dashboard-edit
description: Change an EXISTING DataLinq dashboard/site view token-efficiently using get_view_outline, get_view_page, patch_view, get_view_assets and patch_view_asset instead of reading and rewriting the whole view. Sub-skill of datalinq-dashboard.
---

# Dashboard edit

1. **Outline.** `get_view_outline` -> `html-sections` with kinds (`style`, `script`, `razor-code`, `loop`, `html:section`, ...), line ranges and summaries (ids/classes, DLH calls, routes).
2. **Locate** the section(s). Separate view CSS/JS: `get_view_assets`. Other queries/views involved: `list_view_dependencies`.
3. **Read only that section** (`get_view_page`). `get_view` only for global restructuring.
4. **Patch.**
   - Markup/Razor: `patch_view` (`oldText` exact + unique, `newText`, `page`).
   - Separate CSS/JS: `patch_view_asset` (`asset` = css|js).
   - Compile check is automatic for `patch_view`; for bigger changes run `verify_view` first.
   The user approves each save (or "don't ask again" for this route).
5. **Undo info**: keep every `oldText`/`newText` pair.
6. Back to the review loop.

Rules: several small patches beat one rewrite; never touch unrelated sections; data changes go through `datalinq-dashboard-query`.
