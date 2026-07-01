namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IGZipStreamFormatter
/// </summary>
public interface IGZipStreamFormatter : IDataFormatter<Stream>
{

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the IGZipStreamFormatter.
    /// </summary>
    int BufferSize { get; set; }

}
