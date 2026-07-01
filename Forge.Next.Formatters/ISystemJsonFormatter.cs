using System.Text.Json;

namespace Forge.Next.Formatters;

/// <summary>
/// Interface for ISystemJsonFormatter
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface ISystemJsonFormatter<T> : IDataFormatter<T>
{

    /// <summary>
    /// Gets or sets the serializer options for the formatter.
    /// </summary>
    JsonSerializerOptions SerializerOptions { get; set; }

}
