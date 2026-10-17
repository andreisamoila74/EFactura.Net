using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EFactura.Net.Client.Internal;
using EFactura.Net.Ubl.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EFactura.Net.Client;

/// <summary>Default <see cref="IAnafEFacturaClient"/> implementation.</summary>
public sealed class AnafEFacturaClient : IAnafEFacturaClient
{
    private readonly HttpClient _httpClient;
    private readonly AnafEFacturaOptions _options;
    private readonly ILogger<AnafEFacturaClient> _logger;
    private readonly Uri _baseAddress;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">
    /// The transport. When resolved from DI its handler chain already attaches the bearer token.
    /// </param>
    /// <param name="options">Environment and logging configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public AnafEFacturaClient(
        HttpClient httpClient,
        AnafEFacturaOptions options,
        ILogger<AnafEFacturaClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
        _logger = logger ?? NullLogger<AnafEFacturaClient>.Instance;
        _baseAddress = options.ResolveBaseAddress();
    }

    /// <inheritdoc />
    public async Task<AnafUploadResult> UploadAsync(
        string cif,
        string xml,
        AnafUploadStandard standard,
        AnafUploadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cif);
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var query = new StringBuilder()
            .Append("upload?standard=").Append(ToQueryValue(standard))
            .Append("&cif=").Append(Uri.EscapeDataString(CuiNormalizer.ToBareNumber(cif)));

        if (options?.ForeignBuyer == true)
        {
            query.Append("&extern=DA");
        }

        if (options?.SelfBilled == true)
        {
            query.Append("&autofactura=DA");
        }

        if (options?.Enforcement == true)
        {
            query.Append("&executare=DA");
        }

        using var content = new StringContent(xml, Encoding.UTF8, "text/plain");
        using var response = await _httpClient
            .PostAsync(new Uri(_baseAddress, query.ToString()), content, cancellationToken)
            .ConfigureAwait(false);

        var body = await ReadBodyAsync(response, "upload", cancellationToken).ConfigureAwait(false);

        return AnafResponseParser.ParseUploadResponse(body);
    }

    /// <inheritdoc />
    public async Task<AnafMessageStatus> GetStatusAsync(
        string uploadIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadIndex);

        var uri = new Uri(_baseAddress, $"stareMesaj?id_incarcare={Uri.EscapeDataString(uploadIndex)}");

        using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        var body = await ReadBodyAsync(response, "stareMesaj", cancellationToken).ConfigureAwait(false);

        return AnafResponseParser.ParseStatusResponse(body);
    }

    /// <inheritdoc />
    public async Task<AnafDownloadResult> DownloadAsync(
        string downloadId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadId);

        var uri = new Uri(_baseAddress, $"descarcare?id={Uri.EscapeDataString(downloadId)}");

        using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            HttpResponseGuard.EnsureSuccess(response, "descarcare", SafeDecode(bytes));
        }

        // ANAF answers errors with XML and success with a ZIP, both under HTTP 200 — sniff the body
        // rather than trusting the content type, which is not always set.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true || LooksLikeXml(bytes))
        {
            var xml = SafeDecode(bytes);
            LogBody("descarcare", xml);

            IReadOnlyList<AnafError> errors = [];
            try
            {
                var document = System.Xml.Linq.XDocument.Parse(xml);
                if (document.Root is not null)
                {
                    errors = AnafResponseParser.ExtractErrors(document.Root);
                }
            }
            catch (System.Xml.XmlException)
            {
                // Fall through to the generic message below.
            }

            return new AnafDownloadResult
            {
                IsSuccess = false,
                ZipContent = null,
                Errors = errors.Count > 0
                    ? errors
                    : [new AnafError { Message = Truncate(xml, 500) }],
            };
        }

        return AnafZipReader.Read(bytes);
    }

    /// <inheritdoc />
    public async Task<AnafMessageList> ListMessagesAsync(
        string cif,
        int days,
        AnafMessageFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cif);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, 60);

        var query = new StringBuilder()
            .Append("listaMesajeFactura?zile=").Append(days.ToString(CultureInfo.InvariantCulture))
            .Append("&cif=").Append(Uri.EscapeDataString(CuiNormalizer.ToBareNumber(cif)));

        AppendFilter(query, filter);

        using var response = await _httpClient
            .GetAsync(new Uri(_baseAddress, query.ToString()), cancellationToken)
            .ConfigureAwait(false);

        var body = await ReadBodyAsync(response, "listaMesajeFactura", cancellationToken).ConfigureAwait(false);

        return AnafMessageListParser.Parse(body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AnafMessage> ListMessagesPagedAsync(
        string cif,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        AnafMessageFilter? filter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cif);

        if (endTime < startTime)
        {
            throw new ArgumentException(
                "The end of the range must not be earlier than its start.",
                nameof(endTime));
        }

        // ANAF's paged endpoint takes the window as epoch milliseconds.
        var startMilliseconds = startTime.ToUnixTimeMilliseconds();
        var endMilliseconds = endTime.ToUnixTimeMilliseconds();
        var normalizedCif = CuiNormalizer.ToBareNumber(cif);

        var page = 1;
        var totalPages = 1;

        while (page <= totalPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = new StringBuilder()
                .Append("listaMesajePaginatieFactura?startTime=").Append(startMilliseconds.ToString(CultureInfo.InvariantCulture))
                .Append("&endTime=").Append(endMilliseconds.ToString(CultureInfo.InvariantCulture))
                .Append("&cif=").Append(Uri.EscapeDataString(normalizedCif))
                .Append("&pagina=").Append(page.ToString(CultureInfo.InvariantCulture));

            AppendFilter(query, filter);

            AnafMessageList result;
            using (var response = await _httpClient
                       .GetAsync(new Uri(_baseAddress, query.ToString()), cancellationToken)
                       .ConfigureAwait(false))
            {
                var body = await ReadBodyAsync(response, "listaMesajePaginatieFactura", cancellationToken)
                    .ConfigureAwait(false);

                result = AnafMessageListParser.Parse(body);
            }

            foreach (var message in result.Messages)
            {
                yield return message;
            }

            // ANAF reports an empty range through the `eroare` field rather than an empty list, and
            // omits the page count entirely in that case.
            totalPages = result.TotalPages ?? 1;
            page++;
        }
    }

    private static void AppendFilter(StringBuilder query, AnafMessageFilter? filter)
    {
        if (filter is null)
        {
            return;
        }

        query.Append("&filtru=").Append(filter switch
        {
            AnafMessageFilter.Errors => 'E',
            AnafMessageFilter.Sent => 'T',
            AnafMessageFilter.Received => 'P',
            AnafMessageFilter.BuyerMessages => 'R',
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown message filter."),
        });
    }

    private static string ToQueryValue(AnafUploadStandard standard) => standard switch
    {
        AnafUploadStandard.Ubl => "UBL",
        AnafUploadStandard.CreditNote => "CN",
        AnafUploadStandard.Cii => "CII",
        AnafUploadStandard.Rasp => "RASP",
        _ => throw new ArgumentOutOfRangeException(nameof(standard), standard, "Unknown upload standard."),
    };

    private async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        HttpResponseGuard.EnsureSuccess(response, operation, body);
        LogBody(operation, body);

        return body;
    }

    private void LogBody(string operation, string body)
    {
        if (_options.LogResponseBodies)
        {
            Log.AnafResponseBody(_logger, operation, body);
        }
    }

    private static bool LooksLikeXml(byte[] content)
    {
        if (content.Length < 5)
        {
            return false;
        }

        // A ZIP always starts with "PK"; anything beginning with '<' is XML.
        var prefix = Encoding.UTF8.GetString(content, 0, Math.Min(100, content.Length)).TrimStart();

        return prefix.StartsWith('<');
    }

    private static string SafeDecode(byte[] content) => Encoding.UTF8.GetString(content);

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;
}

