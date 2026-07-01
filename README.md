# Forge.Next.Formatters

Reference, practice and pattern implementations of **data formatters** for .NET: compression
(GZip, Brotli), symmetric encryption (AES) and serialization (XML, SOAP). Every formatter shares
one small, predictable, `async` contract and never throws for expected failures — it returns an
[`ErrorOr<T>`](https://github.com/amantinband/error-or) result instead.

- **Package version:** `3.1.0`
- **Target frameworks:** `net8.0`, `net9.0`, `net10.0`
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
- [Interfaces](#interfaces)
- [Classes and public members](#classes-and-public-members)
  - [`Consts`](#consts)
  - [`GZipByteArrayFormatter`](#gzipbytearrayformatter)
  - [`GZipStreamFormatter`](#gzipstreamformatter)
  - [`BrotliByteArrayFormatter`](#brotlibytearrayformatter)
  - [`BrotliStreamFormatter`](#brotlistreamformatter)
  - [`XmlDataFormatter<T>`](#xmldataformattert)
  - [`XmlSoapFormatter<T>`](#xmlsoapformattert)
  - [`CryptoFormatterBase<T>`](#cryptoformatterbaset)
  - [`AesByteArrayFormatter`](#aesbytearrayformatter)
  - [`AesStreamFormatter`](#aesstreamformatter)
  - [`ServiceCollectionExtensions`](#servicecollectionextensions)
- [Dependency injection](#dependency-injection)
- [Recipes](#recipes)
  - [Compress and then encrypt](#compress-and-then-encrypt)
  - [Deriving keys from a certificate](#deriving-keys-from-a-certificate)
- [Error handling reference](#error-handling-reference)
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
| `BrotliByteArrayFormatter` | `byte[]` | Brotli compression / decompression |
| `BrotliStreamFormatter` | `Stream` | Brotli compression / decompression |
| `XmlDataFormatter<T>` | `T` | XML serialization via `XmlSerializer` |
| `XmlSoapFormatter<T>` | `T` | SOAP serialization via `SoapFormatter` |
| `AesByteArrayFormatter` | `byte[]` | AES-256 encryption / decryption |
| `AesStreamFormatter` | `Stream` | AES-256 encryption / decryption |

All operations are asynchronous and return an `ErrorOr<...>` value, so failures (a `null`
argument, a corrupt payload, a wrong key, …) are surfaced as data instead of exceptions.

There are two shapes of every formatter:

- **`...ByteArrayFormatter`** — the payload is an in-memory `byte[]`. Convenient when the whole
  payload comfortably fits in memory.
- **`...StreamFormatter`** — the payload is a `Stream`. Convenient for larger payloads and for
  wiring formatters together without materializing an intermediate `byte[]`.

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

> **Note:** For the two serialization formatters (`XmlDataFormatter<T>` and `XmlSoapFormatter<T>`)
> the second overload — `ReadAsync(inputStream, outputStream, …)` — is intentionally **not
> implemented** and always returns a `Forbidden` error with the description
> `"Method not implemented."`.

### The `ICryptoFormatter<T>` contract

The AES formatters implement `ICryptoFormatter<T>`, which extends `IDataFormatter<T>` with the
cryptographic configuration surface:

```csharp
public interface ICryptoFormatter<T> : IDataFormatter<T>
{
    int BufferSize { get; set; }               // read/write chunk size
    X509Certificate2? Certificate { get; set; } // derives IV + Key when set
    byte[] IV { get; set; }                    // 16-byte initialization vector
    byte[] Key { get; set; }                   // 32-byte (AES-256) key
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
| `Validation` | An argument was `null`, or `BufferSize <= 0`. The `Description` is the offending parameter name, e.g. `"inputStream"` or `"BufferSize"`. |
| `Forbidden` | The unimplemented `ReadAsync(inputStream, outputStream)` overload of the serialization formatters. `Description` is `"Method not implemented."`. |
| *(failure)* | Any exception raised inside the operation (corrupt data, a non-readable stream, a wrong AES key, …) is caught and returned as an errored result — `IsError` is `true` — instead of propagating. |

Internally the "catch every exception and turn it into an errored result" behavior is provided by
the `ProtectAsync` wrapper from `Forge.Next.Shared`, which every operation runs inside. You never
call it directly; it is the reason a corrupt payload becomes `result.IsError == true` rather than a
thrown exception.

### A note about streams and `Position`

The **single-stream** read overloads that return a `Stream`
(`GZipStreamFormatter.ReadAsync`, `BrotliStreamFormatter.ReadAsync` and
`AesStreamFormatter.ReadAsync`) hand you back a `MemoryStream` that is positioned at **the end** of
the data it just produced. Rewind it before reading:

```csharp
ErrorOr<Stream?> result = await formatter.ReadAsync(input);
Stream output = result.Value!;
output.Position = 0;          // <-- rewind before reading
```

Likewise, after `WriteAsync` fills a `MemoryStream` you must rewind it (`Position = 0`) before you
pass it to a `ReadAsync` call.

### A note about `BufferSize`

Every formatter exposes an `int BufferSize` property (default `Consts.DefaultBufferSize` = `8192`
bytes) that controls the chunk size used while streaming. When an operation reads or writes in
chunks it first validates `BufferSize > 0`; a non-positive value produces a `Validation` error
whose `Description` is `"BufferSize"`.

The only exception is `GZipByteArrayFormatter.WriteAsync`, which writes the supplied `byte[]` to the
GZip stream in a single call and therefore does not consult `BufferSize`.

---

## Interfaces

Every concrete formatter is registered and injected through an interface. The full hierarchy:

| Interface | Extends | Adds | Implemented by |
|-----------|---------|------|----------------|
| `IDataFormatter<T>` | — | `ReadAsync` (×2), `WriteAsync` | *(all formatters)* |
| `ICryptoFormatter<T>` | `IDataFormatter<T>` | `BufferSize`, `Certificate`, `IV`, `Key` | `CryptoFormatterBase<T>` |
| `IGZipByteArrayFormatter` | `IDataFormatter<byte[]>` | `BufferSize` | `GZipByteArrayFormatter` |
| `IGZipStreamFormatter` | `IDataFormatter<Stream>` | `BufferSize` | `GZipStreamFormatter` |
| `IBrotliByteArrayFormatter` | `IDataFormatter<byte[]>` | `BufferSize` | `BrotliByteArrayFormatter` |
| `IBrotliStreamFormatter` | `IDataFormatter<Stream>` | `BufferSize` | `BrotliStreamFormatter` |
| `IXmlDataFormatter<T>` | `IDataFormatter<T>` | `Encoding` | `XmlDataFormatter<T>` |
| `IXmlSoapFormatter<T>` | `IDataFormatter<T>` | — | `XmlSoapFormatter<T>` |
| `IAesByteArrayFormatter` | `ICryptoFormatter<byte[]>` | — | `AesByteArrayFormatter` |
| `IAesStreamFormatter` | `ICryptoFormatter<Stream>` | — | `AesStreamFormatter` |

Program against the interfaces (they are what [`AddForgeFormatters`](#servicecollectionextensions)
registers) and let the container hand you the concrete implementation.

---

## Classes and public members

### `Consts`

Static class holding the constants shared by the formatters.

| Member | Value | Meaning |
|--------|-------|---------|
| `Consts.DefaultBufferSize` | `8192` | Default read/write buffer size (8 KB) used by every formatter. |
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
- `Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken)` — decompress and return a `Stream` (positioned at the end — rewind before reading).
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

// Decompress: the returned stream is positioned at its END
compressed.Position = 0;
ErrorOr<Stream?> read = await formatter.ReadAsync(compressed);

Stream decompressed = read.Value!;
decompressed.Position = 0;                       // rewind!
using var reader = new StreamReader(decompressed);
string text = await reader.ReadToEndAsync();     // "payload to compress"
```

---

### `BrotliByteArrayFormatter`

Compresses / decompresses a `byte[]` using Brotli. Implements
`IBrotliByteArrayFormatter : IDataFormatter<byte[]>`.

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

**Public members**

- `int BufferSize { get; set; }` — chunk size (default `8192`).
- `Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken)` — decompress and return a `Stream` (positioned at the end — rewind before reading).
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

// Decompress: the returned stream is positioned at its END
compressed.Position = 0;
ErrorOr<Stream?> read = await formatter.ReadAsync(compressed);

Stream decompressed = read.Value!;
decompressed.Position = 0;                       // rewind!
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

### `CryptoFormatterBase<T>`

Abstract base class for the AES formatters. Implements `ICryptoFormatter<T> : IDataFormatter<T>`
and manages the initialization vector (IV), key, optional certificate and buffer size. You do not
use this class directly; you use `AesByteArrayFormatter` / `AesStreamFormatter`, which inherit
everything below.

**Constructors** (all `protected`, exposed through the derived AES formatters)

- `CryptoFormatterBase()` — generates a random IV (16 bytes) and key (32 bytes) via `Random.Shared`.
- `CryptoFormatterBase(X509Certificate2 certificate)` — derives the IV and key from the
  certificate's public key. Throws `ArgumentNullException` if `certificate` is `null`.
- `CryptoFormatterBase(byte[] iv, byte[] key)` — uses the supplied IV and key. Throws
  `ArgumentNullException` if either is `null` (and the property setters below validate the length).

**Public properties**

- `int BufferSize { get; set; }` — chunk size (default `8192`).
- `X509Certificate2? Certificate { get; set; }` — when set to a non-`null` certificate, the IV is
  copied from the first 16 bytes of the public key's raw data and the key from the last 32 bytes.
  Setting it to `null` is allowed and leaves the current IV/key untouched.
- `byte[] IV { get; set; }` — 16-byte initialization vector. The setter throws `ArgumentNullException`
  for `null` and `InvalidDataException` when the length is not exactly `Consts.LengthOfIV` (16).
- `byte[] Key { get; set; }` — 32-byte key. The setter throws `ArgumentNullException` for `null` and
  `InvalidDataException` when the length is not exactly `Consts.LengthOfKey` (32).

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

### `AesByteArrayFormatter`

Encrypts / decrypts a `byte[]` with AES-256. Inherits everything from
[`CryptoFormatterBase<byte[]>`](#cryptoformatterbaset) and implements `IAesByteArrayFormatter`.

**Constructors**

- `AesByteArrayFormatter()` — random IV and key.
- `AesByteArrayFormatter(X509Certificate2 certificate)` — IV/key derived from the certificate.
- `AesByteArrayFormatter(byte[] iv, byte[] key)` — explicit 16-byte IV and 32-byte key.

**Public members**

- `Task<ErrorOr<byte[]?>> ReadAsync(Stream inputStream, CancellationToken)` — decrypt into a byte array.
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decrypt into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(byte[] data, Stream outputStream, CancellationToken)` — encrypt `data` into `outputStream`.
- Plus the inherited `IV`, `Key`, `Certificate`, `BufferSize` members.

> To decrypt data you must use the **same IV and key** that were used to encrypt it.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

// Fixed IV (16 bytes) and key (32 bytes) so we can decrypt later.
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
- `AesStreamFormatter(X509Certificate2 certificate)` — IV/key derived from the certificate.
- `AesStreamFormatter(byte[] iv, byte[] key)` — explicit 16-byte IV and 32-byte key.

**Public members**

- `Task<ErrorOr<Stream?>> ReadAsync(Stream inputStream, CancellationToken)` — decrypt and return a `Stream` (positioned at the end — rewind before reading).
- `Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken)` — decrypt into `outputStream`.
- `Task<ErrorOr<Success>> WriteAsync(Stream data, Stream outputStream, CancellationToken)` — encrypt the `data` stream into `outputStream`.
- Plus the inherited `IV`, `Key`, `Certificate`, `BufferSize` members.

**Example**

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

byte[] iv  = new byte[16];  // use real random values in production
byte[] key = new byte[32];
Random.Shared.NextBytes(iv);
Random.Shared.NextBytes(key);

var formatter = new AesStreamFormatter(iv, key);

using var source = new MemoryStream(Encoding.UTF8.GetBytes("stream secret"));
using var cipher = new MemoryStream();

// Encrypt the source stream
await formatter.WriteAsync(source, cipher);

// Decrypt into a returned stream (positioned at its END)
cipher.Position = 0;
ErrorOr<Stream?> read = await formatter.ReadAsync(cipher);

Stream plain = read.Value!;
plain.Position = 0;                         // rewind!
using var reader = new StreamReader(plain);
string message = await reader.ReadToEndAsync(); // "stream secret"
```

---

### `ServiceCollectionExtensions`

Registers the formatters with the Microsoft dependency-injection container.

**Public member**

- `IServiceCollection AddForgeFormatters(this IServiceCollection services)` — registers the
  formatters below and returns the same collection (so calls can be chained). Registrations:

  | Service | Implementation | Lifetime |
  |---------|----------------|----------|
  | `IGZipByteArrayFormatter` | `GZipByteArrayFormatter` | Singleton |
  | `IGZipStreamFormatter` | `GZipStreamFormatter` | Singleton |
  | `IXmlDataFormatter<>` | `XmlDataFormatter<>` | Singleton |
  | `IBrotliStreamFormatter` | `BrotliStreamFormatter` | Singleton |
  | `IBrotliByteArrayFormatter` | `BrotliByteArrayFormatter` | Singleton |
  | `IAesByteArrayFormatter` | `AesByteArrayFormatter` | Scoped |
  | `IAesStreamFormatter` | `AesStreamFormatter` | Scoped |
  | `IXmlSoapFormatter<>` | `XmlSoapFormatter<>` | Transient |

```csharp
using Forge.Next.Formatters;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddForgeFormatters();

using var provider = services.BuildServiceProvider();

var gzip = provider.GetRequiredService<IGZipByteArrayFormatter>();
var xml  = provider.GetRequiredService<IXmlDataFormatter<Person>>();
```

---

## Dependency injection

`AddForgeFormatters` is designed for ASP.NET Core / generic-host applications. Register once at
startup and inject the interfaces where you need them.

```csharp
using Forge.Next.Formatters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddForgeFormatters();

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

> Because `IAesByteArrayFormatter` / `IAesStreamFormatter` are registered as **Scoped**, resolve
> them within a scope (which ASP.NET Core creates per request automatically). Remember that the
> default constructors generate a **random** IV/key per instance, so if you need a stable key,
> configure `IV`/`Key`/`Certificate` yourself or register your own instance.

---

## Recipes

### Compress and then encrypt

A common pipeline is to compress a payload and then encrypt the compressed bytes.

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Text;

byte[] iv  = new byte[16];
byte[] key = new byte[32];
Random.Shared.NextBytes(iv);
Random.Shared.NextBytes(key);

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

When you construct an AES formatter with an `X509Certificate2`, its IV and key are derived
deterministically from the certificate's public key. Two formatters built from the same
certificate can therefore encrypt and decrypt each other's data without sharing any secret
material out of band.

```csharp
using Forge.Next.Formatters;
using ErrorOr;
using System.Security.Cryptography.X509Certificates;
using System.Text;

X509Certificate2 certificate = LoadCertificate(); // your certificate

var writer = new AesByteArrayFormatter(certificate);
var reader = new AesByteArrayFormatter(certificate); // same derived IV/key

using var cipher = new MemoryStream();
await writer.WriteAsync(Encoding.UTF8.GetBytes("hello"), cipher);

cipher.Position = 0;
ErrorOr<byte[]?> read = await reader.ReadAsync(cipher);
// read.Value == UTF8 bytes of "hello"
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

// Corrupt/undecodable input -> errored result (not an exception)
var aes = new AesByteArrayFormatter();                 // random key
var garbage = new MemoryStream(new byte[] { 9, 9, 9 });
ErrorOr<byte[]?> corrupt = await aes.ReadAsync(garbage);
// corrupt.IsError == true
```

Handy `ErrorOr<T>` members:

- `IsError` — `true` when the operation failed.
- `Value` — the successful result (only valid when `IsError` is `false`).
- `FirstError` — the first `Error`; use `.Type`, `.Code`, `.Description`.
- `Errors` — the full list of `Error` values.
- `Match(...)` / `MatchFirst(...)` / `Switch(...)` — functional handling.

---

## License

Copyright © Zoltan Juhasz. Licensed under the [Apache License 2.0](LICENSE).
