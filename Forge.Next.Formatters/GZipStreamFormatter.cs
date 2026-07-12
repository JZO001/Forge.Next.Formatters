using ErrorOr;
using Forge.Next.Shared;
using System.IO.Compression;

namespace Forge.Next.Formatters;

/// <summary>
/// GZipStreamFormatter is a class that implements the IGZipStreamFormatter interface for reading and writing GZip-compressed data.
/// </summary>
public class GZipStreamFormatter : IGZipStreamFormatter
{

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the GZipStreamFormatter. The default value is 8192 bytes.
    /// </summary>
    public int BufferSize { get; set; } = Consts.DefaultBufferSize;

    /// <summary>
    /// Reads GZip-compressed data from the provided input stream and returns the decompressed stream.
    /// </summary>
    /// <param name="inputStream">The input stream containing GZip-compressed data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the decompressed stream.</returns>
    public Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Stream?>)Error.Validation(description: nameof(inputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Stream?>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<GZipStreamFormatter, Stream?>(async (_, _) =>
        {
            using GZipStream gzipStream = new(inputStream, CompressionMode.Decompress, leaveOpen: true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;
            MemoryStream ms = new();

            while ((numRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await ms.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }

            await gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            ms.Position = 0; // the caller receives a readable stream, not one parked at the end

            return ms;
        });
    }

    /// <summary>
    /// Reads GZip-compressed data from the provided input stream and writes the decompressed data to the specified output stream.
    /// </summary>
    /// <param name="inputStream">The input stream containing GZip-compressed data.</param>
    /// <param name="outputStream">The output stream to write the decompressed data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Success value if the operation completes successfully.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(inputStream)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<GZipStreamFormatter, Success>(async (_, _) =>
        {
            using GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, true);
            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await outputStream.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }

            await gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success;
        });
    }

    /// <summary>
    /// Writes the provided data stream to the specified output stream using GZip compression.
    /// </summary>
    /// <param name="data">The input stream containing the data to be compressed.</param>
    /// <param name="outputStream">The output stream to write the compressed data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Success value if the operation completes successfully.</returns>
    public Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<GZipStreamFormatter, Success>(async (_, _) =>
        {
            using GZipStream gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await data.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await gzipStream.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }

            await gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success;
        });
    }

}
