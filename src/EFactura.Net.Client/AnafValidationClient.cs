using System.Text;
using System.Text.Json;
using EFactura.Net.Client.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EFactura.Net.Client;

/// <summary>
/// ANAF's public validation and PDF-rendering endpoints.
/// </summary>
/// <remarks>
/// Separate from <see cref="IAnafEFacturaClient"/> because these endpoints need **no**
/// authentication: you can validate a document, or render it, without ever obtaining an OAuth token.
/// That also makes them safe to exercise from CI.
/// </remarks>
public interface IAnafValidationClient
{
    /// <summary>Validates a document against ANAF's own validator.</summary>
    /// <param name="xml">The document XML.</param>
    /// <param name="standard">Whether it is an invoice or a credit note.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The verdict and any failures — invalid documents do not throw.</returns>
    /// <exception cref="AnafHttpException">ANAF returned a non-success status.</exception>
    /// <remarks>
    /// This is the authoritative check. Running it before uploading turns a rejection that would
    /// otherwise arrive minutes later through the status endpoint into an immediate answer.
    /// </remarks>
    Task<AnafValidationResult> ValidateAsync(
        string xml,
        AnafValidationStandard standard,
        CancellationToken cancellationToken = default);

    /// <summary>Renders a document as a PDF.</summary>
    /// <param name="xml">The document XML.</param>
    /// <param name="standard">Whether it is an invoice or a credit note.</param>
    /// <param name="validate">
    /// When <see langword="true"/> (the default) ANAF validates before rendering and refuses an
    /// invalid document. Pass <see langword="false"/> to render regardless.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF, or the reasons it could not be produced.</returns>
    /// <exception cref="AnafHttpException">ANAF returned a non-success status.</exception>
    Task<AnafPdfResult> ConvertToPdfAsync(
        string xml,
        AnafValidationStandard standard,
        bool validate = true,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IAnafValidationClient"/> implementation.</summary>
public sealed class AnafValidationClient : IAnafValidationClient
{
    private readonly HttpClient _httpClient;
    private readonly AnafValidationOptions _options;
    private readonly ILogger<AnafValidationClient> _logger;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">The transport. No authentication handler is required.</param>
    /// <param name="options">Endpoint configuration; defaults to ANAF's public service.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <see langword="null"/>.</exception>
    public AnafValidationClient(
        HttpClient httpClient,
        AnafValidationOptions? options = null,
        ILogger<AnafValidationClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _options = options ?? new AnafValidationOptions();
        _logger = logger ?? NullLogger<AnafValidationClient>.Instance;
    }

    /// <inheritdoc />
    public async Task<AnafValidationResult> ValidateAsync(
        string xml,
        AnafValidationStandard standard,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var uri = new Uri(_options.BaseAddress, $"validare/{ToPathSegment(standard)}");

        using var content = new StringContent(xml, Encoding.UTF8, "text/plain");
        using var response = await _httpClient.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        HttpResponseGuard.EnsureSuccess(response, "validare", body);

        Log.ValidationResponse(_logger, body.Length);

        return ParseValidationResponse(body);
    }

    /// <inheritdoc />
    public async Task<AnafPdfResult> ConvertToPdfAsync(
        string xml,
        AnafValidationStandard standard,
        bool validate = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        // The trailing /DA segment means "skip validation".
        var path = validate
            ? $"transformare/{ToPathSegment(standard)}"
            : $"transformare/{ToPathSegment(standard)}/DA";

        using var content = new StringContent(xml, Encoding.UTF8, "text/plain");
        using var response = await _httpClient
            .PostAsync(new Uri(_options.BaseAddress, path), content, cancellationToken)
            .ConfigureAwait(false);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            HttpResponseGuard.EnsureSuccess(response, "transformare", Encoding.UTF8.GetString(bytes));
        }

        // A PDF starts with "%PDF"; anything else is a JSON error report under HTTP 200.
        if (IsPdf(bytes))
        {
            return new AnafPdfResult { IsSuccess = true, PdfContent = bytes, Errors = [] };
        }

        var body = Encoding.UTF8.GetString(bytes);

        return new AnafPdfResult
        {
            IsSuccess = false,
            PdfContent = null,
            Errors = ParseValidationResponse(body).Errors,
        };
    }

    private static bool IsPdf(byte[] content) =>
        content.Length >= 4 && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46;

    private static string ToPathSegment(AnafValidationStandard standard) => standard switch
    {
        AnafValidationStandard.Invoice => "FACT1",
        AnafValidationStandard.CreditNote => "FCN",
        _ => throw new ArgumentOutOfRangeException(nameof(standard), standard, "Unknown validation standard."),
    };

    private static AnafValidationResult ParseValidationResponse(string body)
    {
        var errors = new List<AnafError>();
        var isValid = false;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("stare", out var state) && state.ValueKind == JsonValueKind.String)
            {
                isValid = string.Equals(state.GetString(), "ok", StringComparison.OrdinalIgnoreCase);
            }

            if (root.TryGetProperty("Messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in messages.EnumerateArray())
                {
                    if (message.TryGetProperty("message", out var text)
                        && text.ValueKind == JsonValueKind.String
                        && text.GetString() is { Length: > 0 } value)
                    {
                        errors.Add(new AnafError { Message = value });
                    }
                }
            }
        }
        catch (JsonException)
        {
            // ANAF occasionally answers with a bare text body; surface it rather than losing it.
            if (!string.IsNullOrWhiteSpace(body))
            {
                errors.Add(new AnafError { Message = body.Trim() });
            }
        }

        return new AnafValidationResult
        {
            IsValid = isValid && errors.Count == 0,
            Errors = errors,
            RawResponse = body,
        };
    }
}
