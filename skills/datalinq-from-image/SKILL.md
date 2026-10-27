---
name: datalinq-from-image
description: Turn a screenshot, scan, PDF page, mockup or sketch into a layout spec (layout.md) for a DataLinq PDF report or dashboard - regions, placeholders mapped to query columns, structure and template choice - and hand it to the author skill. Shared sub-skill of datalinq-pdf-report and datalinq-dashboard.
user-invocable: false
---

# DataLinq from image

Prerequisites: connected (`datalinq-connect`), data sheet (`datalinq-data`). Workflow: `../datalinq-connect/references/workflow.md`.
Output: `.datalinq-work/<route>/layout.md`; no code, no saves.

## Steps

1. **Analyse** the image and write a region list (show it briefly to the user):
   - **PDF**: paper size/orientation (A4 portrait unless obviously different, ratio 1 : 1.414), regions top to bottom (header/logo/address window, meta block, body, table, totals, footer/signature/page number), approximate positions in mm (page width 210 mm), fonts, sizes (body 9-11 pt), lines, shading. Details: `../datalinq-pdf-report/references/image-to-template.md`.
   - **Dashboard**: regions (header, filter bar, KPI cards, charts, tables, detail panes), grid/columns, breakpoints, colors, fonts, spacing; mockup width in px.
2. **Map** every dynamic text/number/chart to a data-sheet column or query:

   | Region | Placeholder | Column / query | Format |
   |---|---|---|---|

   Missing data -> list it and ask, or hand over to `datalinq-query`. Unmapped values stay visible as `[TODO: field]`.
3. **Structure and template.**
   - **PDF**: flowing body + `NewDraggable` for fixed spots (address window, stamps); long tables -> dynamic page; template table in `datalinq-pdf-author`.
   - **Dashboard**: closest template and the DLH helper per feature (table in `../datalinq-dashboard/references/dashboard-patterns.md`); template list in `datalinq-dashboard-author`.
4. **Write `layout.md`**: region list, mapping table, structure, template, open questions. Add region-level items to the acceptance checklist in `brief.md` (e.g. "address window at 20/45 mm, 85 x 45 mm").
5. Hand over to the author skill of the mode. In review, the visual reviewer compares the rendered result with the image region by region.

## Images and logos - HARD RULE

- Never extract images from the input, never embed base64/`data:image`/inline `<svg>` (the server rejects it), never imitate logos with CSS shapes, boxes, styled text or icon fonts.
- Use a dashed placeholder box of the same size with the text "Logo" and ask once for a public image URL (`DLH.GetImage`) or a map service (`DLH.GetAgsImage` / `SECURITY.GetAgsImage`).

## Rules

- Prioritise structure and correct data over pixel perfection from low-resolution images.
