# Backend conventions (ASP.NET Core, .NET 10)

Read [general.md](general.md) first. The backend is a modular monolith: read [ADR-001](../decisions/ADR-001-modular-monolith.md) and [system-architecture.md §2](../system-architecture.md#2-backend-modules) too.

## Commands (run in `backend/`)
```bash
dotnet build                                   # build everything
dotnet test                                    # run all tests, including architecture tests
dotnet run --project src/Dictora.Api           # run the API (http://localhost:5281)
dotnet format                                  # format code
```
The health check is at `GET /health`, and the OpenAPI document (Development only) is at `/openapi/v1.json`.

## Module rules
1. Code belongs to exactly **one module**. If you can't tell which one, ask before writing it.
2. **Everything is `internal` by default.** Only the module's entry point (`<X>Module.cs`) and its `.Contracts` project are public.
3. A module may reference **only other modules' `.Contracts`** projects. `Dictora.ArchitectureTests` fails the build otherwise.
4. **Each module owns its tables**: its own `DbContext` and its own PostgreSQL schema. Never query or join another module's tables, and never add a foreign key across schemas. Refer to other modules' data by ID only.
5. Need data from another module? Add a method to that module's contract (e.g. `ILessonsApi.GetSegmentAsync(id)`) and implement it in that module.
6. The host (`Dictora.Api`) only wires modules together. No business logic there.

## Inside a module: feature folders
Organize by **use case**, not by technical layer:
```
Dictora.Modules.Lessons/
├── Features/
│   ├── ImportLesson/
│   │   ├── ImportLessonEndpoint.cs     # maps POST /lessons, calls the handler
│   │   ├── ImportLessonHandler.cs      # the use case logic
│   │   └── ImportLessonRequest.cs      # request/response records + validation
│   └── ListMyLessons/
├── Domain/                             # entities and rules (Lesson, Segment, Segmenter)
├── Infrastructure/                     # LessonsDbContext, YouTube caption client
└── LessonsModule.cs                    # Add/Map entry point
```
- One feature folder per endpoint or use case. Files used by only one feature stay in that folder.
- Move code to `Domain/` only when two or more features need it.

## Contracts project
- It contains only **interfaces, records (DTOs) and event records**. No logic, and no EF Core or ASP.NET references.
- Name the interface after the module: `ILessonsApi`, `IIdentityApi`.
- DTOs are immutable `record`s, suffixed with `Dto` (e.g. `SegmentDto`).

## API endpoints
- Use **Minimal APIs**, mapped on the module's route group (`/auth`, `/lessons`, `/practice`).
- Routes are lowercase, plural nouns, kebab-case: `/lessons/{lessonId}/segments`.
- Errors return **ProblemDetails** (RFC 7807) with a stable error `code`. See [general.md › API error codes](general.md#api-error-codes).

## Naming
| Thing | Convention | Example |
|-------|------------|---------|
| Namespace | matches the folder | `Dictora.Modules.Lessons.Features.ImportLesson` |
| Classes, methods, properties | PascalCase | `ImportLessonHandler`, `GetSegmentAsync` |
| Locals, parameters | camelCase | `lessonId` |
| Private fields | `_camelCase` | `_dbContext` |
| Async methods | `Async` suffix | `ImportAsync` |
| Interfaces | `I` prefix | `ICaptionSource` |
| DB schema / tables | snake_case | `lessons.segments` |

## Tests
- Every module gets a `tests/Dictora.Modules.<X>.Tests` project once it has logic. Use `InternalsVisibleTo` to test internals.
- **Must have tests:** domain rules and pure logic (segmentation, answer normalization/diff, URL parsing).
- Test names describe behavior: `Splits_auto_captions_on_long_pauses`.
- Host-level and HTTP tests go in `Dictora.Api.Tests`, using `WebApplicationFactory`.

## Adding a new module
1. Create `src/Modules/<X>/Dictora.Modules.<X>` and `...<X>.Contracts`, and copy the `.csproj` shape of an existing module.
2. Add both to `Dictora.slnx`, reference the implementation from `Dictora.Api`, and reference both from `Dictora.ArchitectureTests`.
3. Call `Add<X>Module()` and `Map<X>Endpoints()` in `Program.cs`.
4. Update the expected module list in `ModuleBoundaryTests.All_modules_are_discovered`.
5. Write an ADR if it changes the module map, and update `docs/system-architecture.md`.
