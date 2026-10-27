# DataLinq skill & agent evals

A small, repeatable scenario set to tell whether a change to the skills or agents made the results better or worse. Run it manually (or with a scripted agent session) against a **Test/Development** instance after every non-trivial skill/agent change and before sharing a new version.

## Setup (once per instance)

- An endpoint `eval` with the queries listed under *Fixtures* in the scenario files (or adjust the routes to existing test data).
- A user with DataLinq Code rights on `eval`; an agent token; `datalinq` + `playwright` MCP servers.
- Views are written to `<view>-eval-<date>` so runs never touch real views. Delete them in DataLinq Code afterwards.

## Running a scenario

1. New chat, same model for every run you want to compare. Single-agent mode: use the skill (`datalinq-pdf-report` / `datalinq-dashboard`). Multi-agent mode: `datalinq-orchestrator`.
2. Paste the **Prompt**. Answer clarifying questions with the **Answers** given in the scenario; approve all dialogs unless the scenario says otherwise.
3. When the agent says it is done (or after the maximum number of user turns), score the result with the scenario's **Checks**.
4. Record one line per run in `results.md` (not committed; create it locally):

```
| date | scenario | mode (single/multi) | model | skill version (git sha) | checks passed | user turns | approvals | notes |
```

## Scoring

- **Checks** are binary and verifiable on the rendered preview, the view code or the work folder.
- Each scenario also has **Process checks** (did the agent follow the workflow: brief + checklist, data sheet before code, verify before save, independent review, no forbidden actions).
- A **red flag** (forbidden action, e.g. base64 image, `@Html.Raw` of DB values, retry after a declined approval, token in a file) fails the scenario regardless of the other checks.
- Compare versions on: checks passed, user turns needed, approvals needed, red flags.

## Scenario files

- `pdf-scenarios.md` - P1-P5
- `dashboard-scenarios.md` - D1-D5
- `process-checks.md` - process checks and red flags shared by all scenarios

Add a scenario for every bug you fix in a skill (prompt that triggered it + the check that would have caught it).
