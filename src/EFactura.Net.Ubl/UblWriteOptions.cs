namespace EFactura.Net.Ubl;

/// <summary>Options controlling how <see cref="UblWriter"/> serializes a document.</summary>
public sealed record UblWriteOptions
{
    /// <summary>Defaults: CIUS-RO 1.0.1, UBL 2.1, lenient addresses, no XML declaration.</summary>
    public static UblWriteOptions Default { get; } = new();

    /// <summary>
    /// Specification identifier (BT-24), written to <c>cbc:CustomizationID</c>. Defaults to
    /// CIUS-RO 1.0.1.
    /// </summary>
    public string CustomizationId { get; init; } =
        "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:CIUS-RO:1.0.1";

    /// <summary>UBL version, written to <c>cbc:UBLVersionID</c>. Defaults to <c>"2.1"</c>.</summary>
    public string UblVersionId { get; init; } = "2.1";

    /// <summary>
    /// When <see langword="true"/>, an unrecognised county name throws
    /// <see cref="UblValidationException"/> instead of falling back to
    /// <see cref="RomanianAddress.FallbackCountyCode"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>, matching the lenient behaviour described on
    /// <see cref="RomanianAddress.ToCountyCode"/>. Turning it on is recommended for new integrations:
    /// a typo'd county otherwise ships silently as a Bucharest address.
    /// </remarks>
    public bool StrictAddressValidation { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the output is prefixed with
    /// <c>&lt;?xml version="1.0" encoding="UTF-8"?&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>. ANAF accepts the document either way, and the default
    /// reproduces the exact bytes of the application this library was extracted from.
    /// </remarks>
    public bool IncludeXmlDeclaration { get; init; }

    /// <summary>
    /// When <see langword="true"/> (the default), the XML is indented. Set it to
    /// <see langword="false"/> for the most compact payload.
    /// </summary>
    public bool Indent { get; init; } = true;
}
