using System.Security.Cryptography.X509Certificates;

namespace Forge.Next.Formatters;

/// <summary>
/// Interface for ICryptoFormatter
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface ICryptoFormatter<T> : IDataFormatter<T>
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    int BufferSize { get; set; }

    /// <summary>
    /// Gets or sets the certificate used for encryption/decryption.
    /// </summary>
    /// <remarks>Obsolete since 3.4.0: the IV and the key come from the certificate's public key, so anyone who has
    /// the certificate can compute them. It is kept, unchanged, so data written this way stays readable.</remarks>
    [Obsolete("Derives the IV and the key from the certificate's public key, so anyone who has the certificate can decrypt. Use a secret IV and Key of your own; keep this only to read data written this way.")]
    X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Gets or sets the initialization vector (IV) used for encryption/decryption.
    /// </summary>
    byte[] IV { get; set; }

    /// <summary>
    /// Gets or sets the encryption key used for encryption/decryption.
    /// </summary>
    byte[] Key { get; set; }

}
