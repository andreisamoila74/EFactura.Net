using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EFactura.Net.Client.Internal;

/// <summary>
/// Unpacks the ZIP ANAF returns from <c>descarcare</c>: the document XML plus ANAF's detached
/// signature, or — for a rejected document — the error report.
/// </summary>
internal static class AnafZipReader
{
    internal static AnafDownloadResult Read(byte[] zipContent)
    {
        string? invoiceXml = null;
        string? signatureXml = null;
        var errors = new List<AnafError>();

        try
        {
            using var stream = new MemoryStream(zipContent);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = ReadEntry(entry);

                if (entry.Name.StartsWith("semnatura", StringComparison.OrdinalIgnoreCase))
                {
                    signatureXml = content;
                    continue;
                }

                // The remaining XML entry is either the signed document or an error report.
                var entryErrors = TryExtractErrors(content);
                if (entryErrors.Count > 0)
                {
                    errors.AddRange(entryErrors);
                }
                else
                {
                    invoiceXml ??= content;
                }
            }
        }
        catch (InvalidDataException ex)
        {
            throw new AnafException("ANAF returned a response that is not a readable ZIP archive.", ex);
        }

        return new AnafDownloadResult
        {
            // The ZIP arrived and was readable. Errors inside it describe a rejected document, which
            // is a successful download of a negative verdict.
            IsSuccess = errors.Count == 0,
            ZipContent = zipContent,
            InvoiceXml = invoiceXml,
            SignatureXml = signatureXml,
            Errors = errors,
        };
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private static IReadOnlyList<AnafError> TryExtractErrors(string content)
    {
        try
        {
            var document = XDocument.Parse(content);

            return document.Root is null
                ? []
                : AnafResponseParser.ExtractErrors(document.Root);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }
    }
}
