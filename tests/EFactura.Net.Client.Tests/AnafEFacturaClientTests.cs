using System.Net;
using RichardSzalay.MockHttp;

namespace EFactura.Net.Client.Tests;

public class UploadTests
{
    [Fact]
    public async Task Posts_to_upload_with_standard_and_cif()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://api.anaf.ro/test/FCTEL/rest/upload")
            .WithExactQueryString("standard=UBL&cif=12345674")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.UploadAsync("12345674", "<Invoice/>", AnafUploadStandard.Ubl);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Strips_the_ro_prefix_from_the_cif()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://api.anaf.ro/test/FCTEL/rest/upload")
            .WithExactQueryString("standard=UBL&cif=12345674")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.UploadAsync("RO 12345674", "<Invoice/>", AnafUploadStandard.Ubl);

        mock.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(AnafUploadStandard.Ubl, "UBL")]
    [InlineData(AnafUploadStandard.CreditNote, "CN")]
    [InlineData(AnafUploadStandard.Cii, "CII")]
    [InlineData(AnafUploadStandard.Rasp, "RASP")]
    public async Task Sends_the_right_standard_token(AnafUploadStandard standard, string expected)
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://api.anaf.ro/test/FCTEL/rest/upload")
            .WithExactQueryString($"standard={expected}&cif=12345674")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.UploadAsync("12345674", "<Invoice/>", standard);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Sends_the_document_as_the_request_body()
    {
        const string Xml = "<Invoice><cbc:ID>FCT nr. 1</cbc:ID></Invoice>";

        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://api.anaf.ro/test/FCTEL/rest/upload")
            .With(request => request.Content!.ReadAsStringAsync().Result == Xml)
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.UploadAsync("12345674", Xml, AnafUploadStandard.Ubl);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Sends_the_body_as_text_plain()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://api.anaf.ro/test/FCTEL/rest/upload")
            .With(request => request.Content!.Headers.ContentType!.MediaType == "text/plain")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.UploadAsync("12345674", "<Invoice/>", AnafUploadStandard.Ubl);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Appends_the_optional_upload_flags()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://api.anaf.ro/test/FCTEL/rest/upload")
            .WithExactQueryString("standard=UBL&cif=12345674&extern=DA&autofactura=DA&executare=DA")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.UploadAsync(
            "12345674",
            "<Invoice/>",
            AnafUploadStandard.Ubl,
            new AnafUploadOptions { ForeignBuyer = true, SelfBilled = true, Enforcement = true });

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Parses_an_accepted_upload()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.UploadAsync("12345674", "<Invoice/>", AnafUploadStandard.Ubl);

        Assert.True(result.IsSuccess);
        Assert.Equal("4455667788", result.UploadIndex);
        Assert.Equal(0, result.ExecutionStatus);
        Assert.Empty(result.Errors);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 12, 30, 0, TimeSpan.Zero), result.ResponseDate);
        Assert.NotEmpty(result.RawResponse);
    }

    /// <summary>
    /// A rejection is an outcome, not an exception — callers branch on it constantly.
    /// </summary>
    [Fact]
    public async Task Reports_a_rejected_upload_as_data()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("upload-rejected.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.UploadAsync("12345674", "<Invoice/>", AnafUploadStandard.Ubl);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UploadIndex);
        Assert.Equal(1, result.ExecutionStatus);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Message.Contains("AccountingSupplierParty", StringComparison.Ordinal));
    }

    /// <summary>
    /// The original read only element text here while ANAF uses the errorMessage attribute, so
    /// rejections surfaced as a raw XML dump. Both forms must now be understood.
    /// </summary>
    [Fact]
    public async Task Reads_errors_from_element_text_as_well_as_the_attribute()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
            Task.FromResult(TestHarness.Xml(Fixture.Read("upload-rejected-text-errors.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.UploadAsync("12345674", "<Invoice/>", AnafUploadStandard.Ubl);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Eroare la validarea structurii XML", error.Message);
    }

    [Fact]
    public async Task Rejects_an_empty_cif_or_document()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateEFacturaClient(mock);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.UploadAsync("", "<Invoice/>", AnafUploadStandard.Ubl));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.UploadAsync("12345674", "  ", AnafUploadStandard.Ubl));
    }
}

