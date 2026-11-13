# Backend conventions (ASP.NET Core, .NET 10)

Read [general.md](general.md) first. The backend is a modular monolith: read [ADR-001](../decisions/ADR-001-modular-monolith.md) and [system-architecture.md §2](../system-architecture.md#2-backend-modules) too.

## Commands (run in `backend/`)
```bash
dotnet build                                   # build everything
dotnet test                                    # run all tests, including architecture tests
dotnet run --project src/Dictora.Api           # run the API (http://localhost:5281)
dotnet format                                  # format code
scripts/migrations.sh add Lessons AddSegments  # add a migration to one module (see Database)
```
The health check is at `GET /health`, and the OpenAPI document (Development only) is at `/openapi/v1.json`.
The database must be running for `dotnet run` and `dotnet test` (see `README.md`).

## Packages
- Versions are managed centrally in `backend/Directory.Packages.props`. In a `.csproj`, write `<PackageReference Include="X" />` **without** a version.
- To add a package: add a `<PackageVersion>` line to `Directory.Packages.props`, then reference it from the project.

## Module rules
1. Code belongs to exactly **one module**. If you can't tell which one, ask before writing it.
2. **Everything is `internal` by default.** Only the module's entry point (`<X>Module.cs`) and its `.Contracts` project are public.
3. A module may reference **only other modules' `.Contracts`** projects. `Dictora.ArchitectureTests` fails the build otherwise.
4. **Each module owns its tables**: its own `DbContext` and its own PostgreSQL schema. Never query or join another module's tables, and never add a foreign key across schemas. Refer to other modules' data by ID only.
5. Need data from another module? Add a method to that module's contract (e.g. `ILessonsApi.GetSegmentAsync(id)`) and implement it in that module.
6. The host (`Dictora.Api`) only wires modules together. No business logic there.

## Inside a module: layers and feature folders
Each module follows **pragmatic clean architecture**: layers are folders, and use cases are feature folders. Why, and where we deliberately bend the rules: [ADR-003](../decisions/ADR-003-clean-architecture-in-modules.md).
```
Dictora.Modules.Lessons/
├── Features/                           # application layer: one folder per use case
│   ├── ImportLesson/
│   │   ├── ImportLessonEndpoint.cs     # maps POST /lessons, calls the handler
│   │   ├── ImportLessonHandler.cs      # the use case: load, call domain, save, map
│   │   └── ImportLessonRequest.cs      # request/response records + validation
│   └── ListMyLessons/
├── Domain/                             # entities, rules, ports (Lesson, Segment, Segmenter, IVideoInfoSource)
├── Infrastructure/                     # LessonsDbContext, YouTube Data API and caption clients
└── LessonsModule.cs                    # Add/Map entry point: wires ports to adapters
```

**What goes where**
- `Domain/`: entities, value objects, business rules, and **ports** (interfaces) for every external system (YouTube, captions, clock via `TimeProvider`), with their result types. Entities and rules go here even if only one feature uses them today.
- `Features/<UseCase>/`: the handler, request/response records, validation and the endpoint. Helpers used by just that feature stay in its folder.
- `Infrastructure/`: the `DbContext`, `IEntityTypeConfiguration` classes, migrations, and the **adapters** that implement the ports (e.g. `YouTubeDataApi/`).

**Dependency rules** (checked in review, not by tests)
1. `Domain/` depends on the .NET base library only: no EF Core, ASP.NET, `HttpClient`, JSON or logging. Pass in what it needs (e.g. `TimeProvider`).
2. `Infrastructure/` and `Features/` depend on `Domain/`, never the other way round.
3. Handlers may use the module's own `DbContext` directly; no repository interfaces. Business rules still belong in `Domain/`, not in handlers or queries.
4. API details stay in the adapter: no vendor JSON shapes, magic strings (`creativeCommon`) or HTTP status codes in `Domain/`.
5. Expected failures are result types (e.g. `VideoInfoResult`); handlers map them to API error codes. Exceptions are for bugs and broken infrastructure.
6. Entities may have a private parameterless constructor and private setters for EF Core. Table mapping goes in `Infrastructure/*Configuration.cs`, never as attributes on entities.
7. Endpoints only map HTTP ↔ handler and results → ProblemDetails. They hold no logic.

## Database
New to this? Read [learning/01](../learning/01-postgresql-schemas.md) and [learning/02](../learning/02-ef-core-dbcontext.md) first.

- **One DbContext per module**, `internal`, in `Infrastructure/`: `IdentityModuleDbContext`, `LessonsDbContext`, `PracticeDbContext`.
- Each context defines `internal const string Schema = "<module>";` and calls `modelBuilder.HasDefaultSchema(Schema)`.
- Register it in the module with `services.AddModuleDbContext<TContext>(TContext.Schema)` from `Dictora.Shared.Database`. That sets up Npgsql, snake_case naming, and the module's own `__EFMigrationsHistory`. Don't call `AddDbContext` directly.
- One connection string for all modules: `ConnectionStrings:Dictora` (env var `ConnectionStrings__Dictora`).
- Configure entities in `IEntityTypeConfiguration<T>` classes next to the context. They're picked up automatically.
- Table and column names are generated in **snake_case** from C# names (`LessonId` → `lesson_id`). Don't hand-write names unless you have to.

### Migrations
```bash
scripts/migrations.sh add    <Module> <Name>   # e.g. add Lessons AddSegments
scripts/migrations.sh remove <Module>          # undo the last migration, if not yet merged/applied
scripts/migrations.sh list   <Module>
scripts/migrations.sh update <Module>          # apply manually (Development already applies on startup)
```
- Migrations live in `Infrastructure/Migrations/` of their module. **Review the generated file** in your PR.
- In Development, the API applies all pending migrations on startup. Staging and production apply them as a deploy step (#7).
- Name migrations after the change (`AddSegments`, not `Update1`). Never edit a migration that's merged to `main`; add a new one.
- If someone else merged a migration to the same module first, `remove` yours, update your branch, and `add` it again.

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
- Host-level and HTTP tests go in `Dictora.Api.Tests`, using `WebApplicationFactory`. Use the `Testing` environment unless the test needs a database.
- Tests that need a real database use **Testcontainers** (`Testcontainers.PostgreSql`, image `postgres:18`), so Docker must be running. Never point tests at your local dev database.

## Adding a new module
1. Create `src/Modules/<X>/Dictora.Modules.<X>` and `...<X>.Contracts`, and copy the `.csproj` shape of an existing module.
2. Add both to `Dictora.slnx`, reference the implementation from `Dictora.Api`, and reference both from `Dictora.ArchitectureTests`.
3. Call `Add<X>Module()` and `Map<X>Endpoints()` in `Program.cs`.
4. Add a DbContext with its own schema (see Database), add the module to `scripts/migrations.sh`, and run `scripts/migrations.sh add <X> Initial`.
5. Update the expected module list in `ModuleBoundaryTests.All_modules_are_discovered` and the schema list in `DatabaseMigrationTests`.
6. Write an ADR if it changes the module map, and update `docs/system-architecture.md`.
