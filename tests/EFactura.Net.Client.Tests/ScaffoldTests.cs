namespace EFactura.Net.Client.Tests;

/// Proves the test host, both target frameworks and the project reference are wired up.
/// Replaced by real coverage in Phase 4.
public class ScaffoldTests
{
    [Fact]
    public void Client_assembly_is_referenced()
    {
        Assert.Equal("EFactura.Net.Client", typeof(ClientAssemblyMarker).Assembly.GetName().Name);
    }
}
