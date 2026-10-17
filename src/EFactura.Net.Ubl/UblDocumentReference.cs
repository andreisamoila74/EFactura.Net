namespace EFactura.Net.Ubl;

/// <summary>
/// A reference to another document — used for the invoice a credit note reverses (BG-3).
/// </summary>
public sealed record UblDocumentReference
{
    /// <summary>
    /// Identifier of the referenced document (BT-25), written to <c>cbc:ID</c>.
    /// </summary>
    /// <remarks>
    /// Must match the <see cref="UblDocument.DocumentId"/> of the original document exactly, or ANAF
    /// cannot tie the credit note to it.
    /// </remarks>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Issue date of the referenced document (BT-26), written to <c>cbc:IssueDate</c>. Omitted when
    /// <see langword="null"/>.
    /// </summary>
    public DateOnly? IssueDate { get; init; }
}
