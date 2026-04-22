using System.Text;
using MdToPdf.Renderer.Parsing.Ast;
using PdfSharp.Drawing;

namespace MdToPdf.Renderer.Layout;

internal sealed class LayoutEngine
{
    private readonly PdfOptions _options;
    private readonly XGraphics _measure;
    private readonly double _pageWidth;
    private readonly double _pageHeight;
    private readonly double _marginLeft;
    private readonly double _marginTop;
    private readonly double _marginBottom;
    private readonly double _contentWidth;
    private readonly double _contentHeight;
    private readonly Dictionary<(string family, bool bold, bool italic, double size), XFont> _fontCache = new();
    private readonly Dictionary<string, (double W, double H)?> _imageDimCache = new();
    private readonly LayoutResult _result = new();

    private int _currentPage;
    private double _y;

    private const string LinkColor = "#1a73e8";
    private const string CodeBackgroundColor = "#f4f4f4";
    private const double CodeBlockPadding = 4;
    private const double ListIndent = 20;
    private const double BlockquoteIndent = 16;
    private const double BlockquoteBarWidth = 3;
    private const double ParagraphSpacing = 0.5; // fraction of body line height

    internal LayoutEngine(PdfOptions options, XGraphics measureContext)
    {
        _options = options;
        _measure = measureContext;
        var size = options.GetEffectivePageSize();
        _pageWidth = size.Width;
        _pageHeight = size.Height;
        _marginLeft = options.MarginLeft;
        _marginTop = options.MarginTop;
        _marginBottom = options.MarginBottom;
        _contentWidth = _pageWidth - _marginLeft - options.MarginRight;
        _contentHeight = _pageHeight - _marginTop - _marginBottom;
        _result.PageWidth = _pageWidth;
        _result.PageHeight = _pageHeight;
        _currentPage = 0;
        _y = _marginTop;
    }

    internal LayoutResult Layout(List<MarkdownBlock> blocks)
    {
        LayoutBlocks(blocks, _marginLeft, _contentWidth);
        _result.PageCount = _currentPage + 1;
        return _result;
    }

    private double PageBottom => _pageHeight - _marginBottom;

    private void AdvancePage()
    {
        _currentPage++;
        _y = _marginTop;
    }

    private void EnsureSpace(double height)
    {
        if (_y + height > PageBottom)
            AdvancePage();
    }

    private void LayoutBlocks(List<MarkdownBlock> blocks, double x, double width)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            bool isFirst = i == 0;
            bool isLast = i == blocks.Count - 1;

