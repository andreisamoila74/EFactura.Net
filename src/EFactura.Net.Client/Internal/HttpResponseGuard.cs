using System.Net;

namespace EFactura.Net.Client.Internal;

/// <summary>
/// Turns transport-level failures into typed exceptions, leaving business outcomes to the callers.
/// </summary>
internal static class HttpResponseGuard
{
    /// <summary>
    /// Throws for 401/403 and for any other non-success status. Returns normally otherwise, including
    /// for 200 responses whose body reports an ANAF business error.
    /// </summary>
    internal static void EnsureSuccess(HttpResponseMessage response, string operation, string? body)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new AnafAuthenticationException(
                response.StatusCode,
                $"ANAF rejected the credentials for {operation} (HTTP {(int)response.StatusCode}). "
                + "The access token may be expired, or issued for a different environment.",
                body);
        }

        throw new AnafHttpException(
            response.StatusCode,
            $"ANAF returned HTTP {(int)response.StatusCode} for {operation}.",
            body);
    }
}
