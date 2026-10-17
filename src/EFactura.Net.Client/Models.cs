namespace EFactura.Net.Client;

/// <summary>A single error reported by ANAF.</summary>
public sealed record AnafError
{
    /// <summary>The message, in Romanian, as ANAF worded it.</summary>
    public required string Message { get; init; }

    /// <summary>The error code when ANAF supplied one.</summary>
    public string? Code { get; init; }
}

/// <summary>Extra flags for an upload, appended as query parameters.</summary>
public sealed record AnafUploadOptions
{
    /// <summary>
    /// <c>extern=DA</c> — the buyer is foreign, so the usual Romanian buyer identification rules are
    /// relaxed.
    /// </summary>
    public bool ForeignBuyer { get; init; }

    /// <summary>
    /// <c>autofactura=DA</c> — self-billing: the document is issued by the buyer on the supplier's
    /// behalf.
    /// </summary>
    public bool SelfBilled { get; init; }

    /// <summary>
    /// <c>executare=DA</c> — the document is issued under an enforcement procedure.
    /// </summary>
    public bool Enforcement { get; init; }
}

/// <summary>Outcome of an upload.</summary>
public sealed record AnafUploadResult
{
    /// <summary>
    /// Whether ANAF accepted the submission for processing. Acceptance is not validation — poll
    /// <see cref="IAnafEFacturaClient.GetStatusAsync"/> with <see cref="UploadIndex"/> for the verdict.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The <c>index_incarcare</c> identifying the submission, used for every subsequent status call.
    /// <see langword="null"/> when the upload was refused outright.
    /// </summary>
    public string? UploadIndex { get; init; }

    /// <summary>The <c>ExecutionStatus</c> attribute — <c>0</c> means accepted.</summary>
    public required int ExecutionStatus { get; init; }

    /// <summary>Errors reported on the upload itself. Empty when <see cref="IsSuccess"/>.</summary>
    public required IReadOnlyList<AnafError> Errors { get; init; }

    /// <summary>The <c>dateResponse</c> timestamp, when ANAF supplied one.</summary>
    public DateTimeOffset? ResponseDate { get; init; }

    /// <summary>The response body exactly as received.</summary>
    public required string RawResponse { get; init; }
}

/// <summary>Processing status of a previously uploaded document.</summary>
public sealed record AnafMessageStatus
{
    /// <summary>The parsed state.</summary>
    public required AnafMessageState State { get; init; }

    /// <summary>
    /// The <c>stare</c> attribute verbatim. Preserved so an unrecognised value — which maps to
    /// <see cref="AnafMessageState.Unknown"/> — is never lost.
    /// </summary>
    public required string RawState { get; init; }

    /// <summary>
    /// The <c>id_descarcare</c> used to fetch the signed document or the rejection details. Present
    /// once processing has finished.
    /// </summary>
    public string? DownloadId { get; init; }

    /// <summary>
    /// Whether ANAF reported a technical fault on their side rather than a problem with the document
    /// — codes 4001, 4002, 5001 or a message mentioning <c>eroare tehnica</c>.
    /// </summary>
    /// <remarks>
    /// Worth distinguishing: a technical error means retrying later may succeed with the identical
    /// document, whereas <see cref="AnafMessageState.Rejected"/> will not.
    /// </remarks>
    public required bool IsTechnicalError { get; init; }

    /// <summary>Errors reported alongside the status.</summary>
    public required IReadOnlyList<AnafError> Errors { get; init; }

    /// <summary>The response body exactly as received.</summary>
    public required string RawResponse { get; init; }

    /// <summary>Whether processing has finished, successfully or not.</summary>
    public bool IsFinal => State is AnafMessageState.Ok or AnafMessageState.Rejected or AnafMessageState.InvalidXml;
}

/// <summary>Result of downloading a message.</summary>
public sealed record AnafDownloadResult
{
    /// <summary>Whether a ZIP was returned. <see langword="false"/> means ANAF returned an error instead.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>The ZIP exactly as received — keep this if you need to archive the original.</summary>
    public byte[]? ZipContent { get; init; }

