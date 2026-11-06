namespace Sentinel.Tests.Integration.TestSupport;

using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Listeners;
using StackExchange.Redis;

public sealed class SentinelWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    internal static readonly string TestScheme = Generated.LowercaseToken(12);

    private readonly HostRoutingHandler _routing = new();
    private readonly string _accessToken;
    private readonly Uri _authority = new($"https://{Generated.NewHostname()}/");
    private FakeListenerHost? _toolListener;
    private FakeListenerHost? _masterListener;
    private FakeListenerHost? _misconfiguredListener;
    private bool _hostCreated;

    public SentinelWebApplicationFactory()
    {
        _accessToken = TokenEndpoint.AccessToken;
    }

    public ScriptedChatClient ChatClient { get; } = new();

    public FakeListenerHost ToolListener => _toolListener ?? throw new InvalidOperationException("The fixture is not initialized.");

    public FakeListenerHost MasterListener => _masterListener ?? throw new InvalidOperationException("The fixture is not initialized.");

    public FakeListenerHost MisconfiguredListener => _misconfiguredListener ?? throw new InvalidOperationException("The fixture is not initialized.");

    public Uri OfflineListenerAddress { get; } = new($"https://{Generated.NewHostname()}/");

    public string ToolListenerId { get; } = Generated.LowercaseToken(10);

    public string MasterListenerId { get; } = Generated.LowercaseToken(10);

    public string MisconfiguredListenerId { get; } = Generated.LowercaseToken(10);

    public string OfflineListenerId { get; } = Generated.LowercaseToken(10);

    public FeedPayloads Feeds { get; } = FeedFixtures.Create();

    public TokenEndpointHandler TokenEndpoint { get; } = new(AccessTokenFor());

    public async ValueTask InitializeAsync()
    {
        var toolAddress = new Uri($"https://{Generated.NewHostname()}/");
        var masterAddress = new Uri($"https://{Generated.NewHostname()}/");
        var misconfiguredAddress = new Uri($"https://{Generated.NewHostname()}/");
        _toolListener = await FakeListenerHost.StartAsync(ToolListenerId, toolAddress, _accessToken, toolAddress, Feeds);
        _masterListener = await FakeListenerHost.StartAsync(MasterListenerId, masterAddress, _accessToken, masterAddress, Feeds);
        _misconfiguredListener = await FakeListenerHost.StartAsync(MisconfiguredListenerId, misconfiguredAddress, _accessToken, new Uri($"https://{Generated.NewHostname()}/"), Feeds);
        _routing.Route(toolAddress, ToolListener.HttpHandler);
        _routing.Route(masterAddress, MasterListener.HttpHandler);
        _routing.Route(misconfiguredAddress, MisconfiguredListener.HttpHandler);
        _routing.Route(_authority, TokenEndpoint);
        await DeleteSentinelKeysAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_hostCreated)
        {
            await DeleteSentinelKeysAsync();
        }

        await base.DisposeAsync();
        await (_toolListener?.DisposeAsync() ?? ValueTask.CompletedTask);
        await (_masterListener?.DisposeAsync() ?? ValueTask.CompletedTask);
        await (_misconfiguredListener?.DisposeAsync() ?? ValueTask.CompletedTask);
        _routing.Dispose();
    }

    public ConversationWaiter Wait(ConversationKey conversation) =>
        new(Services.GetRequiredService<IConversationEvents>(), conversation);

    public T Hosted<T>()
        where T : IHostedService =>
        Services.GetServices<IHostedService>().OfType<T>().Single();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(Settings()));
        var host = base.CreateHost(builder);
        _hostCreated = true;
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var database = context.Configuration[SettingKeys.RedisDatabase];
            if (!int.TryParse(database, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index != IntegrationConstants.TestDatabase)
            {
                throw new InvalidOperationException(
                    $"The integration tier writes to the Redis database it is given, so it refuses '{database}': it must be the test database {IntegrationConstants.TestDatabase}.");
            }

            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(ChatClient);
            foreach (var name in new[] { ListenerHttpClientNames.Cards, ListenerHttpClientNames.Feeds, ListenerHttpClientNames.A2A, ListenerHttpClientNames.TokenClient })
            {
                services.Configure<HttpClientFactoryOptions>(name, options => options.HttpMessageHandlerBuilderActions.Add(handlerBuilder => handlerBuilder.PrimaryHandler = _routing));
            }

            services.AddAuthentication(TestScheme)
                .AddScheme<AuthenticationSchemeOptions, IntegrationAuthHandler>(TestScheme, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
                options.DefaultSignOutScheme = TestScheme;
            });
        });
    }

    private static string AccessTokenFor() => Generated.NewAccessToken();

    private static void AddListener(Dictionary<string, string?> settings, int index, string id, Uri baseAddress, ListenerKind kind)
    {
        var prefix = $"{SettingKeys.Listeners}:{index.ToString(CultureInfo.InvariantCulture)}:";
        settings[$"{prefix}{nameof(ListenerDefinition.Id)}"] = id;
        settings[$"{prefix}{nameof(ListenerDefinition.DisplayName)}"] = Generated.NewDisplayName();
        settings[$"{prefix}{nameof(ListenerDefinition.BaseAddress)}"] = baseAddress.AbsoluteUri;
        settings[$"{prefix}{nameof(ListenerDefinition.Kind)}"] = kind.ToString();
        settings[$"{prefix}{nameof(ListenerDefinition.Pinned)}"] = bool.FalseString;
    }

    private Dictionary<string, string?> Settings()
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            [SettingKeys.OidcAuthority] = _authority.AbsoluteUri,
            [SettingKeys.SentinelClientSecret] = Generated.NewSecretValue(),
            [SettingKeys.AnthropicApiKey] = Generated.NewSecretValue(),
        };
        AddListener(settings, 0, ToolListenerId, ToolListener.BaseAddress, ListenerKind.Tool);
        AddListener(settings, 1, MasterListenerId, MasterListener.BaseAddress, ListenerKind.Master);
        AddListener(settings, 2, MisconfiguredListenerId, MisconfiguredListener.BaseAddress, ListenerKind.Tool);
        AddListener(settings, 3, OfflineListenerId, OfflineListenerAddress, ListenerKind.Tool);
        return settings;
    }

    private async Task DeleteSentinelKeysAsync()
    {
        var database = Services.GetRequiredService<IDatabase>();
        if (database.Database != IntegrationConstants.TestDatabase)
        {
            return;
        }

        await database.PingAsync();
        foreach (var server in database.Multiplexer.GetServers())
        {
            await foreach (var key in server.KeysAsync(database.Database, $"{RedisKeys.Prefix}*"))
            {
                await database.KeyDeleteAsync(key);
            }
        }
    }
}
