---
name: datalinq-pdf-from-image
description: Rebuild a screenshot, scan, PDF page or mockup as a DataLinq PDF report template. Sub-skill of datalinq-pdf-report; analyses the image, maps placeholders to query columns and hands the structure to datalinq-pdf-author.
---

# DataLinq PDF From Image

Prerequisites: connected (`datalinq-connect`), data sheet (`datalinq-pdf-data`).

Follow `../datalinq-pdf-report/references/image-to-template.md` (analyse -> map placeholders -> choose structure -> build -> iterate).

Then draft with `datalinq-pdf-author` using the chosen structure, and run the orchestrator's review loop, comparing each rendered page with the original image region by region.

## Images and logos - HARD RULE

- Never extract images from the input, never embed base64/`data:image`/inline `<svg>` (the server rejects it), never imitate logos with CSS shapes, boxes, styled text or icon fonts.
- Use a dashed placeholder box of the exact size with the text "Logo" and ask once for a public image URL (`DLH.GetImage`) or ArcGIS/gView map (`DLH.GetAgsImage` / `SECURITY.GetAgsImage`).

## Rules

- Prioritise structure and correct data over pixel perfection from low-resolution images.
- Unmapped fields stay visible as `[TODO: field]` until the user clarifies them.
