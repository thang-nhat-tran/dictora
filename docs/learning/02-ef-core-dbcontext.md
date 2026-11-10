# 02: EF Core DbContext

> **Goal:** understand what a `DbContext` is, how it reads and saves data, how migrations work, and why Dictora has one DbContext per module.
> **Before this:** [01: PostgreSQL schemas](01-postgresql-schemas.md).
> **Time:** about 45 minutes.

---

## 1. Why it matters

Every backend feature that stores data (users, lessons, segments, attempts) goes through EF Core. If you understand the `DbContext`, you understand how C# objects become rows in PostgreSQL. You'll also avoid the most common bugs: data that never gets saved, slow queries, and contexts shared across threads.

---

## 2. The big picture

**EF Core** (Entity Framework Core) is an **ORM**, an Object-Relational Mapper. You work with C# classes and LINQ; EF Core translates that to SQL.

```
Your code (C# objects, LINQ)
      │
      ▼
  DbContext  ── knows the model (which classes map to which tables)
      │         tracks changes to objects you loaded
      │         turns LINQ into SQL, and changes into INSERT/UPDATE/DELETE
      ▼
 Npgsql provider  ── the PostgreSQL-specific part
      │
      ▼
  PostgreSQL
```

| Term | Meaning |
|------|---------|
| **Entity** | A C# class that maps to a table, e.g. `Segment` → `lessons.segments`. |
| **`DbContext`** | Your session with the database. You query, add, change and save entities through it. |
| **`DbSet<T>`** | A property on the DbContext representing one table, e.g. `DbSet<Segment> Segments`. |
| **Model** | EF Core's map of entities ↔ tables, columns, keys and relations. Built from conventions + your configuration. |
| **Change tracker** | The part of the DbContext that remembers which loaded entities you changed. |
| **Migration** | A versioned C# file describing a schema change (create a table, add a column). |
| **Provider** | The database-specific plugin. For us: `Npgsql.EntityFrameworkCore.PostgreSQL`. |

---

## 3. A DbContext, line by line

How Dictora's Lessons module will look (simplified):

```csharp
// Domain/Segment.cs: an entity, just a C# class
internal sealed class Segment
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public int Index { get; set; }
    public string Text { get; set; } = "";
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}

// Infrastructure/LessonsDbContext.cs
internal sealed class LessonsDbContext(DbContextOptions<LessonsDbContext> options)
    : DbContext(options)                                   // ① inherits EF Core's DbContext
{
    internal const string Schema = "lessons";

    public DbSet<Lesson> Lessons => Set<Lesson>();         // ② one DbSet per table
    public DbSet<Segment> Segments => Set<Segment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);             // ③ every table goes into the lessons schema
        modelBuilder.ApplyConfigurationsFromAssembly(      // ④ load per-entity configuration classes
            typeof(LessonsDbContext).Assembly);
    }
}
```

1. **Inherits `DbContext`**: this gives you querying, change tracking and `SaveChanges`.
2. **`DbSet<T>`**: each one is a table you can query (`db.Segments.Where(...)`).
3. **`HasDefaultSchema("lessons")`**: this is where lesson 01 meets EF Core. Every table of this context is created in, and read from, the `lessons` schema.
4. **Configuration**: column lengths, indexes and keys live in small `IEntityTypeConfiguration<T>` classes next to the context. That keeps `OnModelCreating` short.

The class is **`internal`**. No other module can even see it. That's the compiler enforcing ADR-001.

### Registering it (in `LessonsModule.cs`)

```csharp
services.AddModuleDbContext<LessonsDbContext>(LessonsDbContext.Schema);
```

That one line comes from `Dictora.Shared.Database`, so all modules are set up the same way. Inside, it does roughly this:

```csharp
services.AddDbContext<LessonsDbContext>(options =>
    options.UseNpgsql(
        configuration.GetConnectionString("Dictora"),       // same connection string for all modules
        npgsql => npgsql.MigrationsHistoryTable(
            "__EFMigrationsHistory", "lessons"))            // keep migration history in our own schema
    .UseSnakeCaseNamingConvention());                       // LessonId → lesson_id
```

`AddDbContext` registers the context with dependency injection as **scoped**, which means **one instance per HTTP request**. Handlers just ask for it in their constructor:

