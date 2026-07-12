using ErrorOr;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Forge.Next.Formatters;

/// <summary>
/// Abstract base class for cryptographic formatters that implement the ICryptoFormatter interface. This class provides common functionality for reading and writing encrypted data using a specified initialization vector (IV) and encryption key. It also supports the use of an X509 certificate to derive the IV and key values from the public key of the certificate.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class CryptoFormatterBase<T> : ICryptoFormatter<T>
{

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private X509Certificate2? _certificate = null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private byte[] _IV = new byte[Consts.LengthOfIV];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private byte[] _key = new byte[Consts.LengthOfKey];

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the formatter. The default value is defined in Consts.DefaultBufferSize.
    /// </summary>
    public int BufferSize { get; set; } = Consts.DefaultBufferSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoFormatterBase{T}"/> class with random IV and key values.
    /// </summary>
    protected CryptoFormatterBase()
    {
#if NETCOREAPP
        Random.Shared.NextBytes(_IV);
        Random.Shared.NextBytes(_key);
#else
        Random random = new Random();
        random.NextBytes(_IV);
        random.NextBytes(_key);
#endif
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoFormatterBase{T}"/> class with the specified X509 certificate.
    /// </summary>
    /// <param name="certificate">The X509 certificate to use for encryption and decryption.</param>
    /// <exception cref="ArgumentNullException">Thrown when the certificate is null.</exception>
    protected CryptoFormatterBase(X509Certificate2 certificate)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));

        Certificate = certificate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoFormatterBase{T}"/> class with the specified IV and key values.
    /// </summary>
    /// <param name="iv"></param>
    /// <param name="key"></param>
    /// <exception cref="ArgumentNullException">Thrown when either iv or key is null.</exception>
    protected CryptoFormatterBase(byte[] iv, byte[] key)
    {
        if (iv is null) throw new ArgumentNullException(nameof(iv));
        if (key is null) throw new ArgumentNullException(nameof(key));

        IV = iv;
        Key = key;
    }

    /// <summary>
    /// Gets or sets the X509 certificate used for encryption and decryption. When set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    public X509Certificate2? Certificate
    {
        get { return _certificate; }
        set
        {
            _certificate = value;

            if (value is null) return;

            Buffer.BlockCopy(value.PublicKey.EncodedKeyValue.RawData, 0, _IV, 0, _IV.Length);
            Buffer.BlockCopy(value.PublicKey.EncodedKeyValue.RawData, value.PublicKey.EncodedKeyValue.RawData.Length - _key.Length, _key, 0, _key.Length);
        }
    }

    /// <summary>
    /// Gets or sets the initialization vector (IV) used for encryption and decryption. The IV must be of the specified length defined in Consts.LengthOfIV.
    /// </summary>
    public byte[] IV
    {
        get { return _IV; }
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            if (value.Length != Consts.LengthOfIV) throw new InvalidDataException();

            _IV = value;
        }
    }

    /// <summary>
    /// Gets or sets the encryption key used for encryption and decryption. The key must be of the specified length defined in Consts.LengthOfKey.
    /// </summary>
    public byte[] Key
    {
        get { return _key; }
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            if (value.Length != Consts.LengthOfKey) throw new InvalidDataException();

            _key = value;
        }
    }

    /// <summary>
    /// Reads and decrypts data from the specified input stream and returns the deserialized object of type T. The method uses the configured IV and key values for decryption. If a certificate is set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    /// <param name="inputStream">The input stream to read data from.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains an ErrorOr&lt;T?&gt; representing the deserialized object or an error.</returns>
    public abstract Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and decrypts data from the specified input stream and writes the decrypted data to the specified output stream. The method uses the configured IV and key values for decryption. If a certificate is set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    /// <param name="inputStream">The input stream to read data from.</param>
    /// <param name="outputStream">The output stream to write the decrypted data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains an ErrorOr&lt;Success&gt; indicating the success or failure of the operation.</returns>
    public abstract Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes and encrypts the specified data to the specified output stream. The method uses the configured IV and key values for encryption. If a certificate is set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    /// <param name="data">The data to write and encrypt.</param>
    /// <param name="outputStream">The output stream to write the encrypted data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains an ErrorOr&lt;Success&gt; indicating the success or failure of the operation.</returns>
    public abstract Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default);

}
