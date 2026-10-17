using System.IO.Compression;
using System.Net;
using System.Text;
using RichardSzalay.MockHttp;

namespace EFactura.Net.Client.Tests;

/// <summary>Loads the sanitised ANAF response samples under <c>Fixtures/</c>.</summary>
internal static class Fixture
{
    internal static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}

/// <summary>A token provider that hands back a fixed value and counts how often it was asked.</summary>
internal sealed class StubTokenProvider(string token = "test-token") : IAnafAccessTokenProvider
{
    internal int CallCount { get; private set; }

    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;

        return ValueTask.FromResult(token);
    }
}

/// <summary>Wires a mocked transport to a real client, including the auth handler.</summary>
internal static class TestHarness
{
    internal static readonly Uri BaseAddress = AnafEFacturaOptions.TestBaseAddress;

    /// <summary>Builds a client whose requests go through <paramref name="mock"/>.</summary>
    internal static AnafEFacturaClient CreateEFacturaClient(
        MockHttpMessageHandler mock,
        IAnafAccessTokenProvider? tokenProvider = null,
        AnafEFacturaOptions? options = null)
    {
        // Mirrors what AddAnafEFactura builds, so the tests exercise the real handler chain.
        var authHandler = new Internal.AnafAccessTokenHandler(tokenProvider ?? new StubTokenProvider())
        {
            InnerHandler = mock,
        };

        var httpClient = new HttpClient(authHandler) { BaseAddress = BaseAddress };

        return new AnafEFacturaClient(httpClient, options ?? new AnafEFacturaOptions());
    }

    internal static AnafValidationClient CreateValidationClient(
        MockHttpMessageHandler mock,
        AnafValidationOptions? options = null) =>
        new(mock.ToHttpClient(), options ?? new AnafValidationOptions());

    internal static AnafCompanyLookupClient CreateLookupClient(
        MockHttpMessageHandler mock,
        TimeProvider? timeProvider = null,
        AnafCompanyLookupOptions? options = null) =>
        new(mock.ToHttpClient(), options ?? new AnafCompanyLookupOptions(), timeProvider);

    internal static AnafOAuthClient CreateOAuthClient(MockHttpMessageHandler mock, AnafOAuthOptions options) =>
        new(mock.ToHttpClient(), options);

    internal static AnafOAuthOptions DefaultOAuthOptions() => new()
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        RedirectUri = new Uri("https://example.test/auth/anaf/callback"),
    };

    /// <summary>Builds the kind of ZIP ANAF returns for an accepted document.</summary>
    internal static byte[] SignedZip(string invoiceXml, string signatureXml, string index = "9988776655")
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, $"{index}.xml", invoiceXml);
            Write(archive, $"semnatura_{index}.xml", signatureXml);
        }

        return stream.ToArray();
    }

    /// <summary>Builds the ZIP ANAF returns for a rejected document — an error report inside.</summary>
    internal static byte[] ErrorZip(params string[] messages)
    {
        var entries = string.Concat(messages.Select(m => $"<Error errorMessage=\"{m}\"/>"));
        var xml = $"<header xmlns=\"mfp:anaf:dgti:spv:respUploadFisier:v1\">{entries}</header>";

        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "9988776656.xml", xml);
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        entryStream.Write(bytes, 0, bytes.Length);
    }

    internal static HttpResponseMessage Xml(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/xml") };

    internal static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    internal static HttpResponseMessage Binary(byte[] content, string mediaType = "application/zip") =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) { Headers = { ContentType = new(mediaType) } } };
}
