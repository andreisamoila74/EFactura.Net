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
    /// Item description (BT-154), written to <c>cac:Item/cbc:Description</c>. Omitted when
    /// <see langword="null"/>.
    /// </summary>
    public string? Description { get; init; }
}
