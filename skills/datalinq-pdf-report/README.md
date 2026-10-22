# DataLinq PDF Report skill

Agent skill (GitHub Copilot, Claude Code and other Agent Skills compatible agents) that helps DataLinq administrators create and change **PDF report templates** from a description, an image/mockup or an existing template - without writing Razor themselves.

## Requirements

- A DataLinq **Test or Development** instance (the MCP server is not available on Production)
- A DataLinq user with DataLinq Code rights for the endpoints you want to edit
- An MCP-capable agent, ideally with a browser tool (e.g. Playwright MCP) so the agent can look at the rendered report

## Installation

1. Copy (or symlink) the folders `datalinq-connect`, `datalinq-pdf-report`, `datalinq-pdf-data`, `datalinq-pdf-author`, `datalinq-pdf-from-image`, `datalinq-pdf-edit` and `datalinq-pdf-review` into the skills folder of your agent:

   | Agent | Location |
   |-------|----------|
   | GitHub Copilot | `.github/skills/datalinq-pdf-report` in your repo or `~/.copilot/skills/` |
   | Claude Code | `.claude/skills/datalinq-pdf-report` or `~/.claude/skills/` |
   | Others | Any folder the agent loads `SKILL.md` skills from |

2. Generate an agent token: in DataLinq Code on the Test/Development instance open *Settings* -> **MCP Agent Token**, enter a name (e.g. "VS Code laptop"), choose a lifetime (8 h - 7 days) and click **Generate token**. The token is shown only once; the dialog also shows a ready-to-copy client config.

3. Add the DataLinq MCP server to your agent, e.g. `.vscode/mcp.json` (the token is asked for once and stored by VS Code, not in the file):

   ```json
   {
     "inputs": [
       { "type": "promptString", "id": "datalinq-token", "description": "DataLinq MCP agent token", "password": true }
     ],
     "servers": {
       "datalinq": {
         "type": "http",
         "url": "https://<datalinq-url>/datalinq-mcp",
         "headers": { "Authorization": "Bearer ${input:datalinq-token}" }
       }
     }
   }
   ```

   Claude Code: `claude mcp add --transport http datalinq https://<datalinq-url>/datalinq-mcp --header "Authorization: Bearer $DATALINQ_MCP_TOKEN"`

4. When the token expires or is revoked the agent gets a 401 and asks you to generate a new one. Never paste the token into the chat and never commit it.

See `../datalinq-connect/references/datalinq-api.md` for details.

## Usage

Ask your agent, for example:

- "Create a PDF invoice for `shop@orders` - one page per order, positions from `shop@order-positions`."
- "Here is a screenshot of our old protocol form, rebuild it as a DataLinq PDF report for `plant@inspections`."
- "In `hr@staff@list-pdf` add a column for the department and switch to landscape."

The agent samples your data, drafts the template, compiles it on the server, **asks before saving**, then opens the report itself, checks for cut-off content/fonts/page count and looks at screenshots before handing it to you.

## Security

- MCP endpoints exist only on Test/Development instances.
- Personal agent tokens generated in DataLinq Code; the agent acts with your DataLinq Code identity and roles. The server stores only a hash in an encrypted registry; tokens expire (max. 7 days by default) and can be revoked at any time in the same dialog.
- All DataLinq Code permission checks apply; query runs and writes require approval in a server-side dialog; nothing is deleted.
- Preview links are single use, must be opened within ~2 minutes and are bound to a single report route.
