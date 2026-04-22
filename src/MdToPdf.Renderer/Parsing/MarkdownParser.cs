using System.Text;
using MdToPdf.Renderer.Parsing.Ast;

namespace MdToPdf.Renderer.Parsing;

internal static class MarkdownParser
{
    internal static List<MarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(markdown))
            return blocks;

        var lines = Normalize(markdown).Split('\n');
        ParseBlocks(lines, 0, lines.Length, baseIndent: 0, blocks);
        return blocks;
    }

    private static string Normalize(string input)
    {
        return input.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static void ParseBlocks(string[] lines, int start, int end, int baseIndent, List<MarkdownBlock> output)
    {
        int i = start;
        while (i < end)
        {
            var line = lines[i];
            var stripped = StripIndent(line, baseIndent);

            if (IsBlank(stripped))
            {
                i++;
                continue;
            }

            if (TryParseThematicBreak(stripped))
            {
                output.Add(new ThematicBreak());
                i++;
                continue;
            }

            if (TryParseAtxHeading(stripped, out var heading))
            {
                output.Add(heading);
                i++;
                continue;
            }

            if (TryParseFencedCodeBlock(lines, i, end, baseIndent, out var codeBlock, out var fenceEnd))
            {
                output.Add(codeBlock);
                i = fenceEnd;
                continue;
            }

            if (IsBlockquoteLine(stripped))
            {
                i = ParseBlockquote(lines, i, end, baseIndent, output);
                continue;
            }

            if (TryGetListMarker(stripped, out var marker))
            {
                i = ParseList(lines, i, end, baseIndent, marker, output);
                continue;
            }

            i = ParseParagraph(lines, i, end, baseIndent, output);
        }
    }

    private static string StripIndent(string line, int baseIndent)
    {
        if (baseIndent <= 0) return line;
        int removed = 0;
        int idx = 0;
        while (idx < line.Length && removed < baseIndent && line[idx] == ' ')
        {
            idx++;
            removed++;
        }
        return line.Substring(idx);
    }

    private static bool IsBlank(string line)
    {
        for (int i = 0; i < line.Length; i++)
            if (line[i] != ' ' && line[i] != '\t') return false;
        return true;
    }

    private static int CountLeadingSpaces(string line)
    {
        int count = 0;
        while (count < line.Length && line[count] == ' ') count++;
        return count;
    }

    private static bool TryParseThematicBreak(string line)
    {
        var trimmed = line.TrimStart(' ');
        if (trimmed.Length == 0) return false;
        var leadingSpaces = line.Length - trimmed.Length;
        if (leadingSpaces > 3) return false;

        char c = trimmed[0];
        if (c != '-' && c != '*' && c != '_') return false;

        int count = 0;
        foreach (var ch in trimmed)
        {
            if (ch == c) count++;
            else if (ch != ' ' && ch != '\t') return false;
        }
        return count >= 3;
    }

    private static bool TryParseAtxHeading(string line, out Heading heading)
    {
        heading = null!;
        var trimmed = line.TrimStart(' ');
        if (trimmed.Length - line.TrimStart(' ').Length > 0) { /* unreachable */ }
        var leadingSpaces = line.Length - trimmed.Length;
        if (leadingSpaces > 3) return false;
        if (trimmed.Length == 0 || trimmed[0] != '#') return false;

        int level = 0;
        while (level < trimmed.Length && trimmed[level] == '#') level++;
        if (level == 0 || level > 6) return false;
        if (level < trimmed.Length && trimmed[level] != ' ' && trimmed[level] != '\t') return false;

        var content = level < trimmed.Length ? trimmed.Substring(level).Trim() : string.Empty;
        // strip trailing run of '#'
        int end = content.Length;
        while (end > 0 && content[end - 1] == '#') end--;
        if (end < content.Length && end > 0 && content[end - 1] != ' ')
        {
            // trailing '#' not preceded by space means it was part of the text
            end = content.Length;
        }
        content = content.Substring(0, end).TrimEnd();

        heading = new Heading { Level = level };
        heading.Inlines.AddRange(InlineParser.Parse(content));
        return true;
    }

    private static bool TryParseFencedCodeBlock(string[] lines, int start, int end, int baseIndent, out CodeBlock block, out int nextIndex)
    {
        block = null!;
        nextIndex = start;
        var stripped = StripIndent(lines[start], baseIndent);
        var trimmed = stripped.TrimStart(' ');
        var leadingSpaces = stripped.Length - trimmed.Length;
        if (leadingSpaces > 3) return false;
        if (trimmed.Length < 3) return false;

        char fenceChar = trimmed[0];
        if (fenceChar != '`' && fenceChar != '~') return false;

        int fenceLen = 0;
        while (fenceLen < trimmed.Length && trimmed[fenceLen] == fenceChar) fenceLen++;
        if (fenceLen < 3) return false;

        var info = trimmed.Substring(fenceLen).Trim();
        if (fenceChar == '`' && info.Contains('`')) return false;

        var body = new StringBuilder();
        int i = start + 1;
        bool closed = false;
        while (i < end)
        {
            var cur = StripIndent(lines[i], baseIndent);
            var curTrim = cur.TrimStart(' ');
            var curLead = cur.Length - curTrim.Length;
            if (curLead <= 3 && curTrim.Length >= fenceLen)
            {
                bool allFence = true;
                int runLen = 0;
                foreach (var ch in curTrim)
                {
                    if (ch == fenceChar) { runLen++; continue; }
                    if (runLen >= fenceLen && (ch == ' ' || ch == '\t')) continue;
                    allFence = false;
                    break;
                }
                if (allFence && runLen >= fenceLen)
                {
                    closed = true;
                    i++;
                    break;
                }
            }
            body.AppendLine(cur);
            i++;
        }

        var content = body.ToString();
        if (content.EndsWith(Environment.NewLine))
            content = content.Substring(0, content.Length - Environment.NewLine.Length);
        else if (content.EndsWith("\n"))
            content = content.Substring(0, content.Length - 1);
        content = content.Replace(Environment.NewLine, "\n");

        block = new CodeBlock
        {
            InfoString = string.IsNullOrEmpty(info) ? null : info,
            Content = content
        };
        nextIndex = i;
        _ = closed;
        return true;
    }

    private static bool IsBlockquoteLine(string line)
    {
        var trimmed = line.TrimStart(' ');
        var leadingSpaces = line.Length - trimmed.Length;
        return leadingSpaces <= 3 && trimmed.Length > 0 && trimmed[0] == '>';
    }

    private static int ParseBlockquote(string[] lines, int start, int end, int baseIndent, List<MarkdownBlock> output)
    {
        var innerLines = new List<string>();
        int i = start;
        while (i < end)
        {
            var stripped = StripIndent(lines[i], baseIndent);
            if (IsBlockquoteLine(stripped))
            {
                var trimmed = stripped.TrimStart(' ');
                var afterMarker = trimmed.Substring(1);
                if (afterMarker.StartsWith(' ')) afterMarker = afterMarker.Substring(1);
                innerLines.Add(afterMarker);
                i++;
                continue;
            }
            if (IsBlank(stripped))
                break;
            if (TryParseThematicBreak(stripped) || TryParseAtxHeading(stripped, out _))
                break;
            if (TryGetListMarker(stripped, out _))
                break;
            // lazy continuation
            innerLines.Add(stripped);
            i++;
        }
        var bq = new Blockquote();
        ParseBlocks(innerLines.ToArray(), 0, innerLines.Count, 0, bq.Children);
        output.Add(bq);
        return i;
    }

    private readonly struct ListMarker
    {
        public readonly bool Ordered;
        public readonly int StartNumber;
        public readonly char BulletChar;
        public readonly int Indent;        // indent of the marker line
        public readonly int ContentIndent; // where item body text begins (used for continuation)

        public ListMarker(bool ordered, int startNumber, char bulletChar, int indent, int contentIndent)
        {
            Ordered = ordered;
            StartNumber = startNumber;
            BulletChar = bulletChar;
            Indent = indent;
            ContentIndent = contentIndent;
        }
    }

    private static bool TryGetListMarker(string line, out ListMarker marker)
    {
        marker = default;
        var trimmed = line.TrimStart(' ');
        var leading = line.Length - trimmed.Length;
        if (leading > 3) return false;
        if (trimmed.Length == 0) return false;

        // unordered
        if (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+')
        {
            if (trimmed.Length < 2) return false;
            if (trimmed[1] != ' ' && trimmed[1] != '\t') return false;
            int contentIndent = leading + 2;
            marker = new ListMarker(false, 0, trimmed[0], leading, contentIndent);
            return true;
        }

        // ordered: 1-9 digits followed by '.' and space
        int digits = 0;
        while (digits < trimmed.Length && digits < 9 && char.IsDigit(trimmed[digits])) digits++;
        if (digits == 0) return false;
        if (digits >= trimmed.Length) return false;
        if (trimmed[digits] != '.') return false;
        if (digits + 1 >= trimmed.Length) return false;
        if (trimmed[digits + 1] != ' ' && trimmed[digits + 1] != '\t') return false;

        int start = int.Parse(trimmed.Substring(0, digits));
        int content = leading + digits + 2;
        marker = new ListMarker(true, start, '0', leading, content);
        return true;
    }

    private static int ParseList(string[] lines, int start, int end, int baseIndent, ListMarker firstMarker, List<MarkdownBlock> output)
    {
        MarkdownBlock listBlock;
        if (firstMarker.Ordered)
        {
            var ol = new OrderedList { StartIndex = firstMarker.StartNumber };
            listBlock = ol;
        }
        else
        {
            listBlock = new BulletList();
        }

        int i = start;
        while (i < end)
        {
            var stripped = StripIndent(lines[i], baseIndent);
            if (!TryGetListMarker(stripped, out var m)) break;
            if (m.Indent != firstMarker.Indent) break;
            if (m.Ordered != firstMarker.Ordered) break;
            if (!m.Ordered && m.BulletChar != firstMarker.BulletChar) break;

            // gather this item's lines: the marker line stripped of marker, plus continuation
            var itemLines = new List<string>();
            var trimmed = stripped.TrimStart(' ');
            // after marker: substring starting at ContentIndent - leading
            var afterMarker = trimmed.Substring(m.ContentIndent - m.Indent);
            itemLines.Add(afterMarker);
            i++;

            while (i < end)
            {
                var next = StripIndent(lines[i], baseIndent);
                if (IsBlank(next))
                {
                    // blank line could end item or continue it — peek next non-blank
                    // collect blank, check if next indented enough
                    int peek = i + 1;
                    while (peek < end && IsBlank(StripIndent(lines[peek], baseIndent))) peek++;
                    if (peek >= end) { i = peek; break; }
                    var peekStripped = StripIndent(lines[peek], baseIndent);
                    int peekLead = CountLeadingSpaces(peekStripped);
                    if (peekLead >= m.ContentIndent)
                    {
                        // continuation after blank — preserve blank lines
                        while (i < peek) { itemLines.Add(string.Empty); i++; }
                        continue;
                    }
                    else if (TryGetListMarker(peekStripped, out var peekMarker) && peekMarker.Indent == m.Indent)
                    {
                        // next sibling item
                        i = peek;
                        break;
                    }
                    else { i = peek; break; }
                }

                int lead = CountLeadingSpaces(next);
                if (lead >= m.ContentIndent)
                {
                    itemLines.Add(next.Substring(m.ContentIndent));
                    i++;
                    continue;
                }
                // sibling list item at same marker indent?
                if (TryGetListMarker(next, out var sibling) && sibling.Indent == m.Indent)
                    break;
                // otherwise end of item
                break;
            }

            var item = new ListItem();
            ParseBlocks(itemLines.ToArray(), 0, itemLines.Count, 0, item.Children);
            if (listBlock is BulletList bl) bl.Items.Add(item);
            else if (listBlock is OrderedList ol2) ol2.Items.Add(item);
        }

        output.Add(listBlock);
        return i;
    }

    private static int ParseParagraph(string[] lines, int start, int end, int baseIndent, List<MarkdownBlock> output)
    {
        var sb = new StringBuilder();
        int i = start;
        while (i < end)
        {
            var stripped = StripIndent(lines[i], baseIndent);
            if (IsBlank(stripped)) break;
            if (i != start)
            {
                if (TryParseThematicBreak(stripped)) break;
                if (TryParseAtxHeading(stripped, out _)) break;
                if (IsBlockquoteLine(stripped)) break;
                if (IsFenceStart(stripped)) break;
                // Note: list marker in the middle of a paragraph is tolerated per CommonMark, but
                // for our pragmatic subset we end the paragraph if a list marker appears.
                if (TryGetListMarker(stripped, out _)) break;
            }
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(stripped);
            i++;
        }

        var text = sb.ToString().TrimEnd();
        var para = new Paragraph();
        para.Inlines.AddRange(InlineParser.Parse(text));
        output.Add(para);
        return i;
    }

    private static bool IsFenceStart(string line)
    {
        var trimmed = line.TrimStart(' ');
        var leading = line.Length - trimmed.Length;
        if (leading > 3 || trimmed.Length < 3) return false;
        char c = trimmed[0];
        if (c != '`' && c != '~') return false;
        int run = 0;
        while (run < trimmed.Length && trimmed[run] == c) run++;
        return run >= 3;
    }
}
