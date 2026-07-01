using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for the abstract <see cref="CryptoFormatterBase{T}"/> class.
/// <para>
/// Because the class is abstract it cannot be instantiated directly, so a minimal
/// concrete subclass (<see cref="TestCryptoFormatter"/>, declared at the bottom of
/// this file) is used to reach the constructors and the IV / Key / Certificate /
/// BufferSize members that live on the base class.
/// </para>
/// </summary>
public class CryptoFormatterBaseTests
{
    /// <summary>
    /// The exact IV length (16 bytes) that the base class accepts. Kept here as a local
    /// copy so the tests are readable and independent from the production constant.
    /// </summary>
    private const int ValidIvLength = 16;

    /// <summary>
    /// The exact key length (32 bytes) that the base class accepts.
    /// </summary>
    private const int ValidKeyLength = 32;

    /// <summary>
    /// The default constructor must leave the buffer size at the documented default (8 KB).
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDefaultBufferSize_Test()
    {
        // Arrange & Act: create an instance through the parameterless constructor.
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // Assert: BufferSize should equal the shared default constant.
        Assert.Equal(Consts.DefaultBufferSize, formatter.BufferSize);
    }

    /// <summary>
    /// The parameterless constructor fills the IV and the Key with random bytes and gives
    /// them the correct lengths. We assert the lengths, and we assert that two independently
    /// created instances produce different material (which proves the values are random and
    /// not left as all-zero arrays). The odds of a false failure are 1 in 2^128.
    /// </summary>
    [Fact]
    public void Constructor_Default_GeneratesRandomIvAndKey_Test()
    {
        // Arrange & Act: two separate instances get two separate random IV/key pairs.
        TestCryptoFormatter first = new TestCryptoFormatter();
        TestCryptoFormatter second = new TestCryptoFormatter();

        // Assert: the buffers have the required cryptographic lengths.
        Assert.Equal(ValidIvLength, first.IV.Length);
        Assert.Equal(ValidKeyLength, first.Key.Length);

        // Assert: randomness => the two instances almost certainly differ.
        Assert.NotEqual(first.IV, second.IV);
        Assert.NotEqual(first.Key, second.Key);
    }

    /// <summary>
    /// The IV/Key constructor must store exactly the arrays that were passed in.
    /// </summary>
    [Fact]
    public void Constructor_WithIvAndKey_StoresProvidedValues_Test()
    {
        // Arrange: build a valid IV and key (correct lengths, recognisable content).
        byte[] iv = CreateSequentialBytes(ValidIvLength);
        byte[] key = CreateSequentialBytes(ValidKeyLength);

        // Act: construct through the (iv, key) constructor.
        TestCryptoFormatter formatter = new TestCryptoFormatter(iv, key);

        // Assert: the properties return exactly what we passed in.
        Assert.Equal(iv, formatter.IV);
        Assert.Equal(key, formatter.Key);
    }

