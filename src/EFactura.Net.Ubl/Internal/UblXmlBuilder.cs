using System.Globalization;
using System.Xml.Linq;

namespace EFactura.Net.Ubl.Internal;

/// <summary>
/// Builds the UBL element tree. Internal: exposing the <see cref="XElement"/> factories would freeze
/// the serializer's internals as public API.
/// </summary>
internal static class UblXmlBuilder
{
    internal static readonly XNamespace Invoice2 = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    internal static readonly XNamespace CreditNote2 = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    internal static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    internal static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    internal static XElement Build(UblDocument document, UblWriteOptions options)
    {
        var errors = new List<string>();
        var root = document switch
        {
            UblInvoice invoice => BuildInvoice(invoice, options, errors),
            UblCreditNote creditNote => BuildCreditNote(creditNote, options, errors),
            _ => throw new UblValidationException($"Unsupported document type '{document.GetType().Name}'."),
        };

        if (errors.Count > 0)
        {
            throw new UblValidationException(errors);
        }

        return root;
    }

    private static XElement BuildInvoice(UblInvoice invoice, UblWriteOptions options, List<string> errors)
    {
        var totals = UblTotals.Calculate(invoice);
        var currency = invoice.CurrencyCode;

        var content = new List<object?>
        {
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),

            new XElement(Cbc + "UBLVersionID", options.UblVersionId),
            new XElement(Cbc + "CustomizationID", options.CustomizationId),
            new XElement(Cbc + "ID", invoice.DocumentId),
            new XElement(Cbc + "IssueDate", FormatDate(invoice.IssueDate)),
        };

        if (invoice.DueDate is { } dueDate)
        {
            content.Add(new XElement(Cbc + "DueDate", FormatDate(dueDate)));
        }

        content.Add(new XElement(Cbc + "InvoiceTypeCode", invoice.InvoiceTypeCode));

        if (!string.IsNullOrWhiteSpace(invoice.Note))
        {
            content.Add(new XElement(Cbc + "Note", invoice.Note));
        }

        content.Add(new XElement(Cbc + "DocumentCurrencyCode", currency));

        if (UsesSeparateTaxCurrency(invoice))
        {
            content.Add(new XElement(Cbc + "TaxCurrencyCode", invoice.TaxCurrencyCode));
        }

        if (!string.IsNullOrWhiteSpace(invoice.BuyerReference))
        {
            content.Add(new XElement(Cbc + "BuyerReference", invoice.BuyerReference));
        }

        content.Add(SupplierParty(invoice.Seller, options, errors));
        content.Add(CustomerParty(invoice.Buyer, options, errors));
        content.Add(PaymentMeans(invoice.PaymentMeans));
        content.Add(TaxExchangeRate(invoice));
        content.Add(TaxTotal(totals, currency));
        content.Add(TaxCurrencyTaxTotal(invoice, totals));
        content.Add(LegalMonetaryTotal(totals, currency));
        content.AddRange(Lines(invoice.Lines, currency, isCreditNote: false));

