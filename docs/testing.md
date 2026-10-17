# Testing

```bash
dotnet test -c Release
```

Runs 307 tests per target framework across `net8.0` and `net10.0`. The integration suite skips itself
unless you opt in, so this needs no configuration and no network.

With coverage:

```bash
dotnet test -c Release -- --coverage --coverage-output-format cobertura
```

---

## The three layers

### `EFactura.Net.Ubl.Tests` — no network

Thirteen document scenarios, each checked three ways:

1. **Snapshot** against a committed `.verified.xml`.
2. **XSD validation** against the 15 vendored OASIS UBL 2.1 schemas under `Schemas/`.
3. **Totals agreement** — `UblTotals.Calculate` must match the amounts actually written.

Plus rounding invariants, all 42 counties, all 24 unit spellings, and the writer options.

### `EFactura.Net.Client.Tests` — no network

Every endpoint's URL, query, method, body and auth header, driven through the real handler chain with
`RichardSzalay.MockHttp`. Fourteen sanitised ANAF response fixtures under `Fixtures/` cover each
documented response state. Also paging, laziness, cancellation and DI registration.

### `EFactura.Net.IntegrationTests` — real ANAF, opt-in

Skipped unless the gating environment variables are set; each test reports its own skip reason.

---

## Running the integration tests

### Without credentials

Six of the eight need **no authentication at all** — `validare`, `transformare` and the company
registry are public endpoints:

```bash
ANAF_INTEGRATION=1 dotnet test tests/EFactura.Net.IntegrationTests -c Release
```

```powershell
$env:ANAF_INTEGRATION = "1"; dotnet test tests/EFactura.Net.IntegrationTests -c Release
```

This is the layer that actually answers *"would ANAF accept this?"*. The XSD tests prove the document
is structurally valid UBL; only ANAF's validator checks the CIUS-RO business rules on top — and those
are the rules that reject documents in production.

### With credentials

The upload round trip needs a token for the ANAF **test** environment:

```bash
export ANAF_INTEGRATION=1
export ANAF_TEST_ACCESS_TOKEN="..."   # test environment, not production
export ANAF_TEST_CIF="..."            # the CIF that token is authorised for

dotnet test tests/EFactura.Net.IntegrationTests -c Release
```

| Variable | Enables |
|---|---|
| `ANAF_INTEGRATION` | validation, PDF conversion, company lookup |
| `ANAF_TEST_ACCESS_TOKEN` + `ANAF_TEST_CIF` | upload → poll → download, message listing |

> Never set a **production** token. The round trip files a real document.

---

## Snapshots

Snapshot comparison is a small helper rather than a library dependency — see
`tests/EFactura.Net.Ubl.Tests/Snapshot.cs` for why.

A mismatch writes a `.received.xml` next to the `.verified.xml` and prints the first differing line.
Diff them; if the change is intended, re-bless:

```bash
EFACTURA_ACCEPT_SNAPSHOTS=1 dotnet test tests/EFactura.Net.Ubl.Tests -c Release
```

**Review the diff before committing.** That variable is how an unintended wire-format change gets
blessed by accident.

Both target frameworks share one snapshot set, deliberately: identical input must produce identical
bytes on each.

---

## Test data

Everything is synthetic. No value came from a production or development database.

| Value | What it is |
|---|---|
| `12345674`, `87654329` | Invented CUIs — **checksum-valid**, because ANAF verifies the check digit |
| `99999992` | Unassigned; used for the not-found path |
| `RO49AAAA1B31007593840000` | The ISO 13616 documentation IBAN |
| `Furnizor Test SRL`, `Client Test SRL` | Invented companies |
| `4221306` | ANAF's **own** CIF, in one lookup test — a public institution |

A Romanian CUI carries a check digit: weights `753217532` right-aligned against the body, sum the
products, ×10, mod 11, with 10 becoming 0. Invented numbers usually fail it, which shows up as
`ERRIdentif: CUI cumparator incorect`.

---

## Writing tests

- **No wall-clock time.** Inject `TimeProvider`; `FakeTimeProvider` is already referenced.
- **No network outside the integration project.** Use `MockHttpMessageHandler`.
- **Fixtures must be synthetic.** Never paste a real ANAF response containing real company data.
- **Snapshots stay LF.** `.gitattributes` enforces it so Windows and Linux agree.

---

## Continuous integration

CI runs build, test and pack on both target frameworks, on Ubuntu and Windows. The credential-free
integration tests run on `main` — they need no secrets, and they are what catches a CIUS-RO rule
changing underneath us.
