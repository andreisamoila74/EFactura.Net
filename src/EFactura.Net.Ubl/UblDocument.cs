namespace EFactura.Net.Ubl;

/// <summary>
/// Base for the two document kinds RO e-Factura accepts: <see cref="UblInvoice"/> and
/// <see cref="UblCreditNote"/>.
/// </summary>
/// <remarks>
/// The hierarchy is closed — the constructor is <c>private protected</c>, so this type cannot be
/// derived from outside the assembly. Match on <see cref="UblInvoice"/> / <see cref="UblCreditNote"/>
/// and the switch is exhaustive.
/// </remarks>
public abstract record UblDocument
{
    private protected UblDocument()
    {
    }

    /// <summary>
    /// Document identifier (BT-1), written to <c>cbc:ID</c> exactly as supplied.
    /// </summary>
    /// <remarks>
    /// This is the full human-readable identifier, not a bare number — compose series and number
    /// yourself, for example <c>$"{series} nr. {number}"</c>. ANAF stores whatever is sent here and
    /// it appears on the buyer's copy, so the format should stay stable across a series.
    /// </remarks>
    public required string DocumentId { get; init; }

    /// <summary>Issue date (BT-2), written to <c>cbc:IssueDate</c> as <c>yyyy-MM-dd</c>.</summary>
    public required DateOnly IssueDate { get; init; }

    /// <summary>
    /// Document currency (BT-5) as an ISO 4217 code, written to <c>cbc:DocumentCurrencyCode</c>.
    /// </summary>
    public required string CurrencyCode { get; init; }

    /// <summary>
    /// VAT accounting currency (BT-6), written to <c>cbc:TaxCurrencyCode</c>. Set this only when VAT
    /// is accounted in a different currency than <see cref="CurrencyCode"/> — for Romanian invoices
    /// issued in a foreign currency that means <c>"RON"</c>.
    /// </summary>
    /// <remarks>
    /// When this is set and differs from <see cref="CurrencyCode"/>, EN 16931 also requires the VAT
    /// total restated in this currency. Supply <see cref="TaxExchangeRate"/> and the writer emits both
    /// <c>cac:TaxExchangeRate</c> and the second <c>cac:TaxTotal</c>; omit it and neither is written.
    /// </remarks>
    public string? TaxCurrencyCode { get; init; }

    /// <summary>
    /// Exchange rate (BT-6 support) from <see cref="CurrencyCode"/> to <see cref="TaxCurrencyCode"/>,
    /// written to <c>cac:TaxExchangeRate/cbc:CalculationRate</c>.
    /// </summary>
    /// <remarks>
    /// Ignored unless <see cref="TaxCurrencyCode"/> is set and differs from <see cref="CurrencyCode"/>.
    /// </remarks>
    public decimal? TaxExchangeRate { get; init; }

    /// <summary>Supplier (BG-4), written to <c>cac:AccountingSupplierParty</c>.</summary>
    public required UblParty Seller { get; init; }

    /// <summary>Customer (BG-7), written to <c>cac:AccountingCustomerParty</c>.</summary>
    public required UblParty Buyer { get; init; }

    /// <summary>
    /// Document lines (BG-25). At least one line is required for a document ANAF will accept.
    /// </summary>
    public required IReadOnlyList<UblLine> Lines { get; init; }

    /// <summary>
    /// Payment details (BG-16), written to <c>cac:PaymentMeans</c>. Omitted from the XML when
    /// <see langword="null"/>.
    /// </summary>
    public UblPaymentMeans? PaymentMeans { get; init; }

    /// <summary>Free-text note (BT-22), written to <c>cbc:Note</c>. Omitted when <see langword="null"/>.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Buyer reference (BT-10), written to <c>cbc:BuyerReference</c>. Omitted when <see langword="null"/>.
    /// </summary>
    public string? BuyerReference { get; init; }
}
