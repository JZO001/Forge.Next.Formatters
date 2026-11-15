using System;
using System.IO;
using System.Reflection;
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
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
        formatter.Certificate = certificate;

        // Assert: the stored certificate, IV and key all match the expectations.
        Assert.Same(certificate, formatter.Certificate);
#pragma warning restore CS0618
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
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
        formatter.Certificate = null;

        // Assert: certificate is now null, but the previously derived material is untouched.
        Assert.Null(formatter.Certificate);
#pragma warning restore CS0618
        Assert.Equal(ivBefore, formatter.IV);
        Assert.Equal(keyBefore, formatter.Key);
    }

    /// <summary>
    /// Pins the derivation from a certificate, so data encrypted with one stays readable: the IV is the
    /// first 16 and the key the last 32 bytes of the encoded public key. The expected values of the fixed
    /// test certificate were computed once from its DER encoding. For an RSA 2048 certificate the first 9
    /// bytes of that IV (the DER header) and the last 5 bytes of that key (the public exponent 65537) are
    /// the same for every certificate.
    /// </summary>
    [Fact]
    public void Certificate_FixedCertificate_DerivesKnownIvAndKey_Test()
    {
        // Arrange
        using X509Certificate2 certificate = LoadFixedCertificate();

        // Act
        TestCryptoFormatter formatter = new TestCryptoFormatter(certificate);

        // Assert
        Assert.Equal(FixedCertificateIv, formatter.IV);
        Assert.Equal(FixedCertificateKey, formatter.Key);
    }

    /// <summary>
    /// Pins the decision of B2: every member that takes or exposes the certificate is marked
    /// <see cref="ObsoleteAttribute"/> since 3.4.0, as a warning and not an error, so callers learn that the
    /// derivation uses only the public key, while their code still compiles and data written this way stays
    /// readable.
    /// </summary>
    [Fact]
    public void Certificate_Members_AreObsoleteAsWarning_Test()
    {
        // Arrange: the interface property, the base property and the three certificate constructors.
        MemberInfo[] members =
        {
            typeof(ICryptoFormatter<byte[]>).GetProperty("Certificate")!,
            typeof(CryptoFormatterBase<byte[]>).GetProperty("Certificate")!,
            typeof(CryptoFormatterBase<byte[]>).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(X509Certificate2) }, null)!,
            typeof(AesByteArrayFormatter).GetConstructor(new[] { typeof(X509Certificate2) })!,
            typeof(AesStreamFormatter).GetConstructor(new[] { typeof(X509Certificate2) })!,
        };

        // Act & Assert: each one carries [Obsolete], and it is a warning.
        foreach (MemberInfo member in members)
        {
            Assert.NotNull(member);
            ObsoleteAttribute? obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(obsolete);
            Assert.False(obsolete!.IsError);
        }
    }

    /// <summary>
    /// A new formatter writes the <see cref="AesOutputFormat.Legacy"/> format by default, so its output is what earlier
    /// versions wrote (since 3.4.0 the format is a choice).
    /// </summary>
    [Fact]
    public void OutputFormat_Default_IsLegacy_Test()
    {
        // Act
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // Assert
        Assert.Equal(AesOutputFormat.Legacy, formatter.OutputFormat);
    }

    /// <summary>
    /// A new formatter accepts the unauthenticated <see cref="AesOutputFormat.Legacy"/> format by default, so data
    /// written by earlier versions stays readable.
    /// </summary>
    [Fact]
    public void AcceptUnauthenticated_Default_IsTrue_Test()
    {
        // Act
        TestCryptoFormatter formatter = new TestCryptoFormatter();

        // Assert
        Assert.True(formatter.AcceptUnauthenticated);
    }

    /// <summary>
    /// The <see cref="CryptoFormatterBase{T}.OutputFormat"/> setter rejects a value that is not a defined
    /// <see cref="AesOutputFormat"/>, and keeps the previous value.
    /// </summary>
    [Fact]
    public void OutputFormat_SetUndefinedValue_ThrowsArgumentOutOfRangeException_Test()
    {
        // Arrange
        TestCryptoFormatter formatter = new TestCryptoFormatter { OutputFormat = AesOutputFormat.Authenticated };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => formatter.OutputFormat = (AesOutputFormat)42);
        Assert.Equal(AesOutputFormat.Authenticated, formatter.OutputFormat);
    }

    // ------------------------------------------------------------------
    // Known-answer data, shared with the AES formatter tests
    // ------------------------------------------------------------------
    // These values pin the AES output that stored data depends on. They come from NIST SP 800-38A
    // or were computed once independently of the formatters; never regenerate them from the
    // formatters' own output, or the tests stop catching a change.

    /// <summary>
    /// The key of the CBC-AES256 example of NIST SP 800-38A, Appendix F.2.5.
    /// </summary>
    internal static readonly byte[] NistKey = FromHex(
        "603deb1015ca71be2b73aef0857d7781" +
        "1f352c073b6108d72d9810a30914dff4");

    /// <summary>
    /// The IV of NIST SP 800-38A, F.2.5.
    /// </summary>
    internal static readonly byte[] NistIv = FromHex("000102030405060708090a0b0c0d0e0f");

    /// <summary>
    /// The four plaintext blocks of NIST SP 800-38A, F.2.5 (64 bytes).
    /// </summary>
    internal static readonly byte[] NistPlaintext = FromHex(
        "6bc1bee22e409f96e93d7e117393172a" +
        "ae2d8a571e03ac9c9eb76fac45af8e51" +
        "30c81c46a35ce411e5fbc1191a0a52ef" +
        "f69f2445df4f9b17ad2b417be66c3710");

    /// <summary>
    /// The four ciphertext blocks of NIST SP 800-38A, F.2.5, followed by the block that PKCS7 padding adds
    /// (80 bytes). The plaintext is whole blocks, so the padding is a full block of 0x10 bytes; its
    /// ciphertext, AES-256 of that block XOR the fourth ciphertext block, was computed once with a
    /// single-block AES encryption, independently of the formatters.
    /// </summary>
    internal static readonly byte[] NistCiphertextWithPkcs7 = FromHex(
        "f58c4c04d6e5f1ba779eabfb5f7bfbd6" +
        "9cfc4e967edb808d679f777bc6702c7d" +
        "39f23369a9d9bacfa530e26304231461" +
        "b2eb05e2c39be9fcda6c19078c6a9d1b" +
        "3f461796d6b0d6b2e0c2a72b4d80e644");

    /// <summary>
    /// A known-answer message of the <see cref="AesOutputFormat.Authenticated"/> format (132 bytes): the key and the
    /// plaintext of <see cref="NistKey"/> and <see cref="NistPlaintext"/>, the IV <c>f0f1...ff</c>. Computed once,
    /// independently of the formatters: the two keys as HMAC-SHA256 of the key over the labels
    /// <c>Forge.Next.Formatters.Aes.Encryption</c> and <c>Forge.Next.Formatters.Aes.Authentication</c>, the ciphertext by
    /// hand-made CBC from single-block AES encryptions, and the tag as HMAC-SHA256 over the header, the IV and the
    /// ciphertext.
    /// </summary>
    internal static readonly byte[] AuthenticatedMessage = FromHex(
        "464e4602f0f1f2f3f4f5f6f7f8f9fafb" +
        "fcfdfeff8e0b4fee4e281db1041bf656" +
        "f3db1850349610860dd54f28d8511007" +
        "74b606ab3d4e0d8b21619748f3e76655" +
        "b6f8ebbf870467eadf7a35e0f481bc80" +
        "bca9f544dc5596fe6469f825d9830fe0" +
        "c34dd70af95f4b18478755ee15cb2ba7" +
        "816e72cef524a1a8e2a61d9f406612fc" +
        "76d8f239");

    /// <summary>
    /// A fixed self-signed RSA 2048 test certificate ("CN=Forge.Next.Formatters.Tests.FixedCertificate"),
    /// DER in base64, made once on 2026-11-15. Only its public part is here: the formatters derive the IV
    /// and the key from the public key, so no private key is needed.
    /// </summary>
    private const string FixedCertificateBase64 =
        "MIIC8TCCAdmgAwIBAgIJAMWUR0/FjLzXMA0GCSqGSIb3DQEBCwUAMDcxNTAzBgNVBAMTLEZvcmdlLk5leHQuRm9y" +
        "bWF0dGVycy5UZXN0cy5GaXhlZENlcnRpZmljYXRlMCAXDTI2MDEwMTAwMDAwMFoYDzIxMjYwMTAxMDAwMDAwWjA3" +
        "MTUwMwYDVQQDEyxGb3JnZS5OZXh0LkZvcm1hdHRlcnMuVGVzdHMuRml4ZWRDZXJ0aWZpY2F0ZTCCASIwDQYJKoZI" +
        "hvcNAQEBBQADggEPADCCAQoCggEBAKS+hOJykf/tYbz2jx1na+bNPaXGIDQeb2yt6BNgiBz+evomOry2HR3LuDuK" +
        "4Xz0te/9iLy+WCsYFOvmTnetg2zlcsPTaPNm2khMYCcxoCW/wpodw7xR/plIpISX05I9YbSqNiPes1a46VBQr4Pt" +
        "2mAc584XM5zuSdRw+B5LSFa0ICUdTzQ2B3gOMJ69UpSMuMtSXAfRNuKm5djTTzrn2un0R62nHrt5DbxGKjKxUt9K" +
        "6tLSr4GOslo2kVTyEB7qM1cde+UNGe+EVeXcgc2ID0o3ZJIqX4dV0UxojpTlXEXzRf9I/PQT8IU++H3t2BB/Bskw" +
        "1mOX0niGfdaW+YRTRLUCAwEAATANBgkqhkiG9w0BAQsFAAOCAQEAk8IHcuSmkx2H71aAcj3/QRKlZmbMMrEFZk9g" +
        "ym1K1ArNt2w1GOY3MmvR1X7Y+X/2z/3bVMLzCxV5iXAccGN7WIuelMVyoe6/IWvkPBUzaPPKrHylRyQ95lvWEhJx" +
        "9ZwAZK9beD/ATL0UhcFxflNNvzLJ9jFtmB1Ju99EZH1EDq0xLTw1RdbvtPDsvK9U6y3bWJeyi6O5aUtbt9DK6nS3" +
        "xgx1R1GWh/2liG8UOw0fikqKakmk18gPXHBrk4yqL5GGr9BIZpsP13uvGH2lQd9HhvjZbyd8SF/yodHUpvAdrBDU" +
        "jsFHaoTYvj84BYiwej7hBVtVvX/WzPg/6/71/JouOQ==";

    /// <summary>
    /// The IV that the fixed test certificate gives: the first 16 bytes of its encoded public key.
    /// </summary>
    internal static readonly byte[] FixedCertificateIv = FromHex("3082010a0282010100a4be84e27291ff");

    /// <summary>
    /// The key that the fixed test certificate gives: the last 32 bytes of its encoded public key.
    /// </summary>
    internal static readonly byte[] FixedCertificateKey = FromHex(
        "13f0853ef87dedd8107f06c930d66397" +
        "d278867dd696f9845344b50203010001");

    /// <summary>
    /// <see cref="NistPlaintext"/> encrypted with the IV and key of the fixed test certificate (80 bytes,
    /// with the PKCS7 block), computed once by hand-made CBC from single-block AES encryptions.
    /// </summary>
    internal static readonly byte[] FixedCertificateCiphertext = FromHex(
        "d0955d46203ecb81493bd98f4954644d" +
        "938edb665540c8b7387c608d070e89ca" +
        "39f01bd2b4f3967376bf09906bf0c14a" +
        "1a0b2f72432987bbc21fb5bba5956d81" +
        "9789da111a115a02ea6f54c70bd7a126");

    /// <summary>
    /// Loads the fixed test certificate from <see cref="FixedCertificateBase64"/>.
    /// </summary>
    /// <returns>The certificate, without a private key; the caller disposes it.</returns>
    internal static X509Certificate2 LoadFixedCertificate()
    {
        byte[] der = Convert.FromBase64String(FixedCertificateBase64);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(der);
#else
        return new X509Certificate2(der);
#endif
    }

    /// <summary>
    /// Converts a hexadecimal string to bytes (<c>Convert.FromHexString</c> does not exist on <c>net48</c>).
    /// </summary>
    /// <param name="hex">An even number of hexadecimal digits.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] FromHex(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
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
#pragma warning disable CS0618 // The certificate members are obsolete since 3.4.0 (B2); tested on purpose, so data written with them stays readable.
            : base(certificate)
#pragma warning restore CS0618
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
