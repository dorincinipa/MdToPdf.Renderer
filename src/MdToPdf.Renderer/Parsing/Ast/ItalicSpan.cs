namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class ItalicSpan : InlineSpan
{
    public List<InlineSpan> Children { get; } = new();
}
