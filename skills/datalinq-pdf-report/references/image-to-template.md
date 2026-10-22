# From an image to a DataLinq PDF template

Input can be a screenshot, a scanned paper form, a PDF page rendered as image, or a hand-drawn mockup.

## 1. Analyse the image

Write down (and show the user briefly):
- Paper size and orientation (A4 portrait unless obviously different; aspect ratio ≈ 1 : 1.414).
- Regions from top to bottom: header (logo, title, address window), meta block (date, number, reference), body (text, key/value grid, table), totals, footer (signature, bank details, page number).
- For each region: static text vs. data placeholder. Mark placeholders as `{{FIELD?}}`.
- Repeating parts: rows of a table → one record per row; whole page repeating → one page per record.
- Approximate positions in mm (measure relative to page width = 210 mm).
- Fonts (serif/sans), sizes (body ≈ 9–11 pt), colors, lines/borders, shaded areas.

## 2. Map placeholders to data

Run `sample_query` for the query (and any additional queries). Build a mapping table:

| Placeholder | Column | Format |
|-------------|--------|--------|
| Customer name | `CUSTOMER_NAME` | text |
| Invoice date | `INVOICE_DATE` | `dd.MM.yyyy` |

Ask the user only about placeholders you cannot map confidently. Unmapped values stay as clearly visible static text like `[TODO: field]` until clarified.

## 3. Choose structure

- Fixed-position form (official forms, letters with address window) → flowing body + `NewDraggable` for fixed spots.
- Lists/tables of variable length → dynamic page (`DynamicTableOptions { Dynamic = true }`).
- Mostly flowing documents → normal HTML flow with CSS grid/flex.

## 4. Build

1. Start from the closest asset template.
2. Reproduce layout with CSS grid/flex in mm units.
3. Insert Razor expressions for placeholders using safe access (`V(r, "COL")`).
4. Logos/photos/maps from the mockup: never extract or base64-encode them. Ask the user whether to use `DLH.GetImage` / `DLH.GetAgsImage` / `SECURITY.GetAgsImage` (see `layout-and-css.md`, "Images") or a same-sized placeholder box ("Logo") they replace later.
5. `verify_view`, save (user approves in the dialog, ideally to a draft view), then open `get_preview_link` in your browser tool, run `assets/render-check.js` and screenshot each `.page` next to the original image. Compare region by region (positions, sizes, fonts, line breaks, missing fields) and fix differences before showing the user the preview link.

## 5. Iterate

Ask for concrete differences ("title too large", "table needs a VAT column") and adjust. Do not try to achieve pixel perfection from low-resolution images – prioritise structure and correct data.
