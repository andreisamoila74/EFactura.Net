using EFactura.Net.Client;
using EFactura.Net.Ubl;

namespace EFactura.Net.Samples.Console.Snippets;

/// <summary>Calling ANAF. Source for the snippets in <c>docs/api-client.md</c>.</summary>
public static class UsingTheApi
{
    /// <summary>
    /// Validate before uploading. Costs one unauthenticated round trip and turns a rejection that
    /// would otherwise surface minutes later through the status endpoint into an immediate answer.
    /// </summary>
    public static async Task<bool> ValidateFirstAsync(
        IAnafValidationClient validation,
        UblInvoice invoice,
        CancellationToken cancellationToken)
    {
        var xml = UblWriter.Write(invoice);

        var result = await validation
            .ValidateAsync(xml, AnafValidationStandard.Invoice, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                System.Console.WriteLine($"  invalid: {error.Message}");
            }
        }

        return result.IsValid;
    }

    /// <summary>Upload, then poll until ANAF reaches a verdict, then fetch the signed document.</summary>
    public static async Task SubmitAsync(
        IAnafEFacturaClient client,
        string cif,
        UblInvoice invoice,
        CancellationToken cancellationToken)
    {
        var upload = await client
            .UploadAsync(cif, UblWriter.Write(invoice), AnafUploadStandard.Ubl, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // A refusal is an outcome, not an exception.
        if (!upload.IsSuccess)
        {
            foreach (var error in upload.Errors)
            {
                System.Console.WriteLine($"  refused: {error.Message}");
            }

            return;
        }

        System.Console.WriteLine($"  queued as {upload.UploadIndex}");

        var status = await PollUntilFinalAsync(client, upload.UploadIndex!, cancellationToken)
            .ConfigureAwait(false);

        switch (status.State)
        {
            case AnafMessageState.Ok:
                var download = await client
                    .DownloadAsync(status.DownloadId!, cancellationToken)
                    .ConfigureAwait(false);

                System.Console.WriteLine($"  accepted; signature {download.SignatureXml?.Length ?? 0} chars");
                break;

            case AnafMessageState.Rejected:
            case AnafMessageState.InvalidXml:
                var rejection = await client
                    .DownloadAsync(status.DownloadId!, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var error in rejection.Errors)
                {
                    System.Console.WriteLine($"  rejected: {error.Message}");
                }

                break;

            default:
                System.Console.WriteLine($"  gave up while still '{status.RawState}'");
                break;
        }
    }

    /// <summary>
    /// The polling loop, with the backoff documented in <c>docs/polling.md</c>.
    /// </summary>
    /// <remarks>
    /// Scheduling is deliberately not part of the SDK — a web app, a worker and a CLI all want
    /// different answers, and baking one in would drag a scheduler into the library.
    /// </remarks>
    public static async Task<AnafMessageStatus> PollUntilFinalAsync(
        IAnafEFacturaClient client,
        string uploadIndex,
        CancellationToken cancellationToken,
        int maxAttempts = 20)
    {
        AnafMessageStatus? status = null;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(DelayFor(attempt), cancellationToken).ConfigureAwait(false);

            status = await client.GetStatusAsync(uploadIndex, cancellationToken).ConfigureAwait(false);

            if (status.IsFinal)
            {
                return status;
            }

            // A fault on ANAF's side says nothing about the document — the same bytes may well be
            // accepted on the next attempt, so keep going rather than giving up.
            if (status.IsTechnicalError)
            {
                System.Console.WriteLine($"  ANAF reported a technical error; retrying ({attempt + 1}/{maxAttempts})");
            }
        }

        return status ?? throw new InvalidOperationException("Polling never ran.");
    }

    /// <summary>5s ×3, then 10s ×3, then 20s ×4, then 30s.</summary>
    public static TimeSpan DelayFor(int attempt) => attempt switch
    {
        < 3 => TimeSpan.FromSeconds(5),
        < 6 => TimeSpan.FromSeconds(10),
        < 10 => TimeSpan.FromSeconds(20),
        _ => TimeSpan.FromSeconds(30),
    };

    /// <summary>Listing inbound invoices from the last week.</summary>
    public static async Task ListInboundAsync(
        IAnafEFacturaClient client,
        string cif,
        CancellationToken cancellationToken)
    {
        var result = await client
            .ListMessagesAsync(cif, days: 7, AnafMessageFilter.Received, cancellationToken)
            .ConfigureAwait(false);

        // "No messages in the interval" arrives as an `eroare` string, not as a failure.
        if (result.Messages.Count == 0)
        {
            System.Console.WriteLine($"  nothing: {result.Error}");
            return;
        }

        foreach (var message in result.Messages)
        {
            System.Console.WriteLine($"  {message.CreatedAt:yyyy-MM-dd} {message.Type} {message.Id}");
        }
    }

    /// <summary>
    /// Walking a longer period. Paging is lazy, so breaking out stops the requests — and this is not
    /// capped at 60 days the way <c>ListMessagesAsync</c> is.
    /// </summary>
    public static async Task ListAcrossAQuarterAsync(
        IAnafEFacturaClient client,
        string cif,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

        await foreach (var message in client
                           .ListMessagesPagedAsync(cif, start, end, cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            System.Console.WriteLine($"  {message.CreatedAt:yyyy-MM-dd} {message.Type} {message.Id}");
        }
    }

    /// <summary>Looking a counterparty up before invoicing them.</summary>
    public static async Task<bool> IsEnrolledInEFacturaAsync(
        IAnafCompanyLookupClient lookup,
        string cui,
        CancellationToken cancellationToken)
    {
        var company = await lookup.GetByCuiAsync(cui, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (company is null)
        {
            System.Console.WriteLine($"  {cui}: not in the registry");
            return false;
        }

        System.Console.WriteLine(
            $"  {company.Name}: VAT={company.IsVatPayer} eFactura={company.IsEFacturaActive} inactive={company.IsInactive}");

        return company.IsEFacturaActive;
    }

    /// <summary>Rendering a document as a PDF through ANAF's own converter.</summary>
    public static async Task<byte[]?> RenderPdfAsync(
        IAnafValidationClient validation,
        UblInvoice invoice,
        CancellationToken cancellationToken)
    {
        var result = await validation
            .ConvertToPdfAsync(UblWriter.Write(invoice), AnafValidationStandard.Invoice, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? result.PdfContent : null;
    }

    /// <summary>What to catch, and what not to.</summary>
    public static async Task HandlingFailuresAsync(
        IAnafEFacturaClient client,
        string uploadIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await client.GetStatusAsync(uploadIndex, cancellationToken).ConfigureAwait(false);

            // Business outcomes are data. There is nothing to catch here.
            System.Console.WriteLine($"  {status.State} ({status.RawState})");
        }
        catch (AnafAuthenticationException ex)
        {
            // Expired token, or one minted for the other environment.
            System.Console.WriteLine($"  auth failed ({ex.StatusCode}); re-authorise");
        }
        catch (AnafHttpException ex)
        {
            // ANAF is down or rate limiting. Retry later.
            System.Console.WriteLine($"  ANAF returned {ex.StatusCode}: {ex.RawResponse}");
        }
    }
}
