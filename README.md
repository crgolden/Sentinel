# Sentinel

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-sentinel](https://github.com/crgolden/Sentinel/actions/workflows/main_crgolden-sentinel.yml/badge.svg)](https://github.com/crgolden/Sentinel/actions/workflows/main_crgolden-sentinel.yml)

[![Quality gate](https://sonarcloud.io/api/project_badges/quality_gate?project=crgolden_Sentinel)](https://sonarcloud.io/summary/new_code?id=crgolden_Sentinel)

An always-on operator portal, built as an ASP.NET Core 10 Blazor Server app on the Microsoft Agent Framework. It
watches and talks to any number of **listeners**: .NET apps that each run on their own server, specialize in one tool
(Grafana, SQL Server, Redis and so on) and expose an Agent Framework agent over the
[A2A protocol](https://a2a-protocol.org). One listener can be the **master bridge**, which relays to an always-on
Claude Code session and publishes the coordination board and audit reports.

From one page the operator can:

- see every listener's status, skills and published health;
- chat with any listener directly;
- chat with **Sentinel**, a Claude orchestrator that decides which listeners to ask and combines their answers;
- approve or reject every risky action a listener or the orchestrator wants to take;
- browse the coordination board, the audit runs and reports, every artifact the listeners publish, and the audit log
  of operator actions.

Where to read next:

- [ARCHITECTURE.md](ARCHITECTURE.md): how the parts fit together and the rules each one keeps.
- [LISTENERS.md](LISTENERS.md): the contract a listener implements to join Sentinel.
- [TESTING.md](TESTING.md): how to run each test tier.
- [DESIGN.md](DESIGN.md): the UI's visual rules.
- [AGENTS.md](AGENTS.md): the invariants an agent editing this repo must keep.

## Architecture

```
Operator (browser, Blazor Server circuit)
  ├── Dashboard / listener pages ── IListenerDirectory ◄── ListenerRefreshService ── GET /.well-known/agent-card.json ──► listeners
  │                              └─ IFeedReader (Redis) ◄── FeedRefreshService ──────── GET /sentinel/v1/{status,artifacts,board} ──► listeners
  ├── Chat (listener or Sentinel) ── IDispatchQueue ──► DispatchWorker
  │                                                       ├── ListenerRunner ── A2A (HTTP+JSON, streaming) ──► listener
  │                                                       └── OrchestratorRunner ── Claude (Anthropic API) ── tools: list_listeners, ask_listener, relay_to_master
  └── Approvals ── IApprovalService ──► audit log (Redis stream) + IDispatchQueue (the decision resumes the waiting task)
```

**Operator authentication:** cookie plus OpenID Connect against the fleet's identity provider (`OidcAuthority`), as the
confidential client `sentinel`. Every page requires a GUID `sub` and the `role` claim
`sentinel.operator`.

**Listener authentication:** every A2A and feed request carries a client-credentials access token for the scope
`listeners`, minted by the same identity provider and cached by `Duende.AccessTokenManagement`.

**Long turns survive the browser:** runs execute in a background worker, not in the Blazor circuit. Every update is
written to Redis before it is pushed to the page, so a closed tab, a reconnect or a restart loses nothing but the
live stream.

## Technologies

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10, Blazor Server (interactive server render mode) |
| Agents | Microsoft Agent Framework 1.27 (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.A2A`, `Microsoft.Agents.AI.Anthropic`) |
| Agent protocol | A2A 1.0 (HTTP+JSON binding, streaming, task lifecycle) |
| Orchestrator model | Claude through the Anthropic API (`OrchestratorOptions:Model`) |
| Persistence | Redis (sessions, transcripts, runs, approvals, cached feeds, audit stream) |
| Auth | OpenID Connect (operators), OAuth 2.0 client credentials (listeners) |
| UI | Stock Bootstrap 5.3.8, no authored CSS |
| Observability | OpenTelemetry to Grafana Alloy (OTLP), Serilog to Elasticsearch |
| Hosting | Azure App Service (Windows, .NET 10, F1 plan, `win-x86`) |

## Prerequisites

| Tool | Notes |
|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | `global.json` pins `10.0.201` and rolls forward to the latest feature band |
| GitHub personal access token | `read:packages` scope, for the `Shared` package on the `crgolden` GitHub Packages feed (`NuGet.Config`). Store it as the `GitHub` source's credentials in your user NuGet configuration, never in this repo |
| Redis | Any Redis the app can reach, with a password |
| OpenID Connect provider | Registers the `sentinel` client, the `sentinel` identity resource carrying `role`, and the `listeners` API scope (see [Identity registration](#identity-registration)) |
| Anthropic API key | For the orchestrator. Not needed for the tests |

## Clone and build

```powershell
git clone https://github.com/crgolden/Sentinel.git
cd Sentinel
dotnet tool restore
dotnet build Sentinel.slnx
```

## Configuration

```powershell
dotnet user-secrets set "SentinelClientSecret" "<client secret>" --project Sentinel/Sentinel.csproj
dotnet user-secrets set "AnthropicApiKey" "<api key>" --project Sentinel/Sentinel.csproj
dotnet user-secrets set "RedisPassword" "<local redis requirepass>" --project Sentinel/Sentinel.csproj
```

These are the keys `Sentinel/appsettings.Development.json` leaves null. Listeners are configured as a list:

```json
"Listeners": [
  { "Id": "grafana", "DisplayName": "Grafana", "BaseAddress": "https://grafana-listener.example/", "Kind": "Tool", "Pinned": true },
  { "Id": "master", "DisplayName": "Master session", "BaseAddress": "https://master-listener.example/", "Kind": "Master", "Pinned": true }
]
```

On App Service the same list is written as `Listeners__0__Id`, `Listeners__0__BaseAddress` and so on.

| Key | Where | Notes |
|---|---|---|
| `OidcAuthority` | Development file, App Service | the identity provider's issuer URL |
| `SentinelClientId`, `SentinelClientSecret` | `appsettings.json`; secret in User Secrets or a Key Vault reference | one confidential client serves both the operator sign-in and the listener tokens |
| `ListenerScope` | `appsettings.json` | `listeners` |
| `AnthropicApiKey` | User Secrets, Key Vault reference | the orchestrator's key |
| `RedisHost`, `RedisPort`, `RedisSsl`, `RedisPassword`, `RedisDatabase` | Development file, User Secrets, App Service | every key Sentinel writes lives under `sentinel:` |
| `Listeners` | App Service | see above; ids are lowercase, unique, and never `sentinel` |
| `OrchestratorOptions` | `appsettings.json` | model, output budget, instructions and tool descriptions; the prose sent to the model is configuration |
| `ListenerRefreshOptions`, `FeedRefreshOptions`, `DispatchOptions`, `ConversationOptions` | `appsettings.json` | polling intervals, size limits, concurrency, transcript length |
| `TranscriptTextOptions`, `TelemetryOptions` | `appsettings.json` | the text Sentinel writes into transcripts, and the metric descriptions |
| Production only: `AlloyEndpoint`, `ElasticsearchNode`, `ElasticsearchUsername`, `ElasticsearchPassword`, `BlobUri`, `DataProtectionKeyIdentifier`, `DefaultAzureCredentialOptions`, `KeepaliveIntervalMinutes` | App Service | telemetry, data protection and the Free-tier keepalive |

## Run locally

```bash
dotnet run --project Sentinel/Sentinel.csproj
```

The app starts at `https://localhost:7180`. `/health` (body exactly `Healthy`) and `/ping` are anonymous; every page
needs an operator sign-in.

## Project Structure

```
Sentinel/                     # the portal: listeners, feeds, dispatch, approvals, orchestrator, Blazor UI
Sentinel.Tests.Unit/          # xUnit v3 + Moq + bUnit, Category=Unit, no external dependencies
Sentinel.Tests.Integration/   # xUnit v3, Category=Integration: real Redis and in-process Agent Framework A2A listeners
```

## Tests

```powershell
dotnet build Sentinel.Tests.Unit --configuration Debug
.\Sentinel.Tests.Unit\bin\Debug\net10.0\Sentinel.Tests.Unit.exe -trait "Category=Unit"

$env:ASPNETCORE_ENVIRONMENT = "CI"
$env:RedisHost = "localhost"; $env:RedisPort = "6379"; $env:RedisSsl = "false"; $env:RedisPassword = "<password>"; $env:RedisDatabase = "1"
dotnet build Sentinel.Tests.Integration --configuration Debug
.\Sentinel.Tests.Integration\bin\Debug\net10.0\Sentinel.Tests.Integration.exe -trait "Category=Integration"
```

See [TESTING.md](TESTING.md) for the full guide.

## Gate

`gate.ps1` is the maintainers' gate. It dot-sources the gate library script that the `GATE_LIBRARY` environment
variable names, which defines the step functions `gate.ps1` calls; unset, as in a plain clone, it stops at once with
`GATE: FAILED` and records nothing. To gate a change without that library, run the checks CI runs, in this order:

```powershell
dotnet tool restore
dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
dotnet jb inspectcode Sentinel.slnx --no-build -e=WARNING --output=inspectcode.sarif
dotnet test --project Sentinel.Tests.Unit --no-build --configuration Release -- --filter-trait "Category=Unit"
dotnet test --project Sentinel.Tests.Integration --no-build --configuration Release -- --filter-trait "Category=Integration"
```

`jb inspectcode` passes only when `inspectcode.sarif` holds no results; the integration tier needs Redis on database 1.

## Identity registration

The identity provider needs, before the first deployment:

- a confidential client `sentinel` with the `authorization_code` grant (PKCE required), redirect `https://<site>/signin-oidc`,
  post-logout redirect `https://<site>/signout-callback-oidc`, scopes `openid`, `profile` and `sentinel`, and the
  `client_credentials` grant for the scope `listeners`;
- an identity resource `sentinel` whose user claim is `role`, and the operator's account holding `role` =
  `sentinel.operator`;
- an API scope `listeners`, which every listener requires on the tokens it accepts.

## Deployment

The GitHub Actions workflow runs on pushes to `main`, pull requests and manual dispatch.

1. Builds the solution under a SonarCloud analysis, then runs `jb inspectcode`.
2. Runs the unit tests with coverage.
3. Runs the integration tests against the Redis named by `REDIS_HOST` (on push to `main` and `workflow_dispatch`).
4. Publishes the app for `win-x86` (the Free tier runs 32-bit) and uploads the artifact.
5. Ends the SonarCloud analysis and fails on any open Sonar issue.
6. On anything but a pull request, signs in to Azure with OpenID Connect and deploys to the `crgolden-sentinel` App
   Service.

The App Service must have **Web sockets** turned on: Blazor Server falls back to long polling without them, which costs
far more of the Free tier's CPU quota. The Free tier has no Always On, so `KeepaliveService` pings `/ping` every
`KeepaliveIntervalMinutes`.

Repository secrets: `SONAR_TOKEN`, `PACKAGES_READ_TOKEN`, `REDIS_PASSWORD`, `AZUREAPPSERVICE_CLIENTID`,
`AZUREAPPSERVICE_TENANTID`, `AZUREAPPSERVICE_SUBSCRIPTIONID`. Repository variables: `SONAR_PROJECT_KEY`,
`SONAR_ORGANIZATION`, `REDIS_HOST`, `REDIS_PORT`, `REDIS_DATABASE_TEST`.
