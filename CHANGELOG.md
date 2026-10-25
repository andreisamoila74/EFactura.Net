# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] — 2026.10.25

First release. Two packages, both targeting `net8.0` and `net10.0`.

### `EFactura.Net.Ubl`

CIUS-RO / EN 16931 UBL 2.1 document model and writer. No third-party dependencies.

- `UblInvoice` and `UblCreditNote` over a closed `UblDocument` base — immutable records with
  `required` members, so a credit note that does not reference the invoice it reverses fails to
  compile.
- `UblWriter` — deterministic XML; the same document always produces the same bytes.
- `UblTotals` — public, returning exactly the figures the writer emits, so a PDF and its XML cannot
  disagree. Rounds to 2 decimals away-from-zero as it accumulates, satisfying EN 16931 `BR-CO-10`
  and `BR-CO-14`.
- All seven VAT categories (`S`, `Z`, `AE`, `E`, `O`, `K`, `G`), with exemption reasons (BT-120 /
  BT-121) and standard fallback wording for the categories that require one.
- Foreign currency: `cac:TaxExchangeRate` plus the second RON VAT total `BR-CO-03` requires.
- `UblUnitCodes` — Romanian unit names to UN/ECE Recommendation 20 codes.
- `RomanianAddress` — county names to ISO 3166-2:RO codes, Bucharest sectors to `SECTORn`.
- Optional strict address validation, reporting every problem at once.

### `EFactura.Net.Client`

ANAF e-Factura REST client, OAuth2 helpers and DI extensions.

- `IAnafEFacturaClient` — `upload`, `stareMesaj`, `descarcare` (ZIP unpacked into document and
  signature), `listaMesajeFactura`, and `listaMesajePaginatieFactura` exposed as a lazy
  `IAsyncEnumerable`.
- `IAnafValidationClient` — `validare` and `transformare` (XML to PDF). **No token required.**
- `IAnafCompanyLookupClient` — VAT-payer registry lookup by CUI, single or batched. **No token
  required.**
- `AnafOAuthClient` — authorization URL, code exchange, refresh.
- `IAnafAccessTokenProvider` — the SDK never stores or refreshes a token; it reads one per request.
- `AddAnafEFactura(…)` — chainable registration that exposes the `IHttpClientBuilder` rather than
  baking in a resilience policy.
- ANAF business outcomes are returned as data (`IsSuccess`, `Errors`, `RawResponse`); only transport
  failures and 401/403 throw.
- Defaults to the ANAF **test** environment.

### Notes

Extracted from a production Romanian invoicing application. Behaviour that had been running against
ANAF was treated as the specification; where this library diverges, it is because ANAF's own
validator said so.

Several rules were discovered the hard way and are now enforced or documented:

- CIUS-RO `UBL-CR-429` **forbids** a bank name on an invoice — only the IBAN may appear, so there is
  no bank-name property to set.
- `BR-AE-10` / `BR-E-10` / `BR-O-10` / `BR-IC-10` / `BR-G-10` require an exemption reason on any VAT
  breakdown carrying no VAT.
- `BR-CO-25` makes a due date effectively mandatory when anything is payable.
- `BR-RO-080` / `BR-RO-090` require a street line and city with real content.
- ANAF validates the Romanian CUI check digit.
- The company registry reports "not found" as HTTP 404 with a normal JSON body — returned as a
  no-result, not an exception.

[Unreleased]: https://github.com/andreisamoila74/EFactura.Net/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/andreisamoila74/EFactura.Net/releases/tag/v0.1.0