        return new XElement(Invoice2 + "Invoice", content.Where(static c => c is not null));
    }

    private static XElement BuildCreditNote(UblCreditNote creditNote, UblWriteOptions options, List<string> errors)
    {
        var totals = UblTotals.Calculate(creditNote);
        var currency = creditNote.CurrencyCode;

        var content = new List<object?>
        {
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),

            new XElement(Cbc + "UBLVersionID", options.UblVersionId),
            new XElement(Cbc + "CustomizationID", options.CustomizationId),
            new XElement(Cbc + "ID", creditNote.DocumentId),
            new XElement(Cbc + "IssueDate", FormatDate(creditNote.IssueDate)),
            new XElement(Cbc + "CreditNoteTypeCode", creditNote.CreditNoteTypeCode),
        };

        if (!string.IsNullOrWhiteSpace(creditNote.Note))
        {
            content.Add(new XElement(Cbc + "Note", creditNote.Note));
        }

        content.Add(new XElement(Cbc + "DocumentCurrencyCode", currency));

        if (UsesSeparateTaxCurrency(creditNote))
        {
            content.Add(new XElement(Cbc + "TaxCurrencyCode", creditNote.TaxCurrencyCode));
        }

        if (!string.IsNullOrWhiteSpace(creditNote.BuyerReference))
        {
            content.Add(new XElement(Cbc + "BuyerReference", creditNote.BuyerReference));
        }

        content.Add(BillingReference(creditNote.PrecedingInvoice));
        content.Add(SupplierParty(creditNote.Seller, options, errors));
        content.Add(CustomerParty(creditNote.Buyer, options, errors));
        content.Add(PaymentMeans(creditNote.PaymentMeans));
        content.Add(TaxExchangeRate(creditNote));
        content.Add(TaxTotal(totals, currency));
        content.Add(TaxCurrencyTaxTotal(creditNote, totals));
        content.Add(LegalMonetaryTotal(totals, currency));
        content.AddRange(Lines(creditNote.Lines, currency, isCreditNote: true));

        return new XElement(CreditNote2 + "CreditNote", content.Where(static c => c is not null));
    }

    private static XElement BillingReference(UblDocumentReference reference)
    {
        var documentReference = new XElement(
            Cac + "InvoiceDocumentReference",
            new XElement(Cbc + "ID", reference.DocumentId));

        if (reference.IssueDate is { } issueDate)
        {
            documentReference.Add(new XElement(Cbc + "IssueDate", FormatDate(issueDate)));
        }

        return new XElement(Cac + "BillingReference", documentReference);
    }

    private static XElement SupplierParty(UblParty party, UblWriteOptions options, List<string> errors)
    {
        var content = new List<object?>
        {
            PostalAddress(party.Address, options, errors),
        };

        if (party.IsVatRegistered)
        {
            content.Add(PartyTaxScheme(party));
        }

        var legalEntity = new XElement(
            Cac + "PartyLegalEntity",
            new XElement(Cbc + "RegistrationName", party.Name));

        // The seller's trade-register number goes on PartyLegalEntity. For a party that is not
        // VAT-registered there is no PartyTaxScheme, so this element carries the only identifier.
        var legalId = party.IsVatRegistered
            ? party.LegalRegistrationId
            : party.LegalRegistrationId ?? CuiNormalizer.ToVatIdentifier(party.VatIdentifier);

        if (!string.IsNullOrWhiteSpace(legalId))
        {
            legalEntity.Add(new XElement(Cbc + "CompanyID", legalId));
        }

        content.Add(legalEntity);

        return new XElement(Cac + "AccountingSupplierParty", new XElement(Cac + "Party", content.Where(static c => c is not null)));
    }

    private static XElement CustomerParty(UblParty party, UblWriteOptions options, List<string> errors)
    {
        var content = new List<object?>();

        // The buyer's trade-register number goes on PartyIdentification, mirroring what ANAF expects
        // on the customer side.
        if (!string.IsNullOrWhiteSpace(party.LegalRegistrationId))
        {
            content.Add(new XElement(
                Cac + "PartyIdentification",
                new XElement(Cbc + "ID", party.LegalRegistrationId)));
        }

        content.Add(PostalAddress(party.Address, options, errors));

        if (party.IsVatRegistered)
        {
            content.Add(PartyTaxScheme(party));
        }

        content.Add(new XElement(
            Cac + "PartyLegalEntity",
            new XElement(Cbc + "RegistrationName", party.Name)));

        return new XElement(Cac + "AccountingCustomerParty", new XElement(Cac + "Party", content.Where(static c => c is not null)));
    }

    private static XElement PartyTaxScheme(UblParty party) => new(
        Cac + "PartyTaxScheme",
        new XElement(Cbc + "CompanyID", CuiNormalizer.ToVatIdentifier(party.VatIdentifier)),
        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")));

    private static XElement PostalAddress(UblAddress address, UblWriteOptions options, List<string> errors)
    {
        string countyCode;
        if (RomanianAddress.TryToCountyCode(address.CountrySubdivision, out var resolved))
        {
            countyCode = resolved;
        }
        else
        {
            countyCode = RomanianAddress.FallbackCountyCode;

            if (options.StrictAddressValidation)
            {
                errors.Add(string.IsNullOrWhiteSpace(address.CountrySubdivision)
                    ? "Address is missing a county (cbc:CountrySubentity)."
                    : $"County '{address.CountrySubdivision}' is not a recognised Romanian county.");
            }
        }

        return new XElement(
            Cac + "PostalAddress",
            new XElement(Cbc + "StreetName", address.StreetName ?? ""),
            new XElement(Cbc + "CityName", RomanianAddress.NormalizeCityName(address.CityName)),
            new XElement(Cbc + "CountrySubentity", countyCode),
            new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode", address.CountryCode)));
    }

    private static XElement? PaymentMeans(UblPaymentMeans? paymentMeans)
    {
        if (paymentMeans is null)
        {
            return null;
        }

        var element = new XElement(
            Cac + "PaymentMeans",
            new XElement(Cbc + "PaymentMeansCode", paymentMeans.PaymentMeansCode));

        if (paymentMeans.PayeeAccount is { } account)
        {
            var payee = new XElement(
                Cac + "PayeeFinancialAccount",
                new XElement(Cbc + "ID", NormalizeIban(account.Iban)));

            if (!string.IsNullOrWhiteSpace(account.BankName))
            {
                payee.Add(new XElement(
                    Cac + "FinancialInstitutionBranch",
                    new XElement(Cbc + "Name", account.BankName)));
            }

            element.Add(payee);
        }

        return element;
    }

    private static XElement? TaxExchangeRate(UblDocument document)
    {
        if (!UsesSeparateTaxCurrency(document) || document.TaxExchangeRate is not { } rate)
        {
            return null;
        }

        return new XElement(
            Cac + "TaxExchangeRate",
            new XElement(Cbc + "SourceCurrencyCode", document.CurrencyCode),
            new XElement(Cbc + "TargetCurrencyCode", document.TaxCurrencyCode),
            new XElement(Cbc + "CalculationRate", FormatRate(rate)));
    }

    private static XElement TaxTotal(UblTotalsResult totals, string currency)
    {
        var element = new XElement(
            Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", totals.TaxAmount, currency));

        foreach (var subtotal in totals.TaxSubtotals)
        {
            element.Add(new XElement(
                Cac + "TaxSubtotal",
                Amount(Cbc + "TaxableAmount", subtotal.TaxableAmount, currency),
                Amount(Cbc + "TaxAmount", subtotal.TaxAmount, currency),
                new XElement(
                    Cac + "TaxCategory",
                    new XElement(Cbc + "ID", TaxCategoryCode(subtotal.Category)),
                    new XElement(Cbc + "Percent", FormatAmount(subtotal.Percent)),
                    new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))));
        }

        return element;
    }

    /// <summary>
    /// EN 16931 BR-CO-03: when VAT is accounted in another currency, the VAT total must also be
    /// stated in that currency. Emitted only when an exchange rate is available to convert it.
    /// </summary>
    private static XElement? TaxCurrencyTaxTotal(UblDocument document, UblTotalsResult totals)
    {
        if (!UsesSeparateTaxCurrency(document) || document.TaxExchangeRate is not { } rate)
        {
            return null;
        }

        var converted = UblTotals.Round(totals.TaxAmount * rate);

        return new XElement(
            Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", converted, document.TaxCurrencyCode!));
    }

    private static XElement LegalMonetaryTotal(UblTotalsResult totals, string currency) => new(
        Cac + "LegalMonetaryTotal",
        Amount(Cbc + "LineExtensionAmount", totals.LineExtensionAmount, currency),
        Amount(Cbc + "TaxExclusiveAmount", totals.TaxExclusiveAmount, currency),
        Amount(Cbc + "TaxInclusiveAmount", totals.TaxInclusiveAmount, currency),
        Amount(Cbc + "PayableAmount", totals.PayableAmount, currency));

    private static IEnumerable<XElement> Lines(IReadOnlyList<UblLine> lines, string currency, bool isCreditNote)
    {
        var lineElementName = isCreditNote ? "CreditNoteLine" : "InvoiceLine";
        var quantityElementName = isCreditNote ? "CreditedQuantity" : "InvoicedQuantity";

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineTotal = UblTotals.Round(line.Quantity * line.UnitPrice);

            var item = new XElement(Cac + "Item", new XElement(Cbc + "Name", line.Name));

            if (!string.IsNullOrWhiteSpace(line.Description))
            {
                // cbc:Description precedes cbc:Name in the UBL sequence.
                item.AddFirst(new XElement(Cbc + "Description", line.Description));
            }

            item.Add(new XElement(
                Cac + "ClassifiedTaxCategory",
                new XElement(Cbc + "ID", TaxCategoryCode(line.TaxCategory)),
                new XElement(Cbc + "Percent", FormatAmount(line.TaxRate)),
                new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT"))));

            yield return new XElement(
                Cac + lineElementName,
                new XElement(Cbc + "ID", (index + 1).ToString(CultureInfo.InvariantCulture)),
                new XElement(
                    Cbc + quantityElementName,
                    new XAttribute("unitCode", UblUnitCodes.FromRomanianUnit(line.Unit)),
                    FormatQuantity(line.Quantity)),
                Amount(Cbc + "LineExtensionAmount", lineTotal, currency),
                item,
                new XElement(Cac + "Price", Amount(Cbc + "PriceAmount", line.UnitPrice, currency)));
        }
    }

    private static XElement Amount(XName name, decimal value, string currency) => new(
        name,
        new XAttribute("currencyID", currency),
        FormatAmount(value));

    private static bool UsesSeparateTaxCurrency(UblDocument document) =>
        !string.IsNullOrWhiteSpace(document.TaxCurrencyCode)
        && !string.Equals(document.TaxCurrencyCode, document.CurrencyCode, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeIban(string iban) =>
        iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatAmount(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) =>
        value.ToString("0.0000", CultureInfo.InvariantCulture);

    private static string FormatRate(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string TaxCategoryCode(UblTaxCategory category) => category switch
    {
        UblTaxCategory.StandardRate => "S",
        UblTaxCategory.ZeroRated => "Z",
        UblTaxCategory.ReverseCharge => "AE",
        UblTaxCategory.Exempt => "E",
        UblTaxCategory.NotSubject => "O",
        UblTaxCategory.IntraCommunity => "K",
        UblTaxCategory.Export => "G",
        _ => "S",
    };
}
