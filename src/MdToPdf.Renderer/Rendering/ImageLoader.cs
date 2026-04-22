using PdfSharp.Drawing;

namespace MdToPdf.Renderer.Rendering;

internal sealed class ImageLoader : IDisposable
{
    private readonly Dictionary<string, XImage?> _cache = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly ImageLoadFailureMode _mode;

    internal ImageLoader(ImageLoadFailureMode mode)
    {
        _mode = mode;
    }

    internal XImage? Load(string src)
    {
        if (string.IsNullOrEmpty(src)) return HandleFailure(src, null);
        if (_cache.TryGetValue(src, out var cached)) return cached;

        try
        {
            XImage? image;
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                image = LoadFromDataUri(src);
            else if (File.Exists(src))
                image = XImage.FromFile(src);
            else
                return HandleFailure(src, new FileNotFoundException(src));
            _cache[src] = image;
            if (image is not null) _disposables.Add(image);
            return image;
        }
        catch (Exception ex)
        {
            return HandleFailure(src, ex);
        }
    }

    private XImage? LoadFromDataUri(string src)
    {
        int comma = src.IndexOf(',');
        if (comma < 0) throw new FormatException("Malformed data URI (no comma).");
        var header = src.Substring(0, comma);
        var payload = src.Substring(comma + 1);
        if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Only base64 data URIs are supported.");
        var bytes = Convert.FromBase64String(payload);
        var stream = new MemoryStream(bytes);
        _disposables.Add(stream);
        return XImage.FromStream(stream);
    }

    private XImage? HandleFailure(string src, Exception? ex)
    {
        _cache[src] = null;
        if (_mode == ImageLoadFailureMode.Throw)
        {
            if (ex is FileNotFoundException || ex is FormatException) throw ex;
            if (ex is not null) throw ex;
            throw new FileNotFoundException($"Image not found: {src}");
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            try { d.Dispose(); } catch { /* swallow */ }
        }
        _disposables.Clear();
        _cache.Clear();
    }
}
