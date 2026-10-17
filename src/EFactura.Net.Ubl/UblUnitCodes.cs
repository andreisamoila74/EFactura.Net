namespace EFactura.Net.Ubl;

/// <summary>
/// Maps Romanian unit-of-measure names to UN/ECE Recommendation 20 codes, the code list ANAF
/// validates the <c>unitCode</c> attribute against.
/// </summary>
public static class UblUnitCodes
{
    /// <summary>
    /// Code used when a unit is unrecognised, <see langword="null"/> or empty — <c>H87</c>, "piece".
    /// </summary>
    public const string Default = "H87";

    /// <summary>Piece — <c>buc</c>, <c>bucata</c>, <c>bucati</c>.</summary>
    public const string Piece = "H87";

    /// <summary>Hour — <c>ora</c>, <c>ore</c>.</summary>
    public const string Hour = "HUR";

    /// <summary>Day — <c>zi</c>, <c>zile</c>.</summary>
    public const string Day = "DAY";

    /// <summary>Month — <c>luna</c>, <c>luni</c>.</summary>
    public const string Month = "MON";

    /// <summary>One (dimensionless) — <c>elem</c>, <c>element</c>, <c>elemente</c>.</summary>
    public const string Unit = "C62";

    /// <summary>Set — <c>set</c>, <c>seturi</c>.</summary>
    public const string Set = "SET";

    /// <summary>Kilogram — <c>kg</c>, <c>kilogram</c>.</summary>
    public const string Kilogram = "KGM";

    /// <summary>Metre — <c>m</c>, <c>metru</c>, <c>metri</c>.</summary>
    public const string Metre = "MTR";

    /// <summary>Square metre — <c>mp</c>, <c>m2</c>.</summary>
    public const string SquareMetre = "MTK";

    /// <summary>Litre — <c>l</c>, <c>litru</c>, <c>litri</c>.</summary>
    public const string Litre = "LTR";

    /// <summary>
    /// Maps a Romanian unit name to its UN/ECE Rec 20 code. Matching is case-insensitive and ignores
    /// surrounding whitespace; anything unrecognised returns <see cref="Default"/>.
    /// </summary>
    /// <param name="unit">A Romanian unit name such as <c>"buc"</c>, <c>"ore"</c> or <c>"mp"</c>.</param>
    /// <returns>The UN/ECE Rec 20 code, never <see langword="null"/>.</returns>
    /// <remarks>
    /// Falling back rather than throwing is deliberate: an unknown unit should not block an invoice,
    /// and <c>H87</c> is the safest default. If you need to detect unmapped units, compare the result
    /// against <see cref="Default"/> for an input you know is not a piece.
    /// </remarks>
    public static string FromRomanianUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "buc" or "bucata" or "bucati" => Piece,
        "ora" or "ore" => Hour,
        "zi" or "zile" => Day,
        "luna" or "luni" => Month,
        "elem" or "element" or "elemente" => Unit,
        "set" or "seturi" => Set,
        "kg" or "kilogram" => Kilogram,
        "m" or "metru" or "metri" => Metre,
        "mp" or "m2" => SquareMetre,
        "l" or "litru" or "litri" => Litre,
        _ => Default,
    };
}
