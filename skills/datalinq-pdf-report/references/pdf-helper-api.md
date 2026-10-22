# PDF helper API (`PDF` / `DataLinqPdfHelper`)

Source of truth: `src/nuget/E.DataLinq.Web/Razor/DataLinqPdfHelper.cs`, option classes in
`src/nuget/E.DataLinq.Web/Models/Razor/DataLinqHelperOptionClasses.cs`, enums in
`src/nuget/E.DataLinq.Web/Razor/Enums.cs`. Live documentation: `<DATALINQ_URL>/datalinq/help` (section "PDF").

Use ONLY the members listed here. All methods return raw HTML and are called with `@PDF.Method(...)`.

## Document structure

```
@PDF.BeginReport(...)          // exactly once, first
	@PDF.NewPage(...)          // 1..n pages
		...HTML...
		@PDF.PageBreak()       // optional, manual break inside a page
		@PDF.NewDraggable(...) // optional absolutely positioned element
			...HTML...
		@PDF.EndDraggable()
	@PDF.EndPage()
@PDF.EndReport()               // exactly once, last
```

Razor loops/conditions may wrap `NewPage`/`EndPage` (e.g. one page per record), as long as every opened page is closed in the same iteration.

## Methods

### `BeginReport(pageNumberOptions = null, pageDateTimeOptions = null, quality = PdfQuality.High, download_button = false, fileName = "dataLinqPdfReport")`
Opens the report container.
- `pageNumberOptions` (`PageNumberOptions`) – automatic page numbers.
- `pageDateTimeOptions` (`PageDateTimeOptions`) – date/time stamp on each page (server time at render).
- `quality` (`PdfQuality`): `Best`, `High`, `Medium`, `Low`, `Preview`.
- `download_button` (bool) – shows a download button container.
- `fileName` (string) – PDF file name without extension.

### `EndReport()`
Closes the report container.

### `NewPage(pageTemplateOptions = null, dynamicTableOptions = null, paperSize = PaperSize.A4, landscape = false)`
Opens a page.
- `paperSize` (`PaperSize`): `A1`, `A2`, `A3`, `A4`, `A5`, `A6`.
- `landscape` (bool).
- `pageTemplateOptions` (`PageTemplateOptions`) – overlays another view's page content (header/footer/letterhead).
- `dynamicTableOptions` (`DynamicTableOptions`) – enables automatic pagination: overflowing content is moved to continuation pages; tables are split row by row with repeated header.

### `EndPage()`
Closes the current page.

### `PageBreak()`
Manual page break inside a page. Content below moves to a new page that inherits template, dynamic options, paper size and orientation.

### `NewDraggable(x = 0, y = 0, layer = 0)` / `EndDraggable()`
Absolutely positioned element (`div.element`, `position:absolute`). `x`/`y` are pixel offsets applied via `transform: translate(x,y)` relative to the element's static position (top-left of the page if it is the first content). `layer` raises z-index (base 3 + layer).

### `PrintPdfButton(id, buttonText = "Print", parameters = null)`
Button that silently downloads the PDF of view `id` (`ep@q@v`). `parameters` is a `Dictionary<string,string>` of query parameters. Use in normal (non-PDF) views to offer a download of a PDF report.

### QR codes (return an `<img>` with base64 PNG)
- `GenerateUrlQRCode(string url, object htmlAttributes = null)`
- `GenerateMailQRCode(MailQrCodeDetails mailDetails, object htmlAttributes = null)`
- `GenerateGeoLocationQRCode(GeoLocationQrCodeDetails geoLocationDetails, object htmlAttributes = null)`
- `GenerateGiroQRCode(GiroQrCodeDetails giroCodeDetails, object htmlAttributes = null)`

`htmlAttributes` is an anonymous object, e.g. `new { style = "width:30mm" }`.

## Option classes

All option parameters accept either `new XxxOptions { ... }` or a `Dictionary<string, object>` (implicit conversion). The `new` form is preferred because it is compile-checked.

| Class | Property | Type | Default | Meaning |
|-------|----------|------|---------|---------|
| `PageNumberOptions` | `UsePageNumbers` | bool | false | enable |
| | `Position` | int | 0 | 0 = left, 1 = center, 2 = right (bottom of page) |
| | `Type` | int | 0 | 0 = "n/total", other = "n" |
| | `SkipPages` | int | 0 | first N pages get no number (and are not counted) |
| | `OffsetX`, `OffsetY` | int | 0 | px offset (x right, y down) |
| `PageDateTimeOptions` | `UsePageDateTime` | bool | false | enable |
| | `Position` | int | 0 | 0/1/2 as above |
| | `Format` | string | `"HH:mm dd.MM.yyyy"` | .NET date format |
| | `SkipPages`, `OffsetX`, `OffsetY` | int | 0 | as above |
| `PageTemplateOptions` | `UsePageTemplate` | bool | false | enable |
| | `TemplateId` | string | "" | `ep@q@v` of another PDF view; the content of its first page is appended to this page |
| `DynamicTableOptions` | `Dynamic` | bool | false | enable automatic pagination |
| | `DynamicUseTemplate` | bool | false | continuation pages also get the page template |
| | `DefaultMarginTop` | double | 10 | px free space at top of continuation pages |
| | `DefaultMarginBottom` | double | 10 | px free space at bottom of each page |
| | `RepeatHeader` | bool | true | repeat table header on continuation pages |
| `MailQrCodeDetails` | `reciever`, `subject`, `body` | string | | (note spelling `reciever`) |
| | `mailEncoding` | `QrCodeMailEncoding` | MAILTO | `MAILTO`, `MATMSG`, `SMTP` |
| `GeoLocationQrCodeDetails` | `latitude`, `longitude` | string | | |
| | `geolocationEncoding` | `QrCodeGeolocationEncoding` | GEO | `GEO`, `GoogleMaps` |
| `GiroQrCodeDetails` | `iban`, `bic`, `name`, `purpose` | string | | |
| | `amount` | decimal | 0 | |

## Examples

```razor
@PDF.BeginReport(
	pageNumberOptions: new PageNumberOptions { UsePageNumbers = true, Position = 2 },
	pageDateTimeOptions: new PageDateTimeOptions { UsePageDateTime = true, Position = 0, Format = "dd.MM.yyyy" },
	fileName: "Protokoll")

	@PDF.NewPage(dynamicTableOptions: new DynamicTableOptions { Dynamic = true, DefaultMarginTop = 40, DefaultMarginBottom = 50 })
		<table>...</table>
	@PDF.EndPage()

@PDF.EndReport()
```

```razor
@PDF.NewPage(pageTemplateOptions: new PageTemplateOptions { UsePageTemplate = true, TemplateId = "reports@layout@letterhead" })
```

```razor
@PDF.GenerateUrlQRCode("https://example.com/order/" + r["ORDER_ID"], new { style = "width:25mm;height:25mm" })
```

The namespaces `E.DataLinq.Web.Razor` and `E.DataLinq.Web.Models.Razor` are imported automatically by the DataLinq compiler, so `PageNumberOptions`, `PaperSize`, `PdfQuality`, etc. can be used unqualified. Do NOT add `@using` lines – they are rejected by the compiler unless the instance explicitly allows them.
