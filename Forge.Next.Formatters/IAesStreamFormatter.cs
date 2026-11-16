namespace Forge.Next.Formatters;

/// <summary>
/// AES-256 encryption of a stream: an <see cref="ICryptoFormatter{T}"/> of <see cref="Stream"/>, implemented by
/// <see cref="AesStreamFormatter"/>.
/// </summary>
public interface IAesStreamFormatter : ICryptoFormatter<Stream>
{
}
