using System.Diagnostics;
using System.Windows;
using MdToPdf.Renderer;
using Microsoft.Win32;

namespace MdToPdf.WpfDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadSampleMarkdown();
    }

    private void LoadSampleBtn_Click(object sender, RoutedEventArgs e)
    {
        LoadSampleMarkdown();
    }

    private void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        var markdown = MarkdownEditor.Text;
        if (string.IsNullOrWhiteSpace(markdown))
        {
            StatusText.Text = "Please enter some markdown first.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = "output.pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var config = BuildConfig();
            config.OnProgress = pct => StatusText.Text = $"Rendering: {pct}%";
            config.OnRenderError = ex => StatusText.Text = $"Page error: {ex.Message}";
            config.ContinueOnError = true;

            var document = PdfGenerator.GeneratePdf(markdown, config);
            document.Save(dialog.FileName);

            StatusText.Text = $"Saved to {dialog.FileName}";
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private PdfOptions BuildConfig()
    {
        var config = new PdfOptions
        {
            PageSize = PageSizeCombo.SelectedIndex switch
            {
                0 => PageSize.A4,
                1 => PageSize.Letter,
                2 => PageSize.Legal,
                3 => PageSize.A3,
                _ => PageSize.A4
            },
            PageOrientation = OrientationCombo.SelectedIndex == 0
                ? PageOrientation.Portrait
                : PageOrientation.Landscape
        };

        if (double.TryParse(MarginBox.Text, out var margin))
            config.SetMargins(margin);

        config.AutoBookmarks = AutoBookmarksCheck.IsChecked == true;
        config.CompressOutput = CompressCheck.IsChecked == true;

        // do not use hardocoded passwords
        config.Security = new PdfSecurityOptions {
            UserPassword = "open",
            OwnerPassword = "admin",
            Permissions = PdfPermissions.Print | PdfPermissions.CopyContent
        };

        return config;
    }

    private void LoadSampleMarkdown()
    {
        MarkdownEditor.Text = """
            # MdToPdf.Renderer

            A .NET 8 library that converts Markdown to PDF using [PdfSharp 6.x](https://github.com/empira/PDFsharp).
            No headless browser, no external process -- a pure managed pipeline from Markdown string to `PdfDocument`.

            ---

            ## Features

            - **Headings** -- ATX-style H1-H6 with proportional sizing
            - **Inline formatting** -- bold, italic, inline code, and combinations
            - **Fenced code blocks** -- monospace font with a shaded background
            - **Blockquotes** -- with a left accent bar
            - **Lists** -- unordered and ordered, including nested levels
            - **Hyperlinks** -- external URLs and `#anchor` internal links with PDF annotations
            - **Images** -- local file paths and `data:` URIs
            - **Thematic breaks** -- rendered as a thin horizontal rule
            - **Auto-bookmarks** -- PDF outline built automatically from heading hierarchy
            - **Password protection** -- user/owner passwords with granular permissions
            - **Compression** -- DEFLATE content-stream compression
            - **Progress reporting** -- callback with 0-100 percent as pages are rendered
            - **Per-page error handling** -- continue or abort on render failures
            - **Custom fonts** -- register TTF bytes or load a folder at startup

            ---

            ## Usage

            ### Minimal

            ```csharp
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

            ---

            ## Auto-bookmarks

            ```csharp
            var doc = PdfGenerator.Create()
                .WithBookmarks()
                .GeneratePdf(markdown);
            ```

            Headings become a nested PDF outline. H1 at the root, H2 nested under the preceding H1, and so on.
            Internal `#anchor` links in the Markdown resolve to heading positions via slug matching.

            ---

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

            ---

            ## Progress and error handling

            ```csharp
            var options = new PdfOptions
            {
                OnProgress      = pct => Console.WriteLine($"Rendering: {pct}%"),
                OnRenderError   = ex  => logger.LogError(ex, "Page render failed"),
                ContinueOnError = true
            };
            ```

            `OnProgress` fires at 0% before the first page and at `(page / total) x 100` after each page.
            When `ContinueOnError` is `true`, a failed page is left blank and rendering continues.

            ---

            ## Lists

            ### Unordered

            - First item
            - Second item with **bold** text
              - Nested item
              - Another nested item
                - Deeply nested

            ### Ordered

            1. Parse Markdown to an AST
            2. Run the layout engine to paginate and measure text
            3. Paint each page with PdfSharp
            4. Apply bookmarks and link annotations

            ---

            ## Blockquote

            > Markdown keeps source readable while producing polished PDFs.
            > Combine it with `MdToPdf.Renderer` and you get a zero-dependency
            > document pipeline that runs anywhere .NET 8 does.

            ---

            *Edit this markdown and click **Generate PDF** to see the output.*
            """;
    }
}
