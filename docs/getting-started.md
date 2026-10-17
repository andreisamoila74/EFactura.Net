# Getting started

## Install

```bash
dotnet add package EFactura.Net.Client
```

`EFactura.Net.Client` pulls in `EFactura.Net.Ubl`. If you only need to produce XML and will post it
yourself, take the UBL package alone — it has no dependencies at all:

```bash
dotnet add package EFactura.Net.Ubl
```

Targets `net8.0` and `net10.0`.

---

## Environments

ANAF runs two environments, and **they do not share credentials**. A token minted for test is
rejected by production and vice versa — a confusing failure, because it surfaces as a plain 401.

| | Base address |
|---|---|
| `AnafEnvironment.Test` *(default)* | `https://api.anaf.ro/test/FCTEL/rest/` |
| `AnafEnvironment.Production` | `https://api.anaf.ro/prod/FCTEL/rest/` |

Test is the default deliberately. Documents sent to production are real filings.

```csharp
services.AddAnafEFactura(o => o.Environment = AnafEnvironment.Production);
```

---

## Setup with dependency injection

```csharp
services.AddAnafEFactura(options =>
    {
        options.Environment = AnafEnvironment.Test;
    })
    .AddAccessTokenProvider<MyTokenProvider>()
    .AddValidationClient()     // optional — ANAF's validator and PDF renderer
    .AddCompanyLookup();       // optional — the VAT-payer registry
```

That registers:

| Service | Needs a token |
|---|:--:|
| `IAnafEFacturaClient` | yes |
| `IAnafValidationClient` | no |
| `IAnafCompanyLookupClient` | no |
| `AnafOAuthClient` *(via `AddOAuthClient`)* | n/a |

The optional clients are opt-in so that a consumer who only generates and uploads does not end up
with three typed clients they never call.

### Adding resilience

The SDK configures no retry policy. How aggressively to retry a tax authority is a decision about
your system, not something a library should decide for you — so the builder hands back the
`IHttpClientBuilder`:

```csharp
var builder = services.AddAnafEFactura().AddAccessTokenProvider<MyTokenProvider>();

builder.EFacturaHttpClientBuilder.AddStandardResilienceHandler();   // Microsoft.Extensions.Http.Resilience
```

---

## Setup without dependency injection

```csharp
var httpClient = new HttpClient { BaseAddress = AnafEFacturaOptions.TestBaseAddress };

var client = new AnafEFacturaClient(
    httpClient,
    new AnafEFacturaOptions { Environment = AnafEnvironment.Test });
```

You are then responsible for attaching the bearer token yourself — the `DelegatingHandler` that
normally does it is wired by `AddAnafEFactura`. The simplest approach is to set the header on the
`HttpClient` you construct per call.

The unauthenticated clients need nothing but an `HttpClient`:

```csharp
var validation = new AnafValidationClient(new HttpClient());
var lookup     = new AnafCompanyLookupClient(new HttpClient());
```

---

## Supplying the access token

The SDK never stores, caches or refreshes a token. Tokens are per-user, long-lived and sensitive,
and where they live is your decision. You implement one method:

```csharp
public interface IAnafAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
```

The token is read **once per request**, so returning a freshly-refreshed value takes effect
immediately — no need to rebuild the client.

A realistic implementation refreshes on the way past:

```csharp
public sealed class StoredTokenProvider(ITokenStore store, AnafOAuthClient oauth) : IAnafAccessTokenProvider
{
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var stored = await store.GetAsync(cancellationToken);

        if (stored.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return stored.AccessToken;
        }

        var refreshed = await oauth.RefreshTokenAsync(stored.RefreshToken, cancellationToken);

        await store.SaveAsync(
            refreshed.AccessToken,
            // ANAF does not always issue a new refresh token; null means keep the old one.
            refreshed.RefreshToken ?? stored.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn),
            cancellationToken);

        return refreshed.AccessToken;
    }
}
```

> **Implementations must be thread-safe.** One client can issue concurrent requests, and without a
> guard several of them will notice the same expired token and refresh it simultaneously. ANAF may
> invalidate the earlier refresh tokens when that happens.

Both of these are compiled in
[`samples/.../Snippets/Configuration.cs`](../samples/EFactura.Net.Samples.Console/Snippets/Configuration.cs).

Getting the first token is the [OAuth2 flow](oauth.md).

---

## A first document

```csharp
var invoice = new UblInvoice
{
    DocumentId = "FCT nr. 1",
    IssueDate = new DateOnly(2026, 1, 15),
    DueDate = new DateOnly(2026, 2, 14),
    CurrencyCode = "RON",
    Seller = seller,
    Buyer = buyer,
    Lines = [new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m }],
};

var xml = UblWriter.Write(invoice);
```

`UblWriter.Write` is pure, deterministic and does no I/O — the same document always produces the same
bytes, which makes it safe to hash, cache or snapshot-test.

See [Building invoices](invoices.md) for what each field means and which ones ANAF insists on.

---

## Next

- [Building invoices](invoices.md)
- [The API client](api-client.md)
- [Polling for status](polling.md)
- [Troubleshooting](troubleshooting.md)
