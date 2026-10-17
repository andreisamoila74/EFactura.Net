using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace EFactura.Net.Ubl;

/// <summary>
/// Shapes Romanian address fields the way CIUS-RO wants them: ISO 3166-2:RO county codes and
/// normalized city names.
/// </summary>
public static partial class RomanianAddress
{
    /// <summary>
    /// Fallback county code used by <see cref="ToCountyCode"/> when the county cannot be resolved —
    /// <c>RO-B</c> (Bucharest).
    /// </summary>
    public const string FallbackCountyCode = "RO-B";

    /// <summary>
    /// Converts a county name to its ISO 3166-2:RO code, for example <c>"Gorj"</c> →
    /// <c>"RO-GJ"</c>.
    /// </summary>
    /// <param name="countyName">
    /// A county name with or without diacritics, optionally prefixed with <c>Județul</c> or
    /// <c>Municipiul</c>. A value already in <c>RO-XX</c> form is returned upper-cased and unchanged.
    /// </param>
    /// <returns>
    /// The ISO code, or <see cref="FallbackCountyCode"/> when the name is empty or unrecognised.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The fallback is lenient by design — it is what the originating application shipped, and
    /// failing an invoice over a misspelled county is worse than defaulting. The consequence is that
    /// a typo silently produces a Bucharest address.
    /// </para>
    /// <para>
    /// Use <see cref="TryToCountyCode"/> when you need to know whether the lookup actually succeeded;
    /// <see cref="UblWriteOptions.StrictAddressValidation"/> turns an unresolved county into an error.
    /// </para>
    /// </remarks>
    public static string ToCountyCode(string? countyName) =>
        TryToCountyCode(countyName, out var code) ? code : FallbackCountyCode;

    /// <summary>
    /// Converts a county name to its ISO 3166-2:RO code, reporting whether the lookup succeeded.
    /// </summary>
    /// <param name="countyName">The county name, in the forms described on <see cref="ToCountyCode"/>.</param>
    /// <param name="code">
    /// The ISO code on success; <see cref="FallbackCountyCode"/> when the method returns
    /// <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if the county was recognised.</returns>
    public static bool TryToCountyCode(string? countyName, [NotNullWhen(true)] out string code)
    {
        code = FallbackCountyCode;

        if (string.IsNullOrWhiteSpace(countyName))
        {
            return false;
        }

        var clean = countyName.Trim().ToUpperInvariant();

        // Already an ISO code.
        if (clean.StartsWith("RO-", StringComparison.Ordinal))
        {
            code = clean;
            return true;
        }

        clean = clean
            .Replace("MUNICIPIUL", "", StringComparison.Ordinal)
            .Replace("JUDEȚUL", "", StringComparison.Ordinal)
            .Replace("JUDETUL", "", StringComparison.Ordinal)
            .Trim();

        if (CountyNameToCode.TryGetValue(clean, out var found))
        {
            code = found;
            return true;
        }

        if (CountyNameToCode.TryGetValue(RemoveDiacritics(clean), out found))
        {
            code = found;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes a city name for <c>cbc:CityName</c>: strips administrative prefixes and converts
    /// Bucharest sectors to the <c>SECTOR1</c>…<c>SECTOR6</c> form ANAF requires.
    /// </summary>
    /// <param name="city">A city name such as <c>"Mun. Târgu Jiu"</c> or <c>"Sector 1 Mun. București"</c>.</param>
    /// <returns>The normalized name, or an empty string when the input is empty.</returns>
    /// <example>
    /// <code>
    /// RomanianAddress.NormalizeCityName("Mun. Târgu Jiu");          // "Târgu Jiu"
    /// RomanianAddress.NormalizeCityName("Sector 1 Mun. București"); // "SECTOR1"
    /// </code>
    /// </example>
    public static string NormalizeCityName(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return "";
        }

        var clean = city.Trim();

        // A sector always wins: ANAF wants the SECTORn token, not the city name.
        if (clean.Contains("Sector", StringComparison.OrdinalIgnoreCase))
        {
            var match = SectorPattern().Match(clean);
            if (match.Success)
            {
                return $"SECTOR{match.Groups[1].Value}";
            }
        }

        foreach (var prefix in AdministrativePrefixes)
        {
            if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[prefix.Length..].Trim();
            }
        }

        // Strip any remaining prefix occurrence together with the token following it
        // ("Târgu Jiu Com. Bâlteni" -> "Târgu Jiu").
        foreach (var prefix in AdministrativePrefixes)
        {
            clean = Regex.Replace(
                clean,
                $@"\s*{Regex.Escape(prefix)}\.?\s*\S*",
                "",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)).Trim();
        }

