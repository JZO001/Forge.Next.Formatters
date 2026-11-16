using System.IO;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="GZipStreamFormatter"/>, which compresses and decompresses a
/// <see cref="Stream"/> using GZip. It is the stream-payload counterpart of
/// <see cref="GZipByteArrayFormatter"/>. Two behaviours make it distinct from the Brotli stream
/// formatter and therefore get dedicated tests:
/// <list type="bullet">
///   <item>All three methods validate that <see cref="GZipStreamFormatter.BufferSize"/> is positive.</item>
///   <item>The single-stream read overload returns a <see cref="MemoryStream"/> rewound to position 0
///   (since 3.2.5; the Brotli and AES stream formatters since 3.4.0).</item>
/// </list>
/// </summary>
public class GZipStreamFormatterTests
{
    /// <summary>
    /// The formatter starts with the shared default buffer size (8 KB).
    /// </summary>
    [Fact]
    public void BufferSize_Default_IsDefaultBufferSize_Test()
    {
        // Act
        GZipStreamFormatter formatter = new GZipStreamFormatter();

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
        GZipStreamFormatter formatter = new GZipStreamFormatter();
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
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// WriteAsync validates a non-positive buffer size (after the null checks) and names "BufferSize".
    /// Valid non-null streams are supplied so the buffer-size guard is the one that fires.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-8192)]
    public async Task WriteAsync_NonPositiveBufferSize_ReturnsValidationError_Test(int bufferSize)
    {
        // Arrange
        GZipStreamFormatter formatter = new GZipStreamFormatter { BufferSize = bufferSize };
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(input, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("BufferSize", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the single-stream read overload yields a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStream_ReturnsValidationError_Test()
    {
        // Arrange
        GZipStreamFormatter formatter = new GZipStreamFormatter();

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// The single-stream read overload validates a non-positive buffer size and names "BufferSize".
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-8192)]
    public async Task ReadAsync_NonPositiveBufferSize_ReturnsValidationError_Test(int bufferSize)
    {
        // Arrange: a valid stream but an invalid buffer size.
        GZipStreamFormatter formatter = new GZipStreamFormatter { BufferSize = bufferSize };
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(input);

        // Assert
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
        GZipStreamFormatter formatter = new GZipStreamFormatter();
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
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using MemoryStream input = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// The two-stream read overload validates a non-positive buffer size and names "BufferSize".
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-8192)]
    public async Task ReadAsyncWithOutput_NonPositiveBufferSize_ReturnsValidationError_Test(int bufferSize)
    {
        // Arrange
        GZipStreamFormatter formatter = new GZipStreamFormatter { BufferSize = bufferSize };
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("BufferSize", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip through the single-stream read overload: the decompressed stream, once
    /// rewound, must contain the original bytes.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsStream_Test()
    {
        // Arrange
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('G', 900) + "-gzip-stream");
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
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('H', 400) + "-gzip-stream-output");
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
    /// A non-readable input stream must be surfaced as an error, not thrown (the GZipStream
    /// constructor rejects it and <c>ProtectAsync</c> catches the exception).
    /// </summary>
    [Fact]
    public async Task ReadAsync_NonReadableStream_ReturnsError_Test()
    {
        // Arrange
        GZipStreamFormatter formatter = new GZipStreamFormatter();
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
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Stream nonWritable = Substitute.For<Stream>();
        nonWritable.CanWrite.Returns(false);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(source, nonWritable);

        // Assert
        Assert.True(result.IsError);
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    /// <summary>
    /// A cancelled token stops the single-stream read, and the caller's own cancellation is thrown as an
    /// <see cref="OperationCanceledException"/> (since 3.4.0; before, it came back as an
    /// <see cref="ErrorType.Unexpected"/> error).
    /// </summary>
    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange: a valid payload, written with a token that is not cancelled.
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// A cancelled token stops the two-stream read, and throws the same way.
    /// </summary>
    [Fact]
    public async Task ReadAsyncWithOutput_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange: a valid payload, written with a token that is not cancelled.
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, output, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// A cancelled token stops the write, and throws the same way.
    /// </summary>
    [Fact]
    public async Task WriteAsync_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.WriteAsync(source, output, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// A cancellation that the caller did not request, here an <see cref="OperationCanceledException"/>
    /// thrown by the input stream itself as an inner timeout would, is still returned as an
    /// <see cref="ErrorType.Unexpected"/> error: only the caller's own cancellation is thrown.
    /// </summary>
    [Fact]
    public async Task ReadAsync_InnerCancellation_ReturnsUnexpectedError_Test()
    {
        // Arrange: a stream that cancels by itself; the caller's token is not cancelled.
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        using Stream input = new SelfCancellingStream();

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(input, CancellationToken.None);

        // Assert: an Unexpected error, made from an OperationCanceledException (or a subclass of it).
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        Assert.True(typeof(OperationCanceledException).IsAssignableFrom(Type.GetType((string)result.FirstError.Metadata!["type"])));
    }

    // ------------------------------------------------------------------
    // The returned stream
    // ------------------------------------------------------------------

    /// <summary>
    /// The single-stream read returns a new <see cref="MemoryStream"/> rewound to position 0 (since
    /// 3.2.5), so the caller can read it at once; <see cref="DataFormatterExtensions"/> depends on it.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ReturnedStream_IsAtPositionZero_Test()
    {
        // Arrange: a valid payload of a known length.
        GZipStreamFormatter formatter = new GZipStreamFormatter();
        byte[] original = Encoding.UTF8.GetBytes(new string('P', 300) + "-position");
        using MemoryStream source = new MemoryStream(original);
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(payload);

        // Assert: the stream holds the original bytes and is at its start.
        Assert.False(result.IsError);
        using Stream decompressed = result.Value!;
        Assert.Equal(original.Length, decompressed.Length);
        Assert.Equal(0, decompressed.Position);
    }

    // ------------------------------------------------------------------
    // Test helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Rewinds a stream and returns all of its bytes, so the round-trip tests do not depend on the
    /// position of the returned stream (that has a test of its own).
    /// </summary>
    /// <param name="stream">The stream to drain.</param>
    /// <returns>All bytes contained in the stream.</returns>
    private static byte[] ReadAllBytes(Stream stream)
    {
        // Rewind so we read from the beginning regardless of the current position.
        stream.Position = 0;

        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// A readable stream whose every read throws <see cref="OperationCanceledException"/>, as a stream with a
    /// timeout of its own would.
    /// </summary>
    private sealed class SelfCancellingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new OperationCanceledException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException<int>(new OperationCanceledException());
#if NETCOREAPP
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(new OperationCanceledException());
#endif
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
