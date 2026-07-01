using ErrorOr;
using Forge.Next.Shared;
using System.Text.Json;

namespace Forge.Next.Formatters;

/// <summary>
/// Implementation of ISystemJsonFormatter
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public class SystemJsonFormatter<T> : ISystemJsonFormatter<T>
{

    /// <summary>
    /// Gets or sets the serializer options for the formatter.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = JsonSerializerOptions.Default;

    /// <summary>
    /// Reads and deserializes an object of type T from the provided input stream asynchronously.
    /// </summary>
    /// <param name="inputStream">The input stream containing the JSON data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the deserialized object of type T.</returns>
    public Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        return this.ProtectAsync<SystemJsonFormatter<T>, T?>(async (_, _) =>
        {
            return await JsonSerializer.DeserializeAsync<T>(inputStream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Reads and deserializes an object of type T from the provided input stream and writes it to the specified output stream asynchronously. This method is not implemented and will return a forbidden error.
    /// </summary>
    /// <param name="inputStream">The input stream containing the JSON data.</param>
    /// <param name="outputStream">The output stream where the JSON data should be written.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read and write operation. The task result contains a Success or an Error.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((ErrorOr<Success>)Error.Forbidden(description: "Method not implemented."));
    }

    /// <summary>
    /// Serializes an object of type T and writes it to the provided output stream asynchronously.
    /// </summary>
    /// <param name="data">The object of type T to be serialized.</param>
    /// <param name="outputStream">The output stream where the JSON data should be written.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a Success or an Error.</returns>
    public Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));

        return this.ProtectAsync<SystemJsonFormatter<T>, Success>(async (_, _) =>
        {
            await JsonSerializer.SerializeAsync(outputStream, data, SerializerOptions, cancellationToken).ConfigureAwait(false);

            return Result.Success;
        });
    }

}
