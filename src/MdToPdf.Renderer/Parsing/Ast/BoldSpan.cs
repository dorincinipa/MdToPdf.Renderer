namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class BoldSpan : InlineSpan
{
    public List<InlineSpan> Children { get; } = new();
}
