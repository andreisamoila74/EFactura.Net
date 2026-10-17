namespace EFactura.Net.Client;

/// <summary>
/// Supplies the OAuth2 access token for ANAF calls. Implement this over wherever you keep tokens.
/// </summary>
/// <remarks>
/// <para>
/// The SDK never stores, caches or refreshes a token — tokens are per-user, long-lived and sensitive,
/// and where they live is the host's decision. The token is read once per request and attached as a
/// bearer header, so returning a freshly-refreshed value takes effect immediately.
/// </para>
/// <para>
/// Implementations must be thread-safe: a single client instance can issue concurrent requests.
/// If refreshing, guard against the stampede of several callers refreshing the same expired token.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// internal sealed class TokenProvider(IUserTokenStore store) : IAnafAccessTokenProvider
/// {
///     public async ValueTask&lt;string&gt; GetAccessTokenAsync(CancellationToken cancellationToken)
///         => (await store.GetCurrentAsync(cancellationToken)).AccessToken;
/// }
/// </code>
/// </example>
public interface IAnafAccessTokenProvider
{
    /// <summary>Returns a valid access token.</summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The bearer token, without the <c>Bearer </c> prefix.</returns>
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