public class StatusTests
{
    [Fact]
    public async Task Gets_status_by_upload_index()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.anaf.ro/test/FCTEL/rest/stareMesaj")
            .WithExactQueryString("id_incarcare=4455667788")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("stare-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.GetStatusAsync("4455667788");

        mock.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("stare-ok.xml", AnafMessageState.Ok, true)]
    [InlineData("stare-nok.xml", AnafMessageState.Rejected, true)]
    [InlineData("stare-in-prelucrare.xml", AnafMessageState.Processing, false)]
    [InlineData("stare-xml-cu-erori.xml", AnafMessageState.InvalidXml, true)]
    public async Task Maps_every_anaf_state(string fixture, AnafMessageState expected, bool isFinal)
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read(fixture))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var status = await client.GetStatusAsync("4455667788");

        Assert.Equal(expected, status.State);
        Assert.Equal(isFinal, status.IsFinal);
    }

    [Fact]
    public async Task Preserves_the_raw_state_string()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("stare-xml-cu-erori.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var status = await client.GetStatusAsync("4455667788");

        Assert.Equal("XML cu erori nepreluat de sistem", status.RawState);
    }

    [Fact]
    public async Task Unknown_state_is_reported_rather_than_guessed()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(
            "<header xmlns=\"mfp:anaf:dgti:spv:stareMesajFactura:v1\" stare=\"ceva nou\"/>")));

        var client = TestHarness.CreateEFacturaClient(mock);

        var status = await client.GetStatusAsync("4455667788");

        // The original switch treated anything unrecognised as "still processing" and kept polling.
        Assert.Equal(AnafMessageState.Unknown, status.State);
        Assert.Equal("ceva nou", status.RawState);
    }

    [Fact]
    public async Task Exposes_the_download_id_once_processing_finishes()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("stare-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var status = await client.GetStatusAsync("4455667788");

        Assert.Equal("9988776655", status.DownloadId);
    }

    /// <summary>
    /// A fault on ANAF's side says nothing about the document, so the state stays Processing and the
    /// caller decides what to do — the original treated this as terminal.
    /// </summary>
    [Fact]
    public async Task Flags_a_technical_error_without_condemning_the_document()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("stare-technical-error.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var status = await client.GetStatusAsync("4455667788");

        Assert.True(status.IsTechnicalError);
        Assert.Equal(AnafMessageState.Processing, status.State);
        Assert.False(status.IsFinal);
        Assert.Contains(status.Errors, e => e.Message.Contains("Cod: 5001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Errors_without_a_state_are_treated_as_a_rejection()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
            Task.FromResult(TestHarness.Xml(Fixture.Read("stare-errors-without-state.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var status = await client.GetStatusAsync("4455667788");

        Assert.Equal(AnafMessageState.Rejected, status.State);
        Assert.Single(status.Errors);
    }

    [Fact]
    public async Task Malformed_xml_throws_with_the_body_attached()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml("not xml at all")));

        var client = TestHarness.CreateEFacturaClient(mock);

        var exception = await Assert.ThrowsAsync<AnafException>(() => client.GetStatusAsync("4455667788"));

        Assert.Equal("not xml at all", exception.RawResponse);
    }
}

public class DownloadTests
{
    private const string InvoiceXml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";
    private const string SignatureXml = "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"/>";

    [Fact]
    public async Task Downloads_by_id()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.anaf.ro/test/FCTEL/rest/descarcare")
            .WithExactQueryString("id=9988776655")
            .Respond(() => Task.FromResult(TestHarness.Binary(TestHarness.SignedZip(InvoiceXml, SignatureXml))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.DownloadAsync("9988776655");

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Unpacks_the_document_and_the_signature()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
            Task.FromResult(TestHarness.Binary(TestHarness.SignedZip(InvoiceXml, SignatureXml))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.DownloadAsync("9988776655");

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceXml, result.InvoiceXml);
        Assert.Equal(SignatureXml, result.SignatureXml);
        Assert.NotNull(result.ZipContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Keeps_the_original_zip_for_archiving()
    {
        var zip = TestHarness.SignedZip(InvoiceXml, SignatureXml);

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Binary(zip)));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.DownloadAsync("9988776655");

        Assert.Equal(zip, result.ZipContent);
    }

    [Fact]
    public async Task Extracts_errors_from_a_rejection_zip()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Binary(
            TestHarness.ErrorZip("Valoarea totala nu corespunde sumei liniilor."))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.DownloadAsync("9988776656");

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Valoarea totala", error.Message, StringComparison.Ordinal);
    }

    /// <summary>ANAF answers errors with XML and success with a ZIP, both under HTTP 200.</summary>
    [Fact]
    public async Task Detects_an_xml_error_response_instead_of_a_zip()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("descarcare-error.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.DownloadAsync("9988776655");

        Assert.False(result.IsSuccess);
        Assert.Null(result.ZipContent);
        Assert.Contains(result.Errors, e => e.Message.Contains("Nu aveti dreptul", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sniffs_the_body_when_the_content_type_is_missing()
    {
        // ANAF does not always set a content type; the body must be the deciding evidence.
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(
            TestHarness.Binary(System.Text.Encoding.UTF8.GetBytes(Fixture.Read("descarcare-error.xml")),
            "application/octet-stream")));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.DownloadAsync("9988776655");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task A_corrupt_archive_throws()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Binary([0x50, 0x4B, 0x03, 0x04, 0x00, 0xFF])));

        var client = TestHarness.CreateEFacturaClient(mock);

        await Assert.ThrowsAsync<AnafException>(() => client.DownloadAsync("9988776655"));
    }
}

public class AuthenticationTests
{
    [Fact]
    public async Task Attaches_the_bearer_token_to_every_request()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect("*")
            .WithHeaders("Authorization", "Bearer test-token")
            .Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("stare-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.GetStatusAsync("4455667788");

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Asks_the_provider_once_per_request()
    {
        var provider = new StubTokenProvider();

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml(Fixture.Read("stare-ok.xml"))));

        var client = TestHarness.CreateEFacturaClient(mock, provider);

        await client.GetStatusAsync("1");
        await client.GetStatusAsync("2");

        // Read per request, so a refreshed token takes effect immediately.
        Assert.Equal(2, provider.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Auth_failures_throw_a_dedicated_exception(HttpStatusCode status)
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml("<error/>", status)));

        var client = TestHarness.CreateEFacturaClient(mock);

        var exception = await Assert.ThrowsAsync<AnafAuthenticationException>(
            () => client.GetStatusAsync("4455667788"));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("<error/>", exception.RawResponse);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Other_http_failures_throw_AnafHttpException(HttpStatusCode status)
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Xml("boom", status)));

        var client = TestHarness.CreateEFacturaClient(mock);

        var exception = await Assert.ThrowsAsync<AnafHttpException>(() => client.GetStatusAsync("4455667788"));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("boom", exception.RawResponse);
    }
}

