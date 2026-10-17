using EFactura.Net.Client;
using EFactura.Net.Samples.Console.Snippets;
using EFactura.Net.Ubl;
using Microsoft.Extensions.DependencyInjection;

// Runnable walkthrough. Everything up to "live" works offline; the live steps are skipped unless
// the relevant environment variables are set, so `dotnet run` is always safe.
//
//   ANAF_INTEGRATION=1          enable the unauthenticated calls (validate, PDF, company lookup)
//   ANAF_ACCESS_TOKEN=...       enable upload / status / download / message lists
//   ANAF_CIF=...                the CIF that token is authorised for
//
// Usage: dotnet run --project samples/EFactura.Net.Samples.Console [section]

var section = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
var cancellationToken = CancellationToken.None;

if (section is "all" or "build")
{
    Heading("Building and serializing");

    var invoice = BuildingDocuments.TypicalInvoice();
    var (xml, totals) = BuildingDocuments.SerializeWithTotals(invoice);

    Console.WriteLine($"  lines    {totals.LineExtensionAmount,10:N2} RON");
    Console.WriteLine($"  VAT      {totals.TaxAmount,10:N2} RON");
    Console.WriteLine($"  payable  {totals.PayableAmount,10:N2} RON");
    Console.WriteLine($"  XML is {xml.Length} characters, {totals.TaxSubtotals.Count} VAT subtotal(s)");

    Console.WriteLine();
    Console.WriteLine("  Reverse charge:");
    PrintCategories(BuildingDocuments.ReverseChargeInvoice());

    Console.WriteLine("  Foreign currency (EUR, VAT in RON):");
    var foreignTotals = UblTotals.Calculate(BuildingDocuments.ForeignCurrencyInvoice());
    Console.WriteLine($"    VAT {foreignTotals.TaxAmount:N2} EUR, restated in RON in a second TaxTotal");

    Console.WriteLine("  Bucharest address normalisation:");
    Console.WriteLine($"    'Sector 1 Mun. București' -> '{RomanianAddress.NormalizeCityName("Sector 1 Mun. București")}'");
    Console.WriteLine($"    'București'               -> '{RomanianAddress.ToCountyCode("București")}'");

    Console.WriteLine("  Credit note:");
    var creditNote = BuildingDocuments.CreditNote();
    Console.WriteLine($"    {creditNote.DocumentId} reverses {creditNote.PrecedingInvoice.DocumentId}");
}

if (section is "all" or "live")
{
    Heading("Talking to ANAF");

    if (Environment.GetEnvironmentVariable("ANAF_INTEGRATION") is null or "")
    {
        Console.WriteLine("  skipped — set ANAF_INTEGRATION=1 to run the unauthenticated calls.");
    }
    else
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var validation = new AnafValidationClient(httpClient);

        Console.WriteLine("  Validating the invoice with ANAF:");
        var valid = await UsingTheApi.ValidateFirstAsync(
            validation,
            BuildingDocuments.TypicalInvoice(),
            cancellationToken);
        Console.WriteLine($"    valid: {valid}");

        Console.WriteLine("  Rendering it as a PDF:");
        var pdf = await UsingTheApi.RenderPdfAsync(
            validation,
            BuildingDocuments.TypicalInvoice(),
            cancellationToken);
        Console.WriteLine($"    {(pdf is null ? "conversion refused" : $"{pdf.Length} bytes")}");

        Console.WriteLine("  Looking up a company (ANAF's own CIF):");
        using var lookupClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        await UsingTheApi.IsEnrolledInEFacturaAsync(
            new AnafCompanyLookupClient(lookupClient),
            "4221306",
            cancellationToken);
    }
}

if (section is "all" or "submit")
{
    Heading("Submitting (ANAF test environment)");

    var token = Environment.GetEnvironmentVariable("ANAF_ACCESS_TOKEN");
    var cif = Environment.GetEnvironmentVariable("ANAF_CIF");

    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(cif))
    {
        Console.WriteLine("  skipped — set ANAF_ACCESS_TOKEN and ANAF_CIF to run the full round trip.");
    }
    else
    {
        var services = Configuration.WithDependencyInjection();
        var client = services.GetRequiredService<IAnafEFacturaClient>();

        var invoice = BuildingDocuments.TypicalInvoice() with
        {
            DocumentId = $"SAMPLE nr. {DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Seller = BuildingDocuments.TypicalInvoice().Seller with { VatIdentifier = cif },
        };

        await UsingTheApi.SubmitAsync(client, cif, invoice, cancellationToken);

        Console.WriteLine("  Recent inbound messages:");
        await UsingTheApi.ListInboundAsync(client, cif, cancellationToken);
    }
}

Console.WriteLine();
Console.WriteLine("Done. See docs/ for the full walkthrough.");

static void Heading(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} {new string('=', Math.Max(0, 60 - title.Length))}");
}

static void PrintCategories(UblDocument document)
{
    foreach (var subtotal in UblTotals.Calculate(document).TaxSubtotals)
    {
        Console.WriteLine($"    {subtotal.Category,-14} {subtotal.Percent,5:N2}%  base {subtotal.TaxableAmount,10:N2}  VAT {subtotal.TaxAmount,8:N2}");
    }
}
