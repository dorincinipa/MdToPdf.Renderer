using MdToPdf.Renderer;
using MdToPdf.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMdToPdf(options =>
{
    options.PageSize = PageSize.A4;
    options.DefaultMargin = 20;
});

var app = builder.Build();

app.MapPost("/api/pdf", async (IPdfGenerator pdf, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var markdown = await reader.ReadToEndAsync();
    var bytes = await pdf.GeneratePdfAsync(markdown);
    return Results.File(bytes, "application/pdf", "output.pdf");
});

app.MapPost("/api/pdf/upload", async (IPdfGenerator pdf, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    using var reader = new StreamReader(file.OpenReadStream());
    var markdown = await reader.ReadToEndAsync();
    var bytes = await pdf.GeneratePdfAsync(markdown);
    return Results.File(bytes, "application/pdf", "output.pdf");
}).DisableAntiforgery();

await app.RunAsync();
