# Forge.Next.Formatters

Reference, practice and pattern implementations of **data formatters** for .NET: compression
(GZip, Brotli), symmetric encryption (AES) and serialization (XML, SOAP, JSON). Every formatter
shares one small, predictable, `async` contract and never throws for expected failures — it returns
an [`ErrorOr<T>`](https://github.com/amantinband/error-or) result instead.

- **Package version:** `3.4.0`
- **Target frameworks:** `net48` (without the Brotli formatters), `net8.0`, `net9.0`, `net10.0`, `net11.0`
- **License:** Apache-2.0
- **Package dependencies:** [`Forge.Next.Shared`](https://www.nuget.org/packages/Forge.Next.Shared), [`SoapFormatter`](https://www.nuget.org/packages/SoapFormatter)
- **Repository:** <https://github.com/JZO001/Forge.Next.Formatters>

---

## Table of contents

- [Installation](#installation)
- [Why this library](#why-this-library)
- [Core concepts](#core-concepts)
  - [The `IDataFormatter<T>` contract](#the-idataformattert-contract)
  - [The `ICryptoFormatter<T>` contract](#the-icryptoformattert-contract)
  - [Working with `ErrorOr<T>` results](#working-with-erroror-results)
  - [A note about streams and `Position`](#a-note-about-streams-and-position)
  - [A note about `BufferSize`](#a-note-about-buffersize)
  - [Cancellation](#cancellation)
- [Interfaces](#interfaces)
- [Classes and public members](#classes-and-public-members)
  - [`Consts`](#consts)
  - [`GZipByteArrayFormatter`](#gzipbytearrayformatter)
  - [`GZipStreamFormatter`](#gzipstreamformatter)
  - [`BrotliByteArrayFormatter`](#brotlibytearrayformatter)
  - [`BrotliStreamFormatter`](#brotlistreamformatter)
  - [`XmlDataFormatter<T>`](#xmldataformattert)
  - [`XmlSoapFormatter<T>`](#xmlsoapformattert)
  - [`SystemJsonFormatter<T>`](#systemjsonformattert)
  - [`CryptoFormatterBase<T>`](#cryptoformatterbaset)
  - [The AES output formats](#the-aes-output-formats)
  - [`AesByteArrayFormatter`](#aesbytearrayformatter)
  - [`AesStreamFormatter`](#aesstreamformatter)
  - [`DataFormatterExtensions`](#dataformatterextensions)
  - [`ServiceCollectionExtensions`](#servicecollectionextensions)
- [Dependency injection](#dependency-injection)
- [Recipes](#recipes)
  - [Compress and then encrypt](#compress-and-then-encrypt)
  - [Deriving keys from a certificate](#deriving-keys-from-a-certificate)
  - [Persist an object to a compressed file](#persist-an-object-to-a-compressed-file)
- [Error handling reference](#error-handling-reference)
- [Security notes](#security-notes)
- [License](#license)

---

## Installation

Install the package from NuGet:

```bash
dotnet add package Forge.Next.Formatters
```

Then import the namespace (and, when you want to inspect results, the `ErrorOr` namespace):

```csharp
using Forge.Next.Formatters;
using ErrorOr;
```

---

## Why this library

Every formatter implements the same interface, `IDataFormatter<T>`, so once you have learned one
formatter you have learned them all. The differences are only:

| Formatter | Payload type `T` | What it does |
|-----------|------------------|--------------|
| `GZipByteArrayFormatter` | `byte[]` | GZip compression / decompression |
| `GZipStreamFormatter` | `Stream` | GZip compression / decompression |
| `BrotliByteArrayFormatter` | `byte[]` | Brotli compression / decompression (not on `net48`) |
| `BrotliStreamFormatter` | `Stream` | Brotli compression / decompression (not on `net48`) |
| `XmlDataFormatter<T>` | `T` | XML serialization via `XmlSerializer` |
| `XmlSoapFormatter<T>` | `T` | SOAP serialization via `SoapFormatter` |
| `SystemJsonFormatter<T>` | `T` | JSON serialization via `System.Text.Json` |
| `AesByteArrayFormatter` | `byte[]` | AES-256 encryption / decryption |
| `AesStreamFormatter` | `Stream` | AES-256 encryption / decryption |

All operations are asynchronous and return an `ErrorOr<...>` value, so failures (a `null`
argument, a corrupt payload, a wrong key, …) are surfaced as data instead of exceptions.

The compression and encryption formatters come in two shapes:

- **`...ByteArrayFormatter`** — the payload is an in-memory `byte[]`. Convenient when the whole
  payload comfortably fits in memory.
- **`...StreamFormatter`** — the payload is a `Stream`. Convenient for larger payloads and for
  wiring formatters together without materializing an intermediate `byte[]`.

On top of the per-formatter methods, the [`DataFormatterExtensions`](#dataformatterextensions)
helpers add file/stream `ReadAsync` / `WriteAsync` overloads that can transparently GZip-(de)compress
the payload for *any* formatter.

---

## Core concepts

### The `IDataFormatter<T>` contract

Every formatter implements `IDataFormatter<T>`, which has exactly three methods:

```csharp
public interface IDataFormatter<T>
{
    // Read/decode the whole input stream and return the produced object.
    Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default);

    // Read/decode the input stream and write the produced bytes to the output stream.
    Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default);

    // Write/encode the object into the output stream.
    Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default);
}
```

Interpretation depends on the formatter:

- For a **compression** formatter, `WriteAsync` *compresses* and `ReadAsync` *decompresses*.
- For an **encryption** formatter, `WriteAsync` *encrypts* and `ReadAsync` *decrypts*.
- For a **serialization** formatter, `WriteAsync` *serializes* and `ReadAsync` *deserializes*.

> **Note:** For the three serialization formatters (`XmlDataFormatter<T>`, `XmlSoapFormatter<T>` and
> `SystemJsonFormatter<T>`) the second overload — `ReadAsync(inputStream, outputStream, …)` — is
> intentionally **not implemented** and always returns a `Forbidden` error with the description
> `"Method not implemented."`.

### The `ICryptoFormatter<T>` contract

The AES formatters implement `ICryptoFormatter<T>`, which extends `IDataFormatter<T>` with the
cryptographic configuration surface:

```csharp
public interface ICryptoFormatter<T> : IDataFormatter<T>
{
    int BufferSize { get; set; }               // read/write chunk size
    X509Certificate2? Certificate { get; set; } // derives IV + Key when set; obsolete since 3.4.0
    byte[] IV { get; set; }                    // 16-byte initialization vector
    byte[] Key { get; set; }                   // 32-byte (AES-256) key
    AesOutputFormat OutputFormat { get; set; } // what WriteAsync writes (since 3.4.0; default Legacy)
    bool AcceptUnauthenticated { get; set; }   // whether ReadAsync accepts Legacy input (since 3.4.0; default true)
}
```

These members are implemented once in [`CryptoFormatterBase<T>`](#cryptoformatterbaset) and inherited
by `AesByteArrayFormatter` and `AesStreamFormatter`.

### Working with `ErrorOr<T>` results

None of the public methods throw for an *expected* failure. Instead they return an
`ErrorOr<T>`. The two patterns you will use most:

```csharp
ErrorOr<byte[]?> result = await formatter.ReadAsync(stream);

// 1) Imperative check
if (result.IsError)
{
    Error error = result.FirstError;
    Console.WriteLine($"Failed: {error.Type} - {error.Description}");
}
else
{
    byte[]? value = result.Value;
    // use value...
}

// 2) Functional Match (from the ErrorOr package)
string message = result.Match(
    value => $"Got {value?.Length ?? 0} bytes",
    errors => $"Failed: {errors[0].Description}");
```

The library produces three kinds of errors:

| Error type | When |
|------------|------|
| `Validation` | An argument was `null`, or `BufferSize <= 0`. The arguments are checked before any work, and the `Description` is the offending parameter name, e.g. `"inputStream"` or `"BufferSize"`. |
| `Forbidden` | The unimplemented `ReadAsync(inputStream, outputStream)` overload of the serialization formatters. `Description` is `"Method not implemented."`. |
| `Unexpected` | Any exception raised inside the operation (corrupt data, a non-readable stream, a wrong AES key, a cancellation you did not request such as an inner timeout, …) is caught and returned instead of propagating. Your own cancellation is the exception: it is thrown (see [Cancellation](#cancellation)). You get **one error per exception in the inner-exception chain**, each with `Code` `"General.Unexpected"`, `Description` set to the exception's message, and `Metadata` with three entries: `"type"` (the exception's assembly-qualified type name), `"message"` and `"stackTrace"`. |

Internally the "catch every exception and turn it into an errored result" behavior is provided by
the `ProtectAsync` wrapper from `Forge.Next.Shared`, which every operation runs inside (with your
token and `rethrowCancellation: true` since 3.4.0). You never call it directly; it is the reason a
corrupt payload becomes `result.IsError == true` rather than a thrown exception.

> The `"stackTrace"` and `"message"` metadata describe the library's internals. Log them, but do not
> pass these errors unfiltered to an end user or across a trust boundary (see
> [Security notes](#security-notes)).

What does throw: the methods throw `OperationCanceledException` when your `CancellationToken`
requested cancellation (since 3.4.0); constructors and property setters throw `ArgumentNullException`
for a `null` certificate, IV, key or `SoapFormatter`, and `InvalidDataException` for an IV that is not
16 bytes or a key that is not 32 bytes.

### A note about streams and `Position`

The **single-stream** read overloads that return a `Stream` hand you back a new `MemoryStream`
that you own (dispose it when you are done). Where it is positioned depends on the formatter:

| Formatter | Position of the returned stream |
|-----------|---------------------------------|
| `GZipStreamFormatter.ReadAsync` | **0**, ready to read (since 3.2.5) |
| `BrotliStreamFormatter.ReadAsync` | **0**, ready to read (since 3.4.0; before, the end) |
| `AesStreamFormatter.ReadAsync` | **0**, ready to read (since 3.4.0; before, the end) |

```csharp
ErrorOr<Stream?> result = await formatter.ReadAsync(input);
using Stream output = result.Value!;
output.Position = 0;          // not needed since 3.4.0; harmless, and needed for older versions
```

Likewise, after `WriteAsync` fills a `MemoryStream` you must rewind it (`Position = 0`) before you
pass it to a `ReadAsync` call. The formatters never close or dispose the streams you pass in, so
you can rewind and reuse them.

### A note about `BufferSize`

The compression and encryption formatters expose an `int BufferSize` property
(default `Consts.DefaultBufferSize` = `8192` bytes) that controls the chunk size used while
streaming. When an operation reads or writes in chunks it first validates `BufferSize > 0`; a
non-positive value produces a `Validation` error whose `Description` is `"BufferSize"`.

`GZipByteArrayFormatter.WriteAsync` writes the supplied `byte[]` to the GZip stream in a single
call, so it does not use `BufferSize`, but it still validates it (since 3.1.0): a non-positive value
gives the same `Validation` error there too. The serialization formatters (XML, SOAP, JSON) do not
expose `BufferSize` at all.

### Cancellation

Every method takes a `CancellationToken` as its last parameter (`= default`). Since 3.4.0, when
**your** token requests cancellation, the method throws `OperationCanceledException` (or a subclass
such as `TaskCanceledException`), as .NET methods do; it is not returned as an error. A cancellation
that your token did not request, for example a stream's own timeout, is still an `Unexpected` error.
Before 3.4.0 every cancellation came back as an `Unexpected` error.

```csharp
try
{
    ErrorOr<byte[]?> result = await formatter.ReadAsync(input, cancellationToken);
    // handle result.IsError as usual
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // the caller cancelled
}
```

How the formatters observe the token:

| Formatter | Observes the token |
|-----------|--------------------|
| GZip, Brotli and AES formatters (both shapes) | On every read and write of the copy loop. |
| `SystemJsonFormatter<T>` | Through `JsonSerializer`. |
| `XmlDataFormatter<T>`, `XmlSoapFormatter<T>` | Once, before the synchronous serializer starts: a cancelled call reads or writes nothing; once started, the call completes. |
| `DataFormatterExtensions` | They pass the token on to the formatter and their GZip layer; the `FileInfo` overloads also check it before they open the file, so a cancelled write leaves an existing file untouched. |

---

## Interfaces

Every concrete formatter is registered and injected through an interface. The full hierarchy:

| Interface | Extends | Adds | Implemented by |
|-----------|---------|------|----------------|
| `IDataFormatter<T>` | — | `ReadAsync` (×2), `WriteAsync` | *(all formatters)* |
| `ICryptoFormatter<T>` | `IDataFormatter<T>` | `BufferSize`, `Certificate`, `IV`, `Key`, `OutputFormat`, `AcceptUnauthenticated` | `CryptoFormatterBase<T>` |
| `IGZipByteArrayFormatter` | `IDataFormatter<byte[]>` | `BufferSize` | `GZipByteArrayFormatter` |
| `IGZipStreamFormatter` | `IDataFormatter<Stream>` | `BufferSize` | `GZipStreamFormatter` |
| `IBrotliByteArrayFormatter` | `IDataFormatter<byte[]>` | `BufferSize` | `BrotliByteArrayFormatter` |
| `IBrotliStreamFormatter` | `IDataFormatter<Stream>` | `BufferSize` | `BrotliStreamFormatter` |
| `IXmlDataFormatter<T>` | `IDataFormatter<T>` | `Encoding` | `XmlDataFormatter<T>` |
| `IXmlSoapFormatter<T>` | `IDataFormatter<T>` | — | `XmlSoapFormatter<T>` |
| `ISystemJsonFormatter<T>` | `IDataFormatter<T>` | `SerializerOptions` | `SystemJsonFormatter<T>` |
| `IAesByteArrayFormatter` | `ICryptoFormatter<byte[]>` | — | `AesByteArrayFormatter` |
| `IAesStreamFormatter` | `ICryptoFormatter<Stream>` | — | `AesStreamFormatter` |

The two Brotli interfaces exist on every target, but their implementations do not exist on `net48`
(`System.IO.Compression.Brotli` is not available there), so on `net48` nothing implements them.

Program against the interfaces (they are what the
[`AddForgeFormatters…`](#servicecollectionextensions) methods register) and let the container hand
you the concrete implementation.

---

## Classes and public members

### `Consts`

Static class holding the constants shared by the formatters.

| Member | Value | Meaning |
|--------|-------|---------|
| `Consts.DefaultBufferSize` | `8192` | Default read/write buffer size (8 KB) used by the compression/encryption formatters. |
| `Consts.LengthOfIV` | `16` | Required AES initialization-vector length in bytes (128 bits). |
| `Consts.LengthOfKey` | `32` | Required AES key length in bytes (256 bits → AES-256). |

```csharp
int buffer = Consts.DefaultBufferSize;     // 8192
byte[] iv  = new byte[Consts.LengthOfIV];  // 16 bytes
byte[] key = new byte[Consts.LengthOfKey]; // 32 bytes
```

---

### `GZipByteArrayFormatter`

Compresses / decompresses a `byte[]` using GZip. Implements
`IGZipByteArrayFormatter : IDataFormatter<byte[]>`.

**Public members**

- `int BufferSize { get; set; }` — chunk size used while streaming (default `8192`).
- `Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken)` — decompress the stream into a byte array.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decompress the stream into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken)` — compress `data` into `outputStream`.

**Example — compress and decompress a byte array**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

var formatter = new GZipByteArrayFormatter
{
    BufferSize = 16 * 1024 // optional: override the 8 KB default
};

byte[] original = Encoding.UTF8.GetBytes("Hello, Forge.Next.Formatters!");

// WriteAsync -> compress into a stream
using var compressed = new MemoryStream();
ErrorOr<Success> write = await formatter.WriteAsync(original, compressed);
if (write.IsError) throw new InvalidOperationException(write.FirstError.Description);

// ReadAsync -> decompress back into a byte array
compressed.Position = 0;
ErrorOr<byte[]?> read = await formatter.ReadAsync(compressed);
byte[] restored = read.Value!;   // equals `original`
```

**Example — decompress directly into another stream**

```csharp
compressed.Position = 0;
using var destination = new MemoryStream();

ErrorOr<Success> result = await formatter.ReadAsync(compressed, destination);
if (!result.IsError)
{
    byte[] restored = destination.ToArray();
}
```

---

### `GZipStreamFormatter`

Compresses / decompresses a `Stream` using GZip. Implements
`IGZipStreamFormatter : IDataFormatter<Stream>`.

**Public members**

- `int BufferSize { get; set; }` — chunk size (default `8192`).
- `Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken)` — decompress and return a new `Stream`, positioned at 0 (since 3.2.5; earlier versions left it at the end).
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decompress into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken)` — compress the `data` stream into `outputStream`.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

var formatter = new GZipStreamFormatter();

using var source = new MemoryStream(Encoding.UTF8.GetBytes("payload to compress"));
using var compressed = new MemoryStream();

// Compress the source stream into `compressed`
await formatter.WriteAsync(source, compressed);

// Decompress: the returned stream is already at position 0
compressed.Position = 0;
ErrorOr<Stream?> read = await formatter.ReadAsync(compressed);

using Stream decompressed = read.Value!;
using var reader = new StreamReader(decompressed);
string text = await reader.ReadToEndAsync();     // "payload to compress"
```

---

### `BrotliByteArrayFormatter`

Compresses / decompresses a `byte[]` using Brotli. Implements
`IBrotliByteArrayFormatter : IDataFormatter<byte[]>`.

> Available on `net8.0` and later only; `net48` has no Brotli (see [Interfaces](#interfaces)).

**Public members**

- `int BufferSize { get; set; }` — chunk size (default `8192`).
- `Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken)` — decompress into a byte array.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decompress into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken)` — compress `data` into `outputStream`.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

var formatter = new BrotliByteArrayFormatter();

byte[] original = Encoding.UTF8.GetBytes(new string('A', 5000));

using var compressed = new MemoryStream();
await formatter.WriteAsync(original, compressed);   // compress

compressed.Position = 0;
ErrorOr<byte[]?> read = await formatter.ReadAsync(compressed); // decompress
byte[] restored = read.Value!;
```

---

### `BrotliStreamFormatter`

Compresses / decompresses a `Stream` using Brotli. Implements
`IBrotliStreamFormatter : IDataFormatter<Stream>`.

> Available on `net8.0` and later only; `net48` has no Brotli (see [Interfaces](#interfaces)).

**Public members**

- `int BufferSize { get; set; }` — chunk size (default `8192`).
- `Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken)` — decompress and return a new `Stream`, positioned at 0 (since 3.4.0; earlier versions left it at the end).
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decompress into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken)` — compress the `data` stream into `outputStream`.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

var formatter = new BrotliStreamFormatter();

using var source = new MemoryStream(Encoding.UTF8.GetBytes("payload to compress"));
using var compressed = new MemoryStream();

// Compress the source stream into `compressed`
await formatter.WriteAsync(source, compressed);

// Decompress: the returned stream is at position 0 (since 3.4.0)
compressed.Position = 0;
ErrorOr<Stream?> read = await formatter.ReadAsync(compressed);

Stream decompressed = read.Value!;
decompressed.Position = 0;                       // only needed before 3.4.0
using var reader = new StreamReader(decompressed);
string text = await reader.ReadToEndAsync();     // "payload to compress"
```

---

### `XmlDataFormatter<T>`

Serializes / deserializes an object of type `T` to XML using `System.Xml.Serialization.XmlSerializer`.
Implements `IXmlDataFormatter<T> : IDataFormatter<T>`.

> `T` must be XML-serializable: a public type with a public parameterless constructor and public
> read/write members.

**Public members**

- `Encoding Encoding { get; set; }` — text encoding used for reading and writing (default `Encoding.UTF8`).
- `Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken)` — deserialize from XML.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — **not implemented**; always returns a `Forbidden` error.
- `Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken)` — serialize `data` to XML.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var formatter = new XmlDataFormatter<Person>
{
    Encoding = Encoding.UTF8 // optional; UTF-8 is the default
};

var person = new Person { Name = "Ada", Age = 36 };

// Serialize
using var xml = new MemoryStream();
await formatter.WriteAsync(person, xml);

// Deserialize
xml.Position = 0;
ErrorOr<Person?> read = await formatter.ReadAsync(xml);
Person restored = read.Value!;   // Name = "Ada", Age = 36

// The stream-to-stream read overload is not supported:
var forbidden = await formatter.ReadAsync(new MemoryStream(), new MemoryStream());
// forbidden.IsError == true, forbidden.FirstError.Type == ErrorType.Forbidden
// forbidden.FirstError.Description == "Method not implemented."
```

---

### `XmlSoapFormatter<T>`

Serializes / deserializes an object of type `T` to SOAP XML using a
`System.Runtime.Serialization.Formatters.Soap.SoapFormatter`. Implements
`IXmlSoapFormatter<T> : IDataFormatter<T>`.

> `T` must be marked `[Serializable]`.

> **Read trusted input only.** `SoapFormatter.Deserialize` creates the types that the payload
> names, not only `T`, so untrusted SOAP input can make it instantiate other types. Unless the input
> is trusted, pass your own `SoapFormatter` whose `Binder` allows only the types you expect (see
> [Security notes](#security-notes)).

**Constructors**

- `XmlSoapFormatter()` — creates an internal `SoapFormatter`.
- `XmlSoapFormatter(SoapFormatter formatter)` — supply your own `SoapFormatter`. Throws
  `ArgumentNullException` if `formatter` is `null`.

**Public members**

- `Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken)` — deserialize from SOAP.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — **not implemented**; always returns a `Forbidden` error.
- `Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken)` — serialize `data` to SOAP.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Runtime.Serialization.Formatters.Soap;

[Serializable]
public class Order
{
    public string Product = string.Empty;
    public int Quantity;
}

// Default constructor
var formatter = new XmlSoapFormatter<Order>();

// ...or supply your own SoapFormatter:
var custom = new XmlSoapFormatter<Order>(new SoapFormatter());

var order = new Order { Product = "Widget", Quantity = 5 };

// Serialize to SOAP
using var soap = new MemoryStream();
await formatter.WriteAsync(order, soap);

// Deserialize
soap.Position = 0;
ErrorOr<Order?> read = await formatter.ReadAsync(soap);
Order restored = read.Value!;   // Product = "Widget", Quantity = 5
```

---

### `SystemJsonFormatter<T>`

Serializes / deserializes an object of type `T` to JSON using `System.Text.Json.JsonSerializer`.
Implements `ISystemJsonFormatter<T> : IDataFormatter<T>`.

**Public members**

- `JsonSerializerOptions SerializerOptions { get; set; }` — options passed to `JsonSerializer`
  (default `JsonSerializerOptions.Default`).
- `Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken)` — deserialize from JSON.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — **not implemented**; always returns a `Forbidden` error.
- `Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken)` — serialize `data` to JSON.

> Unlike the XML/SOAP formatters, `SystemJsonFormatter<T>.ReadAsync(inputStream)` does not
> pre-validate a `null` stream: a `null` argument surfaces as an `Unexpected` error rather than a
> `Validation` error. `WriteAsync` still returns a `Validation`
> error for a `null` `data` or `outputStream`.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text.Json;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var formatter = new SystemJsonFormatter<Person>
{
    // optional; defaults to JsonSerializerOptions.Default
    SerializerOptions = new JsonSerializerOptions { WriteIndented = true }
};

var person = new Person { Name = "Ada", Age = 36 };

// Serialize to JSON
using var json = new MemoryStream();
await formatter.WriteAsync(person, json);

// Deserialize
json.Position = 0;
ErrorOr<Person?> read = await formatter.ReadAsync(json);
Person restored = read.Value!;   // Name = "Ada", Age = 36

// The stream-to-stream read overload is not supported:
var forbidden = await formatter.ReadAsync(new MemoryStream(), new MemoryStream());
// forbidden.FirstError.Type == ErrorType.Forbidden, Description == "Method not implemented."
```

---

### `CryptoFormatterBase<T>`

Abstract base class for the AES formatters. Implements `ICryptoFormatter<T> : IDataFormatter<T>`
and manages the initialization vector (IV), key, optional certificate and buffer size. You do not
use this class directly; you use `AesByteArrayFormatter` / `AesStreamFormatter`, which inherit
everything below.

**Constructors** (all `protected`, exposed through the derived AES formatters)

- `CryptoFormatterBase()` — generates a random IV (16 bytes) and key (32 bytes) with
  `RandomNumberGenerator`, a cryptographic random number generator, on every target. Versions before
  3.4.0 used `System.Random`, which is predictable; on `net48` two instances created at the same moment
  could even get the same IV and key.
- `CryptoFormatterBase(X509Certificate2 certificate)` — **obsolete since 3.4.0.** Derives the IV and key
  from the certificate's **public** key. Throws `ArgumentNullException` if `certificate` is `null`. Anyone who
  has the certificate can compute the same IV and key: see [Security notes](#security-notes).
- `CryptoFormatterBase(byte[] iv, byte[] key)` — uses the supplied IV and key. Throws
  `ArgumentNullException` if either is `null` (and the property setters below validate the length).

**Public properties**

- `int BufferSize { get; set; }` — chunk size (default `8192`).
- `X509Certificate2? Certificate { get; set; }` — **obsolete since 3.4.0** (see
  [Security notes](#security-notes)). When set to a non-`null` certificate, the IV is
  copied from the first 16 bytes of the public key's raw data (`PublicKey.EncodedKeyValue.RawData`)
  and the key from the last 32 bytes, into the current IV and key arrays (so arrays you assigned to
  `IV` or `Key` before are overwritten in place: the setters keep your array, they do not copy it).
  Setting it to `null` is allowed and leaves the current IV/key untouched.
- `byte[] IV { get; set; }` — 16-byte initialization vector. The setter throws `ArgumentNullException`
  for `null` and `InvalidDataException` when the length is not exactly `Consts.LengthOfIV` (16).
- `byte[] Key { get; set; }` — 32-byte key. The setter throws `ArgumentNullException` for `null` and
  `InvalidDataException` when the length is not exactly `Consts.LengthOfKey` (32).
- `AesOutputFormat OutputFormat { get; set; }` — the format `WriteAsync` writes (since 3.4.0; default
  `AesOutputFormat.Legacy`). The setter throws `ArgumentOutOfRangeException` for an undefined value.
  See [The AES output formats](#the-aes-output-formats).
- `bool AcceptUnauthenticated { get; set; }` — whether `ReadAsync` accepts the unauthenticated
  `Legacy` format (since 3.4.0; default `true`).

**Abstract members** (implemented by the concrete AES formatters)

- `Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken)` — decrypt into `T`.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decrypt into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken)` — encrypt `data` into `outputStream`.

**Example — reading and validating the crypto configuration** (via `AesByteArrayFormatter`)

```csharp
using Forge.Next.Formatters;

var formatter = new AesByteArrayFormatter(); // random IV + key

Console.WriteLine(formatter.IV.Length);   // 16
Console.WriteLine(formatter.Key.Length);  // 32
Console.WriteLine(formatter.BufferSize);  // 8192

// Assigning material of the wrong size is rejected:
try
{
    formatter.IV = new byte[8]; // must be 16
}
catch (InvalidDataException)
{
    // handled
}
```

---

### The AES output formats

Since 3.4.0 the AES formatters can write two formats, chosen with `OutputFormat` (the enum
`AesOutputFormat`). **Reading recognizes both by their header**, whatever `OutputFormat` is.

| Format | What `WriteAsync` writes | Protection |
|--------|--------------------------|------------|
| `AesOutputFormat.Legacy` (the default) | The bare AES-256-CBC ciphertext with PKCS7 padding, encrypted with the formatter's `IV` and `Key`: the format of every earlier version. | Confidentiality only. A changed ciphertext is not detected, and one formatter uses one IV for every message. |
| `AesOutputFormat.Authenticated` | The 4-byte header `FNF` `0x02`, a random 16-byte IV per message, the AES-256-CBC ciphertext with PKCS7 padding, and a 32-byte HMAC-SHA256 tag over all of them (encrypt-then-MAC). The encryption and the authentication key are derived from `Key` (HMAC-SHA256 over two fixed labels); `IV` is not used. | Confidentiality and integrity: the tag is checked, in constant time, before anything is decrypted, so a changed or cut-off message gives an error and **nothing reaches the output**. |

`AcceptUnauthenticated` (default `true`) decides whether reading accepts the `Legacy` format. Set it
to `false` once all your data is authenticated: input without a valid tag then gives an
`Unexpected` error. While it is `true`, a message with the header but a wrong tag is also tried as
`Legacy` input, because a `Legacy` message starts with the header bytes by chance once in 2^32.

**Moving to the authenticated format:**

1. Update every reader to 3.4.0 or later: it reads both formats.
2. Set `OutputFormat = AesOutputFormat.Authenticated` on the writers. The formatters built by the
   `AddForgeFormatters…` methods start with `Legacy`; set it on the resolved instance, or register
   your own.
3. Once the old data has been read and written again, set `AcceptUnauthenticated = false`.

```csharp
var formatter = new AesByteArrayFormatter(iv, key)
{
    OutputFormat = AesOutputFormat.Authenticated, // write the authenticated format
    AcceptUnauthenticated = false,               // and refuse anything else
};
```

**Memory:** the authenticated format is built and checked as a whole, so writing and reading keep the
whole message in memory, also with `AesStreamFormatter`. Reading also copies an input stream that
cannot seek into memory first, in both formats, so it can look at the header and read it again; a
seekable input is read from its current position and goes back there after the header is checked.

---

### `AesByteArrayFormatter`

Encrypts / decrypts a `byte[]` with AES-256. Inherits everything from
[`CryptoFormatterBase<byte[]>`](#cryptoformatterbaset) and implements `IAesByteArrayFormatter`.

**Constructors**

- `AesByteArrayFormatter()` — random IV and key.
- `AesByteArrayFormatter(X509Certificate2 certificate)` — IV/key derived from the certificate's public key;
  **obsolete since 3.4.0** (see [Deriving keys from a certificate](#deriving-keys-from-a-certificate)).
- `AesByteArrayFormatter(byte[] iv, byte[] key)` — explicit 16-byte IV and 32-byte key.

**Public members**

- `Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken)` — decrypt into a byte array.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decrypt into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken)` — encrypt `data` into `outputStream`.
- Plus the inherited `IV`, `Key`, `Certificate`, `BufferSize` members.

> To decrypt data you must use the **same key** that was used to encrypt it, and in the default
> `Legacy` format the same IV too: that output is AES-256-CBC with PKCS7 padding and nothing else, no
> header, no IV, no authentication tag. See [The AES output formats](#the-aes-output-formats) for the
> authenticated format, and [Security notes](#security-notes).

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

// Fixed IV (16 bytes) and key (32 bytes) so we can decrypt later. For illustration only: in real
// code use random values (see the AesStreamFormatter example) and keep the key secret.
byte[] iv  = new byte[16] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16 };
byte[] key = new byte[32] {
    1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,
    17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32 };

var formatter = new AesByteArrayFormatter(iv, key);

byte[] plaintext = Encoding.UTF8.GetBytes("Top secret");

// Encrypt
using var cipher = new MemoryStream();
await formatter.WriteAsync(plaintext, cipher);

// Decrypt (same iv/key)
cipher.Position = 0;
ErrorOr<byte[]?> read = await formatter.ReadAsync(cipher);
byte[] decrypted = read.Value!;   // equals `plaintext`
```

---

### `AesStreamFormatter`

Encrypts / decrypts a `Stream` with AES-256. Inherits everything from
[`CryptoFormatterBase<Stream>`](#cryptoformatterbaset) and implements `IAesStreamFormatter`.

**Constructors**

- `AesStreamFormatter()` — random IV and key.
- `AesStreamFormatter(X509Certificate2 certificate)` — IV/key derived from the certificate's public key;
  **obsolete since 3.4.0** (see [Deriving keys from a certificate](#deriving-keys-from-a-certificate)).
- `AesStreamFormatter(byte[] iv, byte[] key)` — explicit 16-byte IV and 32-byte key.

**Public members**

- `Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken)` — decrypt and return a new `Stream`, positioned at 0 (since 3.4.0; earlier versions left it at the end).
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decrypt into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken)` — encrypt the `data` stream into `outputStream`.
- Plus the inherited `IV`, `Key`, `Certificate`, `BufferSize` members.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Security.Cryptography;
using System.Text;

byte[] iv  = new byte[16];
byte[] key = new byte[32];
using (var rng = RandomNumberGenerator.Create())   // cryptographic random values, on every target
{
    rng.GetBytes(iv);
    rng.GetBytes(key);
}

var formatter = new AesStreamFormatter(iv, key);

using var source = new MemoryStream(Encoding.UTF8.GetBytes("stream secret"));
using var cipher = new MemoryStream();

// Encrypt the source stream
await formatter.WriteAsync(source, cipher);

// Decrypt into a returned stream (at position 0 since 3.4.0)
cipher.Position = 0;
ErrorOr<Stream?> read = await formatter.ReadAsync(cipher);

Stream plain = read.Value!;
plain.Position = 0;                         // only needed before 3.4.0
using var reader = new StreamReader(plain);
string message = await reader.ReadToEndAsync(); // "stream secret"
```

---

### `DataFormatterExtensions`

Static class with `ReadAsync` / `WriteAsync` extension methods on **any** `IDataFormatter<T>`
(named `Read` / `Write` before 3.2.5). They add two conveniences over the formatters' own
`ReadAsync` / `WriteAsync` methods:

1. **File or stream** overloads — pass a `FileInfo` and the file is opened for you (`FileShare.Read`);
   writing creates the file or **overwrites** it.
2. **Transparent GZip** — a `compress` / `decompress` flag (which **defaults to `true`**) wraps the
   payload in a GZip layer using an internal `GZipStreamFormatter`.

> **Important — which method you call on a stream:** C# prefers a type's own method to an extension
> method, so `formatter.WriteAsync(data, stream)` and `formatter.ReadAsync(stream)` call the
> **formatter's own** methods, *without* GZip, even though the extensions default to `true`. To get
> the GZip layer on a stream, pass the flag (`formatter.WriteAsync(data, stream, compress: true)`,
> `formatter.ReadAsync(stream, decompress: true)`) or call the extension statically
> (`DataFormatterExtensions.WriteAsync(formatter, data, stream)`). The `FileInfo` overloads have no
> counterpart on the formatters, so `formatter.WriteAsync(data, file)` always reaches the extension
> and compresses by default. Use the same setting on both sides.

**Public members**

- `Task<ErrorOr<T?>> ReadAsync<T>(this IDataFormatter<T> formatter, FileInfo file, bool decompress = true, CancellationToken cancellationToken = default)` — open `file`, read/decode it (GZip-decompressing first when `decompress`).
- `Task<ErrorOr<T?>> ReadAsync<T>(this IDataFormatter<T> formatter, Stream stream, bool decompress = true, CancellationToken cancellationToken = default)` — read/decode `stream` (GZip-decompressing first when `decompress`).
- `Task<ErrorOr<Success>> WriteAsync<T>(this IDataFormatter<T> formatter, T data, FileInfo file, bool compress = true, CancellationToken cancellationToken = default)` — write/encode `data` to `file` (GZip-compressing when `compress`).
- `Task<ErrorOr<Success>> WriteAsync<T>(this IDataFormatter<T> formatter, T data, Stream stream, bool compress = true, CancellationToken cancellationToken = default)` — write/encode `data` to `stream` (GZip-compressing when `compress`).

A `null` `formatter`, `data`, `file` or `stream` returns a `Validation` error naming the offending
argument. With the GZip layer, the whole payload passes through memory: `WriteAsync` serializes into
a `MemoryStream` before compressing it, and `ReadAsync` decompresses the whole input into a
`MemoryStream` before the formatter reads it.

> The `FileInfo` overloads catch every exception except your own cancellation, like the formatters do.
> The `Stream` overloads do not wrap the formatter call: the library's formatters throw nothing there
> but your own cancellation, while any exception thrown by your own `IDataFormatter<T>`
> implementation passes through them.

**Example — write and read a compressed file**

```csharp
using Forge.Next.Formatters;
using ErrorOr;

var formatter = new SystemJsonFormatter<Person>();
var person = new Person { Name = "Ada", Age = 36 };
var file = new FileInfo("person.json.gz");

// Serialize to JSON and GZip-compress into the file (compress: true is the default)
ErrorOr<Success> write = await formatter.WriteAsync(person, file);

// Read it back, GZip-decompressing first (decompress: true is the default)
ErrorOr<Person?> read = await formatter.ReadAsync(file);
Person restored = read.Value!;   // Name = "Ada", Age = 36
```

**Example — a stream, with and without the GZip layer**

```csharp
using var buffer = new MemoryStream();

// GZip on a stream: pass the flag, otherwise the formatter's own WriteAsync is called.
await formatter.WriteAsync(person, buffer, compress: true);
buffer.Position = 0;
ErrorOr<Person?> unzipped = await formatter.ReadAsync(buffer, decompress: true);

// Plain JSON, no compression: the same as the formatter's own methods.
buffer.SetLength(0);
await formatter.WriteAsync(person, buffer, compress: false);
buffer.Position = 0;
ErrorOr<Person?> plain = await formatter.ReadAsync(buffer, decompress: false);
```

Because the methods extend `IDataFormatter<T>`, they compose with every formatter in the library —
e.g. `xmlFormatter.WriteAsync(model, file)` for gzipped XML in a file. Compress before you encrypt
(see [Compress and then encrypt](#compress-and-then-encrypt)): encrypted data does not compress.

---

### `ServiceCollectionExtensions`

Registers the formatters with the Microsoft dependency-injection container. Three entry points are
provided that differ only in the lifetime used for the non-SOAP formatters.

**Public members**

- `IServiceCollection AddForgeFormattersAsTransient(this IServiceCollection services)` — registers
  the formatters below as **transient** and returns the same collection (since 3.2.2).
- `IServiceCollection AddForgeFormattersAsScoped(this IServiceCollection services)` — registers the
  formatters below as **scoped** (SOAP is transient) and returns the same collection.
- `IServiceCollection AddForgeFormattersAsSingleton(this IServiceCollection services)` — registers
  the formatters below as **singletons** (SOAP is transient) and returns the same collection.

  | Service | Implementation | `…AsTransient` | `…AsScoped` | `…AsSingleton` |
  |---------|----------------|----------------|-------------|----------------|
  | `IGZipByteArrayFormatter` | `GZipByteArrayFormatter` | Transient | Scoped | Singleton |
  | `IGZipStreamFormatter` | `GZipStreamFormatter` | Transient | Scoped | Singleton |
  | `IXmlDataFormatter<>` | `XmlDataFormatter<>` | Transient | Scoped | Singleton |
  | `IBrotliStreamFormatter` | `BrotliStreamFormatter` | Transient | Scoped | Singleton |
  | `IBrotliByteArrayFormatter` | `BrotliByteArrayFormatter` | Transient | Scoped | Singleton |
  | `IAesByteArrayFormatter` | `AesByteArrayFormatter` | Transient | Scoped | Singleton |
  | `IAesStreamFormatter` | `AesStreamFormatter` | Transient | Scoped | Singleton |
  | `ISystemJsonFormatter<>` | `SystemJsonFormatter<>` | Transient | Scoped | Singleton |
  | `IXmlSoapFormatter<>` | `XmlSoapFormatter<>` | Transient | Transient | Transient |

  On `net48` the two Brotli rows are not registered, so each method adds seven services there, nine
  on the other targets. The methods use `Add…`, not `TryAdd…`: calling one twice registers
  everything twice.

```csharp
using Forge.Next.Formatters;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddForgeFormattersAsScoped(); // or AddForgeFormattersAsTransient() / AddForgeFormattersAsSingleton()

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var gzip = scope.ServiceProvider.GetRequiredService<IGZipByteArrayFormatter>();
var json = scope.ServiceProvider.GetRequiredService<ISystemJsonFormatter<Person>>();
var xml  = scope.ServiceProvider.GetRequiredService<IXmlDataFormatter<Person>>();
```

---

## Dependency injection

The `AddForgeFormatters…` methods are designed for ASP.NET Core / generic-host applications.
Register once at startup and inject the interfaces where you need them. Pick
`AddForgeFormattersAsSingleton` when the formatters are stateless in your usage,
`AddForgeFormattersAsScoped` when you configure per-scope state (for example a per-request AES key),
or `AddForgeFormattersAsTransient` when every consumer should get its own instance.

```csharp
using Forge.Next.Formatters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddForgeFormattersAsScoped(); // or AddForgeFormattersAsTransient() / AddForgeFormattersAsSingleton()

var app = builder.Build();
```

```csharp
public sealed class ArchiveService
{
    private readonly IGZipByteArrayFormatter _gzip;
    private readonly IAesByteArrayFormatter _aes;

    // The formatters are injected by the container.
    public ArchiveService(IGZipByteArrayFormatter gzip, IAesByteArrayFormatter aes)
    {
        _gzip = gzip;
        _aes = aes;
    }

    public async Task<byte[]> PackAsync(byte[] payload, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var result = await _gzip.WriteAsync(payload, buffer, ct);
        if (result.IsError)
            throw new InvalidOperationException(result.FirstError.Description);

        return buffer.ToArray();
    }
}
```

> The AES default constructors generate a **random** IV/key per instance, with a cryptographic random
> number generator (`RandomNumberGenerator`). With
> `AddForgeFormattersAsSingleton` the same key lives for the whole application; with
> `AddForgeFormattersAsScoped` a new key is created per scope; with `AddForgeFormattersAsTransient`
> every resolution gets a new key, so a formatter injected into one service cannot decrypt what
> another one encrypted. In every case, if you need a *stable* key across instances or processes,
> configure `IV`/`Key` yourself (or register your own instance) rather than relying on the
> generated one.

---

## Recipes

### Compress and then encrypt

A common pipeline is to compress a payload and then encrypt the compressed bytes.

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Security.Cryptography;
using System.Text;

byte[] iv  = new byte[16];
byte[] key = new byte[32];
using (var rng = RandomNumberGenerator.Create())
{
    rng.GetBytes(iv);
    rng.GetBytes(key);
}

var gzip = new GZipByteArrayFormatter();
var aes  = new AesByteArrayFormatter(iv, key);

byte[] payload = Encoding.UTF8.GetBytes(new string('X', 10_000));

// 1) Compress
using var compressed = new MemoryStream();
await gzip.WriteAsync(payload, compressed);

// 2) Encrypt the compressed bytes
using var encrypted = new MemoryStream();
await aes.WriteAsync(compressed.ToArray(), encrypted);

byte[] packaged = encrypted.ToArray();

// --- To restore: decrypt, then decompress ---
using var decryptInput = new MemoryStream(packaged);
ErrorOr<byte[]?> decrypted = await aes.ReadAsync(decryptInput);

using var decompressInput = new MemoryStream(decrypted.Value!);
ErrorOr<byte[]?> restored = await gzip.ReadAsync(decompressInput);
// restored.Value equals `payload`
```

### Deriving keys from a certificate

> **Warning: this is not confidential encryption.** The IV and key come from the certificate's
> **public** key alone, and a certificate is public by nature: anyone who has it can compute the
> same IV and key and decrypt the data. No private key is involved. Use this only where the data
> needs no protection from whoever has the certificate, for example to read data written this way
> earlier. For confidentiality, use a secret key of your own (see [Security notes](#security-notes)).
>
> Since 3.4.0 the certificate constructors and the `Certificate` property are marked `[Obsolete]`, so
> using them gives the compiler warning CS0618. They still work exactly as before, so data written
> this way stays readable; where you call them on purpose, suppress the warning there.

When you construct an AES formatter with an `X509Certificate2`, its IV and key are derived
deterministically from the certificate's public key, so two formatters built from the same
certificate read each other's output:

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Security.Cryptography.X509Certificates;
using System.Text;

X509Certificate2 certificate = LoadCertificate(); // your certificate

#pragma warning disable CS0618 // obsolete since 3.4.0: used on purpose, to read data written this way
var writer = new AesByteArrayFormatter(certificate);
var reader = new AesByteArrayFormatter(certificate); // same derived IV/key
#pragma warning restore CS0618

using var cipher = new MemoryStream();
await writer.WriteAsync(Encoding.UTF8.GetBytes("hello"), cipher);

cipher.Position = 0;
ErrorOr<byte[]?> read = await reader.ReadAsync(cipher);
// read.Value == UTF8 bytes of "hello"
```

### Persist an object to a compressed file

The [`DataFormatterExtensions`](#dataformatterextensions) helpers turn "serialize + compress +
write to disk" into a single call (and the reverse for loading).

```csharp
using Forge.Next.Formatters;
using ErrorOr;

var formatter = new SystemJsonFormatter<Person>(); // any IDataFormatter<T> works
var file = new FileInfo("people/ada.json.gz");
file.Directory!.Create();

var person = new Person { Name = "Ada", Age = 36 };

// Serialize to JSON, GZip-compress, and write to the file in one call (overwrites the file).
ErrorOr<Success> saved = await formatter.WriteAsync(person, file);  // compress: true (default)

// Load: decompress and deserialize in one call.
ErrorOr<Person?> loaded = await formatter.ReadAsync(file);          // decompress: true (default)
Person restored = loaded.Value!;
```

---

## Error handling reference

Every method returns an `ErrorOr<...>`; inspect it instead of catching exceptions.

```csharp
using Forge.Next.Formatters;
using ErrorOr;

var formatter = new GZipByteArrayFormatter();

// null argument -> Validation error, Description = parameter name
ErrorOr<Success> nullData = await formatter.WriteAsync(null!, new MemoryStream());
// nullData.IsError == true
// nullData.FirstError.Type == ErrorType.Validation
// nullData.FirstError.Description == "data"

// BufferSize <= 0 (on chunked reads/writes) -> Validation error
formatter.BufferSize = 0;
ErrorOr<byte[]?> badBuffer = await formatter.ReadAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
// badBuffer.FirstError.Description == "BufferSize"

// Corrupt/undecodable input -> Unexpected error (not an exception)
var aes = new AesByteArrayFormatter();                 // random key
var garbage = new MemoryStream(new byte[] { 9, 9, 9 });
ErrorOr<byte[]?> corrupt = await aes.ReadAsync(garbage);
// corrupt.IsError == true
// corrupt.FirstError.Type == ErrorType.Unexpected
// corrupt.FirstError.Metadata["type"] names the exception; "message" and "stackTrace" too

// Your own cancellation -> OperationCanceledException is thrown (since 3.4.0), not returned
formatter.BufferSize = Consts.DefaultBufferSize;
try
{
    await formatter.WriteAsync(new byte[] { 1 }, new MemoryStream(), new CancellationToken(canceled: true));
}
catch (OperationCanceledException)
{
    // cancelled
}
```

Handy `ErrorOr<T>` members:

- `IsError` — `true` when the operation failed.
- `Value` — the successful result (only valid when `IsError` is `false`).
- `FirstError` — the first `Error`; use `.Type`, `.Code`, `.Description`.
- `Errors` — the full list of `Error` values.
- `Match(...)` / `MatchFirst(...)` / `Switch(...)` — functional handling.

---

## Security notes

- **The default AES output is not authenticated.** The `Legacy` format, still the default, is
  AES-256-CBC with PKCS7 padding, with no header and no authentication tag: a modified ciphertext
  decrypts to modified data, or fails on the padding, without the change being detected as such.
  Since 3.4.0, write `AesOutputFormat.Authenticated` and set `AcceptUnauthenticated = false` (see
  [The AES output formats](#the-aes-output-formats)); as long as `Legacy` input is accepted, an
  attacker can still hand you `Legacy` input.
- **In the `Legacy` format one formatter uses one IV for every message.** The IV is a property of the
  formatter, not of the message, so two messages that begin with the same bytes give ciphertexts that
  begin with the same bytes. The `Authenticated` format uses a new random IV per message.
- **Keys derived from a certificate are not secret.** The IV and key come from the certificate's
  public key, so anyone who has the certificate can decrypt (see
  [Deriving keys from a certificate](#deriving-keys-from-a-certificate)); the certificate members are
  obsolete since 3.4.0. For an RSA 2048
  certificate with the usual exponent 65537, moreover, 9 of the 16 IV bytes and 5 of the 32 key
  bytes are the same for every certificate (the DER header and the exponent).
- **The generated keys.** Since 3.4.0 the parameterless AES constructors use a cryptographic random
  number generator. Earlier versions used `System.Random`, which is predictable, and on `net48`
  formatters created at the same moment got the same key; treat data encrypted with such a
  generated key as weakly protected.
- **SOAP deserialization creates the types the payload names.** `XmlSoapFormatter<T>.ReadAsync`
  uses `SoapFormatter.Deserialize`, which instantiates whatever types the SOAP input names, not
  only `T`. Read trusted input only, or pass a `SoapFormatter` whose `Binder` allows only the types
  you expect.
- **Errors carry internals.** An `Unexpected` error's metadata holds the exception's type, message
  and stack trace. Log it, but do not send it unfiltered to an end user or another system.

---

## License

Copyright © Zoltan Juhasz. Licensed under the [Apache License 2.0](LICENSE).
