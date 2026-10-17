using EFactura.Net.Ubl.Internal;

namespace EFactura.Net.Ubl.Tests;

public class UblUnitCodesTests
{
    [Theory]
    [InlineData("buc", "H87")]
    [InlineData("bucata", "H87")]
    [InlineData("bucati", "H87")]
    [InlineData("ora", "HUR")]
    [InlineData("ore", "HUR")]
    [InlineData("zi", "DAY")]
    [InlineData("zile", "DAY")]
    [InlineData("luna", "MON")]
    [InlineData("luni", "MON")]
    [InlineData("elem", "C62")]
    [InlineData("element", "C62")]
    [InlineData("elemente", "C62")]
    [InlineData("set", "SET")]
    [InlineData("seturi", "SET")]
    [InlineData("kg", "KGM")]
    [InlineData("kilogram", "KGM")]
    [InlineData("m", "MTR")]
    [InlineData("metru", "MTR")]
    [InlineData("metri", "MTR")]
    [InlineData("mp", "MTK")]
    [InlineData("m2", "MTK")]
    [InlineData("l", "LTR")]
    [InlineData("litru", "LTR")]
    [InlineData("litri", "LTR")]
    public void Maps_every_known_romanian_unit(string unit, string expected)
    {
        Assert.Equal(expected, UblUnitCodes.FromRomanianUnit(unit));
    }

