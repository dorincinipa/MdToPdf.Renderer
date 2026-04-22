namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class OrderedList : MarkdownBlock
{
    public int StartIndex { get; set; } = 1;
    public List<ListItem> Items { get; } = new();
}
