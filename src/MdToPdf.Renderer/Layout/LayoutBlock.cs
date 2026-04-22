namespace MdToPdf.Renderer.Layout;

internal abstract class LayoutBlock
{
    public int PageIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

internal sealed class LayoutLine : LayoutBlock
{
    public List<StyledRun> Runs { get; } = new();
    public double FontSize { get; set; }
    public double Baseline { get; set; }
}

internal sealed class LayoutImageBlock : LayoutBlock
{
    public string Src { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
}

internal sealed class LayoutRule : LayoutBlock { }

internal sealed class LayoutBlockquoteBar : LayoutBlock { }

internal sealed class LayoutCodeBackground : LayoutBlock { }

internal sealed class StyledRun
{
    public string Text { get; set; } = string.Empty;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Monospace { get; set; }
    public string Color { get; set; } = "#000000";
    public string? LinkUrl { get; set; }
    public double OffsetX { get; set; }
    public double Width { get; set; }
    public double FontSize { get; set; }
}
