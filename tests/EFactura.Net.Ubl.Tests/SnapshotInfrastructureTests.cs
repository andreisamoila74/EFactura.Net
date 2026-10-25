namespace EFactura.Net.Ubl.Tests;

/// <summary>
/// Guards the snapshot plumbing itself.
/// </summary>
/// <remarks>
/// These exist because of a real failure: the directory used to be derived from
/// <c>[CallerFilePath]</c>, which a deterministic CI build rewrites to a placeholder such as
/// <c>/_/tests/…</c>. Every snapshot test then wrote to — and compared against — a directory that
/// was not the repository, which on Windows silently "passed" the write and failed the compare.
/// A broken resolution now fails here, once and legibly, instead of thirteen times with an
/// unrelated-looking I/O error.
/// </remarks>
public class SnapshotInfrastructureTests
{
    [Fact]
    public void Snapshot_directory_resolves_to_a_real_directory()
    {
        var directory = Snapshot.SnapshotDirectory.Value;

        Assert.True(
            Directory.Exists(directory),
            $"Snapshot directory '{directory}' does not exist. If it looks like a placeholder "
            + "such as '/_/tests/...', the path was normalised by a deterministic build.");
    }

    [Fact]
    public void Snapshot_directory_sits_inside_the_test_project()
    {
        var directory = Snapshot.SnapshotDirectory.Value;

        Assert.EndsWith(
            Path.Combine("EFactura.Net.Ubl.Tests", "Snapshots"),
            directory.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every scenario must already have a committed snapshot. Without this, a scenario whose file
    /// was never committed would quietly write one and report a pass on the next run.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDocuments.ScenarioNames), MemberType = typeof(TestDocuments))]
    public void Every_scenario_has_a_committed_snapshot(string scenario)
    {
        var path = Path.Combine(Snapshot.SnapshotDirectory.Value, $"{scenario}.verified.xml");

        Assert.True(File.Exists(path), $"No committed snapshot at '{path}'.");
        Assert.NotEmpty(File.ReadAllText(path));
    }
}
