using RichardSzalay.MockHttp;

namespace EFactura.Net.Client.Tests;

public class ListMessagesTests
{
    [Fact]
    public async Task Requests_the_window_and_cif()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.anaf.ro/test/FCTEL/rest/listaMesajeFactura")
            .WithExactQueryString("zile=7&cif=12345678")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.ListMessagesAsync("12345678", 7);

        mock.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData(AnafMessageFilter.Errors, "E")]
    [InlineData(AnafMessageFilter.Sent, "T")]
    [InlineData(AnafMessageFilter.Received, "P")]
    [InlineData(AnafMessageFilter.BuyerMessages, "R")]
    public async Task Sends_the_filter_letter(AnafMessageFilter filter, string expected)
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.anaf.ro/test/FCTEL/rest/listaMesajeFactura")
            .WithExactQueryString($"zile=7&cif=12345678&filtru={expected}")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.ListMessagesAsync("12345678", 7, filter);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Omits_the_filter_when_none_is_given()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.anaf.ro/test/FCTEL/rest/listaMesajeFactura")
            .WithExactQueryString("zile=7&cif=12345678")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await client.ListMessagesAsync("12345678", 7, filter: null);

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Parses_messages()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.ListMessagesAsync("12345678", 7);

        Assert.Equal(3, result.Messages.Count);

        var sent = result.Messages[0];
        Assert.Equal("9988776655", sent.Id);
        Assert.Equal("4455667788", sent.UploadIndex);
        Assert.Equal("12345678", sent.Cif);
        Assert.Equal(AnafMessageType.SentInvoice, sent.Type);
        Assert.Equal("FACTURA TRIMISA", sent.RawType);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 12, 30, 0, TimeSpan.Zero), sent.CreatedAt);
        Assert.NotNull(sent.Details);
    }

    [Theory]
    [InlineData("FACTURA TRIMISA", AnafMessageType.SentInvoice)]
    [InlineData("FACTURA PRIMITA", AnafMessageType.ReceivedInvoice)]
    [InlineData("ERORI FACTURA", AnafMessageType.InvoiceErrors)]
    [InlineData("MESAJ CUMPARATOR PRIMIT", AnafMessageType.BuyerMessageReceived)]
    [InlineData("MESAJ CUMPARATOR TRANSMIS", AnafMessageType.BuyerMessageSent)]
    [InlineData("CEVA NOU", AnafMessageType.Unknown)]
    public async Task Maps_message_types(string rawType, AnafMessageType expected)
    {
        var json = $$"""
            { "mesaje": [ { "data_creare": "202601151230", "cif": "12345678",
              "id_solicitare": "1", "detalii": "x", "tip": "{{rawType}}", "id": "2" } ] }
            """;

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(json)));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.ListMessagesAsync("12345678", 7);

        Assert.Equal(expected, result.Messages[0].Type);
        Assert.Equal(rawType, result.Messages[0].RawType);
    }

    /// <summary>
    /// "No messages in the interval" arrives as an `eroare` string under HTTP 200. It is a normal
    /// answer, not a failure.
    /// </summary>
    [Fact]
    public async Task An_empty_window_is_not_an_error()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-empty.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.ListMessagesAsync("12345678", 7);

        Assert.Empty(result.Messages);
        Assert.Contains("Nu exista mesaje", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    [InlineData(1000)]
    public async Task Rejects_a_window_outside_one_to_sixty_days(int days)
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateEFacturaClient(mock);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ListMessagesAsync("12345678", days));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public async Task Accepts_the_window_boundaries(int days)
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.ListMessagesAsync("12345678", days);

        Assert.NotEmpty(result.Messages);
    }
}

public class PagedMessageTests
{
    [Fact]
    public async Task Converts_the_range_to_epoch_milliseconds()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.anaf.ro/test/FCTEL/rest/listaMesajePaginatieFactura")
            .WithExactQueryString(
                $"startTime={from.ToUnixTimeMilliseconds()}&endTime={to.ToUnixTimeMilliseconds()}&cif=12345678&pagina=1")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-single-page.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        await foreach (var _ in client.ListMessagesPagedAsync("12345678", from, to))
        {
            // drain
        }

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Walks_every_page()
    {
        using var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "*").WithQueryString("pagina", "1")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-page1.json"))));
        mock.Expect(HttpMethod.Get, "*").WithQueryString("pagina", "2")
            .Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-page2.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var messages = new List<AnafMessage>();
        await foreach (var message in client.ListMessagesPagedAsync(
                           "12345678",
                           new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                           new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)))
        {
            messages.Add(message);
        }

        Assert.Equal(3, messages.Count);
        Assert.Equal(["2000000001", "2000000002", "2000000003"], messages.Select(m => m.Id));
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Stops_after_the_last_page()
    {
        var requests = 0;

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
        {
            requests++;
            return Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-page2.json")));
        });

        var client = TestHarness.CreateEFacturaClient(mock);

        await foreach (var _ in client.ListMessagesPagedAsync(
                           "12345678",
                           new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                           new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)))
        {
            // drain
        }

        // page2 reports totalPages=2 and is itself page 2, so exactly two fetches happen.
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task An_empty_range_makes_one_request_and_yields_nothing()
    {
        var requests = 0;

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
        {
            requests++;
            return Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-empty.json")));
        });

        var client = TestHarness.CreateEFacturaClient(mock);

        var messages = new List<AnafMessage>();
        await foreach (var message in client.ListMessagesPagedAsync(
                           "12345678",
                           new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                           new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)))
        {
            messages.Add(message);
        }

        Assert.Empty(messages);
        Assert.Equal(1, requests);
    }

    /// <summary>Lazy paging: abandoning the loop must stop the requests.</summary>
    [Fact]
    public async Task Breaking_out_early_stops_fetching()
    {
        var requests = 0;

        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() =>
        {
            requests++;
            return Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-page1.json")));
        });

        var client = TestHarness.CreateEFacturaClient(mock);

        await foreach (var _ in client.ListMessagesPagedAsync(
                           "12345678",
                           new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                           new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)))
        {
            break;
        }

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Rejects_an_inverted_range()
    {
        using var mock = new MockHttpMessageHandler();
        var client = TestHarness.CreateEFacturaClient(mock);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in client.ListMessagesPagedAsync(
                               "12345678",
                               new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                               new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))
            {
                // never reached
            }
        });
    }

    [Fact]
    public async Task Honours_cancellation_between_pages()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-page1.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.ListMessagesPagedAsync(
                               "12345678",
                               new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                               new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                               cancellationToken: cts.Token))
            {
                await cts.CancelAsync();
            }
        });
    }

    [Fact]
    public async Task Exposes_the_paging_metadata()
    {
        using var mock = new MockHttpMessageHandler();
        mock.When("*").Respond(() => Task.FromResult(TestHarness.Json(Fixture.Read("lista-mesaje-page1.json"))));

        var client = TestHarness.CreateEFacturaClient(mock);

        var result = await client.ListMessagesAsync("12345678", 7);

        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(1, result.CurrentPage);
    }
}