        return clean;
    }

    private static readonly string[] AdministrativePrefixes =
        ["Mun.", "Municipiul", "Oraș", "Oras", "Com.", "Comuna", "Sat"];

    [GeneratedRegex(@"Sector\s*(\d)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SectorPattern();

    private static string RemoveDiacritics(string text) => text
        .Replace("Ă", "A", StringComparison.Ordinal).Replace("Â", "A", StringComparison.Ordinal)
        .Replace("Î", "I", StringComparison.Ordinal).Replace("Ș", "S", StringComparison.Ordinal)
        .Replace("Ț", "T", StringComparison.Ordinal)
        .Replace("ă", "a", StringComparison.Ordinal).Replace("â", "a", StringComparison.Ordinal)
        .Replace("î", "i", StringComparison.Ordinal).Replace("ș", "s", StringComparison.Ordinal)
        .Replace("ț", "t", StringComparison.Ordinal);

    private static readonly Dictionary<string, string> CountyNameToCode =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "ALBA", "RO-AB" },
            { "ARAD", "RO-AR" },
            { "ARGEȘ", "RO-AG" },
            { "ARGES", "RO-AG" },
            { "BACĂU", "RO-BC" },
            { "BACAU", "RO-BC" },
            { "BIHOR", "RO-BH" },
            { "BISTRIȚA-NĂSĂUD", "RO-BN" },
            { "BISTRITA-NASAUD", "RO-BN" },
            { "BISTRITA NASAUD", "RO-BN" },
            { "BOTOȘANI", "RO-BT" },
            { "BOTOSANI", "RO-BT" },
            { "BRĂILA", "RO-BR" },
            { "BRAILA", "RO-BR" },
            { "BRAȘOV", "RO-BV" },
            { "BRASOV", "RO-BV" },
            { "BUCUREȘTI", "RO-B" },
            { "BUCURESTI", "RO-B" },
            { "BUCHAREST", "RO-B" },
            { "BUZĂU", "RO-BZ" },
            { "BUZAU", "RO-BZ" },
            { "CĂLĂRAȘI", "RO-CL" },
            { "CALARASI", "RO-CL" },
            { "CARAȘ-SEVERIN", "RO-CS" },
            { "CARAS-SEVERIN", "RO-CS" },
            { "CARAS SEVERIN", "RO-CS" },
            { "CLUJ", "RO-CJ" },
            { "CONSTANȚA", "RO-CT" },
            { "CONSTANTA", "RO-CT" },
            { "COVASNA", "RO-CV" },
            { "DÂMBOVIȚA", "RO-DB" },
            { "DAMBOVITA", "RO-DB" },
            { "DOLJ", "RO-DJ" },
            { "GALAȚI", "RO-GL" },
            { "GALATI", "RO-GL" },
            { "GIURGIU", "RO-GR" },
            { "GORJ", "RO-GJ" },
            { "HARGHITA", "RO-HR" },
            { "HUNEDOARA", "RO-HD" },
            { "IALOMIȚA", "RO-IL" },
            { "IALOMITA", "RO-IL" },
            { "IAȘI", "RO-IS" },
            { "IASI", "RO-IS" },
            { "ILFOV", "RO-IF" },
            { "MARAMUREȘ", "RO-MM" },
            { "MARAMURES", "RO-MM" },
            { "MEHEDINȚI", "RO-MH" },
            { "MEHEDINTI", "RO-MH" },
            { "MUREȘ", "RO-MS" },
            { "MURES", "RO-MS" },
            { "NEAMȚ", "RO-NT" },
            { "NEAMT", "RO-NT" },
            { "OLT", "RO-OT" },
            { "PRAHOVA", "RO-PH" },
            { "SĂLAJ", "RO-SJ" },
            { "SALAJ", "RO-SJ" },
            { "SATU MARE", "RO-SM" },
            { "SATU-MARE", "RO-SM" },
            { "SIBIU", "RO-SB" },
            { "SUCEAVA", "RO-SV" },
            { "TELEORMAN", "RO-TR" },
            { "TIMIȘ", "RO-TM" },
            { "TIMIS", "RO-TM" },
            { "TULCEA", "RO-TL" },
            { "VÂLCEA", "RO-VL" },
            { "VALCEA", "RO-VL" },
            { "VASLUI", "RO-VS" },
            { "VRANCEA", "RO-VN" },
        };
}