```csharp
internal sealed class ImportLessonHandler(LessonsDbContext db) { ... }
```

---

## 4. Reading data

LINQ is translated into SQL when you **execute** the query (`ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, …):

```csharp
var segments = await db.Segments
    .Where(s => s.LessonId == lessonId)
    .OrderBy(s => s.Index)
    .AsNoTracking()                 // read-only: skip change tracking (faster)
    .ToListAsync(ct);
```

roughly becomes:

```sql
SELECT s.id, s.lesson_id, s.index, s.text, s.start, s."end"
FROM lessons.segments AS s
WHERE s.lesson_id = @lessonId
ORDER BY s.index;
```

Rules of thumb:
- Use **`AsNoTracking()`** for read-only queries (lists, details, API responses).
- **Select only what you need** with `.Select(s => new SegmentDto(...))`, so EF doesn't load whole entities.
- Always use the **`Async`** methods and pass the `CancellationToken`.

---

## 5. Saving data: change tracking + `SaveChanges`

The DbContext **remembers** the entities it loaded or that you added. Nothing goes to the database until you call **`SaveChangesAsync()`**:

```csharp
// 1. Add new rows
db.Lessons.Add(lesson);
db.Segments.AddRange(segments);

// 2. Change an existing row: just modify the tracked object
var existing = await db.Lessons.FirstAsync(l => l.Id == id, ct);
existing.Title = "New title";

// 3. Delete a row
db.Segments.Remove(oldSegment);

// 4. Send everything in ONE transaction
await db.SaveChangesAsync(ct);
```

`SaveChangesAsync` looks at every tracked entity, works out what changed, and runs the `INSERT`/`UPDATE`/`DELETE` statements **in a single transaction**. Either all of them succeed, or none do.

The #1 beginner bug: changing objects and **forgetting `SaveChangesAsync`**. Nothing gets saved, and there's no error.

---

## 6. Lifetime: short and per request

| ✅ Do | ❌ Don't |
|------|---------|
| Let DI give you a context per request (scoped) | Register it as a singleton or store it in a static field |
| Use it for one unit of work, then let it go | Keep one context alive for minutes or hours (the change tracker grows, and data goes stale) |
| `await` each call before the next one | Run two queries **in parallel** on the same context (a DbContext is **not thread-safe**) |

If you need a context outside a request (e.g. a background job), inject `IDbContextFactory<T>` and create a short-lived one.

---

## 7. Migrations: versioning the database schema

The **model** (your entities + configuration) and the **real database** must match. Migrations keep them in sync.

```
change entity/config ──▶ dotnet ef migrations add <Name> ──▶ C# migration file (review it!)
                                                                  │
                                         dotnet ef database update ▼
                                              PostgreSQL schema changes + a row in __EFMigrationsHistory
```

1. **`migrations add`** compares your model with the last **snapshot** (`<Context>ModelSnapshot.cs`) and generates a migration with `Up()` (apply) and `Down()` (revert).
2. **Review the generated file.** It's code and gets reviewed in the PR like any other code.
3. **`database update`** runs the migrations that haven't been applied yet, and records each one in **`__EFMigrationsHistory`**. That table is how EF knows what has already run.

Because Dictora has **one context per module**, you always say **which** context:

```bash
# in backend/: the script picks the right context and project for you
scripts/migrations.sh add Lessons AddSegments

# ...which runs:
dotnet ef migrations add AddSegments \
  --context LessonsDbContext \
  --project src/Modules/Lessons/Dictora.Modules.Lessons \
  --startup-project src/Dictora.Api \
  --output-dir Infrastructure/Migrations
```

You rarely need `database update` locally: in Development, the API applies pending migrations on startup.

That's why each module has its own `__EFMigrationsHistory` (`MigrationsHistoryTable(..., "lessons")` above). Lessons' history and Practice's history must not mix.

**Migration rules:**
- Never edit a migration that's already merged to `main`. Add a new one instead.
- One migration per logical change, with a descriptive name (`AddSegments`, not `Update1`).
- If two PRs both add migrations to the same module, the second one to merge must re-create its migration on top of the first.

---

## 8. One DbContext per module: why and what it implies

| Single shared `AppDbContext` | **One context per module (Dictora)** |
|------------------------------|--------------------------------------|
| Any code can query any table | A module can only see **its own** tables, because the context is `internal` |
| Navigation properties across everything (`attempt.Segment.Lesson.User`) | **No navigation across modules.** Store the ID (`attempt.SegmentId`) and ask the other module's contract |
| One migration history | One history per module (per schema) |
| One `SaveChanges` can cover all tables | `SaveChanges` is **per module**: one transaction per module |

The last row has a consequence. If one action writes to **two** modules, those are two separate transactions. Design features so each request writes to one module, and let other modules react afterwards (later, through events). If you hit a case where this is hard, raise it in the issue before coding.

---

## 9. Common mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Forgot `SaveChangesAsync` | Data "saved" but gone | Call it once at the end of the unit of work |
| **N+1 queries**: querying inside a loop | Slow pages, hundreds of SQL queries | Load in one query (`Where(x => ids.Contains(x.Id))`, `Include`, or `Select`) |
| Loading full entities for a list | Slow, high memory | `AsNoTracking()` + `Select` into a DTO |
| Parallel queries on one context | `InvalidOperationException: A second operation was started…` | `await` each one, or use separate contexts |
| Querying another module's tables | Breaks ADR-001; the architecture test may catch it | Use that module's `.Contracts` interface |
| Editing an applied migration | Databases out of sync between devs and environments | Add a new migration |

**Tip:** to see the SQL that EF generates while learning, enable logging in Development: set `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` under `Logging:LogLevel` in `appsettings.Development.json`.

---

## 10. Try it

1. Start the database and API (see `README.md` → Getting started).
2. Connect with `docker exec -it dictora-postgres-1 psql -U dictora -d dictora` and run `\dn`. You should see `identity`, `lessons` and `practice`.
3. Run `SELECT * FROM lessons."__EFMigrationsHistory";` and match the rows to the migration files in `src/Modules/Lessons/Dictora.Modules.Lessons/Infrastructure/Migrations/`.
4. On a throwaway branch: create a small entity in a module (with a `DbSet` on its context), run `scripts/migrations.sh add <Module> TryIt`, and **read** the generated `Up()`/`Down()`. Then undo it with `scripts/migrations.sh remove <Module>`.
5. Turn on the SQL logging from the tip above, call an endpoint, and find the SQL in the console.

---

## 11. Self-check

<details><summary>1. You changed <code>lesson.Title</code> but the database didn't change. What did you forget?</summary>

`await db.SaveChangesAsync()`. Changes stay in the change tracker until you save.
</details>

<details><summary>2. Why is the DbContext registered as scoped and not singleton?</summary>

A DbContext isn't thread-safe and its change tracker grows over time. One short-lived instance per request keeps it safe and fresh.
</details>

<details><summary>3. What does <code>HasDefaultSchema("lessons")</code> do?</summary>

It puts all of that context's tables in the `lessons` PostgreSQL schema (lesson 01), so EF creates and queries `lessons.segments` instead of `public.segments`.
</details>

<details><summary>4. Why does each module have its own <code>__EFMigrationsHistory</code>?</summary>

Each DbContext has its own migrations. If they shared one history table, EF would mix up which migrations belong to which module.
</details>

<details><summary>5. Practice wants <code>attempt.Segment.Text</code>. Why is there no navigation property, and what do you do instead?</summary>

`Segment` belongs to Lessons' DbContext, which Practice can't see. Practice stores `SegmentId` and gets the text through `ILessonsApi`.
</details>

<details><summary>6. When do you use <code>AsNoTracking()</code>?</summary>

For read-only queries, where you won't modify and save the results. It skips change tracking, so it's faster and uses less memory.
</details>

---

## 12. Further reading
- **DbContext lifetime, configuration and initialization**: https://learn.microsoft.com/ef/core/dbcontext-configuration/
- **Change tracking**: https://learn.microsoft.com/ef/core/change-tracking/
- **Migrations overview**: https://learn.microsoft.com/ef/core/managing-schemas/migrations/
- **Using a separate migrations project / multiple contexts**: https://learn.microsoft.com/ef/core/managing-schemas/migrations/projects
- **Efficient querying** (N+1, tracking, projections): https://learn.microsoft.com/ef/core/performance/efficient-querying
- **Npgsql EF Core provider** (PostgreSQL specifics): https://www.npgsql.org/efcore/

**Previous:** [01: PostgreSQL schemas](01-postgresql-schemas.md)
