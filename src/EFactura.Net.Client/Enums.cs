namespace EFactura.Net.Client;

/// <summary>Which ANAF environment the client talks to.</summary>
public enum AnafEnvironment
{
    /// <summary>
    /// The test environment (<c>api.anaf.ro/test</c>). The default — an SDK that silently defaults to
    /// the live tax authority is the wrong failure mode.
    /// </summary>
    /// <remarks>
    /// Test and production issue OAuth credentials separately. A token minted for one environment is
    /// rejected by the other.
    /// </remarks>
    Test,

    /// <summary>The live environment (<c>api.anaf.ro/prod</c>). Documents sent here are real.</summary>
    Production,
}

/// <summary>Document standard declared on upload, sent as the <c>standard</c> query parameter.</summary>
public enum AnafUploadStandard
{
    /// <summary>UBL invoice — <c>standard=UBL</c>. Use for <see cref="Ubl.UblInvoice"/>.</summary>
    Ubl,

    /// <summary>UBL credit note — <c>standard=CN</c>. Use for <see cref="Ubl.UblCreditNote"/>.</summary>
    CreditNote,

    /// <summary>UN/CEFACT Cross Industry Invoice — <c>standard=CII</c>.</summary>
    Cii,

    /// <summary>Buyer response message — <c>standard=RASP</c>.</summary>
    Rasp,
}

/// <summary>Document standard for the validation and PDF-conversion endpoints.</summary>
public enum AnafValidationStandard
{
    /// <summary>Invoice — <c>FACT1</c>.</summary>
    Invoice,

    /// <summary>Credit note — <c>FCN</c>.</summary>
    CreditNote,
}

/// <summary>Processing state of an uploaded document, parsed from the <c>stare</c> attribute.</summary>
public enum AnafMessageState
{
    /// <summary>
    /// Still being processed — <c>in prelucrare</c>. Poll again; see <c>docs/polling.md</c> for a
    /// backoff that works in practice.
    /// </summary>
    Processing,

    /// <summary>Accepted and signed — <c>ok</c>. The signed document can be downloaded.</summary>
    Ok,

    /// <summary>
    /// Rejected — <c>nok</c>. The download contains the reasons; they also surface on
    /// <see cref="AnafMessageStatus.Errors"/>.
    /// </summary>
    Rejected,

    /// <summary>
    /// Not ingested at all — <c>XML cu erori nepreluat de sistem</c>. The document was malformed
    /// enough that ANAF never took it in.
    /// </summary>
    InvalidXml,

    /// <summary>
    /// A <c>stare</c> this library does not recognise. The verbatim value is on
    /// <see cref="AnafMessageStatus.RawState"/>.
    /// </summary>
    Unknown,
}

/// <summary>Kind of message in a message list, parsed from the <c>tip</c> field.</summary>
public enum AnafMessageType
{
    /// <summary>An invoice received from a supplier — <c>FACTURA PRIMITA</c>.</summary>
    ReceivedInvoice,

    /// <summary>An invoice you sent — <c>FACTURA TRIMISA</c>.</summary>
    SentInvoice,

    /// <summary>Validation errors for a document you sent — <c>ERORI FACTURA</c>.</summary>
    InvoiceErrors,

    /// <summary>A buyer response you received — <c>MESAJ CUMPARATOR PRIMIT</c>.</summary>
    BuyerMessageReceived,

    /// <summary>A buyer response you sent — <c>MESAJ CUMPARATOR TRANSMIS</c>.</summary>
    BuyerMessageSent,

    /// <summary>
    /// A <c>tip</c> this library does not recognise. The verbatim value is on
    /// <see cref="AnafMessage.RawType"/>.
    /// </summary>
    Unknown,
}

/// <summary>
/// Restricts a message listing to one kind of message, sent as the <c>filtru</c> query parameter.
/// </summary>
/// <remarks>
/// Deliberately not a <c>[Flags]</c> enum: ANAF accepts exactly one letter, so a combination would be
/// rejected. Pass <see langword="null"/> for no filter, which returns every type.
/// </remarks>
public enum AnafMessageFilter
{
    /// <summary>Validation errors only — <c>filtru=E</c>.</summary>
    Errors,

    /// <summary>Sent invoices only — <c>filtru=T</c>.</summary>
    Sent,

    /// <summary>Received invoices only — <c>filtru=P</c>.</summary>
    Received,

    /// <summary>Buyer response messages only — <c>filtru=R</c>.</summary>
    BuyerMessages,
}