    [Theory]
    [InlineData("BUC")]
    [InlineData("Buc")]
    [InlineData("  buc  ")]
    [InlineData("ORE")]
    public void Matching_ignores_case_and_surrounding_whitespace(string unit)
    {
        Assert.NotEqual("", UblUnitCodes.FromRomanianUnit(unit));
        Assert.Contains(UblUnitCodes.FromRomanianUnit(unit), new[] { "H87", "HUR" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unitate necunoscută")]
    [InlineData("widgets")]
    public void Falls_back_to_piece_for_anything_unrecognised(string? unit)
    {
        // Deliberately lenient: an unknown unit must not block an invoice.
        Assert.Equal(UblUnitCodes.Default, UblUnitCodes.FromRomanianUnit(unit));
        Assert.Equal("H87", UblUnitCodes.FromRomanianUnit(unit));
    }
}

public class RomanianAddressCountyTests
{
    [Theory]
    [InlineData("Gorj", "RO-GJ")]
    [InlineData("Cluj", "RO-CJ")]
    [InlineData("București", "RO-B")]
    [InlineData("Bucuresti", "RO-B")]
    [InlineData("Bucharest", "RO-B")]
    [InlineData("Timiș", "RO-TM")]
    [InlineData("Timis", "RO-TM")]
    [InlineData("Argeș", "RO-AG")]
    [InlineData("Bistrița-Năsăud", "RO-BN")]
    [InlineData("Bistrita-Nasaud", "RO-BN")]
    [InlineData("Caraș-Severin", "RO-CS")]
    [InlineData("Satu Mare", "RO-SM")]
    [InlineData("Satu-Mare", "RO-SM")]
    public void Maps_county_names_with_and_without_diacritics(string name, string expected)
    {
        Assert.Equal(expected, RomanianAddress.ToCountyCode(name));
    }

    [Theory]
    [InlineData("MUNICIPIUL BUCUREȘTI", "RO-B")]
    [InlineData("Județul Gorj", "RO-GJ")]
    [InlineData("JUDETUL CLUJ", "RO-CJ")]
    public void Strips_administrative_prefixes(string name, string expected)
    {
        Assert.Equal(expected, RomanianAddress.ToCountyCode(name));
    }

    [Theory]
    [InlineData("RO-GJ", "RO-GJ")]
    [InlineData("ro-gj", "RO-GJ")]
    public void Passes_through_values_already_in_iso_form(string input, string expected)
    {
        Assert.Equal(expected, RomanianAddress.ToCountyCode(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Nu Există")]
    public void Falls_back_to_bucharest_when_unresolvable(string? name)
    {
        // Lenient by design, and a known trap: a typo ships as a Bucharest address.
        Assert.Equal("RO-B", RomanianAddress.ToCountyCode(name));
    }

    [Fact]
    public void TryToCountyCode_distinguishes_a_real_match_from_the_fallback()
    {
        Assert.True(RomanianAddress.TryToCountyCode("Gorj", out var resolved));
        Assert.Equal("RO-GJ", resolved);

        Assert.False(RomanianAddress.TryToCountyCode("Nu Există", out var fallback));
        Assert.Equal("RO-B", fallback);

        // Bucharest itself must still report success, not look like a fallback.
        Assert.True(RomanianAddress.TryToCountyCode("București", out var bucharest));
        Assert.Equal("RO-B", bucharest);
    }

    [Fact]
    public void All_forty_two_counties_are_reachable()
    {
        string[] counties =
        [
            "Alba", "Arad", "Argeș", "Bacău", "Bihor", "Bistrița-Năsăud", "Botoșani", "Brăila",
            "Brașov", "București", "Buzău", "Călărași", "Caraș-Severin", "Cluj", "Constanța",
            "Covasna", "Dâmbovița", "Dolj", "Galați", "Giurgiu", "Gorj", "Harghita", "Hunedoara",
            "Ialomița", "Iași", "Ilfov", "Maramureș", "Mehedinți", "Mureș", "Neamț", "Olt",
            "Prahova", "Sălaj", "Satu Mare", "Sibiu", "Suceava", "Teleorman", "Timiș", "Tulcea",
            "Vâlcea", "Vaslui", "Vrancea",
        ];

        Assert.Equal(42, counties.Length);
        Assert.All(counties, county => Assert.True(
            RomanianAddress.TryToCountyCode(county, out _),
            $"'{county}' did not resolve."));

        // Every county must map to a distinct ISO code.
        var codes = counties.Select(RomanianAddress.ToCountyCode).ToList();
        Assert.Equal(42, codes.Distinct().Count());
    }
}

public class RomanianAddressCityTests
{
    [Theory]
    [InlineData("Sector 1 Mun. București", "SECTOR1")]
    [InlineData("Sector 2", "SECTOR2")]
    [InlineData("sector 3", "SECTOR3")]
    [InlineData("SECTOR 4", "SECTOR4")]
    [InlineData("Sector5", "SECTOR5")]
    [InlineData("Mun. București Sector 6", "SECTOR6")]
    public void Normalizes_bucharest_sectors_to_the_anaf_form(string city, string expected)
    {
        Assert.Equal(expected, RomanianAddress.NormalizeCityName(city));
    }

    [Theory]
    [InlineData("Mun. Târgu Jiu", "Târgu Jiu")]
    [InlineData("Municipiul Cluj-Napoca", "Cluj-Napoca")]
    [InlineData("Oraș Buftea", "Buftea")]
    [InlineData("Oras Buftea", "Buftea")]
    public void Strips_administrative_prefixes(string city, string expected)
    {
        Assert.Equal(expected, RomanianAddress.NormalizeCityName(city));
    }

    [Theory]
    [InlineData("Cluj-Napoca", "Cluj-Napoca")]
    [InlineData("Târgu Jiu", "Târgu Jiu")]
    [InlineData("Iași", "Iași")]
    public void Leaves_plain_city_names_untouched(string city, string expected)
    {
        Assert.Equal(expected, RomanianAddress.NormalizeCityName(city));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_empty_for_no_input(string? city)
    {
        Assert.Equal("", RomanianAddress.NormalizeCityName(city));
    }

    [Fact]
    public void Trims_surrounding_whitespace()
    {
        Assert.Equal("Cluj-Napoca", RomanianAddress.NormalizeCityName("  Cluj-Napoca  "));
    }
}

public class CuiNormalizerTests
{
    [Theory]
    [InlineData("12345674", "RO12345674")]
    [InlineData("RO12345674", "RO12345674")]
    [InlineData("ro12345674", "RO12345674")]
    [InlineData("  12345674  ", "RO12345674")]
    [InlineData("RO 12345674", "RO12345674")]
    [InlineData("12 345 674", "RO12345674")]
    public void Vat_identifier_is_upper_cased_stripped_and_prefixed(string input, string expected)
    {
        Assert.Equal(expected, CuiNormalizer.ToVatIdentifier(input));
    }

    [Theory]
    [InlineData("12345674", "12345674")]
    [InlineData("RO12345674", "12345674")]
    [InlineData("ro 12345674", "12345674")]
    [InlineData("  RO12345674  ", "12345674")]
    public void Bare_number_drops_the_prefix(string input, string expected)
    {
        Assert.Equal(expected, CuiNormalizer.ToBareNumber(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_yields_empty_output(string? input)
    {
        Assert.Equal("RO", CuiNormalizer.ToVatIdentifier(input));
        Assert.Equal("", CuiNormalizer.ToBareNumber(input));
    }
}
