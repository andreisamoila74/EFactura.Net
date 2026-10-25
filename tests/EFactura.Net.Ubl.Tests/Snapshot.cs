using System.Text;

namespace EFactura.Net.Ubl.Tests;

/// <summary>
/// Minimal snapshot assertion: compares a generated document against a committed
/// <c>Snapshots/&lt;name&gt;.verified.xml</c>, writing a <c>.received.xml</c> beside it on mismatch.
/// </summary>
/// <remarks>
/// <para>
/// Hand-rolled rather than taking a snapshot-testing dependency. `Verify` — the obvious choice — now
/// enforces a maintenance-fee licence check at build time whose exemptions all expire within twelve
/// months, which would put a scheduled break into CI. For byte-comparing XML the whole mechanism is
/// the thirty lines below.
/// </para>
/// <para>
/// Snapshots are compared with normalised line endings, so a repo cloned on Windows and on Linux
/// produces the same verdict regardless of git's autocrlf setting.
/// </para>
/// </remarks>
internal static class Snapshot
{
    /// <summary>
    /// Set <c>EFACTURA_ACCEPT_SNAPSHOTS=1</c> to rewrite every <c>.verified.xml</c> from the current
    /// output. Review the diff before committing — this is how an unintended change gets blessed.
    /// </summary>
    private static bool AutoAccept =>
        Environment.GetEnvironmentVariable("EFACTURA_ACCEPT_SNAPSHOTS") == "1";

    /// <summary>
    /// Where the committed <c>.verified.xml</c> files live, found by walking up from the test
    /// binaries to the directory holding the project file.
    /// </summary>
    /// <remarks>
    /// Deliberately *not* <c>[CallerFilePath]</c>. That is baked in at compile time, and a
    /// deterministic build — which is what <c>ContinuousIntegrationBuild=true</c> turns on in CI —
    /// rewrites it to a placeholder like <c>/_/tests/…</c>. The result points nowhere: on Linux it
    /// fails with "Access to the path '/_' is denied", and on Windows it silently creates
    /// <c>C:\_\tests\…</c> and writes the snapshots there, which is worse.
    /// </remarks>
    internal static readonly Lazy<string> SnapshotDirectory = new(ResolveSnapshotDirectory);

    private static string ResolveSnapshotDirectory()
    {
        // AppContext.BaseDirectory is <project>/bin/<configuration>/<tfm>/.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return Path.Combine(dir.FullName, "Snapshots");
            }
        }

        throw new InvalidOperationException(
            $"Could not locate the test project directory by walking up from '{AppContext.BaseDirectory}'. "
            + "Snapshot tests need the source tree alongside the compiled tests.");
    }

    internal static void Matches(string actual, string snapshotName)
    {
        var directory = SnapshotDirectory.Value;
        Directory.CreateDirectory(directory);

        var verifiedPath = Path.Combine(directory, $"{snapshotName}.verified.xml");
        var receivedPath = Path.Combine(directory, $"{snapshotName}.received.xml");

        var normalized = Normalize(actual);

        if (AutoAccept || !File.Exists(verifiedPath))
        {
            File.WriteAllText(verifiedPath, normalized, new UTF8Encoding(false));

            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }

            if (!AutoAccept)
            {
                Assert.Fail(
                    $"No snapshot existed for '{snapshotName}'. One has been written to "
                    + $"{verifiedPath}. Review it and commit it to accept this as the expected output.");
            }

            return;
        }

        var expected = Normalize(File.ReadAllText(verifiedPath));

        if (string.Equals(expected, normalized, StringComparison.Ordinal))
        {
            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }

            return;
        }

        File.WriteAllText(receivedPath, normalized, new UTF8Encoding(false));

        Assert.Fail(
            $"Snapshot '{snapshotName}' does not match.{Environment.NewLine}"
            + $"  expected: {verifiedPath}{Environment.NewLine}"
            + $"  actual:   {receivedPath}{Environment.NewLine}"
            + $"{Environment.NewLine}{FirstDifference(expected, normalized)}");
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>Reports the first differing line with a little context, so failures are readable.</summary>
    private static string FirstDifference(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');

        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<missing>";

            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                return $"First difference at line {i + 1}:{Environment.NewLine}"
                    + $"  - {expectedLine.Trim()}{Environment.NewLine}"
                    + $"  + {actualLine.Trim()}";
            }
        }

        return "Files differ only in trailing content.";
    }
}
