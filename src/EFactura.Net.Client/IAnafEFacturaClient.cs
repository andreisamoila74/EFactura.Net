namespace EFactura.Net.Client;

/// <summary>
/// The authenticated ANAF e-Factura API: upload, status, download and message listing.
/// </summary>
/// <remarks>
/// Every member needs an OAuth2 access token, supplied by the registered
/// <see cref="IAnafAccessTokenProvider"/>. For the endpoints that need no token see
/// <see cref="IAnafValidationClient"/> and <see cref="IAnafCompanyLookupClient"/>.
/// </remarks>
public interface IAnafEFacturaClient
{
    /// <summary>Uploads a document for processing.</summary>
    /// <param name="cif">
    /// The issuer's tax identifier. The <c>RO</c> prefix and spaces are stripped automatically.
    /// </param>
    /// <param name="xml">The document XML — see <see cref="Ubl.UblWriter"/>.</param>
    /// <param name="standard">Which standard <paramref name="xml"/> conforms to.</param>
    /// <param name="options">Optional upload flags.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The outcome. A rejected upload returns <see cref="AnafUploadResult.IsSuccess"/> as
    /// <see langword="false"/> with the reasons on <see cref="AnafUploadResult.Errors"/> — it does
    /// not throw.
    /// </returns>
    /// <exception cref="AnafAuthenticationException">The token was rejected (HTTP 401/403).</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    /// <remarks>
    /// Acceptance here only means the submission was queued. Poll
    /// <see cref="GetStatusAsync"/> with the returned
    /// <see cref="AnafUploadResult.UploadIndex"/> for the actual verdict.
    /// </remarks>
    Task<AnafUploadResult> UploadAsync(
        string cif,
        string xml,
        AnafUploadStandard standard,
        AnafUploadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Checks the processing status of a submission.</summary>
    /// <param name="uploadIndex">The <c>index_incarcare</c> from the upload.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current status.</returns>
    /// <exception cref="AnafAuthenticationException">The token was rejected (HTTP 401/403).</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    Task<AnafMessageStatus> GetStatusAsync(string uploadIndex, CancellationToken cancellationToken = default);

    /// <summary>Downloads a processed message.</summary>
    /// <param name="downloadId">
    /// The <c>id_descarcare</c> from <see cref="GetStatusAsync"/>, or the
    /// <see cref="AnafMessage.Id"/> from a listing.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The ZIP together with the document XML and signature unpacked from it. For a rejected
    /// document the reasons are on <see cref="AnafDownloadResult.Errors"/>.
    /// </returns>
    /// <exception cref="AnafAuthenticationException">The token was rejected (HTTP 401/403).</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    Task<AnafDownloadResult> DownloadAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>Lists messages from the last <paramref name="days"/> days.</summary>
    /// <param name="cif">The tax identifier to list messages for.</param>
    /// <param name="days">How far back to look, 1 to 60.</param>
    /// <param name="filter">Restricts to one message kind; <see langword="null"/> returns all.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The messages. An empty window is reported by ANAF as an <see cref="AnafMessageList.Error"/>
    /// string with no messages, not as a failure.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is outside 1–60.</exception>
    /// <exception cref="AnafAuthenticationException">The token was rejected (HTTP 401/403).</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    Task<AnafMessageList> ListMessagesAsync(
        string cif,
        int days,
        AnafMessageFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists messages in an arbitrary time range, transparently walking ANAF's pagination.
    /// </summary>
    /// <param name="cif">The tax identifier to list messages for.</param>
    /// <param name="startTime">Start of the range, inclusive.</param>
    /// <param name="endTime">End of the range, inclusive.</param>
    /// <param name="filter">Restricts to one message kind; <see langword="null"/> returns all.</param>
    /// <param name="cancellationToken">Stops the enumeration.</param>
    /// <returns>Every message in the range, one page fetched at a time.</returns>
    /// <exception cref="ArgumentException"><paramref name="endTime"/> is earlier than <paramref name="startTime"/>.</exception>
    /// <exception cref="AnafAuthenticationException">The token was rejected (HTTP 401/403).</exception>
    /// <exception cref="AnafHttpException">ANAF returned another non-success status.</exception>
    /// <remarks>
    /// Pages are fetched lazily, so breaking out of the loop early stops the requests. Unlike
    /// <see cref="ListMessagesAsync"/> this is not capped at 60 days.
    /// </remarks>
    IAsyncEnumerable<AnafMessage> ListMessagesPagedAsync(
        string cif,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        AnafMessageFilter? filter = null,
        CancellationToken cancellationToken = default);
}
