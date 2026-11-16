namespace Forge.Next.Formatters;

/// <summary>
/// GZip compression of a byte array: an <see cref="IDataFormatter{T}"/> of <c>byte[]</c> with a buffer size,
/// implemented by <see cref="GZipByteArrayFormatter"/>.
/// </summary>
public interface IGZipByteArrayFormatter : IDataFormatter<byte[]>
{

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the IGZipByteArrayFormatter.
    /// </summary>
    int BufferSize { get; set; }

}
