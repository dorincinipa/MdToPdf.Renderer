using MdToPdf.Renderer.Adapters;
using MdToPdf.Renderer.Layout;
using MdToPdf.Renderer.Parsing;
using MdToPdf.Renderer.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace MdToPdf.Renderer;

public static class PdfGenerator
{
    static PdfGenerator()
    {
        try
        {
            PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
        catch
        {
            // Non-Windows hosts or a host that already configured fonts differently —
            // the custom FontResolver will handle fallback either way.
        }
    }

    public static PdfBuilder Create() => new();

    public static void LoadFontsFromFolder(string folderPath)
        => FontResolver.Instance.LoadFontsFromFolder(folderPath);

    public static PdfDocument GeneratePdf(string markdown, PdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var document = new PdfDocument();
        AddPdfPages(document, markdown, options);
        return document;
    }

    public static async Task<byte[]> GeneratePdfAsync(string markdown, PdfOptions options)
    {
        using var document = GeneratePdf(markdown, options);
        using var stream = new MemoryStream();
        await document.SaveAsync(stream, false);
        return stream.ToArray();
    }

    public static void AddPdfPages(PdfDocument document, string markdown, PdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var effective = options.GetEffectivePageSize();
        using var measureCtx = XGraphics.CreateMeasureContext(
            new XSize(effective.Width, effective.Height),
            XGraphicsUnit.Point,
            XPageDirection.Downwards);

        var ast = MarkdownParser.Parse(markdown);
        var layout = new LayoutEngine(options, measureCtx).Layout(ast);

        using var imageLoader = new ImageLoader(options.ImageLoadFailureMode);
        ProbeImages(ast, imageLoader);
        var paint = new PaintEngine(options, imageLoader);
        paint.Paint(document, layout);
    }

    private static void ProbeImages(List<Parsing.Ast.MarkdownBlock> blocks, ImageLoader loader)
    {
        foreach (var block in blocks)
            ProbeBlock(block, loader);
    }

    private static void ProbeBlock(Parsing.Ast.MarkdownBlock block, ImageLoader loader)
    {
        switch (block)
        {
            case Parsing.Ast.Paragraph p:
                ProbeInlines(p.Inlines, loader); break;
            case Parsing.Ast.Heading h:
                ProbeInlines(h.Inlines, loader); break;
            case Parsing.Ast.Blockquote bq:
                ProbeImages(bq.Children, loader); break;
            case Parsing.Ast.BulletList bl:
                foreach (var item in bl.Items) ProbeImages(item.Children, loader);
                break;
            case Parsing.Ast.OrderedList ol:
                foreach (var item in ol.Items) ProbeImages(item.Children, loader);
                break;
        }
    }

    private static void ProbeInlines(List<Parsing.Ast.InlineSpan> inlines, ImageLoader loader)
    {
        foreach (var span in inlines)
        {
            switch (span)
            {
                case Parsing.Ast.ImageSpan img:
                    loader.Load(img.Src);
                    break;
                case Parsing.Ast.BoldSpan b: ProbeInlines(b.Children, loader); break;
                case Parsing.Ast.ItalicSpan i: ProbeInlines(i.Children, loader); break;
                case Parsing.Ast.LinkSpan l: ProbeInlines(l.Children, loader); break;
            }
        }
    }
}
