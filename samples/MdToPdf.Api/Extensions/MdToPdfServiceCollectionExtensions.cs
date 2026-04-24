using MdToPdf.Renderer;
using Microsoft.Extensions.DependencyInjection;

namespace MdToPdf.Api.Extensions;

public static class MdToPdfServiceCollectionExtensions
{
    public static IServiceCollection AddMdToPdf(
        this IServiceCollection services,
        Action<PdfOptions>? configure = null)
    {
        var options = new PdfOptions();
        configure?.Invoke(options);

        if (options.DefaultMargin > 0)
            options.SetMargins(options.DefaultMargin);

        if (!string.IsNullOrEmpty(options.FontFolder))
            PdfGenerator.LoadFontsFromFolder(options.FontFolder);

        services.AddSingleton(options);
        services.AddSingleton<IPdfGenerator>(new MdToPdfGenerator(options));

        return services;
    }
}
