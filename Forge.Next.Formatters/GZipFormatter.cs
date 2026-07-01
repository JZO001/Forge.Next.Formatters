using ErrorOr;
using Forge.Next.Shared;
using System.IO.Compression;

namespace Forge.Next.Formatters;

/// <summary>
/// GZipFormatter is a class that implements the IGZipFormatter interface for reading and writing GZip-compressed data.
/// </summary>
public class GZipFormatter : IGZipFormatter
{

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the GZipFormatter. The default value is 8192 bytes.
    /// </summary>
    public int BufferSize { get; set; } = Consts.DefaultBufferSize;

    /// <summary>
    /// Reads GZip-compressed data from the provided input stream and returns the decompressed byte array.
    /// </summary>
    /// <param name="inputStream">The input stream containing GZip-compressed data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the decompressed byte array.</returns>
    public Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<byte[]?>)Error.Validation(description: nameof(inputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<byte[]?>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<GZipFormatter, byte[]?>(async (_, _) =>
        {
            using MemoryStream ms = new();
            using GZipStream gzipStream = new(inputStream, CompressionMode.Decompress, leaveOpen: true);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
                await ms.WriteAsync(buffer, 0, numRead, cancellationToken);
            }

            await gzipStream.FlushAsync(cancellationToken);

            return ms.ToArray();
        });
    }

    /// <summary>
    /// Reads GZip-compressed data from the provided input stream and writes the decompressed data to the specified output stream.
    /// </summary>
    /// <param name="inputStream">The input stream containing GZip-compressed data.</param>
    /// <param name="outputStream">The output stream to write the decompressed data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(inputStream)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<GZipFormatter, Success>(async (_, _) =>
        {
            using GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, true);
            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
                await outputStream.WriteAsync(buffer, 0, numRead, cancellationToken);
            }

            await gzipStream.FlushAsync(cancellationToken);

            return Result.Success;
        });
    }

    /// <summary>
    /// Writes the provided byte array to the specified output stream in GZip-compressed format.
    /// </summary>
    /// <param name="data">The byte array containing the data to be compressed and written.</param>
    /// <param name="outputStream">The output stream to write the compressed data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));

        return this.ProtectAsync<GZipFormatter, Success>(async (_, _) =>
        {
            using GZipStream gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true);
            await gzipStream.WriteAsync(data, 0, data.Length, cancellationToken);
            await gzipStream.FlushAsync(cancellationToken);
            return Result.Success;
        });
    }

}
