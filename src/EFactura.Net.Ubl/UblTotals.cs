namespace EFactura.Net.Ubl;

/// <summary>
/// Computes the monetary totals of a document — the same numbers <see cref="UblWriter"/> writes into
/// <c>cac:TaxTotal</c> and <c>cac:LegalMonetaryTotal</c>.
/// </summary>
/// <remarks>
/// Public so that anything else presenting the document — a PDF, an on-screen preview, an accounting
/// export — can use the figures that actually went into the XML instead of recomputing them and
/// drifting by a ban.
/// </remarks>
public static class UblTotals
{
    /// <summary>Number of decimal places all monetary amounts are rounded to.</summary>
    public const int MonetaryDecimals = 2;

    /// <summary>Calculates the totals for <paramref name="document"/>.</summary>
    /// <param name="document">The document to total. Must not be <see langword="null"/>.</param>
    /// <returns>Line, tax and payable totals, plus one subtotal per VAT category and rate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Every intermediate value is rounded to <see cref="MonetaryDecimals"/> decimals with
    /// <see cref="MidpointRounding.AwayFromZero"/> <em>as it is accumulated</em>, not at formatting
    /// time. That is what keeps the emitted figures internally consistent: the VAT subtotals sum
    /// exactly to the VAT total (EN 16931 <c>BR-CO-14</c>) and the line amounts sum exactly to
    /// <see cref="UblTotalsResult.LineExtensionAmount"/> (<c>BR-CO-10</c>).
    /// </para>
    /// <para>
    /// Lines are grouped by VAT category <em>and</em> rate, so a 0% exempt line and a 0% reverse-charge
    /// line produce two subtotals rather than one. Subtotals keep first-appearance order, making the
    /// output stable for snapshot comparison.
    /// </para>
    /// </remarks>
    public static UblTotalsResult Calculate(UblDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        decimal lineExtensionAmount = 0m;

        // Keyed by (category, rate); the list preserves first-appearance order.
        var order = new List<(UblTaxCategory Category, decimal Rate)>();
        var groups = new Dictionary<(UblTaxCategory, decimal), (decimal Taxable, decimal Tax)>();

        foreach (var line in document.Lines)
        {
            var lineTotal = Round(line.Quantity * line.UnitPrice);
            lineExtensionAmount += lineTotal;

            var lineTax = Round(lineTotal * line.TaxRate / 100m);

            var key = (line.TaxCategory, line.TaxRate);
            if (!groups.TryGetValue(key, out var current))
            {
                order.Add(key);
                current = (0m, 0m);
            }

            groups[key] = (current.Taxable + lineTotal, current.Tax + lineTax);
        }

        var subtotals = new List<UblTaxSubtotal>(order.Count);
        decimal taxAmount = 0m;

        foreach (var key in order)
        {
            var (taxable, tax) = groups[key];
            taxAmount += tax;

            subtotals.Add(new UblTaxSubtotal
            {
                Category = key.Category,
                Percent = key.Rate,
                TaxableAmount = taxable,
                TaxAmount = tax,
            });
        }

        return new UblTotalsResult
        {
            LineExtensionAmount = lineExtensionAmount,
            TaxExclusiveAmount = lineExtensionAmount,
            TaxAmount = taxAmount,
            TaxInclusiveAmount = lineExtensionAmount + taxAmount,
            PayableAmount = lineExtensionAmount + taxAmount,
            TaxSubtotals = subtotals,
        };
    }

    internal static decimal Round(decimal value) =>
        Math.Round(value, MonetaryDecimals, MidpointRounding.AwayFromZero);
}

/// <summary>The computed totals of a document.</summary>
public sealed record UblTotalsResult
{
    /// <summary>Sum of all line net amounts (BT-106), written to <c>cbc:LineExtensionAmount</c>.</summary>
    public required decimal LineExtensionAmount { get; init; }

    /// <summary>Total excluding VAT (BT-109), written to <c>cbc:TaxExclusiveAmount</c>.</summary>
    public required decimal TaxExclusiveAmount { get; init; }

    /// <summary>Total including VAT (BT-112), written to <c>cbc:TaxInclusiveAmount</c>.</summary>
    public required decimal TaxInclusiveAmount { get; init; }

    /// <summary>Amount due for payment (BT-115), written to <c>cbc:PayableAmount</c>.</summary>
    public required decimal PayableAmount { get; init; }

    /// <summary>Total VAT (BT-110), written to <c>cac:TaxTotal/cbc:TaxAmount</c>.</summary>
    public required decimal TaxAmount { get; init; }

    /// <summary>
    /// One entry per VAT category and rate (BG-23), in first-appearance order. Their
    /// <see cref="UblTaxSubtotal.TaxAmount"/> values sum exactly to <see cref="TaxAmount"/>.
    /// </summary>
    public required IReadOnlyList<UblTaxSubtotal> TaxSubtotals { get; init; }
}

/// <summary>VAT breakdown for one category and rate (BG-23).</summary>
public sealed record UblTaxSubtotal
{
    /// <summary>Net amount taxed at this category and rate (BT-116).</summary>
    public required decimal TaxableAmount { get; init; }

    /// <summary>VAT on <see cref="TaxableAmount"/> (BT-117).</summary>
    public required decimal TaxAmount { get; init; }

    /// <summary>The VAT rate as a percentage (BT-119).</summary>
    public required decimal Percent { get; init; }

    /// <summary>The VAT category (BT-118).</summary>
    public required UblTaxCategory Category { get; init; }
}
