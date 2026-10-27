# DataLinq authoring workflow (shared by PDF reports and dashboards)

Single source of truth for the loop, the work folder, the definition of done and the handoff formats. Both orchestrators (`datalinq-pdf-report`, `datalinq-dashboard`) and all DataLinq custom agents follow this file.

## 1. Work folder (handoff artifacts)

Everything another step - or another agent with its own context window - needs is written to files, not only kept in the conversation:

```
.datalinq-work/<endpoint>@<query>@<view>/
  brief.md          goal, audience, decisions, acceptance checklist      (orchestrator)
  data-sheet.md     queries, parameters, exact columns, types, examples  (datalinq-data)
  layout.md         regions/pages, helper mapping, placeholders          (author / from-image)
  changes.md        append-only undo log                                 (every save)
  review-<n>-visual.md / review-<n>-code.md   findings of review round n   (datalinq-view-review)
```

- Folder in the user's workspace root. Add `.datalinq-work/` to `.gitignore` (create the entry if missing; ask once if the workspace is not a git repo you may change). Never commit it.
- No agent tokens, preview links/tokens, passwords or connection strings in these files.
- Example rows: max. 3 per query, shorten long texts, mask personal data (names, e-mail, phone, addresses) unless the user needs them for the layout.
- No workspace / no file tools: keep the same sections in the conversation instead (single-agent mode).
- Resume: if the folder exists for the route, read `brief.md` and the last `review-<n>-*.md` first and continue from there.

## 2. brief.md

```markdown
# <route> - <kind: pdf | dashboard>
Goal: <one sentence>            Audience: <who uses it>
Source: description | image <file> | existing view
Target view: <route> (draft: <route>-draft yes/no)
Decisions: A4 portrait, page numbers n/total bottom-right, ...
Out of scope: ...

## Acceptance checklist
1. [ ] <testable statement, e.g. "one page per order, header shows ORDER_NO and ORDER_DATE dd.MM.yyyy">
2. [ ] ...
```

Rules: 5-15 items, each checkable on the rendered preview or in the code; written **before** any code; the user confirms or corrects it once ("Is this what you need?"). New feedback from the user becomes new/changed checklist items, never only a chat note.

## 3. changes.md (undo log)

```markdown
## <n> <tool> <route> [page/section/asset]   <short reason>
oldText:
~~~
...
~~~
newText:
~~~
...
~~~
```

`save_view` -> store `previousCode`; `update_query` -> store `previousStatement`; `create_view`/`create_query` -> note "created" (undo = user deletes it in DataLinq Code). Undo = reverse `patch_view` / `save_view` with the stored text.

## 4. review-<n>-visual.md / review-<n>-code.md (structured findings)

```markdown
# Review <n> - <visual | code> - <route>
Verdict: pass | fail
Checklist: 1 ok, 2 FAIL, 3 ok, ...
| # | sev | checklist | where (page/section/selector) | evidence | fix hint |
|---|-----|-----------|-------------------------------|----------|----------|
| 1 | high | 2 | page 1 / .rpt-head | ORDER_DATE shows 2024-03-01T00:00:00 | format dd.MM.yyyy |
```

Severity: `high` = wrong/missing data, error text, broken layout, security rule broken; `med` = visible defect (overlap, overflow, contrast); `low` = polish. Every finding cites a checklist item **or** concrete evidence (check-script output, screenshot region, code line). Opinions without evidence are not findings.

## 5. The loop

```
clarify -> brief.md (user confirms checklist)
data    -> data-sheet.md            (+ datalinq-query if data is missing)
design  -> layout.md                (author / from-image)
code    -> verify_view -> save      (changes.md)
review  -> review-<n>-*.md            (visual + code, independent of the author)
fix     -> smallest patches, back to review   (max 3 automatic rounds)
show    -> fresh preview link + summary + open findings
ask     -> "Does this look right, or what should I change?"
feedback-> update checklist -> code -> review -> show ...
```

- Always `verify_view` before a save (patch tools compile automatically; still verify bigger changes first).
- Before the first save: name the target route, suggest `<view>-draft` if the view is in use, mention "Don't ask again for this target" once.
- After 3 automatic fix rounds with remaining `high` findings: stop and show the user the state and the findings.
- Draft used: copy to the real view (`get_view` draft -> `save_view` real) only after the user explicitly agrees.

## 6. Definition of done

All of:
- Last save compiled, last review verdict `pass` (no `high`, no `med` without user acceptance).
- Every checklist item `ok` or explicitly waived by the user.
- Rendered preview checked (check script + screenshots); a compile alone is never "done".
- User said it is OK.
- Final answer: route, fresh preview link, short summary, where the undo log is.

## 7. Roles (single agent or multiple agents)

| Role | Skill(s) | Writes | Must not |
|---|---|---|---|
| orchestrator | `datalinq-pdf-report` / `datalinq-dashboard` | `brief.md` | write view code, judge its own work |
| data analyst | `datalinq-data` | `data-sheet.md` | change queries |
| query engineer | `datalinq-query` | queries, `data-sheet.md` update | touch views |
| designer | `datalinq-from-image`, author step "map/structure" | `layout.md` | save anything |
| view coder | `datalinq-pdf-author` / `datalinq-dashboard-author` / `datalinq-view-edit` | views, `changes.md` | change queries, review itself |
| visual reviewer | `datalinq-view-review` (visual) | `review-<n>-visual.md` | change views or queries |
| code reviewer | `datalinq-view-review` (code) | `review-<n>-code.md` | run queries, change anything |

Single-agent mode: one agent plays all roles in this order. Multi-agent mode (custom agents installed): the orchestrator delegates each role to its agent and passes only the work-folder path plus a one-line task.

### Independence of reviews (anti-bias)

- Reviewers start from `brief.md`, `data-sheet.md`, `layout.md`, the route and the original image - never from the author's reasoning or chat history.
- The author does not decide whether its own work is done; the orchestrator does, based on `review-<n>-*.md` and the user.
- In single-agent mode, review against the checklist item by item and write `review-<n>-*.md` before looking at your own changes again.

### Parallelism

- Safe in parallel: data analysts for different queries; visual + code reviewer on the same saved version; read-only lookups.
- Never in parallel: two writers on the same view or query; several approval-heavy calls at once (the user gets stacked dialogs). Run approval-requiring calls one after another.
- Each reviewer requests its own preview link (links are single use).
