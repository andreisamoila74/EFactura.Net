using EFactura.Net.Ubl;

namespace EFactura.Net.Ubl.Tests;

/// <summary>
/// The scenario corpus. Every value here is synthetic — invented companies, the ISO 13616
/// documentation IBAN, and tax identifiers that belong to no real business. Nothing came from a
/// production or development database.
/// </summary>
public static class TestDocuments
{
    internal const string DocumentationIban = "RO49 AAAA 1B31 0075 9384 0000";

    internal static UblParty Seller { get; } = new()
    {
        Name = "Furnizor Test SRL",
        VatIdentifier = "12345674",
        LegalRegistrationId = "J40/1234/2020",
        Address = new UblAddress
        {
            StreetName = "Str. Exemplu nr. 1",
            CityName = "Mun. Târgu Jiu",
            CountrySubdivision = "Gorj",
        },
    };

    internal static UblParty Buyer { get; } = new()
    {
        Name = "Client Test SRL",
        VatIdentifier = "RO87654329",
        LegalRegistrationId = "J20/4321/2019",
        Address = new UblAddress
        {
            StreetName = "Bd. Clientului nr. 2",
            CityName = "Cluj-Napoca",
            CountrySubdivision = "Cluj",
        },
    };

    internal static UblParty BucharestSectorBuyer { get; } = Buyer with
    {
        Address = new UblAddress
        {
            StreetName = "Calea Victoriei nr. 10",
            CityName = "Sector 1 Mun. București",
            CountrySubdivision = "MUNICIPIUL BUCUREȘTI",
        },
    };

    internal static UblPaymentMeans BankTransfer { get; } = new()
    {
        PayeeAccount = new UblBankAccount { Iban = DocumentationIban },
    };

    /// <summary>The ordinary case: one VAT rate, RON, bank transfer.</summary>
    internal static UblInvoice DomesticRon() => new()
    {
        DocumentId = "FCT nr. 1001",
        IssueDate = new DateOnly(2026, 1, 15),
        DueDate = new DateOnly(2026, 2, 14),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        PaymentMeans = BankTransfer,
        Lines =
        [
            new UblLine { Name = "Consultanță software", Quantity = 10m, Unit = "ore", UnitPrice = 250m, TaxRate = 21m },
        ],
    };

    /// <summary>
    /// Only what EN 16931 strictly requires. Note that a due date is among those requirements:
    /// BR-CO-25 demands BT-9 or payment terms whenever the payable amount is positive.
    /// </summary>
    internal static UblInvoice Minimal() => new()
    {
        DocumentId = "FCT nr. 1002",
        IssueDate = new DateOnly(2026, 1, 15),
        DueDate = new DateOnly(2026, 2, 14),
        CurrencyCode = "RON",
        Seller = new UblParty
        {
            Name = "Furnizor Test SRL",
            VatIdentifier = "RO12345674",
            Address = new UblAddress
            {
                StreetName = "Str. Minimala nr. 1",
                CityName = "Cluj-Napoca",
                CountrySubdivision = "Cluj",
            },
        },
        Buyer = new UblParty
        {
            Name = "Client Test SRL",
            VatIdentifier = "RO87654329",
            Address = new UblAddress
            {
                StreetName = "Str. Minimala nr. 2",
                CityName = "Cluj-Napoca",
                CountrySubdivision = "Cluj",
            },
        },
        Lines = [new UblLine { Name = "Serviciu", Quantity = 1m, UnitPrice = 100m, TaxRate = 21m }],
    };

    /// <summary>Foreign currency with VAT accounted in RON — exercises BR-CO-03.</summary>
    internal static UblInvoice ForeignCurrencyWithExchangeRate() => new()
    {
        DocumentId = "FCT nr. 1003",
        IssueDate = new DateOnly(2026, 2, 1),
        DueDate = new DateOnly(2026, 3, 3),
        CurrencyCode = "EUR",
        TaxCurrencyCode = "RON",
        TaxExchangeRate = 4.9771m,
        Seller = Seller,
        Buyer = Buyer,
        PaymentMeans = BankTransfer,
        Lines = [new UblLine { Name = "Licență anuală", Quantity = 1m, Unit = "buc", UnitPrice = 1200m, TaxRate = 21m }],
    };

