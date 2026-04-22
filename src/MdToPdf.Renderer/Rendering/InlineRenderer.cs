using MdToPdf.Renderer.Layout;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace MdToPdf.Renderer.Rendering;

internal sealed class InlineRenderer
{
    private readonly PdfOptions _options;
    private readonly Dictionary<(string family, bool bold, bool italic, double size), XFont> _fontCache = new();

    internal InlineRenderer(PdfOptions options)
    {
        _options = options;
    }

    internal void DrawLine(XGraphics g, PdfPage page, LayoutLine line)
    {
        double baseline = line.Y + line.Baseline;

        foreach (var run in line.Runs)
        {
            if (string.IsNullOrEmpty(run.Text)) continue;

            var family = run.Monospace ? _options.MonospaceFontFamily : _options.BodyFontFamily;
            var font = GetFont(family, run.Bold, run.Italic, run.FontSize);
            var brush = new XSolidBrush(ParseColor(run.Color));

            double x = line.X + run.OffsetX;
            double y = line.Y;

            g.DrawString(run.Text, font, brush, new XPoint(x, baseline));

            if (!string.IsNullOrEmpty(run.LinkUrl))
            {
                try
                {
                    var rect = new XRect(x, y, run.Width, line.Height);
                    page.AddWebLink(new PdfRectangle(rect), run.LinkUrl);
                }
                catch
                {
                    // annotation failure is non-fatal
                }
            }
        }
    }

    private XFont GetFont(string family, bool bold, bool italic, double size)
    {
        var key = (family, bold, italic, size);
        if (!_fontCache.TryGetValue(key, out var font))
        {
            var style = (bold, italic) switch
            {
                (true, true) => XFontStyleEx.BoldItalic,
                (true, false) => XFontStyleEx.Bold,
                (false, true) => XFontStyleEx.Italic,
                _ => XFontStyleEx.Regular
            };
            try
            {
                font = new XFont(family, size, style);
            }
            catch
            {
                font = new XFont("Arial", size, style);
            }
            _fontCache[key] = font;
        }
        return font;
    }

    private static XColor ParseColor(string color)
    {
        if (string.IsNullOrEmpty(color)) return XColors.Black;
        if (color.StartsWith("#") && (color.Length == 7 || color.Length == 9))
        {
            try
            {
                int r = Convert.ToInt32(color.Substring(1, 2), 16);
                int gn = Convert.ToInt32(color.Substring(3, 2), 16);
                int b = Convert.ToInt32(color.Substring(5, 2), 16);
                return XColor.FromArgb(r, gn, b);
            }
            catch
            {
                return XColors.Black;
            }
        }
        return XColors.Black;
    }
}
