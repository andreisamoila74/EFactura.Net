# The API client

Three clients, split by what they need rather than by topic:

| Client | Endpoints | Token |
|---|---|:--:|
| `IAnafEFacturaClient` | upload, status, download, message lists | **yes** |
| `IAnafValidationClient` | validate, convert to PDF | no |
| `IAnafCompanyLookupClient` | VAT-payer registry | no |

The split is useful: you can validate a document or look up a counterparty without ever obtaining an
OAuth token, which also makes those paths testable in CI without secrets.

Snippets below are compiled in
[`samples/.../Snippets/UsingTheApi.cs`](../samples/EFactura.Net.Samples.Console/Snippets/UsingTheApi.cs).

---

## The error model

This is the part worth reading before anything else.

**ANAF's business outcomes are data.** A rejected upload, a failed validation, an empty message list
— these are answers, not failures, and they arrive on the result record:

```csharp
var upload = await client.UploadAsync(cif, xml, AnafUploadStandard.Ubl);

if (!upload.IsSuccess)
{
    foreach (var error in upload.Errors)
    {
        Console.WriteLine(error.Message);
    }
}
```

**Only transport and authentication failures throw:**

| Exception | When |
|---|---|
| `AnafAuthenticationException` | HTTP 401 or 403 — expired token, or one minted for the other environment |
| `AnafHttpException` | any other non-success status; carries `StatusCode` and `RawResponse` |
| `AnafException` | a response that could not be parsed at all |

```csharp
try
{
    var status = await client.GetStatusAsync(uploadIndex);
}
catch (AnafAuthenticationException)
{
    // Re-authorise.
}
catch (AnafHttpException ex)
{
    // ANAF is down or rate limiting; retry later. ex.RawResponse has the body.
}
```

Every result also carries `RawResponse` — ANAF's error reporting is inconsistent enough that the raw
text is sometimes the only way to work out what happened.

---

## Upload

```csharp
Task<AnafUploadResult> UploadAsync(
    string cif, string xml, AnafUploadStandard standard,
    AnafUploadOptions? options = null, CancellationToken cancellationToken = default);
```

The `RO` prefix and any spaces are stripped from `cif` automatically.

| `AnafUploadStandard` | Sent as | Use for |
|---|---|---|
| `Ubl` | `UBL` | `UblInvoice` |
| `CreditNote` | `CN` | `UblCreditNote` |
| `Cii` | `CII` | UN/CEFACT Cross Industry Invoice |
| `Rasp` | `RASP` | buyer response messages |

Optional flags:

```csharp
new AnafUploadOptions
{
    ForeignBuyer = true,   // extern=DA      — relaxes Romanian buyer identification rules
    SelfBilled   = true,   // autofactura=DA — issued by the buyer on the supplier's behalf
    Enforcement  = true,   // executare=DA   — issued under an enforcement procedure
}
```

> A successful upload means **queued**, not accepted. The verdict comes from `GetStatusAsync`.

---

## Status

```csharp
Task<AnafMessageStatus> GetStatusAsync(string uploadIndex, CancellationToken cancellationToken = default);
```

| `AnafMessageState` | ANAF's `stare` | Final |
|---|---|:--:|
| `Processing` | `in prelucrare` | no |
| `Ok` | `ok` | yes |
| `Rejected` | `nok` | yes |
| `InvalidXml` | `XML cu erori nepreluat de sistem` | yes |
| `Unknown` | anything else | no |

`IsFinal` saves you writing that three-way check. `RawState` keeps ANAF's exact string, so an
unrecognised value is never lost.

### Technical errors

`IsTechnicalError` is set when ANAF reports a fault on *their* side — codes 4001, 4002, 5001, or a
message mentioning `eroare tehnica`. The state stays `Processing`, because nothing has been concluded
about your document:

```csharp
if (status.IsTechnicalError)
{
    // The same bytes may well succeed on the next attempt. Keep polling.
}
```

This is worth distinguishing from `Rejected`, where retrying the identical document cannot help.

See [polling](polling.md) for a backoff that works.

---

## Download

```csharp
Task<AnafDownloadResult> DownloadAsync(string downloadId, CancellationToken cancellationToken = default);
```

