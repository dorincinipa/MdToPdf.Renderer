namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class Blockquote : MarkdownBlock
{
    public List<MarkdownBlock> Children { get; } = new();
}
