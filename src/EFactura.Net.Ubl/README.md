# EFactura.Net.Ubl

CIUS-RO / EN 16931 **UBL 2.1 model and XML writer** for Romania's RO e-Factura.
No third-party dependencies.

> Unofficial. Not affiliated with or endorsed by ANAF.

```bash
dotnet add package EFactura.Net.Ubl
```

```csharp
using EFactura.Net.Ubl;

var invoice = new UblInvoice
{
    DocumentId = "FCT nr. 1",
    IssueDate  = new DateOnly(2026, 1, 15),
    DueDate    = new DateOnly(2026, 2, 14),
    CurrencyCode = "RON",
    Seller = seller,
    Buyer  = buyer,
    Lines  = [new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m }],
};

var xml = UblWriter.Write(invoice);
```

- Invoices (`380`) and credit notes (`381`), with the reference to the original enforced at compile time.
- All seven VAT categories, including reverse charge and the exemption reasons ANAF requires.
- Foreign currency with the RON VAT restatement EN 16931 demands.
- Romanian unit codes, county codes and Bucharest sector normalisation.
- Public `UblTotals`, so your PDF and your XML cannot disagree by a ban.

Every example is checked against ANAF's own validator by the test suite.

For the ANAF REST API, add [`EFactura.Net.Client`](https://www.nuget.org/packages/EFactura.Net.Client).

**Documentation:** https://github.com/andreisamoila74/EFactura.Net
