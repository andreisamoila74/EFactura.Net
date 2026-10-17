# EFactura.Net.Client

**ANAF e-Factura REST client** for Romania's RO e-Factura — upload, status, download, message
listing, validation, XML-to-PDF, OAuth2 helpers and company lookup. Includes
[`EFactura.Net.Ubl`](https://www.nuget.org/packages/EFactura.Net.Ubl).

> Unofficial. Not affiliated with or endorsed by ANAF.

```bash
dotnet add package EFactura.Net.Client
```

```csharp
services.AddAnafEFactura(o => o.Environment = AnafEnvironment.Test)
        .AddAccessTokenProvider<MyTokenProvider>()
        .AddValidationClient();

var upload = await client.UploadAsync(cif, xml, AnafUploadStandard.Ubl);

if (upload.IsSuccess)
{
    var status = await client.GetStatusAsync(upload.UploadIndex!);
}
```

- Every e-Factura endpoint, with paging hidden behind `IAsyncEnumerable`.
- **ANAF's business outcomes are data, not exceptions** — rejections and validation failures arrive
  on the result; only transport failures and 401/403 throw.
- The validation and company-lookup clients need **no token** — ANAF's validator, PDF converter and
  VAT registry are public.
- The SDK never stores a token; you implement `IAnafAccessTokenProvider` over your own storage.
- No resilience policy baked in — the builder hands back the `IHttpClientBuilder`.
- Defaults to the ANAF **test** environment.

**Documentation:** https://github.com/andreisamoila74/EFactura.Net
