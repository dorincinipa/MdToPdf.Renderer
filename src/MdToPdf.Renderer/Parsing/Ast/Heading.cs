namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class Heading : MarkdownBlock
{
    public int Level { get; set; }
    public List<InlineSpan> Inlines { get; set; } = new();
}
