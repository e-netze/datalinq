# Proposal: MCP server improvements for the agentic workflow

Status: proposal only (no code changed). Scope: `src/nuget/E.DataLinq.Web/Mcp/**`.
Goal: make the multi-agent loop in `datalinq-connect/references/workflow.md` cheaper (fewer tokens and approvals), safer (server-side undo) and less dependent on client features (browser tool, file system).

Ordered by value / effort.

## 1. `copy_view` (promote a draft)

**Today:** promoting `<view>-draft` to `<view>` = `get_view` (full code into the context) + `save_view` (full code out again), plus `update_view` for CSS/JS/flags.
**Proposal:** `copy_view(endpointId, queryId, sourceViewId, targetViewId, overwrite)` copies code, CSS, JS, JS libraries and flags server-side. Approval: always asked (shows target and whether it overwrites). Returns `previousCode` of the target.
**Effect:** no code passes through the model; one dialog instead of up to three.

## 2. View history and `restore_view`

**Today:** undo relies on `oldText`/`previousCode` kept by the agent (`changes.md`).
**Proposal:** store the last N versions per view on every MCP save (`save_view`, `patch_view`, `patch_view_asset`, `update_view`, `copy_view`) with user, time and tool. Tools: `list_view_versions(route)` (no approval) and `restore_view(route, version)` (approval, rememberable like `save_view`). Can reuse the ideas of the `v8-version-control` branch.
**Effect:** reliable undo across sessions and agents, independent of the client's files.

## 3. `check_view` (server-side render check)

**Today:** visual review needs a browser MCP in the client; agents without one can only compile.
**Proposal:** `check_view(route, parameters, viewport?)` renders the saved view headless on the server (e.g. Playwright for .NET), runs the logic of `render-check.js` / `dashboard-check.js` and returns the JSON report plus optional screenshots (PNG per `.page` / per viewport) as MCP image content. Approval: same rule as `get_preview_link` (runs the view's queries; rememberable).
**Effect:** every client can do the visual review; one call instead of preview link + navigate + evaluate + screenshot; check scripts versioned with the server.
**Risk:** headless browser on the server (resources, sandboxing) - only on Test/Development, opt-in via `DataLinqMcpOptions`.

## 4. Skills/templates as MCP resources, workflows as MCP prompts

**Today:** users copy skill folders; references and templates can drift from the server version (e.g. PDF helper API).
**Proposal:**
- MCP **resources**: `datalinq://reference/pdf-helper-api`, `datalinq://reference/dashboard-patterns`, `datalinq://templates/pdf/<name>`, `datalinq://templates/dashboard/<name>`, `datalinq://workflow` - generated from the same files the skills ship.
- MCP **prompts**: `datalinq_pdf_report`, `datalinq_dashboard` (arguments: route, description) that start the orchestrated flow in clients without skill support.
**Effect:** knowledge always matches the running server version; skills shrink to routing + rules.

## 5. Batched approval for a review loop

**Today:** each save/preview of a route is approved separately unless "Don't ask again" is chosen, which is per MCP session (subagents in clients that open their own session ask again).
**Proposal:** `begin_edit_session(route, scope: [save, patch, preview, sample], minutes)` - one dialog listing the route and allowed actions; returns an opaque `editSessionId` that later calls pass. Server still logs every action; `create_*`, `update_query`, `run_sql_preview` stay individually approved.
**Effect:** one approval for a whole review loop, also across subagents; clear scope for the user.

## 6. Structured diagnostics

- `verify_view` / `patch_view`: return compiler errors with line numbers **relative to the outline section** (`page`, `lineInPage`) so the coder can patch without reading the whole view.
- `get_view_outline`: add a hash per section; `patch_view` accepts `expectedSectionHash` to reject patches against an outdated version (protects against parallel edits by two agents/users).

## 7. Smaller items

- Approval errors: include the elicitation result action (`decline` vs. `cancel`, or no response) in the "did NOT approve" message and the server log. Today a dialog the client cancelled automatically (e.g. an unattended/autopilot session) looks the same as a user declining, which makes client support hard to diagnose.
- `sample_query`: option `maskPersonalData` (server masks e-mail/phone-like values) for data sheets.
- `get_preview_link`: option `viewport`/`print=true` hint and return the expected page count for PDF views.
- Server instructions (`options.ServerInstructions`): mention the work-folder convention and the review independence rule, so clients without the skills still follow the loop.
