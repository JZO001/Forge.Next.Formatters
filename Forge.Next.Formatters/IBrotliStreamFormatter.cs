namespace Forge.Next.Formatters;

/// <summary>
/// Brotli compression of a stream: an <see cref="IDataFormatter{T}"/> of <see cref="Stream"/> with a buffer size,
/// implemented by <c>BrotliStreamFormatter</c> on the .NET targets; <c>net48</c> has no implementation.
/// </summary>
public interface IBrotliStreamFormatter : IDataFormatter<Stream>
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    int BufferSize { get; set; }

}
