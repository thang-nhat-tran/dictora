# CLAUDE.md

Dictora is a dictation website: paste a YouTube URL and practice English listening sentence by sentence.

This file is the onboarding doc for AI agents. Keep it short. It points to the docs and doesn't copy them, because each fact lives in one place. Process: [docs/workflow.md](docs/workflow.md).

## Stack
- **Backend:** ASP.NET Core (.NET 10, pinned in `backend/global.json`), Minimal APIs, modular monolith ([ADR-001](docs/decisions/ADR-001-modular-monolith.md))
- **Database:** PostgreSQL 18, EF Core. Each module has its own DbContext and schema.
- **Frontend:** React + Vite in `frontend/` (not scaffolded yet, see #4)
- **Hosting:** Railway (API + DB) and Vercel (frontend)

## Layout
```
backend/src/Dictora.Api        Host: only wires the modules together
backend/src/Modules/<X>/       Identity, Lessons, Practice (+ .Contracts each)
backend/src/Shared/            Shared infrastructure (e.g. AddModuleDbContext)
backend/tests/                 Dictora.Api.Tests, Dictora.ArchitectureTests
backend/scripts/migrations.sh  Per-module EF Core migrations
infra/docker-compose.yml       Local PostgreSQL
docs/                          workflow, architecture, conventions, specs, decisions, learning
```

## Commands
```bash
docker compose -f infra/docker-compose.yml up -d --wait   # start the local DB (repo root)

# in backend/
dotnet tool restore                          # first time only (dotnet-ef)
dotnet build -warnaserror                    # CI builds with warnings as errors
dotnet test                                  # needs Docker (Testcontainers)
dotnet user-secrets set "YouTubeDataApi:ApiKey" "<key>" --project src/Dictora.Api   # local secrets (README step 3)
dotnet run --project src/Dictora.Api         # http://localhost:5281/health
dotnet format
scripts/migrations.sh add <Module> <Name>    # e.g. add Lessons AddSegments
```
CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs restore, build `-warnaserror` and test for the backend, and `npm ci`, lint, build and test for the frontend.

## Where to look
| Need | Read |
|------|------|
| How work flows: issues, branches, PRs, DoR/DoD | [docs/workflow.md](docs/workflow.md) |
| How the system fits together, domain terms, config | [docs/system-architecture.md](docs/system-architecture.md) |
| Code rules | [docs/conventions/general.md](docs/conventions/general.md), plus [backend.md](docs/conventions/backend.md) or [frontend.md](docs/conventions/frontend.md) |
| What a feature must do (acceptance criteria) | `docs/specs/SPEC-NNN-*.md` |
| Why a choice was made | `docs/decisions/ADR-NNN-*.md` |

## Starting an issue
1. Read the issue (`gh issue view <n>`). For a feature, read its linked spec. **Acceptance criteria live in the spec**, not in the issue. If the spec is unclear, ask in the issue rather than guessing.
2. Read the conventions for the area you're working in, and any related ADRs.
3. Branch from the latest `main`: `git switch main && git pull && git switch -c <type>/<issue#>-<short-desc>`. Type is `feat`, `fix`, `chore`, `docs` or `spike`.
4. **Spike:** time-boxed. The output is an ADR. Prototype code stays on the branch and is not merged.
5. Leave assigning and moving the board card to the user unless they ask you to do it.

## Key rules
- Code, comments, commits and docs are in English. **Every UI string needs both `en` and `vi`.**
- Use the domain terms from [system-architecture.md §3](docs/system-architecture.md#3-domain-terms) (Lesson, Segment, Caption…). Don't invent synonyms.
- Backend module boundaries: everything is `internal` by default, other modules are reached only through their `.Contracts`, and modules never touch each other's tables. `Dictora.ArchitectureTests` enforces this.
- Inside a module: `Domain/` depends on nothing, external systems sit behind ports in `Domain/` with adapters in `Infrastructure/`, and handlers may use the module's `DbContext` directly ([ADR-003](docs/decisions/ADR-003-clean-architecture-in-modules.md)).
- Package versions live only in `backend/Directory.Packages.props`.
- DB changes go in a migration made with `scripts/migrations.sh`. Never edit a migration that's merged to `main`.
- API errors are ProblemDetails with a stable `code` shaped `<module>.<snake_case_reason>`.
- Add tests for logic. Update docs in the **same PR** when behavior, architecture, conventions or commands change, including this file.
- Never commit secrets.

## Commits and PRs
- Commit or push only when the user asks.
- PR title follows Conventional Commits (`feat: ...`, `fix: ...`, `chore: ...`). It becomes the squash-merge commit.
- Fill in [.github/pull_request_template.md](.github/pull_request_template.md) with `Closes #<n>`. Aim for fewer than 400 changed lines.
