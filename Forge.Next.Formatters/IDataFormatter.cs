using ErrorOr;

namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IDataFormatter
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface IDataFormatter<T>
{

    /// <summary>
    /// Format the content of the inputStream
    /// </summary>
    /// <param name="inputStream">Content inputStream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the read operation</returns>
    Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore the content of the inputStream
    /// </summary>
    /// <param name="inputStream">Source inputStream</param>
    /// <param name="outputStream">Output inputStream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the read operation</returns>
    Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Format the provided object into the supplied inputStream
    /// </summary>
    /// <param name="data">Object that will be formatted</param>
    /// <param name="outputStream">Stream that the formatted data has been written</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the write operation</returns>
    Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default);

}
