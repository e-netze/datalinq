---
name: datalinq-pdf-author
description: Draft a NEW DataLinq PDF report template (Razor view with @PDF.* helper) from a text description or a built-in template. Sub-skill of datalinq-pdf-report; use after the data sheet from datalinq-pdf-data exists.
---

# DataLinq PDF Author

Prerequisites: connected (`datalinq-connect`), data sheet (`datalinq-pdf-data`).

Shared files (relative to this folder):
- `../datalinq-pdf-report/references/pdf-helper-api.md` - read before writing code; the ONLY allowed `PDF.*` API.
- `../datalinq-pdf-report/references/layout-and-css.md` - page sizes, positioning, tables across pages, fonts, images.
- `../datalinq-pdf-report/references/razor-model.md` - data binding, formatting, Razor pitfalls.
- `../datalinq-pdf-report/assets/templates/*.cshtml` - starting points.

## Steps

1. **Requirements.** Clarify only what is unclear: paper size/orientation, one page per record vs. list, header/footer/logo, page numbers, columns, grouping, totals, signature fields. Ask at most a few focused questions; otherwise use defaults (A4 portrait, page numbers `n/total` bottom-right) and state them.
2. **Pick a template.**

   | Need | Template |
   |---|---|
   | Simple single page | `01-minimal.cshtml` |
   | Letter / invoice with address window | `02-letter-invoice.cshtml` |
   | Long list / table | `03-table-list.cshtml` |
   | Cover + TOC + detail pages | `04-master-detail-cover-toc.cshtml` |
   | Shared letterhead | `05-letterhead-page-template.cshtml` |

3. **Draft** by adapting the template. Only columns from the data sheet; guard missing values. Logos/images: placeholder box (see orchestrator hard rule).
4. **Compile** with `verify_view` (new views: any not-yet-existing `viewId`). Fix and re-verify, max ~5 rounds, then show the error to the user.
5. Hand back to the orchestrator for saving and the review loop.

## Code rules

- Exactly one `@PDF.BeginReport(...)` / `@PDF.EndReport()`; every `NewPage` closed by `EndPage`, every `NewDraggable` by `EndDraggable`. No content outside pages.
- Prefer flowing HTML; `NewDraggable` only for fixed spots (logos, address window, stamps).
- Long tables: dynamic page (`DynamicTableOptions`), never guess rows per page.
- `@value` is encoded; `@Html.Raw` only for trusted static markup, never DB values.
- CSS in one `<style>` at the top of the first page, scoped with a report class (e.g. `.rpt-invoice`). Set `font-family`, `font-size`, `line-height` explicitly on `p`, `td`, `th`. No `rem`.
- No fully qualified `System.` names. No external resources unless confirmed reachable.
- Keep each page as a clearly separated block (`@PDF.NewPage(...)` on its own line, a short `@* Page: name *@` comment above it) so page-level tools can find and edit it later.
