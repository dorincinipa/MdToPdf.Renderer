namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class TextSpan : InlineSpan
{
    public string Text { get; set; } = string.Empty;

    public TextSpan() { }
    public TextSpan(string text) { Text = text; }
}
