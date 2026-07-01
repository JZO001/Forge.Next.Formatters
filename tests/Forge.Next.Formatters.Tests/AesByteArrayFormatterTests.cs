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
        AesByteArrayFormatter writer = new AesByteArrayFormatter(certificate);
        AesByteArrayFormatter reader = new AesByteArrayFormatter(certificate);
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
}
