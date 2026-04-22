using PdfSharp.Drawing;

namespace MdToPdf.Renderer;

public enum PageOrientation
{
    Portrait,
    Landscape
}

public enum ImageLoadFailureMode
{
    RenderAltText,
    Throw
}

public class PdfOptions
{
    public XSize PageSize { get; set; } = Renderer.PageSize.A4;
    public double MarginTop { get; set; }
    public double MarginBottom { get; set; }
    public double MarginLeft { get; set; }
    public double MarginRight { get; set; }
    public PageOrientation PageOrientation { get; set; } = PageOrientation.Portrait;
    public double DefaultMargin { get; set; }
    public string? FontFolder { get; set; }

    // Markdown-specific visual options
    public double BaseFontSize { get; set; } = 11;
    public string BodyFontFamily { get; set; } = "Arial";
    public string MonospaceFontFamily { get; set; } = "Courier New";
    public double LineHeight { get; set; } = 1.4;
    public ImageLoadFailureMode ImageLoadFailureMode { get; set; } = ImageLoadFailureMode.RenderAltText;

    public void SetMargins(double all)
    {
        MarginTop = MarginBottom = MarginLeft = MarginRight = all;
    }

    public void SetMargins(double vertical, double horizontal)
    {
        MarginTop = MarginBottom = vertical;
        MarginLeft = MarginRight = horizontal;
    }

    public XSize GetEffectivePageSize()
    {
        return PageOrientation == PageOrientation.Landscape
            ? new XSize(PageSize.Height, PageSize.Width)
            : PageSize;
    }
}
