using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="AesStreamFormatter"/>, the AES formatter whose payload is a
/// <see cref="Stream"/>. The behaviour mirrors <see cref="AesByteArrayFormatter"/> but the
/// input to <c>WriteAsync</c> and the output of the single-stream <c>ReadAsync</c> are streams.
/// </summary>
public class AesStreamFormatterTests
{
    /// <summary>A fixed 16-byte IV so encryption and decryption use identical parameters.</summary>
    private static readonly byte[] Iv = new byte[]
    {
        16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
    };

    /// <summary>A fixed 32-byte (AES-256) key so encryption and decryption match.</summary>
    private static readonly byte[] Key = new byte[]
    {
        32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17,
        16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
    };

    /// <summary>
    /// The default constructor should apply the default buffer size and correct key lengths.
    /// </summary>
    [Fact]
    public void Constructor_Default_InitializesBufferAndKeyLengths_Test()
    {
        // Act
        AesStreamFormatter formatter = new AesStreamFormatter();

        // Assert
        Assert.Equal(Consts.DefaultBufferSize, formatter.BufferSize);
        Assert.Equal(Consts.LengthOfIV, formatter.IV.Length);
        Assert.Equal(Consts.LengthOfKey, formatter.Key.Length);
    }

    /// <summary>
    /// A null data stream must produce a validation error naming "data".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(null!, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("data", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream on write must produce a validation error naming "outputStream".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream input = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the single-stream read overload must produce a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStream_ReturnsValidationError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the two-stream read overload must produce a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStreamWithOutput_ReturnsValidationError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(null!, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream on the two-stream read overload must produce a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream input = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip through the single-stream read overload. The decrypted stream that is
    /// returned is positioned at its end by the implementation, so the test rewinds it before
    /// reading its content back for comparison.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsStream_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        byte[] plaintext = Encoding.UTF8.GetBytes("Streamed AES round-trip payload.");
        using MemoryStream source = new MemoryStream(plaintext);
        using MemoryStream cipher = new MemoryStream();

        // Act (write): encrypt the source stream into the cipher stream.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(source, cipher);
        Assert.False(writeResult.IsError);
        Assert.NotEqual(plaintext, cipher.ToArray());

        // Act (read): rewind the cipher and decrypt it back into a stream.
        cipher.Position = 0;
        ErrorOr<Stream?> readResult = await formatter.ReadAsync(cipher);

