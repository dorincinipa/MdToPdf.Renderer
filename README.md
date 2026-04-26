# MdToPdf.Renderer

[![NuGet](https://img.shields.io/nuget/v/MdToPdf.Renderer.svg)](https://www.nuget.org/packages/MdToPdf.Renderer)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A .NET 8 library that converts Markdown to PDF using [PdfSharp 6.x](https://github.com/empira/PDFsharp).
No headless browser, no external process - a pure managed pipeline from Markdown string to `PdfDocument`.

## Features

- **Headings** - ATX-style H1-H6 with proportional sizing
- **Inline formatting** - bold, italic, inline code, and combinations
- **Fenced code blocks** - monospace font with a shaded background
- **Blockquotes** - with a left accent bar
- **Lists** - unordered and ordered, including nested levels
- **Hyperlinks** - external URLs and `#anchor` internal links with PDF annotations
- **Images** - local file paths and `data:` URIs
- **Thematic breaks** - rendered as a thin horizontal rule
- **Auto-bookmarks** - PDF outline built automatically from heading hierarchy
- **Password protection** - user/owner passwords with granular permissions
- **Compression** - DEFLATE content-stream compression
- **Progress reporting** - callback with 0-100 percent as pages are rendered
- **Per-page error handling** - continue or abort on render failures
- **Custom fonts** - register TTF bytes or load a folder at startup

## Installation

```
dotnet add package MdToPdf.Renderer
```

## Usage

### Minimal

```csharp
using MdToPdf.Renderer;

using var doc = PdfGenerator.GeneratePdf(markdown, new PdfOptions
{
    PageSize = PageSize.A4,
    MarginTop = 40, MarginBottom = 40,
    MarginLeft = 50, MarginRight = 50
});
doc.Save("output.pdf");
```

### Fluent builder

```csharp
var bytes = await PdfGenerator.Create()
    .WithPageSize(PageSize.A4)
    .WithMargin(40)
    .WithBookmarks()
    .WithCompression()
    .GeneratePdfAsync(markdown);

await File.WriteAllBytesAsync("output.pdf", bytes);
```

### Async (returns `byte[]`)

```csharp
byte[] bytes = await PdfGenerator.GeneratePdfAsync(markdown, options);
```

## PdfOptions reference

- `PageSize` (`XSize`, default A4) - use `PageSize.A4`, `Letter`, `Legal`, `A3`
- `PageOrientation` (default Portrait) - `Portrait` or `Landscape`
- `MarginTop / MarginBottom / MarginLeft / MarginRight` (`double`, default 0) - page margins in points
- `BaseFontSize` (`double`, default 11) - body text size in points
- `BodyFontFamily` (`string`, default `"Arial"`) - font family for body text
- `MonospaceFontFamily` (`string`, default `"Courier New"`) - font family for code blocks
- `LineHeight` (`double`, default 1.4) - line height multiplier
- `AutoBookmarks` (`bool`, default `false`) - build PDF outline from headings
- `CompressOutput` (`bool`, default `false`) - DEFLATE compress content streams
- `Security` (`PdfSecurityOptions?`, default `null`) - password and permissions
- `OnProgress` (`Action<int>?`) - callback with percent 0–100
- `OnRenderError` (`Action<Exception>?`) - per-page error callback
- `ContinueOnError` (`bool`, default `false`) - skip failed pages instead of throwing
- `ImageLoadFailureMode` (default `RenderAltText`) - `RenderAltText` or `Throw`

## Auto-bookmarks

```csharp
var doc = PdfGenerator.Create()
    .WithBookmarks()
    .GeneratePdf(markdown);
```

Headings become a nested PDF outline. H1 at the root, H2 nested under the preceding H1, and so on.
Internal `#anchor` links in the Markdown resolve to heading positions via slug matching.

## Password protection

```csharp
var options = new PdfOptions
{
    Security = new PdfSecurityOptions
    {
        UserPassword  = "open",
        OwnerPassword = "admin",
        Permissions   = PdfPermissions.Print | PdfPermissions.CopyContent
    }
};
```

`PdfPermissions` flags: `Print`, `HighQualityPrint`, `ModifyContent`, `CopyContent`,
`Annotate`, `FillForms`, `AssembleDocument`, `All`, `ReadOnly`, `None`.

## Compression

```csharp
var doc = PdfGenerator.Create()
    .WithCompression()
    .GeneratePdf(markdown);
```

Enables DEFLATE compression on all content streams. Produces smaller files at the cost of
slightly longer generation time.

## Progress and error handling

```csharp
var options = new PdfOptions
{
    OnProgress     = pct => Console.WriteLine($"Rendering: {pct}%"),
    OnRenderError  = ex  => logger.LogError(ex, "Page render failed"),
    ContinueOnError = true
};
```

`OnProgress` fires at 0 % before the first page and at `(page / total) × 100` after each page.
When `ContinueOnError` is `true`, a failed page is left blank and rendering continues;
otherwise the exception propagates after calling `OnRenderError`.

## Custom fonts

```csharp
// Register a single font
byte[] fontBytes = File.ReadAllBytes("MyFont-Regular.ttf");
PdfGenerator.Create()
    .WithFont("MyFont", fontBytes)
    .GeneratePdf(markdown);

// Or load a whole folder at startup
PdfGenerator.LoadFontsFromFolder("/app/fonts");
```

On Windows, common system fonts (Arial, Calibri, Segoe UI, Verdana, etc.) are resolved
automatically. Custom fonts registered via `WithFont` take precedence over system mapping.

## License

MIT. PDF rendering by [PdfSharp](https://github.com/empira/PDFsharp) (MIT, see [NOTICE.txt](NOTICE.txt)).
