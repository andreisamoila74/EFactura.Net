namespace EFactura.Net.Ubl;

/// <summary>A seller or buyer (BG-4 / BG-7).</summary>
public sealed record UblParty
{
    /// <summary>
    /// Registered legal name (BT-27 / BT-44), written to <c>cac:PartyLegalEntity/cbc:RegistrationName</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// VAT identifier (BT-31 / BT-48), for example <c>"RO12345674"</c>.
    /// </summary>
    /// <remarks>
    /// Normalized on write: trimmed, upper-cased, inner spaces removed, and prefixed with <c>RO</c>
    /// if absent. Pass it with or without the prefix.
    /// <para>
    /// ANAF validates the Romanian check digit, so an invented identifier usually fails — a
    /// rejection reported as <c>ERRIdentif: CUI cumparator incorect</c>.
    /// </para>
    /// </remarks>
    public required string VatIdentifier { get; init; }

    /// <summary>
    /// Whether the party is registered for VAT. When <see langword="true"/> (the default), the
    /// identifier is written under <c>cac:PartyTaxScheme</c> with a <c>VAT</c> tax scheme.
    /// </summary>
    /// <remarks>
    /// Set this to <see langword="false"/> for a party that is not VAT-registered: CIUS-RO then wants
    /// the identifier under <c>cac:PartyLegalEntity/cbc:CompanyID</c> instead, with no
    /// <c>PartyTaxScheme</c> entry, which is what the writer emits.
    /// </remarks>
    public bool IsVatRegistered { get; init; } = true;

    /// <summary>
    /// Trade register number (BT-30 / BT-47), for example <c>"J40/1234/2020"</c>.
    /// </summary>
    /// <remarks>
    /// Written to <c>cac:PartyLegalEntity/cbc:CompanyID</c> for the seller and to
    /// <c>cac:PartyIdentification/cbc:ID</c> for the buyer, mirroring what ANAF expects on each side.
    /// Omitted when <see langword="null"/> or empty.
    /// </remarks>
    public string? LegalRegistrationId { get; init; }

    /// <summary>Postal address (BG-5 / BG-8).</summary>
    public required UblAddress Address { get; init; }
}
