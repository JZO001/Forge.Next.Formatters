using System.IO;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="BrotliStreamFormatter"/>, which compresses and decompresses a
/// <see cref="Stream"/> using the Brotli algorithm. The single-stream read overload returns a
/// decompressed <see cref="Stream"/> positioned at its end, so tests rewind it before reading.
/// </summary>
public class BrotliStreamFormatterTests
{
    /// <summary>
    /// The formatter starts with the shared default buffer size.
    /// </summary>
    [Fact]
    public void BufferSize_Default_IsDefaultBufferSize_Test()
    {
        // Act
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();

        // Assert
        Assert.Equal(Consts.DefaultBufferSize, formatter.BufferSize);
    }

    /// <summary>
    /// A null data stream on write yields a validation error naming "data".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
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
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the single-stream read overload yields a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStream_ReturnsValidationError_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the two-stream read overload yields a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStreamWithOutput_ReturnsValidationError_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(null!, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream on the two-stream read overload yields a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        using MemoryStream input = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip through the single-stream read overload: the decompressed stream, once
    /// rewound, must contain the original bytes.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsStream_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('C', 800) + "-brotli-stream");
        using MemoryStream source = new MemoryStream(original);
        using MemoryStream compressed = new MemoryStream();

        // Act (write): compress the source stream.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(source, compressed);
        Assert.False(writeResult.IsError);

        // Act (read): rewind and decompress into a returned stream.
        compressed.Position = 0;
        ErrorOr<Stream?> readResult = await formatter.ReadAsync(compressed);

        // Assert
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);
        Assert.Equal(original, ReadAllBytes(readResult.Value!));
    }

    /// <summary>
    /// Full round-trip through the two-stream read overload.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsyncWithOutput_RoundTripsStream_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('D', 300) + "-brotli-stream-output");
        using MemoryStream source = new MemoryStream(original);
        using MemoryStream compressed = new MemoryStream();

        // Act (write)
        ErrorOr<Success> writeResult = await formatter.WriteAsync(source, compressed);
        Assert.False(writeResult.IsError);

        // Act (read into an output stream)
        compressed.Position = 0;
        using MemoryStream decompressed = new MemoryStream();
        ErrorOr<Success> readResult = await formatter.ReadAsync(compressed, decompressed);

        // Assert
        Assert.False(readResult.IsError);
        Assert.Equal(original, decompressed.ToArray());
    }

    /// <summary>
    /// A non-readable input stream must be surfaced as an error, not thrown.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NonReadableStream_ReturnsError_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        Stream nonReadable = Substitute.For<Stream>();
        nonReadable.CanRead.Returns(false);

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(nonReadable);

        // Assert
        Assert.True(result.IsError);
    }

    /// <summary>
    /// A non-writable output stream on write must be surfaced as an error.
    /// </summary>
    [Fact]
    public async Task WriteAsync_NonWritableStream_ReturnsError_Test()
    {
        // Arrange
        BrotliStreamFormatter formatter = new BrotliStreamFormatter();
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Stream nonWritable = Substitute.For<Stream>();
        nonWritable.CanWrite.Returns(false);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(source, nonWritable);

        // Assert
        Assert.True(result.IsError);
    }

    // ------------------------------------------------------------------
    // Test helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Rewinds a stream and returns all of its bytes. Used because the single-stream read
    /// overload returns a stream positioned at the end of the decompressed content.
    /// </summary>
    /// <param name="stream">The stream to drain.</param>
    /// <returns>All bytes contained in the stream.</returns>
    private static byte[] ReadAllBytes(Stream stream)
    {
        stream.Position = 0;
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
