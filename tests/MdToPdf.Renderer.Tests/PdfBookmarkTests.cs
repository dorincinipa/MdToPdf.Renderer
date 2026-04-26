using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MdToPdf.Renderer.Tests;

public class PdfBookmarkTests
{
    private static PdfOptions Options(bool autoBookmarks = true) => new()
    {
        PageSize = PageSize.A4,
        MarginTop = 40,
        MarginBottom = 40,
        MarginLeft = 40,
        MarginRight = 40,
        AutoBookmarks = autoBookmarks
    };

    private static PdfDocument Roundtrip(PdfDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        ms.Position = 0;
        return PdfReader.Open(ms, PdfDocumentOpenMode.Import);
    }

    [Fact]
    public void AutoBookmarks_False_NoOutlines()
    {
        var doc = PdfGenerator.GeneratePdf("# Title", Options(autoBookmarks: false));
        var read = Roundtrip(doc);
        Assert.Empty(read.Outlines);
    }

    [Fact]
    public void AutoBookmarks_NoHeadings_NoOutlines()
    {
        var doc = PdfGenerator.GeneratePdf("Just a paragraph", Options());
        var read = Roundtrip(doc);
        Assert.Empty(read.Outlines);
    }

    [Fact]
    public void AutoBookmarks_SingleH1_OneOutline()
    {
        var doc = PdfGenerator.GeneratePdf("# Main Title", Options());
        var read = Roundtrip(doc);
        Assert.Single(read.Outlines);
        Assert.Equal("Main Title", read.Outlines[0].Title);
    }

    [Fact]
    public void AutoBookmarks_H1H2H3_CorrectHierarchy()
    {
        const string md = "# Chapter\n\n## Section\n\n### Subsection";
        var doc = PdfGenerator.GeneratePdf(md, Options());
        var read = Roundtrip(doc);

        Assert.Single(read.Outlines);
        Assert.Equal("Chapter", read.Outlines[0].Title);
        Assert.Single(read.Outlines[0].Outlines);
        Assert.Equal("Section", read.Outlines[0].Outlines[0].Title);
        Assert.Single(read.Outlines[0].Outlines[0].Outlines);
        Assert.Equal("Subsection", read.Outlines[0].Outlines[0].Outlines[0].Title);
    }

    [Fact]
    public void AutoBookmarks_MultipleH1_FlatOutlines()
    {
        const string md = "# First\n\n# Second\n\n# Third";
        var doc = PdfGenerator.GeneratePdf(md, Options());
        var read = Roundtrip(doc);

        Assert.Equal(3, read.Outlines.Count);
        Assert.Equal("First", read.Outlines[0].Title);
        Assert.Equal("Second", read.Outlines[1].Title);
        Assert.Equal("Third", read.Outlines[2].Title);
    }

    [Fact]
    public void AutoBookmarks_H2WithoutH1_AttachesToRoot()
    {
        const string md = "## Section\n\n### Sub";
        var doc = PdfGenerator.GeneratePdf(md, Options());
        var read = Roundtrip(doc);

        Assert.Single(read.Outlines);
        Assert.Equal("Section", read.Outlines[0].Title);
        Assert.Single(read.Outlines[0].Outlines);
    }

    [Fact]
    public void AutoBookmarks_H1AfterH3_ResetsToRoot()
    {
        const string md = "### Deep\n\n# Reset";
        var doc = PdfGenerator.GeneratePdf(md, Options());
        var read = Roundtrip(doc);

        Assert.Equal(2, read.Outlines.Count);
        Assert.Equal("Deep", read.Outlines[0].Title);
        Assert.Equal("Reset", read.Outlines[1].Title);
    }

    [Fact]
    public void AutoBookmarks_OutlinePointsToPage()
    {
        var doc = PdfGenerator.GeneratePdf("# Title", Options());
        var read = Roundtrip(doc);

        Assert.NotNull(read.Outlines[0].DestinationPage);
    }

    [Fact]
    public void WithBookmarks_FluentApi_CreatesOutlines()
    {
        var doc = PdfGenerator.Create()
            .WithPageSize(PageSize.A4)
            .WithMargin(40)
            .WithBookmarks()
            .GeneratePdf("# Title\n\n## Sub");

        var read = Roundtrip(doc);
        Assert.Single(read.Outlines);
        Assert.Equal("Title", read.Outlines[0].Title);
        Assert.Single(read.Outlines[0].Outlines);
    }
}
