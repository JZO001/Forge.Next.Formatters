using System.Security.Cryptography.X509Certificates;

namespace Forge.Next.Formatters;

/// <summary>
/// Interface for ICryptoFormatter
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ICryptoFormatter<T> : IDataFormatter<T>
{

    /// <summary>
    /// Gets or sets the buffer size for the formatter.
    /// </summary>
    int BufferSize { get; set; }

    /// <summary>
    /// Gets or sets the certificate used for encryption/decryption.
    /// </summary>
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
