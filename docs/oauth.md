# OAuth2

ANAF's e-Factura API authenticates with OAuth2 authorization-code flow. The awkward part is the first
step: it requires a **qualified digital certificate** in the user's browser, so it cannot be
automated. A human has to click through it.

The SDK performs the HTTP calls and nothing else. It does not mint or verify the `state` parameter,
render a callback page, or store a token — all of which depend on your session and storage model.

Snippets here are compiled in
[`samples/.../Snippets/OAuthFlow.cs`](../samples/EFactura.Net.Samples.Console/Snippets/OAuthFlow.cs).

---

## Before you start: register in SPV

1. Sign in to the **ANAF SPV** portal with a qualified digital certificate.
2. Register an OAuth application. You receive a **client id** and **client secret**.
3. Register your **redirect URI**.

Two things cause most of the pain here:

- **The redirect URI must match character for character** — scheme, host, port, path, trailing slash.
  A mismatch is rejected at the authorize step with an unhelpful message.
- **Test and production issue separate credentials.** They are not interchangeable, and mixing them
  surfaces much later as a plain 401 from an endpoint that looks unrelated.

---

## Configure

```csharp
services.AddAnafEFactura()
        .AddAccessTokenProvider<MyTokenProvider>()
        .AddOAuthClient(o =>
        {
            o.ClientId     = configuration["Anaf:ClientId"]!;
            o.ClientSecret = configuration["Anaf:ClientSecret"]!;
            o.RedirectUri  = new Uri(configuration["Anaf:RedirectUri"]!);
        });
```

Keep the secret out of source control — environment variables or a secret store. The defaults for
`AuthorizeEndpoint` and `TokenEndpoint` are ANAF's and rarely need changing.

---

## Step 1 — send the user to ANAF

```csharp
// Unguessable, and tied to this user's session.
var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
await SaveStateForSessionAsync(state);

var authorizationUri = oauth.BuildAuthorizationUri(state);

return Redirect(authorizationUri.ToString());
```

That produces:

```
https://logincert.anaf.ro/anaf-oauth2/v1/authorize
    ?response_type=code
    &client_id=...
    &redirect_uri=...
    &token_content_type=jwt
    &state=...
```

The user authenticates with their certificate and approves access. Because of the certificate
requirement, this step **cannot run headless** — no service account, no background job. Plan for a
human-in-the-loop authorization that is repeated whenever the refresh token finally expires.

---

## Step 2 — handle the callback

```csharp
public async Task<IActionResult> Callback(string? code, string? state, string? error)
{
    // ANAF can report failure in the query string of what otherwise looks like a successful
    // redirect, so check this before looking at the code.
    if (!string.IsNullOrEmpty(error))
    {
        return BadRequest($"ANAF refused authorization: {error}");
    }

    var expected = await LoadStateForSessionAsync();

    if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state ?? ""),
            Encoding.UTF8.GetBytes(expected)))
    {
        return BadRequest("The state parameter did not match.");
    }

    var token = await oauth.ExchangeCodeAsync(code!);

    await tokenStore.SaveAsync(
        token.AccessToken,
        token.RefreshToken!,
        DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn));

    return Ok();
}
```

The `state` check is the CSRF defence for the whole flow — compare it in constant time, and never
skip it because "the code wouldn't work anyway".

---

## Step 3 — refresh

```csharp
var refreshed = await oauth.RefreshTokenAsync(storedRefreshToken);

await tokenStore.SaveAsync(
    refreshed.AccessToken,
    // ANAF does not always issue a new refresh token. Null means keep the one you have.
    refreshed.RefreshToken ?? storedRefreshToken,
    DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn));
```

`ExpiresIn` is surfaced as the raw number of seconds rather than a computed instant, because the
clock that will be compared against later is yours, not the library's.

### The concurrency trap

Several requests can notice the same expired token at the same moment and all refresh it. ANAF may
invalidate the earlier refresh tokens when that happens, logging the user out for reasons that look
random.

Guard the refresh — a per-user semaphore, a distributed lock, or a single background refresher:

```csharp
private static readonly SemaphoreSlim RefreshGate = new(1, 1);

await RefreshGate.WaitAsync(cancellationToken);
try
{
    // Re-read first: another caller may already have refreshed while we waited.
    var current = await store.GetAsync(cancellationToken);
    if (current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
    {
        return current.AccessToken;
    }

    // ... refresh and persist ...
}
finally
{
    RefreshGate.Release();
}
```

---

## Storing tokens

Entirely yours, deliberately. Worth getting right:

- **Encrypt at rest.** An ANAF access token authorises filing tax documents on a company's behalf.
- **One token per user**, not per application — ANAF issues them against the certificate holder.
- **Keep the refresh token.** Losing it means another trip through the certificate flow.
- **Record the expiry** you computed from `ExpiresIn`, not just the token.

---

## Next

- [Getting started](getting-started.md) — the token provider contract
- [The API client](api-client.md)
- [Troubleshooting](troubleshooting.md)
