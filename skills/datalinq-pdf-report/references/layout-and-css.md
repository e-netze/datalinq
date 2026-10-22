# Layout and CSS for DataLinq PDF reports

The report is rendered as HTML in the browser and converted to PDF client-side. What you see in the preview is what ends up in the PDF.

## Page geometry

- `.page` has a fixed size in mm (`A4` = 210 × 297 mm; landscape swaps them), `position: relative`, `overflow: hidden`, **no padding**. Content that does not fit is cut off unless the page is dynamic.
- Add your own margins with an inner wrapper:

```html
<div class="rpt rpt-body">  ... </div>
<style>
  .rpt-body { box-sizing:border-box; padding: 20mm 18mm 22mm 20mm; height:100%; }
</style>
```
- Use `mm`/`pt` units for print fidelity; `px` is fine for small offsets.
- Page numbers and date/time stamps are added at the bottom of every `.page` by `BeginReport` options; keep ~12 mm free at the bottom if you use them.

## Flow vs. positioned elements

- Default: normal HTML flow inside the page (headings, paragraphs, tables, CSS grid/flex). Preferred – robust and easy to maintain.
- `@PDF.NewDraggable(x, y)` creates `div.element { position:absolute }` for fixed spots (logo, address window, stamp, signature box). Give it an explicit width via inner element style. Keep draggables at the start of the page.

## Long tables / many records

Use a dynamic page instead of hard-coding rows per page:

```razor
@PDF.NewPage(dynamicTableOptions: new DynamicTableOptions { Dynamic = true, DefaultMarginTop = 40, DefaultMarginBottom = 60 })
  <table class="rpt-table">
	<thead><tr><th>…</th></tr></thead>
	<tbody>@foreach (var r in Model.Records) { <tr>…</tr> }</tbody>
  </table>
@PDF.EndPage()
```
- Tables must have a `<tbody>`; use `<thead>` so the header repeats.
- The table should be a **direct child of the page** (or the only table inside a draggable) to be split. Do not wrap it in extra padded divs – use `DefaultMarginTop/Bottom` (px) and table margins instead.
- Other overflowing block elements are moved as a whole to the next page.
- `@PDF.PageBreak()` forces a break (e.g. before a summary section).

## One page per record

```razor
@foreach (var r in Model.Records)
{
	@PDF.NewPage()
		…
	@PDF.EndPage()
}
```

## Shared letterhead / header / footer

Create a separate PDF view (e.g. `reports@layout@letterhead`) that contains one page with only the header/footer as draggables. Reference it from other reports with `PageTemplateOptions { UsePageTemplate = true, TemplateId = "reports@layout@letterhead" }` (+ `DynamicUseTemplate = true` on dynamic pages). The template is loaded via `/datalinq/select/<id>` so its query must succeed without parameters.

## Styling

- Put CSS in one `<style>` block in the first page; prefix all selectors with a report-specific class (`.rpt-xyz`) to avoid affecting DataLinq UI.
- Fonts: use system-safe fonts (`Arial, Helvetica, sans-serif`; `"Times New Roman", serif`). Web fonts need to be reachable and loaded before rendering. Set fonts explicitly on `p`, `td`, `th` (see pitfall below).
- Tables: `border-collapse: collapse; width: 100%;` `th, td { padding: 1.5mm 2mm; font-size: 9pt; }` `tr { page-break-inside: avoid; }`.
- Colors: printed backgrounds are kept (rendered to image), so light shades are fine.
- Images: see "Images" below. Always set explicit `width`/`height` in mm.

## Images (logos, photos, maps)

**Never write base64 / `data:` URIs or binary image content into the template yourself** - it costs huge amounts of tokens and stalls the agent. Never read, convert or embed image files the user attaches. Use only:

