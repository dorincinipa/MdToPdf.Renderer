using System.Linq;
using MdToPdf.Renderer.Parsing;
using MdToPdf.Renderer.Parsing.Ast;

namespace MdToPdf.Renderer.Tests.Parsing;

public class MarkdownParserTests
{
    [Fact]
    public void Parses_Heading_LevelsOneThroughSix()
    {
        var input = "# h1\n## h2\n### h3\n#### h4\n##### h5\n###### h6";
        var blocks = MarkdownParser.Parse(input);
        Assert.Equal(6, blocks.Count);
        for (int i = 0; i < 6; i++)
        {
            var h = Assert.IsType<Heading>(blocks[i]);
            Assert.Equal(i + 1, h.Level);
            Assert.Equal($"h{i + 1}", InlineText(h.Inlines));
        }
    }

    [Fact]
    public void Parses_Paragraph_WithInlineBoldItalic()
    {
        var input = "Hello **bold** and *italic* world";
        var blocks = MarkdownParser.Parse(input);
        var p = Assert.IsType<Paragraph>(Assert.Single(blocks));
        Assert.Contains(p.Inlines, s => s is BoldSpan b && InlineText(b.Children) == "bold");
        Assert.Contains(p.Inlines, s => s is ItalicSpan it && InlineText(it.Children) == "italic");
    }

    [Fact]
    public void Parses_BulletList_NestedTwoLevels()
    {
        var input = "- one\n  - nested\n- two";
        var blocks = MarkdownParser.Parse(input);
        var list = Assert.IsType<BulletList>(Assert.Single(blocks));
        Assert.Equal(2, list.Items.Count);
        // First item should have a nested BulletList child in addition to its paragraph
        var nested = list.Items[0].Children.OfType<BulletList>().SingleOrDefault();
        Assert.NotNull(nested);
        Assert.Single(nested!.Items);
    }

    [Fact]
    public void Parses_OrderedList_WithStartIndex()
    {
        var input = "3. first\n4. second";
        var blocks = MarkdownParser.Parse(input);
        var list = Assert.IsType<OrderedList>(Assert.Single(blocks));
        Assert.Equal(3, list.StartIndex);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parses_FencedCodeBlock_PreservesWhitespace()
    {
        var input = "```\nline1\n  indented\nline3\n```";
        var blocks = MarkdownParser.Parse(input);
        var code = Assert.IsType<CodeBlock>(Assert.Single(blocks));
        Assert.Equal("line1\n  indented\nline3", code.Content);
    }

    [Fact]
    public void Parses_Blockquote_WithNestedParagraph()
    {
        var input = "> quote line one\n> quote line two";
        var blocks = MarkdownParser.Parse(input);
        var bq = Assert.IsType<Blockquote>(Assert.Single(blocks));
        var inner = Assert.IsType<Paragraph>(Assert.Single(bq.Children));
        var text = InlineText(inner.Inlines);
        // Soft breaks render as spaces in our parser, so lines are joined with a space
        Assert.Contains("quote line one", text);
        Assert.Contains("quote line two", text);
    }

    [Fact]
    public void Parses_ThematicBreak_ThreeDashes()
    {
        var input = "---";
        var blocks = MarkdownParser.Parse(input);
        Assert.IsType<ThematicBreak>(Assert.Single(blocks));
    }

    [Fact]
    public void Parses_Link_InlineInsideParagraph()
    {
        var input = "See [here](https://example.com) now";
        var blocks = MarkdownParser.Parse(input);
        var p = Assert.IsType<Paragraph>(Assert.Single(blocks));
        var link = p.Inlines.OfType<LinkSpan>().Single();
        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("here", InlineText(link.Children));
    }

    [Fact]
    public void Parses_Image_DataUri()
    {
        var input = "![logo](data:image/png;base64,AAAA)";
        var blocks = MarkdownParser.Parse(input);
        var p = Assert.IsType<Paragraph>(Assert.Single(blocks));
        var img = p.Inlines.OfType<ImageSpan>().Single();
        Assert.StartsWith("data:image/png;base64,", img.Src);
        Assert.Equal("logo", img.Alt);
    }

    [Fact]
    public void Parses_UnterminatedBold_FallsBackToLiteral()
    {
        var input = "text with **unclosed bold";
        var blocks = MarkdownParser.Parse(input);
        var p = Assert.IsType<Paragraph>(Assert.Single(blocks));
        Assert.Empty(p.Inlines.OfType<BoldSpan>());
        Assert.Contains("**", InlineText(p.Inlines));
    }

    [Fact]
    public void Parses_EmptyInput_ReturnsEmptyAst()
    {
        Assert.Empty(MarkdownParser.Parse(""));
        Assert.Empty(MarkdownParser.Parse(null));
        Assert.Empty(MarkdownParser.Parse("   "));
    }

    private static string InlineText(List<InlineSpan> inlines)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var span in inlines) AppendText(span, sb);
        return sb.ToString();
    }

    private static void AppendText(InlineSpan span, System.Text.StringBuilder sb)
    {
        switch (span)
        {
            case TextSpan t: sb.Append(t.Text); break;
            case BoldSpan b: foreach (var c in b.Children) AppendText(c, sb); break;
            case ItalicSpan i: foreach (var c in i.Children) AppendText(c, sb); break;
            case LinkSpan l: foreach (var c in l.Children) AppendText(c, sb); break;
            case CodeSpan cs: sb.Append(cs.Text); break;
            case LineBreakSpan: sb.Append('\n'); break;
            case ImageSpan im: sb.Append(im.Alt); break;
        }
    }
}
