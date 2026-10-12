namespace HslBikeApp.Http;

/// <summary>
/// Creates the <see cref="HttpClient"/> used for aggregator backend calls.
/// </summary>
public static class AggregatorHttpClient
{
    /// <summary>
    /// Upper bound for a single aggregator request. A cold backend normally answers well within
    /// this; anything slower is treated as a failure so callers can retry instead of waiting for
    /// the 100 s <see cref="HttpClient"/> default.
    /// </summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static HttpClient Create(HttpMessageHandler? innerHandler = null)
    {
        var handler = innerHandler is null
            ? new AppSourceHeaderHandler()
            : new AppSourceHeaderHandler(innerHandler);

        return new HttpClient(handler) { Timeout = RequestTimeout };
    }
}
