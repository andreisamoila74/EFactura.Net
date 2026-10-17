namespace EFactura.Net.Ubl;

/// <summary>A document line (BG-25).</summary>
public sealed record UblLine
{
    /// <summary>Item name (BT-153), written to <c>cac:Item/cbc:Name</c>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Quantity (BT-129), written to <c>cbc:InvoicedQuantity</c> (or <c>cbc:CreditedQuantity</c> on a
    /// credit note) with four decimal places.
    /// </summary>
    public required decimal Quantity { get; init; }

    /// <summary>
    /// Unit of measure as a Romanian name — <c>buc</c>, <c>ore</c>, <c>kg</c>, <c>mp</c>, …
    /// </summary>
    /// <remarks>
    /// Mapped to a UN/ECE Recommendation 20 code by <see cref="UblUnitCodes.FromRomanianUnit"/> and
    /// written as the <c>unitCode</c> attribute. Unknown or <see langword="null"/> values fall back to
    /// <see cref="UblUnitCodes.Default"/> (<c>H87</c>, piece).
    /// </remarks>
    public string? Unit { get; init; }

    /// <summary>Net unit price (BT-146), written to <c>cac:Price/cbc:PriceAmount</c>.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>
    /// VAT rate as a percentage (BT-152) — <c>21m</c> means 21%, not <c>0.21m</c>.
    /// </summary>
    public required decimal TaxRate { get; init; }

    /// <summary>
    /// VAT category (BT-151). Defaults to <see cref="UblTaxCategory.StandardRate"/>.
    /// </summary>
    /// <remarks>
    /// The category is not inferred from <see cref="TaxRate"/>: a 0% rate can mean exempt, reverse
    /// charge, intra-community supply or export, and each carries a different code and different
    /// obligations. Set it explicitly whenever <see cref="TaxRate"/> is zero.
    /// </remarks>
    public UblTaxCategory TaxCategory { get; init; } = UblTaxCategory.StandardRate;

    /// <summary>
    /// Why this line carries no VAT (BT-120) — for example <c>"Taxare inversă conform art. 331 Cod
    /// Fiscal"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EN 16931 requires a reason on every VAT breakdown whose category is reverse charge, exempt,
    /// not-subject, intra-community or export (rules <c>BR-AE-10</c>, <c>BR-E-10</c>, <c>BR-O-10</c>,
    /// <c>BR-IC-10</c>, <c>BR-G-10</c>). ANAF rejects documents that omit it.
    /// </para>
    /// <para>
    /// Leave it <see langword="null"/> and a generic standard text is emitted so the document is
    /// still accepted — but supply the actual legal basis whenever you have it, because the generic
    /// wording tells the buyer's accountant nothing.
    /// </para>
    /// </remarks>
    public string? TaxExemptionReason { get; init; }

    /// <summary>
    /// Coded reason for the VAT exemption (BT-121), from the CEF VATEX code list — for example
    /// <c>"VATEX-EU-AE"</c>.
    /// </summary>
    /// <remarks>Optional. Supplying either this or <see cref="TaxExemptionReason"/> satisfies the rule.</remarks>
    public string? TaxExemptionReasonCode { get; init; }

    /// <summary>
    /// Item description (BT-154), written to <c>cac:Item/cbc:Description</c>. Omitted when
    /// <see langword="null"/>.
    /// </summary>
    public string? Description { get; init; }
}
