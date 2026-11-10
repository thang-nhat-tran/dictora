# Learning

Lessons on the concepts Dictora is built on. Each one explains a concept with examples from this codebase, then gives you something to try and a short self-check.

These are **not** project rules (those are in `docs/conventions/`) or decisions (those are in `docs/decisions/`). They're here to help you understand those rules and decisions.

## Lessons

| # | Lesson | Read before working on |
|---|--------|------------------------|
| 01 | [PostgreSQL schemas](01-postgresql-schemas.md) | Anything that touches the database (#5 onward) |
| 02 | [EF Core DbContext](02-ef-core-dbcontext.md) | Any backend feature that stores or reads data |
| 03 | [Package and tool versions](03-package-and-tool-versions.md) | Adding or upgrading a NuGet package or a `dotnet` tool |

Related reading: [ADR-001: Modular monolith](../decisions/ADR-001-modular-monolith.md), which explains *why* each module has its own schema and its own DbContext.

## Adding a lesson
- File name: `NN-<topic>.md`, numbered in the order they should be read.
- Structure: **Why it matters** → **Concept** → **In Dictora** → **Try it** → **Self-check** → **Further reading**.
- Keep it practical. Link to official docs for the full details instead of copying them.
