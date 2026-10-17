using System.Globalization;
using System.Xml.Linq;

namespace EFactura.Net.Client.Internal;

/// <summary>
/// Parses ANAF's XML responses for upload and status. Internal: these shapes are ANAF's, not ours.
/// </summary>
internal static class AnafResponseParser
{
    /// <summary>
    /// Codes ANAF uses for faults on their own side. A document that hit one of these may well
    /// succeed on a later retry, unchanged.
    /// </summary>
    private static readonly string[] TechnicalErrorCodes = ["Cod: 4001", "Cod: 4002", "Cod: 5001"];

    internal static AnafUploadResult ParseUploadResponse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            throw new AnafException("ANAF returned a malformed upload response.", xml);
        }

        var header = document.Root
            ?? throw new AnafException("ANAF returned an empty upload response.", xml);

        var executionStatusText = header.Attribute("ExecutionStatus")?.Value;
        var uploadIndex = header.Attribute("index_incarcare")?.Value;
        var errors = ExtractErrors(header);

        var executionStatus = int.TryParse(executionStatusText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : -1;

        var responseDate = ParseResponseDate(header.Attribute("dateResponse")?.Value);

        // ANAF signals acceptance with ExecutionStatus="0" AND an index to poll. Treat a missing
        // index as failure even if the status says 0 — there would be nothing to poll.
        var isSuccess = executionStatus == 0 && !string.IsNullOrWhiteSpace(uploadIndex) && errors.Count == 0;

        return new AnafUploadResult
        {
            IsSuccess = isSuccess,
            UploadIndex = string.IsNullOrWhiteSpace(uploadIndex) ? null : uploadIndex,
            ExecutionStatus = executionStatus,
            Errors = errors,
            ResponseDate = responseDate,
            RawResponse = xml,
        };
    }

    /// <summary>
    /// ANAF stamps upload responses as <c>yyyyMMddHHmm</c> with no zone or separators, which no
    /// general-purpose parse handles. Falls back to a lenient parse for anything else.
    /// </summary>
    private static DateTimeOffset? ParseResponseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        const DateTimeStyles Styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (DateTimeOffset.TryParseExact(value, "yyyyMMddHHmm", CultureInfo.InvariantCulture, Styles, out var exact))
        {
            return exact;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, Styles, out var parsed)
            ? parsed
            : null;
    }

    internal static AnafMessageStatus ParseStatusResponse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            throw new AnafException("ANAF returned a malformed status response.", xml);
        }

        var header = document.Root
            ?? throw new AnafException("ANAF returned an empty status response.", xml);

        var rawState = header.Attribute("stare")?.Value;
        var downloadId = header.Attribute("id_descarcare")?.Value;
        var errors = ExtractErrors(header);

        var isTechnicalError = errors.Any(error =>
            error.Message.Contains("eroare tehnica", StringComparison.OrdinalIgnoreCase)
            || TechnicalErrorCodes.Any(code => error.Message.Contains(code, StringComparison.Ordinal)));

        // ANAF sometimes answers with errors and no `stare` at all; treat that as a verdict rather
        // than as "still processing", which is what an absent state would otherwise imply.
        if (string.IsNullOrWhiteSpace(rawState) && errors.Count > 0)
        {
            rawState = isTechnicalError ? "eroare tehnica" : "nok";
        }

        rawState ??= "in prelucrare";

        return new AnafMessageStatus
        {
            State = ParseState(rawState, isTechnicalError),
            RawState = rawState,
            DownloadId = string.IsNullOrWhiteSpace(downloadId) ? null : downloadId,
            IsTechnicalError = isTechnicalError,
            Errors = errors,
            RawResponse = xml,
        };
    }

    private static AnafMessageState ParseState(string rawState, bool isTechnicalError)
    {
        if (isTechnicalError)
        {
            // A fault on ANAF's side says nothing about the document; it is still in flight.
            return AnafMessageState.Processing;
        }

        var normalized = rawState.Trim();

        if (normalized.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return AnafMessageState.Ok;
        }

        if (normalized.Equals("nok", StringComparison.OrdinalIgnoreCase))
        {
            return AnafMessageState.Rejected;
        }

        if (normalized.Contains("in prelucrare", StringComparison.OrdinalIgnoreCase))
        {
            return AnafMessageState.Processing;
        }

        return normalized.Contains("nepreluat", StringComparison.OrdinalIgnoreCase)
            ? AnafMessageState.InvalidXml
            : AnafMessageState.Unknown;
    }

    /// <summary>
    /// Pulls errors out of any ANAF XML payload.
    /// </summary>
    /// <remarks>
    /// Matches elements whose local name contains "Error" in any namespace, and reads the
    /// <c>errorMessage</c> attribute in preference to the element text. ANAF uses both forms
    /// depending on endpoint, so reading only one silently produces empty error lists.
    /// </remarks>
    internal static IReadOnlyList<AnafError> ExtractErrors(XElement root)
    {
        var errors = new List<AnafError>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in root.DescendantsAndSelf()
                     .Where(e => e.Name.LocalName.Contains("Error", StringComparison.OrdinalIgnoreCase)))
        {
            var message = element.Attribute("errorMessage")?.Value;

            if (string.IsNullOrWhiteSpace(message))
            {
                // Only take element text when it has no element children of its own, so a wrapping
                // <Errors> node does not swallow its children's text as one blob.
                message = element.HasElements ? null : element.Value;
            }

            if (string.IsNullOrWhiteSpace(message) || !seen.Add(message))
            {
                continue;
            }

            errors.Add(new AnafError
            {
                Message = message.Trim(),
                Code = element.Attribute("errorCode")?.Value,
            });
        }

        return errors;
    }
}
