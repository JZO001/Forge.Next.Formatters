# History and decisions

Newest last. Each entry: what was asked or decided, by whom, and why. The entries before 2026-11-11 are reconstructed
from the commit messages and diffs; where a commit gives no reason, none is given here. The package version follows
`AssemblyVersion` in the csproj. The repository has the tags `v3.0.0.0`, `v3.1.0.0`, `v3.2.0`, `v3.2.1`, `v3.2.2` and
`v3.2.5`.

## 2026-07-01 – 3.0.0: the library

The repository was created on 2026-06-30 with the `.gitignore`, the Apache 2.0 `LICENSE` and a two-line README. The
first full implementation followed on 2026-07-01, targeting `net8.0`, `net9.0` and `net10.0`, with `Forge.Next.Shared`
3.1.0 and `SoapFormatter` 1.1.9 as its dependencies:
- `IDataFormatter<T>`, the contract of every formatter: `ReadAsync` (stream → object), `ReadAsync` (stream → stream)
  and `WriteAsync` (object → stream), all returning `ErrorOr` instead of throwing;
- `Consts` (`DefaultBufferSize` 8192, `LengthOfIV` 16, `LengthOfKey` 32);
- compression: `GZipFormatter` (`byte[]`), `BrotliByteArrayFormatter`, `BrotliStreamFormatter`;
- serialization: `XmlDataFormatter<T>` and `XmlSoapFormatter<T>`, whose stream-to-stream `ReadAsync` is "intentionally
  not implemented" and returns `Forbidden`;
- AES: `CryptoFormatterBase<T>` with a random, a certificate-derived or an explicit IV and key, `AesByteArrayFormatter`
  and `AesStreamFormatter`;
- the interfaces, and `ServiceCollectionExtensions.AddForgeFormatters`: compression and XML data as singletons, AES as
  scoped, SOAP as transient;
- the test project (`net10.0`, xUnit, NSubstitute), one test file per class; the README as the full user documentation;
  the prompts that asked for the two (`prompts/`); the commit-message commands. The generated
  `Forge.Next.Formatters.xml` documentation file is ignored.

## 2026-07-01 – 3.1.0: GZip in a byte-array and a stream formatter

`GZipFormatter` and `IGZipFormatter` were replaced by `GZipByteArrayFormatter` and `GZipStreamFormatter` with their
interfaces, "matching the existing byte-array + stream split already used by the Brotli and AES formatters" (the commit
message). Both are registered as singletons. `GZipStreamFormatter.ReadAsync` returned its stream positioned at the end.
`GZipByteArrayFormatter.WriteAsync` got a `BufferSize` check that `GZipFormatter.WriteAsync` did not have. Removing
public types is a breaking change; it was released as a minor version.

## 2026-07-02 – 3.2.0: the JSON formatter, the file and stream helpers, async I/O, two DI entry points

- `SystemJsonFormatter<T>` and `ISystemJsonFormatter<T>`: System.Text.Json with `SerializerOptions`. Its
  stream-to-stream `ReadAsync` returns `Forbidden`, like the other serializers'.
- `DataFormatterExtensions`: `Read` and `Write` on any `IDataFormatter<T>`, for a `FileInfo` or a `Stream`, with a GZip
  layer that is on by default.
- The AES, Brotli and GZip formatters moved to the async stream calls, with `ConfigureAwait(false)` and the cancellation
  token. One loop kept its synchronous call: the two-stream `ReadAsync` of `AesStreamFormatter` (fixed on 2026-11-15,
  A2).
- `AddForgeFormatters` was replaced by `AddForgeFormattersAsScoped` and `AddForgeFormattersAsSingleton`, nine
  registrations each, SOAP transient in both; in the singleton variant the AES formatters became singletons too. A
  breaking change, released as a minor version.
- The README and the tests cover the new parts.

The commit message describes the changes; it gives no reasons for them.

## 2026-07-12 – 3.2.1: the `net48` target

`net48` was added, "so the library also compiles and runs on the classic framework" (the commit message). The APIs that
.NET Framework 4.8 lacks went behind `#if NETCOREAPP`:
- the two Brotli formatters entirely, and their registrations, because `System.IO.Compression.Brotli` is not available
  there;
