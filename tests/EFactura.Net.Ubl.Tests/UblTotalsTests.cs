namespace EFactura.Net.Ubl.Tests;

public class UblTotalsTests
{
    private static UblInvoice WithLines(params UblLine[] lines) =>
        TestDocuments.DomesticRon() with { Lines = lines };

    [Fact]
    public void Sums_line_amounts_and_vat()
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = 2m, UnitPrice = 100m, TaxRate = 21m },
            new UblLine { Name = "B", Quantity = 1m, UnitPrice = 300m, TaxRate = 21m }));

        Assert.Equal(500m, totals.LineExtensionAmount);
        Assert.Equal(500m, totals.TaxExclusiveAmount);
        Assert.Equal(105m, totals.TaxAmount);
        Assert.Equal(605m, totals.TaxInclusiveAmount);
        Assert.Equal(605m, totals.PayableAmount);
    }

    /// <summary>
    /// EN 16931 BR-CO-14. The original rounded only at print time, so three lines each landing on a
    /// half-ban could print subtotals that did not add up to the printed total.
    /// </summary>
    [Fact]
    public void Vat_subtotals_sum_exactly_to_the_vat_total()
    {
        var totals = UblTotals.Calculate(TestDocuments.RequiringRounding());

        Assert.Equal(totals.TaxAmount, totals.TaxSubtotals.Sum(s => s.TaxAmount));
    }

    /// <summary>EN 16931 BR-CO-10.</summary>
    [Fact]
    public void Line_amounts_sum_exactly_to_the_line_extension_amount()
    {
        var invoice = TestDocuments.RequiringRounding();

        var totals = UblTotals.Calculate(invoice);
        var lineSum = invoice.Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2, MidpointRounding.AwayFromZero));

        Assert.Equal(lineSum, totals.LineExtensionAmount);
    }

    [Theory]
    // 33.335 * 3 = 100.005 -> 100.01, not 100.00 as banker's rounding would give.
    [InlineData(3, 33.335, 100.01)]
    [InlineData(1, 0.005, 0.01)]
    [InlineData(1, 0.015, 0.02)]
    [InlineData(1, 0.025, 0.03)]
    [InlineData(1, 2.344, 2.34)]
    [InlineData(1, 2.345, 2.35)]
    public void Rounds_away_from_zero_not_to_even(decimal quantity, decimal unitPrice, decimal expected)
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = quantity, UnitPrice = unitPrice, TaxRate = 0m }));

        Assert.Equal(expected, totals.LineExtensionAmount);
    }

    [Fact]
    public void Groups_by_rate()
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = 1m, UnitPrice = 100m, TaxRate = 21m },
            new UblLine { Name = "B", Quantity = 1m, UnitPrice = 200m, TaxRate = 21m },
            new UblLine { Name = "C", Quantity = 1m, UnitPrice = 300m, TaxRate = 11m }));

        Assert.Equal(2, totals.TaxSubtotals.Count);

        var standard = totals.TaxSubtotals.Single(s => s.Percent == 21m);
        Assert.Equal(300m, standard.TaxableAmount);
        Assert.Equal(63m, standard.TaxAmount);

        var reduced = totals.TaxSubtotals.Single(s => s.Percent == 11m);
        Assert.Equal(300m, reduced.TaxableAmount);
        Assert.Equal(33m, reduced.TaxAmount);
    }

    /// <summary>
    /// Two lines can share a 0% rate while meaning entirely different things. Grouping on the rate
    /// alone would merge an exempt supply with a reverse-charge one.
    /// </summary>
    [Fact]
    public void Separates_categories_that_share_a_rate()
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = 1m, UnitPrice = 100m, TaxRate = 0m, TaxCategory = UblTaxCategory.ReverseCharge },
            new UblLine { Name = "B", Quantity = 1m, UnitPrice = 200m, TaxRate = 0m, TaxCategory = UblTaxCategory.Exempt }));

        Assert.Equal(2, totals.TaxSubtotals.Count);
        Assert.Equal(100m, totals.TaxSubtotals.Single(s => s.Category == UblTaxCategory.ReverseCharge).TaxableAmount);
        Assert.Equal(200m, totals.TaxSubtotals.Single(s => s.Category == UblTaxCategory.Exempt).TaxableAmount);
    }

    [Fact]
    public void Subtotals_keep_first_appearance_order()
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = 1m, UnitPrice = 100m, TaxRate = 11m },
            new UblLine { Name = "B", Quantity = 1m, UnitPrice = 100m, TaxRate = 21m },
            new UblLine { Name = "C", Quantity = 1m, UnitPrice = 100m, TaxRate = 11m }));

        // Deterministic ordering is what makes the snapshots stable.
        Assert.Equal([11m, 21m], totals.TaxSubtotals.Select(s => s.Percent));
    }

    [Fact]
    public void Handles_a_document_with_no_lines()
    {
        var totals = UblTotals.Calculate(WithLines());

        Assert.Equal(0m, totals.LineExtensionAmount);
        Assert.Equal(0m, totals.TaxAmount);
        Assert.Empty(totals.TaxSubtotals);
    }

    [Fact]
    public void Zero_rate_produces_a_subtotal_with_no_tax()
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = 1m, UnitPrice = 100m, TaxRate = 0m, TaxCategory = UblTaxCategory.ZeroRated }));

        var subtotal = Assert.Single(totals.TaxSubtotals);
        Assert.Equal(100m, subtotal.TaxableAmount);
        Assert.Equal(0m, subtotal.TaxAmount);
        Assert.Equal(100m, totals.PayableAmount);
    }

    [Fact]
    public void Fractional_quantities_are_handled()
    {
        var totals = UblTotals.Calculate(WithLines(
            new UblLine { Name = "A", Quantity = 1.5m, UnitPrice = 10m, TaxRate = 21m }));

        Assert.Equal(15m, totals.LineExtensionAmount);
        Assert.Equal(3.15m, totals.TaxAmount);
    }

    [Fact]
    public void Rejects_a_null_document()
    {
        Assert.Throws<ArgumentNullException>(() => UblTotals.Calculate(null!));
    }

    /// <summary>The totals a caller computes must be the ones the writer emitted.</summary>
    [Theory]
    [MemberData(nameof(TestDocuments.ScenarioNames), MemberType = typeof(TestDocuments))]
    public void Match_the_amounts_written_into_the_xml(string scenario)
    {
        var document = TestDocuments.ByName(scenario);
        var totals = UblTotals.Calculate(document);

        var root = System.Xml.Linq.XDocument.Parse(UblWriter.Write(document)).Root!;
        var cbc = System.Xml.Linq.XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        var cac = System.Xml.Linq.XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");

        var monetaryTotal = root.Element(cac + "LegalMonetaryTotal")!;

        Assert.Equal(
            totals.PayableAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            monetaryTotal.Element(cbc + "PayableAmount")?.Value);
        Assert.Equal(
            totals.TaxAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            root.Elements(cac + "TaxTotal").First().Element(cbc + "TaxAmount")?.Value);
    }
}
