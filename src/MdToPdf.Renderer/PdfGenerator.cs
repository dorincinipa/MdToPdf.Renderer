using MdToPdf.Renderer.Adapters;
using MdToPdf.Renderer.Layout;
using MdToPdf.Renderer.Parsing;
using MdToPdf.Renderer.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Security;
using PdfSharp.Pdf.Annotations;

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
        ApplySecurity(document, options.Security);
        if (options.CompressOutput)
        {
            document.Options.CompressContentStreams = true;
            document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        }
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

        ApplyAnnotations(document, layout, options.AutoBookmarks);
    }

    private static void ApplyAnnotations(PdfDocument document, LayoutResult layout, bool autoBookmarks)
    {
        // Pre-pass: collect all heading markers so forward anchor references resolve correctly.
        var headingAnchors = new Dictionary<string, (int pageIndex, double y)>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in layout.Blocks)
        {
            if (block is LayoutHeadingMarker m)
                headingAnchors.TryAdd(SlugifyHeading(m.Title), (m.PageIndex, m.Y));
        }

        // Main pass: apply bookmarks and link annotations together.
        var bookmarkStack = autoBookmarks ? new Stack<PdfOutline>() : null;
        foreach (var block in layout.Blocks)
        {
            switch (block)
            {
                case LayoutHeadingMarker marker when bookmarkStack is not null:
                    while (bookmarkStack.Count >= marker.Level)
                        bookmarkStack.Pop();

                    var outline = new PdfOutline(marker.Title, document.Pages[marker.PageIndex], false)
                    {
                        Top = layout.PageHeight - marker.Y
                    };
                    var parent = bookmarkStack.Count > 0 ? bookmarkStack.Peek().Outlines : document.Outlines;
                    parent.Add(outline);
                    bookmarkStack.Push(outline);
                    break;

                case LayoutLine line:
                    foreach (var run in line.Runs)
                    {
                        if (string.IsNullOrEmpty(run.LinkUrl)) continue;
                        if (run.LinkUrl.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;

                        var pdfRect = new PdfRectangle(
                            new XPoint(line.X + run.OffsetX, layout.PageHeight - line.Y - line.Height),
                            new XPoint(line.X + run.OffsetX + run.Width, layout.PageHeight - line.Y));

                        var page = document.Pages[line.PageIndex];

                        if (run.LinkUrl.StartsWith('#') && run.LinkUrl.Length > 1)
                        {
                            var slug = run.LinkUrl.Substring(1);
                            if (headingAnchors.TryGetValue(slug, out var target))
                            {
                                var annotation = PdfLinkAnnotation.CreateDocumentLink(
                                    pdfRect, target.pageIndex + 1,
                                    new XPoint(0, layout.PageHeight - target.y));
                                page.Annotations.Add(annotation);
                            }
                        }
                        else
                        {
                            page.AddWebLink(pdfRect, run.LinkUrl);
                        }
                    }
                    break;
            }
        }
    }

    private static string SlugifyHeading(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ') sb.Append('-');
        }
        return sb.ToString();
    }

    private static void ApplySecurity(PdfDocument document, PdfSecurityOptions? security)
    {
        if (security is null) return;

        if (string.IsNullOrEmpty(security.UserPassword) &&
            string.IsNullOrEmpty(security.OwnerPassword))
        {
            throw new InvalidOperationException(
                "PdfSecurityOptions requires at least UserPassword or OwnerPassword.");
        }

        var s = document.SecuritySettings;
        if (!string.IsNullOrEmpty(security.UserPassword))  s.UserPassword  = security.UserPassword;
        if (!string.IsNullOrEmpty(security.OwnerPassword)) s.OwnerPassword = security.OwnerPassword;

        var p = security.Permissions;
        s.PermitPrint            = p.HasFlag(PdfPermissions.Print);
        s.PermitFullQualityPrint = p.HasFlag(PdfPermissions.HighQualityPrint);
        s.PermitModifyDocument   = p.HasFlag(PdfPermissions.ModifyContent);
        s.PermitExtractContent   = p.HasFlag(PdfPermissions.CopyContent);
        s.PermitAnnotations      = p.HasFlag(PdfPermissions.Annotate);
        s.PermitFormsFill        = p.HasFlag(PdfPermissions.FillForms);
        s.PermitAssembleDocument = p.HasFlag(PdfPermissions.AssembleDocument);
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
