using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
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
/// A SOAP sample type with a field of another custom type, for the allowed-types tests.
/// </summary>
[Serializable]
public class SoapOuterSample
{
    /// <summary>A field of a custom type, which the payload names too.</summary>
    public SoapInnerSample Inner = new SoapInnerSample();
}

/// <summary>
/// The custom type of <see cref="SoapOuterSample.Inner"/>.
/// </summary>
[Serializable]
public class SoapInnerSample
{
    /// <summary>A sample string field.</summary>
    public string Text = string.Empty;
}

/// <summary>
/// A SOAP sample type with an array and a list of a custom type, for the allowed-types tests.
/// </summary>
[Serializable]
public class SoapCollectionsSample
{
    /// <summary>An array: the binder is asked for its element type only.</summary>
    public SoapInnerSample[] Items = Array.Empty<SoapInnerSample>();

    /// <summary>A generic list: the binder is asked for the list type itself.</summary>
    public List<SoapInnerSample> List = new List<SoapInnerSample>();
}

/// <summary>
/// A SOAP sample type that counts how often it was deserialized, to show whether a payload of it was created.
/// </summary>
[Serializable]
public class SoapForeignSample
{
    /// <summary>How many instances were deserialized; reset by the test that reads it.</summary>
    public static int Deserialized;

