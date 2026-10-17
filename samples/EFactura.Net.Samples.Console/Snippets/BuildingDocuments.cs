using EFactura.Net.Ubl;

namespace EFactura.Net.Samples.Console.Snippets;

/// <summary>
/// Building documents. Every snippet quoted in <c>docs/invoices.md</c> and
/// <c>docs/credit-notes.md</c> comes from this file, so the documentation cannot drift from code
/// that compiles.
/// </summary>
public static class BuildingDocuments
{
    /// <summary>The smallest invoice ANAF will accept.</summary>
    public static UblInvoice MinimalInvoice() => new()
    {
        DocumentId = "FCT nr. 1",
        IssueDate = new DateOnly(2026, 1, 15),
        CurrencyCode = "RON",
        Seller = new UblParty
        {
            Name = "Furnizor Test SRL",
            VatIdentifier = "RO12345674",
            Address = new UblAddress
            {
                StreetName = "Str. Exemplu nr. 1",
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
                StreetName = "Bd. Clientului nr. 2",
                CityName = "Cluj-Napoca",
                CountrySubdivision = "Cluj",
            },
        },
        Lines =
        [
            new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m },
        ],
    };

    /// <summary>A typical invoice: due date, units, bank details, several lines.</summary>
    public static UblInvoice TypicalInvoice() => MinimalInvoice() with
    {
        DueDate = new DateOnly(2026, 2, 14),
        BuyerReference = "PO-2026-0042",
        PaymentMeans = new UblPaymentMeans
        {
            // IBAN only — CIUS-RO UBL-CR-429 forbids a bank name, and ANAF rejects documents
            // that include one.
            PayeeAccount = new UblBankAccount { Iban = "RO49 AAAA 1B31 0075 9384 0000" },
        },
        Lines =
        [
            new UblLine { Name = "Consultanță software", Quantity = 10m, Unit = "ore", UnitPrice = 250m, TaxRate = 21m },
            new UblLine { Name = "Licență anuală", Quantity = 1m, Unit = "buc", UnitPrice = 1200m, TaxRate = 21m },
        ],
    };

    /// <summary>
    /// Reverse charge. The category must be set explicitly — a 0% rate alone does not say which of
    /// several very different things is meant.
    /// </summary>
    public static UblInvoice ReverseChargeInvoice() => MinimalInvoice() with
    {
        DocumentId = "FCT nr. 2",
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
            },
        ],
    };

    /// <summary>
    /// A foreign-currency invoice. Supplying the exchange rate is what lets the writer emit the RON
    /// VAT total EN 16931 BR-CO-03 requires; without it, neither that nor the rate element appears.
    /// </summary>
    public static UblInvoice ForeignCurrencyInvoice() => MinimalInvoice() with
    {
        DocumentId = "FCT nr. 3",
        CurrencyCode = "EUR",
        TaxCurrencyCode = "RON",
        TaxExchangeRate = 4.9771m,
        Lines =
        [
            new UblLine { Name = "Licență anuală", Quantity = 1m, Unit = "buc", UnitPrice = 1200m, TaxRate = 21m },
        ],
    };

    /// <summary>
    /// A Bucharest address. Write the city however your data holds it — the writer normalises it to
    /// the <c>SECTORn</c> token ANAF requires.
    /// </summary>
    public static UblInvoice BucharestInvoice() => MinimalInvoice() with
    {
        DocumentId = "FCT nr. 4",
        Buyer = MinimalInvoice().Buyer with
        {
            Address = new UblAddress
            {
                StreetName = "Calea Victoriei nr. 10",
                CityName = "Sector 1 Mun. București",
                CountrySubdivision = "București",
            },
        },
    };

    /// <summary>A credit note reversing an earlier invoice. Amounts stay positive.</summary>
    public static UblCreditNote CreditNote() => new()
    {
        DocumentId = "STO nr. 1",
        IssueDate = new DateOnly(2026, 3, 1),
        CurrencyCode = "RON",
        Seller = MinimalInvoice().Seller,
        Buyer = MinimalInvoice().Buyer,

        // Required: a credit note that does not say what it reverses will not compile, let alone
        // be accepted.
        PrecedingInvoice = new UblDocumentReference
        {
            DocumentId = "FCT nr. 1",
            IssueDate = new DateOnly(2026, 1, 15),
        },

        Lines =
        [
            new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m },
        ],
    };

    /// <summary>Serializing, and reading back the totals that went into the XML.</summary>
    public static (string Xml, UblTotalsResult Totals) SerializeWithTotals(UblDocument document)
    {
        var xml = UblWriter.Write(document);

        // The same numbers the writer emitted — use these for a PDF or a ledger entry rather than
        // recomputing and risking a one-ban disagreement.
        var totals = UblTotals.Calculate(document);

        return (xml, totals);
    }

    /// <summary>Opting into strict address validation.</summary>
    public static string SerializeStrictly(UblDocument document) =>
        UblWriter.Write(document, new UblWriteOptions { StrictAddressValidation = true });
}
