# Troubleshooting

Real rejection codes and what to do about them. Most were collected by running this library's own
documents through ANAF's validator and reading what came back.

> **Quickest diagnosis available:** `ValidateAsync` is unauthenticated and returns the same verdict
> the upload path will. One round trip beats guessing.
> ```csharp
> var check = await validation.ValidateAsync(xml, AnafValidationStandard.Invoice);
> foreach (var error in check.Errors) Console.WriteLine(error.Message);
> ```

---

## Validation rejections

### `UBL-CR-429` — "should not include the PaymentMeans PayeeFinancialAccount FinancialInstitutionBranch Name"

CIUS-RO **forbids** the bank name on an invoice. Only the IBAN is allowed.

This library cannot produce it — `UblBankAccount` has no bank-name property, for exactly this reason.
If you see this, the XML came from somewhere else.

### `BR-AE-10`, `BR-E-10`, `BR-O-10`, `BR-IC-10`, `BR-G-10` — "shall have a VAT exemption reason code or text"

A VAT breakdown that carries no VAT must say why.

```csharp
new UblLine
{
    TaxCategory = UblTaxCategory.ReverseCharge,
    TaxExemptionReason = "Taxare inversă conform art. 331 Cod Fiscal",
    // ...
}
```

The library emits standard fallback wording when you supply none, so this should not reach you from
documents it generated. Supply the real legal basis anyway — the generic text is accepted but
useless to the buyer's accountant.

### `BR-CO-25` — "either the Payment due date (BT-9) or the Payment terms (BT-20) shall be present"

A positive payable amount needs a due date. Set `DueDate`. (Payment terms are not yet supported —
see the roadmap.)

### `BR-RO-080` / `BR-RO-081` / `BR-RO-082` — "Adresa … Linia 1 (BT-35) trebuie furnizata"

Seller or buyer address is missing its street line. `StreetName` must have actual content — an empty
string is not enough, and the library omits empty elements rather than emitting hollow ones.

Turn on strict mode to catch this locally:

```csharp
UblWriter.Write(invoice, new UblWriteOptions { StrictAddressValidation = true });
```

### `BR-CO-10` / `BR-CO-14` — totals do not sum

Line amounts must sum to `LineExtensionAmount`; VAT subtotals must sum to the VAT total — **as
stated**, to the ban.

The library handles this by rounding as it accumulates rather than at print time. If you see it,
something downstream is recomputing totals independently. Use `UblTotals.Calculate(document)` so the
same figures reach your PDF and your XML.

### `BR-CO-03` — VAT must also be stated in the tax currency

A foreign-currency invoice must restate its VAT in RON. Supply the exchange rate:

```csharp
CurrencyCode    = "EUR",
TaxCurrencyCode = "RON",
TaxExchangeRate = 4.9771m,   // without this, neither the rate nor the RON total is emitted
```

### `ERRIdentif` — "CUI cumparator incorect" / "CUI vanzator incorect"

ANAF validates the Romanian CUI **check digit**, and an invented number usually fails it. `12345678`
is invalid; `12345674` is the valid form of the same body.

The check: weights `753217532` right-aligned against the body, sum the products, ×10, mod 11, and 10
becomes 0.

Also check the counterparty is actually registered — `GetByCuiAsync` tells you.

---

## Upload and status

### Upload rejected with an unreadable error

If the message looks like a raw XML dump rather than a sentence, the XML came from a parser that
reads only element text. ANAF puts error messages in the **`errorMessage` attribute**; this library
reads the attribute first and falls back to element text, so you get the real reason.

### `stare` stays `in prelucrare`

Normal. ANAF processes asynchronously; see [polling](polling.md). Exhausting your attempt budget is
not a rejection — the upload index stays valid, so persist it and check later.

**Do not re-upload.** The first submission may still be processing and you will file twice.

### `IsTechnicalError` is true

A fault on ANAF's side — codes 4001, 4002, 5001. Says nothing about your document; the same bytes
will often succeed shortly after. Keep polling rather than failing the submission.

### `XML cu erori nepreluat de sistem`

The document was malformed enough that ANAF never ingested it. Usually a schema-level problem rather
than a business rule — run `ValidateAsync` for a specific reason.

### A credit note is rejected outright

Upload it with `AnafUploadStandard.CreditNote` (`standard=CN`), not `Ubl`. See
[credit notes](credit-notes.md).

---

## Authentication

### `AnafAuthenticationException` (401 / 403)

In order of likelihood:

1. **Token expired.** Refresh it.
2. **Wrong environment.** Test and production credentials are not interchangeable, and the resulting
   401 looks identical to an expiry.
3. **The CIF is not one your token is authorised for.** A token authorises specific companies.
4. **Refresh token expired**, which means another trip through the certificate flow.

### The user is randomly logged out

Concurrent refreshes. Several requests notice the same expired token and all refresh it; ANAF may
invalidate the earlier refresh tokens. Guard the refresh with a per-user lock — see
[OAuth](oauth.md#the-concurrency-trap).

### Authorization fails before any code is returned

Check for an `error` query parameter on the callback. ANAF reports failure in the query string of
what otherwise looks like a successful redirect, so code that checks `code` first sees nothing wrong.

Also verify the redirect URI matches the SPV registration character for character — including the
trailing slash.

---

## Addresses

### The county is wrong on a filed document

An unrecognised county name **silently becomes `RO-B` (Bucharest)**. That is the historical default,
so a typo ships a Bucharest address rather than failing.

```csharp
if (!RomanianAddress.TryToCountyCode(county, out var code))
{
    // Unrecognised — `code` is the RO-B fallback.
}
```

Or enable `StrictAddressValidation` to make it an error.

### A Bucharest address is rejected

ANAF wants `SECTOR1`…`SECTOR6` as the city for Bucharest. The library normalises common spellings
(`"Sector 1 Mun. București"`, `"sector 3"`, `"Sector5"`) — but only when the string contains the word
"Sector". A city of `"București"` with no sector will not produce one.

---

## Units

### `unitCode` is `H87` when it should not be

Unrecognised units fall back to `H87` (piece) rather than failing, so a misspelling passes silently.

```csharp
// Detect it where it matters:
if (UblUnitCodes.FromRomanianUnit(unit) == UblUnitCodes.Default && unit is not ("buc" or "bucata" or "bucati"))
{
    // Not actually a piece — the unit was not recognised.
}
```

The supported spellings are listed in [invoices.md](invoices.md#units).

---

## Still stuck?

1. Run `ValidateAsync` and read every error, not just the first.
2. Look at `RawResponse` on the result — it is always populated.
3. Enable response-body logging temporarily:
   ```csharp
   services.AddAnafEFactura(o => o.LogResponseBodies = true);   // Debug level
   ```
   Off by default because ANAF responses carry counterparty identifiers and document contents.
4. Check [ANAF's specification](https://mfinante.gov.ro/ro/web/efactura) for the rule code.
5. If the library produced something ANAF rejects and the fix is not here,
   [open an issue](https://github.com/andreisamoila74/EFactura.Net/issues) with the rule code and a
   minimal document — **with the real identifiers replaced**.
