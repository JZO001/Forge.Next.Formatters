namespace Forge.Next.Formatters;

/// <summary>
/// Contains constant values used in the Forge.Next.Formatters namespace.
/// </summary>
public static class Consts
{

    /// <summary>
    /// The default buffer size used for reading and writing data in a Formatter.
    /// </summary>
    public const int DefaultBufferSize = 8192;

    /// <summary>
    /// The length of the initialization vector (IV) used in AES encryption, which is 16 bytes (128 bits).
    /// </summary>
    public const int LengthOfIV = 16;

    /// <summary>
    /// The length of the key used in AES encryption, which is 32 bytes (256 bits).
    /// </summary>
    public const int LengthOfKey = 32;

}
