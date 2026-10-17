using Microsoft.Extensions.DependencyInjection;

namespace EFactura.Net.Client.Tests;

internal sealed class NoopTokenProvider : IAnafAccessTokenProvider
{
    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult("token");
}

public class DependencyInjectionTests
{
    [Fact]
    public void Registers_a_resolvable_efactura_client()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura().AddAccessTokenProvider<NoopTokenProvider>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnafEFacturaClient>());
    }

    [Fact]
    public void Applies_the_configured_environment_to_the_base_address()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura(o => o.Environment = AnafEnvironment.Production)
            .AddAccessTokenProvider<NoopTokenProvider>();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<AnafEFacturaOptions>();

        Assert.Equal(AnafEnvironment.Production, options.Environment);
        Assert.Equal(new Uri("https://api.anaf.ro/prod/FCTEL/rest/"), options.ResolveBaseAddress());
    }

    [Fact]
    public void Defaults_to_the_test_environment_when_unconfigured()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura().AddAccessTokenProvider<NoopTokenProvider>();

        using var provider = services.BuildServiceProvider();

        Assert.Equal(AnafEnvironment.Test, provider.GetRequiredService<AnafEFacturaOptions>().Environment);
    }

    [Fact]
    public void Optional_clients_are_not_registered_unless_asked_for()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura().AddAccessTokenProvider<NoopTokenProvider>();

        using var provider = services.BuildServiceProvider();

        // A consumer who only generates and uploads should not get three typed clients.
        Assert.Null(provider.GetService<IAnafValidationClient>());
        Assert.Null(provider.GetService<IAnafCompanyLookupClient>());
        Assert.Null(provider.GetService<AnafOAuthClient>());
    }

    [Fact]
    public void Registers_the_validation_client_on_request()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura().AddAccessTokenProvider<NoopTokenProvider>().AddValidationClient();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAnafValidationClient>());
    }

    [Fact]
    public void Registers_the_company_lookup_on_request()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura().AddAccessTokenProvider<NoopTokenProvider>().AddCompanyLookup();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAnafCompanyLookupClient>());
    }

    [Fact]
    public void Registers_the_oauth_client_on_request()
    {
        var services = new ServiceCollection();
        services.AddAnafEFactura()
            .AddAccessTokenProvider<NoopTokenProvider>()
            .AddOAuthClient(o =>
            {
                o.ClientId = "id";
                o.ClientSecret = "secret";
                o.RedirectUri = new Uri("https://example.test/cb");
            });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<AnafOAuthClient>());
        Assert.Equal("id", provider.GetRequiredService<AnafOAuthOptions>().ClientId);
    }

    [Fact]
    public void The_builder_chains()
    {
        var services = new ServiceCollection();

        var builder = services.AddAnafEFactura()
            .AddAccessTokenProvider<NoopTokenProvider>()
            .AddValidationClient()
            .AddCompanyLookup()
            .AddOAuthClient(o =>
            {
                o.ClientId = "id";
                o.ClientSecret = "secret";
                o.RedirectUri = new Uri("https://example.test/cb");
            });

        Assert.NotNull(builder.Services);
        Assert.NotNull(builder.EFacturaHttpClientBuilder);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnafEFacturaClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnafValidationClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnafCompanyLookupClient>());
    }

    /// <summary>
    /// The builder hands back the IHttpClientBuilder precisely so resilience stays the host's call.
    /// </summary>
    [Fact]
    public void Exposes_the_http_client_builder_for_consumer_policies()
    {
        var services = new ServiceCollection();

        var builder = services.AddAnafEFactura().AddAccessTokenProvider<NoopTokenProvider>();
        builder.EFacturaHttpClientBuilder.ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(42));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnafEFacturaClient>());
    }

    [Fact]
    public void Rejects_a_null_service_collection()
    {
        Assert.Throws<ArgumentNullException>(
            () => AnafEFacturaServiceCollectionExtensions.AddAnafEFactura(null!));
    }
}
