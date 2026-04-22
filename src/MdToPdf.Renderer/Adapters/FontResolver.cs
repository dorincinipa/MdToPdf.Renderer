using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace MdToPdf.Renderer.Adapters;

internal sealed class FontResolver : IFontResolver
{
    internal static FontResolver Instance { get; } = new();

    private readonly ConcurrentDictionary<string, byte[]> _customFonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte[]> _systemFaceCache = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _registered;

    private FontResolver() { }

    private void EnsureRegistered()
    {
        if (_registered)
            return;

        _registered = true;
        try
        {
            GlobalFontSettings.FontResolver = this;
        }
        catch (InvalidOperationException)
        {
            // PdfSharp may throw if a font resolver was already set or fonts were already used.
        }
    }

    internal void RegisterFont(string familyName, byte[] fontData)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        ArgumentNullException.ThrowIfNull(fontData);
        _customFonts[familyName] = fontData;
        EnsureRegistered();
    }

    internal void LoadFontsFromFolder(string folderPath)
    {
        ArgumentNullException.ThrowIfNull(folderPath);
        if (!Directory.Exists(folderPath))
            return;

        foreach (var file in Directory.EnumerateFiles(folderPath, "*.*"))
        {
            var ext = Path.GetExtension(file);
            if (!ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".otf", StringComparison.OrdinalIgnoreCase))
                continue;

            var familyName = Path.GetFileNameWithoutExtension(file);
            _customFonts[familyName] = File.ReadAllBytes(file);
        }

        if (_customFonts.Count > 0)
            EnsureRegistered();
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (_customFonts.ContainsKey(familyName))
            return new FontResolverInfo(familyName);

        var faceName = TryMapWindowsFace(familyName, bold, italic);
        if (faceName is not null)
            return new FontResolverInfo(faceName);

        return null;
    }

    public byte[] GetFont(string faceName)
    {
        if (_customFonts.TryGetValue(faceName, out var data))
            return data;
        if (_systemFaceCache.TryGetValue(faceName, out var sys))
            return sys;
        return [];
    }

    private string? TryMapWindowsFace(string familyName, bool bold, bool italic)
    {
        var fontsDir = TryGetWindowsFontsDir();
        if (fontsDir is null) return null;

        foreach (var candidate in WindowsFontCandidates(familyName, bold, italic))
        {
            var path = Path.Combine(fontsDir, candidate);
            if (!File.Exists(path)) continue;
            var faceName = $"{familyName}|{(bold ? "b" : "")}{(italic ? "i" : "")}";
            if (!_systemFaceCache.ContainsKey(faceName))
            {
                try
                {
                    _systemFaceCache[faceName] = File.ReadAllBytes(path);
                }
                catch
                {
                    continue;
                }
            }
            return faceName;
        }

        if (bold || italic)
            return TryMapWindowsFace(familyName, false, false);
        return null;
    }

    private static string? TryGetWindowsFontsDir()
    {
        try
        {
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(win)) return null;
            var fontsDir = Path.Combine(win, "Fonts");
            return Directory.Exists(fontsDir) ? fontsDir : null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> WindowsFontCandidates(string familyName, bool bold, bool italic)
    {
        var key = familyName.Trim().ToLowerInvariant();
        return key switch
        {
            "arial" => VariantsOrdered("arial", "arialbd", "ariali", "arialbi", bold, italic),
            "courier new" or "courier" => VariantsOrdered("cour", "courbd", "couri", "courbi", bold, italic),
            "times new roman" or "times" => VariantsOrdered("times", "timesbd", "timesi", "timesbi", bold, italic),
            "verdana" => VariantsOrdered("verdana", "verdanab", "verdanai", "verdanaz", bold, italic),
            "calibri" => VariantsOrdered("calibri", "calibrib", "calibrii", "calibriz", bold, italic),
            "segoe ui" => VariantsOrdered("segoeui", "segoeuib", "segoeuii", "segoeuiz", bold, italic),
            _ => VariantsOrdered(key, key + "b", key + "i", key + "z", bold, italic)
        };
    }

    private static IEnumerable<string> VariantsOrdered(string reg, string b, string it, string bi, bool bold, bool italic)
    {
        string basename = (bold, italic) switch
        {
            (true, true) => bi,
            (true, false) => b,
            (false, true) => it,
            _ => reg
        };
        yield return basename + ".ttf";
        yield return basename + ".otf";
        yield return reg + ".ttf";
        yield return reg + ".otf";
    }
}
