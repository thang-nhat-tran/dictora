# ADR-001: Modular monolith for the backend

- **Status:** Accepted
- **Date:** 2026-11-09
- **Issue:** #3
- **Related:** SPEC-001, SPEC-002, SPEC-003

## Context
Dictora's backend is an ASP.NET Core API built by a team of 2–3 developers. The product has a few clearly separate capabilities: accounts, importing YouTube lessons, and dictation practice. We want:
- one thing to build, deploy and debug (we host on Railway with a single PostgreSQL database);
- clear ownership, so developers can work in parallel without stepping on each other;
- room to grow without the codebase turning into a tangle.

## Options
1. **Classic layered monolith** (one project, Controllers/Services/Repositories folders)
   - Pros: simplest start.
   - Cons: no boundaries between capabilities. Any code can call or query anything, and it becomes hard to change over time.
2. **Microservices**
   - Pros: independent deploy and scaling.
   - Cons: far too much operational overhead for 2–3 devs (several deploys, network calls, distributed data, harder local dev). We have no scaling need that justifies it.
3. **Modular monolith**: one deployable API split into modules with enforced boundaries
   - Pros: one deploy and one database, but with clear ownership. Boundaries are checked by the compiler and by tests. A module can be extracted into a service later if it's ever needed.
   - Cons: some upfront structure (2 projects per module), and the discipline to go through contracts instead of reaching in.

## Decision
We use a **modular monolith** with three modules that map to our specs:

| Module | Owns | Spec |
|--------|------|------|
| **Identity** | Users, registration, login, sessions | SPEC-001 |
| **Lessons** | YouTube URL import, captions, segments, "My lessons" | SPEC-002 |
| **Practice** | Answer checking, attempts, scoring, progress | SPEC-003 |

Rules:
1. **Each module has 2 projects**: `Dictora.Modules.<X>` (implementation, `internal` by default) and `Dictora.Modules.<X>.Contracts` (the public surface: interfaces, DTOs, integration events).
2. **A module may reference only other modules' `.Contracts`**, never their implementation. Contracts never reference implementations.
3. **Each module owns its data**: its own `DbContext` and its own PostgreSQL schema (`identity`, `lessons`, `practice`). No joins and no foreign keys across schemas; modules refer to each other's data by ID.
4. **Communication**: synchronous calls through a contract interface (e.g. Practice → `ILessonsApi`). In-process integration events are added only when there's a real need for them.
5. **The host (`Dictora.Api`) only wires modules together**, with `Add<X>Module()` / `Map<X>Endpoints()`. It holds no business logic.
6. **Rules 1–2 are enforced** by `tests/Dictora.ArchitectureTests`, which runs in CI.
7. Inside a module, code is organized by **feature folder** (vertical slice), not by technical layer.

## Consequences
- ✅ One deploy, one database, simple local development.
- ✅ Developers can own a module each. Boundary violations fail CI instead of slipping into review.
- ✅ Specs map 1:1 to modules, so it's obvious where new code goes.
- ⚠️ Cross-module reads go through contracts, which is slightly more code than a direct query. That's a deliberate trade-off.
- ⚠️ No shared-kernel project yet. Add `Dictora.Shared` only when a second module genuinely needs the same non-business code (e.g. `ICurrentUser`), and keep it free of business logic.
- ⚠️ No message broker, outbox or CQRS framework. Revisit only if we hit a concrete problem.
- Follow-up: database schemas per module (#5), auth via Identity (#9).
