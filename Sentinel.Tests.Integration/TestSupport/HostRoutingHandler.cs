namespace Sentinel.Tests.Integration.TestSupport;

using System.Collections.Concurrent;
using System.Net;

public sealed class HostRoutingHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, HttpMessageInvoker> _routes = new(StringComparer.OrdinalIgnoreCase);

    public void Route(Uri baseAddress, HttpMessageHandler handler) => _routes[baseAddress.Authority] = new HttpMessageInvoker(handler, false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        request.RequestUri is { } uri && _routes.TryGetValue(uri.Authority, out var invoker)
            ? invoker.SendAsync(request, cancellationToken)
            : throw new HttpRequestException($"No route to {request.RequestUri}.", null, HttpStatusCode.ServiceUnavailable);

    protected override void Dispose(bool disposing)
    {
        foreach (var invoker in _routes.Values)
        {
            invoker.Dispose();
        }

        base.Dispose(disposing);
    }
}
