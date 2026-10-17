using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EFactura.Net.Client.Internal;

namespace EFactura.Net.Client;

/// <summary>
/// The OAuth2 mechanics for obtaining ANAF access tokens: authorization URL, code exchange, refresh.
/// </summary>
/// <remarks>
/// <para>
/// This client performs the HTTP calls and nothing else. It never stores a token, generates or
/// verifies the <c>state</c> parameter, or renders a callback page — those belong to the host, which
/// is the only thing that knows its own session and storage model.
/// </para>
/// <para>
/// The authorization step requires a qualified digital certificate present in the user's browser, so
/// it cannot be automated: the user must be sent to the URL from
/// <see cref="BuildAuthorizationUri"/> interactively.
/// </para>
/// </remarks>
public sealed class AnafOAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly AnafOAuthOptions _options;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">The transport.</param>
    /// <param name="options">Client credentials and endpoints.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public AnafOAuthClient(HttpClient httpClient, AnafOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
    }

    /// <summary>Builds the URL to send the user to in order to authorize access.</summary>
    /// <param name="state">
    /// An opaque value echoed back to the redirect URI. Generate it unguessably, tie it to the
    /// user's session, and verify it on callback — it is the CSRF defence for this flow.
    /// </param>
    /// <returns>The authorization URL.</returns>
    /// <exception cref="ArgumentException"><paramref name="state"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="AnafOAuthOptions.ClientId"/> or <see cref="AnafOAuthOptions.RedirectUri"/> is unset.
    /// </exception>
    public Uri BuildAuthorizationUri(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException($"{nameof(AnafOAuthOptions)}.{nameof(AnafOAuthOptions.ClientId)} is not configured.");
        }

        if (_options.RedirectUri is null)
        {
            throw new InvalidOperationException($"{nameof(AnafOAuthOptions)}.{nameof(AnafOAuthOptions.RedirectUri)} is not configured.");
        }

        var query = new StringBuilder()
            .Append("response_type=code")
            .Append("&client_id=").Append(Uri.EscapeDataString(_options.ClientId))
            .Append("&redirect_uri=").Append(Uri.EscapeDataString(_options.RedirectUri.ToString()))
            .Append("&token_content_type=jwt")
            .Append("&state=").Append(Uri.EscapeDataString(state));

        var separator = string.IsNullOrEmpty(_options.AuthorizeEndpoint.Query) ? "?" : "&";

        return new Uri($"{_options.AuthorizeEndpoint}{separator}{query}");
    }

    /// <summary>Exchanges an authorization code for tokens.</summary>
    /// <param name="code">The <c>code</c> query parameter from the callback.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The access and refresh tokens.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> is empty.</exception>
    /// <exception cref="AnafAuthenticationException">ANAF rejected the client credentials or the code.</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    /// <remarks>
    /// Check the callback for an <c>error</c> query parameter before calling this: ANAF can report
    /// failure in the query string of what otherwise looks like a successful redirect.
    /// </remarks>
    public async Task<AnafTokenResponse> ExchangeCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri?.ToString() ?? "",
            ["token_content_type"] = "jwt",
        };

        return await PostTokenRequestAsync(form, "authorization code exchange", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Exchanges a refresh token for a new access token.</summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The new tokens. ANAF may or may not issue a new refresh token; when
    /// <see cref="AnafTokenResponse.RefreshToken"/> is <see langword="null"/>, keep the existing one.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="refreshToken"/> is empty.</exception>
    /// <exception cref="AnafAuthenticationException">The refresh token was rejected.</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    public async Task<AnafTokenResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };

        return await PostTokenRequestAsync(form, "token refresh", cancellationToken).ConfigureAwait(false);
    }

    private async Task<AnafTokenResponse> PostTokenRequestAsync(
        Dictionary<string, string> form,
        string operation,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };

        // ANAF authenticates the client with HTTP Basic, not with form fields.
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        HttpResponseGuard.EnsureSuccess(response, operation, body);

        return ParseTokenResponse(body);
    }

    private static AnafTokenResponse ParseTokenResponse(string body)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(body);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new AnafException("ANAF returned a token response that is not valid JSON.", ex)
            {
                RawResponse = body,
            };
        }

        var accessToken = root.TryGetProperty("access_token", out var token) ? token.GetString() : null;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new AnafException("ANAF's token response contained no access token.", body);
        }

        return new AnafTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
            ExpiresIn = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
                ? seconds
                : 0,
            TokenType = root.TryGetProperty("token_type", out var type) ? type.GetString() : null,
            Scope = root.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
        };
    }
}
