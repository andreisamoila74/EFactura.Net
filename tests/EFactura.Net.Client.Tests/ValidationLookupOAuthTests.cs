using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using RichardSzalay.MockHttp;

namespace EFactura.Net.Client.Tests;

public class ValidationClientTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\nfake pdf body\n%%EOF");

    [Theory]
    [InlineData(AnafValidationStandard.Invoice, "FACT1")]
    [InlineData(AnafValidationStandard.CreditNote, "FCN")]
    public async Task Posts_to_the_right_validation_path(AnafValidationStandard standard, string segment)
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, $"https://webservicesp.anaf.ro/prod/FCTEL/rest/validare/{segment}")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("validare-ok.json"))));

        var client = TestHarness.CreateValidationClient(mock);

        await client.ValidateAsync("<Invoice/>", standard);

        mock.VerifyNoOutstandingExpectation();
    }

    /// <summary>These endpoints are unauthenticated, which is what makes them usable from CI.</summary>
    [Fact]
    public async Task Sends_no_authorization_header()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect("*")
            .With(request => request.Headers.Authorization is null)
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("validare-ok.json"))));

        var client = TestHarness.CreateValidationClient(mock);

        await client.ValidateAsync("<Invoice/>", AnafValidationStandard.Invoice);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Reports_a_valid_document()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("validare-ok.json"))));

        var client = TestHarness.CreateValidationClient(mock);

        var result = await client.ValidateAsync("<Invoice/>", AnafValidationStandard.Invoice);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Reports_validation_failures_as_data()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("validare-nok.json"))));

        var client = TestHarness.CreateValidationClient(mock);

        var result = await client.ValidateAsync("<Invoice/>", AnafValidationStandard.Invoice);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Message.Contains("BR-CO-10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_non_json_body_is_surfaced_rather_than_swallowed()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Serviciu indisponibil") }));

        var client = TestHarness.CreateValidationClient(mock);

        var result = await client.ValidateAsync("<Invoice/>", AnafValidationStandard.Invoice);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("indisponibil", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Converts_to_pdf()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://webservicesp.anaf.ro/prod/FCTEL/rest/transformare/FACT1")
            .Respond(() => Task.FromResult(TestHarness.Binary(PdfBytes, "application/pdf")));

        var client = TestHarness.CreateValidationClient(mock);

        var result = await client.ConvertToPdfAsync("<Invoice/>", AnafValidationStandard.Invoice);

        Assert.True(result.IsSuccess);
        Assert.Equal(PdfBytes, result.PdfContent);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Appends_the_da_segment_to_skip_validation()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://webservicesp.anaf.ro/prod/FCTEL/rest/transformare/FACT1/DA")
            .Respond(() => Task.FromResult(TestHarness.Binary(PdfBytes, "application/pdf")));

        var client = TestHarness.CreateValidationClient(mock);

        await client.ConvertToPdfAsync("<Invoice/>", AnafValidationStandard.Invoice, validate: false);

        mock.VerifyNoOutstandingExpectation();
    }

    /// <summary>A failed conversion returns JSON under HTTP 200, so the body decides.</summary>
    [Fact]
    public async Task Reports_a_failed_conversion_as_data()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("validare-nok.json"))));

        var client = TestHarness.CreateValidationClient(mock);

        var result = await client.ConvertToPdfAsync("<Invoice/>", AnafValidationStandard.Invoice);

        Assert.False(result.IsSuccess);
        Assert.Null(result.PdfContent);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Rejects_an_empty_document()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateValidationClient(mock);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.ValidateAsync("  ", AnafValidationStandard.Invoice));
    }
}

public class CompanyLookupTests
{
    private static FakeTimeProvider At(int year, int month, int day) =>
        new(new DateTimeOffset(year, month, day, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Posts_the_cui_and_date_as_a_json_array()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva")
            .WithContent("[{\"cui\":12345678,\"data\":\"2026-01-15\"}]")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        await client.GetByCuiAsync("12345678");

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Defaults_the_date_to_today_from_the_time_provider()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect("*")
            .WithContent("[{\"cui\":12345678,\"data\":\"2026-06-30\"}]")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 6, 30));

