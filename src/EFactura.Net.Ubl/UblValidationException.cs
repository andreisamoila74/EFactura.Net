namespace EFactura.Net.Ubl;

/// <summary>
/// Thrown when a document cannot be serialized because it is structurally invalid — for example an
/// unresolvable county while <see cref="UblWriteOptions.StrictAddressValidation"/> is on.
/// </summary>
/// <remarks>
/// This covers problems detectable locally. It says nothing about whether ANAF will accept the
/// document: only ANAF's own validation endpoint can answer that.
/// </remarks>
public sealed class UblValidationException : Exception
{
    /// <summary>Creates an exception carrying a single validation error.</summary>
    /// <param name="message">The error.</param>
    public UblValidationException(string message)
        : base(message) => Errors = [message];

    /// <summary>Creates an exception carrying several validation errors.</summary>
    /// <param name="errors">The errors. Must contain at least one entry.</param>
    public UblValidationException(IReadOnlyList<string> errors)
        : base(FormatMessage(errors)) => Errors = errors;

    /// <summary>Creates an exception with a message and an inner cause.</summary>
    /// <param name="message">The error.</param>
    /// <param name="innerException">The underlying cause.</param>
    public UblValidationException(string message, Exception innerException)
        : base(message, innerException) => Errors = [message];

    /// <summary>Every validation error detected, in the order they were found.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string FormatMessage(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return errors.Count == 1
            ? errors[0]
            : $"The document failed validation with {errors.Count} errors: {string.Join("; ", errors)}";
    }
}