    /// <summary>Foreign currency with no rate supplied — the second TaxTotal must be omitted.</summary>
    internal static UblInvoice ForeignCurrencyWithoutExchangeRate() =>
        ForeignCurrencyWithExchangeRate() with { DocumentId = "FCT nr. 1004", TaxExchangeRate = null };

    /// <summary>Reverse charge: VAT accounted by the buyer.</summary>
    internal static UblInvoice ReverseCharge() => new()
    {
        DocumentId = "FCT nr. 1005",
        IssueDate = new DateOnly(2026, 2, 10),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        Note = "Taxare inversă conform art. 331 Cod Fiscal.",
        Lines =
        [
            new UblLine
            {
                Name = "Lucrări de construcții",
                Quantity = 1m,
                Unit = "buc",
                UnitPrice = 5000m,
                TaxRate = 0m,
                TaxCategory = UblTaxCategory.ReverseCharge,
                TaxExemptionReason = "Taxare inversă conform art. 331 Cod Fiscal",
                TaxExemptionReasonCode = "VATEX-EU-AE",
            },
        ],
    };

    /// <summary>Exempt supply.</summary>
    internal static UblInvoice Exempt() => new()
    {
        DocumentId = "FCT nr. 1006",
        IssueDate = new DateOnly(2026, 2, 11),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        Lines =
        [
            new UblLine
            {
                Name = "Servicii medicale",
                Quantity = 1m,
                Unit = "buc",
                UnitPrice = 800m,
                TaxRate = 0m,
                TaxCategory = UblTaxCategory.Exempt,
                TaxExemptionReason = "Scutit conform art. 292 alin. (1) lit. a) Cod Fiscal",
            },
        ],
    };

    /// <summary>
    /// Several categories and rates at once, including two distinct categories that are both 0% —
    /// they must produce separate subtotals rather than being merged by rate.
    /// </summary>
    internal static UblInvoice MixedTaxCategories() => new()
    {
        DocumentId = "FCT nr. 1007",
        IssueDate = new DateOnly(2026, 2, 12),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = BucharestSectorBuyer,
        PaymentMeans = BankTransfer,
        BuyerReference = "PO-2026-0042",
        Lines =
        [
            new UblLine { Name = "Consultanță", Quantity = 4m, Unit = "ore", UnitPrice = 300m, TaxRate = 21m },
            new UblLine { Name = "Cazare", Quantity = 2m, Unit = "zile", UnitPrice = 250m, TaxRate = 11m },
            new UblLine
            {
                Name = "Transport intracomunitar",
                Quantity = 1m,
                Unit = "buc",
                UnitPrice = 400m,
                TaxRate = 0m,
                TaxCategory = UblTaxCategory.ReverseCharge,
                TaxExemptionReason = "Taxare inversă conform art. 331 Cod Fiscal",
            },
            new UblLine
            {
                Name = "Servicii scutite",
                Quantity = 1m,
                Unit = "buc",
                UnitPrice = 150m,
                TaxRate = 0m,
                TaxCategory = UblTaxCategory.Exempt,
            },
        ],
    };

    /// <summary>Amounts that do not land on whole bani — the rounding rule is visible here.</summary>
    internal static UblInvoice RequiringRounding() => new()
    {
        DocumentId = "FCT nr. 1008",
        IssueDate = new DateOnly(2026, 2, 13),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        Lines =
        [
            new UblLine { Name = "Articol A", Quantity = 3m, Unit = "buc", UnitPrice = 33.335m, TaxRate = 21m },
            new UblLine { Name = "Articol B", Quantity = 7m, Unit = "buc", UnitPrice = 14.285m, TaxRate = 21m },
            new UblLine { Name = "Articol C", Quantity = 1.5m, Unit = "kg", UnitPrice = 9.995m, TaxRate = 21m },
        ],
    };

    /// <summary>Seller not registered for VAT — identity moves to PartyLegalEntity.</summary>
    internal static UblInvoice NonVatRegisteredSeller() => new()
    {
        DocumentId = "FCT nr. 1009",
        IssueDate = new DateOnly(2026, 2, 14),
        CurrencyCode = "RON",
        Seller = Seller with { IsVatRegistered = false, LegalRegistrationId = null },
        Buyer = Buyer,
        Lines =
        [
            new UblLine
            {
                Name = "Servicii",
                Quantity = 1m,
                Unit = "buc",
                UnitPrice = 500m,
                TaxRate = 0m,
                TaxCategory = UblTaxCategory.NotSubject,
            },
        ],
    };

