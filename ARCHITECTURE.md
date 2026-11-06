# Architecture

## Bird's-eye view

One ASP.NET Core app, `Sentinel`, serves a Blazor Server operator portal. It keeps a registry of configured
listeners, refreshes each one's A2A agent card and published feeds on a timer, and turns operator messages into A2A
turns run by a background worker. A Claude orchestrator agent can ask listeners on the operator's behalf through tools.
Anything a listener or the orchestrator wants to do that needs a human waits as a pending approval until the operator
decides. Redis holds every piece of state that must outlive a browser tab or a restart.

## Codemap

| Part | What it does |
|---|---|
| `Sentinel/Program.cs` | configuration, Redis, the named HTTP clients and the listener token handler, the Anthropic chat client, OIDC sign-in and the `Operator` policy, health checks, Blazor, and the production-only telemetry, data protection and keepalive |
| `Sentinel/Listeners` | `ListenerCatalog` (configuration rules), `ListenerRegistry` (the live snapshot per listener), `ListenerRefreshService` (agent cards on a timer), `ListenerCardValidator` (origin pinning, feed support), `ListenerAgentCache` (one `A2AAgent` per card) |
| `Sentinel/Feeds` | the listener feed contract records, `FeedClient` (size-capped GETs against the configured address), `RedisFeedCache`, `FeedRefreshService` |
| `Sentinel/Conversations` | `ConversationKey` (operator + target), transcript and run records, `RedisConversationStore`, `SessionPolicy`, `ConversationLocks` |
| `Sentinel/Dispatch` | `DispatchQueue` and `DispatchWorker` (runs outside the circuit), `ListenerRunner` (one A2A turn), `ListenerUpdateClassifier` (task state and approval requests from raw A2A events), `ConversationJournal` and `ConversationEventHub` (write to Redis, then publish) |
| `Sentinel/Approvals` | the approval contract, `ApprovalService` (the only place a decision is made), `ApprovalLedger`, `RedisApprovalStore`, `RedisAuditLog` |
| `Sentinel/Orchestrator` | `OrchestratorAgentFactory` (a `ChatClientAgent` over Claude), `ListenerToolset` (`list_listeners`, `ask_listener`, the approval-gated `relay_to_master`), `OrchestratorRunner` |
| `Sentinel/Health` | `RedisHealthCheck` (registered, not part of `/health`), `KeepaliveService` |
| `Sentinel/Components` | `App`, `Routes`, `MainLayout`, the pages and the shared components; `DisplayMapper` holds every display decision |
| `Sentinel.Tests.Unit`, `Sentinel.Tests.Integration` | the two test tiers ([TESTING.md](TESTING.md)) |
| `.github/workflows/main_crgolden-sentinel.yml` | build, inspect, test, analyze and deploy |
| `NuGet.Config`, `packages.lock.json` files, `dotnet-tools.json`, `global.json`, `coverage.settings.xml` | package sources (`Shared` from GitHub Packages), locked restore, pinned tools, the SDK, integration coverage settings |
| `gate.ps1` | the maintainers' local gate (README.md, "Gate") |

## Data flow of one turn

1. The chat panel enqueues a `DispatchCommand` and returns at once.
2. `DispatchWorker` takes it, links a per-conversation cancellation source, and calls `ListenerRunner` (or
   `OrchestratorRunner` for the `sentinel` target).
3. `ListenerRunner` takes the conversation's lock, restores the serialized `A2AAgentSession` from Redis, starts a fresh
   session in the same A2A context when the last task finished, and streams `RunStreamingAsync` with the operator's id
   in the request metadata.
4. Every text delta is published to the circuit; every transcript entry and run state is written to Redis first and
   published second.
5. A task that ends in `input-required` with an approval data part becomes a `PendingApproval`. The operator's
   decision is sent back as a data part on the same task, which the listener resumes.

## Invariants

- **Only the UI decides an approval.** `ApprovalService.DecideAsync` is called from the approvals page and nowhere
  else; no tool the orchestrator sees can approve anything, and a unit test asserts the toolset's names.
- **Listener and feed content is data.** It is rendered as encoded text, never as `MarkupString`, and board or feed
  content is never put into the orchestrator's context.
- **Listener addresses come from configuration, never from a card.** A card whose interface URLs leave the configured
  origin, or are not https, marks the listener `Misconfigured` and is not used; feed URLs are always built from the
  configured base address; redirects are never followed.
- **A finished task never receives a follow-up.** After a terminal task state the next message starts a new session in
  the same context, because the Agent Framework A2A host rejects `ReferenceTaskIds`.
- **Runs belong to the worker, not the circuit.** A turn keeps going when the tab closes; on startup the worker resumes
  a run that holds a continuation token and marks the rest `Interrupted`.
- **The master bridge is reached only through `relay_to_master`**, which needs the operator's approval;
  `ask_listener` refuses the master.
- **`/health` reports only this app.** It answers exactly `Healthy`, the contract the fleet's health dashboard checks,
  and does not include Redis or any listener.
- **Prose is configuration.** The orchestrator's instructions, tool descriptions and the text Sentinel writes into
  transcripts come from `appsettings.json`.

## Boundaries

- Sentinel calls the listeners (A2A, agent cards, feeds), the identity provider (sign-in and listener tokens), the
  Anthropic API (orchestrator only) and Redis. Nothing calls Sentinel but the operator's browser and the keepalive.
- Listeners trust a token for the `listeners` scope; Sentinel adds the operator's id as `sentinel.operator` request
  metadata so a listener can audit who asked.

## Cross-cutting concerns

- **Telemetry** is production-only: OpenTelemetry traces (with the `Sentinel` activity source and the Agent Framework
  spans) and metrics over OTLP, and Serilog to an Elasticsearch data stream. Background polls suppress their own
  instrumentation.
- **Free tier**: 32-bit `win-x86`, no Always On (the keepalive), a daily CPU quota (modest refresh intervals) and a small
  WebSocket allowance (one or two operator tabs).
- **Warnings are errors**, and CI also fails on any `jb inspectcode` result and any open SonarCloud issue.
