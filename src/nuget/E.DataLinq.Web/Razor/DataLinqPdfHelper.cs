using E.DataLinq.Web.Html;
using E.DataLinq.Web.Html.Abstractions;
using E.DataLinq.Web.Html.Extensions;
using E.DataLinq.Web.Models.Razor;
using E.DataLinq.Web.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace E.DataLinq.Web.Razor;

/// <summary>
/// de: Die Klasse ist eine Hilfsklasse für PDF-Berichte, die innerhalb der Razor-Umgebung von DataLinq genutzt werden kann. Der Zugriff erfolgt über den globalen Namen DataLinqPdfHelper bzw. der Kurzform PDF.
/// en: This class is a helper class for PDF reports that can be used within the Razor environment of DataLinq. It is accessed through the global name DataLinqPdfHelper or the shorthand PDF.
/// The methods in this class allow building multi-page PDF reports within a DataLinq view, including page layout, elements, and print/download buttons.
/// </summary>
public class DataLinqPdfHelper
{
    private readonly IRazorCompileEngineService _razor;

    public DataLinqPdfHelper(IRazorCompileEngineService razorService)
    {
        _razor = razorService ?? throw new ArgumentNullException(nameof(razorService));
    }

    #region DataLinq PDF Helper

    /// <summary>
    /// de: Startet einen PDF-Bericht und gibt den öffnenden HTML-Container zurück.
    /// en: Starts a PDF report and returns the opening HTML container.
    /// </summary>
    /// <param name="pageNumberOptions">
    /// de: Optionen für die Seitennummerierung (z.B. Position, Typ, übersprungene Seiten). Standard ist null.
    /// en: Options for page numbering (e.g. position, type, skipped pages). Default is null.
    /// </param>
    /// <param name="quality">
    /// de: Die Qualitätsstufe des erzeugten PDFs. Standard ist PdfQuality.High.
    /// en: The quality level of the generated PDF. Default is PdfQuality.High.
    /// </param>
    /// <param name="download_button">
    /// de: Gibt an, ob ein Download-Button angezeigt werden soll. Standard ist false.
    /// en: Indicates whether a download button should be displayed. Default is false.
    /// </param>
    /// <param name="fileName">
    /// de: Der Dateiname des generierten PDFs (ohne Dateiendung). Standard ist "dataLinqPdfReport".
    /// en: The file name of the generated PDF (without file extension). Default is "dataLinqPdfReport".
    /// </param>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit dem öffnenden Container des PDF-Berichts zurück.
    /// en: Returns a raw HTML object with the opening container of the PDF report.
    /// </returns>
    public object BeginPdfReport(
        PageNumberOptions pageNumberOptions = null,
        PdfQuality quality = PdfQuality.High,
        bool download_button = false,
        string fileName = "dataLinqPdfReport"
        )
    {
        pageNumberOptions ??= new PageNumberOptions();

        bool usePageNumbers = pageNumberOptions.UsePageNumbers;
        int position = pageNumberOptions.Position;
        int type = pageNumberOptions.Type;
        int skipPages = pageNumberOptions.SkipPages;

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(dOptions =>
                {
                    if (usePageNumbers)
                    {
                        dOptions.AddClass("pdf-report-options");
                        dOptions.AddAttribute("data-type", type.ToString());
                        dOptions.AddAttribute("data-skipPages", skipPages.ToString());
                        dOptions.AddAttribute("data-position", position.ToString());
                    }

                })
                .AppendDiv(d =>
                {
                    if (download_button)
                    {
                        d.AppendButton(button =>
                        {
                            button.WithId("downloadBtn");
                            button.Content("Download PDF");
                            button.AddClass("datalinq-button-pdf");
                        });
                    }
                    d.AddAttribute("fileName", fileName);
                    d.AddAttribute("quality", quality.ToString());
                    d.AddClass("main");
                    d.AppendDiv(d2 =>
                    {
                        d2.AddClass("pages-container");
                        d2.WithId("pagesContainer");

                    }, WriteTags.OpenOnly);
                }, WriteTags.OpenOnly).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Beendet den PDF-Bericht und schließt den zugehörigen HTML-Container.
    /// en: Ends the PDF report and closes the associated HTML container.
    /// </summary>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit dem schließenden Container des PDF-Berichts zurück.
    /// en: Returns a raw HTML object with the closing container of the PDF report.
    /// </returns>
    public object EndPdfReport()
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                    d.AppendDiv(d2 =>
                    {

                    }, WriteTags.CloseOnly);
                }, WriteTags.CloseOnly).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Öffnet eine neue Seite im PDF-Bericht mit optionalen Vorlagen- und Tabellenoptionen.
    /// en: Opens a new page in the PDF report with optional template and table options.
    /// </summary>
    /// <param name="pageTemplateOptions">
    /// de: Optionen für die Seitenvorlage (z.B. Vorlagen-ID). Standard ist null.
    /// en: Options for the page template (e.g. template ID). Default is null.
    /// </param>
    /// <param name="dynamicTableOptions">
    /// de: Optionen für dynamische Tabellen auf der Seite (z.B. dynamische Ränder). Standard ist null.
    /// en: Options for dynamic tables on the page (e.g. dynamic margins). Default is null.
    /// </param>
    /// <param name="paperSize">
    /// de: Das Papierformat der Seite (z.B. A4, A3). Standard ist PaperSize.A4.
    /// en: The paper size of the page (e.g. A4, A3). Default is PaperSize.A4.
    /// </param>
    /// <param name="landscape">
    /// de: Gibt an, ob die Seite im Querformat dargestellt werden soll. Standard ist false.
    /// en: Indicates whether the page should be displayed in landscape orientation. Default is false.
    /// </param>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit dem öffnenden Container der neuen PDF-Seite zurück.
    /// en: Returns a raw HTML object with the opening container of the new PDF page.
    /// </returns>
    public object NewPage(
        PageTemplateOptions pageTemplateOptions = null,
        DynamicTableOptions dynamicTableOptions = null,
        PaperSize paperSize = PaperSize.A4,
        bool landscape = false)
    {
        pageTemplateOptions ??= new PageTemplateOptions();
        dynamicTableOptions ??= new DynamicTableOptions();

        bool usePageTemplate = pageTemplateOptions.UsePageTemplate;
        string templateId = pageTemplateOptions.TemplateId;

        bool dynamic = dynamicTableOptions.Dynamic;
        bool dynamicUseTemplate = dynamicTableOptions.DynamicUseTemplate;
        double defaultMarginTop = dynamicTableOptions.DefaultMarginTop;
        double defaultMarginBottom = dynamicTableOptions.DefaultMarginBottom;

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                    d.AddClass("page-wrapper");

                    if (dynamic)
                    {
                        d.AddClass("dynamic");
                        d.AddAttribute("data-dynamic-margin-top", defaultMarginTop.ToString(CultureInfo.InvariantCulture));
                        d.AddAttribute("data-dynamic-margin-bottom", defaultMarginBottom.ToString(CultureInfo.InvariantCulture));
                        if (dynamicUseTemplate)
                        {
                            d.AddClass("dynamic-use-template");
                        }
                    }

                    d.AppendDiv(d2 =>
                    {
                        d2.AddClass("page");

                        switch (paperSize)
                        {
                            case PaperSize.A1:
                                d2.AddClass("size-A1");
                                break;
                            case PaperSize.A2:
                                d2.AddClass("size-A2");
                                break;
                            case PaperSize.A3:
                                d2.AddClass("size-A3");
                                break;
                            case PaperSize.A4:
                                d2.AddClass("size-A4");
                                break;
                            case PaperSize.A5:
                                d2.AddClass("size-A5");
                                break;
                            case PaperSize.A6:
                                d2.AddClass("size-A6");
                                break;
                        }

                        if (landscape)
                            d2.AddClass("horizontal");

                        if (usePageTemplate)
                        {
                            d2.AddAttribute("datalinq-pdfreport-template", templateId);
                        }

                        d2.AppendDiv(divLineVertical =>
                        {
                            divLineVertical.AddClass("vertical-middle-line");
                            divLineVertical.AddClass("report-ignore");
                        });
                        d2.AppendDiv(divLineHorizontal =>
                        {
                            divLineHorizontal.AddClass("horizontal-middle-line");
                            divLineHorizontal.AddClass("report-ignore");

                        });
                    }, WriteTags.OpenOnly);
                }, WriteTags.OpenOnly).BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Schließt die aktuelle Seite im PDF-Bericht.
    /// en: Closes the current page in the PDF report.
    /// </summary>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit den schließenden Containern der aktuellen Seite zurück.
    /// en: Returns a raw HTML object with the closing containers of the current page.
    /// </returns>
    public object EndPage()
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                }, WriteTags.CloseOnly)
                .AppendDiv(d =>
                {
                }, WriteTags.CloseOnly)
                .BuildHtmlString()
            );
    }

    /// <summary>
    /// de: Rendert einen Button, der das stille Herunterladen eines PDF-Berichts auslöst.
    /// en: Renders a button that triggers the silent download of a PDF report.
    /// </summary>
    /// <param name="id">
    /// de: Die ID des DataLinq-Endpunkts, dessen PDF heruntergeladen werden soll.
    /// en: The ID of the DataLinq endpoint whose PDF should be downloaded.
    /// </param>
    /// <param name="buttonText">
    /// de: Der anzuzeigende Text auf dem Button. Standard ist "Print".
    /// en: The text to display on the button. Default is "Print".
    /// </param>
    /// <param name="parameters">
    /// de: Optionale Query-Parameter, die beim PDF-Download mitgesendet werden. Standard ist null.
    /// en: Optional query parameters to be sent with the PDF download. Default is null.
    /// </param>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit dem gerenderten Download-Button zurück.
    /// en: Returns a raw HTML object with the rendered download button.
    /// </returns>
    public object PrintPdfButton(string id, string buttonText = "Print", Dictionary<string, string> parameters = null)
    {
        var queryString = parameters is null
            ? string.Empty
            : string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        var buttonId = GenerateUniqueId(id);

        var onClickJs = $"dataLinq.downloadPDFSilently('{id}', '{queryString}', '{buttonId}')";

        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendButton(b =>
                {
                    b.WithId(buttonId);
                    b.AddClass("datalinq-button");
                    b.Content(buttonText);
                    b.AddAttribute("onclick", onClickJs);
                })
                .BuildHtmlString()
        );
    }

    /// <summary>
    /// de: Öffnet ein neues positionierbares PDF-Element innerhalb einer Seite.
    /// en: Opens a new positionable PDF element within a page.
    /// </summary>
    /// <param name="x">
    /// de: Die horizontale Verschiebung des Elements in Pixeln. Standard ist 0.
    /// en: The horizontal offset of the element in pixels. Default is 0.
    /// </param>
    /// <param name="y">
    /// de: Die vertikale Verschiebung des Elements in Pixeln. Standard ist 0.
    /// en: The vertical offset of the element in pixels. Default is 0.
    /// </param>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit dem öffnenden Container des PDF-Elements zurück.
    /// en: Returns a raw HTML object with the opening container of the PDF element.
    /// </returns>
    public object NewPdfElement(double x = 0, double y = 0)
    {
        return _razor.RawString(
            HtmlBuilder.Create()
                .AppendDiv(d =>
                {
                    d.AddClass("element");
                    if (x is not 0 || y is not 0)
                    {
                        d.AddAttribute("data-x", x.ToString(CultureInfo.InvariantCulture));
                        d.AddAttribute("data-y", y.ToString(CultureInfo.InvariantCulture));
                        d.AddStyle("transform", $"translate({x.ToString(CultureInfo.InvariantCulture)}px, {y.ToString(CultureInfo.InvariantCulture)}px)");
                    }

                }, WriteTags.OpenOnly)
                .BuildHtmlString()
        );
    }

    /// <summary>
    /// de: Schließt das aktuelle PDF-Element.
    /// en: Closes the current PDF element.
    /// </summary>
    /// <returns>
    /// de: Gibt ein rohes HTML-Objekt mit dem schließenden Container des PDF-Elements zurück.
    /// en: Returns a raw HTML object with the closing container of the PDF element.
    /// </returns>
    public object EndPdfElement()
    {
        return _razor.RawString(
           HtmlBuilder.Create()
               .AppendDiv(d =>
               {

               }, WriteTags.CloseOnly)
               .BuildHtmlString()
       );
    }

    #endregion

    #region Helpers

    private static string GenerateUniqueId(string id)
    {
        return $"{id}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    #endregion
}
