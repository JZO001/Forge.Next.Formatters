using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// A small JSON-serializable data-transfer object used by <see cref="DataFormatterExtensionsTests"/>
/// for the round-trip scenarios that use a real <see cref="SystemJsonFormatter{T}"/>.
/// </summary>
public class FormatterExtSample
{
    /// <summary>A sample string property.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A sample integer property.</summary>
    public int Value { get; set; }
}

/// <summary>
/// Unit tests for <see cref="DataFormatterExtensions"/>, the <c>Read</c>/<c>Write</c> convenience
/// extension methods on <see cref="IDataFormatter{T}"/>. These add file access and an optional GZip
/// compression layer on top of any formatter.
/// <para>
/// The tests fall into three groups:
/// </para>
/// <list type="bullet">
///   <item>Argument validation (null formatter / stream / file) — deterministic guard clauses.</item>
///   <item>End-to-end round-trips with compression disabled, which are direct pass-throughs to the
///   wrapped formatter and therefore fully reliable.</item>
///   <item>The composition branches (compress on write, decompress on read, delegate to the inner
///   formatter) verified with NSubstitute mocks and with a real GZip layer, so each branch is
///   checked without depending on the others.</item>
/// </list>
/// </summary>
public class DataFormatterExtensionsTests
{
    // ------------------------------------------------------------------
    // Argument validation — Read
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>Read(stream)</c> with a null formatter returns a validation error naming "formatter".
    /// </summary>
    [Fact]
    public async Task Read_NullFormatterWithStream_ReturnsValidationError_Test()
    {
        // Arrange: a null formatter reference (extension methods can be invoked on null).
        IDataFormatter<FormatterExtSample> formatter = null!;
        using MemoryStream stream = new MemoryStream();

        // Act
        ErrorOr<FormatterExtSample?> result = await formatter.Read(stream);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("formatter", result.FirstError.Description);
    }

