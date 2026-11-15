# Forge.Next.Formatters – notes for AI sessions working on this repository

This repository holds one NuGet package: data formatters behind one small asynchronous contract, `IDataFormatter<T>`,
that return `ErrorOr` results instead of throwing for expected failures. It covers compression (GZip, Brotli), AES
encryption and serialization (XML, SOAP, JSON), file and stream helpers that add an optional GZip layer to any
formatter, and dependency-injection registrations. Read in this order:

1. `README.md`: the user documentation: every formatter and public member, with examples. It is packed into the NuGet
   package. It was brought up to date with the code for 3.3.1 on 2026-11-15, its changed examples compiled and run;
   keep it so.
2. This file: the rules that must hold in every change.
3. `AI/README.md`: the knowledge base: how the package came about and what was decided (`AI/HISTORY.md`), the design
   (`AI/DESIGN.md`), how it is verified (`AI/VERIFICATION.md`), and what is still open (`AI/OPEN.md`).
4. `PLANS/RECOMMENDED_WAY_TO_FIX.md`: the plan for fixing the open findings (written 2026-11-12): the order, the
   way, and the decisions to ask Zoltán before coding. Work through it item by item when Zoltán asks for a fix.

## Working with Zoltán

- **Language:** Zoltán Juhász owns the package. Zoltán writes in Hungarian; answer in Hungarian. Code, comments, commit
  messages and every document in this repository are in English.
- **Git and publishing are Zoltán's:** Zoltán manages the git repository and publishes the NuGet package. Do not run
  git commands that change the repository, and do not publish, unless asked. The `/commit.changes` command is such a
  request: it runs the tests and commits, without pushing. It skips the tests when nothing but the knowledge base, other
  Markdown files, or the instruction and prompt files under `.claude/` and `.github/` changed, and says so in its
  report. `/crt.commit.msg` only writes the message into `<branch>.txt` in the repository root, a file git does not
  ignore.
- **Working style:**
  - one topic at a time;
  - options with a recommendation;
  - facts checked before they are stated;
  - after a work item, report and stop.
- **Keep the knowledge base current:** when a change, a decision or a verification happens, update `AI/` in the same
  step, so the next session can continue from it.
- **No reference to the package's consumers** (Zoltán, 2026-11-11): no name of a product, repository or internal
  decision of a project that uses, or will use, this library, in the code, the documents or this knowledge base.
  Describe a consumer generically ("a desktop application", "a local admin tool"). Allowed:
  - the other `Forge.*` projects;
  - the libraries and tools this package uses or documents: ErrorOr, SoapFormatter, System.Text.Json, the `Microsoft.*`
    libraries, xUnit, Shouldly, NSubstitute.

## Invariants (do not break)

1. **The public API is a contract.** The package is published and has consumers.
   - A new parameter is optional, and its default keeps the earlier behaviour.
   - In async methods the `CancellationToken` stays the last parameter, with `= default`.
   - The lifetimes that the `AddForgeFormatters…` methods register are part of the API.
   - A change that breaks a caller (a removed or renamed member, a changed signature, default or lifetime, or a
     changed behaviour a caller relies on) raises at least the minor version, and the commit message says what breaks
     under "Compatibility". Confirmed by Zoltán for this package on 2026-11-15. 3.1.0 and 3.2.0 raised the minor
     version for their breaking changes; 3.2.5 renamed public methods in a patch version, before the rule was
     confirmed (see `AI/HISTORY.md`).
2. **Two package references; the rest comes through `Forge.Next.Shared`.** The csproj references the same two packages
   on every target:

   | Target | Package references | Used through `Forge.Next.Shared` |
   | --- | --- | --- |
   | `net48` | `Forge.Next.Shared` 3.3.2, `SoapFormatter` 1.1.9 | `ErrorOr` 2.1.1, `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.0, `System.Text.Json` 8.0.6 |
   | `net8.0` | the same | `ErrorOr` 2.1.1, `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.0 |
   | `net9.0` | the same | `ErrorOr` 2.1.1, `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 |
   | `net10.0` | the same | `ErrorOr` 2.1.1, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 |
   | `net11.0` | the same | `ErrorOr` 2.1.1; the DI abstractions come with the `Microsoft.NETCore.App` shared framework |

   The public API returns `ErrorOr` types, `ServiceCollectionExtensions` needs the DI abstractions (on `net48` to
   `net10.0` they come with `Microsoft.Extensions.Logging.Abstractions`, a dependency of `Forge.Next.Shared`; on
   `net11.0` with the shared framework), and `SystemJsonFormatter<T>` needs System.Text.Json on `net48`. A new `Forge.Next.Shared` version can change all three: check the restored graph
   (`Forge.Next.Formatters/obj/project.assets.json`) and the package's `.nuspec`. Do not add a package without asking.
3. **One source for every target.** `net48` is the only target without `NETCOREAPP`. Code that cannot exist there sits
   behind `#if NETCOREAPP`: the two Brotli formatters and their registrations, `FlushFinalBlockAsync` and
   `DisposeAsync`, each with a synchronous fallback. The README says which targets lack a feature.
