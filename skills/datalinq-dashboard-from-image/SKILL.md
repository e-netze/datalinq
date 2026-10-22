---
name: datalinq-dashboard-from-image
description: Rebuild a screenshot, mockup or sketch of a web page/dashboard as a DataLinq dashboard view. Sub-skill of datalinq-dashboard; analyses the image, maps placeholders to query columns and hands the structure to datalinq-dashboard-author.
---

# Dashboard from image

1. **Analyse** the image: regions (header, filter bar, KPI cards, charts, tables, detail panes), grid/columns, colors, fonts, spacing. Write a short region list.
2. **Map** every dynamic text/number/chart to a data-sheet column or query (`datalinq-dashboard-data`). Missing -> list them and ask, or `datalinq-dashboard-query`.
3. **Static parts**: logos/photos -> dashed placeholder of the same size + ask once for a public URL. Never trace or embed images.
4. **Structure** -> `datalinq-dashboard-author` (pick the closest template).
5. In review, screenshot at the same width as the mockup and compare region by region; fix differences before showing the user.
