using Microsoft.Extensions.Logging;

namespace EFactura.Net.Client.Internal;

/// <summary>
/// Source-generated logging delegates. Using these instead of the <c>ILogger.LogX</c> extension
/// methods avoids boxing and the params array on every call, including when the level is disabled.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "ANAF {Operation} response: {Body}")]
    internal static partial void AnafResponseBody(ILogger logger, string operation, string body);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "ANAF validare responded with {Length} characters.")]
    internal static partial void ValidationResponse(ILogger logger, int length);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Looked up {Count} tax identifiers.")]
    internal static partial void CompanyLookupBatch(ILogger logger, int count);
}
