# ADR-003: Pragmatic clean architecture inside each module

- **Status:** Proposed
- **Date:** 2026-11-14
- **Issue:** #32
- **Related:** ADR-001, ADR-002

## Context
[ADR-001](ADR-001-modular-monolith.md) sets the boundaries **between** modules: each module has its own projects, schema and contract, and tests enforce that. It says nothing about how code is layered **inside** a module, except that it is organized by feature folder (rule 7). `docs/conventions/backend.md` shows `Features/`, `Domain/` and `Infrastructure/` folders, but doesn't say which may depend on which.

The question came up in #23. Its licence check is an interface (`IVideoInfoSource`) and result types in `Domain/`, an HTTP adapter (`YouTubeDataApiVideoInfoSource`) in `Infrastructure/`, and a `Video` entity built from the result. Is that clean architecture, and where are we allowed to bend the rules?

What matters:
- **Maintainable and testable.** Business rules (licence check, segmentation, answer diff) should be easy to find and to test without a database or network.
- **Swappable external systems.** ADR-002 already expects caption sources to be replaced (YoutubeExplode → own client or yt-dlp) without touching the import flow.
- **A team of 2–3 developers.** Every extra layer, project or interface is code someone has to write, read and keep in sync.
- **Our stack.** EF Core (already a unit of work and repository), Minimal APIs, feature folders (ADR-001 rule 7, which this ADR refines and doesn't reverse).
- **Enforcement is by convention and review only.** No extra projects per layer, and no new architecture tests (decided in #32).

### Clean architecture in one paragraph
Code is split into rings. The **domain** (entities and business rules) is at the center. Around it is the **application** layer (use cases), and outside are the **adapters** (database, HTTP clients, web endpoints). The one rule that matters is the **dependency rule**: source code dependencies point **inwards only**. The domain knows nothing about EF Core, HTTP or ASP.NET. When an inner layer needs something from outside (e.g. "check this video's licence"), it defines an interface (a **port**), and the outer layer implements it (an **adapter**).

## Options
1. **Strict clean architecture**: a project per layer (`Domain`, `Application`, `Infrastructure`, `Api`) in every module, plus repository interfaces for all data access.
   - Pros: the compiler enforces the dependency rule; the domain and use cases are fully persistence-ignorant; everything can be unit tested with fakes.
   - Cons: 4–5 projects per module (12–15 for 3 modules, plus Contracts); a repository and mapping for every entity; a lot of ceremony for small use cases; EF Core is hidden behind a weaker abstraction of itself.
2. **Pragmatic clean architecture**: the layers are **folders** in the module's one project. The dependency rule is strict for the domain and relaxed in a few named places (below).
   - Pros: keeps what pays off (a pure domain, ports for external systems) and skips what costs more than it gives for us; fits feature folders; no new projects.
   - Cons: only review enforces it, so drift is possible; handlers depend on EF Core.
3. **Vertical slices only**: no `Domain/` layer. Each feature folder holds all its logic, data access and HTTP calls.
   - Pros: least code and indirection; each use case reads top to bottom in one place.
   - Cons: rules shared by features (segmentation, licence check) get duplicated or end up in random helpers; external systems are called directly, so they're hard to swap (conflicts with ADR-002) and hard to test.

## Decision
We use **option 2, pragmatic clean architecture**.

### Layers inside a module
| Folder | Layer | Contains | May depend on |
|--------|-------|----------|---------------|
| `Domain/` | Domain | Entities, value objects, domain rules and services, **ports** for external systems, result types | The .NET base library only |
| `Features/<UseCase>/` | Application (+ its endpoint) | Handler (the use case), request/response records, validation, the Minimal API endpoint | `Domain/`, the module's `DbContext`, other modules' `.Contracts` |
| `Infrastructure/` | Adapters | `DbContext`, EF configurations, migrations, HTTP clients and other adapters that implement the ports | `Domain/` |
| `<X>Module.cs` | Composition root | DI registration (ports → adapters), endpoint mapping | Everything in the module |
| `.Contracts` project | Module's public API ([ADR-001](ADR-001-modular-monolith.md)) | Interfaces and DTOs for other modules | Nothing (no domain types leave the module) |

```
Features/<UseCase>/ ──▶ Domain/ ◀── Infrastructure/
        │                                  ▲
        └──── only the module's DbContext ─┘   (rule 3)

<X>Module.cs wires ports to adapters and maps the endpoints.
```
### Obey or break: rule by rule
Each row is a clean architecture rule. *Obey* and *Break* list what each choice gives and costs **for Dictora**. The last column is our choice and when to revisit it.

| # | Rule | Obey: gain / cost | Break: gain / cost | Our choice |
|---|------|-------------------|--------------------|------------|
| 1 | **The domain depends on nothing** (no EF Core, ASP.NET, `HttpClient`, JSON, logging) | ✅ Rules are unit-testable in milliseconds and readable without framework noise. ⚠️ Cost: dependencies such as the clock must be passed in (`TimeProvider`). | ✅ Slightly less code. ❌ Framework details leak into rules, and changing a library means touching business code. | **Obey.** This is the core of the architecture. Never revisit. |
| 2 | **External systems sit behind ports.** The interface is in `Domain/`, the adapter in `Infrastructure/` (YouTube Data API, captions, proxy, clock) | ✅ Swapping a source is one new adapter (ADR-002 relies on this), and use cases can be tested with fakes. ⚠️ Cost: an interface and result types per system. | ✅ Fewer files. ❌ Use cases are tied to a library and a network, so they're hard to test and to swap. | **Obey** for every system outside our process and database. |
| 3 | **Data access goes through repository interfaces**, never the `DbContext` directly | ✅ Handlers are persistence-ignorant and testable with in-memory fakes; the database could be swapped. ⚠️ Cost: an interface, an implementation and mapping per entity, and query flexibility is lost (projections, `Include`, paging) or re-invented. | ✅ EF Core already *is* a repository (`DbSet`) and a unit of work (`SaveChanges`). LINQ projections straight to response records; less code. ❌ Handlers depend on EF Core; handler tests need a real database (Testcontainers, already in use); query logic can be duplicated. | **Break.** Handlers use the module's own `DbContext` directly. Revisit if the same query appears in several handlers (then add a query method or a small read service), or if a business rule shows up inside a query. |
| 4 | **Business rules live in the domain**, not in handlers or endpoints | ✅ One place to find and test each rule; handlers stay thin. ⚠️ Cost: you have to think about where a rule belongs. | ✅ Quicker to write at first. ❌ Rules get scattered and duplicated across features (an "anemic" domain). | **Obey.** Handlers orchestrate: load, call domain methods, save, map. |
| 5 | **Entities are persistence-ignorant** | ✅ Entities look like plain C# objects. ⚠️ Cost: EF Core needs a way to create them, so full ignorance would need a separate persistence model and mapping. | ✅ EF Core materializes entities directly. ❌ Small framework needs in the domain. | **Small break.** A private parameterless constructor and private setters for EF Core are allowed. The mapping (table, keys, lengths) lives in `Infrastructure/*Configuration.cs`, never as attributes on entities. |
| 6 | **A separate presentation layer** (endpoints apart from use cases) | ✅ HTTP concerns fully separate from use cases. ⚠️ Cost: another folder or project, and every feature split in two places. | ✅ Each use case is one folder: endpoint, handler, request (ADR-001 rule 7). ❌ HTTP code sits next to use case code. | **Break.** The endpoint stays in its feature folder, but **only** maps HTTP ↔ handler and result → ProblemDetails. It holds no logic. |
| 7 | **The compiler enforces the layers** (one project per layer) | ✅ Violations don't compile. ⚠️ Cost: 4–5 projects per module, slower builds, more `public` types across projects. | ✅ One project per module, simple. ❌ A wrong `using` in `Domain/` compiles; only review catches it. | **Break.** Folders and `internal` in one assembly, checked in review. Revisit with an architecture test if review keeps finding violations. |
| 8 | **The domain speaks the business language, not the vendor's** | ✅ The domain survives vendor changes. ⚠️ Cost: mapping vendor data to our own types. | ❌ API shapes, magic strings and error codes spread through the code. | **Obey, with a nuance.** A product fact may appear in domain names: Dictora is YouTube-only (SPEC-002), so `YouTubeVideoInfo` and a YouTube video ID are domain terms. API details never cross the port: no `"creativeCommon"`, `ytAgeRestricted`, JSON shapes, ISO 8601 strings or HTTP status codes in `Domain/`. |
| 9 | **Expected failures are values, not exceptions** | ✅ Each outcome is explicit in the signature (`VideoInfoResult`), and the handler maps it to an error code (e.g. `lessons.not_cc_by`). ⚠️ Cost: a result type per port. | ✅ Less code. ❌ Hidden control flow, and business outcomes mixed with real crashes. | **Obey** for expected business outcomes. Exceptions stay for bugs and broken infrastructure. |

### Example: the licence check in #23
- `Domain/IVideoInfoSource.cs`: the port (rule 2), with `Domain/YouTubeVideoInfo.cs` and `VideoInfoResult` as plain C# records (rules 1, 8 and 9).
- `Infrastructure/YouTubeDataApi/YouTubeDataApiVideoInfoSource.cs`: the adapter. It alone knows the URL, the key header, the JSON shape, `creativeCommon` and the quota errors (rules 2 and 8).
- `Domain/Video.cs`: the entity, with a private constructor for EF Core (rule 5). Its table mapping is in `Infrastructure/VideoConfiguration.cs`.
- The import handler (#11) will call the port, create a `Video` and save it through `LessonsDbContext` directly (rule 3).

## Consequences
- ✅ Business rules are pure and fast to test; external systems are swappable behind ports, as ADR-002 needs.
- ✅ It's clear where new code goes, and a use case still reads in one feature folder.
- ✅ No new projects and no repository boilerplate.
- ⚠️ **Only review enforces it.** A `using Microsoft.EntityFrameworkCore;` in `Domain/` still compiles. Reviewers check the dependency rule; if violations keep appearing, add an architecture test (rule 7).
- ⚠️ **Handlers depend on EF Core** (rule 3). Handler tests need a database (Testcontainers); keep the logic in the domain so most tests don't.
- ⚠️ **The folders feel heavy in small modules.** A module with almost no rules may have an almost empty `Domain/`. That's fine; don't invent abstractions to fill it.
- Code placement rules are in [backend.md › Inside a module](../conventions/backend.md#inside-a-module-layers-and-feature-folders).
