using ErrorOr;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Forge.Next.Formatters;

/// <summary>
/// Abstract base class for cryptographic formatters that implement the ICryptoFormatter interface. This class provides common functionality for reading and writing encrypted data using a specified initialization vector (IV) and encryption key. It also supports the use of an X509 certificate to derive the IV and key values from the public key of the certificate.
/// </summary>
/// <typeparam name="T">The type of the data: <c>byte[]</c> or <see cref="Stream"/>.</typeparam>
public abstract class CryptoFormatterBase<T> : ICryptoFormatter<T>
{

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private X509Certificate2? _certificate = null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private byte[] _IV = new byte[Consts.LengthOfIV];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private byte[] _key = new byte[Consts.LengthOfKey];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private AesOutputFormat _outputFormat = AesOutputFormat.Legacy;

    // The authenticated format (AesOutputFormat.Authenticated, since 3.4.0): the header "FNF" 0x02, a random IV, the
    // AES-256-CBC-PKCS7 ciphertext, and an HMAC-SHA256 tag over all of them. The encryption and the authentication key
    // are HMAC-SHA256 of Key over the two labels. Changing any of these makes stored data unreadable.
    private static readonly byte[] AuthenticatedHeader = { 0x46, 0x4E, 0x46, 0x02 };
    private static readonly byte[] EncryptionKeyLabel = Encoding.ASCII.GetBytes("Forge.Next.Formatters.Aes.Encryption");
    private static readonly byte[] AuthenticationKeyLabel = Encoding.ASCII.GetBytes("Forge.Next.Formatters.Aes.Authentication");
    private const int AuthenticationTagLength = 32;
    private const int AesBlockLength = 16;

