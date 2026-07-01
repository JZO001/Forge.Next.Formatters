using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Soap;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// A minimal SOAP-serializable data-transfer object used by <see cref="XmlSoapFormatterTests"/>.
/// The classic <see cref="SoapFormatter"/> works with types marked <see cref="SerializableAttribute"/>
/// and serializes their fields, so public fields (rather than auto-properties, whose backing
/// fields carry compiler-mangled names) are used to keep the emitted SOAP XML clean.
/// </summary>
[Serializable]
public class SoapSampleData
{
    /// <summary>A sample string field.</summary>
    public string Name = string.Empty;

    /// <summary>A sample integer field.</summary>
    public int Value;
}

/// <summary>
/// Unit tests for <see cref="XmlSoapFormatter{T}"/>, which serializes objects to SOAP XML via a
/// <see cref="SoapFormatter"/>. As with the XML formatter, the two-stream read overload is
/// intentionally unimplemented and must return a "forbidden" error.
/// </summary>
public class XmlSoapFormatterTests
{
    /// <summary>
    /// The default constructor must produce a usable instance (it internally creates its own
    /// <see cref="SoapFormatter"/>).
    /// </summary>
    [Fact]
    public void Constructor_Default_CreatesInstance_Test()
    {
        // Act
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();

        // Assert
        Assert.NotNull(formatter);
    }

    /// <summary>
    /// The constructor overload that accepts a <see cref="SoapFormatter"/> must reject null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullFormatter_ThrowsArgumentNullException_Test()
    {
        // Act & Assert: the guard throws with the "formatter" parameter name.
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new XmlSoapFormatter<SoapSampleData>((SoapFormatter)null!));
        Assert.Equal("formatter", ex.ParamName);
    }

    /// <summary>
    /// A null data object on write yields a validation error naming "data".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(null!, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("data", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream on write yields a validation error naming "outputStream".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new SoapSampleData(), null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on read yields a validation error naming "inputStream".
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStream_ReturnsValidationError_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();

        // Act
        ErrorOr<SoapSampleData?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// The two-stream read overload is not implemented and must always return a "forbidden"
    /// error with the documented description.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WithOutputStream_ReturnsForbidden_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        using MemoryStream input = new MemoryStream();
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal("Method not implemented.", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip: an object serialized with <c>WriteAsync</c> must deserialize back into
    /// an equal object with <c>ReadAsync</c> using the SOAP formatter.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsObject_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        SoapSampleData original = new SoapSampleData { Name = "Forge", Value = 7 };
        using MemoryStream buffer = new MemoryStream();

        // Act (write): serialize to SOAP XML.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, buffer);
        Assert.False(writeResult.IsError);

        // Act (read): rewind and deserialize.
        buffer.Position = 0;
        ErrorOr<SoapSampleData?> readResult = await formatter.ReadAsync(buffer);

        // Assert: the deserialized object has the same field values.
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);
        Assert.Equal(original.Name, readResult.Value!.Name);
        Assert.Equal(original.Value, readResult.Value!.Value);
    }

    /// <summary>
    /// Feeding non-SOAP content to <c>ReadAsync</c> causes the deserializer to throw; the
    /// <c>ProtectAsync</c> wrapper must convert that into an error result.
    /// </summary>
    [Fact]
    public async Task ReadAsync_InvalidContent_ReturnsError_Test()
    {
        // Arrange: bytes that are not valid SOAP XML.
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("definitely not soap"));

        // Act
        ErrorOr<SoapSampleData?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.True(result.IsError);
    }
}
