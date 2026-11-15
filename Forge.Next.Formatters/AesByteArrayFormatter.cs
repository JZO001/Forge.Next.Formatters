using ErrorOr;
using Forge.Next.Shared;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Forge.Next.Formatters;

/// <summary>
/// Interface for AesByteArrayFormatter
/// </summary>
public class AesByteArrayFormatter : CryptoFormatterBase<byte[]>, IAesByteArrayFormatter
{

    /// <summary>
    /// Initializes a new instance of the <see cref="AesByteArrayFormatter"/> class.
    /// </summary>
    public AesByteArrayFormatter() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesByteArrayFormatter"/> class.
    /// </summary>
    /// <param name="certificate">The X509 certificate used for encryption and decryption.</param>
    /// <remarks>Obsolete since 3.4.0: the IV and the key come from the certificate's public key, so anyone who has
    /// the certificate can compute them. It is kept, unchanged, so data written this way stays readable.</remarks>
    [Obsolete("Derives the IV and the key from the certificate's public key, so anyone who has the certificate can decrypt. Use a secret IV and Key of your own; keep this only to read data written this way.")]
    public AesByteArrayFormatter(X509Certificate2 certificate) : base(certificate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesByteArrayFormatter"/> class.
    /// </summary>
    /// <param name="iv">The initialization vector (IV) used for AES encryption.</param>
    /// <param name="key">The key used for AES encryption.</param>
    public AesByteArrayFormatter(byte[] iv, byte[] key) : base(iv, key)
    {
    }

    /// <summary>
    /// Reads and decrypts data from the specified input stream asynchronously, returning the decrypted byte array. The decryption is performed using AES encryption with the provided initialization vector (IV) and key. If the input stream is null, a validation error is returned. The method uses a memory stream to accumulate the decrypted data before returning it as a byte array.
    /// </summary>
    /// <param name="inputStream">The input stream containing the encrypted data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the decrypted byte array.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public override Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<byte[]?>)Error.Validation(description: nameof(inputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<byte[]?>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<AesByteArrayFormatter, byte[]?>(async (_, _) =>
        {
            (byte[]? plaintext, Stream? legacy) = await OpenAsync(inputStream, cancellationToken).ConfigureAwait(false);
            if (plaintext is not null) return plaintext;

            try
            {
                using Aes r = Aes.Create();
                r.IV = IV;
                r.Key = Key;

                using ICryptoTransform decryptor = r.CreateDecryptor();
                CryptoStream csDecrypt = new CryptoStream(legacy!, decryptor, CryptoStreamMode.Read);

                using MemoryStream ms = new MemoryStream();
                byte[] buffer = new byte[BufferSize];
                int numRead = 0;

                while ((numRead = await csDecrypt.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await ms.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
                }

                return ms.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(legacy, inputStream)) legacy!.Dispose();
            }
        }, rethrowCancellation: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Reads and decrypts data from the specified input stream asynchronously, writing the decrypted data to the provided output stream. The decryption is performed using AES encryption with the provided initialization vector (IV) and key. If either the input or output stream is null, a validation error is returned. The method reads data from the input stream in chunks, decrypts it, and writes it to the output stream until all data has been processed.
    /// </summary>
    /// <param name="inputStream">The input stream containing the encrypted data.</param>
    /// <param name="outputStream">The output stream to write the decrypted data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains a success indicator.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public override Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(inputStream)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<AesByteArrayFormatter, Success>(async (_, _) =>
        {
            (byte[]? plaintext, Stream? legacy) = await OpenAsync(inputStream, cancellationToken).ConfigureAwait(false);
            if (plaintext is not null)
            {
                await outputStream.WriteAsync(plaintext, 0, plaintext.Length, cancellationToken).ConfigureAwait(false);
                return Result.Success;
            }

            try
            {
                using Aes r = Aes.Create();
                r.IV = IV;
                r.Key = Key;

                using ICryptoTransform decryptor = r.CreateDecryptor();
                CryptoStream csDecrypt = new CryptoStream(legacy!, decryptor, CryptoStreamMode.Read);

                byte[] buffer = new byte[BufferSize];
                int numRead = 0;

                while ((numRead = await csDecrypt.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await outputStream.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
                }

                return Result.Success;
            }
            finally
            {
                if (!ReferenceEquals(legacy, inputStream)) legacy!.Dispose();
            }
        }, rethrowCancellation: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Writes and encrypts the specified byte array data to the provided output stream asynchronously. The encryption is performed using AES encryption with the configured initialization vector (IV) and key. If either the data or output stream is null, a validation error is returned. The method uses a memory stream to read the input data in chunks, encrypts it, and writes the encrypted data to the output stream until all data has been processed.
    /// </summary>
    /// <param name="data">The byte array containing the data to be encrypted.</param>
    /// <param name="outputStream">The output stream to write the encrypted data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation. The task result contains a success indicator.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public override Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));
        if (BufferSize <= 0) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(BufferSize)));

        return this.ProtectAsync<AesByteArrayFormatter, Success>(async (_, _) =>
        {
            using MemoryStream ms = new MemoryStream(data);
            ms.Position = 0;

            if (OutputFormat == AesOutputFormat.Authenticated)
            {
                await WriteAuthenticatedAsync(ms, outputStream, cancellationToken).ConfigureAwait(false);
                return Result.Success;
            }

            using Aes r = Aes.Create();
            r.IV = IV;
            r.Key = Key;

            using ICryptoTransform encryptor = r.CreateEncryptor();
            CryptoStream csEncrypt = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);

            byte[] buffer = new byte[BufferSize];
            int numRead = 0;

            while ((numRead = await ms.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await csEncrypt.WriteAsync(buffer, 0, numRead, cancellationToken).ConfigureAwait(false);
            }
#if NETCOREAPP
            await csEncrypt.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
#else
            csEncrypt.FlushFinalBlock();
#endif

            ms.SetLength(0);

            return Result.Success;
        }, rethrowCancellation: true, cancellationToken: cancellationToken);
    }

}
