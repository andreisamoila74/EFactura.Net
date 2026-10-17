namespace EFactura.Net.Ubl;

/// <summary>
/// VAT category (BT-151 / BT-118), mapped to the UNCL5305 code written to
/// <c>cac:ClassifiedTaxCategory/cbc:ID</c>.
/// </summary>
public enum UblTaxCategory
{
    /// <summary>Standard rate — code <c>S</c>. The ordinary 21%, 11% … Romanian rates.</summary>
    StandardRate,

    /// <summary>Zero-rated goods — code <c>Z</c>. A genuine 0% rate, not an exemption.</summary>
    ZeroRated,

    /// <summary>
    /// Reverse charge (<em>taxare inversă</em>) — code <c>AE</c>. VAT is accounted by the buyer.
    /// </summary>
    ReverseCharge,

    /// <summary>Exempt from VAT (<em>scutit</em>) — code <c>E</c>.</summary>
    Exempt,

    /// <summary>Services outside the scope of VAT — code <c>O</c>.</summary>
    NotSubject,

    /// <summary>Intra-community supply — code <c>K</c>.</summary>
    IntraCommunity,

    /// <summary>Export outside the EU — code <c>G</c>.</summary>
    Export,
}

/// <summary>Helpers for <see cref="UblTaxCategory"/>.</summary>
public static class UblTaxCategoryExtensions
{
    /// <summary>
    /// Whether EN 16931 requires a VAT exemption reason (BT-120 or BT-121) on a breakdown using this
    /// category.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>
    /// <see langword="true"/> for every category that carries no VAT for a stated reason —
    /// reverse charge, exempt, not-subject, intra-community and export.
    /// </returns>
    /// <remarks>
    /// Zero-rated (<c>Z</c>) is deliberately excluded: a genuine 0% rate needs no justification,
    /// which is what distinguishes it from an exemption.
    /// </remarks>
    public static bool RequiresExemptionReason(this UblTaxCategory category) => category switch
    {
        UblTaxCategory.ReverseCharge => true,
        UblTaxCategory.Exempt => true,
        UblTaxCategory.NotSubject => true,
        UblTaxCategory.IntraCommunity => true,
        UblTaxCategory.Export => true,
        _ => false,
    };

    /// <summary>
    /// The standard EN 16931 wording used when a category needs a reason and none was supplied.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>The fallback text, or <see langword="null"/> if the category needs no reason.</returns>
    /// <remarks>
    /// Generic on purpose. It keeps a document from being rejected outright, but it is not a
    /// substitute for citing the actual legal basis.
    /// </remarks>
    public static string? DefaultExemptionReason(this UblTaxCategory category) => category switch
    {
        UblTaxCategory.ReverseCharge => "Reverse charge",
        UblTaxCategory.Exempt => "Exempt from VAT",
        UblTaxCategory.NotSubject => "Not subject to VAT",
        UblTaxCategory.IntraCommunity => "Intra-Community supply",
        UblTaxCategory.Export => "Export outside the EU",
        _ => null,
    };
}
