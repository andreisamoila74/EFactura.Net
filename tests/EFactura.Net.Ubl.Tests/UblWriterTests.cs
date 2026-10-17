using System.Xml.Linq;

namespace EFactura.Net.Ubl.Tests;

public class UblWriterSnapshotTests
{
    /// <summary>
    /// Locks the exact XML for every scenario. A diff here means the wire format moved — which may
    /// be correct, but is never accidental.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDocuments.ScenarioNames), MemberType = typeof(TestDocuments))]
    public void Writes_expected_xml(string scenario)
    {
        var xml = UblWriter.Write(TestDocuments.ByName(scenario));

        Snapshot.Matches(xml, scenario);
    }
}

public class UblSchemaValidationTests
{
    /// <summary>Every scenario must be structurally valid UBL 2.1.</summary>
    [Theory]
    [MemberData(nameof(TestDocuments.ScenarioNames), MemberType = typeof(TestDocuments))]
    public void Produces_schema_valid_xml(string scenario)
    {
        var xml = UblWriter.Write(TestDocuments.ByName(scenario));

        var errors = UblSchemaValidator.Validate(xml);

        Assert.Empty(errors);
    }
}

public class UblWriterStructureTests
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    [Fact]
    public void Invoice_uses_the_invoice_namespace_and_type_code()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        Assert.Equal("Invoice", root.Name.LocalName);
        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", root.Name.NamespaceName);
        Assert.Equal("380", root.Element(Cbc + "InvoiceTypeCode")?.Value);
    }

    [Fact]
    public void Credit_note_uses_the_credit_note_namespace_and_type_code()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.CreditNote())).Root!;

        Assert.Equal("CreditNote", root.Name.LocalName);
        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", root.Name.NamespaceName);
        Assert.Equal("381", root.Element(Cbc + "CreditNoteTypeCode")?.Value);
    }

    [Fact]
    public void Credit_note_lines_use_credited_quantity()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.CreditNote())).Root!;

        Assert.NotEmpty(root.Elements(Cac + "CreditNoteLine"));
        Assert.Empty(root.Elements(Cac + "InvoiceLine"));
        Assert.All(
            root.Elements(Cac + "CreditNoteLine"),
            line => Assert.NotNull(line.Element(Cbc + "CreditedQuantity")));
    }

    [Fact]
    public void Credit_note_references_the_invoice_it_reverses()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.CreditNote())).Root!;

        var reference = root.Element(Cac + "BillingReference")?.Element(Cac + "InvoiceDocumentReference");

        Assert.Equal("FCT nr. 1001", reference?.Element(Cbc + "ID")?.Value);
        Assert.Equal("2026-01-15", reference?.Element(Cbc + "IssueDate")?.Value);
    }

    [Fact]
    public void Credit_note_omits_due_date()
    {
        // DueDate is not a member of UblCreditNote at all; this guards the XML side of that choice.
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.CreditNote())).Root!;

        Assert.Null(root.Element(Cbc + "DueDate"));
    }

    [Fact]
    public void Customization_id_declares_cius_ro()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        Assert.Equal(
            "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:CIUS-RO:1.0.1",
            root.Element(Cbc + "CustomizationID")?.Value);
        Assert.Equal("2.1", root.Element(Cbc + "UBLVersionID")?.Value);
    }

    [Fact]
    public void Seller_vat_identifier_is_ro_prefixed_and_on_party_tax_scheme()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        var party = root.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!;

        // Supplied as "12345674" without the prefix.
        Assert.Equal("RO12345674", party.Element(Cac + "PartyTaxScheme")?.Element(Cbc + "CompanyID")?.Value);
        Assert.Equal("J40/1234/2020", party.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "CompanyID")?.Value);
    }

    [Fact]
    public void Buyer_registration_number_goes_on_party_identification()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        var party = root.Element(Cac + "AccountingCustomerParty")!.Element(Cac + "Party")!;

        // The asymmetry with the seller is deliberate and matches what ANAF expects.
        Assert.Equal("J20/4321/2019", party.Element(Cac + "PartyIdentification")?.Element(Cbc + "ID")?.Value);
        Assert.Null(party.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "CompanyID"));
    }

    [Fact]
    public void Non_vat_registered_seller_omits_party_tax_scheme()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.NonVatRegisteredSeller())).Root!;

        var party = root.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!;

        Assert.Null(party.Element(Cac + "PartyTaxScheme"));
        Assert.Equal("RO12345674", party.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "CompanyID")?.Value);
    }

    [Fact]
    public void Bucharest_sector_is_normalized_for_anaf()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.MixedTaxCategories())).Root!;

        var address = root.Element(Cac + "AccountingCustomerParty")!
            .Element(Cac + "Party")!
            .Element(Cac + "PostalAddress")!;

        Assert.Equal("SECTOR1", address.Element(Cbc + "CityName")?.Value);
        Assert.Equal("RO-B", address.Element(Cbc + "CountrySubentity")?.Value);
    }

    [Fact]
    public void Iban_whitespace_is_stripped()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        var id = root.Element(Cac + "PaymentMeans")!
            .Element(Cac + "PayeeFinancialAccount")!
            .Element(Cbc + "ID")!.Value;

        Assert.Equal("RO49AAAA1B31007593840000", id);
    }

    /// <summary>
    /// CIUS-RO UBL-CR-429 forbids a bank name on an invoice; ANAF's validator rejects any document
    /// carrying one. The account element must contain the IBAN and nothing else.
    /// </summary>
    [Fact]
    public void Payee_account_carries_only_the_iban()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.WithPaymentMeans())).Root!;

        var account = root.Element(Cac + "PaymentMeans")!.Element(Cac + "PayeeFinancialAccount")!;

        Assert.Null(account.Element(Cac + "FinancialInstitutionBranch"));
        Assert.Equal(["ID"], account.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void Payment_means_is_omitted_entirely_when_not_supplied()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.ReverseCharge())).Root!;

        Assert.Null(root.Element(Cac + "PaymentMeans"));
    }

    [Fact]
    public void Foreign_currency_with_rate_emits_exchange_rate_and_second_tax_total()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.ForeignCurrencyWithExchangeRate())).Root!;

        var exchangeRate = root.Element(Cac + "TaxExchangeRate");
        Assert.Equal("EUR", exchangeRate?.Element(Cbc + "SourceCurrencyCode")?.Value);
        Assert.Equal("RON", exchangeRate?.Element(Cbc + "TargetCurrencyCode")?.Value);
        Assert.Equal("4.9771", exchangeRate?.Element(Cbc + "CalculationRate")?.Value);

        var taxTotals = root.Elements(Cac + "TaxTotal").ToList();
        Assert.Equal(2, taxTotals.Count);

        // 1200.00 * 21% = 252.00 EUR; 252.00 * 4.9771 = 1254.23 RON.
        Assert.Equal("EUR", taxTotals[0].Element(Cbc + "TaxAmount")?.Attribute("currencyID")?.Value);
        Assert.Equal("252.00", taxTotals[0].Element(Cbc + "TaxAmount")?.Value);
        Assert.Equal("RON", taxTotals[1].Element(Cbc + "TaxAmount")?.Attribute("currencyID")?.Value);
        Assert.Equal("1254.23", taxTotals[1].Element(Cbc + "TaxAmount")?.Value);
    }

    [Fact]
    public void Foreign_currency_without_rate_omits_exchange_rate_and_second_tax_total()
    {
        // Preserves the original behaviour: TaxCurrencyCode alone, nothing derived from a rate we
        // were never given.
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.ForeignCurrencyWithoutExchangeRate())).Root!;

        Assert.Equal("RON", root.Element(Cbc + "TaxCurrencyCode")?.Value);
        Assert.Null(root.Element(Cac + "TaxExchangeRate"));
        Assert.Single(root.Elements(Cac + "TaxTotal"));
    }

    [Fact]
    public void Tax_currency_code_is_omitted_when_it_matches_the_document_currency()
    {
        var invoice = TestDocuments.DomesticRon() with { TaxCurrencyCode = "RON" };

        var root = XDocument.Parse(UblWriter.Write(invoice)).Root!;

        Assert.Null(root.Element(Cbc + "TaxCurrencyCode"));
        Assert.Single(root.Elements(Cac + "TaxTotal"));
    }

    [Theory]
    [InlineData(UblTaxCategory.StandardRate, "S")]
    [InlineData(UblTaxCategory.ZeroRated, "Z")]
    [InlineData(UblTaxCategory.ReverseCharge, "AE")]
    [InlineData(UblTaxCategory.Exempt, "E")]
    [InlineData(UblTaxCategory.NotSubject, "O")]
    [InlineData(UblTaxCategory.IntraCommunity, "K")]
    [InlineData(UblTaxCategory.Export, "G")]
    public void Tax_category_maps_to_its_uncl5305_code(UblTaxCategory category, string expected)
    {
        var invoice = TestDocuments.DomesticRon() with
        {
            Lines = [new UblLine { Name = "X", Quantity = 1m, UnitPrice = 100m, TaxRate = 0m, TaxCategory = category }],
        };

        var root = XDocument.Parse(UblWriter.Write(invoice)).Root!;

        var lineCategory = root.Element(Cac + "InvoiceLine")!
            .Element(Cac + "Item")!
            .Element(Cac + "ClassifiedTaxCategory")!
            .Element(Cbc + "ID")!.Value;

        Assert.Equal(expected, lineCategory);
    }

    [Fact]
    public void Line_ids_are_sequential_from_one()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.MixedTaxCategories())).Root!;

        var ids = root.Elements(Cac + "InvoiceLine").Select(l => l.Element(Cbc + "ID")!.Value);

        Assert.Equal(["1", "2", "3", "4"], ids);
    }

    [Fact]
    public void Quantities_use_four_decimals_and_amounts_two()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        var line = root.Element(Cac + "InvoiceLine")!;

        Assert.Equal("10.0000", line.Element(Cbc + "InvoicedQuantity")?.Value);
        Assert.Equal("2500.00", line.Element(Cbc + "LineExtensionAmount")?.Value);
    }

    [Fact]
    public void Unit_is_mapped_to_a_unece_code()
    {
        var root = XDocument.Parse(UblWriter.Write(TestDocuments.DomesticRon())).Root!;

        var unitCode = root.Element(Cac + "InvoiceLine")!
            .Element(Cbc + "InvoicedQuantity")!
            .Attribute("unitCode")!.Value;

        Assert.Equal("HUR", unitCode);
    }

    [Fact]
    public void Optional_elements_are_omitted_rather_than_emitted_empty()
    {
        // Built here rather than reusing Minimal(), which carries a due date because BR-CO-25
        // requires one. This document is about omission, not about being acceptable to ANAF.
        var bare = TestDocuments.Minimal() with { DueDate = null };

        var root = XDocument.Parse(UblWriter.Write(bare)).Root!;

        Assert.Null(root.Element(Cbc + "DueDate"));
        Assert.Null(root.Element(Cbc + "Note"));
        Assert.Null(root.Element(Cbc + "BuyerReference"));
        Assert.Null(root.Element(Cac + "PaymentMeans"));
        Assert.Null(root.Element(Cac + "InvoiceLine")!.Element(Cac + "Item")!.Element(Cbc + "Description"));
    }
}