4. **Expected failures are `ErrorOr` values, not exceptions.**
   - Every method that does work checks its arguments first and returns a `Validation` error whose description is the
     parameter name (`inputStream`, `outputStream`, `data`, `BufferSize`, `formatter`, `stream`, `file`).
     `SystemJsonFormatter<T>.ReadAsync` is the one such method without a null check.
   - The work runs inside `ProtectAsync` from `Forge.Next.Shared`, with the caller's token and
     `rethrowCancellation: true` (since 3.4.0, B3): the caller's own cancellation throws `OperationCanceledException`;
     every other exception, a cancellation the token did not request included, becomes `Unexpected` errors, one per
     exception in the inner-exception chain, with the metadata `type`, `message` and `stackTrace` (`includeStackTrace`
     at its default). The XML and SOAP formatters and the `FileInfo` overloads of `DataFormatterExtensions` check the
     token before their work, so a cancelled call does nothing. Changing any of this changes what callers get: a public
     change.
   - The two-stream `ReadAsync` of the XML, SOAP and JSON formatters returns `Forbidden`, "Method not implemented.".
   - Besides the caller's own cancellation, only constructors and property setters throw: `ArgumentNullException` for a
     null certificate, IV, key or `SoapFormatter`, `InvalidDataException` for an IV that is not 16 bytes or a key that
     is not 32, and `ArgumentOutOfRangeException` for an undefined `OutputFormat`. The stream overloads of
     `DataFormatterExtensions` let an exception of an application's own formatter pass (`AI/DESIGN.md`).
5. **The caller's streams stay open.** The compression streams and the XML reader and writer are created with
   `leaveOpen: true`, and the AES `CryptoStream` is never disposed. Callers rewind and reuse a stream after
   `WriteAsync`; the README's examples and the tests do. Since 3.4.0 the AES read looks at the first 4 bytes of a
   seekable input and goes back to where it started; an input that cannot seek is copied into memory first.
6. **The returned streams.** `GZipStreamFormatter.ReadAsync(stream)` returns a new `MemoryStream` at position 0 (since
   3.2.5; `DataFormatterExtensions.ReadAsync` depends on it). `BrotliStreamFormatter` and `AesStreamFormatter` return
   theirs at the end. The caller owns the returned stream.
7. **Data written earlier stays readable.** The `Legacy` AES output (the default) is the bare ciphertext of
   `Aes.Create()` with its default mode and padding (the code sets neither), without a header. The `Authenticated`
   output (since 3.4.0, B1) is the header `FNF` `0x02`, a random IV, the AES-256-CBC-PKCS7 ciphertext under a key
   derived from `Key`, and an HMAC-SHA256 tag over all of them under a second derived key; the two keys are HMAC-SHA256
   of `Key` over the labels `Forge.Next.Formatters.Aes.Encryption` and `Forge.Next.Formatters.Aes.Authentication`.
   Reading recognizes both by the header. A certificate gives the IV from the first 16 and the key from the last 32
   bytes of `PublicKey.EncodedKeyValue.RawData`; its members are `[Obsolete]` (a warning) since 3.4.0 (B2), but work as
   before, so data written with them stays readable. The `compress` layer of the extensions is plain GZip.
   Changing any of these makes stored data unreadable: treat it as a breaking change. The known-answer tests pin
   them on every target (`…_NistVector_…` with NIST SP 800-38A F.2.5, `…_FixedCertificate_…`, and
   `…_AuthenticatedKnownAnswer_…` with `CryptoFormatterBaseTests.AuthenticatedMessage`): a change that makes one of
   them fail is such a change.

## Conventions

- **Code style:**
  - explicit types (no `var`), file-scoped namespaces, one type per file named after the type, all in the one namespace
    `Forge.Next.Formatters` (no sub-folders);
  - a formatter is a public interface (`I<Name>`) and a public class that implements it; compression and AES come in a
    byte-array and a stream variant;
  - `LangVersion` 12.0 for every target, `Nullable` and `ImplicitUsings` on;
  - `ConfigureAwait(false)` on every `await` in the library;
  - XML documentation on every public member (`GenerateDocumentationFile`; a missing one is warning CS1591).
- **Tests** (`tests/Forge.Next.Formatters.Tests`, every target of the library, `LangVersion` 12.0):
  - on `net48` the two Brotli test files are left out in the csproj, and Brotli cases in other test files sit behind
    `#if NETCOREAPP`;
  - xUnit 2 with its `Assert`, NSubstitute for mocks; Shouldly is referenced but not used;
  - one test class per tested class (`<Class>Tests`, in a file of that name, in one flat folder); a sample data type
    sits in the test file that uses it;
  - test methods named `Member_Condition_Outcome_Test` (in `ConstsTests`: `<Member>Test`), with XML summaries and
    Arrange / Act / Assert comments (see `prompts/init_tests.txt`);
  - the AES tests use the self-signed RSA certificate of `CryptoFormatterBaseTests.CreateSelfSignedCertificate()`;
    the known-answer data (the NIST values, the fixed certificate and its outputs, `FromHex`) sits in
    `CryptoFormatterBaseTests` too; its constants were computed once, independently of the formatters, so do not
    regenerate them from the formatters' output;
  - temporary files get a GUID name under `Path.GetTempPath()` and are deleted in `finally`.
- **Before handing over a change:**
  - a 0-warning build of the solution, all targets;
  - all tests;
  - when a target was added or a dependency changed: the package's `lib/` folders, XML documentation files and `.nuspec`
    dependency groups checked, from a Release build into a folder outside the repository (see `AI/VERIFICATION.md`).
- **Documentation:** every public change is reflected in `README.md` in the same step: the signature, an example, and
  the target availability in the header's "Target frameworks".
- **Release:**
  1. Raise `AssemblyVersion` in `Forge.Next.Formatters/Forge.Next.Formatters.csproj` (`Version` and `FileVersion`
     follow it).
  2. Set the same version in the README's header (`**Package version:**`).
  3. Run `dotnet build -c Release`; it writes the package to `artifacts/` (`GeneratePackageOnBuild` in Release,
     `PackageOutputPath` `../artifacts/`). A Release build overwrites the package of the current version there, so
     build a published version again only on purpose. To check a package without touching `artifacts/`, build the
     library project with `-p:PackageOutputPath=<another folder>\`.
  4. Zoltán publishes it.
