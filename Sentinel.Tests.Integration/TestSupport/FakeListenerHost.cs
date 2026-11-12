namespace Sentinel.Tests.Integration.TestSupport;

using System.Net.Http.Headers;
using System.Net.Mime;
using A2A;
using A2A.AspNetCore;
using Duende.IdentityModel;
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
            options.ServerOptions = new A2AServerOptions { SupportsStreaming = true, SupportedInputModes = [MediaTypeNames.Text.Plain, MediaTypeNames.Application.Json] };
        });
        var app = builder.Build();
        var unauthorized = new RequestCounter();
        var expectedAuthorization = new AuthenticationHeaderValue(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, accessToken).ToString();
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(A2AConstants.WellKnownPathPrefix, StringComparison.Ordinal)
                && !string.Equals(context.Request.Headers.Authorization.ToString(), expectedAuthorization, StringComparison.Ordinal))
            {
                unauthorized.Increment();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        });
        var interfacePath = Generated.NewRoutePrefix();
        var interfaceUrl = new Uri(interfaceAddress, interfacePath).AbsoluteUri;
        app.MapA2AHttpJson(agent, $"/{interfacePath}");
        app.MapA2AJsonRpc(agent, $"/{interfacePath}");
        app.MapWellKnownAgentCard(new AgentCard
        {
            Name = name,
            Description = Generated.NewDescription(),
            Version = new Version(Generated.NewCount(), Generated.NewCount(), Generated.NewCount()).ToString(),
            Capabilities = new AgentCapabilities { Streaming = true, Extensions = [new AgentExtension { Uri = FeedContract.ExtensionUri }] },
            DefaultInputModes = [MediaTypeNames.Text.Plain],
            DefaultOutputModes = [MediaTypeNames.Text.Plain],
            Skills = [new A2A.AgentSkill { Id = name, Name = name, Description = name, Tags = [name] }],
            SupportedInterfaces =
            [
                new AgentInterface { Url = interfaceUrl, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = A2AConstants.ProtocolVersion },
                new AgentInterface { Url = interfaceUrl, ProtocolBinding = ProtocolBindingNames.JsonRpc, ProtocolVersion = A2AConstants.ProtocolVersion },
            ],
        });
        app.MapGet($"/{FeedContract.StatusPath}", () => Results.Text(feeds.Status, MediaTypeNames.Application.Json));
        app.MapGet($"/{FeedContract.ArtifactsPath}", () => Results.Text(feeds.Artifacts, MediaTypeNames.Application.Json));
        app.MapGet($"/{FeedContract.BoardPath}", () => Results.Text(feeds.Board, MediaTypeNames.Application.Json));
        await app.StartAsync();
        return new FakeListenerHost(app, baseAddress, handler, unauthorized);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
