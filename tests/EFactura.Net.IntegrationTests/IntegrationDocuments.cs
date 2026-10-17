using EFactura.Net.Ubl;

namespace EFactura.Net.IntegrationTests;

/// <summary>
/// Documents sent to the real ANAF validator. Synthetic throughout — the same invented companies the
/// unit suite uses.
/// </summary>
/// <remarks>
/// Deliberately a separate copy from the unit-test corpus. These travel to a third party, so what
/// they contain is a decision that should be visible here rather than inherited from a shared file
/// that someone later edits for an unrelated reason.
/// </remarks>
internal static class IntegrationDocuments
{
    private static UblParty Seller => new()
    {
        Name = "Furnizor Test SRL",
        VatIdentifier = "RO12345678",
        LegalRegistrationId = "J40/1234/2020",
        Address = new UblAddress
        {
            StreetName = "Str. Exemplu nr. 1",
            CityName = "Cluj-Napoca",
            CountrySubdivision = "Cluj",
        },
    };

    private static UblParty Buyer => new()
    {
        Name = "Client Test SRL",
        VatIdentifier = "RO87654321",
        LegalRegistrationId = "J20/4321/2019",
        Address = new UblAddress
        {
            StreetName = "Bd. Clientului nr. 2",
            CityName = "Sector 1 Mun. București",
            CountrySubdivision = "București",
        },
    };

    internal static UblInvoice Invoice(string scenario)
    {
        var baseInvoice = new UblInvoice
        {
            DocumentId = "IT nr. 1",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)),
            CurrencyCode = "RON",
            Seller = Seller,
            Buyer = Buyer,
            Lines = [StandardLine],
        };

        return scenario switch
        {
            "DomesticRon" => baseInvoice with { PaymentMeans = BankTransfer },

            "Minimal" => baseInvoice with { DueDate = null },

            "ReverseCharge" => baseInvoice with
            {
                Note = "Taxare inversă conform art. 331 Cod Fiscal.",
                Lines = [ZeroRatedLine(UblTaxCategory.ReverseCharge, "Lucrări de construcții")],
            },

            "Exempt" => baseInvoice with
            {
                Lines = [ZeroRatedLine(UblTaxCategory.Exempt, "Servicii medicale")],
            },

            "MixedTaxCategories" => baseInvoice with
            {
                PaymentMeans = BankTransfer,
                BuyerReference = "PO-2026-0042",
                Lines =
                [
                    StandardLine,
                    new UblLine { Name = "Cazare", Quantity = 2m, Unit = "zile", UnitPrice = 250m, TaxRate = 11m },
                    ZeroRatedLine(UblTaxCategory.ReverseCharge, "Transport intracomunitar"),
                ],
            },

            "WithBankName" => baseInvoice with
            {
                PaymentMeans = new UblPaymentMeans
                {
                    PayeeAccount = new UblBankAccount
                    {
                        Iban = "RO49AAAA1B31007593840000",
                        BankName = "Banca Test",
                    },
                },
            },

            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario."),
        };
    }

    internal static UblCreditNote CreditNote() => new()
    {
        DocumentId = "ITSTO nr. 1",
        IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        PaymentMeans = BankTransfer,
        PrecedingInvoice = new UblDocumentReference
        {
            DocumentId = "IT nr. 1",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-10)),
        },
        Lines = [StandardLine],
    };

    private static UblLine StandardLine => new()
    {
        Name = "Consultanță software",
        Quantity = 10m,
        Unit = "ore",
        UnitPrice = 250m,
        TaxRate = 21m,
    };

    private static UblLine ZeroRatedLine(UblTaxCategory category, string name) => new()
    {
        Name = name,
        Quantity = 1m,
        Unit = "buc",
        UnitPrice = 1000m,
        TaxRate = 0m,
        TaxCategory = category,
    };

    private static UblPaymentMeans BankTransfer => new()
    {
        PayeeAccount = new UblBankAccount { Iban = "RO49AAAA1B31007593840000" },
    };
}
