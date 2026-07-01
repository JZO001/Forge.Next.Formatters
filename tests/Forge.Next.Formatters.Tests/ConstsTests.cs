using Forge.Next.Formatters;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// Unit tests for the <see cref="Consts"/> static class.
/// <para>
/// <see cref="Consts"/> only exposes compile-time constants, so these tests act as
/// "guard" tests: they lock down the documented values. If someone accidentally
/// changes one of the constants (for example the AES key length) the corresponding
/// test fails and signals that a breaking change was introduced, because the whole
/// cryptographic pipeline depends on those exact sizes.
/// </para>
/// </summary>
public class ConstsTests
{
    /// <summary>
    /// Verifies that the default buffer size used by every formatter is 8192 bytes (8 KB).
    /// This is the chunk size used while streaming data, so the value must remain stable.
    /// </summary>
    [Fact]
    public void DefaultBufferSizeTest()
    {
        // The constant is expected to be exactly 8 KB (8 * 1024 == 8192).
        Assert.Equal(8192, Consts.DefaultBufferSize);
    }

    /// <summary>
    /// Verifies that the initialization vector (IV) length is 16 bytes (128 bits),
    /// which is the fixed AES block size. AES cannot work with any other IV length.
    /// </summary>
    [Fact]
    public void LengthOfIVTest()
    {
        // AES always uses a 128-bit (16 byte) block, so the IV must be 16 bytes.
        Assert.Equal(16, Consts.LengthOfIV);
    }

    /// <summary>
    /// Verifies that the key length is 32 bytes (256 bits), i.e. AES-256.
    /// The <see cref="CryptoFormatterBase{T}"/> validates keys against this value.
    /// </summary>
    [Fact]
    public void LengthOfKeyTest()
    {
        // A 256-bit AES key is 32 bytes long.
        Assert.Equal(32, Consts.LengthOfKey);
    }
}
