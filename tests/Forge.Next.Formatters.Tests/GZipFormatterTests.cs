using System.IO;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="GZipFormatter"/>, which compresses and decompresses a
/// <see cref="byte"/> array using GZip. Unlike the other compression formatters, the
/// single-stream read overload also validates that <see cref="GZipFormatter.BufferSize"/> is
/// positive, so that branch gets its own test.
/// </summary>
public class GZipFormatterTests
{
    /// <summary>
    /// The formatter starts with the shared default buffer size (8 KB).
    /// </summary>
    [Fact]
    public void BufferSize_Default_IsDefaultBufferSize_Test()
    {
        // Act
        GZipFormatter formatter = new GZipFormatter();

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
        GZipFormatter formatter = new GZipFormatter();
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
        GZipFormatter formatter = new GZipFormatter();

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
        GZipFormatter formatter = new GZipFormatter();

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// The single-stream read overload has an extra guard: a non-positive buffer size must be
    /// rejected with a validation error naming "BufferSize". The null-input check comes first
    /// in the source, so a valid (non-null) stream is supplied to reach the buffer-size guard.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-8192)]
    public async Task ReadAsync_NonPositiveBufferSize_ReturnsValidationError_Test(int bufferSize)
    {
        // Arrange: a valid stream but an invalid buffer size.
        GZipFormatter formatter = new GZipFormatter { BufferSize = bufferSize };
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(input);

        // Assert: the dedicated buffer-size validation fired.
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("BufferSize", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the two-stream read overload yields a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStreamWithOutput_ReturnsValidationError_Test()
    {
        // Arrange
        GZipFormatter formatter = new GZipFormatter();
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
        GZipFormatter formatter = new GZipFormatter();
        using MemoryStream input = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip through the single-stream read overload: GZip-compressed then
    /// decompressed data must equal the original.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsByteArray_Test()
    {
        // Arrange
        GZipFormatter formatter = new GZipFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('E', 1200) + "-gzip-payload");
        using MemoryStream compressed = new MemoryStream();

        // Act (write)
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, compressed);
        Assert.False(writeResult.IsError);

        // Act (read)
        compressed.Position = 0;
        ErrorOr<byte[]?> readResult = await formatter.ReadAsync(compressed);

        // Assert
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
        GZipFormatter formatter = new GZipFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('F', 700) + "-gzip-output");
        using MemoryStream compressed = new MemoryStream();

        // Act (write)
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, compressed);
        Assert.False(writeResult.IsError);

        // Act (read into output)
        compressed.Position = 0;
        using MemoryStream decompressed = new MemoryStream();
        ErrorOr<Success> readResult = await formatter.ReadAsync(compressed, decompressed);

        // Assert
        Assert.False(readResult.IsError);
        Assert.Equal(original, decompressed.ToArray());
    }

    /// <summary>
    /// A non-readable input stream must be surfaced as an error (the GZipStream constructor
    /// rejects it and <c>ProtectAsync</c> catches the exception).
    /// </summary>
    [Fact]
    public async Task ReadAsync_NonReadableStream_ReturnsError_Test()
    {
        // Arrange
        GZipFormatter formatter = new GZipFormatter();
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
        GZipFormatter formatter = new GZipFormatter();
        Stream nonWritable = Substitute.For<Stream>();
        nonWritable.CanWrite.Returns(false);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new byte[] { 1, 2, 3, 4 }, nonWritable);

        // Assert
        Assert.True(result.IsError);
    }
}
