using ErrorOr;
using Forge.Next.Shared;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Forge.Next.Formatters;

/// <summary>
/// Provides AES encryption and decryption for streams.
/// </summary>
public class AesStreamFormatter : CryptoFormatterBase<Stream>, IAesStreamFormatter
{

    /// <summary>
    /// Initializes a new instance of the <see cref="AesStreamFormatter"/> class.
    /// </summary>
    public AesStreamFormatter() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesStreamFormatter"/> class.
    /// </summary>
    /// <param name="certificate">The X509 certificate used for encryption and decryption.</param>
    public AesStreamFormatter(X509Certificate2 certificate) : base(certificate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesStreamFormatter"/> class.
    /// </summary>
    /// <param name="iv">The initialization vector (IV) used for AES encryption.</param>
    /// <param name="key">The key used for AES encryption.</param>
    public AesStreamFormatter(byte[] iv, byte[] key) : base(iv, key)
    {
    }

    /// <summary>
    /// Reads and decrypts data from the specified input stream asynchronously, returning the decrypted stream. The decryption is performed using AES encryption with the provided initialization vector (IV) and key. If the input stream is null, a validation error is returned. The method reads data from the input stream in chunks, decrypts it, and writes it to a memory stream, which is then returned as the result.
    /// </summary>
    /// <param name="inputStream">The input stream containing the encrypted data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the decrypted stream or an error.</returns>
    public override Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Stream?>)Error.Validation(description: nameof(inputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Stream?>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<AesStreamFormatter, Stream?>(async (_, _) =>
        {
            using Aes r = Aes.Create();
            r.IV = IV;
            r.Key = Key;

            using ICryptoTransform decryptor = r.CreateDecryptor();
            CryptoStream csDecrypt = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;
            MemoryStream ms = new MemoryStream();

            try
            {
                while ((numRead = await csDecrypt.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await ms.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                ms.SetLength(0);
                await ms.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return ms;
        });
    }

    /// <summary>
    /// Reads and decrypts data from the specified input stream asynchronously, writing the decrypted data to the provided output stream. The decryption is performed using AES encryption with the provided initialization vector (IV) and key. If either the input or output stream is null, a validation error is returned. The method reads data from the input stream in chunks, decrypts it, and writes it to the output stream until all data has been processed.
    /// </summary>
    /// <param name="inputStream">The input stream containing the encrypted data.</param>
    /// <param name="outputStream">The output stream to write the decrypted data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the result of the operation.</returns>
    public override Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(inputStream)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<AesStreamFormatter, Success>(async (_, _) =>
        {
            using Aes r = Aes.Create();
            r.IV = IV;
            r.Key = Key;

            using ICryptoTransform decryptor = r.CreateDecryptor();
            CryptoStream csDecrypt = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = csDecrypt.Read(buffer, 0, buffer.Length)) != 0)
            {
                await outputStream.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }

            return Result.Success;
        });
    }

    /// <summary>
    /// Writes and encrypts data from the specified input stream asynchronously, writing the encrypted data to the provided output stream. The encryption is performed using AES encryption with the provided initialization vector (IV) and key. If either the input or output stream is null, a validation error is returned. The method reads data from the input stream in chunks, encrypts it, and writes it to the output stream until all data has been processed.
    /// </summary>
    /// <param name="data">The input stream containing the data to be encrypted.</param>
    /// <param name="outputStream">The output stream to write the encrypted data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the result of the operation.</returns>
    public override Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<AesStreamFormatter, Success>(async (_, _) =>
        {
            using Aes r = Aes.Create();
            r.IV = IV;
            r.Key = Key;

            using ICryptoTransform encryptor = r.CreateEncryptor();
            CryptoStream csEncrypt = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await data.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await csEncrypt.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }
            await csEncrypt.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success;
        });
    }

}
