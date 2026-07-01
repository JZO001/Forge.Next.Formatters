namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IBrotliByteArrayFormatter
/// </summary>
public interface IBrotliByteArrayFormatter : IDataFormatter<byte[]>
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    int BufferSize { get; set; }

}
