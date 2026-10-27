---
name: datalinq-connect
description: Connect to a DataLinq instance through the DataLinq MCP server (agent token from DataLinq Code), check the session (whoami), explain the server-side approval dialogs and which DataLinq MCP tools exist. Use whenever a DataLinq skill (PDF reports or dashboards) needs the `datalinq` MCP tools, when those tools are missing, when a call fails with 401 (missing/expired/revoked token), or when an action is "refused" because approval dialogs are not supported.
---

# DataLinq Connect

Shared by all DataLinq skills (PDF reports, dashboards/sites, queries). Run this first, then return to the calling skill.

## 1. Check the session

Call `whoami`. On success remember `user`, `roles`, `environment` and continue with the calling skill.
If the `datalinq` tools are missing or the call fails, continue with section 2. Never just answer that it is not possible.

If a guarded tool returns "Action refused ... does not support approval dialogs": reading works, but **running queries, previews and every save/create are refused by the server**. Tell the user:

> Your chat client cannot show DataLinq approval dialogs. I can look at queries and views, but to run queries or save anything please use a client with MCP elicitation support (current VS Code / Visual Studio / Copilot CLI) or make the change in DataLinq Code.

## 2. Not connected (agent token required)

The MCP server accepts only a personal **agent token** (`Authorization: Bearer dla_...`) generated in DataLinq Code. A 401 means the token is missing, expired or revoked. Reply with this first, prominently, in the user's language, then wait:

> **Action needed: please connect DataLinq.**
> 1. Open DataLinq Code on the Test/Development instance -> *Settings* -> **MCP Agent Token**.
> 2. Enter a name (e.g. "VS Code laptop"), choose how long it is valid and click **Generate token**.
> 3. Copy the shown client config into your MCP client (e.g. `.vscode/mcp.json`), preferably with the token in an input/environment variable, then restart the `datalinq` server.
> 4. Reply "done" here and I will continue.
>
> Do not paste the token into the chat.

- Server not configured at all: show the configuration from `references/datalinq-api.md`.
- Tools disappeared: restart the `datalinq` server (VS Code: *MCP: List Servers* -> `datalinq` -> *Restart*), start a new chat if needed.
- Token expired or revoked: generate a new one in the same dialog (active tokens are listed there and can be revoked).

## 3. Approval dialogs (server-side, cannot be bypassed)

Every tool that **runs a query** or **changes something** makes the server show an approval dialog in the user's chat client (MCP elicitation). The agent cannot answer it. There is no `confirmed` parameter.

- Before such a call, say in one sentence what you will do and why ("I'll run the query `x@y` once to see its columns - please allow it in the dialog.").
- **Declined** -> error "The user did NOT approve". Do **not** retry; ask the user what they want instead.
- **Refused** -> client has no approval support (section 1).
- For repeated runs/saves/previews of the **same** route the dialog offers "Don't ask again for this target in this session". Mention it once at the start of a review loop. Creating items, changing queries and ad-hoc SQL are always asked individually.
- There are **no delete tools**. Deleting is done by the user in DataLinq Code.
- **Subagents** (multi-agent mode): the dialog is shown by the user's chat client for whichever agent made the call, so subagents get the same dialogs. Never run several approval-requiring calls in parallel - the user would get stacked dialogs without context. "Don't ask again" is remembered per MCP session; agents of the same chat normally share it, but a client may open its own session per subagent - then the user is asked again.

## 4. Tools

| Tool | Approval | Purpose |
|------|----------|---------|
| `whoami` | - | User, roles, environment. |
| `list_endpoints` / `list_queries` / `list_views` | - | Discover routes the user may edit. |
| `get_query` | - | Query definition (statement, test parameters). |
| `get_view` | - | Full view code, CSS, JS, flags. Expensive - prefer outline/page tools. |
| `get_view_outline` | - | Cheap map: PDF pages or HTML/Razor sections with line ranges + summary. No code. |
| `get_view_page` | - | Code of one section/page (with line numbers). |
| `verify_view` | - | Compiles Razor code WITHOUT saving. Not layout, not data. |
| `sample_query` | yes (rememberable) | Runs a saved query; exact column names + first rows. |
| `run_sql_preview` | yes (every time) | Unsaved read-only SELECT/WITH on a database endpoint - schema discovery, testing SQL. |
| `get_preview_link` | yes (rememberable) | Short-lived URL rendering the **saved** view (runs its queries). |
| `create_view` | yes | New view (compiled first). |
| `save_view` | yes (rememberable) | Overwrites the full code; returns `previousCode`. |
| `patch_view` | yes (rememberable) | Replaces one exact, unique snippet (optionally inside one section). |
| `update_view` | yes | View properties (name, test parameters, JS libraries, CSS/JS replace, PDF flags...). |
| `create_query` / `update_query` | yes | Create/change a query; dialog shows SQL diff + dependent views. |
| `get_pdf_reference` | - | PDF: documented `@PDF.*` / `SECURITY.*` methods. |
| `get_dlh_reference` | - | Dashboards: documented `@DLH.*` and `Model.Records` extension methods. |
| `list_view_dependencies` | - | Dashboards: referenced queries/views, DLH calls used. |
| `get_view_assets` | - | Dashboards: separate view CSS/JS (numbered). |
| `patch_view_asset` | yes (rememberable) | Dashboards: patch the separate view CSS or JS. |

Details: `references/datalinq-api.md`. How endpoints, queries, views, `Model.Records` and `DLH.GetRecordsAsync` fit together: `references/datalinq-model.md`. Loop, work folder (`.datalinq-work/`), handoff formats, roles and definition of done for all DataLinq skills and agents: `references/workflow.md`.

## Rules

- Never ask for or accept passwords or agent tokens in the chat. If the user pastes a token, tell them to revoke it in DataLinq Code and generate a new one.
- Never print or store agent tokens or preview tokens in code, files, logs or committed config.
- Never work around a declined or refused approval (other tools, other routes, splitting the change).
