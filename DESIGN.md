---
name: Sentinel
colors:
  online: "#198754"
  offline: "#dc3545"
  misconfigured: "#ffc107"
  unknown: "#6c757d"
  master: "#0d6efd"
  running: "#0dcaf0"
  navbar: "#212529"
  surface: "#f8f9fa"
typography:
  body:
    fontFamily: "Bootstrap's system sans-serif stack (--bs-font-sans-serif)"
    fontSize: "1rem"
rounded:
  default: "0.375rem"
---

# Design Documentation

## Overview

A control panel for listeners: a dashboard of tiles, a page per listener with its conversation, status and
artifacts, a chat with Sentinel itself, an approvals queue, and read-only views of the coordination board, audits,
artifacts and the operator audit log. It is built from stock Bootstrap 5.3.8 classes with no authored CSS: in
Development the stylesheet is the copy in `wwwroot/lib/bootstrap`, elsewhere the jsDelivr CDN with subresource
integrity. Blazor's own reconnect banner is used as shipped. Color carries state and nothing else.

## Colors

| Token | Bootstrap class | Meaning |
|---|---|---|
| `online` | `border-success`, `bg-success` | the listener's card was fetched and validated; a feed reports healthy; a turn completed |
| `offline` | `border-danger`, `bg-danger` | the card could not be fetched; a feed reports unhealthy; a turn failed; a high-risk approval |
| `misconfigured` | `border-warning`, `bg-warning text-dark` | the card points outside the configured address; a feed reports degraded; a turn awaits approval; a low-risk approval |
| `unknown` | `border-secondary`, `bg-secondary` | not yet checked; an interrupted or canceled turn |
| `master` | `border-primary`, `bg-primary` | the master bridge's tile and badge, whatever its state |
| `running` | `bg-info text-dark` | a turn in progress |

Transcript entries use `list-group-item-primary` for the operator, plain `list-group-item` for the agent,
`list-group-item-warning` for an approval request, `list-group-item-info` for a decision, `list-group-item-light` for
a status line and `list-group-item-danger` for an error. `DisplayMapper` is the only place these classes are chosen.

## Typography

Bootstrap's defaults throughout. Page titles are `h1.h3`; section titles `h2.h5`; card headers `fw-semibold`;
timestamps and secondary text `small text-muted`; ids (session ids, operator ids) `font-monospace`. Transcript text and
artifact content render in `pre` so line breaks survive.

## Layout

- A dark top navbar: brand, Dashboard, Sentinel chat, Approvals, Board, Audits, Artifacts, Audit log, and **Log out**
  (a POST with an antiforgery token).
- The dashboard is a responsive grid of listener tiles (`row-cols-1`, `row-cols-md-2`, `row-cols-xl-3`, `g-4`), the
  master bridge first, then pinned listeners, then the rest by name, with a pending-approvals button above it.
- A listener page is two columns on large screens: the conversation (`col-lg-7`) beside status and artifacts
  (`col-lg-5`).
- Approval cards show the tool, the target, the risk badge, the exact arguments in a `pre`, an optional reason, and
  Approve / Reject buttons.

## Element ids

Every element a test selects carries a durable `id`: `listener-tile-{id}`, `listener-state-{id}`,
`transcript-entry-{guid}`, `approval-{guid}`, `approve-{guid}`, `reject-{guid}`, `chat-input`, `chat-send`,
`chat-cancel`, and so on. Ids are never derived from display text.