    /// <summary>A sample integer field.</summary>
    public int Value;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) { Deserialized++; }
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
    /// The read leaves the caller's input stream open, so it can be rewound and read again (since 3.4.0: the
    /// <see cref="SoapFormatter"/> closes the stream it deserializes, so the formatter now gives it a copy).
    /// </summary>
    [Fact]
    public async Task ReadAsync_InputStream_StaysOpen_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        using MemoryStream buffer = new MemoryStream();
        Assert.False((await formatter.WriteAsync(new SoapSampleData { Name = "Forge", Value = 7 }, buffer)).IsError);
        buffer.Position = 0;

        // Act: read, rewind, read again.
        ErrorOr<SoapSampleData?> first = await formatter.ReadAsync(buffer);
        bool openAfterRead = buffer.CanRead;
        buffer.Position = 0;
        ErrorOr<SoapSampleData?> second = await formatter.ReadAsync(buffer);

        // Assert
        Assert.False(first.IsError);
        Assert.True(openAfterRead);
        Assert.False(second.IsError);
        Assert.Equal("Forge", second.Value!.Name);
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

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    /// <summary>
    /// The read checks the token once before the synchronous <see cref="SoapFormatter.Deserialize(Stream)"/>:
    /// a cancelled token throws <see cref="OperationCanceledException"/> and nothing is read (since 3.4.0;
    /// before, the read ignored the token).
    /// </summary>
    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsAndReadsNothing_Test()
    {
        // Arrange: valid SOAP, written with a token that is not cancelled.
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(new SoapSampleData { Name = "Forge", Value = 1 }, payload)).IsError);
        payload.Position = 0;

        // Act & Assert: thrown, and the input is not touched.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, new CancellationToken(canceled: true)));
        Assert.Equal(0, payload.Position);
    }

    /// <summary>
    /// The write checks the token once before the synchronous
    /// <see cref="SoapFormatter.Serialize(Stream, object)"/>: a cancelled token throws
    /// <see cref="OperationCanceledException"/> and writes nothing (since 3.4.0; before, the write ignored the
    /// token).
    /// </summary>
    [Fact]
    public async Task WriteAsync_CancelledToken_ThrowsAndWritesNothing_Test()
    {
        // Arrange
        XmlSoapFormatter<SoapSampleData> formatter = new XmlSoapFormatter<SoapSampleData>();
        using MemoryStream output = new MemoryStream();

        // Act & Assert: thrown, and nothing is written.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.WriteAsync(new SoapSampleData { Name = "Forge", Value = 1 }, output, new CancellationToken(canceled: true)));
        Assert.Equal(0, output.Length);
    }

    // ------------------------------------------------------------------
    // WithAllowedTypes (since 3.4.0)
    // ------------------------------------------------------------------

    /// <summary>
    /// A formatter made with <see cref="XmlSoapFormatter{T}.WithAllowedTypes"/> reads a payload of its own type.
    /// </summary>
    [Fact]
    public async Task WithAllowedTypes_PayloadOfT_ReadsIt_Test()
    {
        // Arrange
        using MemoryStream buffer = new MemoryStream();
        Assert.False((await new XmlSoapFormatter<SoapSampleData>().WriteAsync(new SoapSampleData { Name = "Forge", Value = 7 }, buffer)).IsError);
        buffer.Position = 0;

        // Act
        ErrorOr<SoapSampleData?> result = await XmlSoapFormatter<SoapSampleData>.WithAllowedTypes().ReadAsync(buffer);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("Forge", result.Value!.Name);
    }

    /// <summary>
    /// A payload that names another type: the parameterless constructor's formatter creates it before the cast fails,
    /// while the one made with <see cref="XmlSoapFormatter{T}.WithAllowedTypes"/> refuses it before it is created.
    /// Both return an <see cref="ErrorType.Unexpected"/> error.
    /// </summary>
    [Fact]
    public async Task WithAllowedTypes_PayloadOfAnotherType_ReturnsErrorWithoutCreatingIt_Test()
    {
        // Arrange: a payload of SoapForeignSample.
        using MemoryStream buffer = new MemoryStream();
        Assert.False((await new XmlSoapFormatter<SoapForeignSample>().WriteAsync(new SoapForeignSample { Value = 42 }, buffer)).IsError);
        byte[] payload = buffer.ToArray();

        // Act
        SoapForeignSample.Deserialized = 0;
        ErrorOr<SoapSampleData?> unrestricted = await new XmlSoapFormatter<SoapSampleData>().ReadAsync(new MemoryStream(payload));
        int createdUnrestricted = SoapForeignSample.Deserialized;

        SoapForeignSample.Deserialized = 0;
        ErrorOr<SoapSampleData?> restricted = await XmlSoapFormatter<SoapSampleData>.WithAllowedTypes().ReadAsync(new MemoryStream(payload));
        int createdRestricted = SoapForeignSample.Deserialized;

        // Assert
        Assert.True(unrestricted.IsError);
        Assert.Equal(1, createdUnrestricted);
        Assert.True(restricted.IsError);
        Assert.Equal(ErrorType.Unexpected, restricted.FirstError.Type);
        Assert.Equal(0, createdRestricted);
    }

    /// <summary>
    /// The types of the fields must be listed too: with the field's type listed the payload reads, without it the
    /// payload is refused.
    /// </summary>
    [Fact]
    public async Task WithAllowedTypes_FieldTypeListedOrNot_ReadsOrRefuses_Test()
    {
        // Arrange
        using MemoryStream buffer = new MemoryStream();
        SoapOuterSample original = new SoapOuterSample { Inner = new SoapInnerSample { Text = "inner" } };
        Assert.False((await new XmlSoapFormatter<SoapOuterSample>().WriteAsync(original, buffer)).IsError);
        byte[] payload = buffer.ToArray();

        // Act
        ErrorOr<SoapOuterSample?> listed = await XmlSoapFormatter<SoapOuterSample>.WithAllowedTypes(typeof(SoapInnerSample)).ReadAsync(new MemoryStream(payload));
        ErrorOr<SoapOuterSample?> notListed = await XmlSoapFormatter<SoapOuterSample>.WithAllowedTypes().ReadAsync(new MemoryStream(payload));

        // Assert
        Assert.False(listed.IsError);
        Assert.Equal("inner", listed.Value!.Inner.Text);
        Assert.True(notListed.IsError);
    }

    /// <summary>
    /// Arrays need only their element type listed, a generic list needs the list type itself: with the element type
    /// and the list type listed the payload reads, with only the element type it is refused at the list. On
    /// <c>net48</c> the SOAP formatter creates the types of <c>mscorlib</c>, <see cref="List{T}"/> among them,
    /// without asking the binder, so there the payload reads without the list type too: a limit of the
    /// protection on that target.
    /// </summary>
    [Fact]
    public async Task WithAllowedTypes_ArrayAndList_NeedElementAndListType_Test()
    {
        // Arrange
        using MemoryStream buffer = new MemoryStream();
        SoapCollectionsSample original = new SoapCollectionsSample
        {
            Items = new[] { new SoapInnerSample { Text = "a" } },
            List = new List<SoapInnerSample> { new SoapInnerSample { Text = "b" } },
        };
        Assert.False((await new XmlSoapFormatter<SoapCollectionsSample>().WriteAsync(original, buffer)).IsError);
        byte[] payload = buffer.ToArray();

        // Act
        ErrorOr<SoapCollectionsSample?> both = await XmlSoapFormatter<SoapCollectionsSample>
            .WithAllowedTypes(typeof(SoapInnerSample), typeof(List<SoapInnerSample>)).ReadAsync(new MemoryStream(payload));
        ErrorOr<SoapCollectionsSample?> elementOnly = await XmlSoapFormatter<SoapCollectionsSample>
            .WithAllowedTypes(typeof(SoapInnerSample)).ReadAsync(new MemoryStream(payload));

        // Assert
        Assert.False(both.IsError);
        Assert.Equal("a", both.Value!.Items[0].Text);
        Assert.Equal("b", both.Value!.List[0].Text);
#if NETCOREAPP
        Assert.True(elementOnly.IsError);
#else
        Assert.False(elementOnly.IsError);
#endif
    }

    /// <summary>
    /// <see cref="XmlSoapFormatter{T}.WithAllowedTypes"/> rejects a <c>null</c> array.
    /// </summary>
    [Fact]
    public void WithAllowedTypes_NullArray_ThrowsArgumentNullException_Test()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => XmlSoapFormatter<SoapSampleData>.WithAllowedTypes(null!));
        Assert.Equal("otherAllowedTypes", exception.ParamName);
    }
}
