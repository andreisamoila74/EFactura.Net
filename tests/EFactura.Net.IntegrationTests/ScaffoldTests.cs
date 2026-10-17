using EFactura.Net.Client;

namespace EFactura.Net.IntegrationTests;

/// Opt-in suite. Real tests arrive in Phase 4 and are gated on ANAF_* environment variables.
public class ScaffoldTests
{
    [Fact]
    public void Integration_project_builds()
    {
        Assert.Equal("EFactura.Net.Client", typeof(ClientAssemblyMarker).Assembly.GetName().Name);
    }
}
