# Open items

As of 2026-11-15. Close an item by moving its outcome into `HISTORY.md` (decisions) or `VERIFICATION.md` (checks), and
deleting it here.

The order and the way to fix them: `PLANS/RECOMMENDED_WAY_TO_FIX.md` (2026-11-12).

## Waiting for Zoltán

- **The AES output** (B1 of the plan): one IV per formatter, for every message, and no authentication of the
  ciphertext (`DESIGN.md`; the README's "Security notes" say so since 3.4.0). The other two properties of the key
  material are settled (2026-11-15, `HISTORY.md`): the default IV and key come from `RandomNumberGenerator` (A1), and
  the certificate derivation is `[Obsolete]` (B2 (a)). A secure certificate mode (B2 (b): a per-message key wrapped
  with RSA-OAEP, read with the private key) waits until a caller needs one; it would belong to B1's new format.

  Changing the derivation or the output makes stored data unreadable (`CLAUDE.md`, invariant 7).
- **The published versions and the local packages.** Tags mark 3.0.0, 3.1.0, 3.2.0, 3.2.1, 3.2.2 and 3.2.5; 3.3.0 has
  none. `artifacts/Forge.Next.Formatters.3.2.5.nupkg` was rebuilt on 2026-11-02 and depends on `Forge.Next.Shared`
  3.3.0, while the 3.2.5 commit references 3.1.0: if 3.2.5 is published, the local file is not the published one, so do
  not publish it again. Release A is committed as 3.3.1, which will not be published (Zoltán, 2026-11-15); the next
  published version is 3.4.0, with B3 (in the working tree since 2026-11-15), and it carries the new icon. The release
  build into `artifacts/` is Zoltán's.
- **Forge.Next.Formatters.Newtonsoft follows B3** (its plan's B1, the same decision): it can take it once this
  package's 3.4.0 is out.

## Not verified yet

Nothing at the moment.

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
