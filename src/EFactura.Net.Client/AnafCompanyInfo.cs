namespace EFactura.Net.Client;

/// <summary>
/// What ANAF's public registry knows about a company — identification, VAT standing, special VAT
/// regimes, inactive status and addresses.
/// </summary>
/// <remarks>
/// A flattened projection of ANAF's deeply nested, Romanian-named JSON. Every field is optional:
/// the registry returns different subsets depending on the company and the date queried.
/// </remarks>
public sealed record AnafCompanyInfo
{
    /// <summary>Tax identification number, without the <c>RO</c> prefix.</summary>
    public string? Cui { get; init; }

    /// <summary>The date the registry data is stated as of.</summary>
    public DateOnly? QueryDate { get; init; }

    /// <summary>Registered company name.</summary>
    public string? Name { get; init; }

    /// <summary>Address as a single free-text line.</summary>
    public string? Address { get; init; }

    /// <summary>Trade register number, e.g. <c>J40/1234/2020</c>.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>Telephone number on file.</summary>
    public string? Phone { get; init; }

    /// <summary>Fax number on file.</summary>
    public string? Fax { get; init; }

    /// <summary>Postal code.</summary>
    public string? PostalCode { get; init; }

    /// <summary>The incorporating or amending legal document.</summary>
    public string? LegalDocument { get; init; }

    /// <summary>Registration status, e.g. <c>INREGISTRAT</c> or <c>RADIAT</c>.</summary>
    public string? RegistrationStatus { get; init; }

    /// <summary>Date of registration.</summary>
    public DateOnly? RegistrationDate { get; init; }

    /// <summary>Primary CAEN activity code.</summary>
    public string? CaenCode { get; init; }

    /// <summary>IBAN on file with the registry, if any.</summary>
    public string? Iban { get; init; }

    /// <summary>
    /// Whether the company is enrolled in RO e-Factura.
    /// </summary>
    /// <remarks>
    /// Worth checking before sending: an enrolled buyer is obliged to receive through the system.
    /// </remarks>
    public bool IsEFacturaActive { get; init; }

    /// <summary>The competent tax authority.</summary>
    public string? TaxAuthority { get; init; }

    /// <summary>Form of ownership.</summary>
    public string? OwnershipForm { get; init; }

    /// <summary>Form of organisation.</summary>
    public string? OrganizationForm { get; init; }

    /// <summary>Legal form.</summary>
    public string? LegalForm { get; init; }

    /// <summary>Whether the company is registered for VAT at the queried date.</summary>
    public bool IsVatPayer { get; init; }

    /// <summary>Start of the current VAT registration.</summary>
    public DateOnly? VatStartDate { get; init; }

    /// <summary>End of VAT registration, if it ended.</summary>
    public DateOnly? VatEndDate { get; init; }

    /// <summary>Date VAT registration was annulled, if it was.</summary>
    public DateOnly? VatAnnulmentDate { get; init; }

    /// <summary>Free-text note about the VAT registration.</summary>
    public string? VatMessage { get; init; }

    /// <summary>Whether the company uses cash-basis VAT (<em>TVA la încasare</em>).</summary>
    public bool IsCashVatPayer { get; init; }

    /// <summary>Start of the cash-basis VAT regime.</summary>
    public DateOnly? CashVatStartDate { get; init; }

    /// <summary>End of the cash-basis VAT regime.</summary>
    public DateOnly? CashVatEndDate { get; init; }

    /// <summary>Last update to the cash-basis VAT record.</summary>
    public DateOnly? CashVatUpdateDate { get; init; }

    /// <summary>Publication date of the cash-basis VAT record.</summary>
    public DateOnly? CashVatPublishDate { get; init; }

    /// <summary>Type of document establishing the cash-basis VAT regime.</summary>
    public string? CashVatDocumentType { get; init; }

    /// <summary>
    /// Whether the company is on the inactive-taxpayer list. Invoices to an inactive company carry
    /// deductibility consequences for the buyer.
    /// </summary>
    public bool IsInactive { get; init; }

    /// <summary>Date the company was declared inactive.</summary>
    public DateOnly? InactivationDate { get; init; }

    /// <summary>Date the company was reactivated.</summary>
    public DateOnly? ReactivationDate { get; init; }

    /// <summary>Publication date of the inactive-status record.</summary>
    public DateOnly? InactivePublishDate { get; init; }

    /// <summary>Date the company was struck off.</summary>
    public DateOnly? DeregistrationDate { get; init; }

    /// <summary>Whether the company applies split VAT payment.</summary>
    public bool IsSplitVatPayer { get; init; }

    /// <summary>Start of the split VAT regime.</summary>
    public DateOnly? SplitVatStartDate { get; init; }

    /// <summary>Date the split VAT regime was cancelled.</summary>
    public DateOnly? SplitVatCancelDate { get; init; }

    /// <summary>Registered seat address (<em>sediu social</em>).</summary>
    public AnafAddress? RegisteredAddress { get; init; }

    /// <summary>Fiscal domicile address (<em>domiciliu fiscal</em>).</summary>
    public AnafAddress? TaxDomicileAddress { get; init; }
}

/// <summary>A structured address from the ANAF registry.</summary>
public sealed record AnafAddress
{
    /// <summary>Street name.</summary>
    public string? Street { get; init; }

    /// <summary>Street number.</summary>
    public string? StreetNumber { get; init; }

    /// <summary>City or commune name.</summary>
    public string? City { get; init; }

    /// <summary>ANAF's internal city code.</summary>
    public string? CityCode { get; init; }

    /// <summary>County name.</summary>
    public string? County { get; init; }

    /// <summary>ANAF's internal county code.</summary>
    public string? CountyCode { get; init; }

    /// <summary>
    /// The county's vehicle-registration code (e.g. <c>GJ</c>), which is also the ISO 3166-2:RO
    /// suffix.
    /// </summary>
    public string? CountyAutoCode { get; init; }

    /// <summary>Country name.</summary>
    public string? Country { get; init; }

    /// <summary>Remaining address detail — block, staircase, apartment.</summary>
    public string? Details { get; init; }

    /// <summary>Postal code.</summary>
    public string? PostalCode { get; init; }
}
