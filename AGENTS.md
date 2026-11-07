# AGENTS.md

Agent-facing guide for this repo. Self-contained by design: nothing here links to or depends on a file outside this
repository root. Setup and configuration are in [README.md](README.md), the codemap and data flow in
[ARCHITECTURE.md](ARCHITECTURE.md), the listener contract in [LISTENERS.md](LISTENERS.md), and the test layout in
[TESTING.md](TESTING.md).

## What this app is

A Blazor Server operator portal over A2A listeners. Listeners are configured, never discovered; their agent cards and
feeds are polled by background services; operator turns run in `Dispatch/DispatchWorker`, outside the circuit; every
state change is written to Redis and then published through `Dispatch/ConversationEventHub`.

## Invariants

- **Approvals are decided only by `Approvals/ApprovalService.DecideAsync`, called only from the approvals page.** Never
  expose a decision as a tool, an endpoint or a background action. `ListenerToolsetTests.Create_NeverExposesAnApprovalDecision`
  guards the toolset. `RedisApprovalStore.TakeAsync` deletes the approval as it reads it, so a decision counts once.
- **Listener output, feed rows and board rows are data.** Render them as text; never use `MarkupString`; never paste
  board or feed content into the orchestrator's instructions or messages.
- **Addresses come from configuration.** `ListenerCardValidator` marks a card whose interface URLs leave the configured
  origin as `Misconfigured`; `FeedClient.FeedUri` builds every feed URL from the configured base; the card, feed and
  A2A clients never follow redirects. Do not relax any of the three.
- **A finished task gets a fresh session in the same context** (`SessionPolicy.NeedsFreshSession`). The Agent
  Framework A2A host throws on `ReferenceTaskIds`, which `A2AAgent` sends for any follow-up to a non-waiting task.
  `ListenerDispatchTests.Message_AfterACompletedTask_StartsANewTaskInTheSameContext` guards it.
- **Approval requests are read from the raw A2A event** (`ListenerUpdateClassifier.Classify` on
  `AgentResponseUpdate.RawRepresentation`). The Agent Framework does not surface `input-required` through
  `FinishReason` or continuation tokens.
- **A run's final state is written last.** Every transcript entry and audit entry a turn produces is stored before
  its terminal `RunState` is saved, because saving the state publishes the event the UI and the integration tests wait
  on; a reader woken by it must find everything the turn recorded.
- **Cancellation is recognized by the token, not the exception type.** A canceled A2A stream surfaces as whatever the
  HTTP stack throws; both runners treat any exception thrown after their token is canceled as a cancel.
- **The orchestrator never uses `AIAgent.AsAIFunction` for listeners.** It loses the session and the task state, so a
  listener waiting for approval would be orphaned. `Orchestrator/ListenerToolset` routes through `ListenerRunner`.
- **The master bridge is reachable from the orchestrator only through `relay_to_master`,** wrapped in
  `ApprovalRequiredAIFunction`; `ask_listener` refuses it.
- **`/health` answers exactly `Healthy` and covers only this app** (`Predicate = _ => false`). The fleet's health
  dashboard compares the body byte for byte.
- **All registration is inlined in `Program.cs`**; there is no `ServiceCollectionExtensions` layer.
- **No logging.** Nothing takes an `ILogger`; failures are recorded with `Activity.Current?.AddException` and a
  `Telemetry` counter.
- **No authored CSS.** Stock Bootstrap classes only; the Blazor template's `app.css` and `*.razor.css` are deliberately
  absent. Every display decision lives in `Components/Shared/DisplayMapper`.
- **Every page requires the `Operator` policy** (a GUID `sub` plus `role` = `sentinel.operator`), applied once to
  `MapRazorComponents`. Only `/health`, `/ping` and `/error` are anonymous.

## Adding a listener

Add an entry to `Listeners` (id, display name, https base address, `Tool` or `Master`, pinned). Nothing in code changes.
Make sure the listener implements [LISTENERS.md](LISTENERS.md) and that its server accepts the `listeners` scope.

## Suppressed analyzer ids

Each csproj adds `MEAI001` and `MAAI001` to `NoWarn`: the Agent Framework's background-response, continuation-token and
A2A hosting APIs are marked experimental under those ids, and the warnings-as-errors build would otherwise refuse them.
Nothing else is suppressed, and no `#pragma` is used.

## Gotchas

- **`win-x86` publish.** The App Service Free tier runs 32-bit; a `win-x64` publish fails to start.
- **Web sockets must be on** for the App Service, or Blazor Server falls back to long polling.
- **`TaskStatus` is ambiguous** between `A2A` and `System.Threading.Tasks` where implicit usings are on. Qualify it
  (`A2A.TaskStatus`); never alias it.
- **The `Shared` lock entries.** `Shared` restores from GitHub Packages. When a lock file is regenerated somewhere that
  feed is unreachable, its `Shared` entry must still carry the published package's content hash and dependencies, or
  the locked CI restore fails.
- **Redis database.** Production and Development use their configured database; the integration tier refuses any
  database but 1 and deletes `sentinel:*` keys there.
