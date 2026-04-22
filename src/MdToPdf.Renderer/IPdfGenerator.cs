using PdfSharp.Pdf;

namespace MdToPdf.Renderer;

public interface IPdfGenerator
{
    PdfDocument GeneratePdf(string markdown);
    PdfDocument GeneratePdf(string markdown, PdfOptions options);
    Task<byte[]> GeneratePdfAsync(string markdown);
    Task<byte[]> GeneratePdfAsync(string markdown, PdfOptions options);
    void AddPdfPages(PdfDocument document, string markdown);
    void AddPdfPages(PdfDocument document, string markdown, PdfOptions options);
}
