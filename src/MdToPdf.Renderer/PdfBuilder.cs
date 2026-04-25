using PdfSharp.Drawing;
using PdfSharp.Pdf;
using MdToPdf.Renderer.Adapters;

namespace MdToPdf.Renderer;

public sealed class PdfBuilder
{
    private readonly PdfOptions _options = new();

    internal PdfBuilder() { }

    public PdfBuilder WithPageSize(XSize pageSize)
    {
        _options.PageSize = pageSize;
        return this;
    }

    public PdfBuilder WithOrientation(PageOrientation orientation)
    {
        _options.PageOrientation = orientation;
        return this;
    }

    public PdfBuilder WithMargin(double all)
    {
        _options.SetMargins(all);
        return this;
    }

    public PdfBuilder WithMargin(double vertical, double horizontal)
    {
        _options.SetMargins(vertical, horizontal);
        return this;
    }

    public PdfBuilder WithMargin(double top, double right, double bottom, double left)
    {
        _options.MarginTop = top;
        _options.MarginRight = right;
        _options.MarginBottom = bottom;
        _options.MarginLeft = left;
        return this;
    }

    public PdfBuilder WithFont(string familyName, byte[] fontData)
    {
        FontResolver.Instance.RegisterFont(familyName, fontData);
        return this;
    }

    public PdfBuilder WithPassword(string userPassword)
    {
        (_options.Security ??= new PdfSecurityOptions()).UserPassword = userPassword;
        return this;
    }

    public PdfBuilder WithOwnerPassword(string ownerPassword)
    {
        (_options.Security ??= new PdfSecurityOptions()).OwnerPassword = ownerPassword;
        return this;
    }

    public PdfBuilder WithPermissions(PdfPermissions permissions)
    {
        (_options.Security ??= new PdfSecurityOptions()).Permissions = permissions;
        return this;
    }

    public PdfDocument GeneratePdf(string markdown)
    {
        return PdfGenerator.GeneratePdf(markdown, _options);
    }

    public Task<byte[]> GeneratePdfAsync(string markdown)
    {
        return PdfGenerator.GeneratePdfAsync(markdown, _options);
    }
}
