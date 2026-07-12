using ErrorOr;
using Forge.Next.Shared;

namespace Forge.Next.Formatters;

/// <summary>
/// Extension methods for IDataFormatter
/// </summary>
public static class DataFormatterExtensions
{

    /// <summary>
    /// Reads data from a file using the specified formatter, with an option to decompress the data if needed.
    /// </summary>
    /// <typeparam name="T">The type of the data to be read.</typeparam>
    /// <param name="formatter">The data formatter to use for reading the data.</param>
    /// <param name="file">The file containing the data.</param>
    /// <param name="decompress">A flag indicating whether the data should be decompressed. Default is true.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the read data or an error.</returns>
    public static Task<ErrorOr<T?>> ReadAsync<T>(
        this IDataFormatter<T> formatter,
        FileInfo file,
        bool decompress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(formatter)));
        if (file is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(file)));

        return typeof(DataFormatterExtensions).ProtectAsync(async (_, _) =>
        {
            using FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

            return await ReadAsync(formatter, fs, decompress, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Reads data from a stream using the specified formatter, with an option to decompress the data if needed.
    /// </summary>
    /// <typeparam name="T">The type of the data to be read.</typeparam>
    /// <param name="formatter">The data formatter to use for reading the data.</param>
    /// <param name="stream">The input stream containing the data.</param>
    /// <param name="decompress">A flag indicating whether the data should be decompressed. Default is true.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the read data or an error.</returns>
    public static async Task<ErrorOr<T?>> ReadAsync<T>(
        this IDataFormatter<T> formatter,
        Stream stream,
        bool decompress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Error.Validation(description: nameof(formatter));
        if (stream is null) return Error.Validation(description: nameof(stream));

        if (decompress)
        {
            ErrorOr<Stream?> decompressed = await new GZipStreamFormatter().ReadAsync(stream, cancellationToken).ConfigureAwait(false);

            if (decompressed.IsError) return decompressed.Errors;

            // The decompressed stream is ours, so it is ours to dispose once the formatter has read it.
            using Stream decompressedStream = decompressed.Value!;

            return await formatter.ReadAsync(decompressedStream, cancellationToken).ConfigureAwait(false);
        }

        return await formatter.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes data to a file using the specified formatter, with an option to compress the data if needed.
    /// </summary>
    /// <typeparam name="T">The type of the data to be written.</typeparam>
    /// <param name="formatter">The data formatter to use for writing the data.</param>
    /// <param name="data">The data to be written.</param>
    /// <param name="file">The file to write the data to.</param>
    /// <param name="compress">A flag indicating whether the data should be compressed. Default is true.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a success indicator or an error.</returns>
    public static Task<ErrorOr<Success>> WriteAsync<T>(
        this IDataFormatter<T> formatter,
        T data,
        FileInfo file,
        bool compress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(formatter)));
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (file is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(file)));

        return typeof(DataFormatterExtensions).ProtectAsync(async (_, _) =>
        {
            using FileStream fs = new FileStream(file.FullName, FileMode.Create, FileAccess.Write, FileShare.Read);

            return await WriteAsync(formatter, data, fs, compress, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Writes data to a stream using the specified formatter, with an option to compress the data if needed.
    /// </summary>
    /// <typeparam name="T">The type of the data to be written.</typeparam>
    /// <param name="formatter">The data formatter to use for writing the data.</param>
    /// <param name="data">The data to be written.</param>
    /// <param name="stream">The stream to write the data to.</param>
    /// <param name="compress">A flag indicating whether the data should be compressed. Default is true.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a success indicator or an error.</returns>
    public static async Task<ErrorOr<Success>> WriteAsync<T>(
        this IDataFormatter<T> formatter,
        T data,
        Stream stream,
        bool compress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Error.Validation(description: nameof(formatter));
        if (data is null) return Error.Validation(description: nameof(data));
        if (stream is null) return Error.Validation(description: nameof(stream));

        if (compress)
        {
            // Must stay awaited inside the using: a task chain built here would let ms be disposed
            // before the GZip layer ever reads from it.
            using MemoryStream ms = new MemoryStream();

            ErrorOr<Success> serialized = await formatter.WriteAsync(data, ms, cancellationToken).ConfigureAwait(false);

            if (serialized.IsError) return serialized;

            ms.Position = 0;

            return await new GZipStreamFormatter().WriteAsync(ms, stream, cancellationToken).ConfigureAwait(false);
        }

        return await formatter.WriteAsync(data, stream, cancellationToken).ConfigureAwait(false);
    }

}