/// <summary>Parses the JSON message-list payloads.</summary>
file static class AnafMessageListParser
{
    internal static AnafMessageList Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var messages = new List<AnafMessage>();

        if (root.TryGetProperty("mesaje", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var rawType = GetString(item, "tip") ?? "";

                messages.Add(new AnafMessage
                {
                    Id = GetString(item, "id") ?? "",
                    UploadIndex = GetString(item, "id_solicitare") ?? "",
                    Cif = GetString(item, "cif") ?? "",
                    CreatedAt = ParseCreatedAt(GetString(item, "data_creare")),
                    Type = ParseType(rawType),
                    RawType = rawType,
                    Details = GetString(item, "detalii"),
                });
            }
        }

        return new AnafMessageList
        {
            Messages = messages,
            Error = GetString(root, "eroare"),
            Title = GetString(root, "titlu"),
            TotalRecords = GetInt(root, "numar_total_inregistrari"),
            TotalPages = GetInt(root, "numar_total_pagini"),
            CurrentPage = GetInt(root, "index_pagina_curenta"),
        };
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            // ANAF returns these counters as strings on some responses and numbers on others.
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>
    /// ANAF formats message timestamps as <c>yyyyMMddHHmm</c> in Romanian local time, with no zone.
    /// </summary>
    private static DateTimeOffset ParseCreatedAt(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParseExact(
                value,
                "yyyyMMddHHmm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return default;
    }

    private static AnafMessageType ParseType(string rawType) => rawType.ToUpperInvariant() switch
    {
        "FACTURA PRIMITA" => AnafMessageType.ReceivedInvoice,
        "FACTURA TRIMISA" => AnafMessageType.SentInvoice,
        "ERORI FACTURA" => AnafMessageType.InvoiceErrors,
        "MESAJ CUMPARATOR PRIMIT" => AnafMessageType.BuyerMessageReceived,
        "MESAJ CUMPARATOR TRANSMIS" => AnafMessageType.BuyerMessageSent,
        _ => AnafMessageType.Unknown,
    };
}
