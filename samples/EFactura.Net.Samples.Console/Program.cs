// Temporary Phase 3 smoke check — replaced by the real walkthrough in Phase 5.
using EFactura.Net.Ubl;

var seller = new UblParty
{
    Name = "Furnizor Test SRL",
    VatIdentifier = "12345678",
    LegalRegistrationId = "J40/1234/2020",
    Address = new UblAddress
    {
        StreetName = "Str. Exemplu nr. 1",
        CityName = "Sector 1 Mun. București",
        CountrySubdivision = "București",
    },
};

var buyer = new UblParty
{
    Name = "Client Test SRL",
    VatIdentifier = "RO87654321",
    LegalRegistrationId = "J20/4321/2019",
    Address = new UblAddress
    {
        StreetName = "Bd. Clientului nr. 2",
        CityName = "Mun. Târgu Jiu",
        CountrySubdivision = "Gorj",
    },
};

var invoice = new UblInvoice
{
    DocumentId = "FCT nr. 123",
    IssueDate = new DateOnly(2026, 1, 15),
    DueDate = new DateOnly(2026, 2, 14),
    CurrencyCode = "RON",
    Seller = seller,
    Buyer = buyer,
    PaymentMeans = new UblPaymentMeans
    {
        PayeeAccount = new UblBankAccount { Iban = "RO49 AAAA 1B31 0075 9384 0000" },
    },
    Lines =
    [
        new UblLine { Name = "Consultanță software", Quantity = 10m, Unit = "ore", UnitPrice = 250m, TaxRate = 21m },
        new UblLine { Name = "Licență anuală", Quantity = 1m, Unit = "buc", UnitPrice = 1200m, TaxRate = 21m },
        new UblLine { Name = "Transport intracomunitar", Quantity = 1m, Unit = "buc", UnitPrice = 300m, TaxRate = 0m, TaxCategory = UblTaxCategory.ReverseCharge },
    ],
};

Console.WriteLine("================ INVOICE ================");
Console.WriteLine(UblWriter.Write(invoice));

var totals = UblTotals.Calculate(invoice);
Console.WriteLine();
Console.WriteLine($"lines={totals.LineExtensionAmount} vat={totals.TaxAmount} payable={totals.PayableAmount}");
Console.WriteLine($"subtotals sum to vat: {totals.TaxSubtotals.Sum(s => s.TaxAmount) == totals.TaxAmount}");

var creditNote = new UblCreditNote
{
    DocumentId = "STO nr. 7",
    IssueDate = new DateOnly(2026, 3, 1),
    CurrencyCode = "EUR",
    TaxCurrencyCode = "RON",
    TaxExchangeRate = 4.9771m,
    Seller = seller,
    Buyer = buyer,
    PrecedingInvoice = new UblDocumentReference
    {
        DocumentId = "FCT nr. 123",
        IssueDate = new DateOnly(2026, 1, 15),
    },
    Lines = [new UblLine { Name = "Stornare licență", Quantity = 1m, Unit = "buc", UnitPrice = 240.555m, TaxRate = 21m }],
};

Console.WriteLine();
Console.WriteLine("================ CREDIT NOTE (EUR, with exchange rate) ================");
Console.WriteLine(UblWriter.Write(creditNote));
