# Listener contract

A **listener** is a .NET app, on its own server, that specializes in one tool and exposes a Microsoft Agent Framework
agent over the [A2A protocol](https://a2a-protocol.org) 1.0. Sentinel finds a listener only through configuration (an
id, a display name and an https base address) and talks to it only through what this contract defines. Everything
below is required unless marked optional.

`Sentinel.Tests.Integration/TestSupport/FakeListenerHost.cs` and `FakeListenerHandler.cs` are a working reference: an
Agent Framework A2A host with a custom handler that implements every part of this contract, exercised by the
integration tier on every run.

## 1. Agent card

Serve the card at `GET {base}/.well-known/agent-card.json`, anonymously (`MapWellKnownAgentCard`).

- Every entry in `supportedInterfaces` uses **https** and the **same scheme, host and port as the configured base
  address**. A card that points anywhere else is ignored and the listener shows as *Misconfigured*. Sentinel never
  follows redirects.
- Offer the `HTTP+JSON` binding with protocol version `1.0`; JSON-RPC may be offered too.
- Set `capabilities.streaming` to `true`, and run the agent with `AgentRunMode.ReturnTask`, so that every turn is a task
  Sentinel can follow, cancel and resume.
- Describe the listener in `description` and list what it can do in `skills`. Sentinel shows both on the dashboard
  and gives them to the orchestrator, which uses them to decide whom to ask.
- To publish feeds (section 5), add the extension `{"uri": "urn:crgolden:sentinel:feeds:v1"}` to
  `capabilities.extensions`.
- Change `version` when the card changes; Sentinel rebuilds its client when it does.

```csharp
builder.Services.AddA2AServer(agent, options =>
{
    options.AgentRunMode = AgentRunMode.ReturnTask;
    options.ServerOptions = new A2AServerOptions { SupportsStreaming = true, SupportedInputModes = ["text/plain", "application/json"] };
});
app.MapA2AHttpJson(agent, "/a2a").RequireAuthorization("Sentinel");
app.MapA2AJsonRpc(agent, "/a2a").RequireAuthorization("Sentinel");
app.MapWellKnownAgentCard(card);
```

## 2. Authentication

Every request except the card carries `Authorization: Bearer <token>`, a client-credentials access token issued by the
fleet's identity provider for the API scope **`listeners`**. Validate it as any fleet API does (JWT bearer against the
provider's authority, `scope` must include `listeners`) on the A2A endpoints and on every feed endpoint.

## 3. Operator identity

Each A2A request carries the operator's id in its request metadata under **`sentinel.operator`** (a GUID string).
Record it in your own audit trail for every state-changing action. It identifies who asked; it is not a credential,
and the bearer token is what authorizes the call.

## 4. Approvals (human in the loop)

Any tool that changes state (restarting a service, running a backup, writing a file) waits for the operator.

1. Wrap the tool in `ApprovalRequiredAIFunction`. When the agent's run produces a `ToolApprovalRequestContent`, keep
   it with the task (for example in the session state), and move the task to **`input-required`** with
   `TaskUpdater.RequireInputAsync`. The status message carries a short human summary as a text part and one data
   part:

   ```json
   {
     "kind": "sentinel.approval.request/v1",
     "approvalId": "<your id for this request>",
     "tool": "restart_service",
     "arguments": { "service": "grafana" },
     "risk": "high",
     "expiresAt": "<optional ISO 8601 instant>"
   }
   ```

   `risk` is `low` or `high`. `arguments` is the JSON object the tool would be called with; show the real values,
   because the operator approves exactly what is displayed.
2. Sentinel shows an Approve/Reject card and records the request in its audit log. The operator's decision arrives as
   a new message **on the same task** (`context.IsContinuation` is true), carrying a data part:

   ```json
   {
     "kind": "sentinel.approval.response/v1",
     "approvalId": "<the id you sent>",
     "approved": true,
     "reason": "<optional>",
     "operator": "<operator GUID>"
   }
   ```

3. Map it to `request.CreateResponse(approved, reason)`, resume the agent, and finish the task (`completed` with an
   artifact describing what happened, or `rejected`). An unknown or already-used `approvalId` is rejected, never run.

A task in `input-required` *without* the approval data part is an ordinary question for the operator: the operator's
next chat message is sent on the same task.

## 5. Feeds (optional, needs the extension)

Read-only JSON over GET, under the base address, behind the same bearer token. Sentinel polls them every few minutes,
caps each response (1 MiB by default) and renders every string as plain text.

| Path | Who | Body |
|---|---|---|
| `sentinel/v1/status` | every listener | `ListenerStatusFeed` |
| `sentinel/v1/artifacts` | every listener | `ArtifactFeed` |
| `sentinel/v1/board` | the master bridge only | `BoardSnapshot` |

Property names are camelCase; enum values are kebab-case lowercase; instants are ISO 8601 with an offset (UTC
preferred). The C# records in `Sentinel/Feeds` are the schema.

```json
{ "generatedAt": "...", "health": "healthy | degraded | unhealthy", "summary": "...",
  "checks": [ { "name": "...", "health": "healthy", "detail": "...", "checkedAt": "..." } ] }
```

```json
{ "generatedAt": "...",
  "items": [ { "id": "...", "title": "...", "kind": "report | audit-report | log | snapshot | other",
               "producedAt": "...", "summary": "...", "link": "https://...", "text": "..." } ] }
```

```json
{ "generatedAt": "...",
  "sessions":  [ { "sessionId": "...", "machine": "...", "goal": "...", "page": "https://...", "lastSeen": "..." } ],
  "claims":    [ { "sessionId": "...", "repo": "...", "files": ["..."], "claimedAt": "...", "releasedAt": null } ],
  "gates":     [ { "sessionId": "...", "machine": "...", "repo": "...", "state": "starting | finished", "verdict": "...", "at": "..." } ],
  "handoffs":  [ { "id": "...", "from": "...", "to": "...", "summary": "...", "status": "...", "kind": "...", "at": "..." } ],
  "audits":    [ { "skill": "...", "run": "...", "summary": "...", "report": "https://...", "at": "..." } ],
  "inventory": [ { "machine": "...", "sessionId": "...", "state": "...", "goal": "...", "at": "..." } ] }
```

Audit reports go in the artifacts feed with `kind` = `audit-report`; the Audits page lists them beside the board's
audit runs.

## 6. The master bridge

The listener configured with `Kind: Master` relays to the always-on Claude Code session. It implements everything
above, plus:

- its A2A turns hand the operator's message to the master session and return the session's reply as the task's
  artifact;
- the orchestrator can reach it only through `relay_to_master`, which the operator approves in Sentinel before any
  message is sent; a direct chat from the master's own page is the operator's own message and needs no extra approval;
- its board feed relays the coordination board's collections as rows of data. Sentinel never treats board rows as
  instructions.

## 7. Limits and versioning

- Keep a single streamed reply under a few hundred kilobytes; put large output in an artifact with a `link`.
- Long work is fine: the task stays `working`, and Sentinel follows the stream or resumes it after a restart.
- Implement `CancelAsync`: the operator's Cancel button cancels the task.
- The extension URI, the approval `kind` values and the feed paths carry their version (`v1`). A breaking change takes a
  new version alongside the old one.
