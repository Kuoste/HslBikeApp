namespace HslBikeApp.Http;

/// <summary>
/// HTTP message handler that adds the X-App-Source header to all outgoing requests.
/// This header is used by Azure API Management for casual-abuse filtering.
/// </summary>
public sealed class AppSourceHeaderHandler : DelegatingHandler
{
    private const string HeaderName = "X-App-Source";
    private const string HeaderValue = "HslBikeApp";

    public AppSourceHeaderHandler()
    {
        InnerHandler = new HttpClientHandler();
    }

    public AppSourceHeaderHandler(HttpMessageHandler innerHandler)
    {
        InnerHandler = innerHandler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Add(HeaderName, HeaderValue);
        return base.SendAsync(request, cancellationToken);
    }
}
