using Forge.Next.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions"/>. These verify that
/// <see cref="ServiceCollectionExtensions.AddForgeFormatters"/> registers every formatter with
/// the correct service type, implementation type and lifetime, that the registrations can be
/// resolved from a built container, and that the method returns the same collection (fluent API).
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// The extension must return the very same <see cref="IServiceCollection"/> instance it was
    /// given, so that registration calls can be chained fluently.
    /// </summary>
    [Fact]
    public void AddForgeFormatters_ReturnsSameServiceCollection_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        IServiceCollection returned = services.AddForgeFormatters();

        // Assert: identical reference.
        Assert.Same(services, returned);
    }

    /// <summary>
    /// Each formatter must be registered with the exact lifetime documented in the source:
    /// the compression/XML-data formatters are singletons, the AES formatters are scoped and
    /// the SOAP formatter is transient. The implementation type must also match.
    /// </summary>
    /// <param name="serviceType">The service (interface) type expected to be registered.</param>
    /// <param name="implementationType">The concrete implementation type expected.</param>
    /// <param name="expectedLifetime">The expected registration lifetime.</param>
    [Theory]
    [InlineData(typeof(IGZipFormatter), typeof(GZipFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IXmlDataFormatter<>), typeof(XmlDataFormatter<>), ServiceLifetime.Singleton)]
    [InlineData(typeof(IBrotliStreamFormatter), typeof(BrotliStreamFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IBrotliByteArrayFormatter), typeof(BrotliByteArrayFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IAesByteArrayFormatter), typeof(AesByteArrayFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IAesStreamFormatter), typeof(AesStreamFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IXmlSoapFormatter<>), typeof(XmlSoapFormatter<>), ServiceLifetime.Transient)]
    public void AddForgeFormatters_RegistersServiceWithExpectedLifetime_Test(
        System.Type serviceType,
        System.Type implementationType,
        ServiceLifetime expectedLifetime)
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddForgeFormatters();

        // Assert: exactly one descriptor exists for the service type, with matching details.
        ServiceDescriptor descriptor = Assert.Single(
            services, d => d.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(expectedLifetime, descriptor.Lifetime);
    }

    /// <summary>
    /// The method must register exactly the seven formatters it advertises and nothing more,
    /// guarding against accidental extra or duplicate registrations.
    /// </summary>
    [Fact]
    public void AddForgeFormatters_RegistersExactlySevenServices_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddForgeFormatters();

        // Assert
        Assert.Equal(7, services.Count);
    }

    /// <summary>
    /// The closed-generic and non-generic registrations must all be resolvable from a fully
    /// built container. Resolution happens inside a scope so that the scoped AES formatters can
    /// be created safely.
    /// </summary>
    [Fact]
    public void AddForgeFormatters_RegistrationsCanBeResolved_Test()
    {
        // Arrange: build a real provider from the registrations.
        IServiceCollection services = new ServiceCollection();
        services.AddForgeFormatters();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act: resolve everything within a scope.
        using IServiceScope scope = provider.CreateScope();
        System.IServiceProvider sp = scope.ServiceProvider;

        // Assert: every formatter resolves to its concrete implementation type.
        Assert.IsType<GZipFormatter>(sp.GetRequiredService<IGZipFormatter>());
        Assert.IsType<BrotliStreamFormatter>(sp.GetRequiredService<IBrotliStreamFormatter>());
        Assert.IsType<BrotliByteArrayFormatter>(sp.GetRequiredService<IBrotliByteArrayFormatter>());
        Assert.IsType<AesByteArrayFormatter>(sp.GetRequiredService<IAesByteArrayFormatter>());
        Assert.IsType<AesStreamFormatter>(sp.GetRequiredService<IAesStreamFormatter>());

        // Open generics resolve to the matching closed generic implementation.
        Assert.IsType<XmlDataFormatter<string>>(sp.GetRequiredService<IXmlDataFormatter<string>>());
        Assert.IsType<XmlSoapFormatter<string>>(sp.GetRequiredService<IXmlSoapFormatter<string>>());
    }

    /// <summary>
    /// The singleton registrations must return the same instance across resolutions, while the
    /// transient SOAP formatter must return a fresh instance each time. This confirms the
    /// lifetimes actually take effect at runtime (not just in the descriptor metadata).
    /// </summary>
    [Fact]
    public void AddForgeFormatters_LifetimesBehaveAtRuntime_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddForgeFormatters();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act & Assert (singleton): two resolutions yield the identical instance.
        IGZipFormatter firstSingleton = provider.GetRequiredService<IGZipFormatter>();
        IGZipFormatter secondSingleton = provider.GetRequiredService<IGZipFormatter>();
        Assert.Same(firstSingleton, secondSingleton);

        // Act & Assert (transient): two resolutions yield different instances.
        IXmlSoapFormatter<string> firstTransient = provider.GetRequiredService<IXmlSoapFormatter<string>>();
        IXmlSoapFormatter<string> secondTransient = provider.GetRequiredService<IXmlSoapFormatter<string>>();
        Assert.NotSame(firstTransient, secondTransient);
    }
}
