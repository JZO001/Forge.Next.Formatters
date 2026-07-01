using Forge.Next.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions"/>. These verify the two registration
/// entry points, <see cref="ServiceCollectionExtensions.AddForgeFormattersAsScoped"/> and
/// <see cref="ServiceCollectionExtensions.AddForgeFormattersAsSingleton"/>: that each registers
/// every formatter with the correct service type, implementation type and lifetime, that the
/// registrations can be resolved from a built container, and that both methods return the same
/// collection (fluent API).
/// <para>
/// Both methods register the same nine services; they differ only in the lifetime applied to the
/// eight "primary" formatters (scoped vs. singleton). In both methods the SOAP formatter is always
/// registered as transient.
/// </para>
/// </summary>
public class ServiceCollectionExtensionsTests
{
    // ------------------------------------------------------------------
    // AddForgeFormattersAsScoped
    // ------------------------------------------------------------------

    /// <summary>
    /// The scoped extension must return the very same <see cref="IServiceCollection"/> instance it
    /// was given, so that registration calls can be chained fluently.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsScoped_ReturnsSameServiceCollection_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        IServiceCollection returned = services.AddForgeFormattersAsScoped();

        // Assert: identical reference.
        Assert.Same(services, returned);
    }

    /// <summary>
    /// Every formatter must be registered by the scoped method with the exact lifetime the source
    /// declares: the eight primary formatters are scoped, and the SOAP formatter is transient.
    /// The implementation type must also match.
    /// </summary>
    /// <param name="serviceType">The service (interface) type expected to be registered.</param>
    /// <param name="implementationType">The concrete implementation type expected.</param>
    /// <param name="expectedLifetime">The expected registration lifetime.</param>
    [Theory]
    [InlineData(typeof(IGZipByteArrayFormatter), typeof(GZipByteArrayFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IGZipStreamFormatter), typeof(GZipStreamFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IXmlDataFormatter<>), typeof(XmlDataFormatter<>), ServiceLifetime.Scoped)]
    [InlineData(typeof(IBrotliStreamFormatter), typeof(BrotliStreamFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IBrotliByteArrayFormatter), typeof(BrotliByteArrayFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IAesByteArrayFormatter), typeof(AesByteArrayFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(IAesStreamFormatter), typeof(AesStreamFormatter), ServiceLifetime.Scoped)]
    [InlineData(typeof(ISystemJsonFormatter<>), typeof(SystemJsonFormatter<>), ServiceLifetime.Scoped)]
    [InlineData(typeof(IXmlSoapFormatter<>), typeof(XmlSoapFormatter<>), ServiceLifetime.Transient)]
    public void AddForgeFormattersAsScoped_RegistersServiceWithExpectedLifetime_Test(
        System.Type serviceType,
        System.Type implementationType,
        ServiceLifetime expectedLifetime)
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddForgeFormattersAsScoped();

        // Assert: exactly one descriptor exists for the service type, with matching details.
        ServiceDescriptor descriptor = Assert.Single(
            services, d => d.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(expectedLifetime, descriptor.Lifetime);
    }

    /// <summary>
    /// The scoped method must register exactly the nine formatters it advertises and nothing more,
    /// guarding against accidental extra or duplicate registrations.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsScoped_RegistersExactlyNineServices_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddForgeFormattersAsScoped();

        // Assert
        Assert.Equal(9, services.Count);
    }

    /// <summary>
    /// The closed-generic and non-generic scoped registrations must all be resolvable from a fully
    /// built container. Resolution happens inside a scope because the services are scoped.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsScoped_RegistrationsCanBeResolved_Test()
    {
        // Arrange: build a real provider from the registrations.
        IServiceCollection services = new ServiceCollection();
        services.AddForgeFormattersAsScoped();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act: resolve everything within a scope.
        using IServiceScope scope = provider.CreateScope();
        System.IServiceProvider sp = scope.ServiceProvider;

        // Assert: every formatter resolves to its concrete implementation type.
        Assert.IsType<GZipByteArrayFormatter>(sp.GetRequiredService<IGZipByteArrayFormatter>());
        Assert.IsType<GZipStreamFormatter>(sp.GetRequiredService<IGZipStreamFormatter>());
        Assert.IsType<BrotliStreamFormatter>(sp.GetRequiredService<IBrotliStreamFormatter>());
        Assert.IsType<BrotliByteArrayFormatter>(sp.GetRequiredService<IBrotliByteArrayFormatter>());
        Assert.IsType<AesByteArrayFormatter>(sp.GetRequiredService<IAesByteArrayFormatter>());
        Assert.IsType<AesStreamFormatter>(sp.GetRequiredService<IAesStreamFormatter>());

        // Open generics resolve to the matching closed generic implementation.
        Assert.IsType<XmlDataFormatter<string>>(sp.GetRequiredService<IXmlDataFormatter<string>>());
        Assert.IsType<SystemJsonFormatter<string>>(sp.GetRequiredService<ISystemJsonFormatter<string>>());
        Assert.IsType<XmlSoapFormatter<string>>(sp.GetRequiredService<IXmlSoapFormatter<string>>());
    }

    /// <summary>
    /// Scoped semantics at runtime: within a single scope the same instance is returned, but two
    /// different scopes produce different instances. The always-transient SOAP formatter yields a
    /// fresh instance on every resolution.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsScoped_LifetimesBehaveAtRuntime_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddForgeFormattersAsScoped();
        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scopeA = provider.CreateScope();
        using IServiceScope scopeB = provider.CreateScope();

        // Act & Assert (scoped): same instance within a scope...
        IGZipByteArrayFormatter withinScopeFirst = scopeA.ServiceProvider.GetRequiredService<IGZipByteArrayFormatter>();
        IGZipByteArrayFormatter withinScopeSecond = scopeA.ServiceProvider.GetRequiredService<IGZipByteArrayFormatter>();
        Assert.Same(withinScopeFirst, withinScopeSecond);

        // ...but a different instance in another scope.
        IGZipByteArrayFormatter otherScope = scopeB.ServiceProvider.GetRequiredService<IGZipByteArrayFormatter>();
        Assert.NotSame(withinScopeFirst, otherScope);

        // Act & Assert (transient): a fresh SOAP formatter each time.
        IXmlSoapFormatter<string> firstTransient = scopeA.ServiceProvider.GetRequiredService<IXmlSoapFormatter<string>>();
        IXmlSoapFormatter<string> secondTransient = scopeA.ServiceProvider.GetRequiredService<IXmlSoapFormatter<string>>();
        Assert.NotSame(firstTransient, secondTransient);
    }

    // ------------------------------------------------------------------
    // AddForgeFormattersAsSingleton
    // ------------------------------------------------------------------

    /// <summary>
    /// The singleton extension must also return the same collection instance for fluent chaining.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsSingleton_ReturnsSameServiceCollection_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        IServiceCollection returned = services.AddForgeFormattersAsSingleton();

        // Assert
        Assert.Same(services, returned);
    }

    /// <summary>
    /// The singleton method registers the eight primary formatters as singletons and, like the
    /// scoped method, keeps the SOAP formatter transient.
    /// </summary>
    /// <param name="serviceType">The service (interface) type expected to be registered.</param>
    /// <param name="implementationType">The concrete implementation type expected.</param>
    /// <param name="expectedLifetime">The expected registration lifetime.</param>
    [Theory]
    [InlineData(typeof(IGZipByteArrayFormatter), typeof(GZipByteArrayFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IGZipStreamFormatter), typeof(GZipStreamFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IXmlDataFormatter<>), typeof(XmlDataFormatter<>), ServiceLifetime.Singleton)]
    [InlineData(typeof(IBrotliStreamFormatter), typeof(BrotliStreamFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IBrotliByteArrayFormatter), typeof(BrotliByteArrayFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IAesByteArrayFormatter), typeof(AesByteArrayFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(IAesStreamFormatter), typeof(AesStreamFormatter), ServiceLifetime.Singleton)]
    [InlineData(typeof(ISystemJsonFormatter<>), typeof(SystemJsonFormatter<>), ServiceLifetime.Singleton)]
    [InlineData(typeof(IXmlSoapFormatter<>), typeof(XmlSoapFormatter<>), ServiceLifetime.Transient)]
    public void AddForgeFormattersAsSingleton_RegistersServiceWithExpectedLifetime_Test(
        System.Type serviceType,
        System.Type implementationType,
        ServiceLifetime expectedLifetime)
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddForgeFormattersAsSingleton();

        // Assert
        ServiceDescriptor descriptor = Assert.Single(
            services, d => d.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(expectedLifetime, descriptor.Lifetime);
    }

    /// <summary>
    /// The singleton method must register exactly nine services.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsSingleton_RegistersExactlyNineServices_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        services.AddForgeFormattersAsSingleton();

        // Assert
        Assert.Equal(9, services.Count);
    }

    /// <summary>
    /// The singleton registrations must all be resolvable from a built container.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsSingleton_RegistrationsCanBeResolved_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddForgeFormattersAsSingleton();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act & Assert: everything resolves to its concrete implementation type.
        Assert.IsType<GZipByteArrayFormatter>(provider.GetRequiredService<IGZipByteArrayFormatter>());
        Assert.IsType<GZipStreamFormatter>(provider.GetRequiredService<IGZipStreamFormatter>());
        Assert.IsType<BrotliStreamFormatter>(provider.GetRequiredService<IBrotliStreamFormatter>());
        Assert.IsType<BrotliByteArrayFormatter>(provider.GetRequiredService<IBrotliByteArrayFormatter>());
        Assert.IsType<AesByteArrayFormatter>(provider.GetRequiredService<IAesByteArrayFormatter>());
        Assert.IsType<AesStreamFormatter>(provider.GetRequiredService<IAesStreamFormatter>());
        Assert.IsType<XmlDataFormatter<string>>(provider.GetRequiredService<IXmlDataFormatter<string>>());
        Assert.IsType<SystemJsonFormatter<string>>(provider.GetRequiredService<ISystemJsonFormatter<string>>());
        Assert.IsType<XmlSoapFormatter<string>>(provider.GetRequiredService<IXmlSoapFormatter<string>>());
    }

    /// <summary>
    /// Singleton semantics at runtime: the same instance is returned across different scopes, while
    /// the transient SOAP formatter still yields a fresh instance each time.
    /// </summary>
    [Fact]
    public void AddForgeFormattersAsSingleton_LifetimesBehaveAtRuntime_Test()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddForgeFormattersAsSingleton();
        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scopeA = provider.CreateScope();
        using IServiceScope scopeB = provider.CreateScope();

        // Act & Assert (singleton): identical instance even across different scopes.
        IGZipByteArrayFormatter fromScopeA = scopeA.ServiceProvider.GetRequiredService<IGZipByteArrayFormatter>();
        IGZipByteArrayFormatter fromScopeB = scopeB.ServiceProvider.GetRequiredService<IGZipByteArrayFormatter>();
        Assert.Same(fromScopeA, fromScopeB);

        // Act & Assert (transient): different SOAP instances.
        IXmlSoapFormatter<string> firstTransient = provider.GetRequiredService<IXmlSoapFormatter<string>>();
        IXmlSoapFormatter<string> secondTransient = provider.GetRequiredService<IXmlSoapFormatter<string>>();
        Assert.NotSame(firstTransient, secondTransient);
    }
}
