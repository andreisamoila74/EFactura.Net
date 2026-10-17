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
