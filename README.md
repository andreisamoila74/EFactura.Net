# EFactura.Net

[![CI](https://github.com/andreisamoila74/EFactura.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/andreisamoila74/EFactura.Net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EFactura.Net.Client.svg)](https://www.nuget.org/packages/EFactura.Net.Client)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

.NET SDK for Romania's **RO e-Factura** — build CIUS-RO UBL 2.1 invoices and credit notes, and talk to
the ANAF e-Factura REST API.

> **Unofficial.** Not affiliated with, endorsed by, or supported by ANAF or the Romanian Ministry of
> Finance. Verify any output against the official specification before relying on it.

| Package | Description |
|---|---|
| [`EFactura.Net.Ubl`](src/EFactura.Net.Ubl) | CIUS-RO / EN 16931 UBL 2.1 model + XML writer. No third-party dependencies. |
| [`EFactura.Net.Client`](src/EFactura.Net.Client) | ANAF REST client (upload, status, download, message lists, validation, XML→PDF), OAuth2 helpers, company lookup, DI extensions. |

```bash
dotnet add package EFactura.Net.Client
```

## Status

**Pre-release — under active extraction.** The public API is approved but not yet implemented;
see [CHANGELOG.md](CHANGELOG.md). Nothing is published to nuget.org yet.

Documentation, samples and the full feature list land before `0.1.0`.

## Requirements

- .NET 8 or .NET 10

## License

[MIT](LICENSE) © Andrei Samoila
