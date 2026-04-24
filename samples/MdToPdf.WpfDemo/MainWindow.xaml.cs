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

        return config;
    }

    private void LoadSampleMarkdown()
    {
        MarkdownEditor.Text = """
            # MdToPdf.Renderer Demo

            This is a **WPF demo application** that converts Markdown to PDF using
            *MdToPdf.Renderer*. Edit the markdown on the left and click
            **Generate PDF** to try it out.

            ## Features

            - Headings and paragraphs with **bold** and *italic* styling
            - Ordered and unordered lists, including nested lists
            - Fenced code blocks rendered with a monospace font
            - Blockquotes
            - Inline [hyperlinks](https://example.com)
            - Automatic multi-page pagination

            ## Sample code

            ```
            var doc = PdfGenerator.Create()
                .WithPageSize(PageSize.A4)
                .WithMargin(40)
                .GeneratePdf(markdown);
            ```

            > Markdown keeps source readable while producing polished PDFs.
            """;
    }
}