public class UblWriterOptionTests
{
    [Fact]
    public void Xml_declaration_is_omitted_by_default()
    {
        // Matches the originating application byte for byte; see NOTES.md.
        var xml = UblWriter.Write(TestDocuments.DomesticRon());

        Assert.DoesNotContain("<?xml", xml, StringComparison.Ordinal);
        Assert.StartsWith("<Invoice", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_declaration_is_emitted_when_requested()
    {
        var xml = UblWriter.Write(
            TestDocuments.DomesticRon(),
            new UblWriteOptions { IncludeXmlDeclaration = true });

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Indentation_can_be_turned_off()
    {
        var compact = UblWriter.Write(TestDocuments.Minimal(), new UblWriteOptions { Indent = false });

        Assert.DoesNotContain("\n", compact, StringComparison.Ordinal);
    }

    [Fact]
    public void Customization_and_version_are_overridable()
    {
        var xml = UblWriter.Write(
            TestDocuments.Minimal(),
            new UblWriteOptions { CustomizationId = "custom:id", UblVersionId = "2.2" });

        var root = XDocument.Parse(xml).Root!;
        var cbc = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        Assert.Equal("custom:id", root.Element(cbc + "CustomizationID")?.Value);
        Assert.Equal("2.2", root.Element(cbc + "UBLVersionID")?.Value);
    }

    [Fact]
    public void Writing_is_deterministic()
    {
        var first = UblWriter.Write(TestDocuments.MixedTaxCategories());
        var second = UblWriter.Write(TestDocuments.MixedTaxCategories());

        Assert.Equal(first, second);
    }

    [Fact]
    public void ToXDocument_matches_the_string_form()
    {
        var document = UblWriter.ToXDocument(TestDocuments.DomesticRon());
        var xml = UblWriter.Write(TestDocuments.DomesticRon(), new UblWriteOptions { Indent = false });

        Assert.Equal(xml, document.Root!.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void WriteTo_writes_to_the_supplied_writer()
    {
        var builder = new System.Text.StringBuilder();
        using (var writer = System.Xml.XmlWriter.Create(builder, new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            UblWriter.WriteTo(TestDocuments.DomesticRon(), writer);
        }

        Assert.Contains("FCT nr. 1001", builder.ToString(), StringComparison.Ordinal);
    }
}

public class UblWriterValidationTests
{
    [Fact]
    public void Write_rejects_a_null_document()
    {
        Assert.Throws<ArgumentNullException>(() => UblWriter.Write(null!));
    }

    [Fact]
    public void WriteTo_rejects_a_null_writer()
    {
        Assert.Throws<ArgumentNullException>(() => UblWriter.WriteTo(TestDocuments.DomesticRon(), null!));
    }

    [Fact]
    public void Unknown_county_falls_back_to_bucharest_by_default()
    {
        var invoice = TestDocuments.DomesticRon() with
        {
            Seller = TestDocuments.Seller with
            {
                Address = new UblAddress { CountrySubdivision = "Nu Există" },
            },
        };

        var root = XDocument.Parse(UblWriter.Write(invoice)).Root!;
        var cbc = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        var cac = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");

        var county = root.Element(cac + "AccountingSupplierParty")!
            .Element(cac + "Party")!
            .Element(cac + "PostalAddress")!
            .Element(cbc + "CountrySubentity")!.Value;

        Assert.Equal("RO-B", county);
    }

    [Fact]
    public void Unknown_county_throws_in_strict_mode()
    {
        var invoice = TestDocuments.DomesticRon() with
        {
            Seller = TestDocuments.Seller with
            {
                Address = new UblAddress { CountrySubdivision = "Nu Există" },
            },
        };

        var exception = Assert.Throws<UblValidationException>(
            () => UblWriter.Write(invoice, new UblWriteOptions { StrictAddressValidation = true }));

        Assert.Contains("Nu Există", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Strict_mode_reports_every_bad_address_at_once()
    {
        var bad = new UblAddress { CountrySubdivision = null };
        var invoice = TestDocuments.DomesticRon() with
        {
            Seller = TestDocuments.Seller with { Address = bad },
            Buyer = TestDocuments.Buyer with { Address = bad },
        };

        var exception = Assert.Throws<UblValidationException>(
            () => UblWriter.Write(invoice, new UblWriteOptions { StrictAddressValidation = true }));

        // Both parties are reported, rather than failing on the first: county, street and city for
        // each of the two addresses.
        Assert.Equal(6, exception.Errors.Count);
    }
}
