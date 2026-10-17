# Polling for status

Uploading returns an `index_incarcare`, not a verdict. ANAF processes asynchronously — usually in
seconds, sometimes in minutes — and you poll `stareMesaj` until it settles.

## Why the SDK does not do this for you

Scheduling is a decision about your system, not about e-Factura. A web request wants to hand off and
return; a background worker wants durable retries that survive a restart; a CLI is happy to block.
Baking one in would mean dragging a scheduler — Hangfire, Quartz, a hosted service — into a library
whose job is to speak a protocol.

So the SDK exposes `GetStatusAsync` and `IsFinal`, and the loop is yours. Here is one that works.

---

## A backoff that works in practice

```csharp
public static async Task<AnafMessageStatus> PollUntilFinalAsync(
    IAnafEFacturaClient client,
    string uploadIndex,
    CancellationToken cancellationToken,
    int maxAttempts = 20)
{
    AnafMessageStatus? status = null;

    for (var attempt = 0; attempt < maxAttempts; attempt++)
    {
        await Task.Delay(DelayFor(attempt), cancellationToken);

        status = await client.GetStatusAsync(uploadIndex, cancellationToken);

        if (status.IsFinal)
        {
            return status;
        }
    }

    return status ?? throw new InvalidOperationException("Polling never ran.");
}

private static TimeSpan DelayFor(int attempt) => attempt switch
{
    < 3  => TimeSpan.FromSeconds(5),
    < 6  => TimeSpan.FromSeconds(10),
    < 10 => TimeSpan.FromSeconds(20),
    _    => TimeSpan.FromSeconds(30),
};
```

| Attempts | Delay | Elapsed by the end |
|---|---|---|
| 1–3 | 5s | 15s |
| 4–6 | 10s | 45s |
| 7–10 | 20s | 2m 05s |
| 11–20 | 30s | 7m 05s |

Twenty attempts covers roughly seven minutes, which is comfortably past the normal case. This ladder
is the one the production application this library was extracted from used, so it reflects real ANAF
latency rather than a guess.

Compiled version: [`samples/.../Snippets/UsingTheApi.cs`](../samples/EFactura.Net.Samples.Console/Snippets/UsingTheApi.cs).

---

## Handle technical errors separately

```csharp
if (status.IsTechnicalError)
{
    // A fault on ANAF's side. It says nothing about your document — the same bytes will often
    // succeed on the next attempt. Keep polling.
}
```

`IsTechnicalError` covers codes 4001, 4002, 5001 and messages mentioning `eroare tehnica`. The state
stays `Processing`, because nothing has been concluded.

This is worth separating from `Rejected`: a rejection means the document is wrong and retrying the
identical bytes cannot help, whereas a technical error means try again. Treating the two the same —
which is easy to do by accident — produces either pointless retries of a broken document or premature
failure of a good one.

---

## Reaching the end of the budget

Exhausting the attempts is not a rejection. The document is still in flight, and the upload index
stays valid indefinitely:

```csharp
if (!status.IsFinal)
{
    // Persist the upload index and check again later. Do not re-upload — you would file twice.
}
```

**Never re-upload on a timeout.** The first submission may still be processing, and ANAF will
happily accept a duplicate.

---

## In a background worker

For anything that must survive a process restart, persist the upload index immediately and poll from
a durable job:

```csharp
var upload = await client.UploadAsync(cif, xml, AnafUploadStandard.Ubl, cancellationToken: ct);

if (upload.IsSuccess)
{
    await submissions.SaveAsync(invoiceId, upload.UploadIndex!, ct);
    scheduler.Schedule<CheckStatusJob>(j => j.RunAsync(invoiceId), TimeSpan.FromSeconds(5));
}
```

Each run checks once, records the outcome, and reschedules itself with the next delay if the status
is not final — the same ladder, spread across job executions rather than held in one loop. Keep a
retry count on the record so a stuck submission eventually stops rescheduling.

---

## Next

- [The API client](api-client.md)
- [Troubleshooting](troubleshooting.md)