- `Random.Shared` (`new Random()` on `net48`), `FlushFinalBlockAsync` and `DisposeAsync` (synchronous on `net48`), and
  `StreamWriter.FlushAsync` with a token (without one on `net48`). The XML reader and writer moved to constructor
  overloads that exist on both.

The README listed `net8.0`, `net9.0` and `net10.0` until 2026-11-15.

## 2026-07-12 – 3.2.2: `AddForgeFormattersAsTransient`

A third DI entry point registers every formatter as transient, "complementing the existing scoped and singleton
registration methods" (the commit message). Neither the README nor a test covers it. The commit also carried
`main.txt`, its own message without the version line (the file `/crt.commit.msg` writes); the next commit, two minutes
later, deleted it.

## 2026-07-12 – 3.2.5: the compressed round-trip fixed, the extensions renamed

The commit's title says v3.2.4.0; its body, the csproj and the tag say 3.2.5. `artifacts/` holds 3.2.3 and 3.2.4
packages built the same evening (23:13 and 23:21), while the previous commit (`163ce89c`) was the head; no commit
carries those versions. The commit (23:30):
- renamed `Read` and `Write` of `DataFormatterExtensions` to `ReadAsync` and `WriteAsync`, "so they follow the async
  naming convention of the rest of the library". The message calls it a breaking API change; it was released as a patch
  version.
- fixed the compressed round-trip, which "never worked": `GZipStreamFormatter.ReadAsync` returned the stream at its
  end, so the inner formatter read nothing. The stream now comes back at position 0.
- fixed the resource lifetime: a `using` in a method that only built a task chain disposed the `MemoryStream` and the
  `FileStream` before the chain ran. The extension methods are now `async` and await inside the `using`; `ReadAsync`
  disposes the decompressed stream.
- added round-trip tests with compression, over a stream and over a file. The earlier tests mocked one side, so the
  hand-off between the two was never run.

The README was not updated: it named `Read` and `Write`, and the end position, until 2026-11-15 (A7).

## 2026-11-02 – 3.3.0: `Forge.Next.Shared` 3.3.0

The `Forge.Next.Shared` reference went from 3.1.0 to 3.3.0, and `AssemblyVersion` to 3.3.0.0. The test project took
`Microsoft.NET.Test.Sdk` 18.11.0, `NSubstitute` 6.2.0 and `xunit.runner.visualstudio` 4.0.1. No source change; the
commit message ("v3.3.0") gives no reason, and the commit has no tag. The formatters call `ProtectAsync` without the
options that `Forge.Next.Shared` 3.2.0 and 3.3.0 added, so a cancellation still becomes an error, and the errors still
carry the stack trace.

`artifacts/Forge.Next.Formatters.3.2.5.nupkg` was rebuilt the same day at 15:21, just before the 3.3.0 package, and it
depends on `Forge.Next.Shared` 3.3.0, while the 3.2.5 commit references 3.1.0 (see `OPEN.md`).

## 2026-11-11 – The package icon: the Forge mark

Zoltán chose a new icon for the Forge packages: the Forge mark, a hexagon with a smaller, filled hexagon inside, the two
joined by three lines, on a dark rounded tile, so it reads on a light and on a dark ground. `logo.png` is now this mark,
128 by 128 pixels (NuGet's advice), in place of the earlier logo (640 by 640), which looked much like the Jedi Order's
emblem (Lucasfilm), a trademark risk. The file in this repository was replaced on 2026-11-11 and is not committed yet.
The project packs `logo.png` as before. The published versions keep the old icon; the next version carries the new one.

## 2026-11-11 – The knowledge base

Zoltán asked for a knowledge base like the ones in `Forge.Next.Shared` and `Forge.Next.LocalWebShell`; this is it
(`CLAUDE.md`, `AI/`). It was written from the code, the tests and the git history. The build, the tests and the package
contents were checked the same day (see `VERIFICATION.md`); what does not match is in `OPEN.md`.

## 2026-11-15 – The order of the Release A fixes

Zoltán agreed to work through Release A of `PLANS/RECOMMENDED_WAY_TO_FIX.md` in the order A3, A4, A6, A5, A1, A2, A7:
the tests run on every target (A6) before the items whose code differs on `net48`, and the README (A7) comes last, so
it describes the result. The plan was re-checked against the code the same day and refined (A1, A2, A5, A6).

## 2026-11-15 – The `net11.0` target; `Forge.Next.Shared` 3.3.2

**Zoltán's change** (the plan's B5): `net11.0` added to `TargetFrameworks`, and the `Forge.Next.Shared` reference raised
from 3.3.0 to 3.3.2. No source change; `AssemblyVersion` stays 3.3.0.0 until the next release. Zoltán handed the
change over the same day, to be checked and documented.