        // Assert: the returned stream, once rewound, contains the original plaintext.
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);

        byte[] decrypted = ReadAllBytes(readResult.Value!);
        Assert.Equal(plaintext, decrypted);
    }

    /// <summary>
    /// Full round-trip through the two-stream read overload: decrypted bytes written into the
    /// supplied output stream must equal the original plaintext.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsyncWithOutput_RoundTripsStream_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        byte[] plaintext = Encoding.UTF8.GetBytes("Streamed AES round-trip via output overload.");
        using MemoryStream source = new MemoryStream(plaintext);
        using MemoryStream cipher = new MemoryStream();

        // Act (write)
        ErrorOr<Success> writeResult = await formatter.WriteAsync(source, cipher);
        Assert.False(writeResult.IsError);

        // Act (read into an output stream)
        cipher.Position = 0;
        using MemoryStream decrypted = new MemoryStream();
        ErrorOr<Success> readResult = await formatter.ReadAsync(cipher, decrypted);

        // Assert
        Assert.False(readResult.IsError);
        Assert.Equal(plaintext, decrypted.ToArray());
    }

    /// <summary>
    /// Data encrypted with a certificate-derived key must be decryptable by another formatter
    /// built from the same certificate.
    /// </summary>
    [Fact]
    public async Task Certificate_EncryptAndDecrypt_RoundTrips_Test()
    {
        // Arrange
        using X509Certificate2 certificate = CryptoFormatterBaseTests.CreateSelfSignedCertificate();
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
        AesStreamFormatter writer = new AesStreamFormatter(certificate);
        AesStreamFormatter reader = new AesStreamFormatter(certificate);
#pragma warning restore CS0618
        byte[] plaintext = Encoding.UTF8.GetBytes("Certificate derived stream round-trip.");
        using MemoryStream source = new MemoryStream(plaintext);
        using MemoryStream cipher = new MemoryStream();

        // Act
        ErrorOr<Success> writeResult = await writer.WriteAsync(source, cipher);
        Assert.False(writeResult.IsError);

        cipher.Position = 0;
        ErrorOr<Stream?> readResult = await reader.ReadAsync(cipher);

        // Assert
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);
        Assert.Equal(plaintext, ReadAllBytes(readResult.Value!));
    }

    /// <summary>
    /// A non-readable input stream must be converted into an error result (via <c>ProtectAsync</c>)
    /// instead of throwing. The stream is faked with NSubstitute.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NonReadableStream_ReturnsError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        Stream nonReadable = Substitute.For<Stream>();
        nonReadable.CanRead.Returns(false);

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(nonReadable);

        // Assert
        Assert.True(result.IsError);
    }

    /// <summary>
    /// A non-writable output stream on write must be converted into an error result.
    /// </summary>
    [Fact]
    public async Task WriteAsync_NonWritableStream_ReturnsError_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Stream nonWritable = Substitute.For<Stream>();
        nonWritable.CanWrite.Returns(false);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(source, nonWritable);

        // Assert
        Assert.True(result.IsError);
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    /// <summary>
    /// A cancelled token stops the single-stream read, and the caller's own cancellation is thrown as an
    /// <see cref="OperationCanceledException"/> (since 3.4.0; before, it came back as an
    /// <see cref="ErrorType.Unexpected"/> error).
    /// </summary>
    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange: a valid payload, written with a token that is not cancelled.
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// A cancelled token stops the two-stream read, and throws the same way.
    /// </summary>
    [Fact]
    public async Task ReadAsyncWithOutput_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange: a valid payload, written with a token that is not cancelled.
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, output, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// The two-stream read observes the token while it reads: with a cancelled token it reads nothing
    /// from the input and writes nothing to the output. (Before the fix it read the input with the
    /// synchronous <c>CryptoStream.Read</c>, and only the first write to the output saw the token.)
    /// </summary>
    [Fact]
    public async Task ReadAsyncWithOutput_CancelledToken_ReadsNothingFromInput_Test()
    {
        // Arrange: a valid payload, written with a token that is not cancelled.
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the cancellation is thrown, the input untouched, the output empty.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, output, new CancellationToken(canceled: true)));
        Assert.Equal(0, payload.Position);
        Assert.Equal(0, output.Length);
    }

    /// <summary>
    /// A cancelled token stops the write, and throws the same way.
    /// </summary>
    [Fact]
    public async Task WriteAsync_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.WriteAsync(source, output, new CancellationToken(canceled: true)));
    }

    // ------------------------------------------------------------------
    // The returned stream
    // ------------------------------------------------------------------

    /// <summary>
    /// The single-stream read returns a new <see cref="MemoryStream"/> positioned at its end (unlike
    /// <see cref="GZipStreamFormatter"/>, which rewinds it), so the caller rewinds it before reading.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ReturnedStream_IsAtEnd_Test()
    {
        // Arrange: a valid payload of a known length.
        AesStreamFormatter formatter = new AesStreamFormatter(Iv, Key);
        byte[] original = Encoding.UTF8.GetBytes(new string('P', 300) + "-position");
        using MemoryStream source = new MemoryStream(original);
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(source, payload)).IsError);
        payload.Position = 0;

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(payload);

        // Assert: the stream holds the original bytes and is at its end.
        Assert.False(result.IsError);
        using Stream decompressed = result.Value!;
        Assert.Equal(original.Length, decompressed.Length);
        Assert.Equal(decompressed.Length, decompressed.Position);
    }

    // ------------------------------------------------------------------
    // Known answers: the output that stored data depends on
    // ------------------------------------------------------------------

    /// <summary>
    /// Pins the output: with the key, IV and plaintext of the CBC-AES256 example of NIST SP 800-38A
    /// (F.2.5), the formatter writes exactly NIST's four ciphertext blocks and the PKCS7 padding block,
    /// nothing else (no header, no tag). This proves the output is standard AES-256-CBC with PKCS7; a
    /// change of it fails here before it makes stored data unreadable.
    /// </summary>
    [Fact]
    public async Task WriteAsync_NistVector_WritesKnownCiphertext_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey);
        using MemoryStream source = new MemoryStream(CryptoFormatterBaseTests.NistPlaintext);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(source, output);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(CryptoFormatterBaseTests.NistCiphertextWithPkcs7, output.ToArray());
    }

    /// <summary>
    /// The NIST ciphertext with its PKCS7 block reads back to the NIST plaintext.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NistVector_ReturnsPlaintext_Test()
    {
        // Arrange
        AesStreamFormatter formatter = new AesStreamFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey);
        using MemoryStream input = new MemoryStream(CryptoFormatterBaseTests.NistCiphertextWithPkcs7);

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.False(result.IsError);
        using Stream plain = result.Value!;
        Assert.Equal(CryptoFormatterBaseTests.NistPlaintext, ReadAllBytes(plain));
    }

    /// <summary>
    /// Pins the output with a certificate: the formatter built from the fixed test certificate writes the
    /// known ciphertext, computed once from the IV and key that the certificate gives. Together with
    /// <see cref="CryptoFormatterBaseTests.Certificate_FixedCertificate_DerivesKnownIvAndKey_Test"/> it keeps
    /// data encrypted with a certificate readable.
    /// </summary>
    [Fact]
    public async Task WriteAsync_FixedCertificate_WritesKnownCiphertext_Test()
    {
        // Arrange
        using X509Certificate2 certificate = CryptoFormatterBaseTests.LoadFixedCertificate();
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
        AesStreamFormatter formatter = new AesStreamFormatter(certificate);
#pragma warning restore CS0618
        using MemoryStream source = new MemoryStream(CryptoFormatterBaseTests.NistPlaintext);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(source, output);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(CryptoFormatterBaseTests.FixedCertificateCiphertext, output.ToArray());
    }

    /// <summary>
    /// The known ciphertext of the fixed test certificate reads back to the plaintext.
    /// </summary>
    [Fact]
    public async Task ReadAsync_FixedCertificate_ReturnsPlaintext_Test()
    {
        // Arrange
        using X509Certificate2 certificate = CryptoFormatterBaseTests.LoadFixedCertificate();
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
        AesStreamFormatter formatter = new AesStreamFormatter(certificate);
#pragma warning restore CS0618
        using MemoryStream input = new MemoryStream(CryptoFormatterBaseTests.FixedCertificateCiphertext);

        // Act
        ErrorOr<Stream?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.False(result.IsError);
        using Stream plain = result.Value!;
        Assert.Equal(CryptoFormatterBaseTests.NistPlaintext, ReadAllBytes(plain));
    }

    // ------------------------------------------------------------------
    // Test helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads a stream to its end and returns the content as a byte array. The stream is first
    /// rewound to position 0 because the formatter returns decrypted streams positioned at the
    /// end of the written data.
    /// </summary>
    /// <param name="stream">The stream to drain.</param>
    /// <returns>All bytes contained in the stream.</returns>
    private static byte[] ReadAllBytes(Stream stream)
    {
        // Rewind so we read from the beginning regardless of the current position.
        stream.Position = 0;

        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
