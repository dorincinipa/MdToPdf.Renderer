using PdfSharp.Pdf.IO;

namespace MdToPdf.Renderer.Tests;

public class PdfCompressionTests
{
    private const string Md = "# Title\n\nLorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                               "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

    private static byte[] Generate(bool compress)
    {
        var options = new PdfOptions
        {
            PageSize = PageSize.A4,
            MarginTop = 40, MarginBottom = 40, MarginLeft = 40, MarginRight = 40,
            CompressOutput = compress
        };
        using var doc = PdfGenerator.GeneratePdf(Md, options);
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    [Fact]
    public void CompressOutput_False_ProducesValidPdf()
    {
        var bytes = Generate(compress: false);
        using var ms = new MemoryStream(bytes);
        var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void CompressOutput_True_ProducesValidPdf()
    {
        var bytes = Generate(compress: true);
        using var ms = new MemoryStream(bytes);
        var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void CompressOutput_True_ProducesSmallerFile()
    {
        var uncompressed = Generate(compress: false);
        var compressed = Generate(compress: true);
        Assert.True(compressed.Length < uncompressed.Length,
            $"Expected compressed ({compressed.Length}) < uncompressed ({uncompressed.Length})");
    }

    [Fact]
    public void WithCompression_FluentApi_ProducesValidPdf()
    {
        var doc = PdfGenerator.Create()
            .WithPageSize(PageSize.A4)
            .WithMargin(40)
            .WithCompression()
            .GeneratePdf(Md);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        ms.Position = 0;
        var read = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, read.PageCount);
    }
}
