using System.Net;

namespace EFactura.Net.Client;

/// <summary>
/// Base for failures talking to ANAF.
/// </summary>
/// <remarks>
/// Only transport-level and authentication failures throw. Anything ANAF reports as a business
/// outcome — a rejected document, validation errors, an <c>eroare</c> in a message list — is returned
/// as data on the result type, because callers branch on those routinely and exceptions are the wrong
/// shape for an expected answer.
/// </remarks>
public class AnafException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public AnafException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner cause.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying cause.</param>
    public AnafException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception carrying the raw response body.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="rawResponse">The response body as received.</param>
    public AnafException(string message, string? rawResponse)
        : base(message) => RawResponse = rawResponse;

    /// <summary>
    /// The response body exactly as ANAF sent it, when one was read. Never swallowed — ANAF's error
    /// reporting is inconsistent enough that the raw text is often the only way to diagnose a failure.
    /// </summary>
    public string? RawResponse { get; init; }
}

/// <summary>A non-success HTTP status from ANAF.</summary>
public sealed class AnafHttpException : AnafException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="statusCode">The HTTP status received.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="rawResponse">The response body as received.</param>
    public AnafHttpException(HttpStatusCode statusCode, string message, string? rawResponse = null)
        : base(message, rawResponse) => StatusCode = statusCode;

    /// <summary>The HTTP status code ANAF returned.</summary>
    public HttpStatusCode StatusCode { get; }
}

/// <summary>
/// ANAF rejected the credentials — HTTP 401 or 403.
/// </summary>
/// <remarks>
/// Usually an expired access token, but also a token minted for the other environment: test and
/// production credentials are not interchangeable.
/// </remarks>
public sealed class AnafAuthenticationException : AnafException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="statusCode">The HTTP status received — 401 or 403.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="rawResponse">The response body as received.</param>
    public AnafAuthenticationException(HttpStatusCode statusCode, string message, string? rawResponse = null)
        : base(message, rawResponse) => StatusCode = statusCode;

    /// <summary>The HTTP status code ANAF returned.</summary>
    public HttpStatusCode StatusCode { get; }
}
