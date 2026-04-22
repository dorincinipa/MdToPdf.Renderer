namespace MdToPdf.Renderer.Layout;

internal sealed class LayoutResult
{
    public int PageCount { get; set; }
    public double PageWidth { get; set; }
    public double PageHeight { get; set; }
    public List<LayoutBlock> Blocks { get; } = new();
}
