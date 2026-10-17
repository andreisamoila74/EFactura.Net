# Third-party notices

EFactura.Net is distributed under the [MIT License](LICENSE). The components below are included in
or referenced by this repository under their own terms.

---

## Redistributed in this repository

### OASIS Universal Business Language (UBL) 2.1 schemas

**Location:** `tests/EFactura.Net.Ubl.Tests/Schemas/` (15 `.xsd` files)
**Copyright:** Copyright © OASIS Open 2013. All Rights Reserved.
**Source:** <https://docs.oasis-open.org/ubl/os-UBL-2.1/>
**Terms:** [OASIS IPR Policy](https://www.oasis-open.org/policies-guidelines/ipr/) — OASIS Standards
are freely redistributable provided the copyright notices they carry are preserved.

Used **at test time only**, to validate that generated documents are structurally conformant UBL 2.1.
The original file headers are intact and unmodified. **These schemas are not included in either
published NuGet package** — the test project is not packable.

The set comprises the two main document schemas (`UBL-Invoice-2.1.xsd`, `UBL-CreditNote-2.1.xsd`) and
the common modules they import, which transitively include:

- the W3C **XML Signature** schema (`UBL-xmldsig-core-schema-2.1.xsd`) — Copyright © W3C,
  redistributed by OASIS under the [W3C Software and Document
  Notice and License](https://www.w3.org/copyright/software-license/);
- the ETSI **XAdES** v1.3.2 and v1.4.1 schemas (`UBL-XAdESv132-2.1.xsd`, `UBL-XAdESv141-2.1.xsd`) —
  Copyright © ETSI, redistributed by OASIS.

OASIS modified these three files only to remove the external `PUBLIC` and `SYSTEM` identifiers from
their DTD declarations, as noted in the file headers themselves.

---

## Referenced as package dependencies

Neither published package vendors any third-party code. `EFactura.Net.Ubl` has **no** runtime
dependencies at all. `EFactura.Net.Client` depends on the following, all MIT-licensed and all
published by Microsoft as part of .NET:

| Package | License |
|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | MIT |
| `Microsoft.Extensions.Http` | MIT |
| `Microsoft.Extensions.Logging.Abstractions` | MIT |
| `Microsoft.Extensions.Options` | MIT |

### Build and test only — not shipped

| Package | License | Used for |
|---|---|---|
| `MinVer` | Apache-2.0 | version numbers from git tags |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | MIT | locking the public API surface |
| `xunit.v3` | Apache-2.0 | test framework |
| `RichardSzalay.MockHttp` | MIT | mocked HTTP transport in tests |
| `Microsoft.Extensions.TimeProvider.Testing` | MIT | deterministic clock in tests |
| `Microsoft.Testing.Extensions.CodeCoverage` | MIT | coverage collection |

---

## Not affiliated with ANAF

This project is **unofficial**. It is not affiliated with, endorsed by, or supported by ANAF
(Agenția Națională de Administrare Fiscală) or the Romanian Ministry of Finance. "e-Factura",
"ANAF" and related names are used only to describe what the library interoperates with.

Endpoint addresses, request and response formats, and the CIUS-RO specification referenced here are
published by ANAF at <https://mfinante.gov.ro/ro/web/efactura>.
