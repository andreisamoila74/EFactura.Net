using EFactura.Net.Client;
using Microsoft.Extensions.DependencyInjection;

namespace EFactura.Net.Samples.Console.Snippets;

/// <summary>
/// Reads the ANAF access token from an environment variable.
/// </summary>
/// <remarks>
/// Fine for a sample or a one-off script. A real application implements this over wherever it keeps
/// per-user tokens — a database row, a secret store — and refreshes them when they expire. The SDK
/// never stores or refreshes a token itself.
/// </remarks>
public sealed class EnvironmentTokenProvider : IAnafAccessTokenProvider
{
    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = Environment.GetEnvironmentVariable("ANAF_ACCESS_TOKEN")
            ?? throw new InvalidOperationException("ANAF_ACCESS_TOKEN is not set.");

        return ValueTask.FromResult(token);
    }
}

/// <summary>
/// A token provider over a hypothetical store, showing the shape a real one takes.
/// </summary>
public sealed class StoredTokenProvider(ITokenStore store, AnafOAuthClient oauth) : IAnafAccessTokenProvider
{
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var stored = await store.GetAsync(cancellationToken).ConfigureAwait(false);

        if (stored.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return stored.AccessToken;
        }

        // Expired, or close enough that it would expire mid-flight. Refresh and persist.
        var refreshed = await oauth.RefreshTokenAsync(stored.RefreshToken, cancellationToken).ConfigureAwait(false);

        await store.SaveAsync(
            refreshed.AccessToken,
            // ANAF does not always issue a new refresh token; null means keep the old one.
            refreshed.RefreshToken ?? stored.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn),
            cancellationToken).ConfigureAwait(false);

        return refreshed.AccessToken;
    }
}

/// <summary>The storage contract a real token provider would sit on.</summary>
public interface ITokenStore
{
    Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}

/// <summary>Wiring the SDK up, with and without a container.</summary>
public static class Configuration
{
    /// <summary>Registration with dependency injection.</summary>
    public static ServiceProvider WithDependencyInjection()
    {
        var services = new ServiceCollection();

        services.AddAnafEFactura(options =>
            {
                // Test is the default. Switch deliberately; documents sent to Production are real.
                options.Environment = AnafEnvironment.Test;
            })
            .AddAccessTokenProvider<EnvironmentTokenProvider>()
            .AddValidationClient()
            .AddCompanyLookup();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Registration with a resilience policy. The SDK bakes none in — how hard to retry a tax
    /// authority is the host's decision, so the builder hands back the <c>IHttpClientBuilder</c>.
    /// </summary>
    public static ServiceProvider WithResilience()
    {
        var services = new ServiceCollection();

        var builder = services.AddAnafEFactura()
            .AddAccessTokenProvider<EnvironmentTokenProvider>();

        builder.EFacturaHttpClientBuilder.ConfigureHttpClient(
            client => client.Timeout = TimeSpan.FromSeconds(100));

        // builder.EFacturaHttpClientBuilder.AddStandardResilienceHandler();
        //   ... with Microsoft.Extensions.Http.Resilience referenced.

        return services.BuildServiceProvider();
    }

    /// <summary>Construction without a container.</summary>
    public static AnafEFacturaClient WithoutDependencyInjection(HttpClient httpClient) =>
        new(httpClient, new AnafEFacturaOptions { Environment = AnafEnvironment.Test });

    /// <summary>
    /// The unauthenticated clients need no token provider at all, so they can be constructed with
    /// nothing but an <see cref="HttpClient"/>.
    /// </summary>
    public static (AnafValidationClient Validation, AnafCompanyLookupClient Lookup) PublicClients()
    {
        return (new AnafValidationClient(new HttpClient()), new AnafCompanyLookupClient(new HttpClient()));
    }
}
