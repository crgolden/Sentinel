using System.Diagnostics;
using System.Net;
using Anthropic;
using Azure.Identity;
using Duende.AccessTokenManagement;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentinel;
using Sentinel.Approvals;
using Sentinel.Components;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Feeds;
using Sentinel.Health;
using Sentinel.Listeners;
using Sentinel.Orchestrator;
using Serilog;
using Shared.Extensions;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var oidcAuthority = builder.Configuration.GetRequired<Uri>(SettingKeys.OidcAuthority);
    string sentinelClientId = builder.Configuration.GetRequired<string>(SettingKeys.SentinelClientId),
        sentinelClientSecret = builder.Configuration.GetRequired<string>(SettingKeys.SentinelClientSecret),
        listenerScope = builder.Configuration.GetRequired<string>(SettingKeys.ListenerScope),
        anthropicApiKey = builder.Configuration.GetRequired<string>(SettingKeys.AnthropicApiKey);
    var redisOptions = new ConfigurationOptions
    {
        Ssl = builder.Configuration.GetRequired<bool>(SettingKeys.RedisSsl),
        EndPoints = [new DnsEndPoint(builder.Configuration.GetRequired<string>(SettingKeys.RedisHost), builder.Configuration.GetRequired<int>(SettingKeys.RedisPort))],
        AbortOnConnectFail = false,
        Password = builder.Configuration.GetRequired<string>(SettingKeys.RedisPassword),
    };
    var redisDatabase = builder.Configuration.GetRequired<int>(SettingKeys.RedisDatabase);
    var listeners = builder.Configuration.GetSection(SettingKeys.Listeners).Get<List<ListenerDefinition>>() ?? [];
    var orchestratorOptions = Section<OrchestratorOptions>(builder.Configuration);
    if (builder.Environment.IsProduction())
    {
        var defaultAzureCredentialOptions = Section<DefaultAzureCredentialOptions>(builder.Configuration);
        var tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
        Uri blobUri = builder.Configuration.GetRequired<Uri>(SettingKeys.BlobUri),
            dataProtectionKeyIdentifier = builder.Configuration.GetRequired<Uri>(SettingKeys.DataProtectionKeyIdentifier),
            elasticsearchNode = builder.Configuration.GetRequired<Uri>(SettingKeys.ElasticsearchNode),
            alloyEndpoint = builder.Configuration.GetRequired<Uri>(SettingKeys.AlloyEndpoint);
        string applicationName = builder.Configuration.GetRequired<string>(SettingKeys.WebsiteSiteName),
            hostname = builder.Configuration.GetRequired<string>(SettingKeys.WebsiteHostname),
            elasticsearchUsername = builder.Configuration.GetRequired<string>(SettingKeys.ElasticsearchUsername),
            elasticsearchPassword = builder.Configuration.GetRequired<string>(SettingKeys.ElasticsearchPassword);
        var keepaliveInterval = TimeSpan.FromMinutes(builder.Configuration.GetRequired<int>(SettingKeys.KeepaliveIntervalMinutes));
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options => options.Filter = context =>
            !context.Request.Path.StartsWithSegments(EndpointRoutes.Health, StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments(EndpointRoutes.Ping, StringComparison.OrdinalIgnoreCase));
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.WithProperty(nameof(IHostEnvironment.ApplicationName), applicationName)
                .WriteTo.Elasticsearch(
                    [elasticsearchNode],
                    elasticsearchSinkOptions =>
                    {
                        elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "app", nameof(Sentinel));
                        elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                        elasticsearchSinkOptions.TextFormatting.MapCustom = (ecsDocument, _) =>
                        {
                            ecsDocument.Service ??= new Elastic.CommonSchema.Service();
                            ecsDocument.Service.Name = applicationName;
                            return ecsDocument;
                        };
                    },
                    transportConfiguration => transportConfiguration.Authentication(new BasicAuthentication(elasticsearchUsername, elasticsearchPassword))))
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder
                .AddService(applicationName, null, typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
                }))
            .WithMetrics(meterProviderBuilder => meterProviderBuilder
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter(Telemetry.Metrics.MeterName)
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = alloyEndpoint))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .SetSampler(new AlwaysOnSampler())
                .AddSource(Telemetry.Metrics.SourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = alloyEndpoint))
            .Services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToAzureBlobStorage(blobUri, tokenCredential)
            .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
            .AddSingleton(new KeepaliveOptions(KeepaliveService.PingUri(hostname), keepaliveInterval))
            .AddHttpClient<KeepaliveService>().Services
            .AddHostedService<KeepaliveService>();
    }
    else
    {
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider))
            .AddDataProtection()
            .UseEphemeralDataProtectionProvider();
    }

    builder.Services
        .AddSingleton(TimeProvider.System)
        .AddSingleton(Options.Create(Section<TelemetryOptions>(builder.Configuration)))
        .AddSingleton(Options.Create(Section<ListenerRefreshOptions>(builder.Configuration)))
        .AddSingleton(Options.Create(Section<FeedRefreshOptions>(builder.Configuration)))
        .AddSingleton(Options.Create(Section<DispatchOptions>(builder.Configuration)))
        .AddSingleton(Options.Create(Section<ConversationOptions>(builder.Configuration)))
        .AddSingleton(Options.Create(Section<TranscriptTextOptions>(builder.Configuration)))
        .AddSingleton(Options.Create(orchestratorOptions))
        .AddSingleton<Telemetry>()
        .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions))
        .AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(redisDatabase))
        .AddSingleton(new ListenerRegistry(listeners))
        .AddSingleton<IListenerDirectory>(serviceProvider => serviceProvider.GetRequiredService<ListenerRegistry>())
        .AddSingleton<IAgentCardSource, A2AAgentCardSource>()
        .AddSingleton<IListenerAgentProvider, ListenerAgentCache>()
        .AddHostedService<ListenerRefreshService>()
        .AddSingleton<IFeedClient, FeedClient>()
        .AddSingleton<IFeedCache, RedisFeedCache>()
        .AddSingleton<IFeedReader>(serviceProvider => serviceProvider.GetRequiredService<IFeedCache>())
        .AddHostedService<FeedRefreshService>()
        .AddSingleton<IConversationStore, RedisConversationStore>()
        .AddSingleton<IConversationReader>(serviceProvider => serviceProvider.GetRequiredService<IConversationStore>())
        .AddSingleton<ConversationLocks>()
        .AddSingleton<ConversationEventHub>()
        .AddSingleton<IConversationEvents>(serviceProvider => serviceProvider.GetRequiredService<ConversationEventHub>())
        .AddSingleton<ConversationJournal>()
        .AddSingleton<IApprovalStore, RedisApprovalStore>()
        .AddSingleton<IAuditLog, RedisAuditLog>()
        .AddSingleton<IAuditReader>(serviceProvider => serviceProvider.GetRequiredService<IAuditLog>())
        .AddSingleton<ApprovalLedger>()
        .AddSingleton<IApprovalService, ApprovalService>()
        .AddSingleton<DispatchQueue>()
        .AddSingleton<IDispatchQueue>(serviceProvider => serviceProvider.GetRequiredService<DispatchQueue>())
        .AddSingleton<IListenerRunner, ListenerRunner>()
        .AddSingleton<ListenerToolsetFactory>()
        .AddSingleton<IOrchestratorRunner, OrchestratorRunner>()
        .AddHostedService<DispatchWorker>()
        .AddSingleton(new AnthropicClient { ApiKey = anthropicApiKey }.AsIChatClient(orchestratorOptions.Model, orchestratorOptions.MaxOutputTokens))
        .AddSingleton<IOrchestratorAgentFactory, OrchestratorAgentFactory>()
        .AddClientCredentialsTokenManagement()
        .AddClient(ListenerHttpClientNames.TokenClient, client =>
        {
            client.TokenEndpoint = new Uri(oidcAuthority, "connect/token");
            client.ClientId = ClientId.Parse(sentinelClientId);
            client.ClientSecret = ClientSecret.Parse(sentinelClientSecret);
            client.Scope = Scope.Parse(listenerScope);
            client.HttpClientName = ListenerHttpClientNames.TokenClient;
        }).Services
        .AddHttpClient(ListenerHttpClientNames.TokenClient).Services
        .AddHttpClient(ListenerHttpClientNames.Cards)
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false }).Services
        .AddHttpClient(ListenerHttpClientNames.Feeds)
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
        .AddClientCredentialsTokenHandler(ClientCredentialsClientName.Parse(ListenerHttpClientNames.TokenClient)).Services
        .AddHttpClient(ListenerHttpClientNames.A2A, httpClient => httpClient.Timeout = Timeout.InfiniteTimeSpan)
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
        .AddClientCredentialsTokenHandler(ClientCredentialsClientName.Parse(ListenerHttpClientNames.TokenClient)).Services
        .AddHealthChecks()
        .AddCheck<RedisHealthCheck>(RedisHealthCheck.Name).Services
        .AddRazorComponents()
        .AddInteractiveServerComponents().Services
        .AddCascadingAuthenticationState()
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.Authority = oidcAuthority.ToString();
            options.ClientId = sentinelClientId;
            options.ClientSecret = sentinelClientSecret;
            options.ResponseType = "code";
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;
            options.Scope.Add(AuthorizationPolicies.SentinelIdentityScope);
            options.ClaimActions.MapJsonKey(AuthorizationPolicies.RoleClaimType, AuthorizationPolicies.RoleClaimType);
            options.TokenValidationParameters.NameClaimType = AuthorizationPolicies.SubjectClaimType;
            options.TokenValidationParameters.RoleClaimType = AuthorizationPolicies.RoleClaimType;
        }).Services
        .AddAuthorizationBuilder()
        .AddPolicy(AuthorizationPolicies.Operator, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => AuthorizationPolicies.IsOperator(context.User)));

    var app = builder.Build();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, _) =>
        {
            if (Activity.Current is null)
            {
                return;
            }

            diagnosticContext.Set(nameof(Activity.TraceId), Activity.Current.TraceId.ToString());
            diagnosticContext.Set(nameof(Activity.SpanId), Activity.Current.SpanId.ToString());
        };
    });
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.MapHealthChecks(EndpointRoutes.Health, new HealthCheckOptions { Predicate = _ => false }).DisableHttpMetrics().AllowAnonymous();
    app.MapGet(EndpointRoutes.Ping, () => Results.Ok()).DisableHttpMetrics().AllowAnonymous();
    app.MapPost("/logout", () => Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
        .RequireAuthorization();
    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .RequireAuthorization(AuthorizationPolicies.Operator);
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static T Section<T>(IConfiguration configuration)
    where T : class =>
    configuration.GetRequiredSection(typeof(T).Name).Get<T>() ?? throw new InvalidOperationException($"Invalid '{typeof(T).Name}' configuration.");
