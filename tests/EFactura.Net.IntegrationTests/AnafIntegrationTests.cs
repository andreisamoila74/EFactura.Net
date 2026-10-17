using EFactura.Net.Client;
using EFactura.Net.Ubl;

namespace EFactura.Net.IntegrationTests;

/// <summary>
/// Exercises ANAF's public validation endpoints against documents this library generates. These need
/// no credentials, so CI can run them with nothing configured beyond <c>ANAF_INTEGRATION=1</c>.
/// </summary>
/// <remarks>
/// This is the only layer that can answer "would ANAF actually accept this?". The XSD tests in
/// <c>EFactura.Net.Ubl.Tests</c> prove the document is structurally valid UBL; only ANAF's validator
/// checks the CIUS-RO business rules on top.
/// </remarks>
public class ValidationEndpointTests
{
    private static AnafValidationClient CreateClient() =>
        new(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

    [RequiresEnvironmentTheory(EnvironmentVariables.Enabled)]
    [InlineData("DomesticRon")]
    [InlineData("Minimal")]
    [InlineData("ReverseCharge")]
    [InlineData("Exempt")]
    [InlineData("MixedTaxCategories")]
    [InlineData("WithBankName")]
    public async Task Anaf_accepts_the_generated_invoice(string scenario)
    {
        var xml = UblWriter.Write(IntegrationDocuments.Invoice(scenario));

        var result = await CreateClient().ValidateAsync(
            xml,
            AnafValidationStandard.Invoice,
            TestContext.Current.CancellationToken);

        Assert.True(
            result.IsValid,
            $"ANAF rejected '{scenario}':{Environment.NewLine}"
            + string.Join(Environment.NewLine, result.Errors.Select(e => $"  - {e.Message}")));
    }

    [RequiresEnvironment(EnvironmentVariables.Enabled)]
    public async Task Anaf_accepts_the_generated_credit_note()
    {
        var xml = UblWriter.Write(IntegrationDocuments.CreditNote());

        var result = await CreateClient().ValidateAsync(
            xml,
            AnafValidationStandard.CreditNote,
            TestContext.Current.CancellationToken);

        Assert.True(
            result.IsValid,
            "ANAF rejected the credit note:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Errors.Select(e => $"  - {e.Message}")));
    }

    [RequiresEnvironment(EnvironmentVariables.Enabled)]
    public async Task Anaf_reports_errors_for_a_deliberately_invalid_document()
    {
        // No lines, so the totals cannot satisfy the EN 16931 arithmetic rules.
        var invoice = IntegrationDocuments.Invoice("Minimal") with { Lines = [] };

        var result = await CreateClient().ValidateAsync(
            UblWriter.Write(invoice),
            AnafValidationStandard.Invoice,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [RequiresEnvironment(EnvironmentVariables.Enabled)]
    public async Task Anaf_renders_the_invoice_as_pdf()
    {
        var xml = UblWriter.Write(IntegrationDocuments.Invoice("DomesticRon"));

        var result = await CreateClient().ConvertToPdfAsync(
            xml,
            AnafValidationStandard.Invoice,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.PdfContent);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfContent[..4]), StringComparison.Ordinal);
    }
}

/// <summary>
/// The ANAF public company registry. Unauthenticated, so it runs alongside the validation tests.
/// </summary>
public class CompanyLookupEndpointTests
{
    [RequiresEnvironment(EnvironmentVariables.Enabled)]
    public async Task Looks_up_a_well_known_public_institution()
    {
        // ANAF's own CIF (4221306) — a public body, so no private company data is involved.
        var client = new AnafCompanyLookupClient(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

        var company = await client.GetByCuiAsync(
            "4221306",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(company);
        Assert.Equal("4221306", company.Cui);
        Assert.False(string.IsNullOrWhiteSpace(company.Name));
    }

    [RequiresEnvironment(EnvironmentVariables.Enabled)]
    public async Task An_unassigned_identifier_returns_null()
    {
        var client = new AnafCompanyLookupClient(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

        var company = await client.GetByCuiAsync(
            "99999999",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(company);
    }
}

/// <summary>
/// The full submission round trip against the ANAF <em>test</em> environment. Needs a real OAuth
/// token, so it is gated separately and never runs from a fork.
/// </summary>
public class SubmissionRoundTripTests
{
    private sealed class EnvironmentTokenProvider : IAnafAccessTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(EnvironmentVariables.Require(EnvironmentVariables.TestAccessToken));
    }

    [RequiresEnvironment(
        EnvironmentVariables.Enabled,
        EnvironmentVariables.TestAccessToken,
        EnvironmentVariables.TestCif)]
    public async Task Uploads_then_polls_until_anaf_reaches_a_verdict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cif = EnvironmentVariables.Require(EnvironmentVariables.TestCif);

        var handler = new HttpClientHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = AnafEFacturaOptions.TestBaseAddress };

        var client = new AnafEFacturaClient(
            WrapWithToken(httpClient),
            new AnafEFacturaOptions { Environment = AnafEnvironment.Test });

        var invoice = IntegrationDocuments.Invoice("DomesticRon") with
        {
            Seller = IntegrationDocuments.Invoice("DomesticRon").Seller with { VatIdentifier = cif },
            // A unique id per run, so repeated runs do not collide on ANAF's side.
            DocumentId = $"IT nr. {DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
        };

        var upload = await client.UploadAsync(
            cif,
            UblWriter.Write(invoice),
            AnafUploadStandard.Ubl,
            cancellationToken: cancellationToken);

        Assert.True(
            upload.IsSuccess,
            "Upload refused: " + string.Join("; ", upload.Errors.Select(e => e.Message)));
        Assert.NotNull(upload.UploadIndex);

        // The documented backoff, bounded so a stuck submission cannot hang the suite.
        AnafMessageStatus? status = null;
        foreach (var delay in PollingDelays().Take(20))
        {
            await Task.Delay(delay, cancellationToken);

            status = await client.GetStatusAsync(upload.UploadIndex!, cancellationToken);

            if (status.IsFinal)
            {
                break;
            }
        }

        Assert.NotNull(status);
        Assert.True(
            status.IsFinal,
            $"ANAF had not finished processing after the polling budget; last state '{status.RawState}'.");

        if (status.State == AnafMessageState.Ok)
        {
            var download = await client.DownloadAsync(status.DownloadId!, cancellationToken);

            Assert.True(download.IsSuccess);
            Assert.NotNull(download.InvoiceXml);
            Assert.NotNull(download.SignatureXml);
        }
    }

    [RequiresEnvironment(
        EnvironmentVariables.Enabled,
        EnvironmentVariables.TestAccessToken,
        EnvironmentVariables.TestCif)]
    public async Task Lists_recent_messages()
    {
        using var httpClient = new HttpClient { BaseAddress = AnafEFacturaOptions.TestBaseAddress };

        var client = new AnafEFacturaClient(
            WrapWithToken(httpClient),
            new AnafEFacturaOptions { Environment = AnafEnvironment.Test });

        var result = await client.ListMessagesAsync(
            EnvironmentVariables.Require(EnvironmentVariables.TestCif),
            days: 7,
            cancellationToken: TestContext.Current.CancellationToken);

        // Either messages or ANAF's "no messages" note — both are successful answers.
        Assert.True(result.Messages.Count > 0 || !string.IsNullOrWhiteSpace(result.Error));
    }

    private static HttpClient WrapWithToken(HttpClient template)
    {
        var handler = new Client.Internal.AnafAccessTokenHandler(new EnvironmentTokenProvider())
        {
            InnerHandler = new HttpClientHandler(),
        };

        return new HttpClient(handler)
        {
            BaseAddress = template.BaseAddress,
            Timeout = TimeSpan.FromSeconds(60),
        };
    }

    /// <summary>The backoff ladder documented in <c>docs/polling.md</c>.</summary>
    private static IEnumerable<TimeSpan> PollingDelays()
    {
        for (var attempt = 0; ; attempt++)
        {
            yield return attempt switch
            {
                < 3 => TimeSpan.FromSeconds(5),
                < 6 => TimeSpan.FromSeconds(10),
                < 10 => TimeSpan.FromSeconds(20),
                _ => TimeSpan.FromSeconds(30),
            };
        }
    }
}
