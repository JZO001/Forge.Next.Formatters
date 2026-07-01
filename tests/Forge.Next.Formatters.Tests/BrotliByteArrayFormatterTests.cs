using System.IO;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="BrotliByteArrayFormatter"/>, which compresses and decompresses a
/// <see cref="byte"/> array using the Brotli algorithm. Coverage: default state, argument
/// validation, a compress/decompress round-trip (both read overloads) and the error path.
/// </summary>
public class BrotliByteArrayFormatterTests
{
    /// <summary>
    /// The formatter starts life with the shared default buffer size.
    /// </summary>
    [Fact]
    public void BufferSize_Default_IsDefaultBufferSize_Test()
    {
        // Act
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();

        // Assert
        Assert.Equal(Consts.DefaultBufferSize, formatter.BufferSize);
    }

    /// <summary>
    /// A null data array on write yields a validation error naming "data".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
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
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new byte[] { 1, 2, 3 }, null!);

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
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync((Stream)null!);

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
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
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
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
        using MemoryStream input = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip through the single-stream read overload: compressed then decompressed
    /// data must equal the original bytes.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsByteArray_Test()
    {
        // Arrange: highly compressible text makes the round-trip meaningful.
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('A', 1000) + "-brotli-payload");
        using MemoryStream compressed = new MemoryStream();

        // Act (write): compress into the stream.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, compressed);
        Assert.False(writeResult.IsError);

        // Act (read): rewind and decompress.
        compressed.Position = 0;
        ErrorOr<byte[]?> readResult = await formatter.ReadAsync(compressed);

        // Assert: decompressed bytes equal the original.
        Assert.False(readResult.IsError);
        Assert.Equal(original, readResult.Value);
    }

    /// <summary>
    /// Full round-trip through the two-stream read overload.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsyncWithOutput_RoundTripsByteArray_Test()
    {
        // Arrange
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('B', 500) + "-brotli-output");
        using MemoryStream compressed = new MemoryStream();

        // Act (write)
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, compressed);
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
    /// A non-readable input stream must be surfaced as an error, not thrown. The
    /// <c>BrotliStream</c> constructor rejects streams that report they cannot be read.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NonReadableStream_ReturnsError_Test()
    {
        // Arrange
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
        Stream nonReadable = Substitute.For<Stream>();
        nonReadable.CanRead.Returns(false);

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(nonReadable);

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
        BrotliByteArrayFormatter formatter = new BrotliByteArrayFormatter();
        Stream nonWritable = Substitute.For<Stream>();
        nonWritable.CanWrite.Returns(false);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new byte[] { 1, 2, 3, 4 }, nonWritable);

        // Assert
        Assert.True(result.IsError);
    }
}
