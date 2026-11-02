namespace Sentinel.Tests.Integration.TestSupport;

using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentinel.Feeds;

public sealed class FakeListenerHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly RequestCounter _unauthorized;

    private FakeListenerHost(WebApplication app, Uri baseAddress, FakeListenerHandler handler, RequestCounter unauthorized)
    {
        _app = app;
        _unauthorized = unauthorized;
        BaseAddress = baseAddress;
        Handler = handler;
    }

    public Uri BaseAddress { get; }

    public FakeListenerHandler Handler { get; }

    public int UnauthorizedRequests => _unauthorized.Count;

    public HttpMessageHandler HttpHandler => _app.GetTestServer().CreateHandler();

    public static async Task<FakeListenerHost> StartAsync(string name, Uri baseAddress, string accessToken, Uri interfaceAddress, FeedPayloads feeds)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(options => options.BaseAddress = baseAddress);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var handler = new FakeListenerHandler();
        AIAgent agent = new ChatClientAgent(new ScriptedChatClient(), name: name);
        builder.Services.AddKeyedSingleton<IAgentHandler>(name, handler);
        builder.Services.AddA2AServer(agent, options =>
        {
            options.AgentRunMode = AgentRunMode.ReturnTask;
            options.ServerOptions = new A2AServerOptions { SupportsStreaming = true, SupportedInputModes = ["text/plain", "application/json"] };
        });
        var app = builder.Build();
        var unauthorized = new RequestCounter();
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/.well-known", StringComparison.Ordinal)
                && !string.Equals(context.Request.Headers.Authorization.ToString(), $"Bearer {accessToken}", StringComparison.Ordinal))
            {
                unauthorized.Increment();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        });
        app.MapA2AHttpJson(agent, "/a2a");
        app.MapA2AJsonRpc(agent, "/a2a");
        app.MapWellKnownAgentCard(new AgentCard
        {
            Name = name,
            Description = $"{name} listener",
            Version = "1.0.0",
            Capabilities = new AgentCapabilities { Streaming = true, Extensions = [new AgentExtension { Uri = FeedContract.ExtensionUri }] },
            DefaultInputModes = ["text/plain"],
            DefaultOutputModes = ["text/plain"],
            Skills = [new A2A.AgentSkill { Id = name, Name = name, Description = name, Tags = [name] }],
            SupportedInterfaces =
            [
                new AgentInterface { Url = new Uri(interfaceAddress, "a2a").AbsoluteUri, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = "1.0" },
                new AgentInterface { Url = new Uri(interfaceAddress, "a2a").AbsoluteUri, ProtocolBinding = ProtocolBindingNames.JsonRpc, ProtocolVersion = "1.0" },
            ],
        });
        app.MapGet($"/{FeedContract.StatusPath}", () => Results.Text(feeds.Status, "application/json"));
        app.MapGet($"/{FeedContract.ArtifactsPath}", () => Results.Text(feeds.Artifacts, "application/json"));
        app.MapGet($"/{FeedContract.BoardPath}", () => Results.Text(feeds.Board, "application/json"));
        await app.StartAsync();
        return new FakeListenerHost(app, baseAddress, handler, unauthorized);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
