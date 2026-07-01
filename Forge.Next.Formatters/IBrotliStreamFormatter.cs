namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IBrotliStreamFormatter
/// </summary>
public interface IBrotliStreamFormatter : IDataFormatter<Stream>
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    int BufferSize { get; set; }

}
