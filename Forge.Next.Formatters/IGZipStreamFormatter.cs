namespace Forge.Next.Formatters;

/// <summary>
/// GZip compression of a stream: an <see cref="IDataFormatter{T}"/> of <see cref="Stream"/> with a buffer size,
/// implemented by <see cref="GZipStreamFormatter"/>.
/// </summary>
public interface IGZipStreamFormatter : IDataFormatter<Stream>
{

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the IGZipStreamFormatter.
    /// </summary>
    int BufferSize { get; set; }

}
