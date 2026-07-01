using ErrorOr;
using Forge.Next.Shared;
using System.IO.Compression;

namespace Forge.Next.Formatters;

/// <summary>
/// Implementation of the IBrotliStreamFormatter interface for Brotli compression and decompression of streams.
/// </summary>
public class BrotliStreamFormatter : IBrotliStreamFormatter
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    public int BufferSize { get; set; } = Consts.DefaultBufferSize;

    /// <summary>
    /// Reads and decompresses data from the input stream using Brotli compression.
    /// </summary>
    /// <param name="inputStream">The input stream containing the compressed data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the decompressed stream or an error.</returns>
    public Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Stream?>)Error.Validation(description: nameof(inputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Stream?>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<BrotliStreamFormatter, Stream?>(async (_, _) =>
        {
            using BrotliStream brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress, true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;
            MemoryStream ms = new MemoryStream();

            try
            {
                while ((numRead = await brotliStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await ms.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                ms.SetLength(0);
                await ms.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return ms;
        });
    }

    /// <summary>
    /// Reads and decompresses data from the input stream using Brotli compression and writes the decompressed data to the output stream.
    /// </summary>
    /// <param name="inputStream">The input stream containing the compressed data.</param>
    /// <param name="outputStream">The output stream to write the decompressed data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the result of the operation.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(inputStream)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<BrotliStreamFormatter, Success>(async (_, _) =>
        {
            using BrotliStream brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress, true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await brotliStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await outputStream.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }

            return Result.Success;
        });
    }

    /// <summary>
    /// Writes and compresses data from the input stream to the output stream using Brotli compression.
    /// </summary>
    /// <param name="data">The input stream containing the data to be compressed.</param>
    /// <param name="outputStream">The output stream to write the compressed data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the result of the operation.</returns>
    public Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<BrotliStreamFormatter, Success>(async (_, _) =>
        {
            using BrotliStream brotliStream = new BrotliStream(outputStream, CompressionMode.Compress, true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await data.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await brotliStream.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }

            await brotliStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success;
        });
    }

}
