namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class ImageSpan : InlineSpan
{
    public string Src { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
}