**Checked the same day** (`VERIFICATION.md`):
- `Forge.Next.Shared` 3.3.2 has the same dependency groups as 3.3.0 on `net48` to `net10.0`; on `net11.0` it has
  `ErrorOr` only, because from .NET 11 the logging and DI abstractions are part of the `Microsoft.NETCore.App` shared
  framework (both are in the `Microsoft.NETCore.App.Ref` 11.0.0 reference pack). `ServiceCollectionExtensions` takes
  `IServiceCollection` from there; the restored graph has no `Microsoft.Extensions.*` package on `net11.0`.
- This package uses only `ProtectAsync` from `Forge.Next.Shared`. Its code has not changed since 3.3.0 (the last
  commit of `ObjectExtensions.cs` in that repository is 3.3.0's), and its XML documentation is the same in 3.3.0 and
  3.3.2, so the behaviour of every formatter stays. 3.3.1 added `net11.0` there; 3.3.2 fixed `LogUtils`, which this
  package does not use.
- The README header now lists `net48` (without the Brotli formatters) and `net11.0`.

The test project still runs `net10.0` only, so the `net11.0` build is compiled but not tested (A6).

## 2026-11-15 – One XML documentation file per target (A3)

**Asked by Zoltán:** item A3 of the plan. The csproj's `<DocumentationFile>Forge.Next.Formatters.xml</DocumentationFile>`
made every target build, running in parallel, write one file in the project folder, and each target packed whatever was
there at the time: an empty `lib/net9.0` file (the build of 2026-11-11), a truncated `lib/net48` file (3.2.4), and
`lib/net48` files that documented the Brotli formatters (3.2.2, 3.2.3, 3.2.5). Removed: the `DocumentationFile` line,
the `<None Remove="Forge.Next.Formatters.xml" />` item, the `.gitignore` entry (added in 3.0.0), and the stray file.
`GenerateDocumentationFile` stays, so each target writes its own file under `obj/<configuration>/<target>/`, as
Forge.Next.Shared does since 3.3.2. No change to the code or the API; the next package carries a correct XML
documentation file for each target.

## 2026-11-15 – The license in the package (A4)

**Asked by Zoltán:** item A4 of the plan. The csproj had `<licenses>` with a link to the repository's license, which is
not a NuGet property, so no package from 3.0.0 to 3.3.0 declared a license, although the README and the repository's
`LICENSE` say Apache 2.0. It is now `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`, in the same
place as in Forge.Next.Shared and Forge.Next.LocalWebShell. The license itself does not change; the next package
declares it.

## 2026-11-15 – The tests on every target (A6, step 1)

**Asked by Zoltán:** the first step of A6, the existing tests on every target. The test project now targets `net48`,
`net8.0`, `net9.0`, `net10.0` and `net11.0` with `LangVersion` 12.0, as the library (`net48` would default to C# 7.3,
and every test file has a file-scoped namespace). On `net48` the csproj leaves out the two Brotli test files, and
`ServiceCollectionExtensionsTests` keeps its Brotli cases behind `#if NETCOREAPP`; its two "exactly nine services"
tests became `…_RegistersExactlyTheExpectedServices_Test`, nine on the .NET targets, seven on `net48`. Nothing else had
to change to compile for `net48`. The test packages stay as they were; `Microsoft.Extensions.DependencyInjection`
10.0.0 is used on every target.

**Found:** on `net48` the parameterless AES constructor gives two instances created within the same clock tick the
same IV and key: `new Random()` is seeded from `Environment.TickCount` on .NET Framework. The existing test
`Constructor_Default_GeneratesRandomIvAndKey_Test` fails there; it passes on the .NET targets. A1 of the plan replaces
the generator.

## 2026-11-15 – A cryptographic random number generator for the default IV and key (A1)

**Decided by Zoltán:** A1 right after A6 step 1, before A5 and the rest of A6, because the `net48` test run had shown
the bug; option (a), one code path on every target.

The parameterless constructor of `CryptoFormatterBase<T>` filled the IV and the key from `System.Random`:
`Random.Shared` on the .NET targets, which is not a cryptographic generator, and `new Random()` on `net48`, seeded from
the clock, so instances created within one tick got the same IV and key, and the seed could be guessed from the time
of creation. The AES formatters that the DI registrations build get their key this way. It now uses one
`RandomNumberGenerator.Create()` and `GetBytes` for both, on every target, without `#if`; `Random.Shared` left the list
of `CLAUDE.md` invariant 3. No signature changed, and no stored data depends on how a random key was made: it is only
ever known to its own instance. The README says so in the constructor list and the "Dependency injection" note, and
its two examples that made a key with `Random.Shared` use `RandomNumberGenerator` now. No new test: the existing
`Constructor_Default_GeneratesRandomIvAndKey_Test` failed on `net48` before and passes on every target now.

## 2026-11-15 – Tests for the untested behaviour (A6, step 2)

**Asked by Zoltán:** the second step of A6. The tests pin the behaviour as it is, so a later change (B3, B4) has to
change a test on purpose:
- **a cancelled token** on every method that does work, in the test class of each of the nine formatters: the
  compression, AES and JSON formatters give one `Unexpected` error made from an `OperationCanceledException` (checked
  through the error's `type` metadata); the XML read and both SOAP methods ignore the token; the XML write gives the
  error on the .NET targets and succeeds on `net48`;
- **the returned stream** of `ReadAsync(input)`: GZip's at position 0, Brotli's and AES's at their end;
- **`AddForgeFormattersAsTransient`**, as the other two entry points are tested.

The new blocks were generated from one template per kind, so the tests of the nine classes have the same shape.
`GZipStreamFormatterTests` and `ServiceCollectionExtensionsTests` had summaries from before 3.2.5 and 3.2.2; they now
describe the current behaviour. 206 test cases on the .NET targets, 173 on `net48`.

**Found:** a cancelled XML write on the .NET targets returns an error after the complete XML has been written: the
cancelled `StreamWriter.FlushAsync` throws, and disposing the writer flushes anyway (`OPEN.md`). Left as it is, as
input for B3.

## 2026-11-15 – Known-answer tests for the AES output (A5)

**Asked by Zoltán:** A5, with the recommended source, so that no later change (B1, B2) can make stored data unreadable
without a failing test (`CLAUDE.md`, invariant 7).
- **The NIST vector:** the key, IV, plaintext and ciphertext of the CBC-AES256 example of NIST SP 800-38A
  (Appendix F.2.5, pages 28-29), taken from the publication itself; F.2.6 gives the same values back. Both AES
  formatters must write exactly the four ciphertext blocks and the PKCS7 block (80 bytes), and read them back to the
  plaintext. The PKCS7 block is AES-256 of a block of 0x10 bytes XOR the fourth ciphertext block.
- **The fixed certificate:** a self-signed RSA 2048 certificate ("CN=Forge.Next.Formatters.Tests.FixedCertificate",
  valid 2026 to 2126), only its public part, in base64 in the tests. `Certificate_FixedCertificate_DerivesKnownIvAndKey_Test`
  pins the IV and key it gives; both AES formatters must write a known ciphertext with it and read it back.
- **Independent constants:** the PKCS7 block and the certificate's ciphertext were computed by hand-made CBC from
  single-block AES encryptions (ECB, no padding) in a PowerShell script, not with the formatters; the hand-made CBC
  reproduced NIST's 64 bytes, and the platform's own `EncryptCbc` gave the same 80 bytes. The test file says not to
  regenerate them from the formatters' output (`CLAUDE.md`, the tests).
- **The tests fail when they should:** with the byte-array formatter's padding changed (ANSI X9.23) its NIST write test
  failed, and with the IV taken one byte later all five certificate tests failed; the code was restored afterwards
  (checked by hash).
- They pass on every target, so `net48` and the .NET targets write the same bytes.

The shared data (NIST values, the certificate, `FromHex`, `LoadFixedCertificate`) sits in `CryptoFormatterBaseTests`,
next to `CreateSelfSignedCertificate`. `LoadFixedCertificate` uses `X509CertificateLoader` from .NET 9 on, where the
`X509Certificate2(byte[])` constructor is obsolete. 9 new tests: 215 test cases on the .NET targets, 182 on `net48`.

**Found:** for an RSA 2048 certificate with the exponent 65537, 9 bytes of the derived IV and 5 bytes of the derived
key are the same for every certificate (the DER header and the exponent). Recorded for B2 (`OPEN.md`).

## 2026-11-15 – The two-stream AES read observes the token (A2)

**Asked by Zoltán:** A2. `AesStreamFormatter.ReadAsync(input, output)` was the one loop that 3.2.0 left synchronous: it
read with `CryptoStream.Read`, so the token was not observed while reading. It now reads with
`await csDecrypt.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)`, as the one-stream read
and `AesByteArrayFormatter` do. No signature changed, and the output is the same (the A5 tests pass).

The test came first: `ReadAsyncWithOutput_CancelledToken_ReadsNothingFromInput_Test` asserts that a cancelled token
leaves the input at position 0 and the output empty. An error result alone could not tell the two apart, because the
old code read the input and then failed at the first write to the output. Before the fix it failed on all five targets
(16 bytes of the input read), after it it passes on all five. 216 test cases on the .NET targets, 183 on `net48`.

## 2026-11-15 – 3.3.1: the README tells the truth (A7); the version

**Decided by Zoltán:** the next version is **3.3.1**. `AssemblyVersion` is 3.3.1.0 and the README header says 3.3.1.
Release A (A1 to A7, with the `net11.0` target and `Forge.Next.Shared` 3.3.2) changes no signature and no stored
output, so a patch version fits `CLAUDE.md` invariant 1.

**Asked by Zoltán:** A7, the README brought up to date with the code:
- the header (version 3.3.1, the targets with `net48` without Brotli), the table of contents (two new sections), and a
  "not on `net48`" note in the formatter table, the two Brotli sections and the interfaces;
- the error table: `Unexpected`, one error per exception in the inner chain, `Code` `General.Unexpected`, the metadata
  `type`, `message` and `stackTrace`, with a warning not to pass them on unfiltered; that only constructors and setters
  throw;
- a new "Cancellation" subsection: a cancellation is an `Unexpected` error, and which formatters observe the token
  (the XML write only on the .NET targets and after writing; the XML read and SOAP not at all);
- "A note about streams and `Position`" as a table (GZip at 0 since 3.2.5, Brotli and AES at the end), the GZip stream
  section and its example; `BufferSize` checked by `GZipByteArrayFormatter.WriteAsync` too;
- `DataFormatterExtensions`: `ReadAsync` / `WriteAsync` (renamed in 3.2.5), the binding note turned the right way
  round (`formatter.WriteAsync(data, stream)` is the formatter's own method, without GZip), file overwrite and
  `FileShare.Read`, the memory use of the GZip layer, the exception pass-through of the stream overloads, and the
  examples and the recipe with the new names;
- `ServiceCollectionExtensions` and "Dependency injection": three entry points with `AddForgeFormattersAsTransient`,
  seven registrations on `net48`, `Add…` rather than `TryAdd…`;
- the certificate: the constructor and the `Certificate` property point to the risk (and to the in-place overwrite of
  the caller's arrays); the recipe "Deriving keys from a certificate" opens with a warning; the AES example says its
  fixed key is for illustration only;
- the SOAP section warns to read trusted input only; the JSON null note names the `Unexpected` error;
- a new "Security notes" section: no authentication, one IV per formatter, keys from a certificate are not secret,
  the generated keys before 3.3.1, SOAP deserialization, errors carry internals.

Every new or changed example was compiled and run in a scratch program outside the repository, and behaved as the README
says (`VERIFICATION.md`). The package's README is this README.

## 2026-11-15 – The versioning rule (B6)

**Decided by Zoltán:** the rule of `Forge.Next.Shared` holds for this package too: a change that breaks a caller raises
at least the minor version, and the commit message says what breaks under "Compatibility" (`CLAUDE.md`, invariant 1).
3.2.5, which renamed public methods in a patch version, came before the rule. So after 3.3.1 a release with a
behaviour change of Release B (B3, B4) is at least 3.4.0.

## 2026-11-15 – 3.3.1 committed, not published

**Decided by Zoltán:** Release A is committed as 3.3.1, but 3.3.1 will **not** be published. B3 follows on top of it,
and the next published version is 3.4.0, carrying Release A and B3. So the README's "since 3.3.1" and "before 3.3.1"
become 3.4.0 with B3.

## 2026-11-15 – 3.4.0: the caller's cancellation throws (B3)

**Decided by Zoltán:** option (a) of B3, the same decision as B1 of Forge.Next.Formatters.Newtonsoft.
- Every `ProtectAsync` call (18 in the formatters, 2 in the `FileInfo` overloads of `DataFormatterExtensions`) gets the
  caller's token and `rethrowCancellation: true`. `ProtectAsync` rethrows an `OperationCanceledException` only while
  that token requested cancellation, so the caller's own cancellation throws as .NET expects, and a cancellation the
  token did not request (an inner timeout) is still an `Unexpected` error. It does not check the token before the
  lambda runs.
- The XML and SOAP formatters call `ThrowIfCancellationRequested()` once, before the synchronous serializer, and the XML
  write flushes without the token: a cancelled call does nothing, a started one completes, the same on every target.
  This ends the case A6 found (an error after the whole XML had been written) and the `#if` around
  `StreamWriter.FlushAsync`.
- The `FileInfo` overloads check the token before they open the file, so a cancelled write no longer truncates an
  existing file (found while doing B3: `FileMode.Create` ran before the formatter).
- `includeStackTrace` stays at its default; the README's "Security notes" warn about the metadata.
- The public methods, the interface and the base class document the `OperationCanceledException`.

**Tests:** the 24 cancellation tests of A6 and the A2 test now expect `OperationCanceledException`
(`Assert.ThrowsAnyAsync`); the XML and SOAP ones also check that nothing is read or written. New: an inner cancellation
(a stream that throws `OperationCanceledException` itself) is still an `Unexpected` error; the `FileInfo` write keeps an
existing file; the `FileInfo` read and the GZip-layered stream write throw. 220 test cases on the .NET targets, 187 on
`net48`.

**Version:** `AssemblyVersion` 3.4.0.0, the README 3.4.0, its "since 3.3.1" now "since 3.4.0" (3.3.1 is not published).

**Compatibility:** a caller that cancels now gets an `OperationCanceledException` instead of an `Unexpected` error; the
XML and SOAP formatters now observe the token (once, before their work). No signature changed. A minor version, by the
rule confirmed in B6.

## 2026-11-15 – The certificate derivation is obsolete (B2 (a))

**Decided by Zoltán:** option (a) of B2, in 3.4.0, which Zoltán publishes only when all the planned work is done.
- `[Obsolete]`, as a warning, on `ICryptoFormatter<T>.Certificate`, `CryptoFormatterBase<T>.Certificate`, and the
  certificate constructors of `CryptoFormatterBase<T>`, `AesByteArrayFormatter` and `AesStreamFormatter`. The message:
  the IV and the key come from the certificate's public key, so anyone who has the certificate can decrypt; use a
  secret IV and Key, and keep this only to read data written this way. Each member's XML documentation has a remark
  saying the same. `DiagnosticId` is not used: `ObsoleteAttribute` has no such property on `net48`.
- The behaviour is unchanged: the A5 known-answer tests of the certificate still pass, so data written with a
  certificate stays readable. The library builds without warnings, because it uses these members only from inside
  the obsolete members themselves.
- The tests that use the certificate on purpose suppress CS0618 with a `#pragma` around each use, with the reason;
  `Certificate_Members_AreObsoleteAsWarning_Test` pins the five attributes and that they are warnings.
- The README marks the members obsolete, and the certificate recipe shows the `#pragma`.

(b), a secure certificate mode in B1's new format, waits until a caller needs encryption with a certificate.

**Compatibility:** callers of these members get compiler warning CS0618; a build that treats warnings as errors fails
until the use is suppressed or replaced. No signature or behaviour changed.

## 2026-11-15 – The authenticated AES format: decided (B1, in progress)

**Decided by Zoltán:** the format of the plan's B1, the same on every target (`AesGcm` and HKDF do not exist on
`net48`): a 4-byte header (`FNF` and the version byte `0x02`), a random 16-byte IV per message, the AES-256-CBC
ciphertext with PKCS7, and a 32-byte HMAC-SHA256 tag over the header, the IV and the ciphertext (encrypt-then-MAC). The
encryption and the authentication key are derived from `Key` with HMAC-SHA256 over two fixed labels. Reading checks the
tag in constant time before it decrypts; the stream formatter buffers its input for that. Input without the header is
read the current way unless the caller refuses it. Refinement, accepted: a header with a wrong tag falls back to the
current format when that is accepted, because a message of the current format starts with the header bytes by chance
once in 2^32; this does not weaken anything, since such input is accepted unauthenticated anyway.

**Decided by Zoltán:** the new format is opt-in (minor), so it goes into 3.4.0; the default may switch in a later major
version.

## 2026-11-15 – 3.4.0: the authenticated AES format (B1)

**Asked by Zoltán:** B1 as decided above, with the API of option (a): a public enum `AesOutputFormat` (`Legacy = 0`,
`Authenticated = 1`, its own file) and, on `ICryptoFormatter<T>` and `CryptoFormatterBase<T>`, `OutputFormat` (default
`Legacy`; the setter throws `ArgumentOutOfRangeException` for an undefined value) and `AcceptUnauthenticated` (default
`true`).
- `CryptoFormatterBase<T>` holds the format as `private protected` helpers, so no other public member is added:
  `WriteAuthenticatedAsync` builds the ciphertext in memory and writes header, IV, ciphertext and tag; `OpenAsync`
  decides the format of a read by the header, checks the tag in constant time, and returns either the plaintext or the
  stream to decrypt the `Legacy` way. The derived keys are cleared after use. The two AES formatters call them; their
  `Legacy` code is unchanged.
- The read checks `CanRead` first: without it, a non-readable fake stream gave an empty success once the input was
  looked at before the `CryptoStream` was made (found by the existing `ReadAsync_NonReadableStream_ReturnsError_Test`).
- A seekable input is read 4 bytes ahead and set back; one that cannot seek is copied into memory first, in both
  formats.

**Tests:** the known-answer message of the authenticated format, made independently with a PowerShell script (HMAC
key derivation, hand-made CBC, HMAC tag; the hand-made CBC equal to the platform's `EncryptCbc`), reads back on every
target; the writer's output was also checked independently in the scratch program (tag and plaintext). New tests: the
defaults and the setter check; the layout and the round trip of a write, read by a formatter with another IV; a new IV
per message; a flipped bit in the header, IV, ciphertext or tag, and a cut-off message, give an error, with nothing
written when the unauthenticated format is refused; `Legacy` input refused when not accepted; a `Legacy` message whose
first ciphertext block is the header (made by choosing that block) is read the `Legacy` way when accepted; non-seekable
input and input not at its start, in both formats. 253 test cases on the .NET targets, 220 on `net48`.

**Compatibility:** `ICryptoFormatter<T>` has two new members, so a class of an application that implements it no
longer compiles until it adds them; the library's classes and every caller are unaffected. The default output stays
`Legacy`, byte for byte (the A5 tests). Reading now recognizes the authenticated format, copies a non-seekable input
into memory first, and refuses a non-readable one up front. A minor version (3.4.0).

## 2026-11-15 – The returned streams at position 0 (B4, first part)

**Decided by Zoltán:** option (a). `BrotliStreamFormatter.ReadAsync(input)` and `AesStreamFormatter.ReadAsync(input)`
(both AES formats) return their new `MemoryStream` at position 0, as `GZipStreamFormatter` has since 3.2.5. The three
tests that pinned the end position now pin 0; the README's table and examples say so. 253 test cases on the .NET
targets, 220 on `net48`, all passing; 0 warnings.

**Compatibility:** a caller that relied on the end position (for example used `Position` as the length, or wrote on at
the end) sees the stream at 0; a caller that rewinds is unaffected. In 3.4.0.

Left of B4 then: the null check of `SystemJsonFormatter<T>.ReadAsync`, and a default SOAP `Binder` (each a decision).

## 2026-11-16 – The JSON read checks its stream (B4, second part)

**Decided by Zoltán:** a `null` stream on `SystemJsonFormatter<T>.ReadAsync` returns a `Validation` error with the
description `inputStream`, checked before any work, as every other method does; it was the one method without the check,
and the `null` reached `JsonSerializer.DeserializeAsync`, which made an `Unexpected` error of it. The test
`ReadAsync_NullInputStream_ReturnsValidationError_Test` asserts the type and the description; the README note and
`CLAUDE.md` invariant 4 no longer name an exception. In 3.4.0.

**Compatibility:** a caller that passes a `null` stream gets a `Validation` error instead of an `Unexpected` one.

## 2026-11-16 – The SOAP read leaves the caller's stream open (latent bug)

**Found** while answering how the SOAP formatter treats a payload's types: `SoapFormatter.Deserialize` of the
SoapFormatter package closes the stream it reads, on every target, so `XmlSoapFormatter<T>.ReadAsync` broke `CLAUDE.md`
invariant 5; rewinding the stream after a read threw `ObjectDisposedException`. No test reused a stream after a SOAP
read. **Fixed at Zoltán's request:** the read copies the input into a `MemoryStream` (after the token check, with the
token) and deserializes the copy; the caller's stream stays open, at its end. The test
`ReadAsync_InputStream_StaysOpen_Test` failed before the fix on `net10.0` and `net48` and passes after it. In 3.4.0.

## 2026-11-16 – Opt-in allowed types for the SOAP read (B4, third part)

**How it was:** since 3.0.0 the default `SoapFormatter` has no binder, so a payload makes it create whatever
`[Serializable]` types it names, and run their deserialization callbacks, before the cast to `T` fails (checked in a
scratch program on `net10.0` and `net48`). A caller could already pass a `SoapFormatter` with a `Binder`.

**Decided by Zoltán:** option (b), an opt-in, so nothing changes for existing callers. Not a constructor, because a
second one-parameter constructor would make `new XmlSoapFormatter<T>(null)` ambiguous, and an `IEnumerable<Type>`
parameter would change which constructor the DI container picks (it resolves `IEnumerable<>` as an empty list):
- the public `SoapAllowedTypesBinder` (its own file): matches a type by its full name and returns the allowed type,
  so a payload that names the same type in another assembly or version gets the allowed one; any other type throws
  `SerializationException`;
- the static `XmlSoapFormatter<T>.WithAllowedTypes(params Type[] otherAllowedTypes)`: a formatter whose `SoapFormatter`
  has that binder for `T` and the given types.

**Found on the way:** the binder is asked for an array's element type, not the array type, and for a generic list type
itself. On `net48` the SoapFormatter package creates the types of `mscorlib` (`List<T>`, `ArrayList`, `Version`)
without asking the binder, so there the restriction covers only the types of other assemblies; the README says to read
trusted SOAP input only on `net48`, and `WithAllowedTypes_ArrayAndList_NeedElementAndListType_Test` pins the
difference. 263 test cases on the .NET targets, 230 on `net48`.

**Compatibility:** none for existing callers: two additions, `SoapAllowedTypesBinder` and `WithAllowedTypes`.

## 2026-11-16 – Clean-ups C2, C4, C5 and C6

**Asked by Zoltán**, from the plan's ideas, the four without a behaviour change; C1 (copying the `IV` and `Key` arrays)
and C3 (the Brotli interfaces on `net48`) stay as they are.
- **C2:** the `ms.SetLength(0)` at the end of `AesByteArrayFormatter.WriteAsync` and `BrotliByteArrayFormatter.WriteAsync`
  is removed: on the `MemoryStream` over the caller's array it cleared nothing. The two in the `catch` blocks of the
  stream formatters' reads stay: there the stream is the formatter's own.
- **C4:** the 16 tests of `DataFormatterExtensionsTests` are named after the methods they test (`ReadAsync_…`,
  `WriteAsync_…`, `WriteAsyncThenReadAsync_…`, renamed in 3.2.5); the unused Shouldly reference is removed from the
  test project.
- **C5:** the eleven interfaces and `AesByteArrayFormatter` have summaries that say what they are instead of
  repeating the name (`IGZipByteArrayFormatter`'s named another interface, `AesByteArrayFormatter`'s called the class an
  interface); the `T` of `CryptoFormatterBase<T>`, `XmlDataFormatter<T>` and `XmlSoapFormatter<T>`, and the `iv` and
  `key` of the `CryptoFormatterBase` constructor, are described.
- **C6:** `.gitignore` ignores `/*.txt`, the `<branch>.txt` that `/crt.commit.msg` writes in the repository root; no
  other `.txt` file lives there.
