# Open items

As of 2026-11-15. Close an item by moving its outcome into `HISTORY.md` (decisions) or `VERIFICATION.md` (checks), and
deleting it here.

The order and the way to fix them: `PLANS/RECOMMENDED_WAY_TO_FIX.md` (2026-11-12).

## Waiting for Zoltán

- **The AES key material.** Two properties of the code, each a decision (the third, `System.Random` for the default
  IV and key, was fixed on 2026-11-15, see `HISTORY.md`):
  - a certificate gives the IV and the key from its public key, so anyone who has the certificate computes them, no
    private key needed. Since 2026-11-15 the README warns about it (the recipe and "Security notes"); the behaviour
    stays until B2. For an RSA 2048 certificate, moreover, 9 bytes of the IV and 5 of the key are the same for every
    certificate (`DESIGN.md`);
  - one IV per formatter, for every message, and no authentication of the ciphertext (`DESIGN.md`; the README's
    "Security notes" say so since 2026-11-15; B1).

  Changing the derivation or the output makes stored data unreadable (`CLAUDE.md`, invariant 7).
- **The published versions and the local packages.** Tags mark 3.0.0, 3.1.0, 3.2.0, 3.2.1, 3.2.2 and 3.2.5; 3.3.0 has
  none. `artifacts/Forge.Next.Formatters.3.2.5.nupkg` was rebuilt on 2026-11-02 and depends on `Forge.Next.Shared`
  3.3.0, while the 3.2.5 commit references 3.1.0: if 3.2.5 is published, the local file is not the published one, so do
  not publish it again. Release A is committed as 3.3.1, which will not be published (Zoltán, 2026-11-15); the next
  published version is 3.4.0, with B3, and it carries the new icon. The release build into `artifacts/` is Zoltán's.
- **The serializers and the token** (found 2026-11-15, pinned by tests and described in the README's "Cancellation";
  input for B3 of the plan):
  - the XML read and both SOAP methods do not use the token: a cancelled token does not stop them;
  - the XML write passes the token only to `StreamWriter.FlushAsync`, which has no token overload on `net48`: a
    cancelled token gives an `Unexpected` error on the .NET targets and success on `net48`;
  - on the .NET targets that error comes after the data is written: `Serialize` fills the writer, the cancelled
    `FlushAsync` throws, and disposing the `StreamWriter` flushes the whole XML into the output anyway. The caller gets
    an error while the output holds the complete document. Checking the token before `Serialize` would make a cancelled
    write write nothing, the same on every target.

## Not verified yet

- **`DataFormatterExtensions` and the token:** the extensions pass the token on to the formatters (the mocks of
  `DataFormatterExtensionsTests` accept any token); no test runs them with a cancelled one.

## Ideas, not decided

- **Brotli's and AES's returned streams at position 0,** like GZip's since 3.2.5: a behaviour change, with the README.
- **`IV` and `Key` keep the caller's array:** setting `Certificate` afterwards overwrites that array in place (the
  README says so since 2026-11-15).
- **The SOAP formatter and untrusted input:** `SoapFormatter.Deserialize` creates the types that the payload names,
  unless the caller passes a `SoapFormatter` with its own `Binder`. The README warns since 2026-11-15; a default
  `Binder` is B4.
- **`ms.SetLength(0)`** in `AesByteArrayFormatter.WriteAsync` and `BrotliByteArrayFormatter.WriteAsync`, just before the
  `MemoryStream` over the caller's array is disposed, has no effect: it does not clear the array.
- **`SystemJsonFormatter<T>.ReadAsync`** has no null check: a null stream gives `Unexpected`, every other method a
  `Validation` error. The README documents the difference.
- **The Brotli interfaces on `net48`** have no implementation there (documented in the README since 2026-11-15).
- **The tests:** the `DataFormatterExtensionsTests` method names still say `Read_` and `Write_`; Shouldly is referenced
  but not used.
- **The XML documentation:** several summaries only repeat the type's name ("Interface for IDataFormatter");
  `AesByteArrayFormatter`'s calls the class an interface; the `T` of `CryptoFormatterBase<T>`, `XmlDataFormatter<T>` and
  `XmlSoapFormatter<T>`, and the `iv` and `key` of `CryptoFormatterBase(byte[], byte[])`, have empty descriptions.
- **`<branch>.txt`**, which `/crt.commit.msg` writes, is not ignored by git; a `main.txt` was committed once, in 3.2.2.
