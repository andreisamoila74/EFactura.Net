using System.Security.Cryptography;
using EFactura.Net.Client;

namespace EFactura.Net.Samples.Console.Snippets;

/// <summary>
/// The OAuth2 authorization-code flow. Source for <c>docs/oauth.md</c>.
/// </summary>
/// <remarks>
/// The SDK performs the HTTP calls and nothing else: it does not mint or verify the <c>state</c>
/// parameter, render a callback page, or store a token. Those depend on the host's session and
/// storage model, which a library cannot know.
/// </remarks>
public static class OAuthFlow
{
    /// <summary>Step 1 — send the user to ANAF.</summary>
    /// <remarks>
    /// This step needs a qualified digital certificate present in the user's browser, so it cannot
    /// be automated: the user has to visit the URL themselves.
    /// </remarks>
    public static Uri BuildRedirect(AnafOAuthClient oauth, out string state)
    {
        // Unguessable, and tied to the user's session so the callback can be verified as theirs.
        state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        return oauth.BuildAuthorizationUri(state);
    }

    /// <summary>Step 2 — handle the callback.</summary>
    public static async Task<AnafTokenResponse> HandleCallbackAsync(
        AnafOAuthClient oauth,
        string? code,
        string? returnedState,
        string? error,
        string expectedState,
        CancellationToken cancellationToken)
    {
        // ANAF can report failure in the query string of what otherwise looks like a successful
        // redirect, so check for an error before looking at the code.
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"ANAF refused authorization: {error}");
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("The callback carried no authorization code.");
        }

        // Constant-time comparison: this is the CSRF defence for the whole flow.
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(returnedState ?? ""),
                System.Text.Encoding.UTF8.GetBytes(expectedState)))
        {
            throw new InvalidOperationException("The state parameter did not match.");
        }

        return await oauth.ExchangeCodeAsync(code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Step 3 — persist. Entirely the host's job.</summary>
    public static async Task StoreAsync(
        ITokenStore store,
        AnafTokenResponse token,
        CancellationToken cancellationToken)
    {
        await store.SaveAsync(
            token.AccessToken,
            token.RefreshToken ?? throw new InvalidOperationException("No refresh token was issued."),
            // ExpiresIn is surfaced raw; the clock that will be compared against later is yours.
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
            cancellationToken).ConfigureAwait(false);
    }
}
