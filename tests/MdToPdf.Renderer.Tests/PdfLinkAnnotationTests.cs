using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MdToPdf.Renderer.Tests;

public class PdfLinkAnnotationTests
{
    private static PdfOptions Options() => new()
    {
        PageSize = PageSize.A4,
        MarginTop = 40,
        MarginBottom = 40,
        MarginLeft = 40,
        MarginRight = 40,
    };

    private static PdfDocument Roundtrip(PdfDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        ms.Position = 0;
        return PdfReader.Open(ms, PdfDocumentOpenMode.Import);
    }

    [Fact]
    public void ExternalLink_CreatesAnnotation()
    {
        const string md = "[click me](https://example.com)";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.True(read.Pages[0].Annotations.Count >= 1);
    }

    [Fact]
    public void NoLinks_NoAnnotations()
    {
        const string md = "No links here";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.Equal(0, read.Pages[0].Annotations.Count);
    }

    [Fact]
    public void MultipleExternalLinks_AllAnnotated()
    {
        const string md = "[A](https://a.com) [B](https://b.com) [C](https://c.com)";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.True(read.Pages[0].Annotations.Count >= 3);
    }

    [Fact]
    public void MailtoLink_CreatesAnnotation()
    {
        const string md = "[email](mailto:test@example.com)";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.True(read.Pages[0].Annotations.Count >= 1);
    }

    [Fact]
    public void AnchorLink_WithValidHeadingTarget_CreatesAnnotation()
    {
        const string md = "[jump to features](#features)\n\n## Features";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.True(read.Pages[0].Annotations.Count >= 1);
    }

    [Fact]
    public void AnchorLink_WithNoMatchingHeading_NoAnnotation()
    {
        const string md = "[jump](#nonexistent)";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.Equal(0, read.Pages[0].Annotations.Count);
    }

    [Fact]
    public void AnchorLink_MultiWordHeading_SlugMatches()
    {
        const string md = "[go](#my-complex-heading)\n\n## My Complex Heading";
        var read = Roundtrip(PdfGenerator.GeneratePdf(md, Options()));
        Assert.True(read.Pages[0].Annotations.Count >= 1);
    }
}
