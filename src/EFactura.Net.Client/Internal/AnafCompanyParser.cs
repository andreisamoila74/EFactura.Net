using System.Globalization;
using System.Text.Json;

namespace EFactura.Net.Client.Internal;

/// <summary>
/// Projects ANAF's nested, Romanian-named registry JSON onto <see cref="AnafCompanyInfo"/>.
/// Internal: the wire shape is ANAF's and changes with their API version.
/// </summary>
internal static class AnafCompanyParser
{
    internal static IReadOnlyList<AnafCompanyInfo> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("found", out var found)
            || found.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<AnafCompanyInfo>();

        foreach (var entry in found.EnumerateArray())
        {
            // Entries without general data are placeholders for identifiers ANAF could not resolve.
            if (!entry.TryGetProperty("date_generale", out var general)
                || general.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var vat = Section(entry, "inregistrare_scop_Tva");
            var cashVat = Section(entry, "inregistrare_RTVAI");
            var inactive = Section(entry, "stare_inactiv");
            var splitVat = Section(entry, "inregistrare_SplitTVA");

            results.Add(new AnafCompanyInfo
            {
                Cui = Number(general, "cui"),
                QueryDate = Date(general, "data"),
                Name = Text(general, "denumire"),
                Address = Text(general, "adresa"),
                RegistrationNumber = Text(general, "nrRegCom"),
                Phone = Text(general, "telefon"),
                Fax = Text(general, "fax"),
                PostalCode = Text(general, "codPostal"),
                LegalDocument = Text(general, "act"),
                RegistrationStatus = Text(general, "stare_inregistrare"),
                RegistrationDate = Date(general, "data_inregistrare"),
                CaenCode = Text(general, "cod_CAEN"),
                Iban = Text(general, "iban"),
                IsEFacturaActive = Flag(general, "statusRO_e_Factura"),
                TaxAuthority = Text(general, "organFiscalCompetent"),
                OwnershipForm = Text(general, "forma_de_proprietate"),
                OrganizationForm = Text(general, "forma_organizare"),
                LegalForm = Text(general, "forma_juridica"),

                IsVatPayer = Flag(vat, "scpTVA"),
                VatStartDate = Date(vat, "data_inceput_ScpTVA"),
                VatEndDate = Date(vat, "data_sfarsit_ScpTVA"),
                VatAnnulmentDate = Date(vat, "data_anul_imp_ScpTVA"),
                VatMessage = Text(vat, "mesaj_ScpTVA"),

                IsCashVatPayer = Flag(cashVat, "statusTvaIncasare"),
                CashVatStartDate = Date(cashVat, "dataInceputTvaInc"),
                CashVatEndDate = Date(cashVat, "dataSfarsitTvaInc"),
                CashVatUpdateDate = Date(cashVat, "dataActualizareTvaInc"),
                CashVatPublishDate = Date(cashVat, "dataPublicareTvaInc"),
                CashVatDocumentType = Text(cashVat, "tipActTvaInc"),

                IsInactive = Flag(inactive, "statusInactivi"),
                InactivationDate = Date(inactive, "dataInactivare"),
                ReactivationDate = Date(inactive, "dataReactivare"),
                InactivePublishDate = Date(inactive, "dataPublicare"),
                DeregistrationDate = Date(inactive, "dataRadiere"),

                IsSplitVatPayer = Flag(splitVat, "statusSplitTVA"),
                SplitVatStartDate = Date(splitVat, "dataInceputSplitTVA"),
                SplitVatCancelDate = Date(splitVat, "dataAnulareSplitTVA"),

                RegisteredAddress = Address(Section(entry, "adresa_sediu_social"), "s"),
                TaxDomicileAddress = Address(Section(entry, "adresa_domiciliu_fiscal"), "d"),
            });
        }

        return results;
    }

    /// <summary>
    /// The two address blocks carry identical fields distinguished only by an <c>s</c> or <c>d</c>
    /// prefix (<c>sdenumire_Strada</c> / <c>ddenumire_Strada</c>), so one reader serves both.
    /// </summary>
    private static AnafAddress? Address(JsonElement element, string prefix)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AnafAddress
        {
            Street = Text(element, $"{prefix}denumire_Strada"),
            StreetNumber = Text(element, $"{prefix}numar_Strada"),
            City = Text(element, $"{prefix}denumire_Localitate"),
            CityCode = Text(element, $"{prefix}cod_Localitate"),
            County = Text(element, $"{prefix}denumire_Judet"),
            CountyCode = Text(element, $"{prefix}cod_Judet"),
            CountyAutoCode = Text(element, $"{prefix}cod_JudetAuto"),
            Country = Text(element, $"{prefix}tara"),
            Details = Text(element, $"{prefix}detalii_Adresa"),
            PostalCode = Text(element, $"{prefix}cod_Postal"),
        };
    }

    private static JsonElement Section(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var section) ? section : default;

    private static string? Text(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString(),
            _ => null,
        };
    }

    private static bool Flag(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false,
        };
    }

    private static DateOnly? Date(JsonElement element, string name)
    {
        var text = Text(element, name);

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
