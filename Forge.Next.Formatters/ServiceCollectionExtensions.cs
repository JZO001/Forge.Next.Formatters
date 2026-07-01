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
    public static IServiceCollection AddForgeFormattersAsScoped(this IServiceCollection services)
    {
        return services
            .AddScoped<IGZipByteArrayFormatter, GZipByteArrayFormatter>()
            .AddScoped<IGZipStreamFormatter, GZipStreamFormatter>()
            .AddScoped(typeof(IXmlDataFormatter<>), typeof(XmlDataFormatter<>))
            .AddScoped<IBrotliStreamFormatter, BrotliStreamFormatter>()
            .AddScoped<IBrotliByteArrayFormatter, BrotliByteArrayFormatter>()
            .AddScoped<IAesByteArrayFormatter, AesByteArrayFormatter>()
            .AddScoped<IAesStreamFormatter, AesStreamFormatter>()
            .AddScoped(typeof(ISystemJsonFormatter<>), typeof(SystemJsonFormatter<>))
            .AddTransient(typeof(IXmlSoapFormatter<>), typeof(XmlSoapFormatter<>));
    }

    /// <summary>
    /// Adds Forge formatters to the IServiceCollection for dependency injection as singletons.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the formatters to.</param>
    /// <returns>The IServiceCollection with the added formatters.</returns>
    public static IServiceCollection AddForgeFormattersAsSingleton(this IServiceCollection services)
    {
        return services
            .AddSingleton<IGZipByteArrayFormatter, GZipByteArrayFormatter>()
            .AddSingleton<IGZipStreamFormatter, GZipStreamFormatter>()
            .AddSingleton(typeof(IXmlDataFormatter<>), typeof(XmlDataFormatter<>))
            .AddSingleton<IBrotliStreamFormatter, BrotliStreamFormatter>()
            .AddSingleton<IBrotliByteArrayFormatter, BrotliByteArrayFormatter>()
            .AddSingleton<IAesByteArrayFormatter, AesByteArrayFormatter>()
            .AddSingleton<IAesStreamFormatter, AesStreamFormatter>()
            .AddSingleton(typeof(ISystemJsonFormatter<>), typeof(SystemJsonFormatter<>))
            .AddTransient(typeof(IXmlSoapFormatter<>), typeof(XmlSoapFormatter<>));
    }

}
