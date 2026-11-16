using System.Text.Json;

namespace Forge.Next.Formatters;

/// <summary>
/// JSON serialization with <c>System.Text.Json</c>: an <see cref="IDataFormatter{T}"/> with the serializer options,
/// implemented by <see cref="SystemJsonFormatter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface ISystemJsonFormatter<T> : IDataFormatter<T>
{

    /// <summary>
    /// Gets or sets the serializer options for the formatter.
    /// </summary>
    JsonSerializerOptions SerializerOptions { get; set; }

}
