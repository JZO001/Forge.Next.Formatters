using System;
using System.Runtime.Serialization;
using Forge.Next.Formatters;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="SoapAllowedTypesBinder"/>, the binder that lets a SOAP payload name only the types it was
/// given (since 3.4.0). The end-to-end behaviour with a real <c>SoapFormatter</c> is tested in
/// <see cref="XmlSoapFormatterTests"/>.
/// </summary>
public class SoapAllowedTypesBinderTests
{
    /// <summary>
    /// A <c>null</c> array of allowed types is rejected.
    /// </summary>
    [Fact]
    public void Constructor_NullArray_ThrowsArgumentNullException_Test()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new SoapAllowedTypesBinder(null!));
        Assert.Equal("allowedTypes", exception.ParamName);
    }

    /// <summary>
    /// A <c>null</c> element in the allowed types is rejected.
    /// </summary>
    [Fact]
    public void Constructor_NullElement_ThrowsArgumentException_Test()
    {
        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new SoapAllowedTypesBinder(typeof(string), null!));
        Assert.Equal("allowedTypes", exception.ParamName);
    }

    /// <summary>
    /// An allowed type is matched by its full name, whatever assembly the payload names, and the allowed type itself
    /// is returned.
    /// </summary>
    [Fact]
    public void BindToType_AllowedType_ReturnsIt_Test()
    {
        // Arrange
        SoapAllowedTypesBinder binder = new SoapAllowedTypesBinder(typeof(SoapSampleData));

        // Act
        Type bound = binder.BindToType("Some.Other.Assembly, Version=9.9.9.9, Culture=neutral, PublicKeyToken=null", typeof(SoapSampleData).FullName!);

        // Assert
        Assert.Same(typeof(SoapSampleData), bound);
    }

    /// <summary>
    /// A type that is not allowed is refused with a <see cref="SerializationException"/>, before anything is created.
    /// </summary>
    [Fact]
    public void BindToType_OtherType_ThrowsSerializationException_Test()
    {
        // Arrange
        SoapAllowedTypesBinder binder = new SoapAllowedTypesBinder(typeof(SoapSampleData));

        // Act & Assert
        Assert.Throws<SerializationException>(() => binder.BindToType(typeof(Uri).Assembly.FullName!, typeof(Uri).FullName!));
    }
}
