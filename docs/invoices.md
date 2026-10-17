# Building invoices

Every snippet here is compiled in
[`samples/.../Snippets/BuildingDocuments.cs`](../samples/EFactura.Net.Samples.Console/Snippets/BuildingDocuments.cs)
and validated against ANAF's own validator by the integration suite.

---

## The minimum ANAF accepts

Smaller than this and you get a rejection:

```csharp
var invoice = new UblInvoice
{
    DocumentId = "FCT nr. 1",
    IssueDate  = new DateOnly(2026, 1, 15),
    DueDate    = new DateOnly(2026, 2, 14),
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
    Buyer = /* same shape */,

    Lines = [new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m }],
};
```

Three of those are easy to get wrong:

- **`DueDate`.** Not optional in practice. `BR-CO-25` requires a due date *or* payment terms whenever
  the payable amount is positive, and this library does not yet support payment terms — so set a due
  date.
- **`StreetName` and `CityName`.** `BR-RO-080` and `BR-RO-090` require both, with actual content. An
  empty string is not content; the writer omits empty elements rather than emitting hollow ones, and
  [strict mode](#strict-validation) will tell you before ANAF does.
- **`VatIdentifier`.** ANAF validates the CUI **check digit**. An invented number like `12345678`
  fails it — `12345674` is the valid form of the same body. The `RO` prefix is added for you.

---

## Fields

### Document

| Property | BT | Notes |
|---|---|---|
| `DocumentId` | BT-1 | The full human-readable identifier. Compose it yourself — `$"{series} nr. {number}"` is the common Romanian form. |
| `IssueDate` | BT-2 | |
| `DueDate` | BT-9 | Effectively required; see above. |
| `CurrencyCode` | BT-5 | ISO 4217. |
| `TaxCurrencyCode` | BT-6 | Only when VAT is accounted in another currency — see [foreign currency](#foreign-currency). |
| `TaxExchangeRate` | — | Needed to emit the second VAT total. |
| `Note` | BT-22 | Free text. |
| `BuyerReference` | BT-10 | Purchase-order reference, often mandatory for public-sector buyers. |
| `InvoiceTypeCode` | BT-3 | Defaults to `380`. |

### Party

| Property | BT | Notes |
|---|---|---|
| `Name` | BT-27 / BT-44 | Registered legal name. |
| `VatIdentifier` | BT-31 / BT-48 | With or without `RO`; normalised on write. |
| `IsVatRegistered` | — | Defaults to `true`. When `false`, no `PartyTaxScheme` is written and the identifier moves to `PartyLegalEntity`. |
| `LegalRegistrationId` | BT-30 / BT-47 | Trade-register number. Written to `PartyLegalEntity` for the seller and `PartyIdentification` for the buyer — the asymmetry is what ANAF expects. |
| `Address` | BG-5 / BG-8 | |

### Line

| Property | BT | Notes |
|---|---|---|
| `Name` | BT-153 | |
| `Quantity` | BT-129 | Written with four decimals. |
| `Unit` | BT-130 | Romanian name — mapped to a UN/ECE code. See [units](#units). |
| `UnitPrice` | BT-146 | Net price. |
| `TaxRate` | BT-152 | A percentage: `21m` means 21%. |
| `TaxCategory` | BT-151 | Defaults to standard rate. See [VAT categories](#vat-categories). |
| `TaxExemptionReason` | BT-120 | Required for the zero-VAT categories. |
| `TaxExemptionReasonCode` | BT-121 | Optional alternative to the text. |
| `Description` | BT-154 | |

---

## VAT categories

`TaxCategory` is **not** inferred from `TaxRate`, and that is deliberate: a 0% rate can mean an
exemption, a reverse charge, an intra-community supply or an export, and each has different legal
consequences for the buyer. Guessing would be wrong.

| `UblTaxCategory` | Code | Needs a reason |
|---|---|:--:|
| `StandardRate` | `S` | no |
| `ZeroRated` | `Z` | no |
| `ReverseCharge` | `AE` | **yes** |
| `Exempt` | `E` | **yes** |
| `NotSubject` | `O` | **yes** |
| `IntraCommunity` | `K` | **yes** |
| `Export` | `G` | **yes** |

Every category that carries no VAT must say why — `BR-AE-10`, `BR-E-10`, `BR-O-10`, `BR-IC-10`,
`BR-G-10`. ANAF rejects documents that omit it:

```csharp
new UblLine
{
    Name = "Lucrări de construcții",
    Quantity = 1m,
    UnitPrice = 5000m,
    TaxRate = 0m,
    TaxCategory = UblTaxCategory.ReverseCharge,
    TaxExemptionReason = "Taxare inversă conform art. 331 Cod Fiscal",
}
```

If you leave the reason out, the standard wording for that category is emitted (`"Reverse charge"`,
`"Exempt from VAT"`, …) so the document is still accepted. **Treat that as a safety net, not a
default** — the generic text tells the buyer's accountant nothing, and the legal basis is what they
need.

The reason is stated once per VAT breakdown, so annotating a single line in a group is enough.

---

## Totals and rounding

Totals are computed from the lines; you never set them. Every intermediate value is rounded to two
decimals away-from-zero *as it accumulates*, which is what keeps the printed figures consistent —
subtotals sum exactly to the VAT total (`BR-CO-14`) and line amounts to the line total (`BR-CO-10`).

The same numbers are available to you:

```csharp
var totals = UblTotals.Calculate(invoice);

totals.LineExtensionAmount;   // BT-106
totals.TaxAmount;             // BT-110
totals.PayableAmount;         // BT-115
totals.TaxSubtotals;          // BG-23, one per category and rate
```

Use these when rendering a PDF or writing a ledger entry. Recomputing independently is how a document
and its XML end up disagreeing by a ban.

---

## Foreign currency

An invoice in EUR still has to state its VAT in RON (BT-6 / `BR-CO-03`):

```csharp
var invoice = baseInvoice with
{
    CurrencyCode = "EUR",
    TaxCurrencyCode = "RON",
    TaxExchangeRate = 4.9771m,
};
```

With the rate supplied, the writer emits `cac:TaxExchangeRate` and a second `cac:TaxTotal` carrying
the VAT converted to RON. **Without it, neither is written** — so an EUR invoice with
`TaxCurrencyCode` but no rate will be rejected. Omit `TaxCurrencyCode` entirely for a RON invoice.

---

## Addresses

County names and Bucharest sectors are normalised for you:

```csharp
new UblAddress
{
    StreetName = "Calea Victoriei nr. 10",
    CityName = "Sector 1 Mun. București",   // -> SECTOR1
    CountrySubdivision = "București",        // -> RO-B
}
```

- `CountrySubdivision` accepts a county name, with or without diacritics, with or without a
  `Județul` / `Municipiul` prefix — or an ISO code (`RO-GJ`) which passes through.
- `CityName` has administrative prefixes stripped (`Mun.`, `Comuna`, `Oraș`…), and Bucharest sectors
  become the `SECTOR1`…`SECTOR6` tokens ANAF requires.

> **An unrecognised county silently becomes `RO-B` (Bucharest).** That is the historical behaviour
> and the default. A typo therefore ships a Bucharest address rather than failing — which is why
> strict mode exists.

### Strict validation

```csharp
var xml = UblWriter.Write(invoice, new UblWriteOptions { StrictAddressValidation = true });
```

Throws `UblValidationException` for an unresolvable county, a missing street line or a missing city,
reporting **every** problem at once rather than the first. Recommended for new integrations.

---

## Units

`Unit` takes the Romanian name; the UN/ECE Recommendation 20 code is looked up on write.

| Unit | Code | | Unit | Code |
|---|---|---|---|---|
| `buc`, `bucata`, `bucati` | `H87` | | `kg`, `kilogram` | `KGM` |
| `ora`, `ore` | `HUR` | | `m`, `metru`, `metri` | `MTR` |
| `zi`, `zile` | `DAY` | | `mp`, `m2` | `MTK` |
| `luna`, `luni` | `MON` | | `l`, `litru`, `litri` | `LTR` |
| `elem`, `element` | `C62` | | `set`, `seturi` | `SET` |

Matching ignores case and whitespace. **Anything unrecognised falls back to `H87` (piece)** rather
than failing — an unknown unit should not block an invoice. If that matters to you, compare
`UblUnitCodes.FromRomanianUnit(unit)` against `UblUnitCodes.Default` for an input you know is not a
piece.

---

## Serialization options

```csharp
UblWriter.Write(document, new UblWriteOptions
{
    StrictAddressValidation = false,   // RO-B fallback (default)
    IncludeXmlDeclaration   = false,   // default
    Indent                  = true,
    CustomizationId         = "...",   // defaults to CIUS-RO 1.0.1
});
```

`IncludeXmlDeclaration` defaults to `false`. ANAF accepts the document either way — the body is
uploaded as UTF-8 `text/plain` — and this reproduces the bytes of the application this library was
extracted from.

Also available: `UblWriter.WriteTo(document, xmlWriter)` and `UblWriter.ToXDocument(document)` for
post-processing the tree.

---

## Next

- [Credit notes](credit-notes.md)
- [The API client](api-client.md)
- [Troubleshooting](troubleshooting.md) — what ANAF's rejection codes mean