    /// <summary>
    /// The document XML extracted from the ZIP, when one was present.
    /// </summary>
    public string? InvoiceXml { get; init; }

    /// <summary>
    /// ANAF's detached signature (<c>semnatura_*.xml</c>), when present. This is what makes the
    /// downloaded document legally the filed one.
    /// </summary>
    public string? SignatureXml { get; init; }

    /// <summary>
    /// Errors — either returned instead of the ZIP, or carried inside it for a rejected document.
    /// </summary>
    public required IReadOnlyList<AnafError> Errors { get; init; }
}

/// <summary>A page or window of messages.</summary>
public sealed record AnafMessageList
{
    /// <summary>The messages returned.</summary>
    public required IReadOnlyList<AnafMessage> Messages { get; init; }

    /// <summary>
    /// ANAF's <c>eroare</c> field. Populated for conditions ANAF reports with HTTP 200 — notably
    /// "no messages in the requested interval", which is not a failure.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>ANAF's <c>titlu</c> field.</summary>
    public string? Title { get; init; }

    /// <summary>Total records matching the query. Paged listings only.</summary>
    public int? TotalRecords { get; init; }

    /// <summary>Total pages available. Paged listings only.</summary>
    public int? TotalPages { get; init; }

    /// <summary>The 1-based index of this page. Paged listings only.</summary>
    public int? CurrentPage { get; init; }
}

/// <summary>One entry in a message list.</summary>
public sealed record AnafMessage
{
    /// <summary>The message id, used as the <c>id</c> for a download.</summary>
    public required string Id { get; init; }

    /// <summary>The <c>id_solicitare</c> — the upload index of the originating submission.</summary>
    public required string UploadIndex { get; init; }

    /// <summary>The CIF the message concerns.</summary>
    public required string Cif { get; init; }

    /// <summary>When ANAF created the message, parsed from <c>data_creare</c> (<c>yyyyMMddHHmm</c>).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The parsed message kind.</summary>
    public required AnafMessageType Type { get; init; }

    /// <summary>The <c>tip</c> field verbatim.</summary>
    public required string RawType { get; init; }

    /// <summary>ANAF's free-text <c>detalii</c>.</summary>
    public string? Details { get; init; }
}

/// <summary>Result of validating a document against ANAF's validator.</summary>
public sealed record AnafValidationResult
{
    /// <summary>Whether the document passed.</summary>
    public required bool IsValid { get; init; }

    /// <summary>The validation failures. Empty when <see cref="IsValid"/>.</summary>
    public required IReadOnlyList<AnafError> Errors { get; init; }

    /// <summary>The response body exactly as received.</summary>
    public required string RawResponse { get; init; }
}

/// <summary>Result of converting a document to PDF.</summary>
public sealed record AnafPdfResult
{
    /// <summary>Whether a PDF was produced.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>The PDF bytes, when conversion succeeded.</summary>
    public byte[]? PdfContent { get; init; }

    /// <summary>
    /// Why conversion failed. ANAF validates before converting unless validation is skipped, so these
    /// are usually validation errors.
    /// </summary>
    public required IReadOnlyList<AnafError> Errors { get; init; }
}

/// <summary>An OAuth2 token response.</summary>
public sealed record AnafTokenResponse
{
    /// <summary>The access token.</summary>
    public required string AccessToken { get; init; }

    /// <summary>The refresh token, when one was issued.</summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Lifetime of the access token in seconds, verbatim from ANAF.
    /// </summary>
    /// <remarks>
    /// Surfaced as the raw number rather than a computed expiry instant: the conversion needs a clock,
    /// and the caller's clock — not the SDK's — is the one that will be compared against later.
    /// </remarks>
    public required int ExpiresIn { get; init; }

    /// <summary>The token type, normally <c>Bearer</c>.</summary>
    public string? TokenType { get; init; }

    /// <summary>The granted scope, when ANAF returned one.</summary>
    public string? Scope { get; init; }
}
