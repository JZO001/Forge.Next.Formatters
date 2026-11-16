namespace Forge.Next.Formatters;

/// <summary>
/// AES-256 encryption of a byte array: an <see cref="ICryptoFormatter{T}"/> of <c>byte[]</c>, implemented by
/// <see cref="AesByteArrayFormatter"/>.
/// </summary>
public interface IAesByteArrayFormatter : ICryptoFormatter<byte[]>
{
}
