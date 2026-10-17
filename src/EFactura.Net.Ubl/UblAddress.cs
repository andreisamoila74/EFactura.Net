namespace EFactura.Net.Ubl;

/// <summary>A postal address (BG-5 / BG-8).</summary>
public sealed record UblAddress
{
    /// <summary>Street and number (BT-35 / BT-50), written to <c>cbc:StreetName</c>.</summary>
    public string? StreetName { get; init; }

    /// <summary>
    /// City (BT-37 / BT-52), written to <c>cbc:CityName</c>.
    /// </summary>
    /// <remarks>
    /// Normalized on write by <see cref="RomanianAddress.NormalizeCityName"/>: administrative prefixes
    /// such as <c>Mun.</c> or <c>Comuna</c> are stripped, and Bucharest sectors become the
    /// <c>SECTOR1</c>…<c>SECTOR6</c> form ANAF requires.
    /// </remarks>
    public string? CityName { get; init; }

    /// <summary>
    /// County (BT-39 / BT-54), written to <c>cbc:CountrySubentity</c>.
    /// </summary>
    /// <remarks>
    /// Accepts either a county name (<c>"Gorj"</c>) or an ISO 3166-2:RO code (<c>"RO-GJ"</c>); names
    /// are converted on write by <see cref="RomanianAddress.ToCountyCode"/>.
    /// </remarks>
    public string? CountrySubdivision { get; init; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code (BT-40 / BT-55), written to
    /// <c>cac:Country/cbc:IdentificationCode</c>. Defaults to <c>"RO"</c>.
    /// </summary>
    public string CountryCode { get; init; } = "RO";
}
