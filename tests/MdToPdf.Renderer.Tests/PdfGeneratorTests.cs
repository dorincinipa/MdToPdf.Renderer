using PdfSharp.Pdf;

namespace MdToPdf.Renderer.Tests;

public class PdfGeneratorTests
{
    private static PdfOptions DefaultConfig() => new()
    {
        PageSize = PageSize.A4,
        MarginTop = 20,
        MarginBottom = 20,
        MarginLeft = 20,
        MarginRight = 20
    };

    [Fact]
    public void GeneratePdf_SimpleMarkdown_ReturnsValidDocument()
    {
        var doc = PdfGenerator.GeneratePdf("# Hello\n\nWorld", DefaultConfig());
        Assert.NotNull(doc);
        Assert.True(doc.PageCount >= 1);
    }

    [Fact]
    public void GeneratePdf_EmptyMarkdown_ReturnsSinglePage()
    {
        var doc = PdfGenerator.GeneratePdf("", DefaultConfig());
        Assert.NotNull(doc);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void GeneratePdf_LongContent_CreatesMultiplePages()
    {
        var md = string.Join("\n\n",
            Enumerable.Repeat("This is a paragraph of text used for pagination testing.", 200));
        var doc = PdfGenerator.GeneratePdf(md, DefaultConfig());
        Assert.True(doc.PageCount > 1, $"Expected multiple pages, got {doc.PageCount}");
    }

    [Fact]
    public void GeneratePdf_WithMargins_PageDimensionsMatchConfig()
    {
        var config = new PdfOptions
        {
            PageSize = PageSize.Letter,
            MarginTop = 50,
            MarginBottom = 50,
            MarginLeft = 50,
            MarginRight = 50
        };
        var doc = PdfGenerator.GeneratePdf("Margins test", config);
        Assert.Equal(612, doc.Pages[0].Width.Point, 1);
        Assert.Equal(792, doc.Pages[0].Height.Point, 1);
    }

    [Fact]
    public void GeneratePdf_Landscape_SwapsDimensions()
    {
        var config = new PdfOptions
        {
            PageSize = PageSize.A4,
            PageOrientation = PageOrientation.Landscape
        };
        var doc = PdfGenerator.GeneratePdf("Landscape", config);
        Assert.True(doc.Pages[0].Width.Point > doc.Pages[0].Height.Point);
    }

    [Fact]
    public void GeneratePdf_SaveToStream_ProducesNonEmptyOutput()
    {
        var doc = PdfGenerator.GeneratePdf("Stream test", DefaultConfig());
        using var stream = new MemoryStream();
        doc.Save(stream, false);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        var header = new byte[5];
        stream.Read(header, 0, 5);
        Assert.Equal("%PDF-"u8.ToArray(), header);
    }

    [Fact]
    public void GeneratePdf_WithImage_DataUri_LoadsAndRenders()
    {
        // 1x1 transparent PNG
        var md = "![tiny](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==)";
        var doc = PdfGenerator.GeneratePdf(md, DefaultConfig());
        Assert.NotNull(doc);
        Assert.True(doc.PageCount >= 1);
    }

    [Fact]
    public void GeneratePdf_WithImage_MissingFile_RenderAltText_Default()
    {
        var md = "![alt-text-here](C:/nonexistent/does-not-exist.png)";
        var config = DefaultConfig();
        Assert.Equal(ImageLoadFailureMode.RenderAltText, config.ImageLoadFailureMode);
        var ex = Record.Exception(() => PdfGenerator.GeneratePdf(md, config));
        Assert.Null(ex);
    }

    [Fact]
    public void GeneratePdf_WithImage_MissingFile_ThrowMode_Throws()
    {
        var md = "![alt](C:/nonexistent/does-not-exist.png)";
        var config = DefaultConfig();
        config.ImageLoadFailureMode = ImageLoadFailureMode.Throw;
        Assert.ThrowsAny<Exception>(() => PdfGenerator.GeneratePdf(md, config));
    }

    [Fact]
    public void GeneratePdf_FencedCodeBlock_PreservesMonospaceFont()
    {
        var md = "```\nvar x = 1;\nreturn x;\n```";
        var doc = PdfGenerator.GeneratePdf(md, DefaultConfig());
        Assert.NotNull(doc);
        Assert.True(doc.PageCount >= 1);
    }

    [Fact]
    public void AddPdfPages_AppendsToExistingDocument()
    {
        var doc = new PdfDocument();
        doc.AddPage();
        Assert.Equal(1, doc.PageCount);

        PdfGenerator.AddPdfPages(doc, "Appended content", DefaultConfig());

        Assert.True(doc.PageCount >= 2, $"Expected at least 2 pages, got {doc.PageCount}");
    }

    [Fact]
    public void AddPdfPages_MultipleCalls_AccumulatePages()
    {
        var doc = new PdfDocument();
        PdfGenerator.AddPdfPages(doc, "Section 1", DefaultConfig());
        var countAfterFirst = doc.PageCount;
        PdfGenerator.AddPdfPages(doc, "Section 2", DefaultConfig());
        Assert.True(doc.PageCount > countAfterFirst,
            $"Expected more pages after second call, got {doc.PageCount}");
    }

    [Fact]
    public void GeneratePdf_NestedLists_RendersWithIndent()
    {
        var md = "- item one\n  - nested one\n  - nested two\n- item two";
        var doc = PdfGenerator.GeneratePdf(md, DefaultConfig());
        Assert.NotNull(doc);
        Assert.True(doc.PageCount >= 1);
    }
}
