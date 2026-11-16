# Recommended way to fix the open findings

Written on 2026-11-12 by a Claude Code session for the Claude Code session that fixes these items here, locally, with
Zoltán. It turns the findings of `AI/OPEN.md` (found and verified on 2026-11-11, see `AI/VERIFICATION.md`) into an order
of work. Read `CLAUDE.md` and `AI/` first. The invariants of `CLAUDE.md` still hold; an item that changes one says so,
and needs Zoltán's decision.

## How to work through it

- One item at a time, in the order of "The order of work" below. After an item, report and stop until Zoltán names
  the next one.
- An item marked **Decision** needs Zoltán's answer before any code: ask with the options and the recommendation given
  here, one question at a time. Check the facts again first: line numbers move, member names rarely do.
- Every change: a 0-warning build of all five targets, all tests, and for a package change the package check of
  `AI/VERIFICATION.md`, always with `-p:PackageOutputPath=<a folder outside the repository>\`, so `artifacts/` stays
  as it is.
- In the same step: a public change into `README.md`, a decision into `AI/HISTORY.md`, a check into
  `AI/VERIFICATION.md`; a closed item leaves `AI/OPEN.md`, and its box here gets ticked with the date.
- No git command that writes unless Zoltán asks (`/commit.changes`). No name of a product or project that uses this
  package (`CLAUDE.md`).
- Forge.Next.Formatters.Newtonsoft depends on this package and has its own plan. B3 is one decision for both, and a
  release of this package comes before a Newtonsoft release that needs it.

## Releases

- **Release A**, without a change of behaviour, of a signature or of the encrypted output (a patch): A1 to A7.
- **Release B**, new API and changed behaviour (at least a minor): B1 to B6.

Zoltán decides whether they ship apart. Before the first Release build of a release, ask which version is next (3.3.0
has no tag, so it may be unpublished) and raise `AssemblyVersion`: a Release build of the current version overwrites its
package in `artifacts/`. Do not publish `artifacts/Forge.Next.Formatters.3.2.5.nupkg` again: it was rebuilt on
2026-11-02 against Forge.Next.Shared 3.3.0, so it is not the 3.2.5 commit's package.

The `net11.0` target and Forge.Next.Shared 3.3.2 (B5, done on 2026-11-15) are in the working tree, so they ship with
the next release, whichever it is.

## The order of work

Re-checked against the code on 2026-11-15. The baseline after B5: 0 warnings on all five targets, 166/166 on
`net10.0` (`AI/VERIFICATION.md`).

- **Release A** (agreed with Zoltán on 2026-11-15): **A3 → A4 → A6 → A5 → A1 → A2 → A7.** Done so far: A3, A4,
  A6 step 1, A1 (brought forward, see A1), A6 step 2, A5, A2, A7: **Release A is complete** (2026-11-15), committed
  as 3.3.1, which Zoltán will not publish: it ships with B3 as 3.4.0.
  - A3 and A4 change only the build and the package; one Release build into a temporary folder can check both, but
    they stay two items.
  - A6 comes before A5, A1 and A2, so their tests also run the `net48` code paths, and the A5 outputs prove that
    `net48` and the .NET targets give the same bytes.
  - A7 comes last: it describes the code after A1 to A6.
- **Release B** (recommended, not decided yet): **B6 → B3 → B2 → B1 → B4** (B5 is done). Done: B6, B3, B2 (a) and B1,
  in 3.4.0 (2026-11-15), which Zoltán publishes when everything is done. Next: B4. B6 decides the versions of the others; B3 is small and shared with Forge.Next.Formatters.Newtonsoft; B2 (a) is small and comes before the large B1,
  which builds on the A5 tests.

## Release A

### A1. A cryptographic random number generator for the default IV and key [x] 2026-11-15

Done with option (a), moved before A5 and A6 step 2 by Zoltán, because A6 step 1 had shown the `net48` bug
(`AI/HISTORY.md`).

- **Problem:** the parameterless constructor of `CryptoFormatterBase<T>` fills the IV and the key from `System.Random`
  (`Random.Shared`; `new Random()` on `net48`), which is predictable. The formatters that the DI registrations build get
  their key this way.
- **Fix** (options, ask Zoltán):
  - (a) one code path on every target: one `RandomNumberGenerator.Create()` in a `using`, and `GetBytes` for both. It
    removes a `#if`. **Recommended.**
  - (b) `RandomNumberGenerator.Fill(_IV)` and `RandomNumberGenerator.Fill(_key)` on the .NET targets; on `net48` (a).
