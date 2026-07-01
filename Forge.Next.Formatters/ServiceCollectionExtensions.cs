using Microsoft.Extensions.DependencyInjection;

namespace Forge.Next.Formatters;

/// <summary>
/// Extension methods for IServiceCollection to add Forge formatters
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// Adds Forge formatters to the IServiceCollection for dependency injection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the formatters to.</param>
    /// <returns>The IServiceCollection with the added formatters.</returns>
    public static IServiceCollection AddForgeFormatters(this IServiceCollection services)
    {
        return services
            .AddSingleton<IGZipFormatter, GZipFormatter>()
            .AddSingleton(typeof(IXmlDataFormatter<>), typeof(XmlDataFormatter<>))
            .AddSingleton<IBrotliStreamFormatter, BrotliStreamFormatter>()
            .AddSingleton<IBrotliByteArrayFormatter, BrotliByteArrayFormatter>()
            .AddScoped<IAesByteArrayFormatter, AesByteArrayFormatter>()
            .AddScoped<IAesStreamFormatter, AesStreamFormatter>()
            .AddTransient(typeof(IXmlSoapFormatter<>), typeof(XmlSoapFormatter<>));
    }

}