    /// <summary>
    /// A null IV passed to the constructor must be rejected with an <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_WithNullIv_ThrowsArgumentNullException_Test()
    {
        // Arrange: a valid key but a null IV.
        byte[] key = CreateSequentialBytes(ValidKeyLength);

        // Act & Assert: the constructor guard for "iv" fires first.
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new TestCryptoFormatter(null!, key));
        Assert.Equal("iv", ex.ParamName);
    }

    /// <summary>
    /// A null key passed to the constructor must be rejected with an <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_WithNullKey_ThrowsArgumentNullException_Test()
    {
        // Arrange: a valid IV but a null key.
        byte[] iv = CreateSequentialBytes(ValidIvLength);

        // Act & Assert: the constructor guard for "key" fires.
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new TestCryptoFormatter(iv, null!));
        Assert.Equal("key", ex.ParamName);
    }

    /// <summary>
    /// The certificate constructor must reject a null certificate.
    /// </summary>
    [Fact]
    public void Constructor_WithNullCertificate_ThrowsArgumentNullException_Test()
    {
        // Act & Assert: passing null for the certificate throws with the "certificate" param name.
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new TestCryptoFormatter((X509Certificate2)null!));
        Assert.Equal("certificate", ex.ParamName);
    }

    /// <summary>
    /// Setting a valid IV (correct length) through the property must store it.
    /// </summary>
    [Fact]
    public void IV_SetValidValue_StoresValue_Test()
    {
        // Arrange: start from a default instance and build a valid IV.
        TestCryptoFormatter formatter = new TestCryptoFormatter();
        byte[] iv = CreateSequentialBytes(ValidIvLength);

        // Act: assign via the property setter.
        formatter.IV = iv;

        // Assert: the getter returns the same value.
        Assert.Equal(iv, formatter.IV);
    }

    /// <summary>
    /// Assigning null to the IV property must throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void IV_SetNull_ThrowsArgumentNullException_Test()
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // Act & Assert: the setter's null guard fires.
        Assert.Throws<ArgumentNullException>(() => formatter.IV = null!);
    }

    /// <summary>
    /// Assigning an IV with the wrong length must throw <see cref="InvalidDataException"/>,
    /// because AES requires an exactly 16-byte IV.
    /// </summary>
    [Theory]
    [InlineData(0)]   // empty
    [InlineData(15)]  // one byte too short
    [InlineData(17)]  // one byte too long
    [InlineData(32)]  // key length is not a valid IV length
    public void IV_SetWrongLength_ThrowsInvalidDataException_Test(int wrongLength)
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter();
        byte[] iv = new byte[wrongLength];

        // Act & Assert: any length other than 16 is rejected.
        Assert.Throws<InvalidDataException>(() => formatter.IV = iv);
    }

    /// <summary>
    /// Setting a valid key (correct length) through the property must store it.
    /// </summary>
    [Fact]
    public void Key_SetValidValue_StoresValue_Test()
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter();
        byte[] key = CreateSequentialBytes(ValidKeyLength);

        // Act
        formatter.Key = key;

        // Assert
        Assert.Equal(key, formatter.Key);
    }

    /// <summary>
    /// Assigning null to the Key property must throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Key_SetNull_ThrowsArgumentNullException_Test()
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => formatter.Key = null!);
    }

    /// <summary>
    /// Assigning a key with the wrong length must throw <see cref="InvalidDataException"/>,
    /// because AES-256 requires an exactly 32-byte key.
    /// </summary>
    [Theory]
    [InlineData(0)]   // empty
    [InlineData(16)]  // IV length is not a valid key length
    [InlineData(31)]  // one byte too short
    [InlineData(33)]  // one byte too long
    public void Key_SetWrongLength_ThrowsInvalidDataException_Test(int wrongLength)
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter();
        byte[] key = new byte[wrongLength];

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => formatter.Key = key);
    }

    /// <summary>
    /// The buffer size is a plain auto-property with validation logic elsewhere, so a
    /// simple round-trip of the value must work.
    /// </summary>
    [Fact]
    public void BufferSize_SetValue_StoresValue_Test()
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // Act
        formatter.BufferSize = 4096;

        // Assert
        Assert.Equal(4096, formatter.BufferSize);
    }

    /// <summary>
    /// Setting the <see cref="CryptoFormatterBase{T}.Certificate"/> property derives the IV
    /// from the first 16 bytes of the certificate's public key raw data and the key from the
    /// last 32 bytes. This test asserts that exact behaviour deterministically.
    /// </summary>
    [Fact]
    public void Certificate_Set_DerivesIvAndKeyFromPublicKey_Test()
    {
        // Arrange: generate a throw-away self-signed certificate.
        using X509Certificate2 certificate = CreateSelfSignedCertificate();
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // The production code copies from the encoded public key value.
        byte[] rawPublicKey = certificate.PublicKey.EncodedKeyValue.RawData;

        // Expected IV = first 16 bytes; expected key = last 32 bytes.
        byte[] expectedIv = new byte[ValidIvLength];
        Buffer.BlockCopy(rawPublicKey, 0, expectedIv, 0, ValidIvLength);

        byte[] expectedKey = new byte[ValidKeyLength];
        Buffer.BlockCopy(rawPublicKey, rawPublicKey.Length - ValidKeyLength, expectedKey, 0, ValidKeyLength);

        // Act: assigning the certificate triggers the derivation logic in the setter.
        formatter.Certificate = certificate;

        // Assert: the stored certificate, IV and key all match the expectations.
        Assert.Same(certificate, formatter.Certificate);
        Assert.Equal(expectedIv, formatter.IV);
        Assert.Equal(expectedKey, formatter.Key);
    }

    /// <summary>
    /// Two formatters constructed from the same certificate must derive identical IV/key
    /// material. This proves the derivation is deterministic (a prerequisite for being able
    /// to encrypt with one instance and decrypt with another that shares the certificate).
    /// </summary>
    [Fact]
    public void Certificate_SameCertificate_ProducesDeterministicKeys_Test()
    {
        // Arrange
        using X509Certificate2 certificate = CreateSelfSignedCertificate();

        // Act: two instances constructed from the very same certificate.
        TestCryptoFormatter first = new TestCryptoFormatter(certificate);
        TestCryptoFormatter second = new TestCryptoFormatter(certificate);

        // Assert: identical derivation => identical IV and key.
        Assert.Equal(first.IV, second.IV);
        Assert.Equal(first.Key, second.Key);
    }

    /// <summary>
    /// Setting the certificate to <c>null</c> is explicitly tolerated by the setter (it
    /// returns early), and must not throw or alter the previously configured IV/key.
    /// </summary>
    [Fact]
    public void Certificate_SetNull_KeepsExistingKeysAndDoesNotThrow_Test()
    {
        // Arrange: start from a certificate-derived state.
        using X509Certificate2 certificate = CreateSelfSignedCertificate();
        TestCryptoFormatter formatter = new TestCryptoFormatter(certificate);

        byte[] ivBefore = (byte[])formatter.IV.Clone();
        byte[] keyBefore = (byte[])formatter.Key.Clone();

        // Act: assigning null should just store null and return before touching IV/key.
        formatter.Certificate = null;

        // Assert: certificate is now null, but the previously derived material is untouched.
        Assert.Null(formatter.Certificate);
        Assert.Equal(ivBefore, formatter.IV);
        Assert.Equal(keyBefore, formatter.Key);
    }

    // ------------------------------------------------------------------
    // Test helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a byte array of the requested length filled with a predictable, ascending
    /// pattern (0, 1, 2, ...). Predictable content makes equality assertions easy to read.
    /// </summary>
    /// <param name="length">The number of bytes to generate.</param>
    /// <returns>A new byte array of the requested length.</returns>
    private static byte[] CreateSequentialBytes(int length)
    {
        byte[] result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            // Cast to byte so the value wraps around after 255 (harmless for our lengths).
            result[i] = (byte)i;
        }

        return result;
    }

    /// <summary>
    /// Builds a minimal, in-memory self-signed X509 certificate that carries an RSA public
    /// key. The public key's raw data is comfortably longer than 32 bytes, which is required
    /// by the certificate setter (it slices 16 bytes for the IV and 32 bytes for the key).
    /// </summary>
    /// <returns>A freshly generated self-signed certificate.</returns>
    internal static X509Certificate2 CreateSelfSignedCertificate()
    {
        // A 2048-bit RSA key yields a public key blob of ~270 bytes -> more than enough.
        using RSA rsa = RSA.Create(2048);

        CertificateRequest request = new CertificateRequest(
            "CN=Forge.Next.Formatters.Tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Validity window is irrelevant for our purposes; we only read the public key.
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    /// <summary>
    /// Minimal concrete subclass used purely to instantiate the abstract base class under test.
    /// The three abstract read/write methods are implemented as no-ops returning trivial
    /// successful results, because these tests never exercise the actual crypto pipeline –
    /// they only verify the base-class constructors and property behaviour.
    /// </summary>
    private sealed class TestCryptoFormatter : CryptoFormatterBase<byte[]>
    {
        /// <summary>Forwards to the parameterless base constructor (random IV/key).</summary>
        public TestCryptoFormatter()
            : base()
        {
        }

        /// <summary>Forwards to the certificate-based base constructor (derives IV/key).</summary>
        public TestCryptoFormatter(X509Certificate2 certificate)
            : base(certificate)
        {
        }

        /// <summary>Forwards to the (iv, key) base constructor (validated assignment).</summary>
        public TestCryptoFormatter(byte[] iv, byte[] key)
            : base(iv, key)
        {
        }

        /// <summary>No-op override; returns a null value wrapped as a successful result.</summary>
        public override Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ErrorOr<byte[]?>)(byte[]?)null);
        }

        /// <summary>No-op override; returns a success result.</summary>
        public override Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ErrorOr<Success>)Result.Success);
        }

        /// <summary>No-op override; returns a success result.</summary>
        public override Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ErrorOr<Success>)Result.Success);
        }
    }
}
