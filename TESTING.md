# Testing

The Sentinel suite uses xUnit v3 on Microsoft Testing Platform and has two tiers: **unit tests**, which need nothing
outside the process, and **integration tests**, which run the whole app against a real Redis and in-process Agent
Framework A2A listeners.

Run every command from the repo root.

## Test Tiers

| Tier | Trait | Requires | Runs in CI | Floor |
|------|-------|----------|------------|-------|
| Unit | `Category=Unit` | Nothing | Every push and pull request | 202 |
| Integration | `Category=Integration` | Redis with a password, on database 1 | Push to `main` and manual dispatch | 20 |

The floor is the number of tests the tier must execute; fewer executed is a failure.

## Standards

- One sealed test class per unit under test, `Method_Scenario_Expectation` names, and the `// Arrange`, `// Act`,
  `// Assert` markers in unit tests only.
- No branching in a test body, and no sleeps or timeouts: background work is awaited through `TaskCompletionSource`,
  `FakeTimeProvider` or `TestContext.Current.CancellationToken`.
- Every value is generated through `Shared.Testing.Generated`; theory rows are literals or production constants.
- Mocks are Moq with `MockBehavior.Strict`.
- Components are tested with bUnit, selecting elements only by `id`.

## Running Tests Locally

### Unit Tests

```powershell
dotnet build Sentinel.Tests.Unit --configuration Debug
.\Sentinel.Tests.Unit\bin\Debug\net10.0\Sentinel.Tests.Unit.exe -trait "Category=Unit"
```

### Integration Tests

```powershell
$env:ASPNETCORE_ENVIRONMENT = "CI"
$env:RedisHost = "localhost"
$env:RedisPort = "6379"
$env:RedisSsl = "false"
$env:RedisPassword = "<your local requirepass>"
$env:RedisDatabase = "1"
dotnet build Sentinel.Tests.Integration --configuration Debug
.\Sentinel.Tests.Integration\bin\Debug\net10.0\Sentinel.Tests.Integration.exe -trait "Category=Integration"
```

The factory refuses any `RedisDatabase` other than 1, and deletes every `sentinel:*` key in that database before and
after the run. Nothing else outside the process is needed: no identity provider, no Anthropic key, no Azure
credentials.

## Test Infrastructure

| Part | What it does |
|---|---|
| `SentinelWebApplicationFactory` | Boots `Program` with four configured listeners (online tool, online master, misconfigured, offline), supplies the client secret, the Anthropic key and the listener list through host configuration, swaps the Claude chat client for `ScriptedChatClient`, routes every named listener HTTP client through `HostRoutingHandler`, and replaces sign-in with `IntegrationAuthHandler` |
| `FakeListenerHost`, `FakeListenerHandler` | Real Agent Framework A2A hosts on `TestServer` (`AddA2AServer` in `ReturnTask` mode, `MapA2AHttpJson`, `MapA2AJsonRpc`, `MapWellKnownAgentCard`) with a custom `IAgentHandler`: echo replies, `approve:` messages that go to `input-required` with the approval data part, `slow:` messages that wait for cancellation, and the feed endpoints. They reject any request without the client-credentials token. This is the reference listener ([LISTENERS.md](LISTENERS.md)) |
| `TokenEndpointHandler` | Answers the client-credentials token request, so the real `Duende.AccessTokenManagement` handler runs |
| `IntegrationAuthHandler` | Signs a request in from the `X-Integration-Operator` and `X-Integration-Role` headers; without them the request is challenged with 401 |
| `ConversationWaiter` | Completes when a conversation's run leaves `Running`, from the in-process event hub |
| `ScriptedChatClient` | Plays queued replies and tool calls to the orchestrator, so its real function-invocation and approval loop runs |

Unit-tier helpers live in `Sentinel.Tests.Unit/TestSupport`: `ComponentHarness` (a bUnit context with strict mocks of
every service the components inject, and a signed-in operator), `TelemetryHarness` and `CounterCapture` (metric
assertions), `TimerSignalingTimeProvider`, `StubHttpMessageHandler`, and the fixture builders.

## CI pipeline

1. Unit tests run under `coverlet` (OpenCover, branch coverage) on every trigger.
2. Integration tests run under `dotnet-coverage` (VS XML) against the self-hosted Redis named by `REDIS_HOST`, on
   database `REDIS_DATABASE_TEST`, with TLS.
3. SonarCloud reads both reports; `Program.cs` and `gate.ps1` are excluded from coverage.

## Local SonarCloud analysis

The gate runs the same `dotnet-sonarscanner begin` / build / `end` sequence as CI with `sonar.branch.name` set to
the local branch, then fails on any open issue on that branch.
