# Credit notes (storno)

A credit note reverses an invoice you already filed. ANAF accepts two approaches; this library
implements the one described below.

---

## The approach used here

A **separate UBL `CreditNote` document**, type code `381`, referencing the original through
`cac:BillingReference`, uploaded with `standard=CN`.

The alternative — an `Invoice` carrying negative quantities — is also accepted by ANAF but is not
supported here. A distinct document type keeps the reversal explicit, lets the type system enforce
that a credit note references something, and avoids the sign confusion that negative-quantity
invoices invite.

```csharp
var creditNote = new UblCreditNote
{
    DocumentId = "STO nr. 1",
    IssueDate  = new DateOnly(2026, 3, 1),
    CurrencyCode = "RON",
    Seller = seller,
    Buyer  = buyer,

    PrecedingInvoice = new UblDocumentReference
    {
        DocumentId = "FCT nr. 1",                 // exactly as filed
        IssueDate  = new DateOnly(2026, 1, 15),
    },

    Lines =
    [
        new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m },
    ],
};

var xml = UblWriter.Write(creditNote);

await client.UploadAsync(cif, xml, AnafUploadStandard.CreditNote);   // standard=CN, not UBL
```

Two things that catch people out:

- **Amounts stay positive.** The `CreditNote` document type itself carries the reversing sense.
  Negating the values would reverse the reversal.
- **Upload with `AnafUploadStandard.CreditNote`.** Sending a credit note as `standard=UBL` is
  rejected.

---

## Referencing the original

`PrecedingInvoice` is `required` — a credit note that does not say what it reverses will not compile,
let alone be accepted:

```csharp
PrecedingInvoice = new UblDocumentReference
{
    DocumentId = "FCT nr. 1",
    IssueDate  = new DateOnly(2026, 1, 15),   // optional, but include it
},
```

`DocumentId` must match the original's `DocumentId` **character for character**. If the original was
filed as `"FCT nr. 1"`, then `"FCT 1"` or `"FCT nr.1"` will not tie the two documents together on
ANAF's side — and nothing in the response will tell you that happened.

---

## Differences from an invoice

| | `UblInvoice` | `UblCreditNote` |
|---|---|---|
| Root element | `Invoice` | `CreditNote` |
| Namespace | `…:Invoice-2` | `…:CreditNote-2` |
| Type code | `InvoiceTypeCode` `380` | `CreditNoteTypeCode` `381` |
| Lines | `cac:InvoiceLine` / `InvoicedQuantity` | `cac:CreditNoteLine` / `CreditedQuantity` |
| `DueDate` | yes | **not present** |
| `PrecedingInvoice` | — | **required** |
| Upload standard | `Ubl` | `CreditNote` |

`UblCreditNote` has no `DueDate` property at all, because a credit note has nothing to pay. That also
means `BR-CO-25` — which requires a due date on a positive balance — does not apply.

Everything else is shared: parties, payment means, VAT categories, exemption reasons, foreign
currency, totals and rounding all behave exactly as on an [invoice](invoices.md).

---

## Partial credit notes

To reverse part of an invoice, include only the lines being credited, with the quantities being
credited:

```csharp
// Original: 10 hours at 250 RON. Crediting 3 of them.
Lines =
[
    new UblLine { Name = "Consultanță software", Quantity = 3m, Unit = "ore", UnitPrice = 250m, TaxRate = 21m },
],
```

Keep the unit price identical to the original. Crediting `1 × 750` instead of `3 × 250` reaches the
same total but makes the two documents hard to reconcile line by line.

---

## Foreign currency

Same rules as an invoice — if the original was in EUR, the credit note should be too, and needs its
own exchange rate:

```csharp
var creditNote = baseCreditNote with
{
    CurrencyCode = "EUR",
    TaxCurrencyCode = "RON",
    TaxExchangeRate = 4.9771m,
};
```

Use the rate that applies to the **credit note's** issue date, not the original invoice's.

---

## Next

- [Building invoices](invoices.md)
- [The API client](api-client.md)
