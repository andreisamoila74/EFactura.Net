using System.Text;
using System.Xml;
using System.Xml.Linq;
using EFactura.Net.Ubl.Internal;

namespace EFactura.Net.Ubl;

/// <summary>
/// Serializes <see cref="UblInvoice"/> and <see cref="UblCreditNote"/> to CIUS-RO UBL 2.1 XML.
/// </summary>
/// <remarks>
/// Output is deterministic: the same document always produces the same bytes, which makes the result
/// safe to snapshot-test and to hash for deduplication.
/// </remarks>
public static class UblWriter
{
    /// <summary>Serializes a document to a UBL 2.1 XML string.</summary>
    /// <param name="document">The invoice or credit note to serialize.</param>
    /// <param name="options">Serialization options; <see cref="UblWriteOptions.Default"/> when omitted.</param>
    /// <returns>The XML, ready to upload to ANAF.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="UblValidationException">
    /// The document is structurally invalid — see <see cref="UblValidationException.Errors"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var xml = UblWriter.Write(new UblInvoice
    /// {
    ///     DocumentId = "FCT nr. 1",
    ///     IssueDate = new DateOnly(2026, 1, 15),
    ///     CurrencyCode = "RON",
    ///     Seller = seller,
    ///     Buyer = buyer,
    ///     Lines = [new UblLine { Name = "Consultanță", Quantity = 1m, UnitPrice = 1000m, TaxRate = 21m }],
    /// });
    /// </code>
    /// </example>
    public static string Write(UblDocument document, UblWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= UblWriteOptions.Default;

        var root = UblXmlBuilder.Build(document, options);

        var settings = new XmlWriterSettings
        {
            Indent = options.Indent,
            IndentChars = "  ",
            OmitXmlDeclaration = !options.IncludeXmlDeclaration,
            Encoding = Utf8NoBom,
        };

        // XmlWriter takes the declared encoding from the TextWriter, not from settings.Encoding,
        // so writing to a plain StringWriter would declare utf-16 on a document that is handed to
        // ANAF as UTF-8.
        using var output = new Utf8StringWriter();
        using (var writer = XmlWriter.Create(output, settings))
        {
            root.Save(writer);
        }

        return output.ToString();
    }

    /// <summary>Serializes a document to an existing <see cref="XmlWriter"/>.</summary>
    /// <param name="document">The invoice or credit note to serialize.</param>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="options">Serialization options; <see cref="UblWriteOptions.Default"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/> or <paramref name="writer"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="UblValidationException">The document is structurally invalid.</exception>
    /// <remarks>
    /// Named differently from <see cref="Write(UblDocument, UblWriteOptions?)"/> rather than being an
    /// overload of it: two overloads that both take optional parameters cannot be extended later
    /// without introducing ambiguity.
    /// <para>
    /// The caller owns the writer and its settings, so
    /// <see cref="UblWriteOptions.IncludeXmlDeclaration"/> and <see cref="UblWriteOptions.Indent"/>
    /// are ignored here.
    /// </para>
    /// </remarks>
    public static void WriteTo(UblDocument document, XmlWriter writer, UblWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);

        UblXmlBuilder.Build(document, options ?? UblWriteOptions.Default).Save(writer);
    }

    /// <summary>
    /// Serializes a document to an <see cref="XDocument"/>, for callers that want to inspect or
    /// post-process the tree before writing it out.
    /// </summary>
    /// <param name="document">The invoice or credit note to serialize.</param>
    /// <param name="options">Serialization options; <see cref="UblWriteOptions.Default"/> when omitted.</param>
    /// <returns>The document tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="UblValidationException">The document is structurally invalid.</exception>
    public static XDocument ToXDocument(UblDocument document, UblWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new XDocument(UblXmlBuilder.Build(document, options ?? UblWriteOptions.Default));
    }

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>A <see cref="StringWriter"/> that reports UTF-8 so the XML declaration says so.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Utf8NoBom;
    }
}
