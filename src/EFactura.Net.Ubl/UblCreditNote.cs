namespace EFactura.Net.Ubl;

/// <summary>
/// A credit note (<em>storno</em>). Serialized as a UBL <c>CreditNote</c> document and uploaded to
/// ANAF with <c>standard=CN</c>.
/// </summary>
/// <remarks>
/// Quantities and amounts are stated as positive numbers — the <c>CreditNote</c> document type itself
/// carries the reversing sense, so negating the values would double the reversal.
/// </remarks>
public sealed record UblCreditNote : UblDocument
{
    /// <summary>
    /// The invoice being credited (BG-3), written to <c>cac:BillingReference</c>.
    /// </summary>
    /// <remarks>
    /// Required: a credit note that does not say what it reverses is rejected by ANAF.
    /// </remarks>
    public required UblDocumentReference PrecedingInvoice { get; init; }

    /// <summary>
    /// Credit note type code (BT-3), written to <c>cbc:CreditNoteTypeCode</c>. Defaults to
    /// <c>"381"</c>.
    /// </summary>
    public string CreditNoteTypeCode { get; init; } = "381";
}
