using MdToPdf.Renderer.Layout;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace MdToPdf.Renderer.Rendering;

internal sealed class PaintEngine
{
    private readonly PdfOptions _options;
    private readonly InlineRenderer _inlineRenderer;
    private readonly ImageLoader _imageLoader;

    internal PaintEngine(PdfOptions options, ImageLoader imageLoader)
    {
        _options = options;
        _inlineRenderer = new InlineRenderer(options);
        _imageLoader = imageLoader;
    }

    internal void Paint(PdfDocument document, LayoutResult layout)
    {
        int pages = Math.Max(1, layout.PageCount);
        var blocksByPage = layout.Blocks
            .GroupBy(b => b.PageIndex)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (int i = 0; i < pages; i++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(layout.PageWidth);
            page.Height = XUnit.FromPoint(layout.PageHeight);
            using var g = XGraphics.FromPdfPage(page);

            if (!blocksByPage.TryGetValue(i, out var list)) continue;

            // Draw backgrounds and bars first so text is on top
            foreach (var block in list.OfType<LayoutCodeBackground>())
                DrawCodeBackground(g, block);
            foreach (var bar in list.OfType<LayoutBlockquoteBar>())
                DrawBlockquoteBar(g, bar);

            foreach (var block in list)
            {
                switch (block)
                {
                    case LayoutLine line:
                        _inlineRenderer.DrawLine(g, page, line);
                        break;
                    case LayoutRule rule:
                        DrawRule(g, rule);
                        break;
                    case LayoutImageBlock img:
                        DrawImage(g, img);
                        break;
                }
            }
        }
    }

    private static void DrawRule(XGraphics g, LayoutRule rule)
    {
        var pen = new XPen(XColor.FromArgb(200, 200, 200), 1);
        double y = rule.Y + rule.Height / 2;
        g.DrawLine(pen, rule.X, y, rule.X + rule.Width, y);
    }

    private static void DrawCodeBackground(XGraphics g, LayoutCodeBackground bg)
    {
        var brush = new XSolidBrush(XColor.FromArgb(244, 244, 244));
        g.DrawRectangle(brush, bg.X, bg.Y, bg.Width, bg.Height);
    }

    private static void DrawBlockquoteBar(XGraphics g, LayoutBlockquoteBar bar)
    {
        var brush = new XSolidBrush(XColor.FromArgb(200, 200, 200));
        g.DrawRectangle(brush, bar.X, bar.Y, bar.Width, bar.Height);
    }

    private void DrawImage(XGraphics g, LayoutImageBlock block)
    {
        var image = _imageLoader.Load(block.Src);
        if (image is null) return;
        g.DrawImage(image, block.X, block.Y, block.Width, block.Height);
    }
}
