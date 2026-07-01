using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// A simple, XML-serializable data-transfer object used by <see cref="XmlDataFormatterTests"/>.
/// <see cref="System.Xml.Serialization.XmlSerializer"/> requires the type to be public and to
/// expose a public parameterless constructor plus public read/write members.
/// </summary>
public class XmlSampleData
{
    /// <summary>A sample string property.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A sample integer property.</summary>
    public int Value { get; set; }
}

/// <summary>
/// Unit tests for <see cref="XmlDataFormatter{T}"/>, which serializes objects to XML and back.
/// The two-stream read overload is intentionally unimplemented in production and must return a
/// "forbidden" error, so that behaviour is asserted explicitly.
/// </summary>
public class XmlDataFormatterTests
{
    /// <summary>
    /// The default encoding must be UTF-8.
    /// </summary>
    [Fact]
    public void Encoding_Default_IsUtf8_Test()
    {
        // Act
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();

        // Assert: the property defaults to UTF-8.
        Assert.Equal(Encoding.UTF8, formatter.Encoding);
    }

    /// <summary>
    /// The encoding property must be assignable (round-trips the value).
    /// </summary>
    [Fact]
    public void Encoding_Set_StoresValue_Test()
    {
        // Arrange
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();

        // Act: pick a distinctly different encoding.
        formatter.Encoding = Encoding.Unicode;

        // Assert
        Assert.Equal(Encoding.Unicode, formatter.Encoding);
    }

    /// <summary>
    /// A null data object on write yields a validation error naming "data".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();
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
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new XmlSampleData(), null!);

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
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();

        // Act
        ErrorOr<XmlSampleData?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// The two-stream read overload is not implemented and must always return a "forbidden"
    /// error with the documented description, regardless of the arguments supplied.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WithOutputStream_ReturnsForbidden_Test()
    {
        // Arrange
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();
        using MemoryStream input = new MemoryStream();
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, output);

        // Assert: the fixed forbidden error is returned.
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal("Method not implemented.", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip: an object serialized with <c>WriteAsync</c> must deserialize back into
    /// an equal object with <c>ReadAsync</c>.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsObject_Test()
    {
        // Arrange
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();
        XmlSampleData original = new XmlSampleData { Name = "Forge", Value = 42 };
        using MemoryStream buffer = new MemoryStream();

        // Act (write): serialize to XML.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, buffer);
        Assert.False(writeResult.IsError);

        // Act (read): rewind and deserialize.
        buffer.Position = 0;
        ErrorOr<XmlSampleData?> readResult = await formatter.ReadAsync(buffer);

        // Assert: the deserialized object has the same field values.
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);
        Assert.Equal(original.Name, readResult.Value!.Name);
        Assert.Equal(original.Value, readResult.Value!.Value);
    }

    /// <summary>
    /// Feeding non-XML content to <c>ReadAsync</c> causes the XML deserializer to throw; the
    /// <c>ProtectAsync</c> wrapper must convert that into an error result instead of throwing.
    /// </summary>
    [Fact]
    public async Task ReadAsync_InvalidXml_ReturnsError_Test()
    {
        // Arrange: bytes that are definitely not valid XML.
        XmlDataFormatter<XmlSampleData> formatter = new XmlDataFormatter<XmlSampleData>();
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("this is not xml at all"));

        // Act
        ErrorOr<XmlSampleData?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.True(result.IsError);
    }
}