public class EnvironmentTests
{
    [Fact]
    public void Defaults_to_the_test_environment()
    {
        // An SDK that silently points at the live tax authority is the wrong default.
        var options = new AnafEFacturaOptions();

        Assert.Equal(AnafEnvironment.Test, options.Environment);
        Assert.Equal(new Uri("https://api.anaf.ro/test/FCTEL/rest/"), options.ResolveBaseAddress());
    }

    [Fact]
    public void Production_resolves_to_the_prod_base_address()
    {
        var options = new AnafEFacturaOptions { Environment = AnafEnvironment.Production };

        Assert.Equal(new Uri("https://api.anaf.ro/prod/FCTEL/rest/"), options.ResolveBaseAddress());
    }

    [Fact]
    public void An_explicit_base_address_wins()
    {
        var custom = new Uri("https://localhost:5001/fake/");
        var options = new AnafEFacturaOptions { Environment = AnafEnvironment.Production, BaseAddress = custom };

        Assert.Equal(custom, options.ResolveBaseAddress());
    }
}

public class CancellationTests
{
    [Fact]
    public async Task Upload_honours_cancellation()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            return TestHarness.Xml("<header/>");
        });

        var client = TestHarness.CreateEFacturaClient(mock);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.UploadAsync("12345674", "<Invoice/>", AnafUploadStandard.Ubl, null, cts.Token));
    }

    [Fact]
    public async Task Status_honours_cancellation()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            return TestHarness.Xml("<header/>");
        });

        var client = TestHarness.CreateEFacturaClient(mock);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetStatusAsync("1", cts.Token));
    }
}
