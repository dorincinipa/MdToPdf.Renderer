using System.Text;
using MdToPdf.Renderer.Parsing.Ast;

namespace MdToPdf.Renderer.Parsing;

internal static class InlineParser
{
    internal static List<InlineSpan> Parse(string? text)
    {
        var result = new List<InlineSpan>();
        if (string.IsNullOrEmpty(text)) return result;
        ParseRange(text, 0, text.Length, result);
        return result;
    }

    private static void ParseRange(string text, int start, int end, List<InlineSpan> output)
    {
        var buffer = new StringBuilder();
        int i = start;
        while (i < end)
        {
            char c = text[i];

            // escape
            if (c == '\\' && i + 1 < end && IsAsciiPunct(text[i + 1]))
            {
                // backslash + newline = hard break
                if (text[i + 1] == '\n')
                {
                    Flush(buffer, output);
                    output.Add(new LineBreakSpan());
                    i += 2;
                    continue;
                }
                buffer.Append(text[i + 1]);
                i += 2;
                continue;
            }

            // hard break: two+ trailing spaces before newline
            if (c == '\n')
            {
                int back = buffer.Length;
                int trailingSpaces = 0;
                while (back > 0 && buffer[back - 1] == ' ') { trailingSpaces++; back--; }
                if (trailingSpaces >= 2)
                {
                    buffer.Length = back; // trim spaces
                    Flush(buffer, output);
                    output.Add(new LineBreakSpan());
                    i++;
                    continue;
                }
                // soft break renders as space
                buffer.Append(' ');
                i++;
                continue;
            }

            // code span
            if (c == '`')
            {
                int runLen = 0;
                while (i + runLen < end && text[i + runLen] == '`') runLen++;
                int closer = FindMatchingBacktickRun(text, i + runLen, end, runLen);
                if (closer >= 0)
                {
                    Flush(buffer, output);
                    var content = text.Substring(i + runLen, closer - (i + runLen));
                    // trim one leading and trailing space per CommonMark if content both has them and is not all spaces
                    if (content.Length >= 2 && content[0] == ' ' && content[^1] == ' ' && content.TrimEnd().Length > 0)
                        content = content.Substring(1, content.Length - 2);
                    // normalize internal newlines to spaces
                    content = content.Replace('\n', ' ');
                    output.Add(new CodeSpan(content));
                    i = closer + runLen;
                    continue;
                }
                // no closer → literal
                buffer.Append(text, i, runLen);
                i += runLen;
                continue;
            }

            // image ![alt](src)
            if (c == '!' && i + 1 < end && text[i + 1] == '[')
            {
                if (TryParseImage(text, i, end, out var image, out int next))
                {
                    Flush(buffer, output);
                    output.Add(image);
                    i = next;
                    continue;
                }
            }

            // link [text](url)
            if (c == '[')
            {
                if (TryParseLink(text, i, end, out var link, out int next))
                {
                    Flush(buffer, output);
                    output.Add(link);
                    i = next;
                    continue;
                }
            }

            // emphasis: * or _
            if ((c == '*' || c == '_'))
            {
                if (TryParseEmphasis(text, i, end, out var span, out int next))
                {
                    Flush(buffer, output);
                    output.Add(span);
                    i = next;
                    continue;
                }
            }

            buffer.Append(c);
            i++;
        }

        Flush(buffer, output);
    }

    private static void Flush(StringBuilder buffer, List<InlineSpan> output)
    {
        if (buffer.Length == 0) return;
        output.Add(new TextSpan(buffer.ToString()));
        buffer.Clear();
    }

    private static bool IsAsciiPunct(char c)
    {
        if (c == '\n') return true;
        return "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".IndexOf(c) >= 0;
    }

    private static int FindMatchingBacktickRun(string text, int from, int end, int runLen)
    {
        int i = from;
        while (i < end)
        {
            if (text[i] == '`')
            {
                int r = 0;
                while (i + r < end && text[i + r] == '`') r++;
                if (r == runLen) return i;
                i += r;
            }
            else i++;
        }
        return -1;
    }

    private static bool TryParseImage(string text, int start, int end, out ImageSpan image, out int next)
    {
        image = null!;
        next = start;
        // ![alt](src)
        if (text[start] != '!' || start + 1 >= end || text[start + 1] != '[') return false;
        if (!TryParseLinkCore(text, start + 1, end, out var altText, out var src, out int consumed)) return false;
        image = new ImageSpan { Alt = altText, Src = src };
        next = consumed;
        return true;
    }

    private static bool TryParseLink(string text, int start, int end, out LinkSpan link, out int next)
    {
        link = null!;
        next = start;
        if (text[start] != '[') return false;
        if (!TryParseLinkCore(text, start, end, out var linkText, out var url, out int consumed)) return false;
        link = new LinkSpan { Url = url };
        link.Children.AddRange(Parse(linkText));
        next = consumed;
        return true;
    }

