namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class CodeSpan : InlineSpan
{
    public string Text { get; set; } = string.Empty;

    public CodeSpan() { }
    public CodeSpan(string text) { Text = text; }
}
