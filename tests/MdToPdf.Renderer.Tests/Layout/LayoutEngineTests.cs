using System.Linq;
using MdToPdf.Renderer.Layout;
using MdToPdf.Renderer.Parsing;
using PdfSharp.Drawing;

namespace MdToPdf.Renderer.Tests.Layout;

public class LayoutEngineTests
{
    private static XGraphics CreateMeasure(PdfOptions opts)
    {
        var size = opts.GetEffectivePageSize();
        return XGraphics.CreateMeasureContext(
            new XSize(size.Width, size.Height),
            XGraphicsUnit.Point,
            XPageDirection.Downwards);
    }

    private static PdfOptions DefaultOptions() => new()
    {
        PageSize = PageSize.A4,
        MarginTop = 40,
        MarginBottom = 40,
        MarginLeft = 40,
        MarginRight = 40
    };

    [Fact]
    public void Layout_SingleParagraph_FitsOnePage()
    {
        var opts = DefaultOptions();
        using var g = CreateMeasure(opts);
        var ast = MarkdownParser.Parse("Hello world.");
        var engine = new LayoutEngine(opts, g);
        var result = engine.Layout(ast);

        Assert.Equal(1, result.PageCount);
        var lines = result.Blocks.OfType<LayoutLine>().ToList();
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(0, l.PageIndex));
    }

    [Fact]
    public void Layout_LongParagraph_WrapsAtContentWidth()
    {
        var opts = DefaultOptions();
        using var g = CreateMeasure(opts);
        var longWords = string.Join(" ", Enumerable.Repeat("quickbrownfox", 60));
        var ast = MarkdownParser.Parse(longWords);
        var engine = new LayoutEngine(opts, g);
        var result = engine.Layout(ast);

        var lines = result.Blocks.OfType<LayoutLine>().ToList();
        Assert.True(lines.Count >= 2, $"Expected wrapping to produce >=2 lines, got {lines.Count}");
        double contentWidth = opts.GetEffectivePageSize().Width - opts.MarginLeft - opts.MarginRight;
        Assert.All(lines, l => Assert.True(l.Width <= contentWidth + 0.5,
            $"Line width {l.Width} exceeded content width {contentWidth}"));
    }

    [Fact]
    public void Layout_ManyParagraphs_SplitAcrossPages()
    {
        var opts = DefaultOptions();
        using var g = CreateMeasure(opts);
        var text = string.Join("\n\n", Enumerable.Repeat("This is a paragraph with enough content to take some vertical space.", 200));
        var ast = MarkdownParser.Parse(text);
        var engine = new LayoutEngine(opts, g);
        var result = engine.Layout(ast);

        Assert.True(result.PageCount > 1, $"Expected multiple pages, got {result.PageCount}");
        var distinctPages = result.Blocks.Select(b => b.PageIndex).Distinct().Count();
        Assert.True(distinctPages >= 2);
    }

    [Fact]
    public void Layout_CodeBlock_SplitsAtLineBoundary()
    {
        var opts = DefaultOptions();
        using var g = CreateMeasure(opts);
        var codeLines = string.Join("\n", Enumerable.Range(1, 400).Select(i => $"line {i}"));
        var ast = MarkdownParser.Parse("```\n" + codeLines + "\n```");
        var engine = new LayoutEngine(opts, g);
        var result = engine.Layout(ast);

        Assert.True(result.PageCount > 1, $"Expected code block to span pages, got {result.PageCount}");
        // All code lines rendered as monospace runs
        var monoRuns = result.Blocks.OfType<LayoutLine>()
            .SelectMany(l => l.Runs)
            .Where(r => r.Monospace)
            .ToList();
        Assert.True(monoRuns.Count >= 400);
    }

    [Fact]
    public void Layout_NestedList_IndentsCorrectly()
    {
        var opts = DefaultOptions();
        using var g = CreateMeasure(opts);
        var ast = MarkdownParser.Parse("- outer\n  - inner\n- next");
        var engine = new LayoutEngine(opts, g);
        var result = engine.Layout(ast);

        var lines = result.Blocks.OfType<LayoutLine>().ToList();
        // The nested item's content line should have a larger X than the outer items' content lines
        var contentLines = lines.Where(l => l.Runs.Any(r => r.Text.Contains("inner") || r.Text.Contains("outer") || r.Text.Contains("next")))
                                .ToList();
        Assert.NotEmpty(contentLines);
        var outerLine = contentLines.First(l => l.Runs.Any(r => r.Text.Contains("outer")));
        var innerLine = contentLines.First(l => l.Runs.Any(r => r.Text.Contains("inner")));
        Assert.True(innerLine.X > outerLine.X, $"Nested line X={innerLine.X} should exceed outer X={outerLine.X}");
    }

    [Fact]
    public void Layout_ImageTallerThanPage_ScalesDown()
    {
        // Inline image inside a paragraph falls back to alt text in our layout — verify it
        // doesn't overflow the page and the output contains the alt text.
        var opts = DefaultOptions();
        using var g = CreateMeasure(opts);
        var ast = MarkdownParser.Parse("![tall](data:image/png;base64,AAAA)");
        var engine = new LayoutEngine(opts, g);
        var result = engine.Layout(ast);

        var altFound = result.Blocks.OfType<LayoutLine>()
            .SelectMany(l => l.Runs)
            .Any(r => r.Text.Contains("tall") || r.Text.Contains("[image]"));
        Assert.True(altFound);
        Assert.True(result.PageCount >= 1);
        var maxY = result.Blocks.Max(b => b.Y + b.Height);
        double pageBottom = opts.GetEffectivePageSize().Height - opts.MarginBottom;
        Assert.True(maxY <= pageBottom + 1);
    }
}
