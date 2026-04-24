using MdToPdf.Renderer;

var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
Directory.CreateDirectory(outputDir);

var markdown = """
    # MdToPdf.Renderer Demo

    This is a **console demo application** that converts Markdown to PDF using
    *MdToPdf.Renderer*.

    ## Features

    - Headings and paragraphs with **bold** and *italic* inline styling
    - Ordered and unordered lists, including nested lists
    - Fenced code blocks rendered with a monospace font
    - Blockquotes
    - Inline [hyperlinks](https://example.com)
    - Automatic multi-page pagination

    ## Sample code

    ```
    var bytes = await PdfGenerator.Create()
        .WithPageSize(PageSize.A4)
        .WithMargin(40)
        .GeneratePdfAsync(markdown);
    ```

    > Markdown keeps source readable while producing polished PDF output.
    """;

var pdfPath = Path.Combine(outputDir, "demo.pdf");
var bytes = await PdfGenerator.Create()
    .WithPageSize(PageSize.A4)
    .WithMargin(40)
    .GeneratePdfAsync(markdown);

await File.WriteAllBytesAsync(pdfPath, bytes);
Console.WriteLine($"Created: {pdfPath}");
