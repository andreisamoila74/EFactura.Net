using System.Net.Http.Headers;

namespace EFactura.Net.Client.Internal;

/// <summary>
/// Attaches the bearer token to each outgoing request.
/// </summary>
/// <remarks>
/// Per-request rather than by setting <c>DefaultRequestHeaders</c> on a pooled client: the handler
/// chain behind an <c>IHttpClientFactory</c> client is shared, so per-user credentials must never be
/// written onto it. Same bytes on the wire, no shared mutable state.
/// </remarks>
internal sealed class AnafAccessTokenHandler(IAnafAccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
