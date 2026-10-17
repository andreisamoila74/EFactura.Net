using EFactura.Net.Client.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EFactura.Net.Client;

/// <summary>Registers the ANAF e-Factura clients with a dependency-injection container.</summary>
public static class AnafEFacturaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAnafEFacturaClient"/> as a typed <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the environment and logging.</param>
    /// <returns>A builder for registering the optional clients and the token provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// You must also register an <see cref="IAnafAccessTokenProvider"/> — via
    /// <see cref="IAnafEFacturaBuilder.AddAccessTokenProvider{T}"/> or directly — or resolving the
    /// client will fail.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddAnafEFactura(o => o.Environment = AnafEnvironment.Production)
    ///         .AddAccessTokenProvider&lt;MyTokenProvider&gt;()
    ///         .AddCompanyLookup()
    ///         .AddValidationClient()
    ///         .EFacturaHttpClientBuilder.AddStandardResilienceHandler();
    /// </code>
    /// </example>
    public static IAnafEFacturaBuilder AddAnafEFactura(
        this IServiceCollection services,
        Action<AnafEFacturaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new AnafEFacturaOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddTransient<AnafAccessTokenHandler>();

        var httpClientBuilder = services
            .AddHttpClient<IAnafEFacturaClient, AnafEFacturaClient>(client =>
                client.BaseAddress = options.ResolveBaseAddress())
            .AddHttpMessageHandler<AnafAccessTokenHandler>();

        return new AnafEFacturaBuilder(services, httpClientBuilder);
    }
}

/// <summary>Fluent configuration returned by <see cref="AnafEFacturaServiceCollectionExtensions.AddAnafEFactura"/>.</summary>
public interface IAnafEFacturaBuilder
{
    /// <summary>The underlying service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// The builder for the e-Factura typed client, so resilience, timeouts or extra handlers can be
    /// added.
    /// </summary>
    /// <remarks>
    /// The SDK deliberately configures no retry policy: how aggressively to retry a tax authority is
    /// the host's decision, not a library default.
    /// </remarks>
    IHttpClientBuilder EFacturaHttpClientBuilder { get; }

    /// <summary>Registers the token provider the e-Factura client authenticates with.</summary>
    /// <typeparam name="T">The implementation type.</typeparam>
    /// <returns>This builder.</returns>
    IAnafEFacturaBuilder AddAccessTokenProvider<T>()
        where T : class, IAnafAccessTokenProvider;

    /// <summary>Registers <see cref="IAnafValidationClient"/>. Needs no token.</summary>
    /// <param name="configure">Configures the endpoint.</param>
    /// <returns>This builder.</returns>
    IAnafEFacturaBuilder AddValidationClient(Action<AnafValidationOptions>? configure = null);

    /// <summary>Registers <see cref="IAnafCompanyLookupClient"/>. Needs no token.</summary>
    /// <param name="configure">Configures the endpoint.</param>
    /// <returns>This builder.</returns>
    IAnafEFacturaBuilder AddCompanyLookup(Action<AnafCompanyLookupOptions>? configure = null);

    /// <summary>Registers <see cref="AnafOAuthClient"/>.</summary>
    /// <param name="configure">Configures the client credentials and endpoints.</param>
    /// <returns>This builder.</returns>
    IAnafEFacturaBuilder AddOAuthClient(Action<AnafOAuthOptions> configure);
}

internal sealed class AnafEFacturaBuilder(IServiceCollection services, IHttpClientBuilder httpClientBuilder)
    : IAnafEFacturaBuilder
{
    public IServiceCollection Services { get; } = services;

    public IHttpClientBuilder EFacturaHttpClientBuilder { get; } = httpClientBuilder;

    public IAnafEFacturaBuilder AddAccessTokenProvider<T>()
        where T : class, IAnafAccessTokenProvider
    {
        Services.AddScoped<IAnafAccessTokenProvider, T>();

        return this;
    }

    public IAnafEFacturaBuilder AddValidationClient(Action<AnafValidationOptions>? configure = null)
    {
        var options = new AnafValidationOptions();
        configure?.Invoke(options);
        Services.AddSingleton(options);

        Services.AddHttpClient<IAnafValidationClient, AnafValidationClient>(client =>
            client.BaseAddress = options.BaseAddress);

        return this;
    }

    public IAnafEFacturaBuilder AddCompanyLookup(Action<AnafCompanyLookupOptions>? configure = null)
    {
        var options = new AnafCompanyLookupOptions();
        configure?.Invoke(options);
        Services.AddSingleton(options);

        Services.AddHttpClient<IAnafCompanyLookupClient, AnafCompanyLookupClient>();

        return this;
    }

    public IAnafEFacturaBuilder AddOAuthClient(Action<AnafOAuthOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AnafOAuthOptions();
        configure(options);
        Services.AddSingleton(options);

        Services.AddHttpClient<AnafOAuthClient>();

        return this;
    }
}
