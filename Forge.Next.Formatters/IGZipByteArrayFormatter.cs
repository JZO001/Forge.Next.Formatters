namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IGZipFormatter
/// </summary>
public interface IGZipByteArrayFormatter : IDataFormatter<byte[]>
{

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the IGZipByteArrayFormatter.
    /// </summary>
    int BufferSize { get; set; }

}
