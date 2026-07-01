using ErrorOr;
using Forge.Next.Shared;
using System.IO.Compression;

namespace Forge.Next.Formatters;

/// <summary>
/// Implementation of IBrotliByteArrayFormatter
/// </summary>
public class BrotliByteArrayFormatter : IBrotliByteArrayFormatter
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    public int BufferSize { get; set; } = Consts.DefaultBufferSize;

    /// <summary>
    /// Reads and decompresses data from the input stream using Brotli compression and returns the decompressed byte array.
    /// </summary>
    /// <param name="inputStream">The input stream containing the compressed data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the decompressed byte array.</returns>
    public Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<byte[]?>)Error.Validation(description: nameof(inputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<byte[]?>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<BrotliByteArrayFormatter, byte[]?>(async (_, _) =>
        {
            using BrotliStream brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress, true);
            using MemoryStream ms = new MemoryStream();

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = brotliStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                ms.Write(buffer, 0, numRead);
            }

            return ms.ToArray();
        });
    }

    /// <summary>
    /// Reads and decompresses data from the input stream using Brotli compression and writes the decompressed data to the output stream.
    /// </summary>
    /// <param name="inputStream">The input stream containing the compressed data.</param>
    /// <param name="outputStream">The output stream to write the decompressed data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains a success indicator.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(inputStream)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<BrotliByteArrayFormatter, Success>(async (_, _) =>
        {
            using BrotliStream brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress, true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = brotliStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                outputStream.Write(buffer, 0, numRead);
            }

            return Result.Success;
        });
    }

    /// <summary>
    /// Serializes and writes the provided byte array to the output stream using Brotli compression.
    /// </summary>
    /// <param name="data">The byte array containing the data to be compressed and written.</param>
    /// <param name="outputStream">The output stream to write the compressed data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a success indicator.</returns>
    public Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<BrotliByteArrayFormatter, Success>(async (_, _) =>
        {
            using MemoryStream ms = new MemoryStream(data);
            ms.Position = 0;

            using BrotliStream brotliStream = new BrotliStream(outputStream, CompressionMode.Compress, true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = ms.Read(buffer, 0, buffer.Length)) != 0)
            {
                brotliStream.Write(buffer, 0, numRead);
            }

            brotliStream.Flush();

            ms.SetLength(0);

            return Result.Success;
        });
    }

}
