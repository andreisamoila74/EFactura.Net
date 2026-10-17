# Contributing

Thanks for taking a look. Issues and pull requests are welcome.

## Build and test

```bash
git clone https://github.com/andreisamoila74/EFactura.Net.git
cd EFactura.Net

dotnet build -c Release     # must be warning-free
dotnet test  -c Release
dotnet pack  -c Release -o ./artifacts
```

You need the **.NET 10 SDK**; `global.json` pins it with `rollForward: latestFeature`. Both packages
target `net8.0` and `net10.0`, and the suite runs against both.

Warnings are errors. That is deliberate — it is what keeps missing XML documentation, culture-
sensitive string operations and unreviewed public API from accumulating.

## Before opening a pull request

```bash
dotnet build -c Release    # 0 warnings
dotnet test  -c Release    # all green
```

If you changed the public API, the build will fail until you update `PublicAPI.Unshipped.txt` in the
affected project. That is the analyzer doing its job: every addition to the public surface should be
a conscious act. Add the symbols it names.

If you changed what the XML looks like, snapshots will fail. Diff the generated `.received.xml`
against the `.verified.xml`, and if the change is intended:

```bash
EFACTURA_ACCEPT_SNAPSHOTS=1 dotnet test tests/EFactura.Net.Ubl.Tests -c Release
```

**Review that diff.** Re-blessing without reading it is how a wire-format regression ships.

## Integration tests

They are skipped by default. Most need no credentials, because ANAF's validator, PDF converter and
company registry are public:

```bash
ANAF_INTEGRATION=1 dotnet test tests/EFactura.Net.IntegrationTests -c Release
```

Please run these before submitting a change that touches XML generation. They are the only check that
ANAF actually accepts the output — see [docs/testing.md](docs/testing.md).

The upload round trip additionally needs `ANAF_TEST_ACCESS_TOKEN` and `ANAF_TEST_CIF`, for the
**test** environment. Never point them at production.

## Test data

Every fixture and example must be synthetic — no value from a real database, invoice or company.

- Tax identifiers must be **checksum-valid** (ANAF verifies the check digit) and obviously invented:
  `12345674`, `87654329`.
- IBAN: `RO49AAAA1B31007593840000`, the ISO 13616 documentation example.
- Companies: `Furnizor Test SRL`, `Client Test SRL`.

A pull request containing a real CUI, IBAN, company name or CNP will be asked to rewrite its history,
not just its files.

## Style

Mostly enforced by `.editorconfig` and the analyzers, so the build will tell you. Worth knowing:

- File-scoped namespaces, explicit accessibility, `ConfigureAwait(false)` on every library `await`.
- Public members need XML documentation, and model properties should cite their EN 16931 **BT-/BG-**
  code — that is the single most useful thing for a consumer reading IntelliSense.
- Comments should explain *why*, especially where ANAF's behaviour is surprising. There is a lot of
  that here, and a comment recording which rule forced a decision saves the next person a rejection.
- Tests read as sentences: `Credit_note_omits_due_date`.

## Reporting a problem with ANAF's rules

The most valuable issues are "ANAF rejected a document this library generated". Please include:

- the rule code (`BR-CO-25`, `UBL-CR-429`, …) and ANAF's message verbatim;
- a minimal document that reproduces it, **with identifiers replaced**;
- whether `ValidateAsync` reproduces it.

## Security

Do not open a public issue for a vulnerability — see [SECURITY.md](SECURITY.md).

## License

Contributions are accepted under the [MIT License](LICENSE).
