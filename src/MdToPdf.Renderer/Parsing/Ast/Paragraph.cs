namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class Paragraph : MarkdownBlock
{
    public List<InlineSpan> Inlines { get; set; } = new();
}
