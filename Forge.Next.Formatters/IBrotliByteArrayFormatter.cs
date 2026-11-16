namespace Forge.Next.Formatters;

/// <summary>
/// Brotli compression of a byte array: an <see cref="IDataFormatter{T}"/> of <c>byte[]</c> with a buffer size,
/// implemented by <c>BrotliByteArrayFormatter</c> on the .NET targets; <c>net48</c> has no implementation.
/// </summary>
public interface IBrotliByteArrayFormatter : IDataFormatter<byte[]>
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    int BufferSize { get; set; }

}
