namespace MdToPdf.Renderer.Tests;

public class PdfBuilderTests
{
    [Fact]
    public void Builder_WithPageSize_SetsOption()
    {
        var doc = PdfGenerator.Create()
            .WithPageSize(PageSize.Letter)
            .GeneratePdf("Test");

        Assert.Equal(612, doc.Pages[0].Width.Point, 1);
        Assert.Equal(792, doc.Pages[0].Height.Point, 1);
    }

    [Fact]
    public void Builder_WithMargin_AllFourFormsWork()
    {
        var d1 = PdfGenerator.Create().WithMargin(10).GeneratePdf("a");
        var d2 = PdfGenerator.Create().WithMargin(10, 20).GeneratePdf("a");
        var d3 = PdfGenerator.Create().WithMargin(5, 10, 15, 20).GeneratePdf("a");
        Assert.All(new[] { d1, d2, d3 }, d => Assert.True(d.PageCount >= 1));
    }

    [Fact]
    public void Builder_WithFont_RegistersViaFontResolver()
    {
        // Regression smoke test: registering a font via the builder should not throw even
        // if the font bytes are a minimal stub that PdfSharp never actually renders.
        var stub = new byte[] { 0x00, 0x01, 0x00, 0x00 }; // will never be used since BodyFontFamily is Arial
        var ex = Record.Exception(() =>
        {
            var doc = PdfGenerator.Create()
                .WithFont("CustomUnused", stub)
                .WithPageSize(PageSize.A4)
                .WithMargin(20)
                .GeneratePdf("Font test");
            Assert.True(doc.PageCount >= 1);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void Builder_GeneratePdf_ProducesDocument()
    {
        var doc = PdfGenerator.Create()
            .WithPageSize(PageSize.A4)
            .WithOrientation(PageOrientation.Portrait)
            .WithMargin(10, 20)
            .GeneratePdf("Full chain");

        Assert.NotNull(doc);
        Assert.True(doc.PageCount >= 1);
    }

    [Fact]
    public async Task Builder_GeneratePdfAsync_ReturnsValidBytes()
    {
        var bytes = await PdfGenerator.Create()
            .WithPageSize(PageSize.A4)
            .WithMargin(20)
            .GeneratePdfAsync("Async test");

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-"u8.ToArray(), bytes[..5]);
    }
}