    /// <summary>
    /// Gets or sets the buffer size used for reading and writing data in the formatter. The default value is defined in Consts.DefaultBufferSize.
    /// </summary>
    public int BufferSize { get; set; } = Consts.DefaultBufferSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoFormatterBase{T}"/> class with a random IV and key from a
    /// cryptographic random number generator (<see cref="RandomNumberGenerator"/>), so every instance gets its own,
    /// unpredictable values.
    /// </summary>
    protected CryptoFormatterBase()
    {
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(_IV);
        rng.GetBytes(_key);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoFormatterBase{T}"/> class with the specified X509 certificate.
    /// </summary>
    /// <param name="certificate">The X509 certificate to use for encryption and decryption.</param>
    /// <exception cref="ArgumentNullException">Thrown when the certificate is null.</exception>
    /// <remarks>Obsolete since 3.4.0: the IV and the key come from the certificate's public key, so anyone who has
    /// the certificate can compute them. It is kept, unchanged, so data written this way stays readable.</remarks>
    [Obsolete("Derives the IV and the key from the certificate's public key, so anyone who has the certificate can decrypt. Use a secret IV and Key of your own; keep this only to read data written this way.")]
    protected CryptoFormatterBase(X509Certificate2 certificate)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));

        Certificate = certificate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoFormatterBase{T}"/> class with the specified IV and key values.
    /// </summary>
    /// <param name="iv">The 16-byte initialization vector.</param>
    /// <param name="key">The 32-byte (AES-256) key.</param>
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
    /// <remarks>Obsolete since 3.4.0: the IV and the key come from the certificate's public key, so anyone who has
    /// the certificate can compute them. It is kept, unchanged, so data written this way stays readable.</remarks>
    [Obsolete("Derives the IV and the key from the certificate's public key, so anyone who has the certificate can decrypt. Use a secret IV and Key of your own; keep this only to read data written this way.")]
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
    /// Gets or sets the format that <c>WriteAsync</c> writes (since 3.4.0). The default,
    /// <see cref="AesOutputFormat.Legacy"/>, writes the format of the earlier versions;
    /// <see cref="AesOutputFormat.Authenticated"/> writes the authenticated format, with a random IV per message.
    /// <c>ReadAsync</c> recognizes either format by its header, whatever this property is.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="AesOutputFormat"/>.</exception>
    public AesOutputFormat OutputFormat
    {
        get { return _outputFormat; }
        set
        {
            if (value != AesOutputFormat.Legacy && value != AesOutputFormat.Authenticated) throw new ArgumentOutOfRangeException(nameof(value));

            _outputFormat = value;
        }
    }

    /// <summary>
    /// Gets or sets whether <c>ReadAsync</c> accepts input in the <see cref="AesOutputFormat.Legacy"/> format, which is
    /// not authenticated (since 3.4.0). The default is <c>true</c>, so data written by earlier versions stays readable.
    /// Set it to <c>false</c> once all data is in the <see cref="AesOutputFormat.Authenticated"/> format: input without a
    /// valid authentication tag then gives an error.
    /// </summary>
    public bool AcceptUnauthenticated { get; set; } = true;

    /// <summary>
    /// Reads and decrypts data from the specified input stream and returns the deserialized object of type T. The method uses the configured IV and key values for decryption. If a certificate is set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    /// <param name="inputStream">The input stream to read data from.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains an ErrorOr&lt;T?&gt; representing the deserialized object or an error.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public abstract Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and decrypts data from the specified input stream and writes the decrypted data to the specified output stream. The method uses the configured IV and key values for decryption. If a certificate is set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    /// <param name="inputStream">The input stream to read data from.</param>
    /// <param name="outputStream">The output stream to write the decrypted data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains an ErrorOr&lt;Success&gt; indicating the success or failure of the operation.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public abstract Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes and encrypts the specified data to the specified output stream. The method uses the configured IV and key values for encryption. If a certificate is set, the IV and key values are derived from the public key of the certificate.
    /// </summary>
    /// <param name="data">The data to write and encrypt.</param>
    /// <param name="outputStream">The output stream to write the encrypted data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains an ErrorOr&lt;Success&gt; indicating the success or failure of the operation.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public abstract Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts the rest of <paramref name="plaintext"/> in the <see cref="AesOutputFormat.Authenticated"/> format and
    /// writes the message to <paramref name="outputStream"/>. The ciphertext is built in memory first, because the tag
    /// covers all of it, so nothing is written to the output before the encryption has finished.
    /// </summary>
    /// <param name="plaintext">The data to encrypt, read from its current position.</param>
    /// <param name="outputStream">The stream the message is written to; it stays open.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the message is written.</returns>
    private protected async Task WriteAuthenticatedAsync(Stream plaintext, Stream outputStream, CancellationToken cancellationToken)
    {
        byte[] iv = new byte[Consts.LengthOfIV];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }

        byte[] encryptionKey = DeriveKey(EncryptionKeyLabel);
        byte[] authenticationKey = DeriveKey(AuthenticationKeyLabel);

        try
        {
            using MemoryStream ciphertext = new MemoryStream();

            using (Aes aes = CreateAuthenticatedAes(encryptionKey, iv))
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            {
                CryptoStream csEncrypt = new CryptoStream(ciphertext, encryptor, CryptoStreamMode.Write);

                byte[] buffer = new byte[BufferSize];
                int numRead = 0;

                while ((numRead = await plaintext.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await csEncrypt.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
                }
#if NETCOREAPP
                await csEncrypt.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
#else
                csEncrypt.FlushFinalBlock();
#endif
            }

            byte[] tag;
            using (HMACSHA256 hmac = new HMACSHA256(authenticationKey))
            {
                hmac.TransformBlock(AuthenticatedHeader, 0, AuthenticatedHeader.Length, null, 0);
                hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                hmac.TransformFinalBlock(ciphertext.GetBuffer(), 0, (int)ciphertext.Length);
                tag = hmac.Hash!;
            }

            await outputStream.WriteAsync(AuthenticatedHeader, 0, AuthenticatedHeader.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(ciphertext.GetBuffer(), 0, (int)ciphertext.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(tag, 0, tag.Length, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(encryptionKey, 0, encryptionKey.Length);
            Array.Clear(authenticationKey, 0, authenticationKey.Length);
        }
    }

    /// <summary>
    /// Opens the encrypted input of a read and decides its format by the header.
    /// <list type="bullet">
    ///   <item>No header: the <see cref="AesOutputFormat.Legacy"/> format, refused unless
    ///   <see cref="AcceptUnauthenticated"/> is <c>true</c>.</item>
    ///   <item>The header: the whole message is read into memory, and its tag checked in constant time before anything
    ///   is decrypted. A wrong tag is refused, unless the current format is accepted: then the message is read the
    ///   <see cref="AesOutputFormat.Legacy"/> way, in memory, because a message of that format starts with the header
    ///   bytes by chance once in 2^32.</item>
    /// </list>
    /// A stream that cannot seek is read into memory first, so its first bytes can be looked at and read again.
    /// </summary>
    /// <param name="inputStream">The encrypted input, read from its current position.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// Either the plaintext, when the input was decrypted in memory, or the stream to decrypt the
    /// <see cref="AesOutputFormat.Legacy"/> way, at the position where the input started: <paramref name="inputStream"/>
    /// itself, or the in-memory copy of it, which the caller disposes.
    /// </returns>
    /// <exception cref="CryptographicException">The input is refused (see above).</exception>
    private protected async Task<(byte[]? Plaintext, Stream? Legacy)> OpenAsync(Stream inputStream, CancellationToken cancellationToken)
    {
        // As the CryptoStream constructor did before the input was looked at first.
        if (!inputStream.CanRead) throw new ArgumentException("The input stream is not readable.", nameof(inputStream));

        Stream source = inputStream;
        bool disposeSource = false;

        if (!inputStream.CanSeek)
        {
            MemoryStream copy = new MemoryStream();
            source = copy;
            disposeSource = true;
            await CopyAsync(inputStream, copy, cancellationToken).ConfigureAwait(false);
            copy.Position = 0;
        }

        try
        {
            long start = source.Position;
            byte[] head = new byte[AuthenticatedHeader.Length];
            int headLength = await ReadBlockAsync(source, head, cancellationToken).ConfigureAwait(false);
            source.Position = start;

            if (headLength != head.Length || !FixedTimeEquals(AuthenticatedHeader, head, 0))
            {
                if (!AcceptUnauthenticated) throw new CryptographicException("The input is not in the authenticated AES format, and AcceptUnauthenticated is false.");

                disposeSource = false;
                return (null, source);
            }

            byte[] message;
            using (MemoryStream all = new MemoryStream())
            {
                await CopyAsync(source, all, cancellationToken).ConfigureAwait(false);
                message = all.ToArray();
            }

            byte[]? plaintext = OpenAuthenticated(message);
            if (plaintext is not null) return (plaintext, null);

            if (!AcceptUnauthenticated) throw new CryptographicException("The authentication tag of the AES input does not match.");

            return (DecryptLegacy(message), null);
        }
        finally
        {
            if (disposeSource) source.Dispose();
        }
    }

    /// <summary>
    /// Checks the tag of a message in the <see cref="AesOutputFormat.Authenticated"/> format and decrypts it.
    /// </summary>
    /// <param name="message">The whole message, header included.</param>
    /// <returns>The plaintext, or <c>null</c> when the message is too short or its tag does not match.</returns>
    private byte[]? OpenAuthenticated(byte[] message)
    {
        int headerAndIvLength = AuthenticatedHeader.Length + Consts.LengthOfIV;
        int ciphertextLength = message.Length - headerAndIvLength - AuthenticationTagLength;
        if (ciphertextLength < AesBlockLength || ciphertextLength % AesBlockLength != 0) return null;

        byte[] encryptionKey = DeriveKey(EncryptionKeyLabel);
        byte[] authenticationKey = DeriveKey(AuthenticationKeyLabel);

        try
        {
            byte[] expectedTag;
            using (HMACSHA256 hmac = new HMACSHA256(authenticationKey))
            {
                expectedTag = hmac.ComputeHash(message, 0, message.Length - AuthenticationTagLength);
            }

            if (!FixedTimeEquals(expectedTag, message, message.Length - AuthenticationTagLength)) return null;

            byte[] iv = new byte[Consts.LengthOfIV];
            Buffer.BlockCopy(message, AuthenticatedHeader.Length, iv, 0, iv.Length);

            using Aes aes = CreateAuthenticatedAes(encryptionKey, iv);
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(message, headerAndIvLength, ciphertextLength);
        }
        finally
        {
            Array.Clear(encryptionKey, 0, encryptionKey.Length);
            Array.Clear(authenticationKey, 0, authenticationKey.Length);
        }
    }

    /// <summary>
    /// Decrypts a whole message of the <see cref="AesOutputFormat.Legacy"/> format in memory, with <see cref="IV"/> and
    /// <see cref="Key"/>, as the formatters do while streaming.
    /// </summary>
    /// <param name="message">The ciphertext.</param>
    /// <returns>The plaintext.</returns>
    private byte[] DecryptLegacy(byte[] message)
    {
        using Aes aes = Aes.Create();
        aes.IV = IV;
        aes.Key = Key;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(message, 0, message.Length);
    }

    /// <summary>
    /// Derives a key of the <see cref="AesOutputFormat.Authenticated"/> format: HMAC-SHA256 of <see cref="Key"/> over
    /// <paramref name="label"/>.
    /// </summary>
    /// <param name="label">The fixed label of the key.</param>
    /// <returns>The 32-byte key; the caller clears it after use.</returns>
    private byte[] DeriveKey(byte[] label)
    {
        using HMACSHA256 hmac = new HMACSHA256(Key);
        return hmac.ComputeHash(label);
    }

    /// <summary>
    /// Creates the AES-256-CBC instance with PKCS7 padding of the <see cref="AesOutputFormat.Authenticated"/> format.
    /// </summary>
    /// <param name="key">The derived encryption key.</param>
    /// <param name="iv">The IV of the message.</param>
    /// <returns>The configured instance; the caller disposes it.</returns>
    private static Aes CreateAuthenticatedAes(byte[] key, byte[] iv)
    {
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        return aes;
    }

    /// <summary>
    /// Compares <paramref name="expected"/> with the bytes of <paramref name="actual"/> from
    /// <paramref name="actualOffset"/> in a time that does not depend on where they differ.
    /// </summary>
    /// <param name="expected">The expected bytes.</param>
    /// <param name="actual">The array holding the bytes to compare.</param>
    /// <param name="actualOffset">Where the bytes to compare start in <paramref name="actual"/>.</param>
    /// <returns><c>true</c> when all bytes are equal.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool FixedTimeEquals(byte[] expected, byte[] actual, int actualOffset)
    {
        int difference = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            difference |= expected[i] ^ actual[actualOffset + i];
        }

        return difference == 0;
    }

    /// <summary>
    /// Copies the rest of <paramref name="source"/> to <paramref name="destination"/> in <see cref="BufferSize"/> chunks.
    /// </summary>
    /// <param name="source">The stream to copy from.</param>
    /// <param name="destination">The stream to copy to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the copy is done.</returns>
    private async Task CopyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferSize];
        int numRead = 0;

        while ((numRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads until <paramref name="buffer"/> is full or the stream ends.
    /// </summary>
    /// <param name="source">The stream to read.</param>
    /// <param name="buffer">The buffer to fill.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of bytes read.</returns>
    private static async Task<int> ReadBlockAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        int numRead = 0;

        while (total < buffer.Length && (numRead = await source.ReadAsync(buffer, total, buffer.Length - total, cancellationToken).ConfigureAwait(false)) != 0)
        {
            total += numRead;
        }

        return total;
    }

}