    private static bool TryParseLinkCore(string text, int bracketStart, int end, out string linkText, out string url, out int consumed)
    {
        linkText = string.Empty;
        url = string.Empty;
        consumed = bracketStart;

        // find matching ']' — allow balanced brackets
        int depth = 0;
        int closeBracket = -1;
        for (int j = bracketStart; j < end; j++)
        {
            char ch = text[j];
            if (ch == '\\' && j + 1 < end) { j++; continue; }
            if (ch == '`')
            {
                // skip over a code span to avoid matching ] inside
                int r = 0;
                while (j + r < end && text[j + r] == '`') r++;
                int close = FindMatchingBacktickRun(text, j + r, end, r);
                if (close >= 0) j = close + r - 1;
                continue;
            }
            if (ch == '[') depth++;
            else if (ch == ']')
            {
                depth--;
                if (depth == 0) { closeBracket = j; break; }
            }
        }
        if (closeBracket < 0) return false;
        if (closeBracket + 1 >= end || text[closeBracket + 1] != '(') return false;

        // parse url up to matching ')', permit balanced parens inside url
        int urlStart = closeBracket + 2;
        int urlEnd = -1;
        int pdepth = 1;
        for (int j = urlStart; j < end; j++)
        {
            char ch = text[j];
            if (ch == '\\' && j + 1 < end) { j++; continue; }
            if (ch == '(') pdepth++;
            else if (ch == ')')
            {
                pdepth--;
                if (pdepth == 0) { urlEnd = j; break; }
            }
            else if (ch == ' ' || ch == '\t' || ch == '\n')
            {
                // url cannot contain raw whitespace (unless angle-bracketed which we don't support here)
                // treat as failure
                return false;
            }
        }
        if (urlEnd < 0) return false;

        linkText = text.Substring(bracketStart + 1, closeBracket - bracketStart - 1);
        url = text.Substring(urlStart, urlEnd - urlStart);
        consumed = urlEnd + 1;
        return true;
    }

    private static bool TryParseEmphasis(string text, int start, int end, out InlineSpan span, out int next)
    {
        span = null!;
        next = start;
        char c = text[start];
        if (c != '*' && c != '_') return false;

        int runLen = 0;
        while (start + runLen < end && text[start + runLen] == c) runLen++;
        if (runLen == 0) return false;
        if (runLen > 3) runLen = 3;

        int afterOpener = start + runLen;
        if (afterOpener >= end) return false;
        if (char.IsWhiteSpace(text[afterOpener])) return false;

        // for `_` require the opener not to be preceded by alphanumeric (CommonMark intraword rule)
        if (c == '_' && start > 0 && char.IsLetterOrDigit(text[start - 1])) return false;

        int closerPos = FindEmphasisCloser(text, afterOpener, end, c, runLen);
        if (closerPos < 0) return false;

        var inner = new List<InlineSpan>();
        ParseRange(text, afterOpener, closerPos, inner);

        span = BuildEmphasisSpan(runLen, inner);
        next = closerPos + runLen;
        return true;
    }

    private static int FindEmphasisCloser(string text, int from, int end, char c, int runLen)
    {
        int i = from;
        while (i < end)
        {
            char ch = text[i];
            if (ch == '\\' && i + 1 < end) { i += 2; continue; }
            if (ch == '`')
            {
                int r = 0;
                while (i + r < end && text[i + r] == '`') r++;
                int close = FindMatchingBacktickRun(text, i + r, end, r);
                if (close >= 0) { i = close + r; continue; }
                i += r;
                continue;
            }
            if (ch == c)
            {
                int r = 0;
                while (i + r < end && text[i + r] == c) r++;
                if (r == runLen && i > 0 && !char.IsWhiteSpace(text[i - 1]))
                {
                    if (c == '_' && i + r < end && char.IsLetterOrDigit(text[i + r])) { i += r; continue; }
                    return i;
                }
                i += r;
                continue;
            }
            i++;
        }
        return -1;
    }

    private static InlineSpan BuildEmphasisSpan(int runLen, List<InlineSpan> inner)
    {
        if (runLen == 1)
        {
            var it = new ItalicSpan();
            it.Children.AddRange(inner);
            return it;
        }
        if (runLen == 2)
        {
            var b = new BoldSpan();
            b.Children.AddRange(inner);
            return b;
        }
        // 3: bold containing italic
        var it3 = new ItalicSpan();
        it3.Children.AddRange(inner);
        var b3 = new BoldSpan();
        b3.Children.Add(it3);
        return b3;
    }
}
