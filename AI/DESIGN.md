# Design

## The parts

All 25 types are public, one per file, in the namespace `Forge.Next.Formatters`.

| File | Kind | Role |
| --- | --- | --- |
| `IDataFormatter.cs` | interface | The contract: `ReadAsync(input)` → `T?`, `ReadAsync(input, output)`, `WriteAsync(data, output)`, each a `Task<ErrorOr<…>>` with an optional `CancellationToken` |
| `ICryptoFormatter.cs` | interface | `IDataFormatter<T>` plus `BufferSize`, `Certificate`, `IV`, `Key`, and since 3.4.0 `OutputFormat` and `AcceptUnauthenticated` |
| `AesOutputFormat.cs` | enum (since 3.4.0) | `Legacy` (0, the default) and `Authenticated` (1): the format the AES formatters write |
| `IGZipByteArrayFormatter.cs`, `IGZipStreamFormatter.cs`, `IBrotliByteArrayFormatter.cs`, `IBrotliStreamFormatter.cs` | interface | `IDataFormatter<byte[]>` or `IDataFormatter<Stream>` plus `BufferSize`; the Brotli ones exist on `net48` too |
| `IAesByteArrayFormatter.cs`, `IAesStreamFormatter.cs` | interface | `ICryptoFormatter<byte[]>` or `ICryptoFormatter<Stream>`, nothing added |
| `IXmlDataFormatter.cs`, `IXmlSoapFormatter.cs`, `ISystemJsonFormatter.cs` | interface | `IDataFormatter<T>` plus `Encoding`; nothing; `SerializerOptions` |
| `Consts.cs` | static class | `DefaultBufferSize` (8192), `LengthOfIV` (16), `LengthOfKey` (32) |
| `GZipByteArrayFormatter.cs`, `GZipStreamFormatter.cs` | class | GZip through `GZipStream`, in `BufferSize` chunks; the byte-array write passes the whole array in one call |
| `BrotliByteArrayFormatter.cs`, `BrotliStreamFormatter.cs` | class, .NET targets only | Brotli through `BrotliStream`, in `BufferSize` chunks |
| `CryptoFormatterBase.cs` | abstract class | The IV, the key, the certificate, the buffer size, the output format and `AcceptUnauthenticated`, with their checks; three protected constructors; the authenticated format's writing and the format detection of a read, as `private protected` helpers |
| `AesByteArrayFormatter.cs`, `AesStreamFormatter.cs` | class | AES through `Aes.Create()` and a `CryptoStream`, in `BufferSize` chunks |
| `XmlDataFormatter.cs` | class | A new `XmlSerializer` per call, through a `StreamReader` or `StreamWriter` with `Encoding` (UTF-8 by default) |
| `XmlSoapFormatter.cs` | class | The `SoapFormatter` of the SoapFormatter package: its own, or one passed to the constructor |
| `SystemJsonFormatter.cs` | class | `JsonSerializer` with `SerializerOptions` (`JsonSerializerOptions.Default` by default) |
| `DataFormatterExtensions.cs` | static class | `ReadAsync` / `WriteAsync` on any `IDataFormatter<T>`, for a `FileInfo` or a `Stream`, with an optional GZip layer (on by default) |
| `ServiceCollectionExtensions.cs` | static class | `AddForgeFormattersAsTransient`, `AddForgeFormattersAsScoped`, `AddForgeFormattersAsSingleton` |

## The targets

Every target references `Forge.Next.Shared` 3.3.2 and `SoapFormatter` 1.1.9. SoapFormatter ships one `netstandard2.0`
assembly without dependencies, used on every target.