    /// <summary>
    /// Payment details on a document that has no other optional fields. The IBAN is the only thing
    /// CIUS-RO allows here — UBL-CR-429 forbids a bank name.
    /// </summary>
    internal static UblInvoice WithPaymentMeans() => new()
    {
        DocumentId = "FCT nr. 1010",
        IssueDate = new DateOnly(2026, 2, 15),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        PaymentMeans = BankTransfer,
        Lines = [new UblLine { Name = "Abonament", Quantity = 1m, Unit = "luna", UnitPrice = 199m, TaxRate = 21m }],
    };

    /// <summary>Every line field populated, including the optional description.</summary>
    internal static UblInvoice WithLineDescriptions() => new()
    {
        DocumentId = "FCT nr. 1011",
        IssueDate = new DateOnly(2026, 2, 16),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        Lines =
        [
            new UblLine
            {
                Name = "Mentenanță",
                Description = "Mentenanță lunară, inclusiv actualizări de securitate.",
                Quantity = 1m,
                Unit = "luna",
                UnitPrice = 750m,
                TaxRate = 21m,
            },
        ],
    };

    /// <summary>A storno reversing <see cref="DomesticRon"/>.</summary>
    internal static UblCreditNote CreditNote() => new()
    {
        DocumentId = "STO nr. 7",
        IssueDate = new DateOnly(2026, 3, 1),
        CurrencyCode = "RON",
        Seller = Seller,
        Buyer = Buyer,
        PaymentMeans = BankTransfer,
        PrecedingInvoice = new UblDocumentReference
        {
            DocumentId = "FCT nr. 1001",
            IssueDate = new DateOnly(2026, 1, 15),
        },
        Lines =
        [
            new UblLine { Name = "Consultanță software", Quantity = 10m, Unit = "ore", UnitPrice = 250m, TaxRate = 21m },
        ],
    };

    /// <summary>A foreign-currency storno — credit note plus BR-CO-03 together.</summary>
    internal static UblCreditNote ForeignCurrencyCreditNote() => CreditNote() with
    {
        DocumentId = "STO nr. 8",
        CurrencyCode = "EUR",
        TaxCurrencyCode = "RON",
        TaxExchangeRate = 4.9771m,
    };

    /// <summary>Named scenarios, used to drive the snapshot and schema-validation suites.</summary>
    public static TheoryData<string> ScenarioNames =>
    [
        nameof(DomesticRon),
        nameof(Minimal),
        nameof(ForeignCurrencyWithExchangeRate),
        nameof(ForeignCurrencyWithoutExchangeRate),
        nameof(ReverseCharge),
        nameof(Exempt),
        nameof(MixedTaxCategories),
        nameof(RequiringRounding),
        nameof(NonVatRegisteredSeller),
        nameof(WithPaymentMeans),
        nameof(WithLineDescriptions),
        nameof(CreditNote),
        nameof(ForeignCurrencyCreditNote),
    ];

    internal static UblDocument ByName(string name) => name switch
    {
        nameof(DomesticRon) => DomesticRon(),
        nameof(Minimal) => Minimal(),
        nameof(ForeignCurrencyWithExchangeRate) => ForeignCurrencyWithExchangeRate(),
        nameof(ForeignCurrencyWithoutExchangeRate) => ForeignCurrencyWithoutExchangeRate(),
        nameof(ReverseCharge) => ReverseCharge(),
        nameof(Exempt) => Exempt(),
        nameof(MixedTaxCategories) => MixedTaxCategories(),
        nameof(RequiringRounding) => RequiringRounding(),
        nameof(NonVatRegisteredSeller) => NonVatRegisteredSeller(),
        nameof(WithPaymentMeans) => WithPaymentMeans(),
        nameof(WithLineDescriptions) => WithLineDescriptions(),
        nameof(CreditNote) => CreditNote(),
        nameof(ForeignCurrencyCreditNote) => ForeignCurrencyCreditNote(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown scenario."),
    };
}
