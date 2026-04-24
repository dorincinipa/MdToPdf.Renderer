using MdToPdf.Renderer;
using PdfSharp.Pdf;

namespace MdToPdf.Api;

internal sealed class MdToPdfGenerator : IPdfGenerator
{
    private readonly PdfOptions _defaults;

    internal MdToPdfGenerator(PdfOptions defaults)
    {
        _defaults = defaults;
    }

    public PdfDocument GeneratePdf(string markdown)
        => PdfGenerator.GeneratePdf(markdown, _defaults);

    public PdfDocument GeneratePdf(string markdown, PdfOptions options)
        => PdfGenerator.GeneratePdf(markdown, options);

    public Task<byte[]> GeneratePdfAsync(string markdown)
        => PdfGenerator.GeneratePdfAsync(markdown, _defaults);

    public Task<byte[]> GeneratePdfAsync(string markdown, PdfOptions options)
        => PdfGenerator.GeneratePdfAsync(markdown, options);

    public void AddPdfPages(PdfDocument document, string markdown)
        => PdfGenerator.AddPdfPages(document, markdown, _defaults);

    public void AddPdfPages(PdfDocument document, string markdown, PdfOptions options)
        => PdfGenerator.AddPdfPages(document, markdown, options);
}
