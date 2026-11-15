namespace Forge.Next.Formatters;

/// <summary>
/// The format that the AES formatters write (<see cref="ICryptoFormatter{T}.OutputFormat"/>). Reading recognizes
/// either format by its header, whatever the formatter writes.
/// </summary>
public enum AesOutputFormat
{

    /// <summary>
    /// The format of the versions before 3.4.0, and the default: the bare AES-256-CBC ciphertext with PKCS7 padding,
    /// encrypted with the formatter's <see cref="ICryptoFormatter{T}.IV"/> and <see cref="ICryptoFormatter{T}.Key"/>,
    /// with no header and no authentication tag. A changed ciphertext is not detected, and one formatter uses one IV for
    /// every message.
    /// </summary>
    Legacy = 0,

    /// <summary>
    /// The authenticated format (since 3.4.0): the 4-byte header <c>FNF</c> and <c>0x02</c>, a random 16-byte IV per
    /// message, the AES-256-CBC ciphertext with PKCS7 padding, and a 32-byte HMAC-SHA256 tag over all of them
    /// (encrypt-then-MAC). The encryption and the authentication key are HMAC-SHA256 of
    /// <see cref="ICryptoFormatter{T}.Key"/> over two fixed labels; <see cref="ICryptoFormatter{T}.IV"/> is not used.
    /// Reading checks the tag before it decrypts, so a changed message gives an error and no output.
    /// </summary>
    Authenticated = 1,

}