        await client.GetByCuiAsync("12345678");

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task An_explicit_date_overrides_today()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect("*")
            .WithContent("[{\"cui\":12345678,\"data\":\"2024-03-01\"}]")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 6, 30));

        await client.GetByCuiAsync("12345678", new DateOnly(2024, 3, 1));

        mock.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("RO12345678")]
    [InlineData("  ro 12345678 ")]
    public async Task Accepts_the_cui_in_any_common_form(string cui)
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect("*")
            .WithContent("[{\"cui\":12345678,\"data\":\"2026-01-15\"}]")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        await client.GetByCuiAsync(cui);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Projects_the_registry_payload()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        var company = await client.GetByCuiAsync("12345678");

        Assert.NotNull(company);
        Assert.Equal("12345678", company.Cui);
        Assert.Equal("FURNIZOR TEST SRL", company.Name);
        Assert.Equal("J40/1234/2020", company.RegistrationNumber);
        Assert.Equal("6201", company.CaenCode);
        Assert.True(company.IsVatPayer);
        Assert.True(company.IsEFacturaActive);
        Assert.False(company.IsInactive);
        Assert.False(company.IsSplitVatPayer);
        Assert.False(company.IsCashVatPayer);
        Assert.Equal(new DateOnly(2020, 2, 1), company.RegistrationDate);
        Assert.Equal(new DateOnly(2020, 3, 1), company.VatStartDate);
    }

    [Fact]
    public async Task Projects_both_addresses()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        var company = await client.GetByCuiAsync("12345678");

        Assert.Equal("Str. Exemplu", company!.RegisteredAddress?.Street);
        Assert.Equal("GORJ", company.RegisteredAddress?.County);
        Assert.Equal("GJ", company.RegisteredAddress?.CountyAutoCode);
        Assert.Equal("Str. Exemplu", company.TaxDomicileAddress?.Street);
        Assert.Equal("Romania", company.TaxDomicileAddress?.Country);
    }

    [Fact]
    public async Task Empty_strings_become_null_rather_than_empty_values()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        var company = await client.GetByCuiAsync("12345678");

        Assert.Null(company!.Iban);
        Assert.Null(company.Fax);
        Assert.Null(company.VatEndDate);
    }

    [Fact]
    public async Task An_unknown_cui_returns_null()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
            Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup-notfound.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        Assert.Null(await client.GetByCuiAsync("99999999"));
    }

    [Fact]
    public async Task Batches_several_identifiers_into_one_request()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect("*")
            .WithContent("[{\"cui\":12345678,\"data\":\"2026-01-15\"},{\"cui\":87654321,\"data\":\"2026-01-15\"}]")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json"))));

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        await client.GetManyByCuiAsync(["12345678", "RO87654321"]);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Splits_inputs_larger_than_the_batch_size()
    {
        var requests = 0;

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
        {
            requests++;
            return Task.FromResult(TestHarness.Json(Fixture.Read("company-lookup.json")));
        });

        var client = TestHarness.CreateLookupClient(
            mock,
            At(2026, 1, 15),
            new AnafCompanyLookupOptions { BatchSize = 2 });

        await client.GetManyByCuiAsync(["1", "2", "3", "4", "5"]);

        Assert.Equal(3, requests);
    }

    [Fact]
    public async Task An_empty_input_makes_no_request()
    {
        var requests = 0;

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
        {
            requests++;
            return Task.FromResult(TestHarness.Json("{}"));
        });

        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        Assert.Empty(await client.GetManyByCuiAsync([]));
        Assert.Equal(0, requests);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("RO-ABC")]
    public async Task Rejects_a_non_numeric_identifier(string cui)
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateLookupClient(mock, At(2026, 1, 15));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetByCuiAsync(cui));
    }
}

public class OAuthClientTests
{
    [Fact]
    public void Builds_the_authorization_url()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        var uri = client.BuildAuthorizationUri("opaque-state");
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal("https://logincert.anaf.ro/anaf-oauth2/v1/authorize", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("https://example.test/auth/anaf/callback", query["redirect_uri"]);
        Assert.Equal("jwt", query["token_content_type"]);
        Assert.Equal("opaque-state", query["state"]);
    }

    [Fact]
    public void Escapes_the_state_value()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        // Data-protected state values routinely contain characters that need escaping.
        var uri = client.BuildAuthorizationUri("a+b/c=d&e");
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal("a+b/c=d&e", query["state"]);
    }

    [Fact]
    public void Requires_a_state_value()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        Assert.Throws<ArgumentException>(() => client.BuildAuthorizationUri(""));
    }

    [Fact]
    public void Fails_clearly_when_credentials_are_unconfigured()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateOAuthClient(mock, new AnafOAuthOptions());

        var exception = Assert.Throws<InvalidOperationException>(() => client.BuildAuthorizationUri("s"));

        Assert.Contains("ClientId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exchanges_a_code_using_basic_auth()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://logincert.anaf.ro/anaf-oauth2/v1/token")
            .WithHeaders("Authorization", $"Basic {Convert.ToBase64String(
                Encoding.UTF8.GetBytes("test-client-id:test-client-secret"))}")
            .WithFormData("grant_type", "authorization_code")
            .WithFormData("code", "the-code")
            .WithFormData("token_content_type", "jwt")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("token-response.json"))));

        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        var token = await client.ExchangeCodeAsync("the-code");

        Assert.Equal("test-access-token-value", token.AccessToken);
        Assert.Equal("test-refresh-token-value", token.RefreshToken);
        Assert.Equal(3600, token.ExpiresIn);
        Assert.Equal("Bearer", token.TokenType);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Refreshes_a_token()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://logincert.anaf.ro/anaf-oauth2/v1/token")
            .WithFormData("grant_type", "refresh_token")
            .WithFormData("refresh_token", "old-refresh")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("token-response.json"))));

        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        var token = await client.RefreshTokenAsync("old-refresh");

        Assert.Equal("test-access-token-value", token.AccessToken);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task A_rejected_refresh_token_raises_an_auth_exception()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(
            TestHarness.Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.Unauthorized)));

        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        await Assert.ThrowsAsync<AnafAuthenticationException>(() => client.RefreshTokenAsync("stale"));
    }

    [Fact]
    public async Task A_response_without_an_access_token_is_an_error()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json("{\"token_type\":\"Bearer\"}")));

        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        var exception = await Assert.ThrowsAsync<AnafException>(() => client.ExchangeCodeAsync("code"));

        Assert.Contains("no access token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Keeps_the_existing_refresh_token_when_anaf_omits_one()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(
            TestHarness.Json("{\"access_token\":\"new\",\"expires_in\":3600}")));

        var client = TestHarness.CreateOAuthClient(mock, TestHarness.DefaultOAuthOptions());

        var token = await client.RefreshTokenAsync("old-refresh");

        // Null means "keep what you have", which the caller must handle — it is not an error.
        Assert.Null(token.RefreshToken);
        Assert.Equal("new", token.AccessToken);
    }
}