Takes the `DownloadId` from a status response, or the `Id` from a message listing. The ZIP is
unpacked for you:

```csharp
var result = await client.DownloadAsync(status.DownloadId!);

result.InvoiceXml;      // the document
result.SignatureXml;    // ANAF's detached signature — what makes it legally the filed copy
result.ZipContent;      // the original bytes, if you archive them
result.Errors;          // populated instead, for a rejected document
```

ANAF returns errors as XML and success as a ZIP, **both under HTTP 200** and not always with a
matching content type, so the client sniffs the body rather than trusting the header.

---

## Message lists

```csharp
// Up to 60 days back.
Task<AnafMessageList> ListMessagesAsync(
    string cif, int days, AnafMessageFilter? filter = null, CancellationToken cancellationToken = default);

// Any range; paging handled internally.
IAsyncEnumerable<AnafMessage> ListMessagesPagedAsync(
    string cif, DateTimeOffset startTime, DateTimeOffset endTime,
    AnafMessageFilter? filter = null, CancellationToken cancellationToken = default);
```

| `AnafMessageFilter` | Sent as | |
|---|---|---|
| `Errors` | `E` | validation errors |
| `Sent` | `T` | invoices you sent |
| `Received` | `P` | invoices sent to you |
| `BuyerMessages` | `R` | buyer responses |

Pass `null` for all types. It is a plain enum, not `[Flags]`, because ANAF accepts exactly one
letter.

> **An empty window is not a failure.** ANAF reports it as an `Error` string with no messages:
> ```csharp
> if (result.Messages.Count == 0) Console.WriteLine(result.Error);  // "Nu exista mesaje in ultimele 7 zile"
> ```

Paging is lazy — breaking out of the loop stops the requests:

```csharp
await foreach (var message in client.ListMessagesPagedAsync(cif, start, end))
{
    if (message.Type == AnafMessageType.ReceivedInvoice)
    {
        var invoice = await client.DownloadAsync(message.Id);
        break;   // no further pages are fetched
    }
}
```

`AnafMessage.CreatedAt` is parsed from ANAF's `yyyyMMddHHmm` format, which carries no time zone.

---

## Validation and PDF

Unauthenticated, so no token provider is needed.

```csharp
Task<AnafValidationResult> ValidateAsync(string xml, AnafValidationStandard standard, CancellationToken ct = default);
Task<AnafPdfResult> ConvertToPdfAsync(string xml, AnafValidationStandard standard, bool validate = true, CancellationToken ct = default);
```

`ValidateAsync` is the authoritative check — the same rules that decide acceptance after upload.
Running it first turns a rejection you would otherwise meet minutes later into an immediate answer,
for one round trip:

```csharp
var check = await validation.ValidateAsync(xml, AnafValidationStandard.Invoice);

if (!check.IsValid)
{
    foreach (var error in check.Errors) Console.WriteLine(error.Message);
    return;
}
```

`AnafValidationStandard.Invoice` is `FACT1`; `CreditNote` is `FCN`. Passing `validate: false` to the
PDF converter appends ANAF's `/DA` segment, rendering without validating first.

---

## Company lookup

```csharp
Task<AnafCompanyInfo?> GetByCuiAsync(string cui, DateOnly? asOf = null, CancellationToken ct = default);
Task<IReadOnlyList<AnafCompanyInfo>> GetManyByCuiAsync(IEnumerable<string> cuis, DateOnly? asOf = null, CancellationToken ct = default);
```

```csharp
var company = await lookup.GetByCuiAsync("RO12345674");

company?.Name;
company?.IsVatPayer;
company?.IsEFacturaActive;   // worth checking before sending
company?.IsInactive;         // invoices to an inactive company carry deductibility consequences
company?.RegisteredAddress;
```

`asOf` defaults to today. It matters: VAT standing and inactive status are date-dependent, so asking
about a historical invoice date can give a different answer than asking about today.

A CUI with no registry record returns `null` — ANAF signals that with HTTP 404 *and* a normal JSON
body, which the client translates into a no-result rather than an exception. Inputs longer than
`BatchSize` (500) are split across requests automatically.

---

## Next

- [Polling for status](polling.md)
- [OAuth2](oauth.md)
- [Troubleshooting](troubleshooting.md)
