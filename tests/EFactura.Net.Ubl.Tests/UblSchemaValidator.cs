using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace EFactura.Net.Ubl.Tests;

/// <summary>
/// Validates generated documents against the OASIS UBL 2.1 schemas shipped under <c>Schemas/</c>.
/// </summary>
/// <remarks>
/// This is a structural check — element order, cardinality, data types, required attributes. It is
/// not a CIUS-RO business-rule check; only ANAF's own validator can give that verdict, which is what
/// the opt-in integration suite is for.
/// </remarks>
internal static class UblSchemaValidator
{
    private static readonly Lazy<XmlSchemaSet> Schemas = new(Load, isThreadSafe: true);

    private static XmlSchemaSet Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Schemas");
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };

        // DtdProcessing.Parse is needed for exactly one file: UBL's copy of the W3C
        // xmldsig-core schema carries an internal DTD subset. OASIS already stripped its PUBLIC and
        // SYSTEM identifiers, so the subset is self-contained and nothing is fetched. These are
        // vendored, trusted, local files — and this setting lives only here, never in the library.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver(),
        };

        // Add every schema explicitly rather than relying on the maindoc imports to pull them in.
        // The signature modules reach the xmldsig namespace through an import that the resolver
        // does not follow on its own, which leaves ds:Signature undeclared at compile time.
        foreach (var file in Directory.GetFiles(root, "*.xsd", SearchOption.AllDirectories))
        {
            using var reader = XmlReader.Create(file, settings);
            set.Add(null, reader);
        }

        set.Compile();

        return set;
    }

    /// <summary>Returns every schema violation in <paramref name="xml"/>; empty means valid.</summary>
    internal static IReadOnlyList<string> Validate(string xml)
    {
        var errors = new List<string>();
        var document = XDocument.Parse(xml);

        document.Validate(
            Schemas.Value,
            (_, args) => errors.Add($"{args.Severity}: {args.Message}"),
            addSchemaInfo: false);

        return errors;
    }
}
