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
    public static Task<ErrorOr<T?>> Read<T>(
        this IDataFormatter<T> formatter,
        FileInfo file,
        bool decompress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(formatter)));
        if (file is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(file)));

        return typeof(DataFormatterExtensions).ProtectAsync((_, _) =>
        {
            using FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

            return Read(formatter, fs, decompress, cancellationToken);
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
    public static Task<ErrorOr<T?>> Read<T>(
        this IDataFormatter<T> formatter,
        Stream stream,
        bool decompress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(formatter)));
        if (stream is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(stream)));

        if (decompress)
        {
            return new GZipStreamFormatter()
                .ReadAsync(stream, cancellationToken)
                .ThenAsync(decompressedStream => formatter.ReadAsync(decompressedStream!, cancellationToken));
        }

        return formatter.ReadAsync(stream, cancellationToken);
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
    public static Task<ErrorOr<Success>> Write<T>(
        this IDataFormatter<T> formatter,
        T data,
        FileInfo file,
        bool compress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(formatter)));
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (file is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(file)));

        return typeof(DataFormatterExtensions).ProtectAsync((_, _) =>
        {
            using FileStream fs = new FileStream(file.FullName, FileMode.Create, FileAccess.Write, FileShare.Read);

            return Write(formatter, data, fs, compress, cancellationToken);
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
    public static Task<ErrorOr<Success>> Write<T>(
        this IDataFormatter<T> formatter,
        T data,
        Stream stream,
        bool compress = true,
        CancellationToken cancellationToken = default)
    {
        if (formatter is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(formatter)));
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (stream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(stream)));

        if (compress)
        {
            using MemoryStream ms = new MemoryStream();

            return formatter
                .WriteAsync(data, ms, cancellationToken)
                .ThenDo(_ => ms.Position = 0)
                .ThenAsync(_ => new GZipStreamFormatter().WriteAsync(ms, stream, cancellationToken));
        }

        return formatter.WriteAsync(data, stream, cancellationToken);
    }

}
