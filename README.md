# MdToPdf.Renderer

A .NET 8 library that converts Markdown to PDF using [PdfSharp 6.x](https://github.com/empira/PDFsharp).

Supports headings, paragraphs with bold/italic/inline code, fenced code blocks, nested lists, blockquotes, thematic breaks, inline links, and images (local files and data URIs). Configurable page sizes (A4, Letter, Legal, A3) with portrait/landscape orientation and multi-page pagination.

## Usage

### Fluent API

```csharp
using MdToPdf.Renderer;

var doc = PdfGenerator.Create()
    .WithPageSize(PageSize.A4)
    .WithMargin(40)
    .GeneratePdf("# Hello\n\nWorld");

doc.Save("output.pdf");
```

### Async (returns byte[])

```csharp
var bytes = await PdfGenerator.Create()
    .WithPageSize(PageSize.A4)
    .WithMargin(40)
    .GeneratePdfAsync(markdown);

await File.WriteAllBytesAsync("output.pdf", bytes);
```

### Direct API

```csharp
var options = new PdfOptions
{
    PageSize = PageSize.A4,
    PageOrientation = PageOrientation.Landscape,
    MarginTop = 20,
    MarginBottom = 20,
    MarginLeft = 40,
    MarginRight = 40
};

var doc = PdfGenerator.GeneratePdf(markdown, options);
doc.Save("output.pdf");
```


## License

Licensed under MIT. PDF rendering by [PdfSharp](https://github.com/empira/PDFsharp) (MIT, see [NOTICE.txt](NOTICE.txt)).