| Target | `NETCOREAPP` | Through `Forge.Next.Shared` 3.3.2, besides `ErrorOr` 2.1.1 |
| --- | --- | --- |
| `net48` | no | `Microsoft.Extensions.Logging.Abstractions` 8.0.0 with `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.0, `System.Text.Json` 8.0.6 |
| `net8.0` | yes | The logging and DI abstractions 8.0.0 |
| `net9.0` | yes | The logging and DI abstractions 9.0.0 |
| `net10.0` | yes | The logging and DI abstractions 10.0.0 |
| `net11.0` | yes | Nothing else: the logging and DI abstractions are part of the `Microsoft.NETCore.App` shared framework from .NET 11 |

`net11.0` compiles the same code paths as the other .NET targets.

**What differs on `net48`:**
- no Brotli formatters and no Brotli registrations; the two Brotli interfaces exist;
- `CryptoStream.FlushFinalBlock()` and `MemoryStream.Dispose()` instead of their async forms. (The XML write's
  `StreamWriter.FlushAsync` takes no token on any target since 3.4.0, so it no longer differs.)

## One operation

1. **The checks.** The arguments, and `BufferSize > 0` where the formatter has one, are checked first; a failure returns
   a `Validation` error at once, before any work, its description the parameter name. `GZipByteArrayFormatter`'s
   `WriteAsync` checks `BufferSize` without using it.
2. **The work** runs in `ProtectAsync` from `Forge.Next.Shared`: on the formatter itself (`this.ProtectAsync(...)`) or,
   in the extensions, on `typeof(DataFormatterExtensions)`. The lambda captures the caller's token and passes it to the
   stream calls. Since 3.4.0 (B3) `ProtectAsync` also gets the token and `rethrowCancellation: true`: an
   `OperationCanceledException` thrown while the caller's token requested cancellation is rethrown; every other
   exception, an inner cancellation included, becomes `Unexpected` errors (code `General.Unexpected`, description the
   exception's message), one per exception in the inner-exception chain, with the metadata `type`, `message` and
   `stackTrace` (`includeStackTrace` at its default). `ProtectAsync` does not check the token before it runs the
   lambda; the lambdas that need that check do it themselves (step 4).
3. **The I/O.** The compression and AES formatters copy in a loop that reads `BufferSize` bytes at a time into one
   buffer and writes them on, with `ReadAsync`, `WriteAsync` and `ConfigureAwait(false)` (the two-stream `ReadAsync` of
   `AesStreamFormatter` too, since 2026-11-15; it used the synchronous `CryptoStream.Read` before). The XML and SOAP formatters call the
   synchronous `Serialize` and `Deserialize`; the JSON formatter calls `JsonSerializer`'s async methods with the token.
4. **The token** stops the compression, AES and JSON formatters through their async calls. The XML and SOAP formatters
   call `ThrowIfCancellationRequested()` once, before the synchronous serializer, and the XML write flushes without the
   token: a cancelled call does nothing, a started one completes (since 3.4.0; before, the XML read and SOAP ignored
   the token, and the XML write threw at the flush after the whole document had gone into the writer, which disposing
   it flushed anyway). The `FileInfo` overloads of `DataFormatterExtensions` check the token before they open the file,
   so a cancelled write does not truncate an existing file. The tests pin each of these.

The stream overloads of `DataFormatterExtensions` do not run in `ProtectAsync`: an exception from an application's own
`IDataFormatter<T>` passes through them, and so does the caller's cancellation from a formatter. The `FileInfo`
overloads catch everything but the caller's cancellation.

## The streams

- **The caller's streams stay open:** `GZipStream`, `BrotliStream`, `StreamReader` and `StreamWriter` are created with
  `leaveOpen: true`. The AES formatters never dispose their `CryptoStream`, and finish a write with
  `FlushFinalBlockAsync` (`FlushFinalBlock` on `net48`).
- **The returned streams** (`ReadAsync(input)` of the stream formatters) are new `MemoryStream`s that the caller owns:
  `GZipStreamFormatter`'s at position 0, `BrotliStreamFormatter`'s and `AesStreamFormatter`'s at the end (in both AES
  formats). On a failure the Brotli and AES ones are emptied and disposed before the exception becomes an error.
- **The AES read looks at the input first** (since 3.4.0): a seekable input is read 4 bytes ahead and set back to
  where it started; an input that cannot seek is copied into memory first (in both formats, so a large
  non-seekable `Legacy` input now costs memory on the two-stream read). A `Legacy` input from a seekable stream is
  still decrypted while it streams; an `Authenticated` one is read whole, checked, then decrypted.
- **The extensions:** `WriteAsync` with `compress` serializes into a `MemoryStream`, then GZips it into the target.
  `ReadAsync` with `decompress` decompresses the whole input into a `MemoryStream`, hands it to the formatter and
  disposes it. A file is opened with `FileShare.Read`: `FileMode.Open` to read, `FileMode.Create` (overwrite) to write.
- **Binding:** on a stream, `formatter.ReadAsync(stream)` and `formatter.WriteAsync(data, stream)` bind to the
  formatter's own methods, because C# prefers an applicable instance method to an extension method. The stream
  overloads of the extensions, and their `true` defaults, are reached only with the flag passed (`compress: true`) or
  through a static call (`DataFormatterExtensions.WriteAsync(formatter, data, stream)`). The `FileInfo` overloads have
  no instance counterpart. The tests call the extensions in these ways, and so does the README since 2026-11-15.

## The AES key material

- `IV` and `Key` must be 16 and 32 bytes (AES-256). The setters keep the caller's array, and the getters return the
  internal one.
- The parameterless constructor fills both from one `RandomNumberGenerator.Create()`, the same code on every target
  (since 2026-11-15, A1). Before, it used `System.Random`: `Random.Shared`, and on `net48` a `new Random()` seeded
  from the clock, which gave instances created within one tick the same IV and key.
- A certificate copies the IV from the first 16 and the key from the last 32 bytes of
  `PublicKey.EncodedKeyValue.RawData` into the current arrays. Setting `null` keeps them. For an RSA 2048 certificate
  that encoding is the DER `SEQUENCE` of the modulus and the exponent, so the first 9 bytes of the IV
  (`3082010a0282010100`) and the last 5 bytes of the key (`0203010001`, the exponent 65537) are the same for every such
  certificate with the usual exponent 65537; only 7 bytes of the IV and 27 of the key come from the modulus (checked 2026-11-15).
  Since 3.4.0 (B2) the `Certificate` property of `ICryptoFormatter<T>` and of `CryptoFormatterBase<T>`, and the three
  certificate constructors, carry `[Obsolete]` as a warning, with the same message; the library itself uses them only
  from inside these obsolete members, so it builds without warnings, and the tests suppress CS0618 around their
  deliberate uses. `Certificate_Members_AreObsoleteAsWarning_Test` pins the attributes.
- **The `Legacy` format** (the default): every call creates an `Aes` with these values. The code sets no mode or
  padding, so `Aes`'s defaults apply (CBC, PKCS7). The output has no header and no authentication tag, and one
  formatter uses one IV for every message.
- **The `Authenticated` format** (since 3.4.0, B1, opt-in through `OutputFormat`): the header `46 4E 46 02` (`FNF`
  and the version 2), a random 16-byte IV per message from `RandomNumberGenerator`, the AES-256-CBC-PKCS7 ciphertext,
  and an HMAC-SHA256 tag over the header, the IV and the ciphertext. The encryption key and the authentication key
  are HMAC-SHA256 of `Key` over the ASCII labels `Forge.Next.Formatters.Aes.Encryption` and
  `Forge.Next.Formatters.Aes.Authentication`; both are cleared after use. `IV` is not used. The write builds the
  ciphertext in a `MemoryStream`, then writes header, IV, ciphertext and tag. The read checks the length (at least
  one ciphertext block, whole blocks), the tag in constant time (an explicit loop with `NoInlining` and
  `NoOptimization`, the same on every target), then decrypts in memory, so nothing reaches the output before the
  tag matched.
- **Reading decides by the header**, whatever `OutputFormat` is. No header: `Legacy`, refused unless
  `AcceptUnauthenticated` (a `CryptographicException`, so an `Unexpected` error). The header with a wrong tag or a
  bad length: refused unless `AcceptUnauthenticated`; if accepted, decrypted as `Legacy` in memory, because a
  `Legacy` message starts with the header by chance once in 2^32. A flipped header bit makes a message look like
  `Legacy` input; while that is accepted, its streaming decryption may write some output before it fails.
- **The input must be readable**: the read checks `CanRead` first, as the `CryptoStream` constructor did.
- **Pinned by tests** (2026-11-15, A5): with the key, IV and plaintext of NIST SP 800-38A F.2.5 both AES formatters
  write NIST's ciphertext and the PKCS7 block, byte for byte, and read it back; a fixed test certificate pins the
  derivation and its output. They pass on every target, so `net48` and the .NET targets write the same bytes. The
  `Authenticated` format is pinned by a known-answer message made independently (B1): both formatters read it back
  on every target.

## The DI registrations

| Service | Implementation | `…AsTransient` | `…AsScoped` | `…AsSingleton` |
| --- | --- | --- | --- | --- |
| `IGZipByteArrayFormatter` | `GZipByteArrayFormatter` | Transient | Scoped | Singleton |
| `IGZipStreamFormatter` | `GZipStreamFormatter` | Transient | Scoped | Singleton |
| `IXmlDataFormatter<>` | `XmlDataFormatter<>` | Transient | Scoped | Singleton |
| `IBrotliStreamFormatter` | `BrotliStreamFormatter` | Transient | Scoped | Singleton |
| `IBrotliByteArrayFormatter` | `BrotliByteArrayFormatter` | Transient | Scoped | Singleton |
| `IAesByteArrayFormatter` | `AesByteArrayFormatter` | Transient | Scoped | Singleton |
| `IAesStreamFormatter` | `AesStreamFormatter` | Transient | Scoped | Singleton |
| `ISystemJsonFormatter<>` | `SystemJsonFormatter<>` | Transient | Scoped | Singleton |
| `IXmlSoapFormatter<>` | `XmlSoapFormatter<>` | Transient | Transient | Transient |

The Brotli rows exist on the .NET targets only: nine registrations there, seven on `net48`. The methods call `Add…`,
not `TryAdd…`, so a second call registers everything again. The README: the AES formatters built by the container
have a random IV and key per instance, one for the whole application with `…AsSingleton`, one per scope with
`…AsScoped`.

## Why each choice was made

The reasons below are the ones the commit messages and the README give; where none is recorded, it says so.
- **One contract:** "once you have learned one formatter you have learned them all" (README).
- **Errors as values:** failures "are surfaced as data instead of exceptions" (README). `ProtectAsync` from
  `Forge.Next.Shared` turns the exceptions of the .NET APIs into errors.
- **A byte-array and a stream variant:** a `byte[]` when the payload fits in memory, a `Stream` for larger payloads and
  for chaining formatters (README). GZip got the split in 3.1.0, to match Brotli and AES (the commit message).
- **An interface for every formatter:** the DI methods register the interfaces, and callers program against them
  (README).
- **Three DI lifetimes:** singletons when the formatters are stateless in the application's use, scoped for per-scope
  state such as a per-request AES key (README); the transient method complements the two (3.2.2). SOAP is transient in
  every variant since 3.0.0; the reason is not recorded.
- **`net48`** (3.2.1): "so the library also compiles and runs on the classic framework"; no Brotli there, because
  `System.IO.Compression.Brotli` is not available on it (the commit message).
- **The two-stream read of the serializers returns `Forbidden`:** "intentionally not implemented" (3.0.0); the reason is
  not recorded.
- **Asynchronous stream I/O** (3.2.0): the commit message describes the change, not its reason.
- **`ReadAsync` and `WriteAsync` as the names of the extensions** (3.2.5): the async naming of the rest of the library
  (the commit message).
- **GZip's returned stream at position 0** (3.2.5): left at its end, it gave the inner formatter nothing to read, and
  the compressed round-trip of the extensions failed (the commit message).
- **The extensions await inside their `using`** (3.2.5): a method that only returned a task chain disposed its streams
  before the chain ran (the commit message).
