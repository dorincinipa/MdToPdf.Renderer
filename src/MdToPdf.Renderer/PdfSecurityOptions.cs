namespace MdToPdf.Renderer;

public sealed class PdfSecurityOptions
{
    public string? UserPassword { get; set; }
    public string? OwnerPassword { get; set; }
    public PdfPermissions Permissions { get; set; } = PdfPermissions.All;
}
