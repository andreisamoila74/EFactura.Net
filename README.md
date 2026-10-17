# EFactura.Net

[![CI](https://github.com/andreisamoila74/EFactura.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/andreisamoila74/EFactura.Net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EFactura.Net.Client.svg)](https://www.nuget.org/packages/EFactura.Net.Client)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A .NET SDK for Romania's **RO e-Factura**: build CIUS-RO UBL 2.1 invoices and credit notes, and talk
to the ANAF e-Factura API.

Getting a document *accepted* by ANAF takes more than valid XML. The rules that bite are the ones you
only discover on rejection — a reverse-charge line needs a stated exemption reason, a bank name is
forbidden outright, a positive balance needs a due date, VAT subtotals must sum to the stated total
to the ban. This library knows those, and its test suite checks every example against ANAF's own
validator rather than against someone's reading of the spec.

> **Unofficial.** Not affiliated with, endorsed by, or supported by ANAF or the Romanian Ministry of
> Finance. Always verify output against the official specification before relying on it.

| Package | What it does | Dependencies |
|---|---|---|
| [`EFactura.Net.Ubl`](src/EFactura.Net.Ubl) | CIUS-RO / EN 16931 document model, XML writer, totals | **none** |
| [`EFactura.Net.Client`](src/EFactura.Net.Client) | ANAF REST API, OAuth2 helpers, company lookup, DI | `Microsoft.Extensions.*` |

```bash
dotnet add package EFactura.Net.Client   # includes .Ubl
dotnet add package EFactura.Net.Ubl      # XML only, no HTTP
```

Targets **.NET 8** and **.NET 10**.

---

## Quickstart

Build an invoice, check it with ANAF, send it, wait for the verdict:

```csharp
using EFactura.Net.Client;
using EFactura.Net.Ubl;

var invoice = new UblInvoice
{
    DocumentId = "FCT nr. 1",
    IssueDate  = new DateOnly(2026, 1, 15),
    DueDate    = new DateOnly(2026, 2, 14),   // BR-CO-25: required when anything is payable
    CurrencyCode = "RON",

    Seller = new UblParty
    {
        Name = "Furnizor Test SRL",
        VatIdentifier = "RO12345674",
        Address = new UblAddress
        {
            StreetName = "Str. Exemplu nr. 1",
            CityName = "Cluj-Napoca",
            CountrySubdivision = "Cluj",
        },
    },
    Buyer = new UblParty
    {
        Name = "Client Test SRL",
        VatIdentifier = "RO87654329",
        Address = new UblAddress
        {
            StreetName = "Calea Victoriei nr. 10",
            CityName = "Sector 1 Mun. București",   // normalised to SECTOR1 for you
            CountrySubdivision = "București",
        },
    },

    PaymentMeans = new UblPaymentMeans
    {
        PayeeAccount = new UblBankAccount { Iban = "RO49 AAAA 1B31 0075 9384 0000" },
    },

    Lines =
    [
        new UblLine { Name = "Consultanță software", Quantity = 10m, Unit = "ore", UnitPrice = 250m, TaxRate = 21m },
    ],
};

var xml = UblWriter.Write(invoice);

// Optional but worth it: ANAF's validator is unauthenticated, so this costs one
// round trip and turns a rejection you would meet minutes later into an answer now.
var check = await validationClient.ValidateAsync(xml, AnafValidationStandard.Invoice);
if (!check.IsValid)
{
    foreach (var error in check.Errors) Console.WriteLine(error.Message);
    return;
}

var upload = await client.UploadAsync("RO12345674", xml, AnafUploadStandard.Ubl);
if (!upload.IsSuccess)
{
    foreach (var error in upload.Errors) Console.WriteLine(error.Message);
    return;
}

// Poll until ANAF decides, then fetch the signed document.
var status = await PollUntilFinalAsync(client, upload.UploadIndex!);   // see docs/polling.md
if (status.State == AnafMessageState.Ok)
{
    var signed = await client.DownloadAsync(status.DownloadId!);
    // signed.InvoiceXml, signed.SignatureXml, signed.ZipContent
}
```

Setup:

```csharp
services.AddAnafEFactura(o => o.Environment = AnafEnvironment.Test)
        .AddAccessTokenProvider<MyTokenProvider>()
        .AddValidationClient()
        .AddCompanyLookup();
```

A runnable version of all of this is in [`samples/`](samples/EFactura.Net.Samples.Console):

```bash
dotnet run --project samples/EFactura.Net.Samples.Console
```

---

## What it covers

| ANAF endpoint | Method | Auth |
|---|---|:--:|
| `POST /upload` | `UploadAsync` | token |
| `GET /stareMesaj` | `GetStatusAsync` | token |
| `GET /descarcare` | `DownloadAsync` — unpacks the ZIP | token |
| `GET /listaMesajeFactura` | `ListMessagesAsync` | token |
| `GET /listaMesajePaginatieFactura` | `ListMessagesPagedAsync` — paging hidden behind `IAsyncEnumerable` | token |
| `POST /validare/{FACT1\|FCN}` | `ValidateAsync` | **none** |
| `POST /transformare/{…}` | `ConvertToPdfAsync` | **none** |
| `PlatitorTvaRest/v9/tva` | `GetByCuiAsync`, `GetManyByCuiAsync` | **none** |
| OAuth2 authorize / token / refresh | `AnafOAuthClient` | — |

Documents: invoices (`380`) and credit notes (`381`), all seven VAT categories
(`S`, `Z`, `AE`, `E`, `O`, `K`, `G`), foreign currency with the RON VAT restatement EN 16931 requires,
Romanian unit codes, county codes and Bucharest sector normalisation.

### Design decisions you will notice

- **ANAF's business outcomes are data, not exceptions.** A rejected document, a validation failure,
  an empty message list — all come back on the result with `IsSuccess`, `Errors` and `RawResponse`.
  Only transport failures and 401/403 throw. You will not be writing `catch` around a rejection.
- **The SDK never stores a token.** You implement `IAnafAccessTokenProvider` over your own storage;
  the token is read once per request, so a refresh takes effect immediately.
- **No resilience policy is baked in.** How hard to retry a tax authority is your call — the builder
  hands back the `IHttpClientBuilder` so you can add Polly or not.
- **Totals are public.** `UblTotals.Calculate` returns the exact figures the writer emitted, so your
  PDF and your XML cannot disagree by a ban.
- **Test is the default environment.** An SDK that silently points at the live tax authority is the
  wrong failure mode.

---

## Documentation

| Guide | |
|---|---|
| [Getting started](docs/getting-started.md) | installation, DI and non-DI setup, environments, token providers |
| [Building invoices](docs/invoices.md) | mandatory fields, VAT categories, foreign currency, addresses |
| [Credit notes](docs/credit-notes.md) | storno documents and referencing the original |
| [The API client](docs/api-client.md) | every method, ANAF's states, the error model, paging |
| [OAuth2](docs/oauth.md) | SPV registration, the certificate requirement, code exchange, refresh |
| [Polling for status](docs/polling.md) | a backoff that works, and why scheduling is not in the SDK |
| [Troubleshooting](docs/troubleshooting.md) | real ANAF rejection codes and what to do about them |
| [Testing](docs/testing.md) | running the suite, the opt-in integration tests |

---

## Roadmap

Not in `0.1.0`, in rough order of usefulness:

- **Local Schematron validation.** Today, CIUS-RO business rules are checked by calling ANAF.
  Vendoring the EN 16931 + CIUS-RO Schematron would give the same verdict offline and in unit tests.
- **Reading documents.** The library writes UBL; it does not parse it. Parsing the invoices you
  *receive* through `listaMesajeFactura` is the obvious next step.
- **Allowances and charges** (`cac:AllowanceCharge`, BG-20/BG-21) — document- and line-level discounts.
- **Payment terms** (BT-20), as an alternative to a due date for satisfying BR-CO-25.
- **More document types**: `384` (corrected), `389` (self-billed), and `RASP` buyer responses.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Security issues: [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © Andrei Samoila. Third-party notices: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
