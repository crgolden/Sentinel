namespace Sentinel.Tests.Unit.TestSupport;

using System.Net;

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];
    private readonly TaskCompletionSource _firstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal IReadOnlyList<HttpRequestMessage> Requests => _requests;

    internal Task FirstRequest => _firstRequest.Task;

    internal static StubHttpMessageHandler Responding(HttpStatusCode statusCode, string content) =>
        new(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(content) });

    internal static StubHttpMessageHandler Throwing(Exception exception) => new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        _firstRequest.TrySetResult();
        return Task.FromResult(respond(request));
    }
}