    /// <summary>
    /// <c>Read(stream)</c> with a null stream returns a validation error naming "stream".
    /// </summary>
    [Fact]
    public async Task Read_NullStream_ReturnsValidationError_Test()
    {
        // Arrange
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();

        // Act
        ErrorOr<FormatterExtSample?> result = await formatter.Read((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("stream", result.FirstError.Description);
    }

    /// <summary>
    /// <c>Read(file)</c> with a null formatter returns a validation error naming "formatter".
    /// </summary>
    [Fact]
    public async Task Read_NullFormatterWithFile_ReturnsValidationError_Test()
    {
        // Arrange: a null formatter and a harmless (never-opened) file reference.
        IDataFormatter<FormatterExtSample> formatter = null!;

        // Act
        ErrorOr<FormatterExtSample?> result = await formatter.Read(new FileInfo("does-not-matter.json"));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("formatter", result.FirstError.Description);
    }

    /// <summary>
    /// <c>Read(file)</c> with a null file returns a validation error naming "file".
    /// </summary>
    [Fact]
    public async Task Read_NullFile_ReturnsValidationError_Test()
    {
        // Arrange
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();

        // Act
        ErrorOr<FormatterExtSample?> result = await formatter.Read((FileInfo)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("file", result.FirstError.Description);
    }

    // ------------------------------------------------------------------
    // Argument validation — Write
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>Write(stream)</c> with a null formatter returns a validation error naming "formatter".
    /// </summary>
    [Fact]
    public async Task Write_NullFormatterWithStream_ReturnsValidationError_Test()
    {
        // Arrange
        IDataFormatter<FormatterExtSample> formatter = null!;
        using MemoryStream stream = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.Write(new FormatterExtSample(), stream);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("formatter", result.FirstError.Description);
    }

    /// <summary>
    /// <c>Write(stream)</c> with a null stream returns a validation error naming "stream".
    /// </summary>
    [Fact]
    public async Task Write_NullStream_ReturnsValidationError_Test()
    {
        // Arrange
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();

        // Act
        ErrorOr<Success> result = await formatter.Write(new FormatterExtSample(), (Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("stream", result.FirstError.Description);
    }

    /// <summary>
    /// <c>Write(file)</c> with a null formatter returns a validation error naming "formatter".
    /// </summary>
    [Fact]
    public async Task Write_NullFormatterWithFile_ReturnsValidationError_Test()
    {
        // Arrange
        IDataFormatter<FormatterExtSample> formatter = null!;

        // Act
        ErrorOr<Success> result = await formatter.Write(new FormatterExtSample(), new FileInfo("does-not-matter.json"));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("formatter", result.FirstError.Description);
    }

    /// <summary>
    /// <c>Write(file)</c> with a null file returns a validation error naming "file".
    /// </summary>
    [Fact]
    public async Task Write_NullFile_ReturnsValidationError_Test()
    {
        // Arrange
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();

        // Act
        ErrorOr<Success> result = await formatter.Write(new FormatterExtSample(), (FileInfo)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("file", result.FirstError.Description);
    }

    // ------------------------------------------------------------------
    // Round-trips without compression (direct pass-through)
    // ------------------------------------------------------------------

    /// <summary>
    /// With compression disabled, <c>Write</c> then <c>Read</c> against a stream must round-trip the
    /// object unchanged (the extension simply forwards to the wrapped formatter).
    /// </summary>
    [Fact]
    public async Task WriteThenRead_StreamWithoutCompression_RoundTrips_Test()
    {
        // Arrange
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();
        FormatterExtSample data = new FormatterExtSample { Name = "round-trip", Value = 7 };
        using MemoryStream stream = new MemoryStream();

        // Act (write, no compression)
        ErrorOr<Success> writeResult = await formatter.Write(data, stream, compress: false);
        Assert.False(writeResult.IsError);

        // Act (read, no decompression)
        stream.Position = 0;
        ErrorOr<FormatterExtSample?> readResult = await formatter.Read(stream, decompress: false);

        // Assert
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);
        Assert.Equal(data.Name, readResult.Value!.Name);
        Assert.Equal(data.Value, readResult.Value!.Value);
    }

    /// <summary>
    /// With compression disabled, <c>Write</c> then <c>Read</c> against a file must round-trip the
    /// object. A unique temporary file is used and removed afterwards.
    /// </summary>
    [Fact]
    public async Task WriteThenRead_FileWithoutCompression_RoundTrips_Test()
    {
        // Arrange: a unique temp-file path so parallel test runs never collide.
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();
        FormatterExtSample data = new FormatterExtSample { Name = "file-round-trip", Value = 3 };
        string path = Path.Combine(Path.GetTempPath(), "fnf_" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            // Act (write to file, no compression)
            ErrorOr<Success> writeResult = await formatter.Write(data, new FileInfo(path), compress: false);
            Assert.False(writeResult.IsError);
            Assert.True(File.Exists(path));

            // Act (read from file, no decompression)
            ErrorOr<FormatterExtSample?> readResult = await formatter.Read(new FileInfo(path), decompress: false);

            // Assert
            Assert.False(readResult.IsError);
            Assert.NotNull(readResult.Value);
            Assert.Equal(data.Name, readResult.Value!.Name);
            Assert.Equal(data.Value, readResult.Value!.Value);
        }
        finally
        {
            // Clean up the temporary file regardless of the test outcome.
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ------------------------------------------------------------------
    // Composition branches
    // ------------------------------------------------------------------

    /// <summary>
    /// With compression enabled, <c>Write</c> must emit a valid GZip stream whose decompressed
    /// content equals exactly the formatter's uncompressed serialization. This checks the compress
    /// branch in isolation (a real GZip decompressor validates the output).
    /// </summary>
    [Fact]
    public async Task Write_StreamWithCompression_ProducesValidGZip_Test()
    {
        // Arrange
        SystemJsonFormatter<FormatterExtSample> formatter = new SystemJsonFormatter<FormatterExtSample>();
        FormatterExtSample data = new FormatterExtSample { Name = "zip", Value = 5 };

        // The uncompressed serialization is the expected decompressed payload.
        using MemoryStream raw = new MemoryStream();
        await formatter.Write(data, raw, compress: false);
        byte[] expectedJson = raw.ToArray();

        // Act: write with compression enabled.
        using MemoryStream compressed = new MemoryStream();
        ErrorOr<Success> writeResult = await formatter.Write(data, compressed, compress: true);
        Assert.False(writeResult.IsError);

        // Assert: the output is valid GZip and decompresses back to the raw JSON bytes.
        compressed.Position = 0;
        ErrorOr<byte[]?> decompressed = await new GZipByteArrayFormatter().ReadAsync(compressed);
        Assert.False(decompressed.IsError);
        Assert.Equal(expectedJson, decompressed.Value);
    }

    /// <summary>
    /// With compression disabled, <c>Read</c> must simply delegate to the wrapped formatter's
    /// <see cref="IDataFormatter{T}.ReadAsync(Stream, CancellationToken)"/> and return its result.
    /// A NSubstitute mock verifies both the delegation and the passed-through value.
    /// </summary>
    [Fact]
    public async Task Read_WithoutCompression_DelegatesToFormatter_Test()
    {
        // Arrange: a mock formatter that returns a known object.
        IDataFormatter<FormatterExtSample> mock = Substitute.For<IDataFormatter<FormatterExtSample>>();
        FormatterExtSample expected = new FormatterExtSample { Name = "delegated", Value = 11 };
        using MemoryStream stream = new MemoryStream();
        mock.ReadAsync(stream, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((ErrorOr<FormatterExtSample?>)expected));

        // Act
        ErrorOr<FormatterExtSample?> result = await mock.Read(stream, decompress: false);

        // Assert: the wrapped formatter was called exactly once with the same stream, and its
        // result flowed straight back to the caller.
        Assert.False(result.IsError);
        Assert.Same(expected, result.Value);
        await mock.Received(1).ReadAsync(stream, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With compression disabled, <c>Write</c> must delegate to the wrapped formatter's
    /// <see cref="IDataFormatter{T}.WriteAsync(T, Stream, CancellationToken)"/>. Verified with a mock.
    /// </summary>
    [Fact]
    public async Task Write_WithoutCompression_DelegatesToFormatter_Test()
    {
        // Arrange
        IDataFormatter<FormatterExtSample> mock = Substitute.For<IDataFormatter<FormatterExtSample>>();
        FormatterExtSample data = new FormatterExtSample { Name = "delegated-write", Value = 22 };
        using MemoryStream stream = new MemoryStream();
        mock.WriteAsync(data, stream, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((ErrorOr<Success>)Result.Success));

        // Act
        ErrorOr<Success> result = await mock.Write(data, stream, compress: false);

        // Assert
        Assert.False(result.IsError);
        await mock.Received(1).WriteAsync(data, stream, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With decompression enabled, <c>Read</c> must first peel the GZip layer and then delegate to
    /// the wrapped formatter with the decompressed stream. A real GZip payload drives the
    /// decompression while a mock formatter confirms the delegation and supplies the final value.
    /// </summary>
    [Fact]
    public async Task Read_WithCompression_DecompressesThenDelegatesToFormatter_Test()
    {
        // Arrange: produce a genuine GZip payload so the decompression step succeeds.
        byte[] payload = Encoding.UTF8.GetBytes("the decompressed bytes are irrelevant to the mock");
        using MemoryStream gzip = new MemoryStream();
        await new GZipByteArrayFormatter().WriteAsync(payload, gzip);
        gzip.Position = 0;

        // A mock formatter that returns a known object regardless of the stream content.
        IDataFormatter<FormatterExtSample> mock = Substitute.For<IDataFormatter<FormatterExtSample>>();
        FormatterExtSample expected = new FormatterExtSample { Name = "after-decompress", Value = 33 };
        mock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((ErrorOr<FormatterExtSample?>)expected));

        // Act
        ErrorOr<FormatterExtSample?> result = await mock.Read(gzip, decompress: true);

        // Assert: the GZip layer was peeled and the inner formatter produced the final value.
        Assert.False(result.IsError);
        Assert.Same(expected, result.Value);
        await mock.Received(1).ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }
}
