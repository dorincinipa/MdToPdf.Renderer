namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class ListItem
{
    public List<MarkdownBlock> Children { get; } = new();
}
