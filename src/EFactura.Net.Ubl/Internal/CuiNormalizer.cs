namespace EFactura.Net.Ubl.Internal;

/// <summary>
/// Single place where Romanian tax identifiers are normalized. The originating application had four
/// divergent copies of this logic; this is the reconciled one.
/// </summary>
internal static class CuiNormalizer
{
    /// <summary>
    /// Normalizes a CUI for use as a VAT identifier: trimmed, upper-cased, inner whitespace removed,
    /// and prefixed with <c>RO</c> when the prefix is absent.
    /// </summary>
    internal static string ToVatIdentifier(string? cui)
    {
        var clean = Strip(cui);

        return clean.StartsWith("RO", StringComparison.Ordinal) ? clean : "RO" + clean;
    }

    /// <summary>
    /// Normalizes a CUI to its bare numeric form, with any <c>RO</c> prefix removed. This is the form
    /// ANAF's <c>cif</c> query parameter and company-lookup endpoint expect.
    /// </summary>
    internal static string ToBareNumber(string? cui)
    {
        var clean = Strip(cui);

        return clean.StartsWith("RO", StringComparison.Ordinal) ? clean[2..] : clean;
    }

    private static string Strip(string? cui)
    {
        if (string.IsNullOrWhiteSpace(cui))
        {
            return "";
        }

        var clean = cui.Trim().ToUpperInvariant();

        return clean.Contains(' ', StringComparison.Ordinal)
            ? clean.Replace(" ", "", StringComparison.Ordinal)
            : clean;
    }
}
