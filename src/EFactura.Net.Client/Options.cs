namespace EFactura.Net.Client;

/// <summary>Configuration for <see cref="IAnafEFacturaClient"/>.</summary>
public sealed class AnafEFacturaOptions
{
    /// <summary>Base address of the ANAF test environment.</summary>
    public static Uri TestBaseAddress { get; } = new("https://api.anaf.ro/test/FCTEL/rest/");

    /// <summary>Base address of the ANAF production environment.</summary>
    public static Uri ProductionBaseAddress { get; } = new("https://api.anaf.ro/prod/FCTEL/rest/");

    /// <summary>
    /// Which environment to talk to. Defaults to <see cref="AnafEnvironment.Test"/>.
    /// </summary>
    public AnafEnvironment Environment { get; set; } = AnafEnvironment.Test;

    /// <summary>
    /// Overrides the base address derived from <see cref="Environment"/>. Leave <see langword="null"/>
    /// unless you are pointing at a proxy or a recorded fixture server.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// When <see langword="true"/>, response bodies are logged at <c>Debug</c>.
    /// </summary>
    /// <remarks>
    /// Off by default. ANAF responses carry counterparty identifiers and document contents, so this
    /// is diagnostic-only and should not be left on in production.
    /// </remarks>
    public bool LogResponseBodies { get; set; }

    /// <summary>The effective base address — <see cref="BaseAddress"/> if set, else by environment.</summary>
    public Uri ResolveBaseAddress() => BaseAddress ?? Environment switch
    {
        AnafEnvironment.Production => ProductionBaseAddress,
        _ => TestBaseAddress,
    };
}

/// <summary>Configuration for <see cref="AnafOAuthClient"/>.</summary>
public sealed class AnafOAuthOptions
{
    /// <summary>OAuth2 client id issued by ANAF for this environment.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>OAuth2 client secret issued by ANAF for this environment.</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Redirect URI. Must match the value registered in the ANAF SPV portal character for character.
    /// </summary>
    public Uri? RedirectUri { get; set; }

    /// <summary>Authorization endpoint. Defaults to ANAF's.</summary>
    public Uri AuthorizeEndpoint { get; set; } = new("https://logincert.anaf.ro/anaf-oauth2/v1/authorize");

    /// <summary>Token endpoint. Defaults to ANAF's.</summary>
    public Uri TokenEndpoint { get; set; } = new("https://logincert.anaf.ro/anaf-oauth2/v1/token");
}

/// <summary>Configuration for <see cref="IAnafValidationClient"/>.</summary>
public sealed class AnafValidationOptions
{
    /// <summary>
    /// Base address of the public validation service. These endpoints need no authentication.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://webservicesp.anaf.ro/prod/FCTEL/rest/");
}

/// <summary>Configuration for <see cref="IAnafCompanyLookupClient"/>.</summary>
public sealed class AnafCompanyLookupOptions
{
    /// <summary>
    /// Address of the public VAT-payer registry endpoint. Needs no authentication.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva");

    /// <summary>
    /// Maximum identifiers per request. ANAF caps a batch at 500; the client splits larger inputs.
    /// </summary>
    public int BatchSize { get; set; } = 500;
}
