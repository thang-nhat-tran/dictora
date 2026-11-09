# Workflow

This is the starting point for every developer on Dictora. It explains how work moves from an idea to production, where every document lives, and what each term means.

If something here doesn't match reality, fix this file in a PR. A workflow nobody follows is worse than none.

---

## 1. Start here: reading order

Read these in order when you join the project. After that, come back whenever you're unsure.

| # | Document | What you get from it | Time |
|---|----------|----------------------|------|
| 1 | `docs/workflow.md` (this file) | How we work, terms, rules | 15 min |
| 2 | `README.md` | How to run the project locally | 10 min |
| 3 | `docs/system-architecture.md` | How the parts fit together: frontend, backend, DB, YouTube captions, hosting | 15 min |
| 4 | `docs/conventions/general.md`, then `backend.md` or `frontend.md` | How to write code here: naming, folders, style | 10 min |
| 5 | `docs/decisions/` | Why the big technical choices were made | skim titles, read when relevant |
| 6 | `docs/specs/` | What each feature must do | read the spec of the issue you pick |
| 7 | `docs/learning/` | Lessons on concepts we use (DB schemas, EF Core, …) | when a concept is new to you |
| 8 | [Milestones](https://github.com/thang-nhat-tran/dictora/milestones) + [Project board (Dictora Workspace)](https://github.com/users/thang-nhat-tran/projects/2) | What we're building now and who's on what | 5 min |

---

## 2. Project map

```
dictora/
├── CLAUDE.md              Instructions for AI coding agents (Claude Code; Copilot via AGENTS.md)
├── README.md              Short intro + "Getting started" (how to run locally)
├── backend/               ASP.NET Core Web API (C#)
├── frontend/              React app (Vite)
├── infra/                 Local dev and deployment config (docker-compose, Railway/Vercel notes)
├── docs/
│   ├── workflow.md        This file: process, rules, glossary
│   ├── system-architecture.md
│   ├── conventions/
│   │   ├── general.md     Rules for the whole codebase
│   │   ├── backend.md     ASP.NET / modular monolith rules
│   │   └── frontend.md    React rules
│   ├── learning/          Lessons on concepts we use (schemas, EF Core, …)
│   ├── specs/
│   │   ├── _template.md             Copy this to start a new spec
│   │   └── SPEC-NNN-<feature>.md    One file per feature, e.g. SPEC-001-auth.md
│   └── decisions/
│       ├── _template.md             Copy this to start a new decision
│       └── ADR-NNN-<title>.md       One file per decision, e.g. ADR-001-modular-monolith.md
└── .github/
    ├── ISSUE_TEMPLATE/    Forms for Feature / Bug / Task issues
    ├── pull_request_template.md
    └── workflows/ci.yml   CI: build, lint and test on every PR
```

### What each doc is for

| Path | Answers the question | Update it when |
|------|---------------------|----------------|
| `docs/workflow.md` | *How do we work?* | The process changes |
| `docs/system-architecture.md` | *How does the system work right now?* Components, data flow, external services, environments, env var names (never secret values) | You add or change a component, service, or integration |
| `docs/conventions/*.md` | *How should code look?* `general.md` for the whole codebase; `backend.md` / `frontend.md` for naming, folder structure, API style, i18n keys, testing | The team agrees on a new rule |
| `docs/specs/SPEC-NNN-*.md` | *What must this feature do?* Goal, user flow, rules, acceptance criteria, out of scope | Before building the feature, and whenever its requirements change |
| `docs/decisions/ADR-NNN-*.md` | *Why did we choose X over Y?* | You make a choice that's hard to reverse or that people will ask "why?" about |
| `CLAUDE.md` | *What does an AI agent need to know?* Stack, commands, where docs are, key rules | Commands, structure, or key rules change |

**Rules of thumb**
- **Specs say *what*, the architecture doc says *how it works*, decisions say *why*, and issues say *who and when*.**
- One fact lives in one place. Link to it; don't copy it.
- Docs change in the **same PR** as the code that makes them out of date.

### IDs
Every spec and decision has a permanent ID, which goes in its file name and title:

| Kind | ID format | File name example | Title example |
|------|-----------|-------------------|---------------|
| Spec | `SPEC-NNN` | `SPEC-003-dictation-practice.md` | `# SPEC-003: Dictation practice` |
| Decision | `ADR-NNN` | `ADR-001-modular-monolith.md` | `# ADR-001: Modular monolith` |

- `NNN` is 3 digits, in order. Take the next free number in the folder **when you create the file**.
- **Don't reserve numbers** for future docs. Until a decision is written, refer to it by its topic and issue, e.g. "the caption-source ADR (#2)".
- An ID is **never reused or renumbered**, even if the file is deleted or superseded. The words after the ID can change; the ID can't.
- Use the ID when you refer to a doc in issues, PRs, commits and other docs, e.g. "as decided in ADR-001", "implements SPEC-003".

### Specs (`docs/specs/`)
- File name: `SPEC-NNN-<feature>.md`, with the feature name in kebab-case.
- Start from `_template.md`. Set **Status**: `Draft` (being written) → `Ready` (agreed, issues can be created) → `Done` (shipped).
- **Acceptance criteria** live here, not in issues.
- A spec is merged through a PR like code, so the team reviews it before anyone builds it.

### Decisions (`docs/decisions/`)
- File name: `ADR-NNN-<short-title>.md`, e.g. `ADR-001-modular-monolith.md`.
- Start from `docs/decisions/_template.md`. Short format: **Context** (the problem) → **Options** (what we considered) → **Decision** (what we chose) → **Consequences** (trade-offs, limits).
- Never edit an old decision to reverse it. Write a new one that says "Supersedes ADR-001", and set the old one's Status to `Superseded by ADR-00N`.

---

## 3. Glossary

### Planning (GitHub)

| Term | Meaning here |
|------|--------------|
| **Milestone** | A release goal with a target date, e.g. `M0 Foundation`, `M1 MVP`. Every issue belongs to one. A milestone is done when all of its issues are closed. |
| **Issue** | One unit of work, small enough for one dev in **≤ 2 days**. Created from a template. |
| **Feature issue** | Builds (part of) a spec. Contains a link to the spec and the scope. No requirements inside. |
| **Bug issue** | Something is broken. Contains steps to reproduce and expected vs actual. |
| **Task issue** | Technical work without a spec. Kind is `chore` or `spike`. |
| **Chore** | Setup, tooling, CI, refactor, dependency update: no user-visible change. |
| **Spike** | A **time-boxed** investigation to reduce uncertainty (e.g. "can we fetch captions from Railway?"). The output is a **decision** (ADR), not production code. |
| **Label** | Tag for filtering. Type: `feature`, `bug`, `chore`, `spike`. Area: `frontend`, `backend`, `infra`. |
| **Project board** | [Dictora Workspace](https://github.com/users/thang-nhat-tran/projects/2), the GitHub Project that shows every issue's status. It's the single source of truth for "who's doing what". |
| **Backlog / Ready / In Progress / In Review / Done** | Board columns. See [section 4](#4-the-workflow-step-by-step). |
| **Priority** | Board field. **P0** = must have for the milestone, or blocks others. **P1** = should have. **P2** = nice to have, can slip to the next milestone. Within Ready, pick P0 before P1. |
| **Size** | Board field, a rough effort guess. **XS** < 2 h · **S** ≈ half a day · **M** ≈ 1 day · **L** ≈ 2 days · **XL** = too big, so split it before it goes to Ready. |
| **WIP limit** | "Work in progress" limit: at most **2 issues In Progress per dev**. Finish before starting. |
| **Assignee** | The one person responsible for an issue. Assign yourself when you start. |

### Documents

| Term | Meaning here |
|------|--------------|
| **Spec** | Feature specification in `docs/specs/`, with an ID like `SPEC-003`: what the feature must do. |
| **Acceptance criteria (AC)** | Checklist in a spec that defines "this works". Reviewers test against it. |
| **Out of scope** | What the feature deliberately does *not* do (yet). Prevents scope creep. |
| **ADR** | Architecture Decision Record, a file in `docs/decisions/` with an ID like `ADR-001`: one decision and why we made it. |
| **Definition of Ready (DoR)** | Conditions for an issue to move to Ready. See [section 5](#5-definitions-of-ready-and-done). |
| **Definition of Done (DoD)** | Conditions for an issue to count as Done. See [section 5](#5-definitions-of-ready-and-done). |

### Code and Git

| Term | Meaning here |
|------|--------------|
| **`main`** | The only long-lived branch. Always deployable. Protected: nobody pushes to it directly. |
| **Trunk-based development** | Everyone branches from `main` and merges back within days. No `develop` and no long-lived feature branches. |
| **Feature branch** | Your short-lived branch for one issue, e.g. `feat/14-dictation-player`. |
| **PR (Pull Request)** | A request to merge your branch into `main`. Where review and CI happen. |
| **Draft PR** | A PR marked "not ready". Open it early to share progress or get early feedback. |
| **Review / Approval** | Another dev reads the PR, tests it, and approves. **1 approval is required.** |
| **Squash merge** | All commits of a PR become **one** commit on `main`, titled with the PR title. Keeps history clean. |
| **Conventional Commits** | Title format `type: description`, e.g. `feat: add segment replay`, `fix: handle videos without captions`. Types: `feat`, `fix`, `chore`, `docs`, `refactor`, `test`. |
| **Closing keyword** | `Closes #14` in the PR body: GitHub closes the issue when the PR merges. |
| **Rebase / update branch** | Pull the latest `main` into your branch before merging, to resolve conflicts on your side. |

### Build, test, deploy

| Term | Meaning here |
|------|--------------|
| **CI (Continuous Integration)** | GitHub Actions (`.github/workflows/ci.yml`) runs build, lint and tests on every PR. A red CI blocks the merge. |
| **Lint** | Automatic style and bug checks (ESLint for frontend, analyzers / `-warnaserror` for backend). |
| **Local** | Your machine: PostgreSQL in Docker (`infra/`), backend and frontend run by you. |
| **Staging** | Shared test environment: API + DB on Railway, frontend on Vercel. Auto-deployed from `main`. |
| **Production (prod)** | What real users use. Deployed when a milestone is released. |
| **Environment variable (env var)** | Config passed to the app at runtime (DB URL, API URL). Names are documented; values are never committed. |
| **Secret** | A sensitive env var (passwords, keys). Stored only in Railway/Vercel/GitHub settings. |
| **Migration** | A versioned DB schema change (EF Core). Committed with the code that needs it. |

---

## 4. The workflow, step by step

```
 Spec (docs/specs)          GitHub                       Code
 ─────────────────          ──────                       ────
 1. Write spec ──PR──▶ 2. Create issues ──▶ 3. Ready ──▶ 4. Branch ──▶ 5. PR ──▶ 6. Review ──▶ 7. Merge ──▶ Done
                         (Backlog)                    (In Progress)       (In Review)            (staging)
```

### Step 1: Spec (features only)
1. Copy `docs/specs/_template.md` to `docs/specs/SPEC-NNN-<feature>.md`, using the next free number.
2. Fill in the goal, user flow, rules and edge cases, acceptance criteria, and out of scope. Status: `Draft`.
3. Open a PR titled `docs: spec for <feature>`. The team comments, you adjust, and it merges with Status `Ready`.

Skip this step for bugs and tasks.

### Step 2: Create issues (Backlog)
1. **New issue → Feature**. Link the spec and write the scope this issue covers.
   One spec usually becomes 2–3 issues, e.g. *backend API* and *UI*. Each one must be ≤ 2 days of work.
2. Add labels (area) and the milestone. The issue lands in **Backlog**.
3. Write the issue numbers back into the spec's `Issues:` line.

### Step 3: Plan (Backlog → Ready)
The team moves issues that meet the **Definition of Ready** into **Ready**, in priority order. The top of Ready is what gets picked up next.

### Step 4: Start work (Ready → In Progress)
1. Take the **top** issue in Ready that you can do. Assign yourself and move it to **In Progress**.
2. Create a branch from the latest `main`:
   ```bash
   git switch main && git pull
   git switch -c feat/14-dictation-player
   ```
   Branch name: `<type>/<issue-number>-<short-description>`, where type is `feat`, `fix`, `chore`, `docs`, or `spike`.
3. Read the spec. If it's unclear or wrong, ask in the issue, and fix the spec in your PR.
4. Commit as often as you like. Commit messages on your branch are free-form, since squash merge replaces them.

### Step 5: Open a PR (In Progress → In Review)
1. Push and open a PR. You can open it as a **Draft** early.
2. **PR title** follows Conventional Commits: `feat: dictation player with segment replay`.
3. Fill in the PR template: `Closes #14`, what changed, how to test.
4. Keep it small: aim for **< 400 changed lines**. If it's bigger, split the issue.
5. CI must be green. When it's ready, mark it **Ready for review** and move the issue to **In Review**.

### Step 6: Review
**Reviewer**
- Review within **1 working day**. Unblocking others comes before starting your own new work.
- Check against the spec's **acceptance criteria**, not personal taste. Check out the branch and run it locally to click through.
- Prefix optional comments with `nit:`, so it's clear they don't block the merge.

**Author**
- Reply to every comment (fix it, or explain why not). Don't take review personally; it's about the code.

### Step 7: Merge (→ Done)
1. Update the branch with `main` if needed, and wait for CI to go green.
2. **Squash and merge**. The branch is deleted automatically.
3. The issue closes (via `Closes #14`) and moves to **Done**. `main` auto-deploys to **staging**.
4. Check it quickly on staging.
5. If it was the last issue of a spec, set the spec Status to `Done`.

### Bugs
Found a bug? **New issue → Bug**. If it blocks someone or breaks staging, it goes to the top of Ready. Fix it on a `fix/<number>-...` branch with the same PR flow.

### Spikes
Time-box it (e.g. 2 days). Prototype code can live on the spike branch and is **not** merged. Only the ADR in `docs/decisions/` gets merged, plus any follow-up issues you created.

---

## 5. Definitions of Ready and Done

### Definition of Ready: an issue can move to Ready when
- [ ] It was created from a template, with every required field filled
- [ ] Feature: it links to a spec with Status `Ready`
- [ ] It's small enough for ≤ 2 days of work (Size XS–L, not XL)
- [ ] It has a milestone, labels, Priority and Size
- [ ] Anything it depends on is done, or the dependency is noted in the issue

### Definition of Done: an issue is Done when
- [ ] Every acceptance criterion in its scope is met
- [ ] The PR is approved, CI is green, and the PR is squash-merged into `main`
- [ ] Tests are added for logic (answer checking, caption splitting, auth, etc.)
- [ ] New UI text exists in both `en` and `vi`
- [ ] DB changes are in a migration
- [ ] Docs are updated if behavior, architecture, conventions, or commands changed
- [ ] It works on staging

---

## 6. Using AI tools (Claude Code, Copilot)

- `CLAUDE.md` is the agent's onboarding doc. Copilot reads the same content through `AGENTS.md`. Keep it short and up to date.
- Point the agent at the **spec and the issue**: e.g. "Implement #14 following `docs/specs/SPEC-003-dictation-practice.md`."
- You own what the agent writes. Read the diff before you push. Review rules are the same for AI-written code.
- If the agent keeps getting a project rule wrong, add that rule to `CLAUDE.md` or the right file in `docs/conventions/`.

---

## 7. Cheat sheet

> **Note:** these commands are for when you'd rather use the GitHub CLI ([`gh`](https://cli.github.com/)) instead of the GitHub website. Each one does the same thing as the matching button on github.com. Run `gh auth login` once before using them.

```bash
# Start an issue
git switch main && git pull
git switch -c feat/14-dictation-player

# Open a PR (fills from the template)
git push -u origin HEAD
gh pr create --draft

# Mark it ready, check CI
gh pr ready
gh pr checks

# See your work / what's open
gh issue list --assignee @me
gh pr list

# Create issues from templates
gh issue create --template "Feature"
gh issue create --template "Bug"
gh issue create --template "Task / Spike"
```

| I want to... | Go to |
|---|---|
| Know what to build next | [Project board](https://github.com/users/thang-nhat-tran/projects/2), top of **Ready** |
| Know what a feature must do | `docs/specs/SPEC-NNN-*.md` |
| Know why we chose something | `docs/decisions/` |
| Know how the system fits together | `docs/system-architecture.md` |
| Know how to name/structure code | `docs/conventions/` (`general.md` + `backend.md` / `frontend.md`) |
| Run the project | `README.md` |
