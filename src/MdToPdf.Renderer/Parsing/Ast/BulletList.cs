namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class BulletList : MarkdownBlock
{
    public List<ListItem> Items { get; } = new();
}
