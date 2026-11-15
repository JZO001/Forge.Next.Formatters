using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using NSubstitute;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for <see cref="AesByteArrayFormatter"/>, the AES formatter whose payload is a
/// <see cref="byte"/> array. The tests cover argument validation, a full encrypt/decrypt
/// round-trip (both read overloads), certificate-derived keys, and the error path that the
/// <c>ProtectAsync</c> wrapper produces when the underlying stream misbehaves.
/// </summary>
public class AesByteArrayFormatterTests
{
    /// <summary>A fixed 16-byte IV so encryption and decryption use identical parameters.</summary>
    private static readonly byte[] Iv = new byte[]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16
    };

    /// <summary>A fixed 32-byte (AES-256) key so encryption and decryption match.</summary>
    private static readonly byte[] Key = new byte[]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    };

    /// <summary>
    /// The default constructor should apply the default buffer size and produce IV/key
    /// buffers of the correct AES lengths.
    /// </summary>
    [Fact]
    public void Constructor_Default_InitializesBufferAndKeyLengths_Test()
    {
        // Act
        AesByteArrayFormatter formatter = new AesByteArrayFormatter();

        // Assert: buffer size defaulted, IV = 16 bytes, key = 32 bytes.
        Assert.Equal(Consts.DefaultBufferSize, formatter.BufferSize);
        Assert.Equal(Consts.LengthOfIV, formatter.IV.Length);
        Assert.Equal(Consts.LengthOfKey, formatter.Key.Length);
    }

    /// <summary>
    /// Passing a null data array to <see cref="AesByteArrayFormatter.WriteAsync(byte[], Stream, System.Threading.CancellationToken)"/>
    /// must short-circuit into a validation error whose description names the "data" argument.
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(null!, output);

        // Assert: this is an explicit up-front validation error (not routed through crypto).
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("data", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream must produce a validation error naming "outputStream".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new byte[] { 1, 2, 3 }, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the single-stream read overload must return a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStream_ReturnsValidationError_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null input stream on the two-stream read overload must return a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStreamWithOutput_ReturnsValidationError_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(null!, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("inputStream", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream on the two-stream read overload must return a validation error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        using MemoryStream input = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip through the single-stream read overload: data encrypted with
    /// <c>WriteAsync</c> and then decrypted with <c>ReadAsync</c> must equal the original.
    /// The ciphertext must also differ from the plaintext, proving that encryption happened.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsByteArray_Test()
    {
        // Arrange: a well-known plaintext and a formatter with fixed IV/key.
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        byte[] plaintext = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog.");
        using MemoryStream cipher = new MemoryStream();

        // Act (write): encrypt the plaintext into the cipher stream.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(plaintext, cipher);

        // Assert (write): success and the bytes are actually scrambled.
        Assert.False(writeResult.IsError);
        Assert.NotEqual(plaintext, cipher.ToArray());

        // Act (read): rewind and decrypt.
        cipher.Position = 0;
        ErrorOr<byte[]?> readResult = await formatter.ReadAsync(cipher);

        // Assert (read): the decrypted bytes equal the original plaintext.
        Assert.False(readResult.IsError);
        Assert.Equal(plaintext, readResult.Value);
    }

    /// <summary>
    /// Full round-trip through the two-stream read overload: the decrypted output written to
    /// the supplied output stream must equal the original plaintext.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsyncWithOutput_RoundTripsByteArray_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        byte[] plaintext = Encoding.UTF8.GetBytes("Round-trip via the output-stream overload.");
        using MemoryStream cipher = new MemoryStream();

        // Act (write)
        ErrorOr<Success> writeResult = await formatter.WriteAsync(plaintext, cipher);
        Assert.False(writeResult.IsError);

        // Act (read into an output stream)
        cipher.Position = 0;
        using MemoryStream decrypted = new MemoryStream();
        ErrorOr<Success> readResult = await formatter.ReadAsync(cipher, decrypted);

        // Assert: success and the decrypted output matches the original.
        Assert.False(readResult.IsError);
        Assert.Equal(plaintext, decrypted.ToArray());
    }

    /// <summary>
    /// Data encrypted using an X509 certificate must be decryptable by a second formatter
    /// constructed from the same certificate, because the IV/key are derived deterministically.
    /// </summary>
    [Fact]
    public async Task Certificate_EncryptAndDecrypt_RoundTrips_Test()
    {
        // Arrange: two formatters that share the same certificate (hence the same derived keys).
        using X509Certificate2 certificate = CryptoFormatterBaseTests.CreateSelfSignedCertificate();
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
        AesByteArrayFormatter writer = new AesByteArrayFormatter(certificate);
        AesByteArrayFormatter reader = new AesByteArrayFormatter(certificate);
#pragma warning restore CS0618
        byte[] plaintext = Encoding.UTF8.GetBytes("Certificate derived key round-trip.");
        using MemoryStream cipher = new MemoryStream();

        // Act
        ErrorOr<Success> writeResult = await writer.WriteAsync(plaintext, cipher);
        Assert.False(writeResult.IsError);

        cipher.Position = 0;
        ErrorOr<byte[]?> readResult = await reader.ReadAsync(cipher);

        // Assert
        Assert.False(readResult.IsError);
        Assert.Equal(plaintext, readResult.Value);
    }

    /// <summary>
    /// When the input stream cannot be read, the internal <c>CryptoStream</c> constructor
    /// throws; the <c>ProtectAsync</c> wrapper must catch that and surface it as an error
    /// rather than letting the exception escape. A non-readable stream is simulated with
    /// NSubstitute (the preferred mocking library).
    /// </summary>
    [Fact]
    public async Task ReadAsync_NonReadableStream_ReturnsError_Test()
    {
        // Arrange: a substitute stream that reports it cannot be read.
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        Stream nonReadable = Substitute.For<Stream>();
        nonReadable.CanRead.Returns(false);

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(nonReadable);

        // Assert: the exception was converted into an error result.
        Assert.True(result.IsError);
    }

    /// <summary>
    /// When the output stream cannot be written, the encrypting <c>CryptoStream</c> constructor
    /// throws; <c>ProtectAsync</c> must convert that into an error result.
    /// </summary>
    [Fact]
    public async Task WriteAsync_NonWritableStream_ReturnsError_Test()
    {
        // Arrange: a substitute stream that reports it cannot be written to.
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        Stream nonWritable = Substitute.For<Stream>();
        nonWritable.CanWrite.Returns(false);

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new byte[] { 1, 2, 3, 4 }, nonWritable);

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
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(new byte[] { 1, 2, 3, 4 }, payload)).IsError);
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
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        using MemoryStream payload = new MemoryStream();
        Assert.False((await formatter.WriteAsync(new byte[] { 1, 2, 3, 4 }, payload)).IsError);
        payload.Position = 0;
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.ReadAsync(payload, output, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// A cancelled token stops the write, and throws the same way.
    /// </summary>
    [Fact]
    public async Task WriteAsync_CancelledToken_ThrowsOperationCanceledException_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key);
        using MemoryStream output = new MemoryStream();

        // Act & Assert: the caller's own cancellation is thrown, not returned as an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => formatter.WriteAsync(new byte[] { 1, 2, 3, 4 }, output, new CancellationToken(canceled: true)));
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
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(CryptoFormatterBaseTests.NistPlaintext, output);

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
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey);
        using MemoryStream input = new MemoryStream(CryptoFormatterBaseTests.NistCiphertextWithPkcs7);

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(CryptoFormatterBaseTests.NistPlaintext, result.Value);
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
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(certificate);
#pragma warning restore CS0618
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(CryptoFormatterBaseTests.NistPlaintext, output);

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
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(certificate);
#pragma warning restore CS0618
        using MemoryStream input = new MemoryStream(CryptoFormatterBaseTests.FixedCertificateCiphertext);

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(CryptoFormatterBaseTests.NistPlaintext, result.Value);
    }

    // ------------------------------------------------------------------
    // The authenticated format (AesOutputFormat.Authenticated, since 3.4.0)
    // ------------------------------------------------------------------

    /// <summary>
    /// Pins the authenticated format: the known-answer message, made independently of the formatters, reads back to
    /// the plaintext, with the formatter's default settings (the format is recognized by its header).
    /// </summary>
    [Fact]
    public async Task ReadAsync_AuthenticatedKnownAnswer_ReturnsPlaintext_Test()
    {
        // Arrange: the IV property is not used by this format; a different one proves it.
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, CryptoFormatterBaseTests.NistKey);
        using MemoryStream input = new MemoryStream(CryptoFormatterBaseTests.AuthenticatedMessage);

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(CryptoFormatterBaseTests.NistPlaintext, result.Value);
    }

    /// <summary>
    /// With <see cref="AesOutputFormat.Authenticated"/> the formatter writes the header, a 16-byte IV, the ciphertext
    /// and a 32-byte tag, and a reader with the same key, but another IV and the default settings, reads it back.
    /// </summary>
    [Fact]
    public async Task WriteAsync_Authenticated_WritesTheFormatAndRoundTrips_Test()
    {
        // Arrange
        AesByteArrayFormatter writer = new AesByteArrayFormatter(Iv, Key) { OutputFormat = AesOutputFormat.Authenticated };
        AesByteArrayFormatter reader = new AesByteArrayFormatter(new byte[16], Key);
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> write = await writer.WriteAsync(CryptoFormatterBaseTests.NistPlaintext, output);
        byte[] message = output.ToArray();
        ErrorOr<byte[]?> read = await reader.ReadAsync(new MemoryStream(message));

        // Assert: 4 + 16 + 80 (64 bytes and a full PKCS7 block) + 32 bytes, starting with the header.
        Assert.False(write.IsError);
        Assert.Equal(132, message.Length);
        Assert.Equal(new byte[] { 0x46, 0x4E, 0x46, 0x02 }, message.Take(4).ToArray());
        Assert.False(read.IsError);
        Assert.Equal(CryptoFormatterBaseTests.NistPlaintext, read.Value);
    }

    /// <summary>
    /// Every authenticated message gets a new random IV, so the same plaintext gives different messages.
    /// </summary>
    [Fact]
    public async Task WriteAsync_AuthenticatedTwice_UsesANewIvEachTime_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, Key) { OutputFormat = AesOutputFormat.Authenticated };
        using MemoryStream first = new MemoryStream();
        using MemoryStream second = new MemoryStream();

        // Act
        Assert.False((await formatter.WriteAsync(CryptoFormatterBaseTests.NistPlaintext, first)).IsError);
        Assert.False((await formatter.WriteAsync(CryptoFormatterBaseTests.NistPlaintext, second)).IsError);

        // Assert: the IVs (bytes 4 to 19) differ, and so do the messages.
        Assert.NotEqual(first.ToArray().Skip(4).Take(16).ToArray(), second.ToArray().Skip(4).Take(16).ToArray());
        Assert.NotEqual(first.ToArray(), second.ToArray());
    }

    /// <summary>
    /// One flipped bit anywhere in an authenticated message gives an error. When the unauthenticated format is refused,
    /// nothing reaches the output; when it is accepted, nothing does either, unless the flip is in the header, which
    /// makes the input look like the unauthenticated format.
    /// </summary>
    /// <param name="position">The byte whose lowest bit is flipped: the header, the IV, the ciphertext or the tag.</param>
    /// <param name="acceptUnauthenticated">The value of <see cref="CryptoFormatterBase{T}.AcceptUnauthenticated"/>.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(20, false)]
    [InlineData(99, false)]
    [InlineData(131, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(20, true)]
    [InlineData(99, true)]
    [InlineData(131, true)]
    public async Task ReadAsyncWithOutput_AuthenticatedFlippedBit_ReturnsError_Test(int position, bool acceptUnauthenticated)
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, CryptoFormatterBaseTests.NistKey) { AcceptUnauthenticated = acceptUnauthenticated };
        byte[] message = (byte[])CryptoFormatterBaseTests.AuthenticatedMessage.Clone();
        message[position] ^= 0x01;
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(new MemoryStream(message), output);

        // Assert
        Assert.True(result.IsError);
        if (!acceptUnauthenticated || position >= 4) Assert.Equal(0, output.Length);
    }

    /// <summary>
    /// A cut-off authenticated message gives an error, whether or not the unauthenticated format is accepted.
    /// </summary>
    /// <param name="cut">The number of bytes cut off the end.</param>
    /// <param name="acceptUnauthenticated">The value of <see cref="CryptoFormatterBase{T}.AcceptUnauthenticated"/>.</param>
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(4, false)]
    [InlineData(32, false)]
    [InlineData(32, true)]
    public async Task ReadAsync_AuthenticatedCutOff_ReturnsError_Test(int cut, bool acceptUnauthenticated)
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(Iv, CryptoFormatterBaseTests.NistKey) { AcceptUnauthenticated = acceptUnauthenticated };
        byte[] message = CryptoFormatterBaseTests.AuthenticatedMessage.Take(CryptoFormatterBaseTests.AuthenticatedMessage.Length - cut).ToArray();

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(new MemoryStream(message));

        // Assert
        Assert.True(result.IsError);
    }

    /// <summary>
    /// With <see cref="CryptoFormatterBase{T}.AcceptUnauthenticated"/> set to <c>false</c>, input of the
    /// <see cref="AesOutputFormat.Legacy"/> format is refused with an error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_LegacyInputNotAccepted_ReturnsError_Test()
    {
        // Arrange
        AesByteArrayFormatter formatter = new AesByteArrayFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey) { AcceptUnauthenticated = false };

        // Act
        ErrorOr<byte[]?> result = await formatter.ReadAsync(new MemoryStream(CryptoFormatterBaseTests.NistCiphertextWithPkcs7));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
    }

    /// <summary>
    /// A message of the <see cref="AesOutputFormat.Legacy"/> format whose first bytes equal the header (by chance once
    /// in 2^32; made here on purpose by choosing the first ciphertext block) is still read the legacy way when that
    /// format is accepted, and refused when it is not.
    /// </summary>
    /// <param name="acceptUnauthenticated">The value of <see cref="CryptoFormatterBase{T}.AcceptUnauthenticated"/>.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_LegacyInputStartingWithTheHeader_IsReadTheLegacyWayWhenAccepted_Test(bool acceptUnauthenticated)
    {
        // Arrange: the plaintext block whose ciphertext is "FNF" 0x02 and 12 zero bytes: P = AES-Decrypt(C) XOR IV.
        byte[] chosenBlock = new byte[16];
        chosenBlock[0] = 0x46; chosenBlock[1] = 0x4E; chosenBlock[2] = 0x46; chosenBlock[3] = 0x02;
        byte[] plaintext;
        using (Aes aes = Aes.Create())
        {
            aes.Key = CryptoFormatterBaseTests.NistKey;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            plaintext = decryptor.TransformFinalBlock(chosenBlock, 0, 16);
        }

        for (int i = 0; i < 16; i++) plaintext[i] ^= CryptoFormatterBaseTests.NistIv[i];

        AesByteArrayFormatter writer = new AesByteArrayFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey);
        using MemoryStream legacy = new MemoryStream();
        Assert.False((await writer.WriteAsync(plaintext, legacy)).IsError);
        Assert.Equal(chosenBlock, legacy.ToArray().Take(16).ToArray());

        AesByteArrayFormatter reader = new AesByteArrayFormatter(CryptoFormatterBaseTests.NistIv, CryptoFormatterBaseTests.NistKey) { AcceptUnauthenticated = acceptUnauthenticated };

        // Act
        ErrorOr<byte[]?> result = await reader.ReadAsync(new MemoryStream(legacy.ToArray()));

        // Assert
        Assert.Equal(!acceptUnauthenticated, result.IsError);
        if (acceptUnauthenticated) Assert.Equal(plaintext, result.Value);
    }
}