- **Compatibility:** none affected. A random key is only ever known to its own instance, so no stored data depends on
  how it was made.
- **Tests:** none new. `CryptoFormatterBaseTests.Constructor_Default_GeneratesRandomIvAndKey_Test` already asserts
  that two instances differ and that the lengths are 16 and 32; no test can tell a cryptographic generator from
  `System.Random`.
- **Docs:** `CLAUDE.md` invariant 3 (`Random.Shared` leaves the list, if (a)), `AI/DESIGN.md` ("What differs on
  `net48`", "The AES key material"), the README's "Dependency injection" section.

### A2. The two-stream AES read observes the token [x] 2026-11-15

Done as planned: the test first, red on every target (16 bytes read), then the fix (`AI/HISTORY.md`).

- **Problem:** `AesStreamFormatter.ReadAsync(input, output)` reads with the synchronous `CryptoStream.Read`, so it does
  not observe the token while reading; 3.2.0 made every other loop asynchronous.
- **Fix:** `await csDecrypt.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)`, as the
  one-stream read does.
- **Tests:** a cancelled token stops the read before anything is read: the input stream's `Position` stays 0. An
  error result alone does not tell the two apart: today, with a cancelled token, the synchronous `Read` reads the input,
  then `outputStream.WriteAsync` throws, and the result is an `Unexpected` error too (B3). Write the test first and see
  it fail on today's code.

### A3. One XML documentation file per target [x] 2026-11-15

- **Problem:** `<DocumentationFile>Forge.Next.Formatters.xml</DocumentationFile>` makes the target builds, which
  run in parallel, write one file in the project folder, and each target copies whatever is there into its output.
  Seen: an empty `lib/net9.0` file in the build of 2026-11-11, a truncated `lib/net48` file in 3.2.4, and `lib/net48`
  files with the Brotli formatters, which `net48` does not have, in 3.2.2, 3.2.3 and 3.2.5.
- **Fix:** in `Forge.Next.Formatters/Forge.Next.Formatters.csproj` remove the `DocumentationFile` line (keep
  `GenerateDocumentationFile`) and the `<None Remove="Forge.Next.Formatters.xml" />` item; remove the file's entry from
  `.gitignore`, and delete the ignored `Forge.Next.Formatters/Forge.Next.Formatters.xml`.
- **Check:** a Release build into a temporary folder, twice; every `lib/<target>/Forge.Next.Formatters.xml` parses as
  XML, the `net48` one without the Brotli formatters.

### A4. The license in the package [x] 2026-11-15

- **Problem:** `<licenses>` is not a NuGet property, so no package from 3.0.0 to 3.3.0 has a license, although the
  README says Apache-2.0.
- **Fix:** `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` instead, as Forge.Next.Shared and
  Forge.Next.LocalWebShell have.
- **Check:** the package's `.nuspec` has `<license type="expression">Apache-2.0</license>`.

### A5. Known-answer tests for the AES output [x] 2026-11-15

Done as recommended: the NIST values taken from the publication (pages 28-29), the PKCS7 block, the fixed certificate
and its output computed by hand-made CBC from single-block AES, independently of the formatters (`AI/HISTORY.md`).

- **Why before B1:** B1 adds a format beside the current one. Fixed outputs pin the current one, so no later change
  makes stored data unreadable without a failing test.
- **Fix:** for `AesByteArrayFormatter` and `AesStreamFormatter`, constants made once with today's code: a fixed IV, key
  and plaintext, and the exact ciphertext bytes. `WriteAsync` must give those bytes, and `ReadAsync` the plaintext back.
  One more for a key and IV from a fixed test certificate, which pins the derivation (B2).
- **Recommended source of the constants:** the CBC-AES256 example of NIST SP 800-38A (F.2.5): its key, IV and four
  plaintext blocks; the first 64 output bytes must be NIST's ciphertext, followed by one 16-byte PKCS7 padding block.
  Beside pinning today's output, this proves from an independent source that it is standard AES-256-CBC with PKCS7.
  Take the values from the publication, not from memory.
- **The certificate:** generated once, only its public part (DER, base64) in the test file; no private key is needed,
  the derivation reads the public key only.

### A6. Tests on every target [x] 2026-11-15

- **Problem:** the test project targets `net10.0` only. `net48` has code of its own: `new Random()` (A1 replaces it),
  the synchronous `FlushFinalBlock` and `Dispose`, `StreamWriter.FlushAsync()` without a token, and the `netstandard2.0`
  SoapFormatter assembly on .NET Framework.
- **Fix:** `<TargetFrameworks>net48;net8.0;net9.0;net10.0;net11.0</TargetFrameworks>` in the test project, with the
  Brotli tests only on the .NET targets. Whether the tests compile for `net48` was not tried; report what does not.
  - The test project needs a `LangVersion`: it sets none, `net48` defaults to C# 7.3, and every test file uses a
    file-scoped namespace. 12.0, as the library, unless something does not compile with it: Forge.Next.Shared 3.3.2
    took 14 in its test project, because C# 12 made seven Shouldly calls ambiguous on `net9.0` (this test project
    references Shouldly but does not use it).
  - The Brotli tests: `BrotliByteArrayFormatterTests.cs` and `BrotliStreamFormatterTests.cs` as `Compile Remove` on
    `net48`; in `ServiceCollectionExtensionsTests.cs` the Brotli `InlineData` rows and assertions behind
    `#if NETCOREAPP`.
  - The test packages support `net48` (checked 2026-11-15 in the NuGet cache): Microsoft.NET.Test.Sdk 18.11.0
    (`net462`), xunit.runner.visualstudio 4.0.1 (`net472`), NSubstitute 6.2.0 and Shouldly 4.3.0 (`netstandard2.0`),
    Microsoft.Extensions.DependencyInjection 10.0.0 (`net462`). `CertificateRequest` and `RSA.Create(int)`, which the
    AES tests use, exist since .NET Framework 4.7.2.
  - Two steps: first the existing tests on every target, then the new tests below.
  - **Step 1 done 2026-11-15:** everything compiles on all five targets with `LangVersion` 12.0; 166/166 on the .NET
    targets, 141/142 on `net48`. The one failure is a bug of the library that A1 fixes: on `net48`, `new Random()` is
    seeded from the clock, so two AES formatters created within the same tick get the same IV and key
    (`AI/HISTORY.md`).
  - Docs: `CLAUDE.md` ("Tests": `net10.0` only), `AI/VERIFICATION.md` (the test counts per target; the "Tests on another
    target" check is then not needed).
- **Also:** the behaviour without a test: a cancellation token, the position of a returned stream (GZip's at 0 since
  3.2.5), and `AddForgeFormattersAsTransient`.
- **Step 2 done 2026-11-15:** the three, pinned as they are; 206 test cases on the .NET targets, 173 on `net48`. Found:
  the XML and SOAP formatters do not observe the token (the XML write only on the .NET targets, and after writing the
  whole document); see B3.

### A7. The README tells the truth [x] 2026-11-15

Done, every point below, with the version 3.3.1 and a "Cancellation" subsection; the new and changed examples compiled
and run (`AI/HISTORY.md`).

Correct the README to the code as it is (the list in `AI/OPEN.md`):
- the header: the version, and the targets with `net48` (without Brotli);
- `DataFormatterExtensions.ReadAsync` and `WriteAsync`, renamed in 3.2.5. Mind that `formatter.WriteAsync(data, stream)`
  binds to the formatter's own method, without GZip; compression needs `compress: true` or a call through
  `DataFormatterExtensions` (`AI/DESIGN.md`, "Binding");
- GZip's returned stream is at position 0 since 3.2.5, Brotli's and AES's at the end;
- `GZipByteArrayFormatter.WriteAsync` checks `BufferSize`, since 3.1.0;
- `AddForgeFormattersAsTransient` (3.2.2), and three entry points, not two;
- the error table: an error from an exception is `Unexpected`, one per exception in the inner chain, with the metadata
  `type`, `message` and `stackTrace`; a cancellation is such an error too (B3);
- a "Security notes" section:
  - the AES output has no integrity protection, and one formatter uses one IV for every message (B1);
  - the certificate's IV and key come from its public key, so anyone who has the certificate can decrypt (B2);
  - the SOAP formatter creates the types its input names, so it reads trusted input only, unless the caller passes a
    `SoapFormatter` with its own `Binder`.

## Release B

### B1. An authenticated AES output with an IV per message [x] 2026-11-15

Done as decided, in 3.4.0 (`AI/HISTORY.md`). Left for later: the default switch (a major version) and a chunked
format.

**Decided by Zoltán on 2026-11-15:** the format below as proposed, with one refinement: when a header is present but
the tag does not match, and the current format is accepted (`AcceptUnauthenticated`), the input is read the current
way, because a message of the current format starts with the header bytes by chance once in 2^32. Writing: (b),
opt-in, so it goes into 3.4.0. The API: a public enum `AesOutputFormat` (`Legacy = 0`, `Authenticated = 1`) and, on
`ICryptoFormatter<T>` and `CryptoFormatterBase<T>`, `OutputFormat` (default `Legacy`, what `WriteAsync` writes) and
`AcceptUnauthenticated` (default `true`); `ReadAsync` recognizes the format by its header whatever `OutputFormat` is.

- **Problem:** the output is AES-CBC with no header and no authentication tag, and one formatter encrypts every message
  with the same IV, so equal plaintext beginnings give equal ciphertext beginnings, and a changed ciphertext decrypts to
  changed data without an error.
- **Format proposal** (confirm with Zoltán). `AesGcm` does not exist on `net48`, so the format is AES-256-CBC with
  HMAC-SHA256, encrypt-then-MAC, the same on every target:
  - a 4-byte header, e.g. `FNF` and the version byte `0x02`;
  - a random 16-byte IV per message;
  - the ciphertext;
  - a 32-byte HMAC-SHA256 tag over the header, the IV and the ciphertext.
  - Two keys from `Key`: HMAC-SHA256 of `Key` over two fixed labels, e.g. `Forge.Next.Formatters.Aes.Encryption` and
    `Forge.Next.Formatters.Aes.Authentication`. `HKDF` does not exist on `net48`.
  - The `IV` property is then only for reading the current format.
- **Reading:** check the tag before any plaintext leaves the formatter, compared in constant time
  (`CryptographicOperations.FixedTimeEquals` on the .NET targets, an explicit loop on `net48`), then decrypt. The stream
  formatter has to read its input into memory for that: document the memory cost, and leave a chunked format for when a
  caller needs it.
- **The current format:** input without the header is read the current way, with the configured IV and key. A property
  (working name `AcceptUnauthenticated`) lets a caller refuse it; its default is `true` in the release that adds the new
  format, so stored data stays readable.
- **Decision, writing:**
  - (a) the new format by default. An older package version cannot read it, so this is breaking for anyone who reads
    with an older version: a major version;
  - (b) opt-in with a property (working name `Format`) in a minor version, with the default switched in the next major.
    **Recommended.**
- **Tests:**
  - the round trip of both formatters in both formats;
  - one flipped bit anywhere gives an error and writes nothing to the output;
  - a cut-off input gives an error;
  - the A5 outputs still read.
- **Docs:** README (the format, the property, the migration), `CLAUDE.md` invariant 7, `AI/DESIGN.md`, `AI/HISTORY.md`.

### B2. The certificate [x] 2026-11-15 (option (a))

Done with (a) (Zoltán), in 3.4.0: `[Obsolete]` as a warning on the two `Certificate` properties and the three
certificate constructors; the behaviour and the A5 outputs unchanged (`AI/HISTORY.md`). (b) stays an idea until a
caller needs encryption with a certificate.

- **Problem:** `Certificate` copies the IV from the first 16 and the key from the last 32 bytes of the certificate's
  public key (`PublicKey.EncodedKeyValue.RawData`), so anyone who has the certificate can compute them; no private key
  is needed. The README's recipe "Deriving keys from a certificate" presents it as sharing no secret. For an RSA 2048
  certificate 9 bytes of the IV and 5 of the key are even the same for every certificate (found 2026-11-15, A5).
- **Options:**
  - (a) `[Obsolete]` with a warning on the `Certificate` property and the certificate constructors, and the README recipe
    rewritten as a warning; the behaviour stays, so stored data stays readable (a minor version). **Recommended now.**
  - (b) a secure certificate mode in the new format. A random content key per message is wrapped with the certificate's
    RSA public key (RSA-OAEP-SHA256) into the header, and reading needs the private key. It works on `net48` with
    `RSACng`; check that `GetRSAPublicKey` returns one there. **Recommended** when a caller needs encryption with a
    certificate.
  - (c) remove the derivation: a major version.
- **Tests:** the A5 certificate output still reads under (a) and (b).

### B3. Cancellation and the stack trace [x] 2026-11-15

Done with the recommendation, (a) (Zoltán): the token and `rethrowCancellation: true`, the XML/SOAP and `FileInfo`
token checks, `includeStackTrace` unchanged; 3.4.0 (`AI/HISTORY.md`). Forge.Next.Formatters.Newtonsoft follows with its
B1.

- **Problem:** `ProtectAsync` is called without the token, `rethrowCancellation` and `includeStackTrace`. A cancellation
  therefore comes back as an `Unexpected` error instead of an `OperationCanceledException`, and every error from an
  exception carries the stack trace.
- **Recommended:**
  - pass the token and `rethrowCancellation: true`, so the caller's own cancellation throws as .NET expects; a minor
    version, and the README's error table;
  - leave `includeStackTrace` at its default, with the README note of A7.
- **Tests:** a cancelled token throws `OperationCanceledException` from every method; an inner timeout is still an error.
- **Also decide** (found by A6 on 2026-11-15, `AI/OPEN.md`): the XML read and both SOAP methods ignore the token, and
  the XML write observes it only on the .NET targets, after the whole document is written. Recommended: check the token
  once before the synchronous `Serialize` and `Deserialize`, so a cancelled call does no work, the same on every
  target. The A6 tests that pin today's behaviour change with it.

### B4. Smaller behaviour changes [x] 2026-11-16 (each a Decision, a minor version)

- Brotli's and AES's returned streams at position 0, as GZip's are since 3.2.5. **Done 2026-11-15 (option (a)), in
  3.4.0.**
- `SystemJsonFormatter<T>.ReadAsync` checks for a null stream and returns `Validation`, as every other method does;
  today it is `Unexpected`. **Done 2026-11-16, in 3.4.0.**
- A default `Binder` for the SOAP formatter that refuses every type the caller has not allowed; this breaks callers that
  read types they never named, so only on Zoltán's word. The README warning of A7 is the minimum. **Done 2026-11-16 as
  an opt-in (Zoltán's option (b)), in 3.4.0:** `XmlSoapFormatter<T>.WithAllowedTypes` and `SoapAllowedTypesBinder`; on
  `net48` it cannot refuse the types of `mscorlib`. Found on the way and fixed: the SOAP read closed the caller's
  stream.

### B5. A `net11.0` target [x] 2026-11-15

- Forge.Next.Shared 3.3.1 has one. On `net11.0` it has no logging-abstractions reference, so
  `ServiceCollectionExtensions` would take `Microsoft.Extensions.DependencyInjection.Abstractions` from the shared
  framework, which has it (checked on 2026-11-11 in the `Microsoft.NETCore.App.Ref` 11.0.0 reference pack).
- Forge.Next.Formatters.Newtonsoft can only get `net11.0` after this package has it.
- **Done:** Zoltán added `net11.0` and raised Forge.Next.Shared to 3.3.2 on 2026-11-15; checked and documented the
  same day (`AI/HISTORY.md`, `AI/VERIFICATION.md`). The `net11.0` tests come with A6.

### B6. The versioning rule [x] 2026-11-15

- 3.2.5 renamed public methods in a patch version. `CLAUDE.md` (invariant 1) carries over Forge.Next.Shared's rule: a
  breaking change raises at least the minor version. Ask Zoltán to confirm it for this package; it decides the versions
  of B1 to B4.
- **Confirmed by Zoltán on 2026-11-15.** After 3.3.1, a release with B3 or B4 is at least 3.4.0.

## Ideas, only when Zoltán asks

These are clean-ups without a behaviour change; `AI/OPEN.md` has the details.

- C1. `IV` and `Key` keep the caller's array, so setting `Certificate` afterwards overwrites that array in place: copy
  the array in the setters.
- C2. `ms.SetLength(0)` in `AesByteArrayFormatter.WriteAsync` and `BrotliByteArrayFormatter.WriteAsync` does not clear
  the caller's array. If clearing was the intent, `Array.Clear`; otherwise remove it. **Done 2026-11-16: removed.**
- C3. The Brotli interfaces exist on `net48` without an implementation: document it, or leave them out there (which
  removes public types: a version decision).
- C4. The tests: the `DataFormatterExtensionsTests` names still say `Read_` and `Write_`; Shouldly is referenced but not
  used. **Done 2026-11-16.**
- C5. The XML documentation: the summaries that only repeat the type's name, `AesByteArrayFormatter`'s "interface", and
  the empty `T`, `iv` and `key` descriptions. **Done 2026-11-16.**
- C6. `.gitignore`: the `<branch>.txt` that `/crt.commit.msg` writes; a `main.txt` was committed once, in 3.2.2.
  **Done 2026-11-16: `/*.txt`.**
