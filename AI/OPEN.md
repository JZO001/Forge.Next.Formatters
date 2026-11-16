# Open items

As of 2026-11-15. Close an item by moving its outcome into `HISTORY.md` (decisions) or `VERIFICATION.md` (checks), and
deleting it here.

The order and the way to fix them: `PLANS/RECOMMENDED_WAY_TO_FIX.md` (2026-11-12).

## Waiting for Zoltán

- **The AES output, later steps** (B1 is done, opt-in, in 3.4.0): switching the default `OutputFormat` to
  `Authenticated`, and `AcceptUnauthenticated` to `false`, needs a major version (Zoltán decides when). A chunked
  authenticated format, for messages too large for memory, and a secure certificate mode (B2 (b): a per-message key
  wrapped with RSA-OAEP, read with the private key) wait until a caller needs them.

  Changing the derivation or the output makes stored data unreadable (`CLAUDE.md`, invariant 7).
- **The published versions and the local packages.** Tags mark 3.0.0, 3.1.0, 3.2.0, 3.2.1, 3.2.2 and 3.2.5; 3.3.0 has
  none. `artifacts/Forge.Next.Formatters.3.2.5.nupkg` was rebuilt on 2026-11-02 and depends on `Forge.Next.Shared`
  3.3.0, while the 3.2.5 commit references 3.1.0: if 3.2.5 is published, the local file is not the published one, so do
  not publish it again. Release A is committed as 3.3.1, which will not be published (Zoltán, 2026-11-15); the next
  published version is 3.4.0, with B3, B2 (a) and B1, and it carries the new icon; Zoltán publishes it when all the
  planned work is done. The release build into `artifacts/` is Zoltán's.
- **Forge.Next.Formatters.Newtonsoft follows B3** (its plan's B1, the same decision): it can take it once this
  package's 3.4.0 is out.

## Not verified yet

Nothing at the moment.

## Ideas, not decided

- **`IV` and `Key` keep the caller's array:** setting `Certificate` afterwards overwrites that array in place (the
  README says so since 2026-11-15).
- **The SOAP formatter on `net48`:** the SoapFormatter package creates the types of `mscorlib` without asking the
  binder there, so `WithAllowedTypes` refuses only the types of other assemblies on that target (2026-11-16,
  `VERIFICATION.md`; the README says to read trusted SOAP input only on `net48`). A default restrictive binder (B4
  option (c)) would need a major version; Zoltán chose the opt-in (b).
- **The Brotli interfaces on `net48`** have no implementation there (documented in the README since 2026-11-15).