| Source | Helper |
|--------|--------|
| Public image (logo on a web server) | `@await DLH.GetImage(baseUrl, "path/logo.png", null, new { style = "width:30mm;height:15mm" })` |
| ArcGIS Server / gView map (public) | `@await DLH.GetAgsImage(AgsServiceType.MapService, serverUrl, "Folder/Service", new { size = "800,600", bbox = "...", format = "png" }, new { style = "width:180mm" })` |
| ArcGIS Server / gView map (secured) | `@await SECURITY.GetAgsImage(AgsServiceType.MapService, serverUrl, "Folder/Service", new { size = "800,600", bbox = "..." }, new AgsTokenObject { UsernameKey = "ags-user", PasswordKey = "ags-pwd" }, new { style = "width:180mm" })` |

- `baseUrl`/`serverUrl`: preferably `DLH.GetConstant("...")` (or `SECURITY.GetSecret`), not hard-coded. `path` must be relative (no leading `/`, no `..`).
- `AgsServiceType` is `MapService` or `ImageService`. `UsernameKey`/`PasswordKey` are **names of secrets**, never values.
- The helpers fetch the image on the server and emit an `<img>` - do not look at or copy the result.
- Layers: `DLH.OverlayImages(...)` can stack several helper images.

If the user wants an image but the source is unclear, **ask** which option they prefer:
1. Helper function - ask for the URL/constant (and for maps: service, bbox/parameters, secret names).
2. Placeholder - a box with the exact final size (`border:1px dashed #aaa`, text "Logo"/"Map") that the user replaces later.

Without an answer use the placeholder.
- Avoid `position: fixed`, viewport units (`vh`, `vw`), animations, and hover effects.

## Charts

Charts rendered by JS (e.g. Chart.js via the view's included JS libraries) work if they finish drawing before PDF generation. Keep them inside a draggable with fixed width/height. Ensure the needed JS library is enabled in the view's `included_js_libs`.

## Pitfall: DataLinq global CSS overrides fonts

The report page loads `css/datalinq/datalinq.css`, which contains:

    p, td, th { font-family: Arial; font-size: 14px; }
    html { font-size: 14px/16px; font-family: "Segoe UI", ... }

Font settings on a wrapper (`.rpt-x { font-size: 8pt }`) are only inherited,
so they LOSE against this rule. `p`, `td` and `th` end up at 14px Arial,
the content gets taller than the page, and because `.page` has `overflow: hidden`
the bottom of the page (signatures, footers) is cut off without any error.

Rules:
- Always set `font-family`, `font-size` and `line-height` explicitly on
  `p`, `td`, `th` inside the report scope, e.g.
  `.rpt-x p, .rpt-x td, .rpt-x th { font-family: Verdana, sans-serif; font-size: 8pt; line-height: 1.4; }`
- Do not rely on `em`/`rem` or inheritance for these elements.
- Do not use `rem` units at all (html font size changes between 14px and 16px at a viewport width of 768px).
- For fixed single-page forms, add up the height: content must fit into
  297 mm minus the page margins. With page numbers, leave about 12 mm free at the bottom.
- `verify_view` only checks compilation. Always check the rendered page (see below)
  before reporting success: no page may report `overflows: true`, `page_count` must match
  the expected number of `.page` elements, and `fonts` must show the intended font
  (`Arial 14px` = the global rule won).

## Checking the rendered result

Open the URL from the MCP tool `get_preview_link` with your browser tool (e.g. Playwright MCP), wait until pagination/decorations are done, then:

- Take a screenshot of every `.page` element and inspect each one visually.
- Evaluate `assets/render-check.js`: `pages[].overflows`, `clipped_elements` (text of cut-off elements), `hidden_overflow_containers`, `fonts` (computed `family size` → count), `broken_images` and a summary in `issues`.
- Check the browser console and failed network requests.

The preview shows the **stored** code.

## Checklist before saving

- [ ] Every page has content that fits, or the page is dynamic
- [ ] Bottom space reserved for page numbers
- [ ] No `null` exceptions for empty optional columns
- [ ] Empty result handled (`Model.Records.Length == 0`)
- [ ] All CSS scoped with a report class
- [ ] `p`, `td`, `th` have explicit font-family/size/line-height; no `rem`
- [ ] No `System.` prefixes

## Checklist after saving

- [ ] `render-check.js` reports `success: true`
- [ ] Every page screenshot looked at and matches the requirements/mockup
