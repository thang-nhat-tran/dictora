# General conventions

Rules for the whole codebase. Stack-specific rules are in [backend.md](backend.md) and [frontend.md](frontend.md). When the team agrees on a new rule, add it to the right file in a PR.

## Language
- Code, comments, commits, issues, PRs and docs are in **English**.
- UI text is in **English and Vietnamese**. Every new string needs both (see [frontend.md](frontend.md)).

## Formatting
- Let the tools decide: `dotnet format` for the backend, Prettier for the frontend.
- Don't argue about style in reviews. If a rule matters, add it to the formatter or linter config.

## Naming
- Names say what something is or does.
- No abbreviations except well-known ones: `Id`, `Url`, `Dto`, `Api`.
- Use the domain terms from [system-architecture.md §3](../system-architecture.md#3-domain-terms) (Lesson, Segment, Caption…) consistently in code, the API and the UI. Don't invent synonyms like "clip", "sentence" or "chunk".

## API error codes
The backend and frontend share these codes, so the rule lives here:
- Every API error returns a stable, machine-readable `code`, formatted `<module>.<snake_case_reason>`, e.g. `lessons.no_english_captions`, `identity.email_taken`.
- The frontend maps each `code` to an en/vi message. It never shows the raw server message to users.
- Codes are part of the API contract. Don't rename one without updating the frontend in the same PR.

## Secrets and config
- Never commit secrets. `.env` files are git-ignored; commit an `.env.example` with names only.
- Exception: the **local-only** database credentials (`dictora`/`dictora`) in `infra/docker-compose.yml` and `appsettings.Development.json`. They only work against your own Docker container. Real credentials never go there.
- Document every config variable (name and purpose) in [system-architecture.md §5](../system-architecture.md#5-configuration).
