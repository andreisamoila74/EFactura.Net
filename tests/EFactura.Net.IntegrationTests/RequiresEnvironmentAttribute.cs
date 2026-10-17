using System.Runtime.CompilerServices;

namespace EFactura.Net.IntegrationTests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself unless the named environment variables are all
/// set to a non-empty value.
/// </summary>
/// <remarks>
/// <para>
/// These tests reach real ANAF endpoints, so they must never run by accident — not on a developer's
/// machine, and never on a pull request from a fork where secrets are unavailable.
/// </para>
/// <para>
/// Fixed arities rather than <c>params</c>: xUnit v3 requires custom attributes to forward
/// <see cref="CallerFilePathAttribute"/> and <see cref="CallerLineNumberAttribute"/> to the base
/// constructor, and a <c>params</c> array has to come last.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresEnvironmentAttribute : FactAttribute
{
    public RequiresEnvironmentAttribute(
        string variable,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber) => Skip = EnvironmentGate.SkipReason(variable);

    public RequiresEnvironmentAttribute(
        string variable1,
        string variable2,
        string variable3,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber) =>
        Skip = EnvironmentGate.SkipReason(variable1, variable2, variable3);
}

/// <summary>The <see cref="TheoryAttribute"/> equivalent of <see cref="RequiresEnvironmentAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresEnvironmentTheoryAttribute : TheoryAttribute
{
    public RequiresEnvironmentTheoryAttribute(
        string variable,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber) => Skip = EnvironmentGate.SkipReason(variable);
}

internal static class EnvironmentGate
{
    /// <summary>Returns the skip reason, or <see langword="null"/> when every variable is set.</summary>
    internal static string? SkipReason(params string[] variables)
    {
        var missing = variables
            .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            .ToList();

        return missing.Count == 0
            ? null
            : $"Requires environment variable(s): {string.Join(", ", missing)}.";
    }
}

/// <summary>The environment variables that gate this suite.</summary>
internal static class EnvironmentVariables
{
    /// <summary>Set to any non-empty value to enable the unauthenticated endpoint tests.</summary>
    internal const string Enabled = "ANAF_INTEGRATION";

    /// <summary>A valid access token for the ANAF <em>test</em> environment.</summary>
    internal const string TestAccessToken = "ANAF_TEST_ACCESS_TOKEN";

    /// <summary>The CIF that <see cref="TestAccessToken"/> is authorised for.</summary>
    internal const string TestCif = "ANAF_TEST_CIF";

    internal static string Require(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} is not set.");
}
