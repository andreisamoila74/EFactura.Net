namespace EFactura.Net.Ubl;

/// <summary>Payment instructions (BG-16), written to <c>cac:PaymentMeans</c>.</summary>
public sealed record UblPaymentMeans
{
    /// <summary>
    /// Payment means code (BT-81) from UNCL4461, written to <c>cbc:PaymentMeansCode</c>. Defaults to
    /// <c>"42"</c> (payment to bank account), which is what Romanian B2B invoices normally use.
    /// </summary>
    public string PaymentMeansCode { get; init; } = "42";

    /// <summary>
    /// Account to be credited (BG-17). Omitted from the XML when <see langword="null"/>.
    /// </summary>
    public UblBankAccount? PayeeAccount { get; init; }
}

/// <summary>A bank account (BG-17), written to <c>cac:PayeeFinancialAccount</c>.</summary>
public sealed record UblBankAccount
{
    /// <summary>
    /// IBAN (BT-84), written to <c>cbc:ID</c>.
    /// </summary>
    /// <remarks>
    /// ANAF validates the IBAN format when this element is present. Whitespace is stripped and the
    /// value upper-cased on write, so a grouped form such as <c>"RO49 AAAA 1B31 0075 9384 0000"</c>
    /// is accepted.
    /// </remarks>
    public required string Iban { get; init; }

    /// <summary>
    /// Bank name (BT-86), written to <c>cac:FinancialInstitutionBranch/cbc:Name</c>. Omitted when
    /// <see langword="null"/> or empty.
    /// </summary>
    public string? BankName { get; init; }
}
