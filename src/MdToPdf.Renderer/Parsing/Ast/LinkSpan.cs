namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class LinkSpan : InlineSpan
{
    public string Url { get; set; } = string.Empty;
    public List<InlineSpan> Children { get; } = new();
}