            switch (block)
            {
                case Heading h: LayoutHeading(h, x, width, blocks, i); break;
                case Paragraph p: LayoutParagraph(p, x, width); break;
                case ThematicBreak: LayoutThematicBreak(x, width); break;
                case CodeBlock cb: LayoutCodeBlock(cb, x, width); break;
                case BulletList bl: LayoutBulletList(bl, x, width); break;
                case OrderedList ol: LayoutOrderedList(ol, x, width); break;
                case Blockquote bq: LayoutBlockquote(bq, x, width); break;
            }
            if (!isLast)
                _y += ParagraphSpacing * BodyLineHeight();
        }
    }

    private double BodyFontSize() => _options.BaseFontSize;
    private double BodyLineHeight() => _options.BaseFontSize * _options.LineHeight;

    private double HeadingFontSize(int level) => level switch
    {
        1 => _options.BaseFontSize * 2.0,
        2 => _options.BaseFontSize * 1.5,
        3 => _options.BaseFontSize * 1.25,
        4 => _options.BaseFontSize * 1.0,
        5 => _options.BaseFontSize * 0.9,
        _ => _options.BaseFontSize * 0.85
    };

    private void LayoutHeading(Heading h, double x, double width, List<MarkdownBlock> siblings, int index)
    {
        var fontSize = HeadingFontSize(h.Level);
        var lineHeight = fontSize * _options.LineHeight;

        // Heading glue: require space for heading + one body line unless heading is last block
        bool hasFollowing = index < siblings.Count - 1;
        double needed = lineHeight + (hasFollowing ? BodyLineHeight() : 0);
        EnsureSpace(needed);

        var style = new InlineStyle
        {
            FontSize = fontSize,
            Bold = true,
            Family = _options.BodyFontFamily,
            Color = "#000000"
        };
        var runs = FlattenInlines(h.Inlines, style);
        var lines = WrapRuns(runs, width);
        EmitLines(lines, x, fontSize);
    }

    private void LayoutParagraph(Paragraph p, double x, double width)
    {
        var fontSize = BodyFontSize();
        var style = new InlineStyle
        {
            FontSize = fontSize,
            Family = _options.BodyFontFamily,
            Color = "#000000"
        };
        var runs = FlattenInlines(p.Inlines, style);
        var lines = WrapRuns(runs, width);
        EmitLines(lines, x, fontSize);
    }

    private void LayoutThematicBreak(double x, double width)
    {
        double gap = BodyLineHeight() * 0.4;
        EnsureSpace(gap + 1);
        _y += gap / 2;
        _result.Blocks.Add(new LayoutRule
        {
            PageIndex = _currentPage,
            X = x,
            Y = _y,
            Width = width,
            Height = 1
        });
        _y += 1 + gap / 2;
    }

    private void LayoutCodeBlock(CodeBlock cb, double x, double width)
    {
        var fontSize = BodyFontSize() * 0.95;
        var lineHeight = fontSize * _options.LineHeight;
        var lines = cb.Content.Split('\n');

        int lineIndex = 0;
        while (lineIndex < lines.Length)
        {
            // how many lines fit on the current page?
            if (_y + lineHeight + 2 * CodeBlockPadding > PageBottom)
                AdvancePage();

            int pageStartLine = lineIndex;
            double startY = _y;
            double available = PageBottom - _y - 2 * CodeBlockPadding;
            int linesThatFit = Math.Max(1, (int)Math.Floor(available / lineHeight));
            int linesThisPage = Math.Min(linesThatFit, lines.Length - lineIndex);

            _result.Blocks.Add(new LayoutCodeBackground
            {
                PageIndex = _currentPage,
                X = x,
                Y = startY,
                Width = width,
                Height = linesThisPage * lineHeight + 2 * CodeBlockPadding
            });

            _y = startY + CodeBlockPadding;
            for (int k = 0; k < linesThisPage; k++)
            {
                var raw = lines[lineIndex + k];
                var run = new StyledRun
                {
                    Text = raw,
                    Monospace = true,
                    FontSize = fontSize,
                    OffsetX = 0,
                    Width = MeasureText(raw, _options.MonospaceFontFamily, false, false, fontSize)
                };
                var line = new LayoutLine
                {
                    PageIndex = _currentPage,
                    X = x + CodeBlockPadding,
                    Y = _y,
                    Width = width - 2 * CodeBlockPadding,
                    Height = lineHeight,
                    FontSize = fontSize,
                    Baseline = fontSize
                };
                line.Runs.Add(run);
                _result.Blocks.Add(line);
                _y += lineHeight;
            }
            _y = startY + linesThisPage * lineHeight + 2 * CodeBlockPadding;
            lineIndex += linesThisPage;
        }
    }

    private void LayoutBulletList(BulletList list, double x, double width)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            LayoutListItem(list.Items[i], marker: "• ", x, width);
        }
    }

    private void LayoutOrderedList(OrderedList list, double x, double width)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            LayoutListItem(list.Items[i], marker: $"{list.StartIndex + i}. ", x, width);
        }
    }

    private void LayoutListItem(ListItem item, string marker, double x, double width)
    {
        double markerWidth = MeasureText(marker, _options.BodyFontFamily, false, false, BodyFontSize());
        double itemIndent = Math.Max(ListIndent, markerWidth + 4);
        double innerX = x + itemIndent;
        double innerWidth = width - itemIndent;

        double markerY = _y;
        int markerPage = _currentPage;

        LayoutBlocks(item.Children, innerX, innerWidth);

        // Insert marker aligned with first line of item
        var markerRun = new StyledRun
        {
            Text = marker,
            FontSize = BodyFontSize(),
            Width = markerWidth,
            OffsetX = 0
        };
        var markerLine = new LayoutLine
        {
            PageIndex = markerPage,
            X = x,
            Y = markerY,
            Width = itemIndent,
            Height = BodyLineHeight(),
            FontSize = BodyFontSize(),
            Baseline = BodyFontSize()
        };
        markerLine.Runs.Add(markerRun);
        _result.Blocks.Add(markerLine);
    }

    private void LayoutBlockquote(Blockquote bq, double x, double width)
    {
        double innerX = x + BlockquoteIndent;
        double innerWidth = width - BlockquoteIndent;
        double startY = _y;
        int startPage = _currentPage;

        LayoutBlocks(bq.Children, innerX, innerWidth);

        // Emit vertical bar(s), possibly spanning pages
        int page = startPage;
        double barTop = startY;
        while (page < _currentPage)
        {
            _result.Blocks.Add(new LayoutBlockquoteBar
            {
                PageIndex = page,
                X = x + 4,
                Y = barTop,
                Width = BlockquoteBarWidth,
                Height = PageBottom - barTop
            });
            page++;
            barTop = _marginTop;
        }
        _result.Blocks.Add(new LayoutBlockquoteBar
        {
            PageIndex = page,
            X = x + 4,
            Y = barTop,
            Width = BlockquoteBarWidth,
            Height = _y - barTop
        });
    }

    // ---- Inline flattening and wrapping ----

    private sealed class InlineStyle
    {
        public double FontSize;
        public bool Bold;
        public bool Italic;
        public bool Monospace;
        public string Family = string.Empty;
        public string Color = "#000000";
        public string? LinkUrl;

        public InlineStyle Clone() => new()
        {
            FontSize = FontSize,
            Bold = Bold,
            Italic = Italic,
            Monospace = Monospace,
            Family = Family,
            Color = Color,
            LinkUrl = LinkUrl
        };
    }

    private sealed class InlineToken
    {
        public string Text = string.Empty;
        public bool IsBreak;
        public bool IsImage;
        public string? ImageSrc;
        public string? ImageAlt;
        public InlineStyle Style = null!;
    }

    private List<InlineToken> FlattenInlines(List<InlineSpan> inlines, InlineStyle baseStyle)
    {
        var tokens = new List<InlineToken>();
        foreach (var span in inlines)
            Flatten(span, baseStyle, tokens);
        return tokens;
    }

    private void Flatten(InlineSpan span, InlineStyle style, List<InlineToken> tokens)
    {
        switch (span)
        {
            case TextSpan t:
                tokens.Add(new InlineToken { Text = t.Text, Style = style });
                break;
            case BoldSpan b:
                {
                    var s = style.Clone(); s.Bold = true;
                    foreach (var c in b.Children) Flatten(c, s, tokens);
                    break;
                }
            case ItalicSpan i:
                {
                    var s = style.Clone(); s.Italic = true;
                    foreach (var c in i.Children) Flatten(c, s, tokens);
                    break;
                }
            case CodeSpan cs:
                {
                    var s = style.Clone();
                    s.Monospace = true;
                    s.Family = _options.MonospaceFontFamily;
                    tokens.Add(new InlineToken { Text = cs.Text, Style = s });
                    break;
                }
            case LinkSpan l:
                {
                    var s = style.Clone();
                    s.Color = LinkColor;
                    s.LinkUrl = l.Url;
                    foreach (var c in l.Children) Flatten(c, s, tokens);
                    break;
                }
            case ImageSpan im:
                tokens.Add(new InlineToken
                {
                    IsImage = true,
                    ImageSrc = im.Src,
                    ImageAlt = im.Alt,
                    Style = style
                });
                break;
            case LineBreakSpan:
                tokens.Add(new InlineToken { IsBreak = true, Style = style });
                break;
        }
    }

    private sealed class WordPiece
    {
        public string Text = string.Empty;
        public double Width;
        public InlineStyle Style = null!;
        public bool IsSpace;
        public bool IsBreak;
    }

    private List<WordPiece> TokenizeForWrap(List<InlineToken> tokens)
    {
        var pieces = new List<WordPiece>();
        foreach (var t in tokens)
        {
            if (t.IsBreak)
            {
                pieces.Add(new WordPiece { IsBreak = true, Style = t.Style });
                continue;
            }
            if (t.IsImage)
            {
                // Treat inline image as an alt-text word (actual image rendering is block-level in our layout —
                // if inside paragraph, fall back to alt text).
                var alt = string.IsNullOrEmpty(t.ImageAlt) ? "[image]" : $"[{t.ImageAlt}]";
                pieces.Add(new WordPiece
                {
                    Text = alt,
                    Style = t.Style,
                    Width = MeasureText(alt, t.Style.Family.Length > 0 ? t.Style.Family : _options.BodyFontFamily, t.Style.Bold, t.Style.Italic, t.Style.FontSize)
                });
                continue;
            }
            // Split on whitespace, keep spaces as separate pieces
            var text = t.Text;
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == ' ' || text[i] == '\t' || text[i] == '\n')
                {
                    int j = i;
                    while (j < text.Length && (text[j] == ' ' || text[j] == '\t' || text[j] == '\n')) j++;
                    pieces.Add(new WordPiece
                    {
                        Text = " ",
                        IsSpace = true,
                        Style = t.Style,
                        Width = MeasureText(" ", t.Style.Family.Length > 0 ? t.Style.Family : _options.BodyFontFamily, t.Style.Bold, t.Style.Italic, t.Style.FontSize)
                    });
                    i = j;
                }
                else
                {
                    int j = i;
                    while (j < text.Length && text[j] != ' ' && text[j] != '\t' && text[j] != '\n') j++;
                    var word = text.Substring(i, j - i);
                    pieces.Add(new WordPiece
                    {
                        Text = word,
                        Style = t.Style,
                        Width = MeasureText(word, t.Style.Family.Length > 0 ? t.Style.Family : _options.BodyFontFamily, t.Style.Bold, t.Style.Italic, t.Style.FontSize)
                    });
                    i = j;
                }
            }
        }
        return pieces;
    }

    private List<List<WordPiece>> WrapRuns(List<InlineToken> tokens, double maxWidth)
    {
        var lines = new List<List<WordPiece>>();
        var pieces = TokenizeForWrap(tokens);
        var current = new List<WordPiece>();
        double cw = 0;
        foreach (var p in pieces)
        {
            if (p.IsBreak)
            {
                lines.Add(current);
                current = new List<WordPiece>();
                cw = 0;
                continue;
            }
            if (p.IsSpace && current.Count == 0) continue; // trim leading space
            if (cw + p.Width > maxWidth && current.Count > 0 && !p.IsSpace)
            {
                // finish line (trim trailing spaces)
                TrimTrailingSpaces(current, ref cw);
                lines.Add(current);
                current = new List<WordPiece>();
                cw = 0;
                if (p.IsSpace) continue;
            }
            current.Add(p);
            cw += p.Width;
        }
        if (current.Count > 0)
        {
            TrimTrailingSpaces(current, ref cw);
            lines.Add(current);
        }
        return lines;
    }

    private static void TrimTrailingSpaces(List<WordPiece> pieces, ref double width)
    {
        while (pieces.Count > 0 && pieces[^1].IsSpace)
        {
            width -= pieces[^1].Width;
            pieces.RemoveAt(pieces.Count - 1);
        }
    }

    private void EmitLines(List<List<WordPiece>> lines, double x, double blockFontSize)
    {
        foreach (var piecesLine in lines)
        {
            double maxSize = blockFontSize;
            foreach (var p in piecesLine)
                if (p.Style.FontSize > maxSize) maxSize = p.Style.FontSize;
            double lineHeight = maxSize * _options.LineHeight;

            EnsureSpace(lineHeight);

            var line = new LayoutLine
            {
                PageIndex = _currentPage,
                X = x,
                Y = _y,
                Width = 0,
                Height = lineHeight,
                FontSize = maxSize,
                Baseline = maxSize
            };
            double offset = 0;
            foreach (var piece in piecesLine)
            {
                line.Runs.Add(new StyledRun
                {
                    Text = piece.Text,
                    Bold = piece.Style.Bold,
                    Italic = piece.Style.Italic,
                    Monospace = piece.Style.Monospace,
                    Color = piece.Style.Color,
                    LinkUrl = piece.Style.LinkUrl,
                    FontSize = piece.Style.FontSize,
                    OffsetX = offset,
                    Width = piece.Width
                });
                offset += piece.Width;
            }
            line.Width = offset;
            _result.Blocks.Add(line);
            _y += lineHeight;
        }
    }

    private double MeasureText(string text, string family, bool bold, bool italic, double size)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var font = GetFont(family, bold, italic, size);
        return _measure.MeasureString(text, font).Width;
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
}
