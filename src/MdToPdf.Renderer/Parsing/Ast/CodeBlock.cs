namespace MdToPdf.Renderer.Parsing.Ast;

internal sealed class CodeBlock : MarkdownBlock
{
    public string? InfoString { get; set; }
    public string Content { get; set; } = string.Empty;
}
