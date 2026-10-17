using System.Globalization;
using System.Text;
using System.Text.Json;
using EFactura.Net.Client.Internal;
using EFactura.Net.Ubl.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EFactura.Net.Client;

/// <summary>
/// Looks companies up in ANAF's public VAT-payer registry by tax identifier.
/// </summary>
/// <remarks>
/// Needs no authentication. Useful for pre-filling counterparty details and for checking whether a
/// buyer is enrolled in e-Factura before sending to them.
/// </remarks>
public interface IAnafCompanyLookupClient
{
    /// <summary>Looks up one company.</summary>
    /// <param name="cui">The tax identifier, with or without the <c>RO</c> prefix.</param>
    /// <param name="asOf">
    /// The date to state the registry data as of. Defaults to today — registry facts such as VAT
    /// standing are date-dependent, so this changes the answer.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The company, or <see langword="null"/> if the registry has no record.</returns>
    /// <exception cref="ArgumentException"><paramref name="cui"/> is not numeric.</exception>
    /// <exception cref="AnafHttpException">ANAF returned a non-success status.</exception>
    Task<AnafCompanyInfo?> GetByCuiAsync(
        string cui,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up several companies in one round trip.</summary>
    /// <param name="cuis">The tax identifiers.</param>
    /// <param name="asOf">The date to state the registry data as of. Defaults to today.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The companies found, in ANAF's order. Identifiers with no record are simply absent, so the
    /// result can be shorter than the input.
    /// </returns>
    /// <exception cref="ArgumentException">An identifier is not numeric.</exception>
    /// <exception cref="AnafHttpException">ANAF returned a non-success status.</exception>
    /// <remarks>
    /// Inputs longer than <see cref="AnafCompanyLookupOptions.BatchSize"/> are split across requests
    /// automatically.
    /// </remarks>
    Task<IReadOnlyList<AnafCompanyInfo>> GetManyByCuiAsync(
        IEnumerable<string> cuis,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IAnafCompanyLookupClient"/> implementation.</summary>
public sealed class AnafCompanyLookupClient : IAnafCompanyLookupClient
{
    private readonly HttpClient _httpClient;
    private readonly AnafCompanyLookupOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AnafCompanyLookupClient> _logger;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">The transport. No authentication handler is required.</param>
    /// <param name="options">Endpoint configuration; defaults to ANAF's public registry.</param>
    /// <param name="timeProvider">Supplies "today"; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <see langword="null"/>.</exception>
    public AnafCompanyLookupClient(
        HttpClient httpClient,
        AnafCompanyLookupOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<AnafCompanyLookupClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _options = options ?? new AnafCompanyLookupOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<AnafCompanyLookupClient>.Instance;
    }

    /// <inheritdoc />
    public async Task<AnafCompanyInfo?> GetByCuiAsync(
        string cui,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cui);

        var results = await GetManyByCuiAsync([cui], asOf, cancellationToken).ConfigureAwait(false);

        return results.Count > 0 ? results[0] : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnafCompanyInfo>> GetManyByCuiAsync(
        IEnumerable<string> cuis,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cuis);

        var numbers = cuis.Select(ParseCui).ToList();
        if (numbers.Count == 0)
        {
            return [];
        }

        var date = asOf ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var results = new List<AnafCompanyInfo>(numbers.Count);

        foreach (var batch in numbers.Chunk(Math.Max(1, _options.BatchSize)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.AddRange(await LookupBatchAsync(batch, date, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private async Task<IReadOnlyList<AnafCompanyInfo>> LookupBatchAsync(
        long[] batch,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var payload = new StringBuilder("[");
        for (var i = 0; i < batch.Length; i++)
        {
            if (i > 0)
            {
                payload.Append(',');
            }

            payload
                .Append("{\"cui\":").Append(batch[i].ToString(CultureInfo.InvariantCulture))
                .Append(",\"data\":\"").Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append("\"}");
        }

        payload.Append(']');

        using var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient
            .PostAsync(_options.BaseAddress, content, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // The registry answers "none of these identifiers exist" with 404 — and a perfectly good
        // JSON body listing them under `notFound`. That is an outcome, not a transport failure, so
        // it comes back as an empty result rather than an exception.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound
            && AnafCompanyParser.LooksLikeRegistryResponse(body))
        {
            Log.CompanyLookupBatch(_logger, batch.Length);

            return [];
        }

        HttpResponseGuard.EnsureSuccess(response, "company lookup", body);

        Log.CompanyLookupBatch(_logger, batch.Length);

        return AnafCompanyParser.Parse(body);
    }

    private static long ParseCui(string cui)
    {
        var bare = CuiNormalizer.ToBareNumber(cui);

        if (!long.TryParse(bare, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            throw new ArgumentException($"'{cui}' is not a valid Romanian tax identifier.", nameof(cui));
        }

        return number;
    }
}
