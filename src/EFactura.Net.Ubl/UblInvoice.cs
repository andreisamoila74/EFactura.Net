namespace EFactura.Net.Ubl;

/// <summary>
/// A commercial invoice. Serialized as a UBL <c>Invoice</c> document and uploaded to ANAF with
/// <c>standard=UBL</c>.
/// </summary>
public sealed record UblInvoice : UblDocument
{
    /// <summary>
    /// Payment due date (BT-9), written to <c>cbc:DueDate</c>. Omitted from the XML when
    /// <see langword="null"/>.
    /// </summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>
    /// Invoice type code (BT-3), written to <c>cbc:InvoiceTypeCode</c>. Defaults to <c>"380"</c>
    /// (commercial invoice); other values CIUS-RO accepts include <c>384</c> (corrected invoice),
    /// <c>389</c> (self-billed) and <c>751</c> (invoice information for accounting).
    /// </summary>
    public string InvoiceTypeCode { get; init; } = "380";
}
